using static FactsPDF.SfntDirectory;

namespace FactsPDF;

/// <summary>Bounded acyclic dependency closure for the glyphs retained in an embedded subset.</summary>
internal sealed class SubsetGlyphClosure
{
    internal readonly record struct Component(int GlyphFieldOffset, ushort Glyph);
    private struct Node { internal bool Done; internal int Height; internal Component[] Components; }
    private readonly SfntDirectory sfnt;
    private readonly CancellationToken token;
    private readonly bool longOffsets;
    // Only the glyphs the closure reaches get state, so the cost follows the subset, not the font's glyph count.
    private readonly Dictionary<ushort, Node> nodes = [];
    private int edges;
    internal int GlyphCount { get; }
    internal int MaxDepth { get; private set; }
    internal int MaxComponents { get; private set; }

    internal SubsetGlyphClosure(SfntDirectory sfnt, CancellationToken token)
    {
        this.sfnt = sfnt; this.token = token;
        GlyphCount = U16(sfnt.Table("maxp", 6), 4); longOffsets = S16(sfnt.Table("head", 54), 50) == 1;
        _ = sfnt.Table("loca", (GlyphCount + 1) * (longOffsets ? 4 : 2));
    }
    // PdfFont validated every loca entry (ascending, within glyf), so offsets are read per glyph on demand.
    private int Offset(int id)
    {
        var loca = sfnt.Table("loca", 0);
        return longOffsets ? checked((int)U32(loca, id * 4)) : U16(loca, id * 2) * 2;
    }
    internal ReadOnlySpan<byte> Glyph(ushort id)
    { var start = Offset(id); return sfnt.Table("glyf", 0).Slice(start, Offset(id + 1) - start); }
    internal Component[] Components(ushort id) => nodes.TryGetValue(id, out var node) ? node.Components ?? [] : [];
    internal ushort[] Selected()
    {
        var selected = new ushort[nodes.Count]; var count = 0;
        foreach (var (id, node) in nodes) if (node.Done) selected[count++] = id;
        Array.Sort(selected, 0, count);
        return count == selected.Length ? selected : selected[..count];
    }

    internal int Include(ushort id, int depth = 0)
    {
        token.ThrowIfCancellationRequested(); Check(id < GlyphCount, "Composite references an absent glyph.");
        Check(depth <= 64, "Composite nesting exceeds 64.");
        var known = nodes.TryGetValue(id, out var existing); Check(!known || existing.Done, "Cyclic composite glyph graph.");
        if (known) return existing.Height;
        var components = ReadComponents(Glyph(id)); nodes[id] = new Node { Components = components };
        var height = 0;
        foreach (var child in components) height = Math.Max(height, 1 + Include(child.Glyph, depth + 1));
        Check(height <= 64, "Composite dependency depth exceeds 64.");
        nodes[id] = new Node { Done = true, Height = height, Components = components };
        MaxDepth = Math.Max(MaxDepth, height); MaxComponents = Math.Max(MaxComponents, components.Length);
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
            Check(result.Count > 0 || (flags & 2) != 0, "The first composite component must use XY positioning.");
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
