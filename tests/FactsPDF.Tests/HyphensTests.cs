using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// hyphens:manual (the initial value) breaks a word at a soft hyphen (U+00AD) and prints a hyphen
/// there; hyphens:none ignores the soft hyphen. An unbroken soft hyphen is invisible and has no width.
/// </summary>
[TestFixture]
public sealed partial class HyphensTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    private static string[] Lines(string html) =>
        [.. HexString().Matches(Render(html)).Select(match => Encoding.ASCII.GetString(Convert.FromHexString(match.Groups[1].Value)))];

    private static FactsPdfException Failure(string html)
    {
        using var stream = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Options))!;
    }

    [GeneratedRegex("<([0-9A-F]+)> Tj")]
    private static partial Regex HexString();

    private const string Word = "Super&shy;cali&shy;fragil&shy;istic";

    [Test]
    public void ALongWordBreaksAtTheLastSoftHyphenThatFitsAndPrintsAHyphen()
    {
        Assert.That(Lines($"<div style='width:70pt'>aa {Word} bb</div>"),
            Is.EqualTo(new[] { "aa Super-", "cali-", "fragil-", "istic bb" }));
    }

    [Test]
    public void TheHyphenCountsTowardTheLineWidth()
    {
        // The built-in font is 7.2pt per glyph at 12pt: "Supercali-" is 72pt wide.
        Assert.That(Lines("<div style='width:71pt'>Super&shy;cali&shy;fragil</div>"),
            Is.EqualTo(new[] { "Super-", "cali-", "fragil" }));
        Assert.That(Lines("<div style='width:72pt'>Super&shy;cali&shy;fragil</div>"),
            Is.EqualTo(new[] { "Supercali-", "fragil" }));
    }

    [Test]
    public void AnUnbrokenSoftHyphenIsInvisibleAndHasNoWidth()
    {
        Assert.That(Render("<div>aa Super&shy;cali bb</div>"), Is.EqualTo(Render("<div>aa Supercali bb</div>")));
        Assert.That(Render("<div>aa Super&#173;cali bb</div>"), Is.EqualTo(Render("<div>aa Supercali bb</div>")));
        // Exactly fitting without the hyphen: no break is needed, so no hyphen appears.
        Assert.That(Lines("<div style='width:108pt'>Super&shy;cali&shy;fragil</div>"), Is.EqualTo(new[] { "Supercalifragil" }));
    }

    [Test]
    public void HyphensNoneIgnoresSoftHyphens()
    {
        var html = $"<div style='width:70pt;hyphens:none'>aa {Word} bb</div>";
        Assert.That(Failure(html).Code, Is.EqualTo("FPDF1302"));
        Assert.That(Render("<div style='hyphens:none'>aa Super&shy;cali bb</div>"),
            Is.EqualTo(Render("<div>aa Supercali bb</div>")));
        Assert.That(Lines("<div style='width:100pt;hyphens:none'>aa Super&shy;cali bb</div>"),
            Is.EqualTo(Lines("<div style='width:100pt'>aa Supercali bb</div>")));
    }

    [Test]
    public void HyphensIsInheritedAndTheOverrideWins()
    {
        var narrow = $"<div style='hyphens:none'><div style='width:70pt;hyphens:manual'>aa {Word} bb</div></div>";
        Assert.That(Lines(narrow), Is.EqualTo(new[] { "aa Super-", "cali-", "fragil-", "istic bb" }));
        var inherited = $"<div style='hyphens:none'><div style='width:70pt;hyphens:inherit'>aa {Word} bb</div></div>";
        Assert.That(Failure(inherited).Code, Is.EqualTo("FPDF1302"));
        var initial = $"<div style='hyphens:none'><div style='width:70pt;hyphens:initial'>aa {Word} bb</div></div>";
        Assert.That(Lines(initial), Is.EqualTo(Lines(narrow)));
    }

    [Test]
    public void ASoftHyphenInsideASpanBreaksTheSameWay()
    {
        Assert.That(Lines("<div style='width:60pt'>aa Super<span>&shy;cali&shy;fragil&shy;</span>istic</div>"),
            Is.EqualTo(Lines("<div style='width:60pt'>aa Super&shy;cali&shy;fragil&shy;istic</div>").Select(line => line).ToArray()));
        // A span that turns hyphens off removes only its own soft hyphens.
        var mixed = Lines("<div style='width:100pt'>Super&shy;cali<span style='hyphens:none'>&shy;fragil</span>&shy;istic</div>");
        Assert.That(mixed[0], Does.EndWith("-"));
    }

    [Test]
    public void SoftHyphensWorkInTableCellsAndListItems()
    {
        var cell = Lines($"<table><tr><td style='width:60pt'>aa {Word} bb</td></tr></table>");
        Assert.That(cell, Has.Some.EndsWith("-"));
        var item = Lines($"<ul><li style='width:80pt'>aa {Word} bb</li></ul>");
        Assert.That(item, Has.Some.EndsWith("-"));
    }

    [Test]
    public void AutomaticTableColumnsShrinkToTheLongestSoftHyphenSegment()
    {
        var table = $"<div style='width:70pt'><table style='width:100%'><tr><td>{Word}</td></tr></table></div>";
        Assert.That(Lines(table), Has.Some.EndsWith("-"));
        // The same word without soft hyphens cannot fit.
        Assert.That(Failure("<div style='width:70pt'><table style='width:100%'><tr><td>Supercalifragilistic</td></tr></table></div>").Code,
            Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void AWordWhoseSegmentsAreTooWideStillFails()
    {
        Assert.That(Failure("<div style='width:40pt'>Super&shy;califragilistic</div>").Code, Is.EqualTo("FPDF1302"));
        // A hyphen that does not fit after its segment is no break point.
        Assert.That(Failure("<div style='width:36pt'>Super&shy;cali</div>").Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void NowrapAndPreKeepSoftHyphensInvisible()
    {
        Assert.That(Render("<div style='white-space:nowrap'>aa Super&shy;cali bb</div>"),
            Is.EqualTo(Render("<div style='white-space:nowrap'>aa Supercali bb</div>")));
        Assert.That(Lines("<div style='width:60pt;white-space:pre-wrap'>aa Super&shy;cali&shy;fragil</div>"),
            Has.Some.EndsWith("-"));
    }

    [Test]
    public void ATrailingSoftHyphenBreaksNothing()
    {
        Assert.That(Render("<div>Super&shy; bb</div>"), Is.EqualTo(Render("<div>Super bb</div>")));
    }

    [TestCase("hyphens:auto")]
    [TestCase("hyphens:bogus")]
    [TestCase("hyphens:none manual")]
    public void UnsupportedValuesFailExplicitly(string css)
    {
        Assert.That(Failure($"<div style='{css}'>x</div>").Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void HyphensIsAcceptedOnEveryElement()
    {
        const string body = "<ul><li>a</li></ul><table><tr><td>b</td></tr></table><p>c <span>d</span></p><h1 style='font-weight:normal'>e</h1>";
        Assert.That(Render("<style>*{hyphens:manual}</style>" + body), Is.EqualTo(Render(body)));
    }
}
