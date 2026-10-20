using System.Buffers;
using System.Globalization;
using System.Text;
using static FactsPDF.TextLines;

namespace FactsPDF;

// Page text never changes body pages or geometry. Its searchable runs enter
// the ordinary font/subset/serialization pipeline after body layout succeeds.
internal static class PageMarginLayout
{
    private const double Clearance = 2;
    private const double Epsilon = 0.000001;
    private static readonly TextStyle Style = new(10, 1.2, new Rgb(0, 0, 0), TextAlignment.Center);

    internal static void Append(List<LayoutPage> pages, PdfOptions options, long displayCommands,
        CancellationToken cancellation)
    {
        var resolved = new Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)>();
        var header = options.HeaderText?.Trim(' ');
        var footer = options.FooterText?.Trim(' ');
        void Ensure(int count)
        {
            if (count > options.MaxDisplayCommands - displayCommands)
                throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.");
        }
        void Charge(int count, int offset) { Ensure(count); displayCommands += count; }
        for (var index = 0; index < pages.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var page = pages[index];
            var bodyRuns = page.Runs.Count;
            if (!string.IsNullOrEmpty(header)) AppendText(header, true);
            if (!string.IsNullOrEmpty(footer) || options.PageNumbers) AppendText(footer ?? "", false);

            void AppendText(string text, bool topMargin)
            {
                // Retain F2's budget-before-glyph resolution order, page by page.
                Ensure(1);
                text = Normalize(text, cancellation);
                if (!topMargin && options.PageNumbers)
                {
                    var number = (index + 1).ToString(CultureInfo.InvariantCulture);
                    text = text.Length == 0 ? number : text + " | " + number;
                }
                var paragraph = new Paragraph(Style);
                paragraph.Runs.Add(new(text, Style));
                using var lines = Wrap(paragraph, options.PageWidth - 2 * options.Margin,
                    options, resolved, cancellation).GetEnumerator();
                if (!lines.MoveNext()) throw new InvalidOperationException("Missing page text line.");
                // Wrap lends its glyph buffer. Detect wrapping before appending or
                // charging runs, and keep a copy across its next MoveNext.
                var line = lines.Current with { Glyphs = [.. lines.Current.Glyphs] };
                if (lines.MoveNext()) throw Geometry();
                var ascent = line.Ascent; var descent = line.Descent;
                var x = (options.PageWidth - line.Width) / 2;
                var advance = 0d;
                foreach (var glyph in line.Glyphs)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // Courier's standard AFM box; explicit fonts use only the
                    // declared boxes actually drawing glyphs, at each origin.
                    var left = glyph.Font?.XMin1000 ?? -23;
                    var right = glyph.Font?.XMax1000 ?? 715;
                    var top = glyph.Font?.YMax1000 ?? 805;
                    var bottom = glyph.Font?.YMin1000 ?? -250;
                    ascent = Math.Max(ascent, top * Style.FontSize / 1000);
                    descent = Math.Max(descent, -bottom * Style.FontSize / 1000);
                    if (x + advance + left * Style.FontSize / 1000 < Clearance - Epsilon ||
                        x + advance + right * Style.FontSize / 1000 > options.PageWidth - Clearance + Epsilon)
                        throw Geometry();
                    advance += glyph.Width;
                }
                if (ascent + descent + 2 * Clearance > options.Margin + Epsilon) throw Geometry();
                var baseline = topMargin
                    ? options.PageHeight - (options.Margin - ascent - descent) / 2 - ascent
                    : (options.Margin - ascent - descent) / 2 + descent;
                var bodyEdge = topMargin ? baseline - descent : baseline + ascent;
                void CheckBody(double top, double bottom)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (topMargin ? top + Clearance > bodyEdge + Epsilon : bottom < bodyEdge + Clearance - Epsilon)
                        throw Geometry();
                }
                // Body ink may extend outside line boxes. Do not count the header
                // just appended as body, and never repaginate to fix an overlap.
                for (var i = 0; i < bodyRuns; i++)
                {
                    var run = page.Runs[i];
                    CheckBody(run.Baseline + (run.Font?.YMax1000 ?? 805) * run.Style.FontSize / 1000,
                        run.Baseline + (run.Font?.YMin1000 ?? -250) * run.Style.FontSize / 1000);
                }
                foreach (var disc in page.Discs) CheckBody(disc.Y + disc.Radius, disc.Y - disc.Radius);
                foreach (var image in page.Images) CheckBody(options.PageHeight - image.Top, options.PageHeight - image.Top - image.Height);
                foreach (var box in page.PaintBoxes)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (box.CommandCount > 0) CheckBody(options.PageHeight - box.Top, options.PageHeight - box.Top - box.Height);
                }
                AppendLine(line, x, baseline, page.Runs, Charge, cancellation);
            }
        }
    }

    private static string Normalize(string text, CancellationToken cancellation)
    {
        // Scan the complete field before resolving any glyph; first lexical
        // defect wins. Only U+0020 is collapsible in this single-line contract.
        for (var i = 0; i < text.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(text.AsSpan(i), out var rune, out var consumed) != OperationStatus.Done)
                throw new FactsPdfException("FPDF1304", "Invalid UTF-16 page text; unpaired surrogate.");
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                throw new FactsPdfException("FPDF1305", "Page text must be a single line without control characters.");
            i += consumed;
        }
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static FactsPdfException Geometry() => new("FPDF1302",
        "Single-line page text cannot fit its margin with clearance from page edges and body content.");
}
