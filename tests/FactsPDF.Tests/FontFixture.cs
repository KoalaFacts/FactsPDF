using System.Buffers.Binary;
using System.Text;

namespace FactsPDF.Tests;

// Original test data builder: simple rectangular outlines, not a third-party font.
// No font file is distributed. Real typography is independently tested in CI.
internal static class FontFixture
{
    public static byte[] Create(bool format12 = true, bool latinOnly = false, ushort fsType = 0, bool ranged4 = false)
    {
        var map = new SortedDictionary<int, ushort>
        {
            [32] = 1, [65] = 2, [66] = 3, [87] = 5, [105] = 4, [160] = 1,
            [0x3002] = 7, [0x3008] = 8, [0x3009] = 9,
            [0x4e2d] = 6, [0x6587] = 7, [0xff08] = 8, [0xff09] = 9, [0xff0c] = 10,
            [0x20000] = 11
        };
        foreach (var cp in map.Keys.ToArray())
            if ((latinOnly && cp > 255) || (!format12 && cp > 65535)) map.Remove(cp);
        byte[] sub;
        if (format12)
        {
            sub = new byte[16 + map.Count * 12]; U16(sub, 0, 12); U32(sub, 4, (uint)sub.Length); U32(sub, 12, (uint)map.Count);
            var i = 0;
            foreach (var (cp, glyph) in map)
            { var at = 16 + 12 * i++; U32(sub, at, (uint)cp); U32(sub, at + 4, (uint)cp); U32(sub, at + 8, glyph); }
        }
        else
        {
            var n = map.Count + 1;
            sub = new byte[16 + n * 8 + (ranged4 ? map.Count * 2 : 0)];
            U16(sub, 0, 4); U16(sub, 2, sub.Length); U16(sub, 6, n * 2);
            var power = 1; var log = 0; while (power * 2 <= n) { power *= 2; log++; }
            U16(sub, 8, power * 2); U16(sub, 10, log); U16(sub, 12, n * 2 - power * 2);
            var i = 0;
            foreach (var (cp, glyph) in map)
            {
                U16(sub, 14 + i * 2, cp); U16(sub, 16 + n * 2 + i * 2, cp);
                U16(sub, 16 + n * 4 + i * 2, ranged4 ? 2 : unchecked((ushort)(glyph - cp)));
                if (ranged4) { U16(sub, 16 + n * 6 + i * 2, n * 2); U16(sub, 16 + n * 8 + i * 2, unchecked((ushort)(glyph - 2))); }
                i++;
            }
            U16(sub, 14 + (n - 1) * 2, 65535); U16(sub, 16 + n * 2 + (n - 1) * 2, 65535);
            U16(sub, 16 + n * 4 + (n - 1) * 2, 1);
        }
        var cmap = new byte[12 + sub.Length]; U16(cmap, 2, 1); U16(cmap, 4, 3); U16(cmap, 6, format12 ? 10 : 1); U32(cmap, 8, 12); sub.CopyTo(cmap, 12);
        var head = new byte[54]; U32(head, 0, 0x00010000); U32(head, 12, 0x5f0f3cf5); U16(head, 18, 1000); U16(head, 40, 900); U16(head, 42, 700); U16(head, 50, 1);
        var hhea = new byte[36]; U32(hhea, 0, 0x00010000); U16(hhea, 4, 800); U16(hhea, 6, unchecked((ushort)-200)); U16(hhea, 10, 1000); U16(hhea, 18, 1); U16(hhea, 34, 11);
        var maxp = new byte[32]; U32(maxp, 0, 0x00010000); U16(maxp, 4, 12); U16(maxp, 6, 4); U16(maxp, 8, 1); U16(maxp, 14, 1);
        var hmtx = new byte[46]; int[] widths = [600, 300, 600, 600, 200, 900, 1000, 1000, 1000, 1000, 1000];
        for (var i = 0; i < widths.Length; i++) U16(hmtx, i * 4, widths[i]);
        var os2 = new byte[96]; U16(os2, 0, 4); U16(os2, 4, 400); U16(os2, 6, 5); U16(os2, 8, fsType); U16(os2, 62, 128); U16(os2, 68, 800); U16(os2, 70, unchecked((ushort)-200)); U16(os2, 74, 800); U16(os2, 76, 200); U16(os2, 86, 500); U16(os2, 88, 700);
        var rectangle = new byte[34]; U16(rectangle, 0, 1); U16(rectangle, 6, 500); U16(rectangle, 8, 700); U16(rectangle, 10, 3);
        for (var i = 0; i < 4; i++) rectangle[14 + i] = 1;
        U16(rectangle, 20, 500); U16(rectangle, 24, unchecked((ushort)-500)); U16(rectangle, 30, 700);
        using var glyfStream = new MemoryStream(); var loca = new byte[13 * 4];
        for (var i = 0; i < 12; i++) { U32(loca, i * 4, (uint)glyfStream.Length); if (i != 1) glyfStream.Write(rectangle); }
        U32(loca, 48, (uint)glyfStream.Length);
        var label = Encoding.BigEndianUnicode.GetBytes("FactsPDFTest"); var name = new byte[18 + label.Length];
        U16(name, 2, 1); U16(name, 4, 18); U16(name, 6, 3); U16(name, 8, 1); U16(name, 10, 0x409); U16(name, 12, 6); U16(name, 14, label.Length); label.CopyTo(name, 18);
        var post = new byte[32]; U32(post, 0, 0x00030000);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        { ["OS/2"] = os2, ["cmap"] = cmap, ["glyf"] = glyfStream.ToArray(), ["head"] = head, ["hhea"] = hhea, ["hmtx"] = hmtx, ["loca"] = loca, ["maxp"] = maxp, ["name"] = name, ["post"] = post };
        var size = 12 + tables.Count * 16 + tables.Values.Sum(t => (t.Length + 3) & ~3); var font = new byte[size];
        U32(font, 0, 0x00010000); U16(font, 4, tables.Count); U16(font, 6, 128); U16(font, 8, 3); U16(font, 10, 32);
        var record = 12; var offset = 12 + tables.Count * 16;
        foreach (var (tag, bytes) in tables)
        {
            Encoding.ASCII.GetBytes(tag).CopyTo(font, record); U32(font, record + 4, Checksum(bytes)); U32(font, record + 8, (uint)offset); U32(font, record + 12, (uint)bytes.Length);
            bytes.CopyTo(font, offset); record += 16; offset += (bytes.Length + 3) & ~3;
        }
        U32(font, Table(font, "head") + 8, unchecked(0xb1b0afbau - Checksum(font)));
        return font;
    }

    public static int Table(byte[] font, string name)
    {
        for (var at = 12; at < 12 + BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4)) * 16; at += 16)
            if (Encoding.ASCII.GetString(font, at, 4) == name) return checked((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at + 8)));
        throw new InvalidOperationException(name);
    }
    public static void U16(byte[] b, int o, int v) => BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(o, 2), unchecked((ushort)v));
    public static void U32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o, 4), v);
    private static uint Checksum(byte[] bytes)
    {
        uint sum = 0;
        for (var at = 0; at < bytes.Length; at += 4)
        { uint word = 0; for (var i = 0; i < 4; i++) word = (word << 8) | (at + i < bytes.Length ? bytes[at + i] : 0u); sum = unchecked(sum + word); }
        return sum;
    }
}
