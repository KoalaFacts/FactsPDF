using System.Buffers;
using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class TextLines
{
    internal readonly record struct Glyph(int Scalar, TextStyle Style, PdfFont? Font, double Width1000, int SourceOffset = -1)
    {
        // PDF Tc adds fixed character spacing after each shown glyph, across
        // ASCII and embedded CID TrueType font runs. Layout must count the
        // identical advance before wrapping, centring or underlining.
        public double Width => Style.FontSize * Width1000 / 1000 + Style.LetterSpacing
            + (Scalar == 32 ? Style.WordSpacing : 0);
    }
    // Glyph storage is borrowed until the next MoveNext. Each consumer copies
    // every placed text run before advancing; the iterator reuses its glyph storage.
    internal readonly record struct Line(List<Glyph> Glyphs, double Width, double Height, double Ascent, double Descent,
        double FirstLineIndent = 0);

    internal static void AppendLine(Line line, double x, double baseline, List<PlacedText> target,
        Action<int, int> charge, CancellationToken cancellation, int sourceOffset = -1)
    {
        for (var start = 0; start < line.Glyphs.Count;)
        {
            var first = line.Glyphs[start];
            // A decorated run emits one additional filled-path command; charge
            // before retaining its text, so MaxDisplayCommands remains exact.
            charge(first.Style.Decoration == TextDecoration.None ? 1 : 2, sourceOffset); var width = 0d; var end = start; var utf16Length = 0;
            while (end < line.Glyphs.Count && line.Glyphs[end].Style == first.Style && ReferenceEquals(line.Glyphs[end].Font, first.Font))
            {
                if ((end & 255) == 0) cancellation.ThrowIfCancellationRequested();
                var glyph = line.Glyphs[end++];
                utf16Length = checked(utf16Length + (glyph.Scalar <= 65535 ? 1 : 2));
                width += glyph.Width;
            }
            var text = string.Create(utf16Length,
                (Glyphs: line.Glyphs, Start: start, End: end, Cancellation: cancellation),
                static (destination, state) =>
                {
                    var offset = 0;
                    for (var i = state.Start; i < state.End; i++)
                    {
                        if ((i & 255) == 0) state.Cancellation.ThrowIfCancellationRequested();
                        offset += new Rune(state.Glyphs[i].Scalar).EncodeToUtf16(destination[offset..]);
                    }
                });
            target.Add(new(text, x, baseline, first.Style, first.Font, width));
            x += width; start = end;
        }
    }

    internal static bool HasTextContent(Paragraph paragraph, CancellationToken cancellation)
    {
        // Match Wrap's HTML whitespace and explicit-break rules without
        // resolving glyphs or building any temporary text representation.
        foreach (var run in paragraph.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run.IsBreak) continue;
            foreach (var character in run.Text)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!HtmlTokens.Space(character)) return true;
            }
        }
        return false;
    }

    internal static IEnumerable<Line> Wrap(Paragraph paragraph, double available, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation,
        ListMarker? firstMarker = null)
    {
        Glyph Resolve(int scalar, TextStyle style, int sourceOffset)
        {
            var role = FontRoles.Role(style);
            if (!resolved.TryGetValue((role, scalar), out var value))
            {
                var fonts = FontRoles.Chain(options, role, sourceOffset);
                if (fonts.Count == 0)
                {
                    if (scalar is < 32 or > 126) throw new FactsPdfException("FPDF1301", $"U+{scalar:X4} requires an explicitly supplied font.");
                    value = (null, 600);
                }
                else
                {
                    CheckSimpleScalar(scalar);
                    PdfFont? chosen = null; ushort glyph = 0;
                    foreach (var font in fonts) { glyph = font.GlyphFor(scalar); if (glyph != 0) { chosen = font; break; } }
                    if (chosen is null) throw role == FontRole.Normal
                        ? new FactsPdfException("FPDF1504", $"No supplied font contains U+{scalar:X4}.")
                        : new FactsPdfException("FPDF1504", $"No supplied {FontRoles.Name(role)} font contains U+{scalar:X4}.", sourceOffset);
                    value = (chosen, chosen.Width1000(glyph));
                }
                resolved.Add((role, scalar), value);
            }
            return new(scalar, style, value.Font, value.Width, sourceOffset);
        }
        IEnumerable<Glyph> ReadGlyphs()
        {
            Glyph? pendingSpace = null;
            // In pre-line, hold the last authored segment break until actual
            // following content. CSS Text discards a final segment break, but
            // multiple consecutive breaks still produce blank PDF lines.
            Glyph? pendingSegmentBreak = null;
            var previousCarriageReturn = false;
            var afterText = false;
            // An ASCII word can cross differently styled inline spans, even
            // when one span explicitly resets text-transform to none.
            var insideAsciiWord = false;
            foreach (var run in paragraph.Runs)
            {
                cancellation.ThrowIfCancellationRequested();
                if (run.IsBreak)
                {
                    // HTML <br> is always explicit; unlike a final pre-line
                    // source newline, it must not be discarded.
                    if (pendingSegmentBreak is { } deferred)
                    {
                        yield return deferred;
                        pendingSegmentBreak = null;
                    }
                    previousCarriageReturn = false;
                    pendingSpace = null;
                    afterText = false;
                    insideAsciiWord = false;
                    yield return new Glyph(10, run.Style, null, 0, run.SourceOffset);
                    continue;
                }
                for (var i = 0; i < run.Text.Length;)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (Rune.DecodeFromUtf16(run.Text.AsSpan(i), out var rune, out var consumed) != OperationStatus.Done)
                        throw new FactsPdfException("FPDF1304", "Invalid UTF-16 text; unpaired surrogate.");
                    i += consumed;
                    var scalar = rune.Value;
                    if (paragraph.Style.PreserveLineBreaks)
                    {
                        // HTML's CRLF segment break is one break even across
                        // differently styled spans; CR and FF also separate lines.
                        if (scalar == 10 && previousCarriageReturn)
                        {
                            previousCarriageReturn = false;
                            continue;
                        }
                        previousCarriageReturn = scalar == 13;
                        if (scalar is 10 or 13 or 12)
                        {
                            if (pendingSegmentBreak is { } previousBreak)
                                yield return previousBreak;
                            pendingSegmentBreak = new Glyph(10, run.Style, null, 0, run.SourceOffset);
                            pendingSpace = null;
                            afterText = false;
                            insideAsciiWord = false;
                            continue;
                        }
                    }
                    // Scoped word definition: ASCII letters/digits form tokens;
                    // punctuation, spaces and non-ASCII scalars delimit them.
                    var wordCharacter = scalar is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
                    if (run.Style.Transform != TextTransform.None)
                    {
                        // ASCII-only case conversion is deterministic and cannot
                        // expand a character or change the font's glyph count.
                        // Do not silently misrender cased Unicode letters:
                        // full casing/locale rules and shaping are out of scope.
                        if (scalar > 127 &&
                            (Rune.GetUnicodeCategory(rune) is
                                (UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter)
                             // Unicode also maps some LetterNumber and OtherSymbol
                             // scalars, e.g. Roman numerals and circled letters.
                             // ASCII-only output must never leave those silently
                             // unchanged when case conversion is requested.
                             || Rune.ToUpperInvariant(rune) != rune
                             || Rune.ToLowerInvariant(rune) != rune))
                            throw new FactsPdfException("FPDF1305",
                                "Non-ASCII cased text requires Unicode case conversion beyond the supported ASCII report subset.",
                                run.SourceOffset);
                        if (run.Style.Transform == TextTransform.Uppercase && scalar is >= 'a' and <= 'z')
                            scalar -= 'a' - 'A';
                        else if (run.Style.Transform == TextTransform.Lowercase && scalar is >= 'A' and <= 'Z')
                            scalar += 'a' - 'A';
                        else if (run.Style.Transform == TextTransform.Capitalize && !insideAsciiWord && scalar is >= 'a' and <= 'z')
                            scalar -= 'a' - 'A';
                    }
                    insideAsciiWord = wordCharacter;
                    if (scalar <= 127 && HtmlTokens.Space((char)scalar))
                    {
                        // Resolve at the original point even when a later
                        // break/end removes this space. Its font diagnostic
                        // and the first whitespace run's style are preserved.
                        if (afterText && !pendingSpace.HasValue)
                            pendingSpace = Resolve(32, run.Style, run.SourceOffset);
                    }
                    else
                    {
                        if (pendingSegmentBreak is { } deferred)
                        {
                            yield return deferred;
                            pendingSegmentBreak = null;
                        }
                        if (pendingSpace.HasValue)
                        {
                            yield return pendingSpace.Value;
                            pendingSpace = null;
                        }
                        yield return Resolve(scalar, run.Style, run.SourceOffset);
                        afterText = true;
                    }
                }
            }
        }
        // The opt-in first-line inset is relative to the paragraph's resolved
        // content width, not to the page or its painted border box. It belongs
        // only to the first logical line; continuation pages never re-indent.
        var firstIndent = paragraph.Style.FirstLineIndent.Resolve(available);
        // Resolve the complete paragraph before reporting width errors, and
        // validate every word before yielding a line to pagination. Recording
        // a width failure (rather than throwing immediately) keeps the legacy
        // glyph -> width -> layout/budget error order without retaining lines.
        var segmentWidth = 0d;
        var previousScalar = -1;
        var tooWide = false;
        var firstSegment = true;
        foreach (var glyph in ReadGlyphs())
        {
            cancellation.ThrowIfCancellationRequested();
            if (paragraph.Style.NoWrap)
            {
                // Collapsed spaces remain advance widths, but only an explicit
                // <br> starts a new line. Validate the whole forced-break
                // segment before painting any page.
                if (glyph.Scalar == 10) { segmentWidth = 0; firstSegment = false; continue; }
                segmentWidth += glyph.Width;
                tooWide |= segmentWidth > (firstSegment ? available - firstIndent : available) + 0.000001;
                continue;
            }
            if (glyph.Scalar is 32 or 10)
            {
                segmentWidth = 0;
                previousScalar = -1;
                continue;
            }
            if (paragraph.Style.BreakWords)
            {
                // Emergency breaks are between already resolved glyphs.
                // A glyph wider than the fresh content line still fails.
                tooWide |= glyph.Width > available + 0.000001;
                previousScalar = glyph.Scalar;
                continue;
            }
            segmentWidth = previousScalar < 0 || CanBreak(previousScalar, glyph.Scalar)
                ? glyph.Width : segmentWidth + glyph.Width;
            tooWide |= segmentWidth > available + 0.000001;
            previousScalar = glyph.Scalar;
        }
        // Keep the legacy resource/diagnostic priority: resolve every glyph
        // before reporting indentation geometry, including for overwide boxes.
        if (!double.IsFinite(firstIndent) || firstIndent < 0 ||
            firstIndent > 0 && firstIndent >= available - 0.000001)
            throw new FactsPdfException("FPDF1302", "First-line indentation leaves no usable text width.", paragraph.SourceOffset);
        if (tooWide)
            throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.");

        var current = new List<Glyph>(); var width = 0d; Glyph? space = null;
        var firstLine = true;
        // CSS Inline Layout: a specified (non-normal) line-height uses the
        // metrics of the *first available font*, even if fallback fonts draw
        // some glyphs. Each inline style contributes its own leading-adjusted
        // ascent/descent; the parent's invisible strut is always present.
        // Using glyph fallback hhea metrics here incorrectly moves all text
        // baselines on mixed Latin/CJK lines and inflates explicit line-height.
        (double Ascent, double Descent) Metrics(TextStyle style, int sourceOffset)
        {
            var fonts = FontRoles.Chain(options, FontRoles.Role(style), sourceOffset);
            var size = style.FontSize;
            var ascent = size * (fonts.Count == 0 ? 0.8 : fonts[0].Ascent1000 / 1000);
            var descent = size * (fonts.Count == 0 ? 0.2 : -fonts[0].Descent1000 / 1000);
            var leading = ((style.AbsoluteLineHeight ?? size * style.LineHeight) - ascent - descent) / 2;
            return (ascent + leading, descent + leading);
        }
        Line CurrentLine(bool softWrapped = false)
        {
            // Only a real automatic word wrap may justify. Explicit <br>,
            // final paragraph lines and nowrap content retain their measured
            // ragged end. Stretch existing collapsed U+0020 space advances;
            // this uses the same WordSpacing/TJ paint path as authored gaps.
            var paintedWidth = width;
            if (softWrapped && paragraph.Style.Alignment == TextAlignment.Justify)
            {
                var spaces = 0;
                foreach (var glyph in current)
                    if (glyph.Scalar == 32) spaces++;
                var remaining = (firstLine ? available - firstIndent : available) - width;
                if (spaces > 0 && remaining > 0.000001)
                {
                    var extra = remaining / spaces;
                    for (var i = 0; i < current.Count; i++)
                    {
                        var glyph = current[i];
                        if (glyph.Scalar == 32)
                            current[i] = glyph with
                            {
                                Style = glyph.Style with { WordSpacing = glyph.Style.WordSpacing + extra }
                            };
                    }
                    paintedWidth = width + extra * spaces;
                }
            }
            var (ascent, descent) = Metrics(paragraph.Style, paragraph.SourceOffset);
            if (firstLine && firstMarker is { } marker)
            {
                var (markerAscent, markerDescent) = Metrics(marker.Style, marker.SourceOffset);
                if (marker.Text is null)
                {
                    markerAscent = Math.Max(markerAscent, 0.45 * marker.Style.FontSize);
                    markerDescent = Math.Max(markerDescent, -0.15 * marker.Style.FontSize);
                }
                ascent = Math.Max(ascent, markerAscent);
                descent = Math.Max(descent, markerDescent);
            }
            foreach (var glyph in current)
            {
                var (glyphAscent, glyphDescent) = Metrics(glyph.Style, glyph.SourceOffset);
                ascent = Math.Max(ascent, glyphAscent);
                descent = Math.Max(descent, glyphDescent);
            }
            return new(current, paintedWidth, ascent + descent, ascent, descent, firstLine ? firstIndent : 0);
        }
        void ResetLine()
        {
            current.Clear(); width = 0; space = null;
            firstLine = false;
        }
        // The consumer finishes a borrowed line before MoveNext clears it.
        // Retained page strings/paint and the PDF buffer are unchanged: this
        // is line-at-a-time wrapping, not a streaming document serializer.
        var word = new List<Glyph>();
        using var cursor = ReadGlyphs().GetEnumerator();
        var hasGlyph = cursor.MoveNext();
        while (hasGlyph)
        {
            cancellation.ThrowIfCancellationRequested();
            if (paragraph.Style.NoWrap)
            {
                // The paragraph deliberately has no automatic break
                // opportunities; HTML <br> remains an explicit break.
                if (cursor.Current.Scalar == 10)
                {
                    yield return CurrentLine();
                    ResetLine();
                }
                else
                {
                    current.Add(cursor.Current);
                    width += cursor.Current.Width;
                }
                hasGlyph = cursor.MoveNext();
                continue;
            }
            if (cursor.Current.Scalar == 10)
            {
                yield return CurrentLine();
                ResetLine();
                hasGlyph = cursor.MoveNext();
                continue;
            }
            if (cursor.Current.Scalar == 32) { space = cursor.Current; hasGlyph = cursor.MoveNext(); continue; }
            word.Clear();
            var wordWidth = 0d;
            int previous;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                var glyph = cursor.Current;
                word.Add(glyph);
                wordWidth += glyph.Width;
                if (!paragraph.Style.BreakWords && wordWidth > available + 0.000001)
                    throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.");
                previous = glyph.Scalar;
                hasGlyph = cursor.MoveNext();
            }
            while (hasGlyph && cursor.Current.Scalar is not (32 or 10) && !CanBreak(previous, cursor.Current.Scalar));
            var gap = current.Count > 0 && space.HasValue ? space.Value.Width : 0;
            var lineAvailable = firstLine ? available - firstIndent : available;
            // Break only a token that cannot fit a fresh line, or the
            // indented first line when no earlier text can move. A fitting
            // word moves as a whole at the preceding ordinary space.
            if (paragraph.Style.BreakWords &&
                (wordWidth > available + 0.000001 ||
                 current.Count == 0 && wordWidth > lineAvailable + 0.000001))
            {
                if (current.Count > 0)
                {
                    yield return CurrentLine(softWrapped: true);
                    ResetLine();
                }
                foreach (var glyph in word)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var capacity = firstLine ? available - firstIndent : available;
                    if (width + glyph.Width > capacity + 0.000001)
                    {
                        if (current.Count == 0)
                            throw new FactsPdfException("FPDF1302",
                                "An individual glyph cannot fit within the report text width.", glyph.SourceOffset);
                        yield return CurrentLine(softWrapped: true);
                        ResetLine();
                    }
                    current.Add(glyph);
                    width += glyph.Width;
                }
                space = null;
                continue;
            }
            if (width + gap + wordWidth > lineAvailable + 0.000001)
            {
                if (current.Count > 0)
                {
                    yield return CurrentLine(softWrapped: true);
                    ResetLine();
                }
                else
                    throw new FactsPdfException("FPDF1302",
                        "The first word cannot fit after first-line indentation.", paragraph.SourceOffset);
                gap = 0;
            }
            if (gap > 0) { current.Add(space!.Value); width += gap; }
            current.AddRange(word);
            width += wordWidth; space = null;
        }
        if (current.Count > 0 || firstLine && firstMarker is not null) yield return CurrentLine();
    }

    private static void CheckSimpleScalar(int scalar)
    {
        var category = Rune.GetUnicodeCategory(new Rune(scalar));
        var simpleRange = scalar is >= 32 and <= 0x052f or >= 0x1e00 and <= 0x1fff or >= 0x2000 and <= 0x2bff
            or >= 0x3000 and <= 0x30ff or >= 0x3400 and <= 0x9fff or >= 0xac00 and <= 0xd7a3
            or >= 0xf900 and <= 0xfaff or >= 0xff00 and <= 0xffef or >= 0x20000 and <= 0x323af;
        if (!simpleRange || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.Control
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.OtherNotAssigned)
            throw new FactsPdfException("FPDF1305", $"U+{scalar:X4} needs text processing outside this simple horizontal Unicode subset (shaping/bidi/sequence support is not implemented).");
    }
    private static bool Cjk(int scalar) => scalar is >= 0x3000 and <= 0x30ff or >= 0x3400 and <= 0x9fff
        or >= 0xac00 and <= 0xd7a3 or >= 0xf900 and <= 0xfaff or >= 0xff01 and <= 0xff65 or >= 0x20000 and <= 0x323af;
    private static bool CanBreak(int previous, int next)
    {
        if (previous == 160 || next == 160) return false;
        const string opening = "（〔［｛〈《「『【〖〘〚‘“([{", closing = "、。，．？！：；）》」』】〕］｝〗〙〛’”!?;:.,)]}";
        if (previous <= 65535 && opening.Contains((char)previous) || next <= 65535 && closing.Contains((char)next)) return false;
        return Cjk(previous) || Cjk(next);
    }
}
