using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class PdfSerializer
{
    public static MemoryStream Build(List<LayoutPage> pages, PdfOptions o, CancellationToken cancellation)
    {
        var output = new MemoryStream(Math.Min(4096, o.MaxOutputBytes));
        var offsets = new List<long> { 0 };
        var images = new PdfImageWriter(pages, cancellation);
        var firstImage = 4 + 2 * pages.Count;
        void Write(string text)
        {
            if (output.Length + text.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
            Span<byte> buffer = stackalloc byte[4096];
            for (var i = 0; i < text.Length;)
            {
                cancellation.ThrowIfCancellationRequested();
                var count = Math.Min(buffer.Length, text.Length - i);
                var bytes = Encoding.ASCII.GetBytes(text.AsSpan(i, count), buffer);
                output.Write(buffer[..bytes]); i += count;
            }
        }
        void Object(int id, string body)
        {
            if (id != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            offsets.Add(output.Position);
            Write($"{id} 0 obj\n{body}\nendobj\n");
        }
        try
        {
            Write("%PDF-1.7\n%FactsPDF\n");
            Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
            var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{4 + 2 * i} 0 R"));
            Object(2, $"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>");
            Object(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
            for (var i = 0; i < pages.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var contents = new StringBuilder();
                PdfPaintSerializer.Append(contents, pages[i].PaintBoxes, o.PageHeight, o.MaxOutputBytes, cancellation);
                PdfPaintSerializer.AppendDiscs(contents, pages[i].Discs, o.MaxOutputBytes, cancellation);
                images.Append(contents, pages[i], o, cancellation);
                // PDF character spacing is persistent graphics text state
                // across BT/ET. Restore zero explicitly after a tracked run,
                // but preserve byte-identical legacy PDFs that never track.
                var previousTracking = 0d;
                foreach (var run in pages[i].Runs)
                {
                    var color = run.Style.Color;
                    var hex = System.Convert.ToHexString(Encoding.ASCII.GetBytes(run.Text));
                    var tracking = previousTracking == run.Style.LetterSpacing
                        ? "" : $"{N(run.Style.LetterSpacing)} Tc ";
                    previousTracking = run.Style.LetterSpacing;
                    contents.Append($"BT /F1 {N(run.Style.FontSize)} Tf {tracking}{N(color.R)} {N(color.G)} {N(color.B)} rg ");
                    contents.Append($"1 0 0 1 {N(run.X)} {N(run.Baseline)} Tm ");
                    if (run.Style.WordSpacing == 0 || !run.Text.Contains(' '))
                        contents.Append($"<{hex}> Tj ET\n");
                    else
                    {
                        // PDF Tw does not reliably position two-byte CID space codes.
                        // Explicit TJ displacements match the shared glyph-width model.
                        contents.Append('[');
                        var start = 0;
                        for (var j = 0; j < run.Text.Length; j++)
                        {
                            if (run.Text[j] != ' ') continue;
                            contents.Append('<').Append(System.Convert.ToHexString(
                                Encoding.ASCII.GetBytes(run.Text[start..(j + 1)]))).Append("> ");
                            contents.Append(N(-run.Style.WordSpacing * 1000 / run.Style.FontSize)).Append(' ');
                            start = j + 1;
                        }
                        if (start < run.Text.Length)
                            contents.Append('<').Append(System.Convert.ToHexString(
                                Encoding.ASCII.GetBytes(run.Text[start..]))).Append('>');
                        contents.Append("] TJ ET\n");
                    }
                    PdfPaintSerializer.AppendTextDecoration(contents, run, o.MaxOutputBytes);
                    if (contents.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
                }
                var content = contents.ToString();
                Object(4 + 2 * i, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(o.PageWidth)} {N(o.PageHeight)}] /Resources << /Font << /F1 3 0 R >>{images.Resources(pages[i], firstImage, o.MaxOutputBytes, cancellation)} >> /Contents {5 + 2 * i} 0 R >>");
                Object(5 + 2 * i, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
            }
            for (var i = 0; i < images.ObjectCount; i++)
            {
                if (firstImage + i != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
                offsets.Add(output.Position);
                images.WriteObject(output, i, firstImage + i, o.MaxOutputBytes, cancellation);
            }
            var xref = output.Position;
            Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1)) Write(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
