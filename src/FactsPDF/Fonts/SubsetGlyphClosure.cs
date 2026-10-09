using static FactsPDF.SfntDirectory;

namespace FactsPDF;

/// <summary>Bounded acyclic dependency closure for the glyphs retained in an embedded subset.</summary>
internal sealed class SubsetGlyphClosure
{
    internal readonly record struct Component(int GlyphFieldOffset, ushort Glyph);
    private readonly SfntDirectory sfnt;
    private readonly CancellationToken token;
    private readonly int[] offsets;
    private readonly byte[] states;
    private readonly int[] heights;
    private readonly Component[]?[] references;
    private int edges;
    internal int GlyphCount => states.Length;
    internal int MaxDepth { get; private set; }
    internal int MaxComponents { get; private set; }

    internal SubsetGlyphClosure(SfntDirectory sfnt, CancellationToken token)
    {
        this.sfnt = sfnt; this.token = token;
        var count = U16(sfnt.Table("maxp", 6), 4); states = new byte[count]; heights = new int[count]; references = new Component[count][];
        offsets = new int[count + 1]; var longOffsets = S16(sfnt.Table("head", 54), 50) == 1;
        var loca = sfnt.Table("loca", (count + 1) * (longOffsets ? 4 : 2));
        for (var i = 0; i <= count; i++) offsets[i] = longOffsets ? checked((int)U32(loca, i * 4)) : U16(loca, i * 2) * 2;
    }
    internal ReadOnlySpan<byte> Glyph(ushort id) => sfnt.Table("glyf", 0).Slice(offsets[id], offsets[id + 1] - offsets[id]);
    internal Component[] Components(ushort id) => references[id] ?? [];
    internal ushort[] Selected() => Enumerable.Range(0, GlyphCount).Where(i => states[i] == 2).Select(i => (ushort)i).ToArray();

    internal int Include(ushort id, int depth = 0)
    {
        token.ThrowIfCancellationRequested(); Check(id < GlyphCount, "Composite references an absent glyph.");
        Check(depth <= 64, "Composite nesting exceeds 64."); Check(states[id] != 1, "Cyclic composite glyph graph.");
        if (states[id] == 2) return heights[id];
        states[id] = 1; var components = ReadComponents(Glyph(id)); references[id] = components;
        var height = 0;
        foreach (var child in components) height = Math.Max(height, 1 + Include(child.Glyph, depth + 1));
        Check(height <= 64, "Composite dependency depth exceeds 64.");
        heights[id] = height; states[id] = 2; MaxDepth = Math.Max(MaxDepth, height); MaxComponents = Math.Max(MaxComponents, components.Length);
        return height;
    }
    private Component[] ReadComponents(ReadOnlySpan<byte> glyph)
    {
        if (glyph.IsEmpty) return [];
        Check(glyph.Length >= 10, "Truncated glyph header."); if (S16(glyph, 0) >= 0) return [];
        var result = new List<Component>(); var at = 10; var instructions = false; ushort flags;
        do
        {
            token.ThrowIfCancellationRequested(); Check(at + 4 <= glyph.Length, "Truncated composite component.");
            flags = U16(glyph, at); var child = U16(glyph, at + 2);
            Check((flags & 0xe010) == 0, "Reserved composite flags are unsupported.");
            var scales = flags & (8 | 64 | 128); Check(scales is 0 or 8 or 64 or 128, "Conflicting composite transforms.");
            Check((flags & 0x1800) != 0x1800, "Conflicting composite offset flags.");
            result.Add(new(at + 2, child));
            at += 4 + ((flags & 1) != 0 ? 4 : 2) + (scales switch { 8 => 2, 64 => 4, 128 => 8, _ => 0 });
            Check(at <= glyph.Length, "Truncated composite arguments or transform."); instructions |= (flags & 256) != 0;
            Check(result.Count <= 1024 && ++edges <= 1_000_000, "Composite component budget exceeded.");
        } while ((flags & 32) != 0);
        if (instructions)
        { Check(at + 2 <= glyph.Length, "Missing composite instruction length."); Check(at + 2L + U16(glyph, at) <= glyph.Length, "Truncated composite instructions."); }
        return result.ToArray();
    }
    internal static void Check(bool valid, string message)
    { if (!valid) throw new FactsPdfException("FPDF1506", message); }
}
