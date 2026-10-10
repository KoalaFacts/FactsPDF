using System.Text;

namespace FactsPDF;

internal static class TextLayout
{
    private readonly record struct Glyph(char Value, TextStyle Style)
    {
        public double Width => Style.FontSize * 0.6; // Standard Courier: 600/1000 em per glyph.
    }
    private sealed record Line(List<Glyph> Glyphs, double Width, double Height, double MaxFontSize);

    public static List<LayoutPage> Layout(List<Paragraph> paragraphs, PdfOptions o, CancellationToken cancellation)
    {
        var pages = new List<LayoutPage> { new() };
        var y = o.Margin;
        var after = 0d;
        var breakNext = false;
        var availableWidth = o.PageWidth - 2 * o.Margin;
        var bottom = o.PageHeight - o.Margin;

        void NewPage()
        {
            if (pages.Count >= o.MaxPages) throw new FactsPdfException("FPDF1303", "Page limit exceeded.");
            pages.Add(new()); y = o.Margin; after = 0;
        }
        foreach (var paragraph in paragraphs)
        {
            cancellation.ThrowIfCancellationRequested();
            var lines = Wrap(paragraph, availableWidth, cancellation);
            if (lines.Count == 0) { after = Math.Max(after, paragraph.Style.MarginAfter); breakNext |= paragraph.Style.BreakBefore || paragraph.Style.BreakAfter; continue; }
            if ((breakNext || paragraph.Style.BreakBefore) && y > o.Margin) NewPage();
            breakNext = false;
            var gap = Math.Max(after, paragraph.Style.MarginBefore);
            if (y + gap + lines[0].Height > bottom && y > o.Margin) { NewPage(); gap = 0; }
            if (gap + lines[0].Height > bottom - o.Margin)
                throw new FactsPdfException("FPDF1302", "Paragraph margin and first line cannot fit on a page.");
            y += gap;
            foreach (var line in lines)
            {
                cancellation.ThrowIfCancellationRequested();
                if (line.Height > bottom - o.Margin) throw new FactsPdfException("FPDF1302", "Line height exceeds the usable page height.");
                if (y + line.Height > bottom + 0.000001) NewPage();
                var x = o.Margin + (paragraph.Style.Alignment switch
                { TextAlignment.Center => (availableWidth - line.Width) / 2, TextAlignment.Right => availableWidth - line.Width, _ => 0 });
                var baseline = o.PageHeight - y - (line.Height - line.MaxFontSize) / 2 - 0.8 * line.MaxFontSize;
                var start = 0;
                while (start < line.Glyphs.Count)
                {
                    var style = line.Glyphs[start].Style;
                    var text = new StringBuilder();
                    var width = 0d;
                    var end = start;
                    while (end < line.Glyphs.Count && line.Glyphs[end].Style == style)
                    { text.Append(line.Glyphs[end].Value); width += line.Glyphs[end].Width; end++; }
                    pages[^1].Runs.Add(new(text.ToString(), x, baseline, style));
                    x += width;
                    start = end;
                }
                y += line.Height;
            }
            after = paragraph.Style.MarginAfter;
            breakNext = paragraph.Style.BreakAfter;
        }
        return pages;
    }

    private static List<Line> Wrap(Paragraph paragraph, double available, CancellationToken cancellation)
    {
        var glyphs = new List<Glyph>();
        foreach (var run in paragraph.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run.IsBreak)
            {
                if (glyphs.Count > 0 && glyphs[^1].Value == ' ') glyphs.RemoveAt(glyphs.Count - 1);
                glyphs.Add(new('\n', run.Style));
                continue;
            }
            for (var i = 0; i < run.Text.Length; i++)
            {
                if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                var ch = run.Text[i];
                if (HtmlTokens.Space(ch))
                {
                    if (glyphs.Count > 0 && glyphs[^1].Value is not (' ' or '\n')) glyphs.Add(new(' ', run.Style));
                }
                else if (ch is >= ' ' and <= '~') glyphs.Add(new(ch, run.Style));
                else throw new FactsPdfException("FPDF1301", $"U+{(int)ch:X4} needs Unicode/font support, which is not implemented in this slice.");
            }
        }
        if (glyphs.Count > 0 && glyphs[^1].Value == ' ') glyphs.RemoveAt(glyphs.Count - 1);
        var lines = new List<Line>();
        var current = new List<Glyph>();
        var width = 0d;
        Glyph? space = null;
        void Flush(bool force)
        {
            if (current.Count == 0 && !force) return;
            var fontSize = paragraph.Style.FontSize;
            var height = fontSize * paragraph.Style.LineHeight;
            foreach (var glyph in current)
            { fontSize = Math.Max(fontSize, glyph.Style.FontSize); height = Math.Max(height, glyph.Style.FontSize * glyph.Style.LineHeight); }
            lines.Add(new(current, width, height, fontSize));
            current = []; width = 0; space = null;
        }
        for (var i = 0; i < glyphs.Count;)
        {
            cancellation.ThrowIfCancellationRequested();
            if (glyphs[i].Value == '\n') { Flush(true); i++; continue; }
            if (glyphs[i].Value == ' ') { space = glyphs[i++]; continue; }
            var end = i;
            var wordWidth = 0d;
            while (end < glyphs.Count && glyphs[end].Value is not (' ' or '\n')) wordWidth += glyphs[end++].Width;
            if (wordWidth > available + 0.000001) throw new FactsPdfException("FPDF1302", "An unbreakable word exceeds the usable page width.");
            var gap = current.Count > 0 && space.HasValue ? space.Value.Width : 0;
            if (width + gap + wordWidth > available + 0.000001) { Flush(false); gap = 0; }
            if (gap > 0) { current.Add(space!.Value); width += gap; }
            while (i < end) current.Add(glyphs[i++]);
            width += wordWidth;
            space = null;
        }
        Flush(false);
        return lines;
    }
}
