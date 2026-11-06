using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// FactsPdfException.Code points users at the error code table in docs/development.md. These
// tests keep that table and the codes the engine and CLI actually use in step: a code added to
// the source without a table row, or a row left behind after its codes are gone, fails here.
[TestFixture]
public sealed partial class ErrorCodeCatalogTests
{
    [Test]
    public void EveryCodeTheSourceUsesIsInTheErrorCodeTable()
    {
        var documented = DocumentedRanges();
        var undocumented = SourceCodes()
            .Where(code => !documented.Any(range => range.Contains(code)))
            .Select(code => $"FPDF{code}")
            .ToArray();

        Assert.That(undocumented, Is.Empty,
            "Add these codes to the error code table in docs/development.md (Resource limits and diagnostics).");
    }

    [Test]
    public void EveryErrorCodeTableRangeStartsAndEndsWithACodeTheSourceUses()
    {
        var used = SourceCodes();
        var stale = DocumentedRanges()
            .Where(range => !used.Contains(range.First) || !used.Contains(range.Last))
            .Select(range => range.ToString())
            .ToArray();

        Assert.That(stale, Is.Empty, "These error code table rows name codes the source no longer uses.");
    }

    [Test]
    public void ErrorCodeTableRangesDoNotOverlap()
    {
        var ranges = DocumentedRanges().OrderBy(range => range.First).ToArray();

        Assert.That(ranges, Has.Length.GreaterThanOrEqualTo(7), "The error code table was not found or not parsed.");
        for (var i = 1; i < ranges.Length; i++)
        {
            Assert.That(ranges[i].First, Is.GreaterThan(ranges[i - 1].Last), $"{ranges[i - 1]} overlaps {ranges[i]}.");
        }
    }

    [Test]
    public void ExceptionMessageStartsWithItsCode()
    {
        var exception = new FactsPdfException("FPDF1306", "Footer at character 3: invalid page text template.");

        Assert.Multiple(() =>
        {
            Assert.That(exception.Code, Is.EqualTo("FPDF1306"));
            Assert.That(exception.Message, Is.EqualTo("FPDF1306: Footer at character 3: invalid page text template."));
            Assert.That(exception.SourceOffset, Is.EqualTo(-1));
        });
    }

    [Test]
    public void InvalidPageFurnitureTemplateFailsWithFpdf1306NamingTheFieldAndPosition()
    {
        var exception = Assert.Throws<FactsPdfException>(() => Pdf.Write()
            .WithPageFurniture(pages => pages.FooterCenter("Page {x}"))
            .ToBytes("<p>Body</p>"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo("FPDF1306"));
            Assert.That(exception.Message, Does.StartWith("FPDF1306: "));
            Assert.That(exception.Message, Does.Contain("FooterCenter").IgnoreCase);
            Assert.That(exception.Message, Does.Match(@"at character \d+"));
        });
    }

    private static HashSet<int> SourceCodes()
    {
        var codes = new HashSet<int>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(FindRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file)) continue;
            foreach (Match match in SourceCode().Matches(File.ReadAllText(file)))
            {
                codes.Add(int.Parse(match.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        Assert.That(codes, Is.Not.Empty, "No FPDF codes found under src/.");
        return codes;
    }

    private static CodeRange[] DocumentedRanges()
    {
        var text = File.ReadAllText(Path.Combine(FindRoot(), "docs", "development.md"));
        return TableRow().Matches(text)
            .Select(match =>
            {
                var first = int.Parse(match.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
                var last = match.Groups[2].Success
                    ? int.Parse(match.Groups[2].ValueSpan, System.Globalization.CultureInfo.InvariantCulture)
                    : first;
                Assert.That(last, Is.GreaterThanOrEqualTo(first), $"Range FPDF{first}-{last} is reversed.");
                return new CodeRange(first, last);
            })
            .ToArray();
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx"))) return directory.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    // Only string literals count: a code that survives only in a comment is not a code the source uses.
    [GeneratedRegex(@"""FPDF(\d{4})""")]
    private static partial Regex SourceCode();

    [GeneratedRegex(@"^\| FPDF(\d{4})(?:-(\d{4}))? \|", RegexOptions.Multiline)]
    private static partial Regex TableRow();

    private readonly record struct CodeRange(int First, int Last)
    {
        public bool Contains(int code) => code >= First && code <= Last;

        public override string ToString() => First == Last ? $"FPDF{First}" : $"FPDF{First}-{Last}";
    }
}
