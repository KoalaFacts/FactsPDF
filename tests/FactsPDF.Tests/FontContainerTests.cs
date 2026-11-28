using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The font containers Chrome loads besides a plain sfnt: TrueType collections, WOFF 1.0 and WOFF 2.0
/// (with and without the glyf/loca and hmtx transforms). Each container is built here from the
/// synthetic fixture font, so the decoded font must measure, map and outline exactly like it.
/// </summary>
[TestFixture]
public sealed class FontContainerTests
{
    private static byte[] Plain(int advance = 600)
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        return bytes;
    }

    private static void AssertSameFont(PdfFont decoded, PdfFont original)
    {
        Assert.Multiple(() =>
        {
            Assert.That(decoded.PostScriptName, Is.EqualTo(original.PostScriptName));
            Assert.That(decoded.Ascent1000, Is.EqualTo(original.Ascent1000));
            Assert.That(decoded.Descent1000, Is.EqualTo(original.Descent1000));
            Assert.That(decoded.XHeight1000, Is.EqualTo(original.XHeight1000));
            foreach (var scalar in new[] { 32, 65, 66, 87, 105, 0x4e2d, 0x20000 })
            {
                var glyph = original.GlyphFor(scalar);
                Assert.That(decoded.GlyphFor(scalar), Is.EqualTo(glyph), $"U+{scalar:X4}");
                Assert.That(decoded.Width1000(glyph), Is.EqualTo(original.Width1000(glyph)), $"U+{scalar:X4}");
                Assert.That(decoded.GlyphVerticalBounds1000(glyph), Is.EqualTo(original.GlyphVerticalBounds1000(glyph)), $"U+{scalar:X4}");
            }
        });
    }

    [Test]
    public void WoffDecodesToTheSameFont()
    {
        var plain = Plain();
        var font = PdfFont.FromBytes(Woff(plain));
        AssertSameFont(font, PdfFont.FromBytes(plain));
        Assert.That(font.ByteLength, Is.EqualTo(plain.Length), "The decoded sfnt, not the WOFF file, is what is embedded and budgeted.");
    }

    [Test]
    public void WoffStreamAndFileLoadersDecodeToo()
    {
        var plain = Plain();
        using var stream = new MemoryStream(Woff(plain));
        AssertSameFont(PdfFont.FromStream(stream), PdfFont.FromBytes(plain));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Woff2DecodesToTheSameFont(bool transformGlyf, bool transformHmtx)
    {
        var plain = Plain();
        var font = PdfFont.FromBytes(Woff2(plain, transformGlyf, transformHmtx));
        AssertSameFont(font, PdfFont.FromBytes(plain));
        if (transformGlyf)
            Assert.That(Outlines(font), Is.EqualTo(Outlines(PdfFont.FromBytes(plain))),
                "The reconstructed glyf must hold every original point, on-curve flag and contour end.");
    }

    [Test]
    public void Woff2TextLaysOutAndEmbedsLikeThePlainFont()
    {
        var plain = PdfFont.FromBytes(Plain(700));
        var web = PdfFont.FromBytes(Woff2(Plain(700), transformGlyf: true, transformHmtx: true));
        static IEnumerable<(string, double)> Runs(PdfFont font)
        {
            var options = new PdfOptions { Fonts = [font] };
            return TextLayout.Layout(HtmlDocumentReader.ReadTree("<p>A BA iW</p>", options, default), options, default)[0].Runs
                .Select(run => (run.Text, run.X));
        }
        Assert.That(Runs(web), Is.EqualTo(Runs(plain)));
        var pdf = Encoding.Latin1.GetString(Pdf.Write().WithFonts(f => f.Regular(web).Subset()).ToBytes("<p>A BA</p>"));
        Assert.That(pdf, Does.Contain("/FontFile2"));
    }

    [Test]
    public void ACollectionExposesEachFace()
    {
        var collection = Collection(Plain(300), Plain(900));
        Assert.That(PdfFont.CountFaces(collection), Is.EqualTo(2));
        Assert.That(PdfFont.CountFaces(Plain()), Is.EqualTo(1));
        Assert.That(PdfFont.FromBytes(collection).Width1000(2), Is.EqualTo(300), "FromBytes reads face 0, as @font-face does.");
        Assert.That(PdfFont.FromCollection(collection, 1).Width1000(2), Is.EqualTo(900));
        AssertSameFont(PdfFont.FromCollection(collection, 1), PdfFont.FromBytes(Plain(900)));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.FromCollection(collection, 2))!.Code, Is.EqualTo("FPDF1502"));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.FromCollection(Plain(), 1))!.Code, Is.EqualTo("FPDF1502"));
    }

    [Test]
    public void CffOutlinesStayUnsupported()
    {
        var data = Plain(); Encoding.ASCII.GetBytes("OTTO").CopyTo(data, 0);
        var error = Assert.Throws<FactsPdfException>(() => PdfFont.FromBytes(data))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1502"));
        Assert.That(error.Message, Does.Contain("CFF"));
    }

    [Test]
    public void VariableFontsStayUnsupported()
    {
        var data = Plain(); var record = 12 + 16 * Array.FindIndex(Enumerable.Range(0, 10).ToArray(),
            i => Encoding.ASCII.GetString(data, 12 + 16 * i, 4) == "post");
        Encoding.ASCII.GetBytes("fvar").CopyTo(data, record);
        var error = Assert.Throws<FactsPdfException>(() => PdfFont.FromBytes(data))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1502"));
        Assert.That(error.Message, Does.Contain("Variable"));
    }

    [TestCase("wOFF")]
    [TestCase("wOF2")]
    [TestCase("ttcf")]
    public void CorruptContainersFailAsInvalidFonts(string signature)
    {
        var data = Plain(); Encoding.ASCII.GetBytes(signature).CopyTo(data, 0);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.FromBytes(data))!.Code, Is.AnyOf("FPDF1501", "FPDF1502"));
    }

    [Test]
    public void AWoff2CollectionIsRefused()
    {
        var woff2 = Woff2(Plain(), false, false);
        BinaryPrimitives.WriteUInt32BigEndian(woff2.AsSpan(4), 0x74746366);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.FromBytes(woff2))!.Code, Is.EqualTo("FPDF1502"));
    }

    [Test]
    public void DecodedSizeCountsAgainstTheByteLimit()
    {
        var woff2 = Woff2(Plain(), true, true);
        Assert.That(woff2.Length, Is.LessThan(Plain().Length));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.FromBytes(woff2, woff2.Length + 1))!.Code, Is.EqualTo("FPDF1503"));
    }

    // ---- container builders (test-side encoders of the published formats) ----

    private static List<(string Tag, byte[] Data)> Tables(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new List<(string, byte[])>();
        for (var i = 0; i < count; i++)
        {
            var record = 12 + 16 * i;
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12));
            tables.Add((Encoding.ASCII.GetString(font, record, 4), font.AsSpan(offset, length).ToArray()));
        }
        return tables;
    }

    internal static byte[] Woff(byte[] font)
    {
        var tables = Tables(font);
        using var body = new MemoryStream();
        var directory = new byte[20 * tables.Count];
        var offset = 44 + directory.Length;
        for (var i = 0; i < tables.Count; i++)
        {
            var (tag, data) = tables[i];
            using var packed = new MemoryStream();
            using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(data);
            var stored = packed.Length < data.Length ? packed.ToArray() : data;
            Encoding.ASCII.GetBytes(tag).CopyTo(directory, 20 * i);
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan(20 * i + 4), (uint)(offset + body.Length));
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan(20 * i + 8), (uint)stored.Length);
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan(20 * i + 12), (uint)data.Length);
            body.Write(stored);
            while (body.Length % 4 != 0) body.WriteByte(0);
        }
        var file = new byte[44 + directory.Length + body.Length];
        Encoding.ASCII.GetBytes("wOFF").CopyTo(file, 0);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12), (ushort)tables.Count);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(16), (uint)font.Length);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(20), 1);
        directory.CopyTo(file, 44);
        body.ToArray().CopyTo(file, 44 + directory.Length);
        return file;
    }

    private static readonly string[] KnownTags = ["cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca"];

    internal static byte[] Woff2(byte[] font, bool transformGlyf, bool transformHmtx)
    {
        var tables = Tables(font);
        var glyf = tables.Find(t => t.Tag == "glyf").Data; var loca = tables.Find(t => t.Tag == "loca").Data;
        var glyphCount = BinaryPrimitives.ReadUInt16BigEndian(tables.Find(t => t.Tag == "maxp").Data.AsSpan(4));
        var metrics = BinaryPrimitives.ReadUInt16BigEndian(tables.Find(t => t.Tag == "hhea").Data.AsSpan(34));
        using var directory = new MemoryStream();
        using var stream = new MemoryStream();
        foreach (var (tag, data) in tables)
        {
            var index = Array.IndexOf(KnownTags, tag);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), tag);
            byte[] stored; int version; var transformed = false;
            if (tag is "glyf" or "loca")
            {
                transformed = transformGlyf; version = transformGlyf ? 0 : 3;
                stored = !transformGlyf ? data : tag == "glyf" ? TransformGlyf(glyf, loca, glyphCount) : [];
            }
            else if (tag == "hmtx" && transformHmtx)
            {
                transformed = true; version = 1;
                stored = new byte[1 + metrics * 2]; stored[0] = 3;
                for (var i = 0; i < metrics; i++) Array.Copy(data, i * 4, stored, 1 + i * 2, 2);
            }
            else { version = 0; stored = data; }
            directory.WriteByte((byte)(index | version << 6));
            Base128(directory, (uint)data.Length);
            if (transformed) Base128(directory, (uint)stored.Length);
            stream.Write(stored);
        }
        var raw = stream.ToArray();
        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(raw.Length)];
        Assert.That(BrotliEncoder.TryCompress(raw, compressed, out var written), Is.True);
        var file = new byte[48 + directory.Length + written];
        Encoding.ASCII.GetBytes("wOF2").CopyTo(file, 0);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12), (ushort)tables.Count);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(16), (uint)font.Length);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(20), (uint)written);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(24), 1);
        directory.ToArray().CopyTo(file, 48);
        compressed.AsSpan(0, written).CopyTo(file.AsSpan(48 + (int)directory.Length));
        return file;
    }

    private static void Base128(Stream output, uint value)
    {
        var bytes = new List<byte>();
        do { bytes.Insert(0, (byte)(value & 0x7f)); value >>= 7; } while (value != 0);
        for (var i = 0; i < bytes.Count - 1; i++) bytes[i] |= 0x80;
        output.Write(bytes.ToArray());
    }

    private sealed record Outline(short[] Box, int[] EndPoints, (int X, int Y, bool On)[] Points);

    // A simple-glyph parser over a long-loca glyf table: the reference both sides are compared by.
    private static Outline? Parse(byte[] glyf, int start, int end)
    {
        if (start == end) return null;
        var span = glyf.AsSpan(start, end - start);
        short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(span[at..]);
        var contours = S16(0);
        var box = new[] { S16(2), S16(4), S16(6), S16(8) };
        var ends = Enumerable.Range(0, contours).Select(i => (int)(ushort)S16(10 + 2 * i)).ToArray();
        var at = 10 + 2 * contours;
        at += 2 + (ushort)S16(at);
        var count = ends[^1] + 1;
        var flags = new byte[count];
        for (var i = 0; i < count;)
        {
            var flag = span[at++]; flags[i++] = flag;
            if ((flag & 8) != 0) for (var repeat = span[at++]; repeat > 0; repeat--) flags[i++] = flag;
        }
        var xs = new int[count]; var ys = new int[count];
        int Coordinate(byte flag, int shortBit, int sameBit)
        {
            if ((flag & shortBit) != 0) { var v = span[at++]; return (flag & sameBit) != 0 ? v : -v; }
            if ((flag & sameBit) != 0) return 0;
            var w = S16(at); at += 2; return w;
        }
        for (int i = 0, x = 0; i < count; i++) xs[i] = x += Coordinate(flags[i], 2, 16);
        for (int i = 0, y = 0; i < count; i++) ys[i] = y += Coordinate(flags[i], 4, 32);
        return new(box, ends, Enumerable.Range(0, count).Select(i => (xs[i], ys[i], (flags[i] & 1) != 0)).ToArray());
    }

    private static string Outlines(PdfFont font)
    {
        var data = font.Data.ToArray(); var tables = Tables(data);
        var glyf = tables.Find(t => t.Tag == "glyf").Data; var loca = tables.Find(t => t.Tag == "loca").Data;
        var builder = new StringBuilder();
        for (var g = 0; g + 1 < loca.Length / 4; g++)
        {
            var outline = Parse(glyf, (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(g * 4)), (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(g * 4 + 4)));
            builder.Append(g).Append(':');
            if (outline is not null)
                builder.Append(string.Join(',', outline.Box)).Append('|').Append(string.Join(',', outline.EndPoints)).Append('|')
                    .Append(string.Join(' ', outline.Points.Select(p => $"{p.X},{p.Y},{(p.On ? 1 : 0)}")));
            builder.Append(';');
        }
        return builder.ToString();
    }

    // WOFF2 section 5.1, encoder side: contour counts, point counts, flags with 4-byte triplets,
    // instruction lengths and an explicit bounding box for glyph 2 (computed for the others).
    private static byte[] TransformGlyf(byte[] glyf, byte[] loca, int glyphCount)
    {
        using var contours = new MemoryStream(); using var points = new MemoryStream(); using var flags = new MemoryStream();
        using var glyphs = new MemoryStream(); using var boxes = new MemoryStream();
        var bitmap = new byte[((glyphCount + 31) >> 5) << 2];
        for (var g = 0; g < glyphCount; g++)
        {
            var outline = Parse(glyf, (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(g * 4)), (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(g * 4 + 4)));
            if (outline is null) { Write16(contours, 0); continue; }
            Write16(contours, outline.EndPoints.Length);
            var previous = -1;
            foreach (var end in outline.EndPoints) { UShort255(points, end - previous); previous = end; }
            int x = 0, y = 0;
            foreach (var (px, py, on) in outline.Points)
            {
                int dx = px - x, dy = py - y; x = px; y = py;
                flags.WriteByte((byte)((on ? 0 : 0x80) | 124 | (dx >= 0 ? 1 : 0) | (dy >= 0 ? 2 : 0)));
                Write16(glyphs, Math.Abs(dx)); Write16(glyphs, Math.Abs(dy));
            }
            UShort255(glyphs, 0);
            if (g == 2)
            {
                bitmap[g >> 3] |= (byte)(0x80 >> (g & 7));
                foreach (var value in outline.Box) Write16(boxes, value);
            }
        }
        byte[][] streams = [contours.ToArray(), points.ToArray(), flags.ToArray(), glyphs.ToArray(), [], [.. bitmap, .. boxes.ToArray()], []];
        using var table = new MemoryStream();
        Write16(table, 0); Write16(table, 0); Write16(table, glyphCount); Write16(table, 1);
        foreach (var s in streams) { var size = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(size, (uint)s.Length); table.Write(size); }
        foreach (var s in streams) table.Write(s);
        return table.ToArray();
    }

    private static void Write16(Stream stream, int value) { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
    private static void UShort255(Stream stream, int value)
    {
        if (value < 253) stream.WriteByte((byte)value);
        else { stream.WriteByte(253); Write16(stream, value); }
    }

    internal static byte[] Collection(params byte[][] fonts)
    {
        var header = 12 + 4 * fonts.Length;
        var file = new byte[header + fonts.Sum(f => f.Length)];
        Encoding.ASCII.GetBytes("ttcf").CopyTo(file, 0);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(8), (uint)fonts.Length);
        var at = header;
        for (var i = 0; i < fonts.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(12 + 4 * i), (uint)at);
            fonts[i].CopyTo(file, at);
            var count = BinaryPrimitives.ReadUInt16BigEndian(fonts[i].AsSpan(4));
            for (var t = 0; t < count; t++)
            {
                var field = file.AsSpan(at + 12 + 16 * t + 8);
                BinaryPrimitives.WriteUInt32BigEndian(field, BinaryPrimitives.ReadUInt32BigEndian(field) + (uint)at);
            }
            at += fonts[i].Length;
        }
        return file;
    }
}
