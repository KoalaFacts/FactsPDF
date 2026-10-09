using System.Security.Cryptography;
using System.Text;
using static FactsPDF.SfntDirectory;
using static FactsPDF.SubsetSfnt;

namespace FactsPDF;

internal sealed record FontSubset(byte[] Bytes, ushort[] GlyphMap, string PostScriptName);

/// <summary>Independent static TrueType glyph subsetting for this renderer's PDF-only font resources.</summary>
internal static class TrueTypeSubsetter
{
    internal static FontSubset Create(PdfFont font, IReadOnlyList<int> scalars, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(font); ArgumentNullException.ThrowIfNull(scalars); cancellation.ThrowIfCancellationRequested();
        if (!font.AllowsSubsetting) throw new FactsPdfException("FPDF1505", "The supplied font prohibits subsetting.");
        if (scalars.Count > 65535) throw new FactsPdfException("FPDF1503", "Subset character input exceeds 65,535 entries.");
        var sfnt = font.Directory; var closure = new SubsetGlyphClosure(sfnt, cancellation); closure.Include(0);
        var characters = new SortedDictionary<int, ushort>();
        foreach (var scalar in scalars)
        {
            cancellation.ThrowIfCancellationRequested(); var glyph = font.GlyphFor(scalar);
            if (glyph == 0) throw new FactsPdfException("FPDF1504", $"No glyph for requested U+{scalar:X4}.");
            closure.Include(glyph); characters[scalar] = glyph;
        }
        var kept = closure.Selected(); var map = new ushort[closure.GlyphCount];
        for (var i = 0; i < kept.Length; i++) map[kept[i]] = checked((ushort)i);
        foreach (var scalar in characters.Keys.ToArray()) characters[scalar] = map[characters[scalar]];

        var loca = new byte[(kept.Length + 1) * 4]; long glyfLength = 0;
        foreach (var glyph in kept) glyfLength += ((long)closure.Glyph(glyph).Length + 3) & ~3L;
        if (glyfLength > 67_108_864) throw new FactsPdfException("FPDF1503", "Subset glyph data exceeds its byte limit.");
        var glyf = new byte[(int)glyfLength]; var hmtx = new byte[kept.Length * 4]; var position = 0;
        var oldMetrics = U16(sfnt.Table("hhea", 36), 34); var oldHmtx = sfnt.Table("hmtx", 4);
        for (var i = 0; i < kept.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var old = kept[i]; var glyph = closure.Glyph(old); Put32(loca, i * 4, (uint)position);
            glyph.CopyTo(glyf.AsSpan(position));
            foreach (var component in closure.Components(old)) Put16(glyf, position + component.GlyphFieldOffset, map[component.Glyph]);
            position += (glyph.Length + 3) & ~3;
            Put16(hmtx, i * 4, U16(oldHmtx, Math.Min(old, oldMetrics - 1) * 4));
            Put16(hmtx, i * 4 + 2, S16(oldHmtx, old < oldMetrics ? old * 4 + 2 : oldMetrics * 4 + (old - oldMetrics) * 2));
        }
        Put32(loca, kept.Length * 4, (uint)position);
        var head = sfnt.Table("head", 54).ToArray(); Put32(head, 8, 0); Put16(head, 50, 1);
        var hhea = sfnt.Table("hhea", 36).ToArray(); Put16(hhea, 34, kept.Length);
        var maxp = sfnt.Table("maxp", 32).ToArray(); Put16(maxp, 4, kept.Length); Put16(maxp, 28, closure.MaxComponents); Put16(maxp, 30, closure.MaxDepth);
        var post = new byte[32]; if (sfnt.Contains("post")) sfnt.Table("post", 16)[..Math.Min(32, sfnt.Table("post", 16).Length)].CopyTo(post); Put32(post, 0, 0x00030000);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["OS/2"] = sfnt.Table("OS/2", 68).ToArray(), ["cmap"] = Cmap(characters), ["glyf"] = glyf,
            ["head"] = head, ["hhea"] = hhea, ["hmtx"] = hmtx, ["loca"] = loca, ["maxp"] = maxp,
            ["name"] = sfnt.Table("name", 6).ToArray(), ["post"] = post
        };
        // Preserve glyph instructions and their shared programs/data. All unhandled index-bearing tables and signatures are omitted.
        foreach (var tag in new[] { "cvt ", "fpgm", "prep", "gasp" }) if (sfnt.Contains(tag)) tables[tag] = sfnt.Table(tag, 0).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Span<byte> length = stackalloc byte[4];
        foreach (var (tag, bytes) in tables)
        {
            cancellation.ThrowIfCancellationRequested(); hash.AppendData(Encoding.ASCII.GetBytes(tag));
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes);
        }
        var digest = hash.GetHashAndReset(); var prefix = new char[6]; for (var i = 0; i < prefix.Length; i++) prefix[i] = (char)('A' + digest[i] % 26);
        var name = new string(prefix) + "+" + font.PostScriptName[..Math.Min(56, font.PostScriptName.Length)];
        tables["name"] = Rename(tables["name"], name);
        return new(Build(tables, cancellation), map, name);
    }
}
