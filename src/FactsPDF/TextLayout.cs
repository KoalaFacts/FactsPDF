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
        // Retain the origin of pending top padding. Only still-open boxes may
        // carry that leading padding with their first text line to a new page.
        // The top padding of a closed empty box becomes trailing spacing.
        var pendingTopPadding = 0d; var pendingClosedPadding = 0d;
        var opened = new Stack<(double Top, int ContentEpoch)>();
        var contentEpoch = 0;
        var bottom = o.PageHeight - o.Margin;
        void NewPage()
        {
            if (pages.Count >= o.MaxPages) throw new FactsPdfException("FPDF1303", "Page limit exceeded.");
            pages.Add(new()); y = o.Margin; after = 0;
        }
        foreach (var step in steps)
        {
            cancellation.ThrowIfCancellationRequested();
            if (step is BeginBlock begin)
            {
                opened.Push((begin.PaddingTop, contentEpoch));
                pendingTopPadding += begin.PaddingTop;
                continue;
            }
            if (step is EndBlock closing)
            {
                var (top, openedAtEpoch) = opened.Pop();
                if (openedAtEpoch == contentEpoch)
                {
                    // No laid-out text has consumed this box's top padding.
                    // Since the box is now closed, it cannot move with later
                    // content across an explicit/overflow page boundary.
                    pendingTopPadding = Math.Max(0, pendingTopPadding - top);
                    pendingClosedPadding += top;
                }
                pendingClosedPadding += closing.PaddingBottom;
                continue;
            }
            var item = (LayoutParagraph)step;
            var paragraph = item.Paragraph;
            var lines = Wrap(paragraph, item.ContentWidth, o.Fonts, resolved, cancellation);
            if (lines.Count == 0) { after = Math.Max(after, paragraph.Style.MarginAfter); breakNext |= paragraph.Style.BreakBefore || paragraph.Style.BreakAfter; continue; }
            if (breakNext || paragraph.Style.BreakBefore)
            {
                if (y > o.Margin) NewPage();
                // Never carry a preceding box's bottom padding past an
                // explicit page break. Keep the next box's top padding.
                pendingClosedPadding = 0;
            }
            breakNext = false;
            var gap = Math.Max(after, paragraph.Style.MarginBefore) + pendingTopPadding + pendingClosedPadding;
            if (y + gap + lines[0].Height > bottom && y > o.Margin)
            {
                NewPage();
                // Paragraph margin and trailing padding belong to the previous
                // page; only the new box's leading padding travels with text.
                gap = pendingTopPadding;
            }
            if (gap + lines[0].Height > bottom - o.Margin)
                throw new FactsPdfException("FPDF1302", "Paragraph margin, box padding and first line cannot fit on a page.");
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
