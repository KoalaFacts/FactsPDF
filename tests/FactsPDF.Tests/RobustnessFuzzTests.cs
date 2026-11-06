using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.Configuration;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Bounded, fixed-seed mutation fuzzing of every untrusted input: HTML text, stylesheets, page furniture
// templates and JSON, font bytes and image bytes. The contract under test: each input either renders a
// structurally complete PDF or throws FactsPdfException. Any other exception is a bug. Failures report
// the case seed so the exact input can be regenerated. A longer, unbounded run of the same mutators found the PNG preset-dictionary
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
    public void MutatedExampleStylesheetsRenderOrFailWithFactsPdfException()
    {
        // Each case takes an example that renders unchanged with default options, mutates the text of its style
        // elements and leaves the markup alone, so the CSS tokenizer, parser, selector matcher and cascade see
        // hostile CSS on real HTML. Most edits are small so that a useful share of cases still reaches layout.
        var documents = Directory.EnumerateFiles(Path.Combine(FindRoot(), "examples"), "*.html", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(File.ReadAllText)
            .Where(html => StyleElement.IsMatch(html) && RendersUnchanged(html)).ToArray();
        var sheets = documents.SelectMany(html => StyleElement.Matches(html).Select(match => match.Groups[1].Value)).ToArray();
        Assert.That(documents, Has.Length.GreaterThan(20));
        var rendered = Run(1_500, Seed + 3, random =>
        {
            var html = StyleElement.Replace(documents[random.Next(documents.Length)],
                match => "<style>" + MutateCss(random, match.Groups[1].Value, sheets) + "</style>");
            return () => Render(html, new PdfOptions());
        });
        // Coverage floor (measured 150 on 92 qualifying examples); re-tune it when the example corpus changes.
        Assert.That(rendered, Is.GreaterThan(75), "Mutated stylesheets must still reach layout, not only the CSS parser.");
    }

    [Test]
    public void FuzzedPageFurnitureTextRendersOrFailsWithFactsPdfException()
    {
        // Templates are untrusted text; font sizes stay inside their documented 1–144pt configuration range,
        // where an out-of-range value is an ArgumentException by contract rather than an input failure.
        var font = PdfFont.FromBytes(Font);
        var rendered = Run(1_000, Seed + 4, random =>
        {
            var styled = random.Next(4) != 0;
            // A quarter of the cases carry hostile template fragments; the rest are well formed so they reach band layout.
            var hostile = random.Next(4) == 0;
            var furniture = new PdfPageFurniture
            {
                Default = FuzzRegions(random, hostile),
                FirstPage = random.Next(3) == 0 ? FuzzRegions(random, hostile) : null,
            };
            var options = new PdfOptions
            {
                PageFurniture = furniture,
                // Small pages leave little room for the bands; 73pt keeps the default 36pt margin valid.
                PageWidth = random.Next(4) == 0 ? 73 + random.Next(200) : 595.28,
                PageHeight = random.Next(4) == 0 ? 73 + random.Next(200) : 841.89,
                Margin = random.Next(4) == 0 ? random.Next(36) : 36,
                // Without fonts the built-in ASCII fallback has no bold or italic chain, a documented failure.
                Fonts = styled ? [font] : [], BoldFonts = styled ? [font] : [],
                ItalicFonts = styled ? [font] : [], BoldItalicFonts = styled ? [font] : [],
            };
            var body = string.Concat(Enumerable.Repeat("<p>AB Wi</p>", 1 + random.Next(random.Next(4) == 0 ? 400 : 4)));
            return () => Render(body, options);
        });
        // Coverage floor (measured 142); re-tune it when band layout rules change.
        Assert.That(rendered, Is.GreaterThan(75), "Furniture cases must reach band layout, not only template validation.");
    }

    [Test]
    public void MutatedPageFurnitureJsonParsesAndRendersOrFailsByContract()
    {
        // The CLI's configuration reader rejects malformed JSON with ArgumentException; whatever it accepts must
        // then render or fail with FactsPdfException.
        var source = File.ReadAllBytes(Path.Combine(FindRoot(), "examples", "business-page-furniture.json"));
        var font = PdfFont.FromBytes(Font);
        int parsed = 0, rendered = 0;
        var master = new Random(Seed + 5);
        for (var i = 0; i < 2_000; i++)
        {
            var caseSeed = master.Next();
            var random = new Random(caseSeed);
            var bytes = random.Next(2) == 0 ? MutateBytes(random, source)
                : Encoding.UTF8.GetBytes(MutateJson(random, Encoding.UTF8.GetString(source)));
            PdfPageFurniture furniture;
            try { furniture = PageFurnitureJson.Parse(bytes); parsed++; }
            // Only the reader's own rejection counts; a subclass such as ArgumentOutOfRangeException would be a bug.
            catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException)
                && ex.Message.StartsWith("PageFurniture ", StringComparison.Ordinal)) { continue; }
            catch (Exception ex) { Assert.Fail($"case seed {caseSeed}: parser threw {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}"); throw; }
            try
            {
                // Half the cases use the built-in ASCII fallback, which covers the example's text; the fixture font
                // covers only a few glyphs, so those cases mostly exercise the missing-glyph failure.
                var options = random.Next(2) == 0 ? new PdfOptions { PageFurniture = furniture }
                    : new PdfOptions { PageFurniture = furniture, Fonts = [font], BoldFonts = [font], ItalicFonts = [font] };
                var pdf = Render("<p>AB Wi</p>", options);
                Assert.That(Encoding.ASCII.GetString(pdf, 0, 5), Is.EqualTo("%PDF-"), $"case seed {caseSeed}");
                Assert.That(Encoding.ASCII.GetString(pdf, pdf.Length - 6, 6).TrimEnd(), Does.EndWith("%%EOF"), $"case seed {caseSeed}");
                rendered++;
            }
            catch (FactsPdfException) { }
            catch (AssertionException) { throw; }
            catch (Exception ex) { Assert.Fail($"case seed {caseSeed}: unexpected {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}"); }
        }
        Assert.That(parsed, Is.GreaterThan(40), "Mutated JSON must still get past the reader (measured: 63).");
        Assert.That(rendered, Is.GreaterThan(15), "Parsed furniture must still render (measured: 21).");
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
            catch (Exception ex) { Assert.Fail($"case seed {caseSeed}: building the case threw {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}"); throw; }
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

    private static readonly Regex StyleElement =
        new("<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex CssDeclaration = new("([a-z-]+)\\s*:\\s*([^;{}]+)");
    private static readonly string[] CssFragments =
        ["{", "}", ";", ":", ",", "(", ")", "[", "]", "\"", "'", "\\", "/*", "*/", "<!--", "-->", "@media print{", "@page{", "@import url(x);",
         "@font-face{", "@supports (x){", "!important", "*", ">", "+", "~", "||", "#", ".", "::before", ":not(", ":nth-child(2n+1)",
         ":is(", ":where(", ":hover", "[a]", "[a=b]", "[a^=\"x\"]", "[a$=x i]", "[a*=\"\"]", "[a~=b]", "[a|=en]", "[lang|=en s]",
         "\\0", "\\110000", "\\d800 ", "\\\n", "--x:{a}", "var(--x)", "calc(1px+2%)", "1e999px", "-0", "+.5e-3em", "U+0-10FFFF",
         "\u0000", "\uFFFD", "\uD800", "\r\n", "\f", "url(", "\"unterminated", "p", "div", "td", "table", "h1", "li", "body", "html"];

    private static string MutateCss(Random random, string source, string[] corpus)
    {
        string Pick(string[] items) => items[random.Next(items.Length)];
        // Half the cases keep the sheet well formed and swap one declaration's property or value, so the cascade
        // and layout see unusual but parseable CSS; the rest edit the raw text.
        if (random.Next(2) == 0)
        {
            var declarations = CssDeclaration.Matches(source);
            if (declarations.Count == 0) return source;
            var target = declarations[random.Next(declarations.Count)];
            var group = target.Groups[random.Next(3) == 0 ? 1 : 2];
            var replacement = group.Index == target.Groups[1].Index ? Pick(Properties)
                : random.Next(3) == 0 ? $"{Pick(Values)} {Pick(Values)}" : Pick(Values);
            return string.Concat(source.AsSpan(0, group.Index), replacement, source.AsSpan(group.Index + group.Length));
        }
        var css = new StringBuilder(source);
        var edits = random.Next(8) == 0 ? 1 + random.Next(40) : 1;
        for (var k = 0; k < edits; k++)
        {
            var at = random.Next(css.Length + 1);
            switch (random.Next(8))
            {
                case 0: css.Insert(at, Pick(CssFragments)); break;
                case 1: css.Insert(at, $"{Pick(Tags)}{Pick(CssFragments)}{{{Pick(Properties)}:{Pick(Values)}}}"); break;
                case 2: css.Insert(at, $"{Pick(Properties)}:{Pick(Values)} {Pick(Values)};"); break;
                case 3: css.Remove(Math.Min(at, css.Length), Math.Min(css.Length - Math.Min(at, css.Length), random.Next(200))); break;
                case 4: css.Insert(at, string.Concat(Enumerable.Repeat(Pick(["{", "(", "[", ":not(", "/*", "\"", "@media x{"]), 1 + random.Next(300)))); break;
                case 5:
                {
                    var other = corpus[random.Next(corpus.Length)];
                    var from = random.Next(other.Length);
                    css.Insert(at, other.AsSpan(from, Math.Min(other.Length - from, random.Next(600))));
                    break;
                }
                case 6: css.Insert(at, (char)random.Next(0, 0x10000)); break;
                default:
                    if (css.Length > 0) css[Math.Min(at, css.Length - 1)] = Pick(["{", "}", ";", ":", "\"", "\\", "/", "*", " ", "-", "0", "a"])[0];
                    break;
            }
        }
        return css.ToString();
    }

    private static readonly string[] TemplateFragments =
        ["{page}", "{pages}", "{{", "}}", "{", "}", "{x}", "{page", "pages}", "{PAGE}", "{ page }", "\uD800", "\uDC00", "\uD83D\uDE00",
         "\u0000", "\t", "\n", "\r", "\u200B", "\u00AD", "\uFFFE", "中文", "AB Wi", " ", "%", "(", ")", "\\", "<p>", "&amp;"];

    private static readonly string[] SafeTemplateFragments = ["{page}", "{pages}", "AB", "Wi", " ", "B"];

    private static PdfPageFurnitureRegions FuzzRegions(Random random, bool hostile)
    {
        PdfPageText? Text()
        {
            if (random.Next(3) == 0) return null;
            var template = new StringBuilder();
            var parts = random.Next(8) == 0 ? random.Next(400) : random.Next(6);
            for (var k = 0; k < parts; k++)
                template.Append(!hostile ? SafeTemplateFragments[random.Next(SafeTemplateFragments.Length)]
                    : random.Next(5) == 0 ? ((char)random.Next(0, 0x10000)).ToString() : TemplateFragments[random.Next(TemplateFragments.Length)]);
            return new PdfPageText
            {
                Template = template.ToString(),
                // Mostly sizes that fit the default 36pt margin; the rest probe both ends of the range.
                FontSize = random.Next(10) switch { 0 => 1, 1 => 144, 2 => 1 + random.NextDouble() * 143, _ => 6 + random.Next(19) },
                Color = new PdfRgbColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)),
                Bold = random.Next(4) == 0, Italic = random.Next(4) == 0,
            };
        }
        return new PdfPageFurnitureRegions
        {
            HeaderLeft = Text(), HeaderCenter = Text(), HeaderRight = Text(),
            FooterLeft = Text(), FooterCenter = Text(), FooterRight = Text(),
        };
    }

    private static readonly string[] JsonFragments =
        ["{", "}", "[", "]", ",", ":", "\"", "null", "true", "false", "0", "-0", "1e999", "144.0000001", "0.99", "NaN", "\"#GGGGGG\"",
         "\"#fff\"", "\"\\ud800\"", "\"\\udc00\"", "\"\\u0000\"", "\"{page}\"", "\"default\"", "\"firstPage\"",
         "\"headerLeft\"", "\"template\"", "\"fontSize\"", "\"color\"", "\"bold\"", "\"italic\"", "\"unknown\"", "/*x*/", "\uFEFF"];

    private static string MutateJson(Random random, string source)
    {
        var json = new StringBuilder(source);
        var edits = 1 + random.Next(6);
        for (var k = 0; k < edits; k++)
        {
            var at = random.Next(json.Length + 1);
            if (random.Next(3) == 0) json.Remove(Math.Min(at, json.Length), Math.Min(json.Length - Math.Min(at, json.Length), random.Next(40)));
            else json.Insert(at, random.Next(10) == 0
                ? string.Concat(Enumerable.Repeat(JsonFragments[random.Next(2) * 2], 1 + random.Next(20)))
                : JsonFragments[random.Next(JsonFragments.Length)]);
        }
        return json.ToString();
    }

    private static bool RendersUnchanged(string html)
    {
        try { Render(html, new PdfOptions()); return true; }
        catch (FactsPdfException) { return false; }
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
