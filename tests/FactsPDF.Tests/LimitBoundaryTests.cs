using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// R1b: every public resource limit in PdfLimitsSetup is exact at its boundary through the public
// SDK, fails with its documented FactsPdfException code, writes nothing, and stops a flood of
// input far beyond the limit with bounded time and memory.
[TestFixture]
[NonParallelizable]
public sealed class LimitBoundaryTests
{
    private static readonly byte[] FontBytes = FontFixture.Create();
    private static readonly PdfImage First = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"));
    private static readonly PdfImage Second = PdfImage.FromBytes(JpegResourceTests.Bytes("gray"));

    // One case per public limit: the document, the writer it is rendered with, the smallest value of
    // the limit that still renders it, and the code for one less.
    public sealed record LimitCase(string Limit, Func<PdfLimitsSetup, int, PdfLimitsSetup> Set, string Html,
        Func<PdfWriter> Writer, Func<int> Smallest, string Code)
    {
        public override string ToString() => Limit;
    }

    private static PdfWriter Plain() => Pdf.Write();

    private static PdfWriter WithFont() => Pdf.Write().WithFonts(f => f.Regular(PdfFont.FromBytes(FontBytes)));

    private static PdfWriter WithImages() => Pdf.Write().WithImage("a", First).WithImage("b", Second);

    private const string ImagesHtml = "<img src=\"a\"><img src=\"b\">";

    private static string Paragraphs(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p>Paragraph ").Append(i).Append("</p>");
        return html.ToString();
    }

    private static readonly string ManyPages = Paragraphs(160);

    private static int Fixed(int value) => value;

    public static IEnumerable<LimitCase> Cases()
    {
        yield return new("MaxInputCharacters", (l, v) => l.MaxInputCharacters(v), "<p>x</p>", Plain, () => Fixed(8), "FPDF1001");
        yield return new("MaxElements", (l, v) => l.MaxElements(v), "<p>a</p><p>b</p><p>c</p>", Plain, () => Fixed(3), "FPDF1002");
        yield return new("MaxDepth", (l, v) => l.MaxDepth(v), "<div><div><p>x</p></div></div>", Plain, () => Fixed(3), "FPDF1003");
        yield return new("MaxPages", (l, v) => l.MaxPages(v), ManyPages, Plain,
            () => Pages(Plain(), ManyPages), "FPDF1303");
        yield return new("MaxOutputBytes", (l, v) => l.MaxOutputBytes(v), "<p>Output bytes</p>", Plain,
            () => Plain().ToBytes("<p>Output bytes</p>").Length, "FPDF1401");
        yield return new("MaxTotalFontBytes", (l, v) => l.MaxTotalFontBytes(v), "<p>AB</p>", WithFont,
            () => FontBytes.Length, "FPDF1503");
        yield return new("MaxImageResources", (l, v) => l.MaxImageResources(v), ImagesHtml, WithImages, () => Fixed(2), "FPDF1603");
        yield return new("MaxTotalImageBytes", (l, v) => l.MaxTotalImageBytes(v), ImagesHtml, WithImages,
            () => First.ByteLength + Second.ByteLength, "FPDF1603");
        yield return new("MaxCssCharacters", (l, v) => l.MaxCssCharacters(v), "<style>p{color:red}</style><p>x</p>", Plain, () => Fixed(12), "FPDF1205");
        yield return new("MaxCssSelectors", (l, v) => l.MaxCssSelectors(v), "<style>p,h1{color:red}</style><p>x</p>", Plain, () => Fixed(2), "FPDF1205");
        yield return new("MaxCssDeclarations", (l, v) => l.MaxCssDeclarations(v), "<style>p{color:red;margin-top:2pt}</style><p>x</p>", Plain, () => Fixed(2), "FPDF1205");
        yield return new("MaxCssMatchOperations", (l, v) => l.MaxCssMatchOperations(v), "<style>p{color:red}</style><p>x</p><p>y</p>", Plain, () => Fixed(6), "FPDF1205");
        yield return new("MaxCssSyntaxNodes", (l, v) => l.MaxCssSyntaxNodes(v), "<style>p{color:red}</style><p>x</p>", Plain, () => Fixed(4), "FPDF1205");
        yield return new("MaxCssSyntaxDepth", (l, v) => l.MaxCssSyntaxDepth(v), "<style>p{color:rgb(1,2,3)}</style><p>x</p>", Plain, () => Fixed(2), "FPDF1205");
        yield return new("MaxDisplayCommands", (l, v) => l.MaxDisplayCommands(v), "<p>a</p><p>b</p><p>c</p>", Plain, () => Fixed(3), "FPDF1401");
    }

    private static int Pages(PdfWriter writer, string html)
    {
        using var stream = new MemoryStream();
        return writer.ToStream(html, stream).PageCount;
    }

    [TestCaseSource(nameof(Cases))]
    public void SmallestFittingLimitRendersAndOneLessFailsWithItsCode(LimitCase limitCase)
    {
        var smallest = limitCase.Smallest();
        Assert.That(smallest, Is.GreaterThan(1), "a limit of 1 leaves no smaller value to reject");
        var pdf = limitCase.Writer().WithLimits(l => limitCase.Set(l, smallest)).ToBytes(limitCase.Html);
        Assert.That(Encoding.ASCII.GetString(pdf, 0, 5), Is.EqualTo("%PDF-"));

        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() =>
            limitCase.Writer().WithLimits(l => limitCase.Set(l, smallest - 1)).ToStream(limitCase.Html, output))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(limitCase.Code));
            Assert.That(error.Message, Is.Not.Empty);
            Assert.That(output.Length, Is.Zero, "nothing is written when a limit fails");
        });
    }

    [TestCaseSource(nameof(Cases))]
    public void LimitSetterRejectsZeroAndNegativeValues(LimitCase limitCase)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithLimits(l => limitCase.Set(l, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithLimits(l => limitCase.Set(l, -1)));
    }

    // A flood far beyond a small limit fails with the limit's code, and its cost stays linear in
    // the input. Where StopsEarly is set, the limit is checked while the flood is still being read,
    // so failing costs less than half of rendering the same input under roomy limits. The other
    // limits are checked only after a whole phase (CSS tokenizing, the HTML collecting pass or
    // serializing) and are bounded by the input limits instead; see R1c in docs/mvp-roadmap.md.
    public sealed record FloodCase(string Limit, Func<PdfLimitsSetup, PdfLimitsSetup> Small, Func<PdfLimitsSetup, PdfLimitsSetup> Large,
        string Html, string Code, bool StopsEarly)
    {
        public override string ToString() => Limit;
    }

    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static readonly string FloodParagraphs = Paragraphs(20_000);

    private static PdfLimitsSetup Roomy(PdfLimitsSetup l) => l.MaxInputCharacters(8_000_000).MaxElements(400_000).MaxPages(10_000)
        .MaxOutputBytes(256 << 20).MaxDisplayCommands(2_000_000).MaxCssCharacters(8_000_000).MaxCssSelectors(400_000)
        .MaxCssDeclarations(400_000).MaxCssSyntaxNodes(8_000_000).MaxCssMatchOperations(100_000_000);

    public static IEnumerable<FloodCase> Floods()
    {
        var style = (string css) => "<style>" + css + "</style><p>x</p>";
        yield return new("MaxInputCharacters", l => Roomy(l).MaxInputCharacters(1_000), Roomy, FloodParagraphs, "FPDF1001", true);
        yield return new("MaxElements", l => Roomy(l).MaxElements(1_000), Roomy, FloodParagraphs, "FPDF1002", true);
        yield return new("MaxDepth", l => Roomy(l).MaxDepth(16), l => Roomy(l).MaxDepth(100),
            Repeat("<div>", 100_000) + "x", "FPDF1003", false);
        yield return new("MaxPages", l => Roomy(l).MaxPages(2), Roomy, FloodParagraphs, "FPDF1303", true);
        yield return new("MaxOutputBytes", l => Roomy(l).MaxOutputBytes(10_000), Roomy, FloodParagraphs, "FPDF1401", false);
        yield return new("MaxDisplayCommands", l => Roomy(l).MaxDisplayCommands(100), Roomy, FloodParagraphs, "FPDF1401", true);
        yield return new("MaxCssCharacters", l => Roomy(l).MaxCssCharacters(1_000), Roomy,
            style(Repeat("p{color:red}", 100_000)), "FPDF1205", true);
        yield return new("MaxCssSelectors", l => Roomy(l).MaxCssSelectors(100), Roomy,
            style(Repeat("p{color:red}", 100_000)), "FPDF1205", false);
        yield return new("MaxCssDeclarations", l => Roomy(l).MaxCssDeclarations(100), Roomy,
            style("p{" + Repeat("color:red;", 100_000) + "}"), "FPDF1205", false);
        yield return new("MaxCssSyntaxNodes", l => Roomy(l).MaxCssSyntaxNodes(1_000), Roomy,
            style(Repeat("p{color:red}", 100_000)), "FPDF1205", false);
        yield return new("MaxCssSyntaxDepth", l => Roomy(l).MaxCssSyntaxDepth(16), l => Roomy(l).MaxCssSyntaxDepth(64),
            style("p{color:" + Repeat("f(", 50_000) + "}"), "FPDF1205", false);
        yield return new("MaxCssMatchOperations", l => Roomy(l).MaxCssMatchOperations(1_000), Roomy,
            "<style>" + Repeat("div p{color:red}", 200) + "</style>" + Paragraphs(2_000), "FPDF1205", true);
    }

    private static (long Allocated, TimeSpan Elapsed, Exception? Error) Measure(PdfWriter writer, string html)
    {
        try { writer.ToBytes(html); } catch (Exception) { } // warm up
        var stopwatch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Exception? error = null;
        try { writer.ToBytes(html); }
        catch (Exception e) { error = e; }
        return (GC.GetAllocatedBytesForCurrentThread() - before, stopwatch.Elapsed, error);
    }

    [TestCaseSource(nameof(Floods))]
    public void FloodFarBeyondTheLimitFailsWithBoundedCost(FloodCase flood)
    {
        var failing = Measure(Pdf.Write().WithLimits(l => flood.Small(l)), flood.Html);
        var reference = Measure(Pdf.Write().WithLimits(l => flood.Large(l)), flood.Html);
        TestContext.Out.WriteLine($"{flood.Limit}: failing {failing.Allocated / 1024} KiB in {failing.Elapsed.TotalMilliseconds:F0} ms; "
            + $"reference {reference.Allocated / 1024} KiB in {reference.Elapsed.TotalMilliseconds:F0} ms ({reference.Error?.GetType().Name ?? "rendered"})");
        Assert.That(failing.Error, Is.TypeOf<FactsPdfException>());
        Assert.That(((FactsPdfException)failing.Error!).Code, Is.EqualTo(flood.Code));
        Assert.That(failing.Allocated, Is.LessThan(256L * flood.Html.Length), "failing cost must stay linear in the input");
        Assert.That(failing.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
        if (flood.StopsEarly)
            Assert.That(failing.Allocated, Is.LessThan(reference.Allocated / 2), "the limit must stop the flood before the whole input is processed");
    }
}
