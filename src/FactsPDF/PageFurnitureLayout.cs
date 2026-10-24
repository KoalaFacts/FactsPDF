using System.Buffers;
using System.Globalization;
using System.Text;
using static FactsPDF.TextLines;

namespace FactsPDF;

// Appended only after body pagination and display accounting. No body reflow,
// implicit resources, extra pages or changes to the legacy margin-text path.
internal static class PageFurnitureLayout
{
    private const double Clearance = 2;
    private const double Epsilon = 0.000001;
    private sealed record PlacedRegion(string Name, Line Line, double X,
        double Left, double Right, double Ascent, double Descent);

    private static IEnumerable<(string Name, PdfPageText? Text)> Fields(PdfPageFurnitureRegions regions)
    {
        yield return ("HeaderLeft", regions.HeaderLeft);
        yield return ("HeaderCenter", regions.HeaderCenter);
        yield return ("HeaderRight", regions.HeaderRight);
        yield return ("FooterLeft", regions.FooterLeft);
        yield return ("FooterCenter", regions.FooterCenter);
        yield return ("FooterRight", regions.FooterRight);
    }

    internal static long Validate(PdfOptions options)
    {
        var furniture = options.PageFurniture;
        if (furniture is null) return 0;
        if (options.PageNumbers || options.HeaderText is not null || options.FooterText is not null)
            throw new ArgumentException("PageFurniture cannot be combined with legacy page numbers, HeaderText or FooterText.");
        if (furniture.Default is null) throw new ArgumentException("PageFurniture.Default must not be null.");

        long length = 0;
        foreach (var (group, regions) in new[] {
            ("Default", furniture.Default), ("FirstPage", furniture.FirstPage) })
        {
            if (regions is null) continue;
            foreach (var (name, field) in Fields(regions))
            {
                if (field is null) continue;
                if (field.Template is null)
                    throw new ArgumentException($"PageFurniture.{group}.{name}.Template must not be null.");
                if (!double.IsFinite(field.FontSize) || field.FontSize is < 1 or > 144)
                    throw new ArgumentOutOfRangeException(nameof(options), $"PageFurniture.{group}.{name}.FontSize must be 1–144.");
                if (field.Template.Length > 1024)
                    throw new FactsPdfException("FPDF1001", $"PageFurniture.{group}.{name} exceeds 1024 UTF-16 units.");
                length += field.Template.Length;
            }
        }
        return length;
    }

    internal static void Append(List<LayoutPage> pages, PdfOptions options,
        long displayCommands, CancellationToken cancellation)
    {
        var furniture = options.PageFurniture;
        if (furniture is null) return;
        // Compile every supplied definition, even a suppressed/unused first page.
        // Only rendered nonempty lines resolve glyphs and font roles.
        foreach (var (group, regions) in new[] {
            ("Default", furniture.Default), ("FirstPage", furniture.FirstPage) })
        {
            if (regions is null) continue;
            foreach (var (name, field) in Fields(regions))
                if (field is not null) _ = Expand(field.Template, 1, pages.Count, group + "." + name, cancellation);
        }

        var resolved = new Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)>();
        for (var index = 0; index < pages.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var page = pages[index];
            var bodyRuns = page.Runs.Count;
            var regions = index == 0 && furniture.FirstPage is not null
                ? furniture.FirstPage : furniture.Default;
            var selected = Fields(regions).ToArray();
            AppendBand(selected[..3], true);
            AppendBand(selected[3..], false);

            void AppendBand((string Name, PdfPageText? Text)[] fields, bool topMargin)
            {
                var staged = new List<PlacedRegion>(3);
                foreach (var (name, field) in fields)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (field is null) continue;
                    var text = Expand(field.Template, index + 1, pages.Count,
                        (index == 0 && furniture.FirstPage is not null ? "FirstPage." : "Default.") + name, cancellation);
                    if (text.Length == 0) continue;
                    // Refuse insufficient display budget before resolving fonts.
                    if (displayCommands + staged.Count + 1 > options.MaxDisplayCommands)
                        throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.");
                    var alignment = name.EndsWith("Left", StringComparison.Ordinal) ? TextAlignment.Left
                        : name.EndsWith("Right", StringComparison.Ordinal) ? TextAlignment.Right : TextAlignment.Center;
                    var style = new TextStyle(field.FontSize, 1.2,
                        new Rgb(field.Color.R / 255d, field.Color.G / 255d, field.Color.B / 255d),
                        alignment, Bold: field.Bold, Italic: field.Italic);
                    var paragraph = new Paragraph(style);
                    paragraph.Runs.Add(new TextRun(text, style));
                    Line line;
                    try
                    {
                        using var lines = Wrap(paragraph, options.PageWidth - 2 * options.Margin,
                            options, resolved, cancellation).GetEnumerator();
                        if (!lines.MoveNext()) continue;
                        line = lines.Current with { Glyphs = [.. lines.Current.Glyphs] };
                        if (lines.MoveNext()) throw Geometry(index, name, "cannot wrap to another line");
                    }
                    catch (FactsPdfException error)
                    {
                        throw new FactsPdfException(error.Code,
                            $"Page {index + 1}, {name}: {error.Message}");
                    }

                    var x = alignment switch
                    {
                        TextAlignment.Left => options.Margin,
                        TextAlignment.Right => options.PageWidth - options.Margin - line.Width,
                        _ => (options.PageWidth - line.Width) / 2
                    };
                    var left = x;
                    var right = x + line.Width;
                    var ascent = line.Ascent;
                    var descent = line.Descent;
                    var offset = 0d;
                    foreach (var glyph in line.Glyphs)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var size = glyph.Style.FontSize;
                        var glyphLeft = x + offset + (glyph.Font?.XMin1000 ?? -23) * size / 1000;
                        var glyphRight = x + offset + (glyph.Font?.XMax1000 ?? 715) * size / 1000;
                        left = Math.Min(left, glyphLeft);
                        right = Math.Max(right, glyphRight);
                        ascent = Math.Max(ascent, (glyph.Font?.YMax1000 ?? 805) * size / 1000);
                        descent = Math.Max(descent, -(glyph.Font?.YMin1000 ?? -250) * size / 1000);
                        offset += glyph.Width;
                    }
                    if (left < Clearance - Epsilon || right > options.PageWidth - Clearance + Epsilon)
                        throw Geometry(index, name, "extends beyond the page-edge clearance");
                    staged.Add(new(name, line, x, left, right, ascent, descent));
                }
                if (staged.Count == 0) return;
                // Fields are ordered left/centre/right; allow width to use all
                // available space, but forbid ink intervals from colliding.
                for (var i = 1; i < staged.Count; i++)
                    if (staged[i - 1].Right + Clearance > staged[i].Left + Epsilon)
                        throw Geometry(index, staged[i - 1].Name + "/" + staged[i].Name, "overlapping regions");
                var ascentMax = staged.Max(item => item.Ascent);
                var descentMax = staged.Max(item => item.Descent);
                if (ascentMax + descentMax + 2 * Clearance > options.Margin + Epsilon)
                    throw Geometry(index, topMargin ? "Header" : "Footer", "margin is too shallow");
                var baseline = topMargin
                    ? options.PageHeight - (options.Margin - ascentMax - descentMax) / 2 - ascentMax
                    : (options.Margin - ascentMax - descentMax) / 2 + descentMax;
                var bodyEdge = topMargin ? baseline - descentMax : baseline + ascentMax;

                void CheckBody(double top, double bottom)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (topMargin ? top + Clearance > bodyEdge + Epsilon
                        : bottom < bodyEdge + Clearance - Epsilon)
                        throw Geometry(index, topMargin ? "Header" : "Footer", "collides with body ink");
                }
                for (var i = 0; i < bodyRuns; i++)
                {
                    var run = page.Runs[i];
                    CheckBody(run.Baseline + (run.Font?.YMax1000 ?? 805) * run.Style.FontSize / 1000,
                        run.Baseline + (run.Font?.YMin1000 ?? -250) * run.Style.FontSize / 1000);
                }
                foreach (var disc in page.Discs) CheckBody(disc.Y + disc.Radius, disc.Y - disc.Radius);
                foreach (var image in page.Images)
                    CheckBody(options.PageHeight - image.Top, options.PageHeight - image.Top - image.Height);
                foreach (var box in page.PaintBoxes)
                    if (box.CommandCount > 0)
                        CheckBody(options.PageHeight - box.Top, options.PageHeight - box.Top - box.Height);
                void Charge(int count, int sourceOffset)
                {
                    if (count > options.MaxDisplayCommands - displayCommands)
                        throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.");
                    displayCommands += count;
                }
                foreach (var entry in staged)
                {
                    cancellation.ThrowIfCancellationRequested();
                    AppendLine(entry.Line, entry.X, baseline, page.Runs, Charge, cancellation);
                }
            }
        }
    }

    private static string Expand(string template, int page, int total, string field, CancellationToken cancellation)
    {
        // Input lexical defects win over brace grammar/glyph diagnostics.
        for (var i = 0; i < template.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(template.AsSpan(i), out var rune, out var consumed) != OperationStatus.Done)
                throw new FactsPdfException("FPDF1304", $"{field} at character {i}: invalid UTF-16.");
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                throw new FactsPdfException("FPDF1305", $"{field} at character {i}: expected single-line text.");
            i += consumed;
        }
        var result = new StringBuilder(template.Length);
        for (var i = 0; i < template.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            var c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                { result.Append('{'); i += 2; continue; }
                if (template.AsSpan(i).StartsWith("{pages}".AsSpan(), StringComparison.Ordinal))
                { result.Append(total.ToString(CultureInfo.InvariantCulture)); i += 7; continue; }
                if (template.AsSpan(i).StartsWith("{page}".AsSpan(), StringComparison.Ordinal))
                { result.Append(page.ToString(CultureInfo.InvariantCulture)); i += 6; continue; }
                throw TemplateError(field, i);
            }
            if (c == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                { result.Append('}'); i += 2; continue; }
                throw TemplateError(field, i);
            }
            result.Append(c); i++;
        }
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static FactsPdfException TemplateError(string field, int position)
        => new("FPDF1306", $"{field} at character {position}: invalid page text template.");
    private static FactsPdfException Geometry(int index, string region, string detail)
        => new("FPDF1302", $"Page {index + 1}, {region}: {detail}.");
}
