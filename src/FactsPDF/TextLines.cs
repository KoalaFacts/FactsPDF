using System.Buffers;
using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class TextLines
{
    internal readonly record struct Glyph(int Scalar, TextStyle Style, PdfFont? Font, double Width1000, int SourceOffset = -1)
    {
        public double Width => Style.FontSize * Width1000 / 1000;
    }
    // Glyph storage is borrowed until the next MoveNext. Each consumer copies
    // every placed text run before advancing; the iterator reuses its glyph storage.
    internal readonly record struct Line(List<Glyph> Glyphs, double Width, double Height, double Ascent, double Descent);

    internal static void AppendLine(Line line, double x, double baseline, List<PlacedText> target,
        Action<int, int> charge, CancellationToken cancellation, int sourceOffset = -1)
    {
        for (var start = 0; start < line.Glyphs.Count;)
        {
            charge(1, sourceOffset);
            var first = line.Glyphs[start]; var width = 0d; var end = start; var utf16Length = 0;
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
            target.Add(new(text, x, baseline, first.Style, first.Font));
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
            var afterText = false;
            foreach (var run in paragraph.Runs)
            {
                cancellation.ThrowIfCancellationRequested();
                if (run.IsBreak)
                {
                    pendingSpace = null;
                    afterText = false;
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
        // Resolve the complete paragraph before reporting width errors, and
        // validate every word before yielding a line to pagination. Recording
        // a width failure (rather than throwing immediately) keeps the legacy
        // glyph -> width -> layout/budget error order without retaining lines.
        var segmentWidth = 0d;
        var previousScalar = -1;
        var tooWide = false;
        foreach (var glyph in ReadGlyphs())
        {
            cancellation.ThrowIfCancellationRequested();
            if (glyph.Scalar is 32 or 10)
            {
                segmentWidth = 0;
                previousScalar = -1;
                continue;
            }
            segmentWidth = previousScalar < 0 || CanBreak(previousScalar, glyph.Scalar)
                ? glyph.Width : segmentWidth + glyph.Width;
            tooWide |= segmentWidth > available + 0.000001;
            previousScalar = glyph.Scalar;
        }
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
            var leading = (size * style.LineHeight - ascent - descent) / 2;
            return (ascent + leading, descent + leading);
        }
        Line CurrentLine()
        {
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
            return new(current, width, ascent + descent, ascent, descent);
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
                if (wordWidth > available + 0.000001)
                    throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.");
                previous = glyph.Scalar;
                hasGlyph = cursor.MoveNext();
            }
            while (hasGlyph && cursor.Current.Scalar is not (32 or 10) && !CanBreak(previous, cursor.Current.Scalar));
            var gap = current.Count > 0 && space.HasValue ? space.Value.Width : 0;
            if (width + gap + wordWidth > available + 0.000001)
            {
                if (current.Count > 0)
                {
                    yield return CurrentLine();
                    ResetLine();
                }
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
