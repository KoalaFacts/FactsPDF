using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The all shorthand sets every modelled property to a CSS-wide keyword at its position in the cascade;
/// page:auto is the only page value the single page geometry can print.
/// </summary>
[TestFixture]
public sealed class AllShorthandTests
{
    private static byte[] Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, new PdfOptions { PageWidth = 400, PageHeight = 500, Margin = 20 });
        return stream.ToArray();
    }

    private static FactsPdfException Failure(string html)
    {
        using var stream = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, new PdfOptions()))!;
    }

    [Test]
    public void UnsetResetsInlineTextToWhatItInherits() =>
        Assert.That(Render("<style>span.r{all:unset}span{font-size:20pt}</style><p>aa <span class='r'>bb</span> <span>cc</span></p>"),
            Is.EqualTo(Render("<p>aa <span>bb</span> <span style='font-size:20pt'>cc</span></p>")));

    [Test]
    public void TheShorthandWinsOrLosesBySourcePosition()
    {
        var plain = Render("<p>aa <span>bb</span></p>");
        Assert.That(Render("<style>span{font-size:20pt;all:unset}</style><p>aa <span>bb</span></p>"), Is.EqualTo(plain));
        var bold = Render("<p>aa <span style='font-size:20pt'>bb</span></p>");
        Assert.That(Render("<style>span{all:unset;font-size:20pt}</style><p>aa <span>bb</span></p>"), Is.EqualTo(bold));
        // !important on a longhand beats a later all; !important on all beats a later longhand.
        Assert.That(Render("<style>span{font-size:20pt !important}span{all:unset}</style><p>aa <span>bb</span></p>"), Is.EqualTo(bold));
        Assert.That(Render("<style>span{all:unset !important;font-size:20pt}</style><p>aa <span>bb</span></p>"), Is.EqualTo(plain));
        Assert.That(Render("<p>aa <span style='font-size:20pt;all:unset'>bb</span></p>"), Is.EqualTo(plain));
    }

    [Test]
    public void RevertDropsEveryAuthorValueAndWorksOnAnyElement()
    {
        Assert.That(Render("<style>p{font-size:20pt;color:red;margin-bottom:30pt}p.r{all:revert}</style><p class='r'>aa</p>"),
            Is.EqualTo(Render("<p>aa</p>")));
        Assert.That(Render("<style>a{color:green;font-size:20pt}a{all:revert}</style><p>aa <a href='https://example.com/x'>bb</a></p>"),
            Is.EqualTo(Render("<p>aa <a href='https://example.com/x'>bb</a></p>")));
        Assert.That(Render("<style>.r{all:revert-layer}h1{font-size:30pt}</style><h1 class='r'>t</h1>"), Is.EqualTo(Render("<h1>t</h1>")));
        Assert.DoesNotThrow(() => Render("<style>*{all:revert}</style><ul><li>a</li></ul><table><tbody><tr><td>b</td></tr></tbody></table><p>c <span>d</span></p><hr>"));
    }

    [Test]
    public void UnsetOnALinkDropsItsUserAgentColourAndUnderline() =>
        Assert.That(Render("<style>a{all:unset}</style><p>aa <a href='https://example.com/x'>bb</a></p>"),
            Is.EqualTo(Render("<style>a{color:inherit;text-decoration:none}</style><p>aa <a href='https://example.com/x'>bb</a></p>")));

    [Test]
    public void InheritCopiesTheParentsBoxAndTextProperties()
    {
        Assert.That(Render("<style>.o{font-size:14pt;border:2px solid #000;padding:5pt;background:#ddd}.i{all:inherit}</style><div class='o'><div class='i'>aa</div></div>"),
            Is.EqualTo(Render("<style>.o,.i{font-size:14pt;border:2px solid #000;padding:5pt;background:#ddd}</style><div class='o'><div class='i'>aa</div></div>")));
        Assert.That(Render("<style>.o{font-size:14pt;color:#a00}.i{all:inherit}</style><div class='o'><p class='i'>aa</p></div>"),
            Is.EqualTo(Render("<style>.o{font-size:14pt;color:#a00}</style><div class='o'><p style='margin-top:0;margin-bottom:0'>aa</p></div>")));
    }

    [Test]
    public void CustomPropertiesAreNotReset() =>
        Assert.That(Render("<style>:root{--c:#f00}span{all:initial;color:var(--c)}</style><p>aa <span>bb</span></p>"),
            Is.EqualTo(Render("<p>aa <span style='color:#f00'>bb</span></p>")));

    [Test]
    public void DirectionAndUnicodeBidiAreNotReset() =>
        Assert.That(Render("<style>span{direction:ltr;unicode-bidi:normal;all:unset}</style><p>aa <span>bb</span></p>"),
            Is.EqualTo(Render("<p>aa <span>bb</span></p>")));

    [Test]
    public void AllResetsContentOfPseudoElements()
    {
        // all after content resets it to normal, so no box is generated; before it, content wins.
        Assert.That(Render("<style>p::before{content:'x ';all:initial}</style><p>aa</p>"), Is.EqualTo(Render("<p>aa</p>")));
        Assert.That(Render("<style>p::before{all:initial;content:'x '}</style><p>aa</p>"),
            Is.EqualTo(Render("<style>p::before{content:'x '}</style><p>aa</p>")));
    }

    [TestCase("div.c{all:initial}")]
    [TestCase("div.c{all:unset}")]
    [TestCase("p.c{all:unset}")]
    [TestCase("li.c{all:unset}")]
    [TestCase("td.c{all:initial}")]
    public void KeywordsThatTurnABlockIntoInlineTextFail(string rule)
    {
        var tag = rule[..rule.IndexOf('.')];
        var html = tag switch
        {
            "li" => "<ul><li class='c'>x</li></ul>",
            "td" => "<table><tbody><tr><td class='c'>x</td></tr></tbody></table>",
            _ => $"<{tag} class='c'>x</{tag}>"
        };
        Assert.That(Failure($"<style>{rule}</style>{html}").Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void InheritOnTextThatWouldBecomeBlockFails() =>
        Assert.That(Failure("<style>p{font-size:20pt}span{all:inherit}</style><p>aa <span>bb</span></p>").Code, Is.EqualTo("FPDF1201"));

    [Test]
    public void InitialOnInlineTextFailsWhereItWouldResetAnInheritedIndent() =>
        Assert.That(Failure("<style>p{white-space:pre-wrap}span{all:initial}</style><p>aa <span>bb</span></p>").Code, Is.EqualTo("FPDF1201"));

    [TestCase("all:red")]
    [TestCase("all:initial inherit")]
    [TestCase("all:")]
    [TestCase("all:10px")]
    public void OnlyACssWideKeywordIsValid(string css) =>
        Assert.That(Failure($"<style>span{{{css}}}</style><p><span>x</span></p>").Code, Is.EqualTo("FPDF1202"));

    [Test]
    public void PageAutoIsAcceptedAndPageNamesFail()
    {
        Assert.That(Render("<style>div{page:auto}</style><div>x</div>"), Is.EqualTo(Render("<div>x</div>")));
        Assert.That(Render("<style>div{page:initial}</style><div>x</div>"), Is.EqualTo(Render("<div>x</div>")));
        Assert.That(Failure("<style>div{page:wide}</style><div>x</div>").Code, Is.EqualTo("FPDF1202"));
        Assert.That(Failure("<style>@page wide{margin:10pt}</style><div>x</div>").Code, Is.EqualTo("FPDF1204"));
    }
}
