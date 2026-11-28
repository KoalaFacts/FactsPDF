using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Link targets Chrome writes as URI annotations beyond the original HTTPS/mailto subset, and img
/// data: URLs (R5x-resources-links-media).
/// </summary>
[TestFixture]
public sealed class LinkTargetAndDataImageTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 200, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static string Hex(string uri) => "/URI <" + Convert.ToHexString(Encoding.ASCII.GetBytes(uri)) + ">";

    // Absolute http and https URLs (fragments included) already written as Chrome serializes them.
    [TestCase("http://example.com/orders/7")]
    [TestCase("https://example.com/orders#section-2")]
    [TestCase("http://example.com/a?b=1;c=2#d")]
    [TestCase("https://example.com:8443/x")]
    public void SerializedHttpAndFragmentUrlsBecomeUriAnnotations(string href)
        => Assert.That(Encoding.Latin1.GetString(Pdf("<p><a href=\"" + href + "\">Open</a></p>")), Does.Contain(Hex(href)));

    // Over-correction: text Chrome would rewrite (adding a slash, lowercasing, dropping a default port,
    // escaping an apostrophe) keeps failing instead of being written differently from Chrome.
    [TestCase("http://example.com")]
    [TestCase("HTTP://example.com/")]
    [TestCase("http://Example.com/")]
    [TestCase("http://example.com:80/")]
    [TestCase("http://example.com/?q='x'")]
    [TestCase("https://example.com/a b")]
    [TestCase("ftp://example.com/")]
    public void UnserializedUrlsStillFail(string href)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<p><a href=\"" + href + "\">Open</a></p>"))!.Code, Is.EqualTo("FPDF1103"));

    [Test]
    public void TheOriginalHttpsSubsetIsWrittenUnchanged()
    {
        // https://example.com has no path; the original subset writes it as given (unchanged bytes).
        Assert.That(Encoding.Latin1.GetString(Pdf("<p><a href=\"https://example.com\">Open</a></p>")), Does.Contain(Hex("https://example.com")));
    }

    private static readonly byte[] Png = PngFixture.Image([0, 255, 0, 0, 255, 0, 0, 255, 255]);

    private static string DataUrl(string type, byte[] bytes) => "data:" + type + ";base64," + Convert.ToBase64String(bytes);

    [Test]
    public void ADataUrlImageRendersLikeTheSameSuppliedImage()
    {
        var supplied = Pdf("<img src='logo' style='width:40pt'>", Page with
        {
            Images = new Dictionary<string, PdfImage>(StringComparer.Ordinal) { ["logo"] = PdfImage.FromBytes(Png) },
        });
        Assert.That(Pdf("<img src='" + DataUrl("image/png", Png) + "' style='width:40pt'>"), Is.EqualTo(supplied));
        // The declared type is matched case-insensitively and white space inside the base64 is skipped.
        var spaced = "DATA:IMAGE/PNG;BASE64," + Regex.Replace(Convert.ToBase64String(Png), "(.{8})", "$1\n");
        Assert.That(Pdf("<img src='" + spaced + "' style='width:40pt'>"), Is.EqualTo(supplied));
    }

    [Test]
    public void TheSameDataUrlIsDecodedOnceAndEmbeddedOnce()
    {
        var url = DataUrl("image/png", Png);
        var once = Encoding.Latin1.GetString(Pdf("<img src='" + url + "'>"));
        var twice = Encoding.Latin1.GetString(Pdf("<img src='" + url + "'><img src='" + url + "'>"));
        Assert.That(Regex.Count(twice, "/Subtype /Image"), Is.EqualTo(Regex.Count(once, "/Subtype /Image")));
        // Distinct URLs count against MaxImageResources together with supplied images.
        var two = "<img src='" + url + "'><img src='" + DataUrl("image/png", PngFixture.Image([0, 1, 2, 3, 255, 4, 5, 6, 255])) + "'>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(two, Page with { MaxImageResources = 1 }))!.Code, Is.EqualTo("FPDF1603"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<img src='" + url + "'>", Page with { MaxTotalImageBytes = Png.Length - 1 }))!.Code,
            Is.EqualTo("FPDF1603"));
    }

    [TestCase("data:image/gif;base64,R0lGODlhAQABAAAAACw=", "FPDF1103")]
    [TestCase("data:image/png,rawbytes", "FPDF1103")]
    [TestCase("data:image/png;charset=utf-8;base64,AAAA", "FPDF1103")]
    [TestCase("data:image/png;base64,%41AAA", "FPDF1103")]
    [TestCase("data:image/png;base64,AAA", "FPDF1103")]
    [TestCase("data:image/png;base64,!!!!", "FPDF1103")]
    [TestCase("data:", "FPDF1103")]
    public void UnsupportedDataUrlsFail(string src, string code)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<img src='" + src + "'>"))!.Code, Is.EqualTo(code));

    [Test]
    public void ADataUrlThatIsNotASupportedImageFailsWithTheLoaderCode()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<img src='" + DataUrl("image/jpeg", [1, 2, 3, 4]) + "'>"))!;
        Assert.That(error.Code, Does.StartWith("FPDF16"));
    }
}
