using NUnit.Framework;

namespace FactsPDF.Tests;

// Subsetting keeps state only for the glyphs the closure reaches. Before Z6n it allocated four arrays
// sized by the font's glyph count (state, height, component references and loca offsets) on every
// conversion, about 17 bytes per glyph: 107 KiB for DejaVu Sans, 340 KiB for a 20,000-glyph font.
[TestFixture]
[NonParallelizable]
public sealed class SubsetClosureAllocationTests
{
    private const int ExtraGlyphs = 20_000;
    private static readonly PdfFont Small = PdfFont.FromBytes(FontFixture.Create());
    private static readonly PdfFont Large = PdfFont.FromBytes(FontFixture.Create(emptyGlyphs: ExtraGlyphs));

    private static long AllocatedPerSubset(PdfFont font, int[] scalars)
    {
        for (var i = 0; i < 3; i++) TrueTypeSubsetter.Create(font, scalars);
        var before = GC.GetAllocatedBytesForCurrentThread();
        const int runs = 5;
        for (var i = 0; i < runs; i++) TrueTypeSubsetter.Create(font, scalars);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / runs;
    }

    [AllocationBudget]
    [Test]
    public void AFontsUnusedGlyphsAddOnlyTheGlyphMap()
    {
        int[] scalars = [65, 66, 87, 105];
        var small = AllocatedPerSubset(Small, scalars);
        var large = AllocatedPerSubset(Large, scalars);
        TestContext.Out.WriteLine($"subset: {small} bytes (12 glyphs), {large} bytes ({12 + ExtraGlyphs} glyphs)");
        // The returned old-to-new glyph map is 2 bytes per font glyph; everything else follows the subset.
        Assert.That(large - small, Is.LessThan(ExtraGlyphs * 2 + 4_096));
    }

    [Test]
    public void ExtraUnusedGlyphsDoNotChangeTheSubset()
    {
        int[] scalars = [65, 66, 87, 105, 0x4e2d, 0x20000];
        var small = TrueTypeSubsetter.Create(Small, scalars);
        var large = TrueTypeSubsetter.Create(Large, scalars);
        Assert.That(large.Bytes, Is.EqualTo(small.Bytes));
        Assert.That(large.PostScriptName, Is.EqualTo(small.PostScriptName));
        Assert.That(large.GlyphMap.Length, Is.EqualTo(12 + ExtraGlyphs));
        Assert.That(large.GlyphMap[..12], Is.EqualTo(small.GlyphMap));
    }

    [Test]
    public void TheLastGlyphOfALargeFontStillResolves()
    {
        // The closure reads loca per glyph; the last glyph's end offset is the table's final entry.
        var closure = new SubsetGlyphClosure(Large.Directory, default);
        Assert.That(closure.GlyphCount, Is.EqualTo(12 + ExtraGlyphs));
        Assert.That(closure.Include((ushort)(11 + ExtraGlyphs)), Is.Zero);
        Assert.That(closure.Glyph((ushort)(11 + ExtraGlyphs)).IsEmpty, Is.True);
        Assert.That(closure.Glyph(2).Length, Is.EqualTo(34));
        Assert.That(closure.Selected(), Is.EqualTo(new ushort[] { (ushort)(11 + ExtraGlyphs) }));
        var absent = Assert.Throws<FactsPdfException>(() => closure.Include((ushort)(12 + ExtraGlyphs)));
        Assert.That(absent!.Code, Is.EqualTo("FPDF1506"));
    }
}
