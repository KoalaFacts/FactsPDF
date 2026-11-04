using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Every standard named colour must reach real PDF ink, not just CSS parsing.</summary>
[TestFixture]
public sealed class CssNamedColorTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    // Explicit standard CSS colour reference: independent HEX values are the
    // byte-level oracle, rather than an engine lookup used as its own expected output.
    private const string StandardColors = "aliceblue:f0f8ff antiquewhite:faebd7 aqua:00ffff aquamarine:7fffd4 azure:f0ffff beige:f5f5dc bisque:ffe4c4 black:000000 blanchedalmond:ffebcd blue:0000ff blueviolet:8a2be2 brown:a52a2a burlywood:deb887 cadetblue:5f9ea0 chartreuse:7fff00 chocolate:d2691e coral:ff7f50 cornflowerblue:6495ed cornsilk:fff8dc crimson:dc143c cyan:00ffff darkblue:00008b darkcyan:008b8b darkgoldenrod:b8860b darkgray:a9a9a9 darkgreen:006400 darkgrey:a9a9a9 darkkhaki:bdb76b darkmagenta:8b008b darkolivegreen:556b2f darkorange:ff8c00 darkorchid:9932cc darkred:8b0000 darksalmon:e9967a darkseagreen:8fbc8f darkslateblue:483d8b darkslategray:2f4f4f darkslategrey:2f4f4f darkturquoise:00ced1 darkviolet:9400d3 deeppink:ff1493 deepskyblue:00bfff dimgray:696969 dimgrey:696969 dodgerblue:1e90ff firebrick:b22222 floralwhite:fffaf0 forestgreen:228b22 fuchsia:ff00ff gainsboro:dcdcdc ghostwhite:f8f8ff gold:ffd700 goldenrod:daa520 gray:808080 green:008000 greenyellow:adff2f grey:808080 honeydew:f0fff0 hotpink:ff69b4 indianred:cd5c5c indigo:4b0082 ivory:fffff0 khaki:f0e68c lavender:e6e6fa lavenderblush:fff0f5 lawngreen:7cfc00 lemonchiffon:fffacd lightblue:add8e6 lightcoral:f08080 lightcyan:e0ffff lightgoldenrodyellow:fafad2 lightgray:d3d3d3 lightgreen:90ee90 lightgrey:d3d3d3 lightpink:ffb6c1 lightsalmon:ffa07a lightseagreen:20b2aa lightskyblue:87cefa lightslategray:778899 lightslategrey:778899 lightsteelblue:b0c4de lightyellow:ffffe0 lime:00ff00 limegreen:32cd32 linen:faf0e6 magenta:ff00ff maroon:800000 mediumaquamarine:66cdaa mediumblue:0000cd mediumorchid:ba55d3 mediumpurple:9370db mediumseagreen:3cb371 mediumslateblue:7b68ee mediumspringgreen:00fa9a mediumturquoise:48d1cc mediumvioletred:c71585 midnightblue:191970 mintcream:f5fffa mistyrose:ffe4e1 moccasin:ffe4b5 navajowhite:ffdead navy:000080 oldlace:fdf5e6 olive:808000 olivedrab:6b8e23 orange:ffa500 orangered:ff4500 orchid:da70d6 palegoldenrod:eee8aa palegreen:98fb98 paleturquoise:afeeee palevioletred:db7093 papayawhip:ffefd5 peachpuff:ffdab9 peru:cd853f pink:ffc0cb plum:dda0dd powderblue:b0e0e6 purple:800080 rebeccapurple:663399 red:ff0000 rosybrown:bc8f8f royalblue:4169e1 saddlebrown:8b4513 salmon:fa8072 sandybrown:f4a460 seagreen:2e8b57 seashell:fff5ee sienna:a0522d silver:c0c0c0 skyblue:87ceeb slateblue:6a5acd slategray:708090 slategrey:708090 snow:fffafa springgreen:00ff7f steelblue:4682b4 tan:d2b48c teal:008080 thistle:d8bfd8 tomato:ff6347 turquoise:40e0d0 violet:ee82ee wheat:f5deb3 white:ffffff whitesmoke:f5f5f5 yellow:ffff00 yellowgreen:9acd32";

    private static IEnumerable<object[]> NamedColourCases =>
        StandardColors.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(':'))
            .Select(parts => new object[] { parts[0], "#" + parts[1] });

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [TestCaseSource(nameof(NamedColourCases))]
    public void All148NamedColoursProduceExactHexEquivalentPdf(string name, string hex)
    {
        var source = "<section style='background:" + name + ";border:2pt solid " + name +
            "'><p style='color:" + name + ";text-decoration:underline dotted " + name +
            "'>Service approved</p></section>";
        var control = source.Replace(name, hex, StringComparison.Ordinal);
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)), name);
    }

    [Test]
    public void CssCaseAliasesCascadeInheritanceAndProvidedFontRemainDeterministic()
    {
        const string names = "<style>section{color:MidnightBlue}p{color:Coral!important}" +
            ".note{text-decoration:underline dotted RebeccaPurple}</style>" +
            "<section><p style='color:Navy'>AB <span class=note>WA</span></p></section>";
        const string hex = "<style>section{color:#191970}p{color:#ff7f50!important}" +
            ".note{text-decoration:underline dotted #663399}</style>" +
            "<section><p style='color:#000080'>AB <span class=note>WA</span></p></section>";
        var fonts = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        Assert.That(Pdf(names, fonts), Is.EqualTo(Pdf(hex, fonts)));
        Assert.That(Pdf("<p style='color:grey'>A</p>"), Is.EqualTo(Pdf("<p style='color:gray'>A</p>")));
        Assert.That(Pdf("<p style='color:cyan'>A</p>"), Is.EqualTo(Pdf("<p style='color:aqua'>A</p>")));
        Assert.That(Pdf("<p style='color:fuchsia'>A</p>"), Is.EqualTo(Pdf("<p style='color:magenta'>A</p>")));
    }

    [Test]
    public void NamedColourReportPreservesEveryLineAndMultiPagePosition()
    {
        var report = ReadReport("report-named-colours.html");
        var control = ReadReport("report-named-colours-oracle.html");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(report, Page, default), Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(Pdf(report), Is.EqualTo(Pdf(control)));
        var text = string.Join(" ", pages.SelectMany(p => p.Runs).Select(r => r.Text));
        for (var i = 1; i <= 64; i++)
            Assert.That(text, Does.Contain("NAMED-" + i.ToString("00")));
    }

    [Test]
    public void ManagedCliMatchesApiForRealPaginatedNamedColourReport()
    {
        var report = ReadReport("report-named-colours.html");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(report));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(report, new())));
    }

    [TestCase("color:transparent")]
    [TestCase("color:buttontext")]
    [TestCase("background:systemcolor")]
    [TestCase("background:darkishblue")]
    [TestCase("border-color:slategreyish")]
    [TestCase("text-decoration:underline mysticpurple")]
    [TestCase("background:linear-gradient(red, blue)")]
    public void UnsupportedNamedOrSystemColoursPreserveOriginalOutput(string css)
    {
        using var output = new MemoryStream();
        output.Write("original customer PDF"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<section style='" + css + "'>Never written</section>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("original customer PDF"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void BadUnmatchedRuleFailsAndDoesNotBecomeIgnoredCss()
    {
        using var output = new MemoryStream();
        output.Write("previous PDF"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>.unused{color:buttontext}</style><p>Visible</p>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("previous PDF"u8.ToArray()));
    }

    private static string ReadReport(string name)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FactsPDF.slnx")))
            dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "Source checkout required for authored reports.");
        return File.ReadAllText(Path.Combine(dir!.FullName, "examples", name));
    }
}
