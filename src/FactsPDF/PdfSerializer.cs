using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class PdfSerializer
{
    public static MemoryStream Build(List<LayoutPage> pages, PdfOptions o, CancellationToken cancellation)
    {
        var output = new MemoryStream(Math.Min(4096, o.MaxOutputBytes));
        var offsets = new List<long> { 0 };
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
                foreach (var run in pages[i].Runs)
                {
                    var color = run.Style.Color;
                    var hex = System.Convert.ToHexString(Encoding.ASCII.GetBytes(run.Text));
                    contents.Append($"BT /F1 {N(run.Style.FontSize)} Tf {N(color.R)} {N(color.G)} {N(color.B)} rg ");
                    contents.Append($"1 0 0 1 {N(run.X)} {N(run.Baseline)} Tm <{hex}> Tj ET\n");
                    if (contents.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
                }
                var content = contents.ToString();
                Object(4 + 2 * i, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(o.PageWidth)} {N(o.PageHeight)}] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + 2 * i} 0 R >>");
                Object(5 + 2 * i, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
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
