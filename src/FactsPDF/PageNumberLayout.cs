using System.Globalization;
using static FactsPDF.TextLines;

namespace FactsPDF;

// Footer composition never changes the body's pages or geometry. Its commands
// enter the same font/subset/serialization pipeline as ordinary searchable text.
internal static class PageNumberLayout
{
    private const double Clearance = 2;
    private const double Epsilon = 0.000001;
    private static readonly TextStyle Style = new(10, 1.2, new Rgb(0, 0, 0), TextAlignment.Center);

    internal static void Append(List<LayoutPage> pages, PdfOptions options, long displayCommands,
        CancellationToken cancellation)
    {
        var resolved = new Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)>();
        void Ensure(int count)
        {
            if (count > options.MaxDisplayCommands - displayCommands)
                throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.");
        }
        void Charge(int count, int offset) { Ensure(count); displayCommands += count; }
        for (var index = 0; index < pages.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            // Do not resolve another page's glyphs after consuming the budget.
            Ensure(1);
            var paragraph = new Paragraph(Style);
            paragraph.Runs.Add(new((index + 1).ToString(CultureInfo.InvariantCulture), Style));
            using var lines = Wrap(paragraph, options.PageWidth - 2 * options.Margin,
                options, resolved, cancellation).GetEnumerator();
            if (!lines.MoveNext()) throw new InvalidOperationException("Missing page-number line.");
            var line = lines.Current;
            var ascent = line.Ascent; var descent = line.Descent;
            var x = (options.PageWidth - line.Width) / 2;
            var advance = 0d;
            foreach (var glyph in line.Glyphs)
            {
                cancellation.ThrowIfCancellationRequested();
                // Courier bounds are Adobe's standard AFM bounds. For explicit
                // fonts use only fonts actually drawing these digits, not unused
                // fallback fonts. Bounds are conservative, not outline sanitization.
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
            var baseline = (options.Margin - ascent - descent) / 2 + descent;
            var topEdge = baseline + ascent;
            void CheckBodyBottom(double bottom)
            {
                cancellation.ThrowIfCancellationRequested();
                if (bottom < topEdge + Clearance - Epsilon) throw Geometry();
            }
            var page = pages[index];
            // Body line boxes need not contain all ink. Inspect placed content
            // before appending the footer and reject possible overlap without
            // repaginating or changing legacy small-line-height behavior.
            foreach (var run in page.Runs)
                CheckBodyBottom(run.Baseline + (run.Font?.YMin1000 ?? -250) * run.Style.FontSize / 1000);
            foreach (var disc in page.Discs) CheckBodyBottom(disc.Y - disc.Radius);
            foreach (var image in page.Images) CheckBodyBottom(options.PageHeight - image.Top - image.Height);
            foreach (var box in page.PaintBoxes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (box.CommandCount > 0) CheckBodyBottom(options.PageHeight - box.Top - box.Height);
            }
            AppendLine(line, x, baseline, page.Runs, Charge, cancellation);
        }
    }

    private static FactsPdfException Geometry() => new("FPDF1302",
        "Page number cannot fit the bottom margin with clearance from page edges and body content.");
}
