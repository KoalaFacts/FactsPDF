using System.Globalization;
using NUnit.Framework;

namespace FactsPDF.Tests;

// PDF syntax is locale-free: decimal points, minus signs and digits must never follow the
// caller's culture. sv-SE uses U+2212 for minus and a comma for decimals; fa-IR and ar-SA
// also use non-Latin digits and separators.
[TestFixture]
[NonParallelizable]
public sealed class ConversionCultureTests
{
    private static readonly byte[] Font = FontFixture.Create(listDigits: true);

    private const string Report = """
        <h1 style="letter-spacing:0.75pt">AB Wi</h1>
        <div style="padding:7.5pt;border:1.25pt solid #123456;border-radius:3.5pt;background-color:#eef4fa">
          <p style="text-indent:12.25pt;word-spacing:1.5pt;text-decoration:underline dotted red">AB Wi 中文 AB Wi</p>
        </div>
        <p style="border-bottom:0.75pt dashed #123456">AB</p>
        <ol><li>AB</li><li>Wi</li></ol><ul style="list-style-type:circle"><li>AB</li></ul>
        <table style="width:300pt"><colgroup><col style="width:100.5pt"><col style="width:199.5pt"></colgroup>
          <tbody><tr><td>AB</td><td style="vertical-align:middle">Wi</td></tr></tbody></table>
        """;

    private static byte[] Render(string culture, string html, PdfOptions options)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
                culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
            using var output = new MemoryStream();
            PdfConverter.Convert(html, output, options);
            return output.ToArray();
        }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous; }
    }

    [TestCase("sv-SE")]
    [TestCase("de-DE")]
    [TestCase("fa-IR")]
    [TestCase("ar-SA")]
    public void PdfBytesDoNotDependOnTheCurrentCulture(string culture)
    {
        var font = PdfFont.LoadTrueType(Font);
        var options = new PdfOptions
        {
            Fonts = [font], BoldFonts = [font], PageNumbers = true,
            PageWidth = 420.75, PageHeight = 595.5, Margin = 30.25, SubsetFonts = true, Title = "AB"
        };
        Assert.That(Render(culture, Report, options), Is.EqualTo(Render("", Report, options)));
        var ascii = "<p style=\"margin-left:0.5pt\">Report 1.5</p>";
        Assert.That(Render(culture, ascii, new PdfOptions { PageNumbers = true }),
            Is.EqualTo(Render("", ascii, new PdfOptions { PageNumbers = true })));
    }
}
