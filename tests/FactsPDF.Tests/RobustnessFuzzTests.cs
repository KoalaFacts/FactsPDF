using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Bounded, fixed-seed mutation fuzzing of every untrusted input: HTML/CSS text, font bytes and image
// bytes. The contract under test: each input either renders a structurally complete PDF or throws
// FactsPdfException. Any other exception is a bug. Failures report the case seed so the exact input
// can be regenerated. A longer, unbounded run of the same mutators found the PNG preset-dictionary
// crash covered in PngValidationTests.
[TestFixture]
public sealed class RobustnessFuzzTests
{
    private const int Seed = 20261105;
    private static readonly byte[] Font = FontFixture.Create(listDigits: true);

    [Test]
    public void MutatedExampleHtmlRendersOrFailsWithFactsPdfException()
    {
        var corpus = Directory.EnumerateFiles(Path.Combine(FindRoot(), "examples"), "*.html", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(File.ReadAllText).ToArray();
        Assert.That(corpus, Is.Not.Empty);
        var font = PdfFont.FromBytes(Font);
        var image = PdfImage.FromBytes(PngFixture.Image([0, 10, 20, 30, 128], 1));
        var options = new PdfOptions
        {
            Fonts = [font], BoldFonts = [font], ItalicFonts = [font], BoldItalicFonts = [font],
            PageNumbers = true, HeaderText = "H",
            Images = new Dictionary<string, PdfImage> { ["logo"] = image, ["photo"] = image, ["quality-chart"] = image }
        };
        var rendered = Run(1_500, Seed, random =>
        {
            var html = MutateHtml(random, corpus[random.Next(corpus.Length)]);
            return () => Render(html, random.Next(2) == 0 ? options : new PdfOptions());
        });
        Assert.That(rendered, Is.GreaterThan(0), "Mutations must still reach the renderer, not only the parser.");
    }

    [Test]
    public void MutatedFontBytesLoadAndRenderOrFailWithFactsPdfException()
    {
        var rendered = Run(1_000, Seed + 1, random =>
        {
            var bytes = MutateBytes(random, Font);
            var subset = random.Next(2) == 0;
            return () => Render("<p>AB Wi 中文</p><p><b>AB</b></p>", new PdfOptions
            {
                Fonts = [PdfFont.FromBytes(bytes)], BoldFonts = [PdfFont.FromBytes(Font)], SubsetFonts = subset
            });
        });
        Assert.That(rendered, Is.GreaterThan(0));
    }

    [Test]
    public void MutatedImageBytesLoadAndRenderOrFailWithFactsPdfException()
    {
        var seeds = new[]
        {
            PngFixture.Image([0, 10, 20, 30, 128, 1, 1, 2, 3, 4, 4, 5, 6, 7, 8], 1, 3),
            PngFixture.Image([0, 1, 2, 3, 4, 5, 6], 2, 1, 8, 2),
            PngFixture.Image([0, 0b1011_0000], 4, 1, 1, 0),
            File.ReadAllBytes(Path.Combine(FindRoot(), "tests", "fixtures", "jpeg", "rgb-420.jpg")),
            File.ReadAllBytes(Path.Combine(FindRoot(), "tests", "fixtures", "jpeg", "gray.jpg")),
        };
        var rendered = Run(3_000, Seed + 2, random =>
        {
            var bytes = MutateBytes(random, seeds[random.Next(seeds.Length)]);
            if (random.Next(2) == 0) RepairPngChunkCrcs(bytes);
            return () => Render(
                "<p>x</p><img src=\"a\"><table style=\"width:100pt\"><colgroup><col style=\"width:100pt\"></colgroup>"
                + "<tbody><tr><td><img src=\"a\"></td></tr></tbody></table>",
                new PdfOptions { Images = new Dictionary<string, PdfImage> { ["a"] = PdfImage.FromBytes(bytes) } });
        });
        Assert.That(rendered, Is.GreaterThan(0));
    }

    [Test]
    public void ConcurrentConversionsWithSharedResourcesAreByteIdentical()
    {
        var root = FindRoot();
        var font = PdfFont.FromBytes(Font);
        var options = new PdfOptions { Fonts = [font], BoldFonts = [font], SubsetFonts = true, PageNumbers = true };
        var html = string.Concat(Enumerable.Repeat("<p>AB <b>Wi</b> 中文</p>", 120));
        var quickStart = File.ReadAllText(Path.Combine(root, "examples", "quick-start.html"));
        var expected = new[] { Render(html, options), Render(quickStart, new PdfOptions()) };
        var outputs = new byte[64][];
        Parallel.For(0, outputs.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            i => outputs[i] = i % 2 == 0 ? Render(html, options) : Render(quickStart, new PdfOptions()));
        for (var i = 0; i < outputs.Length; i++)
            Assert.That(outputs[i], Is.EqualTo(expected[i % 2]), $"Concurrent output {i} differs from the serial render.");
    }

    // Runs count cases; each case seed derives from the fixture seed so a failure is reproducible alone.
    private static int Run(int count, int seed, Func<Random, Func<byte[]>> makeCase)
    {
        var master = new Random(seed); var rendered = 0;
        for (var i = 0; i < count; i++)
        {
            var caseSeed = master.Next();
            Func<byte[]> work;
            try { work = makeCase(new Random(caseSeed)); }
            catch (FactsPdfException) { continue; }
            try
            {
                var pdf = work();
                Assert.That(Encoding.ASCII.GetString(pdf, 0, 5), Is.EqualTo("%PDF-"), $"case seed {caseSeed}");
                Assert.That(Encoding.ASCII.GetString(pdf, pdf.Length - 6, 6).TrimEnd(), Does.EndWith("%%EOF"), $"case seed {caseSeed}");
                rendered++;
            }
            catch (FactsPdfException) { }
            catch (AssertionException) { throw; }
            catch (Exception ex) { Assert.Fail($"case seed {caseSeed}: unexpected {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}"); }
        }
        return rendered;
    }

    private static byte[] Render(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    private static byte[] MutateBytes(Random random, byte[] source)
    {
        var bytes = source.ToList();
        var edits = 1 + random.Next(random.Next(4) != 0 ? 2 : 40);
        for (var k = 0; k < edits && bytes.Count > 16; k++)
        {
            var at = 8 + random.Next(bytes.Count - 8);
            switch (random.Next(4) == 0 ? random.Next(7) : random.Next(2))
            {
                case 0: bytes[at] = (byte)random.Next(256); break;
                case 1: bytes[at] ^= (byte)(1 << random.Next(8)); break;
                case 2: bytes.RemoveRange(at, Math.Min(bytes.Count - at, 1 + random.Next(64))); break;
                case 3: bytes.InsertRange(at, Enumerable.Range(0, 1 + random.Next(32)).Select(_ => (byte)random.Next(256))); break;
                case 4:
                {
                    var value = new byte[] { 0xff, 0x7f, 0x80, 0x00 }[random.Next(4)];
                    for (var j = 0; j < 4 && at + j < bytes.Count; j++) bytes[at + j] = value;
                    break;
                }
                case 5:
                {
                    var length = Math.Min(bytes.Count - at, 1 + random.Next(256));
                    bytes.InsertRange(at, bytes.GetRange(random.Next(bytes.Count - length + 1), length));
                    break;
                }
                default: bytes.RemoveRange(at, bytes.Count - at); break;
            }
        }
        return [.. bytes];
    }

    // Recomputes PNG chunk CRCs so mutations reach the zlib and scanline decoders instead of the CRC check.
    private static void RepairPngChunkCrcs(byte[] bytes)
    {
        if (!bytes.AsSpan().StartsWith(PngFixture.Signature)) return;
        for (var at = 8; at + 12 <= bytes.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at));
            if (length < 0 || at + 12L + length > bytes.Length) return;
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at + 8 + length), Crc(bytes.AsSpan(at + 4, 4 + length)));
            at += 12 + length;
        }
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var b in data) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        return ~crc;
    }

    private static readonly string[] Tags =
        ["p", "h1", "h3", "div", "section", "span", "b", "i", "strong", "small", "br", "hr", "pre", "blockquote", "ul", "ol",
         "li", "table", "thead", "tbody", "tr", "td", "th", "col", "colgroup", "img", "style", "u", "s", "script", "svg"];
    private static readonly string[] Properties =
        ["width", "max-width", "min-height", "height", "margin", "padding", "border", "border-radius", "font-size", "font-weight",
         "line-height", "letter-spacing", "text-indent", "text-align", "text-decoration", "white-space", "word-break", "color",
         "background", "display", "break-inside", "break-before", "vertical-align", "box-sizing", "outline"];
    private static readonly string[] Values =
        ["0", "-1px", "1e308pt", "1e-308em", "999999999mm", "100%", "-100%", "auto", "none", "inherit", "unset", "0.5em", "3rem",
         "12pc", "4q", "#fff", "#12345678", "rgb(1,2,3)", "hsl(1e9,50%,50%)", "currentColor", "nowrap", "pre-wrap", "break-all",
         "justify", "page", "avoid", "2pt solid red", "1pt 2pt 3pt 4pt 5pt", "NaNpx", "", "\"x\"", "!important", "url(x)", "var(--a)"];

    private static string MutateHtml(Random random, string source)
    {
        var html = new StringBuilder(source);
        string Pick(string[] items) => items[random.Next(items.Length)];
        var edits = 1 + random.Next(8);
        for (var k = 0; k < edits; k++)
        {
            var at = random.Next(html.Length + 1);
            string insert;
            switch (random.Next(11))
            {
                case 0: insert = $"<{Pick(Tags)}>"; break;
                case 1: insert = $"</{Pick(Tags)}>"; break;
                case 2: insert = $"<{Pick(Tags)} style=\"{Pick(Properties)}:{Pick(Values)}\">"; break;
                case 3: insert = $"<style>{Pick(Tags)} {Pick(Tags)}{{{Pick(Properties)}:{Pick(Values)}}}</style>"; break;
                case 4: insert = string.Concat(Enumerable.Repeat($"<{Pick(Tags)}>", 1 + random.Next(200))); break;
                case 5: insert = new string((char)random.Next(0x20, 0x2fff), 1 + random.Next(2000)); break;
                case 6: insert = ((char)random.Next(0, 0x10000)).ToString(); break;
                case 7: insert = "&" + Pick(["amp;", "lt;", "#0;", "#xD800;", "#1114112;", "nbsp", "#x1F600;", "zz;"]); break;
                case 8:
                {
                    html.Remove(Math.Min(at, html.Length), Math.Min(html.Length - Math.Min(at, html.Length), random.Next(400)));
                    continue;
                }
                case 9: insert = $" {Pick(Properties)}:{Pick(Values)};"; break;
                default:
                {
                    if (html.Length == 0) continue;
                    var from = random.Next(html.Length);
                    insert = html.ToString(from, Math.Min(html.Length - from, random.Next(800)));
                    break;
                }
            }
            html.Insert(Math.Min(at, html.Length), insert);
        }
        return html.ToString();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
