using System.Buffers;
using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class TextLayout
{
    private readonly record struct Glyph(int Scalar, TextStyle Style, PdfFont? Font, double Width1000)
    {
        public double Width => Style.FontSize * Width1000 / 1000;
    }
    private sealed record Line(List<Glyph> Glyphs, double Width, double Height, double Ascent, double Descent);
    private readonly record struct OpenBlock(double Top, int ContentEpoch, BoxStyle Style,
        double X, double Width, Rgb TextColor, int SourceOffset, int StartPage,
        double StartY, int Order, bool IsParagraph, bool HasOccupiedDescendant = false,
        bool HasParagraphDescendant = false, bool TopConsumed = false);

    // M6 resolves content widths and X positions from the nested tree, while
    // glyphs and pagination remain text-based. Traverse lazily without flattening
    // the box hierarchy into a second global paragraph list.
    public static List<LayoutPage> Layout(DocumentRoot document, PdfOptions o, CancellationToken cancellation)
        => LayoutCore(BlockLayout.Steps(document, o, cancellation), o, cancellation);

    // Compatibility path for existing font/layout tests.
    public static List<LayoutPage> Layout(List<Paragraph> paragraphs, PdfOptions o, CancellationToken cancellation)
        => LayoutCore(paragraphs.Select(p => (BlockLayoutStep)new LayoutParagraph(p, o.Margin,
            o.PageWidth - 2 * o.Margin)), o, cancellation);

    private static List<LayoutPage> LayoutCore(IEnumerable<BlockLayoutStep> steps, PdfOptions o, CancellationToken cancellation)
    {
        var pages = new List<LayoutPage> { new() };
        var resolved = new Dictionary<int, (PdfFont? Font, double Width)>();
        var y = o.Margin; var after = 0d; var breakNext = false;
        // Retain pending top padding only for still-open blocks. Closed empty
        // predecessors contribute trailing spacing; a spanning box receives
        // page-local decoration slices rather than repeating its top padding.
        var pendingTopPadding = 0d; var pendingClosedPadding = 0d;
        var opened = new List<OpenBlock>();
        var contentEpoch = 0; var paintOrder = 0;
        var displayCommands = 0;
        void ChargeDisplayCommands(int count, int sourceOffset = -1)
        {
            // Charge before retaining output, across all pages and both layout
            // entry points. Subtraction avoids overflowing at an int.MaxValue
            // limit; zero-area fragments retain their existing zero cost.
            if (count > o.MaxDisplayCommands - displayCommands)
                throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.", sourceOffset);
            displayCommands += count;
        }
        var bottom = o.PageHeight - o.Margin;
        void NewPage()
        {
            if (pages.Count >= o.MaxPages) throw new FactsPdfException("FPDF1303", "Page limit exceeded.");
            pages.Add(new()); y = o.Margin; after = 0;
        }
        bool CurrentPageOccupied() => y > o.Margin + 0.000001
            || pendingClosedPadding > 0
            || pages[^1].Runs.Count > 0
            || pages[^1].PaintBoxes.Any(box => box.CommandCount > 0);

        void MoveUnconsumedBlocksToNewPage()
        {
            // Recompute leading space from its owning scopes, not the old
            // page's aggregate. A continued painted box (and consumed scopes
            // within it) has already used its top padding on the first page.
            // A genuinely new child still moves with its own leading space.
            var leading = 0d;
            var continuedPaint = false;
            for (var i = 0; i < opened.Count; i++)
            {
                var scope = opened[i];
                var hasText = scope.ContentEpoch != contentEpoch;
                var retainOrigin = scope.HasOccupiedDescendant &&
                    (scope.Style.HasPaint || continuedPaint);
                if (scope.Style.HasPaint && (hasText || scope.TopConsumed || scope.HasOccupiedDescendant))
                    continuedPaint = true;
                if (hasText || scope.TopConsumed) continue;
                if (retainOrigin)
                {
                    // Remember consumption even if this page contains only
                    // an empty paragraph: EndBlock must not re-add this top.
                    opened[i] = scope with { TopConsumed = true };
                    continue;
                }
                // Preserve M6's first-text leading-space behavior for purely
                // unpainted scopes outside any continuing painted ancestor.
                opened[i] = scope with { StartPage = pages.Count - 1, StartY = o.Margin + leading };
                leading += scope.Top;
            }
            pendingTopPadding = leading;
        }
        foreach (var step in steps)
        {
            cancellation.ThrowIfCancellationRequested();
            if (step is BeginBlock begin)
            {
                // A previous break-after must precede a painted successor
                // even when that successor contains no text paragraph.
                // Unpainted containers continue deferring the break to their
                // first text leaf, preserving existing paragraph-only behavior.
                if (breakNext && begin.Style.HasPaint)
                {
                    if (CurrentPageOccupied())
                    {
                        NewPage();
                        MoveUnconsumedBlocksToNewPage();
                    }
                    pendingClosedPadding = 0;
                    breakNext = false;
                }
                // Even an empty paragraph has a bottom margin that belongs to
                // its containing block's content height, not its own border.
                if (begin.IsParagraph)
                    for (var i = 0; i < opened.Count; i++)
                        opened[i] = opened[i] with { HasParagraphDescendant = true };
                var start = y + pendingTopPadding + pendingClosedPadding + after;
                opened.Add(new(begin.PaddingTop, contentEpoch, begin.Style,
                    begin.X, begin.OuterWidth, begin.TextColor, begin.SourceOffset,
                    pages.Count - 1, start, paintOrder++, begin.IsParagraph));
                pendingTopPadding += begin.PaddingTop;
                continue;
            }
            if (step is EndBlock closing)
            {
                var scope = opened[^1];
                opened.RemoveAt(opened.Count - 1);
                var emittedPaint = false;
                if (scope.Style.HasPaint)
                {
                    // Paragraph margins are outside their own border box,
                    // while the last child's bottom margin is within a
                    // containing block's content height.
                    var childBottomMargin = !scope.IsParagraph &&
                        (scope.ContentEpoch != contentEpoch || scope.HasParagraphDescendant)
                        ? after : 0d;
                    var finalPage = pages.Count - 1;
                    // The first page's border-box origin and the last page's
                    // cursor use different page-local coordinate systems.
                    // Never use the first-page starting Y as a lower bound
                    // for the *last* fragment when this block spans pages.
                    var minimumEnd = scope.StartPage == finalPage
                        ? scope.StartY + scope.Top : o.Margin;
                    var finish = Math.Max(minimumEnd,
                        y + pendingTopPadding + pendingClosedPadding + childBottomMargin);
                    finish += closing.PaddingBottom;
                    if (displayCommands == o.MaxDisplayCommands)
                    {
                        // With no allowance left, a definitely nonzero paint
                        // command must fail before later geometry validation.
                        // Reuse actual clipped/zero-area accounting rather
                        // than treating HasPaint alone as a command. At most
                        // three shapes exist: first, last and a middle slice.
                        void CheckFragment(double top, double end, bool first, bool last)
                        {
                            var candidate = new PaintedBox(scope.X, top, scope.Width,
                                Math.Max(0, end - top), scope.Style, scope.TextColor, scope.Order,
                                IsFirstFragment: first, IsLastFragment: last);
                            ChargeDisplayCommands(candidate.CommandCount, scope.SourceOffset);
                        }
                        CheckFragment(scope.StartY, scope.StartPage == finalPage ? finish : bottom,
                            true, scope.StartPage == finalPage);
                        if (scope.StartPage < finalPage)
                            CheckFragment(o.Margin, finish, false, true);
                        if (finalPage - scope.StartPage > 1)
                            CheckFragment(o.Margin, bottom, false, false);
                    }
                    if (!double.IsFinite(finish) || finish > bottom + 0.000001 ||
                        scope.StartY < o.Margin - 0.000001 || scope.Width < 0 ||
                        scope.StartPage > finalPage || scope.StartY > bottom + 0.000001)
                        throw new FactsPdfException("FPDF1302",
                            "Decorated box fragment exceeds the usable page area.", scope.SourceOffset);

                    // The existing M7 single-page path retains exactly the
                    // same rectangle geometry and PDF bytes. Spanning boxes
                    // instead produce one bounded, page-local slice on each
                    // occupied page. No top/bottom padding or border is
                    // duplicated on continuation pages.
                    for (var pageIndex = scope.StartPage; pageIndex <= finalPage; pageIndex++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var isFirst = pageIndex == scope.StartPage;
                        var isLast = pageIndex == finalPage;
                        var fragmentTop = isFirst ? scope.StartY : o.Margin;
                        var fragmentEnd = isLast ? finish : bottom;
                        var height = Math.Max(0, fragmentEnd - fragmentTop);
                        if (fragmentEnd < fragmentTop - 0.000001 ||
                            fragmentTop < o.Margin - 0.000001 ||
                            fragmentEnd > bottom + 0.000001)
                            throw new FactsPdfException("FPDF1302",
                                "Page fragment geometry exceeds the page margins.", scope.SourceOffset);
                        var painted = new PaintedBox(scope.X, fragmentTop, scope.Width, height,
                            scope.Style, scope.TextColor, scope.Order,
                            IsFirstFragment: isFirst, IsLastFragment: isLast);
                        ChargeDisplayCommands(painted.CommandCount, scope.SourceOffset);
                        pages[pageIndex].PaintBoxes.Add(painted);
                        emittedPaint |= painted.CommandCount > 0;
                    }
                }
                if (scope.ContentEpoch == contentEpoch && !scope.TopConsumed)
                {
                    // No laid-out text or retained first fragment consumed
                    // this top padding. Closure now makes it trailing space.
                    pendingTopPadding = Math.Max(0, pendingTopPadding - scope.Top);
                    pendingClosedPadding += scope.Top;
                }
                pendingClosedPadding += closing.PaddingBottom;
                // An unpainted empty child can still consume page geometry
                // through padding, borders or margins. If it has already
                // occupied space on a page, a painted ancestor cannot be
                // moved intact to another page before its first text line.
                var occupiesSpace = emittedPaint || scope.HasOccupiedDescendant ||
                    scope.Top > 0 || closing.PaddingBottom > 0 ||
                    (scope.IsParagraph && after > 0);
                if (occupiesSpace)
                    for (var i = 0; i < opened.Count; i++)
                        opened[i] = opened[i] with { HasOccupiedDescendant = true };
                continue;
            }
            var item = (LayoutParagraph)step;
            var paragraph = item.Paragraph;
            // Do not allocate the next paragraph's glyph/line lists after an
            // earlier paragraph or box has used the exact command allowance.
            // Blank/whitespace/break-only paragraphs can still affect geometry
            // without emitting text commands and must retain that behavior.
            if (displayCommands == o.MaxDisplayCommands && HasTextContent(paragraph, cancellation))
                ChargeDisplayCommands(1);
            var lines = Wrap(paragraph, item.ContentWidth, o.Fonts, resolved, cancellation);
            if (lines.Count == 0)
            {
                // Empty painted/padded paragraphs still occupy box geometry
                // and can request page breaks, even without glyphs.
                if (paragraph.Style.BreakBefore)
                {
                    if (CurrentPageOccupied())
                    {
                        NewPage();
                        MoveUnconsumedBlocksToNewPage();
                    }
                    pendingClosedPadding = 0;
                }
                if (opened.Count > 0 && opened[^1].IsParagraph &&
                    opened[^1].ContentEpoch == contentEpoch &&
                    (opened[^1].Style.HasPaint || opened[^1].Top > 0))
                {
                    var before = Math.Max(after, paragraph.Style.MarginBefore);
                    opened[^1] = opened[^1] with
                    {
                        StartY = opened[^1].StartY + Math.Max(0, before - after)
                    };
                    pendingClosedPadding += before;
                    after = paragraph.Style.MarginAfter;
                }
                else after = Math.Max(after, paragraph.Style.MarginAfter);
                breakNext |= paragraph.Style.BreakAfter;
                continue;
            }
            if (breakNext || paragraph.Style.BreakBefore)
            {
                if (CurrentPageOccupied())
                {
                    NewPage();
                    MoveUnconsumedBlocksToNewPage();
                }
                // Never carry a preceding box's bottom padding past an
                // explicit page break. Keep the next box's top padding.
                pendingClosedPadding = 0;
            }
            breakNext = false;
            var gap = Math.Max(after, paragraph.Style.MarginBefore) + pendingTopPadding + pendingClosedPadding;
            if (y + gap + lines[0].Height > bottom && CurrentPageOccupied())
            {
                NewPage();
                MoveUnconsumedBlocksToNewPage();
                // Paragraph margin and trailing padding belong to the previous
                // page; only the new box's leading padding travels with text.
                gap = pendingTopPadding;
            }
            if (gap + lines[0].Height > bottom - o.Margin)
                throw new FactsPdfException("FPDF1302", "Paragraph margin, box padding and first line cannot fit on a page.");
            if (opened.Count > 0 && opened[^1].IsParagraph &&
                opened[^1].ContentEpoch == contentEpoch)
                opened[^1] = opened[^1] with { StartY = y + gap - opened[^1].Top };
            y += gap;
            pendingTopPadding = 0;
            pendingClosedPadding = 0;
            contentEpoch++;
            foreach (var line in lines)
            {
                cancellation.ThrowIfCancellationRequested();
                if (line.Height > bottom - o.Margin) throw new FactsPdfException("FPDF1302", "Line height exceeds the usable page height.");
                if (y + line.Height > bottom + 0.000001) NewPage();
                var x = item.ContentX + (paragraph.Style.Alignment switch
                { TextAlignment.Center => (item.ContentWidth - line.Width) / 2,
                  TextAlignment.Right => item.ContentWidth - line.Width, _ => 0 });
                var baseline = o.PageHeight - y - line.Ascent;
                for (var start = 0; start < line.Glyphs.Count;)
                {
                    ChargeDisplayCommands(1);
                    var first = line.Glyphs[start]; var text = new StringBuilder(); var width = 0d; var end = start;
                    while (end < line.Glyphs.Count && line.Glyphs[end].Style == first.Style && ReferenceEquals(line.Glyphs[end].Font, first.Font))
                    {
                        var glyph = line.Glyphs[end++];
                        if (glyph.Scalar <= 65535) text.Append((char)glyph.Scalar); else text.Append(char.ConvertFromUtf32(glyph.Scalar));
                        width += glyph.Width;
                    }
                    pages[^1].Runs.Add(new(text.ToString(), x, baseline, first.Style, first.Font));
                    x += width; start = end;
                }
                y += line.Height;
            }
            after = paragraph.Style.MarginAfter; breakNext = paragraph.Style.BreakAfter;
        }
        return pages;
    }

    private static bool HasTextContent(Paragraph paragraph, CancellationToken cancellation)
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

    private static List<Line> Wrap(Paragraph paragraph, double available, IReadOnlyList<PdfFont> fonts,
        Dictionary<int, (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        Glyph Resolve(int scalar, TextStyle style)
        {
            if (!resolved.TryGetValue(scalar, out var value))
            {
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
                    if (chosen is null) throw new FactsPdfException("FPDF1504", $"No supplied font contains U+{scalar:X4}.");
                    value = (chosen, chosen.Width1000(glyph));
                }
                resolved.Add(scalar, value);
            }
            return new(scalar, style, value.Font, value.Width);
        }
        var glyphs = new List<Glyph>();
        foreach (var run in paragraph.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run.IsBreak)
            {
                if (glyphs.Count > 0 && glyphs[^1].Scalar == 32) glyphs.RemoveAt(glyphs.Count - 1);
                glyphs.Add(new(10, run.Style, null, 0)); continue;
            }
            for (var i = 0; i < run.Text.Length;)
            {
                cancellation.ThrowIfCancellationRequested();
                if (Rune.DecodeFromUtf16(run.Text.AsSpan(i), out var rune, out var consumed) != OperationStatus.Done)
                    throw new FactsPdfException("FPDF1304", "Invalid UTF-16 text; unpaired surrogate.");
                i += consumed; var scalar = rune.Value;
                if (scalar <= 127 && HtmlTokens.Space((char)scalar))
                {
                    if (glyphs.Count > 0 && glyphs[^1].Scalar is not (32 or 10)) glyphs.Add(Resolve(32, run.Style));
                }
                else glyphs.Add(Resolve(scalar, run.Style));
            }
        }
        if (glyphs.Count > 0 && glyphs[^1].Scalar == 32) glyphs.RemoveAt(glyphs.Count - 1);
        var lines = new List<Line>(); var current = new List<Glyph>(); var width = 0d; Glyph? space = null;
        // CSS Inline Layout: a specified (non-normal) line-height uses the
        // metrics of the *first available font*, even if fallback fonts draw
        // some glyphs. Each inline style contributes its own leading-adjusted
        // ascent/descent; the parent's invisible strut is always present.
        // Using glyph fallback hhea metrics here incorrectly moves all text
        // baselines on mixed Latin/CJK lines and inflates explicit line-height.
        (double Ascent, double Descent) Metrics(TextStyle style)
        {
            var size = style.FontSize;
            var ascent = size * (fonts.Count == 0 ? 0.8 : fonts[0].Ascent1000 / 1000);
            var descent = size * (fonts.Count == 0 ? 0.2 : -fonts[0].Descent1000 / 1000);
            var leading = (size * style.LineHeight - ascent - descent) / 2;
            return (ascent + leading, descent + leading);
        }
        void Flush(bool force)
        {
            if (current.Count == 0 && !force) return;
            var (ascent, descent) = Metrics(paragraph.Style); // line box strut
            foreach (var glyph in current)
            {
                var (glyphAscent, glyphDescent) = Metrics(glyph.Style);
                ascent = Math.Max(ascent, glyphAscent);
                descent = Math.Max(descent, glyphDescent);
            }
            var height = ascent + descent;
            lines.Add(new(current, width, height, ascent, descent));
            current = []; width = 0; space = null;
        }
        for (var i = 0; i < glyphs.Count;)
        {
            cancellation.ThrowIfCancellationRequested();
            if (glyphs[i].Scalar == 10) { Flush(true); i++; continue; }
            if (glyphs[i].Scalar == 32) { space = glyphs[i++]; continue; }
            var end = i + 1; var wordWidth = glyphs[i].Width;
            while (end < glyphs.Count && glyphs[end].Scalar is not (32 or 10) && !CanBreak(glyphs[end - 1].Scalar, glyphs[end].Scalar)) wordWidth += glyphs[end++].Width;
            if (wordWidth > available + 0.000001) throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.");
            var gap = current.Count > 0 && space.HasValue ? space.Value.Width : 0;
            if (width + gap + wordWidth > available + 0.000001) { Flush(false); gap = 0; }
            if (gap > 0) { current.Add(space!.Value); width += gap; }
            while (i < end) current.Add(glyphs[i++]);
            width += wordWidth; space = null;
        }
        Flush(false); return lines;
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
