namespace FactsPDF;

internal sealed record FontSubset(byte[] Bytes, ushort[] GlyphMap, string PostScriptName);

internal static class TrueTypeSubsetter
{
    // Deliberate compiling baseline: tests must demonstrate absent subsetting behavior.
    internal static FontSubset Create(PdfFont font, IReadOnlyList<int> scalars,
        CancellationToken cancellation = default)
        => new(font.Data.ToArray(), Enumerable.Range(0, 65536).Select(i => (ushort)i).ToArray(), font.PostScriptName);
}
