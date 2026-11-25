using NUnit.Framework;

namespace FactsPDF.Tests;

// HTML defines menu as a toolbar list with the same user-agent style as ul, so it renders
// exactly like ul: disc markers, the list gutter, nesting limits, CSS and pagination (R5cd2).
// Before, it failed FPDF1102.
[TestFixture]
public sealed class MenuElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string AsUl(string html) => html.Replace("<menu", "<ul").Replace("</menu>", "</ul>")
        .Replace("menu{", "ul{").Replace("menu li{", "ul li{").Replace("menu.", "ul.");

    [TestCase("<menu><li>Copy</li><li>Paste</li></menu><p>After</p>")]
    [TestCase("<p>Before</p><menu><li>One<menu><li>Nested</li></menu></li><li>Two</li></menu>")]
    [TestCase("<menu><li>Outer<ul><li>Mixed ul</li></ul></li></menu><ol><li>A<menu><li>B</li></menu></li></ol>")]
    [TestCase("<div><menu><li><p>Para item</p></li></menu></div>")]
    [TestCase("<style>menu{list-style-type:square;margin:0;padding-left:20pt}menu li{color:#c00}</style><menu><li>Styled</li></menu>")]
    [TestCase("<style>menu.n{list-style-position:inside}</style><menu class=n id=m><li>Inside</li></menu>")]
    public void RendersLikeUl(string html)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(AsUl(html))));

    [Test]
    public void PaginatesLikeUl()
    {
        var items = string.Concat(Enumerable.Range(1, 40).Select(i => $"<li>Item {i}</li>"));
        Assert.That(Pdf($"<menu>{items}</menu>"), Is.EqualTo(Pdf($"<ul>{items}</ul>")));
    }

    [TestCase("<menu>Loose text</menu>")]
    [TestCase("<menu><li>A</li></ol>")]
    [TestCase("<p><span><menu><li>A</li></menu></span></p>")]
    [TestCase("<menu href='x'><li>A</li></menu>")]
    [TestCase("<menu><li>A<menu><li>B<menu><li>C</li></menu></li></menu></li></menu>")]
    public void FailsLikeUl(string html)
    {
        string Code(string source)
        {
            using var output = new MemoryStream();
            var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(source, output, Page))!;
            Assert.That(output.Length, Is.Zero);
            return ex.Code;
        }
        Assert.That(Code(html), Is.EqualTo(Code(AsUl(html))));
    }
}
