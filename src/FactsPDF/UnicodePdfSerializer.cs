using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace FactsPDF;

internal static class UnicodePdfSerializer
{
    private sealed class FontUse(PdfFont font, int index)
    {
        internal PdfFont Font { get; } = font;
        internal int ObjectId => 3 + index * 6;
        internal string Resource => "F" + (index + 1).ToString(CultureInfo.InvariantCulture);
        internal Dictionary<int, ushort> Codes { get; } = [];
        internal List<(int Scalar, ushort Glyph, double Width)> Characters { get; } = [];
        internal void Register(int scalar)
        {
            if (Codes.ContainsKey(scalar)) return;
            if (Characters.Count >= 65535) throw new FactsPdfException("FPDF1503", "Too many distinct scalars for a single embedded font resource.");
            var glyph = Font.GlyphFor(scalar);
            if (glyph == 0) throw new FactsPdfException("FPDF1504", "A used character has no glyph in its assigned font.");
            Codes.Add(scalar, checked((ushort)(Characters.Count + 1)));
            Characters.Add((scalar, glyph, Font.Width1000(glyph)));
        }
    }
    private sealed class LimitedBuffer(int limit, CancellationToken cancellation) : MemoryStream(Math.Min(4096, limit))
    {
        private void Check(int count)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Position + count > limit) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    }

    internal static MemoryStream Build(List<LayoutPage> pages, PdfOptions o, CancellationToken cancellation)
    {
        var fonts = new List<FontUse>(); var byFont = new Dictionary<PdfFont, FontUse>();
        foreach (var page in pages)
            foreach (var run in page.Runs)
            {
                cancellation.ThrowIfCancellationRequested();
                var font = run.Font ?? throw new InvalidOperationException("Embedded text has no assigned font.");
                if (!byFont.TryGetValue(font, out var use)) { use = new FontUse(font, fonts.Count); fonts.Add(use); byFont.Add(font, use); }
                foreach (var rune in run.Text.EnumerateRunes()) use.Register(rune.Value);
            }
        var output = new LimitedBuffer(o.MaxOutputBytes, cancellation); var offsets = new List<long> { 0 };
        void Write(string value)
        {
            Span<byte> bytes = stackalloc byte[4096];
            for (var at = 0; at < value.Length;)
            { var length = Math.Min(4096, value.Length - at); Encoding.ASCII.GetBytes(value.AsSpan(at, length), bytes); output.Write(bytes[..length]); at += length; }
        }
        void Begin(int id)
        {
            if (id != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            offsets.Add(output.Position); Write(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        }
        void Obj(int id, string value) { Begin(id); Write(value); Write("\nendobj\n"); }
        void StreamObject(int id, ReadOnlySpan<byte> bytes, string extra = "")
        {
            Begin(id); Write("<< /Length " + bytes.Length.ToString(CultureInfo.InvariantCulture) + extra + " >>\nstream\n");
            for (var at = 0; at < bytes.Length; at += Math.Min(16384, bytes.Length - at)) output.Write(bytes.Slice(at, Math.Min(16384, bytes.Length - at)));
            Write("\nendstream\nendobj\n");
        }
        try
        {
            Write("%PDF-1.7\n"); output.Write(new byte[] { 0x25, 0xe2, 0xe3, 0xcf, 0xd3, 0x0a });
            Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
            var firstPage = 3 + fonts.Count * 6;
            Obj(2, $"<< /Type /Pages /Count {pages.Count.ToString(CultureInfo.InvariantCulture)} /Kids [" + string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => (firstPage + 2 * i).ToString(CultureInfo.InvariantCulture) + " 0 R")) + "] >>");
            foreach (var use in fonts)
            {
                cancellation.ThrowIfCancellationRequested(); var id = use.ObjectId; var font = use.Font;
                var subset = o.SubsetFonts && font.AllowsSubsetting
                    ? TrueTypeSubsetter.Create(font, use.Characters.Select(c => c.Scalar).ToArray(), cancellation) : null;
                var name = subset?.PostScriptName ?? font.PostScriptName;
                ReadOnlySpan<byte> embeddedData = subset is null ? font.Data : subset.Bytes.AsSpan();
                Obj(id, FormattableString.Invariant($"<< /Type /Font /Subtype /Type0 /BaseFont /{name} /Encoding /Identity-H /DescendantFonts [{id + 1} 0 R] /ToUnicode {id + 4} 0 R >>"));
                var widths = string.Join(" ", use.Characters.Select(c => N(c.Width)));
                Obj(id + 1, FormattableString.Invariant($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{name} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {id + 2} 0 R /CIDToGIDMap {id + 5} 0 R /DW 1000 /W [1 [{widths}]] >>"));
                Obj(id + 2, FormattableString.Invariant($"<< /Type /FontDescriptor /FontName /{name} /Flags 4 /FontBBox [{N(font.XMin1000)} {N(font.YMin1000)} {N(font.XMax1000)} {N(font.YMax1000)}] /ItalicAngle {N(font.ItalicAngle)} /Ascent {N(font.Ascent1000)} /Descent {N(font.Descent1000)} /CapHeight {N(font.CapHeight1000)} /StemV 80 /FontFile2 {id + 3} 0 R >>"));
                using (var compressed = new LimitedBuffer(o.MaxOutputBytes, cancellation))
                {
                    using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                        for (var at = 0; at < embeddedData.Length; at += Math.Min(16384, embeddedData.Length - at))
                        { cancellation.ThrowIfCancellationRequested(); zlib.Write(embeddedData.Slice(at, Math.Min(16384, embeddedData.Length - at))); }
                    StreamObject(id + 3, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)), " /Filter /FlateDecode /Length1 " + embeddedData.Length.ToString(CultureInfo.InvariantCulture));
                }
                StreamObject(id + 4, Encoding.ASCII.GetBytes(ToUnicode(use)));
                var map = new byte[(use.Characters.Count + 1) * 2];
                for (var i = 0; i < use.Characters.Count; i++)
                {
                    var oldGlyph = use.Characters[i].Glyph;
                    BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan((i + 1) * 2, 2), subset is null ? oldGlyph : subset.GlyphMap[oldGlyph]);
                }
                StreamObject(id + 5, map);
            }
            var resources = string.Join(" ", fonts.Select(f => "/" + f.Resource + " " + f.ObjectId.ToString(CultureInfo.InvariantCulture) + " 0 R"));
            for (var i = 0; i < pages.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested(); var content = new StringBuilder();
                foreach (var run in pages[i].Runs)
                {
                    var use = byFont[run.Font!]; var color = run.Style.Color;
                    content.Append($"BT /{use.Resource} {N(run.Style.FontSize)} Tf {N(color.R)} {N(color.G)} {N(color.B)} rg 1 0 0 1 {N(run.X)} {N(run.Baseline)} Tm <");
                    foreach (var rune in run.Text.EnumerateRunes()) content.Append(use.Codes[rune.Value].ToString("X4", CultureInfo.InvariantCulture));
                    content.Append("> Tj ET\n");
                    if (content.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
                }
                Obj(firstPage + 2 * i, FormattableString.Invariant($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(o.PageWidth)} {N(o.PageHeight)}] /Resources << /Font << {resources} >> >> /Contents {firstPage + 2 * i + 1} 0 R >>"));
                StreamObject(firstPage + 2 * i + 1, Encoding.ASCII.GetBytes(content.ToString()));
            }
            var xref = output.Position;
            Write("xref\n0 " + offsets.Count.ToString(CultureInfo.InvariantCulture) + "\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1)) Write(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            Write(FormattableString.Invariant($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private static string ToUnicode(FontUse use)
    {
        var text = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /FactsPDF-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        for (var start = 0; start < use.Characters.Count; start += 100)
        {
            var count = Math.Min(100, use.Characters.Count - start);
            text.Append(count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            for (var i = start; i < start + count; i++)
            {
                var unicode = System.Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(char.ConvertFromUtf32(use.Characters[i].Scalar)));
                text.Append('<').Append((i + 1).ToString("X4", CultureInfo.InvariantCulture)).Append("> <").Append(unicode).Append(">\n");
            }
            text.Append("endbfchar\n");
        }
        return text.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
    }
    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
