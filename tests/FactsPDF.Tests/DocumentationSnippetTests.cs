using System.Text;
using NUnit.Framework;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FactsPDF.Tests;

/// <summary>
/// Compiles every <c>```csharp</c> block in README.md and the guides under docs/ against the
/// current FactsPDF assembly and the shared framework only, as a console app with the SDK's
/// implicit usings would, so a renamed or removed SDK member cannot leave a copy-paste example
/// that no longer builds. Dated design records under docs/superpowers/ sketch internal
/// interfaces and are not compiled.
/// </summary>
[TestFixture]
public sealed partial class DocumentationSnippetTests
{
    // Names that snippets use without declaring them, standing for the reader's own values.
    private static readonly (string Name, string Declaration)[] FreeNames =
    [
        ("html", "string html = \"<p>x</p>\";"),
        ("response", "var response = new { Body = System.IO.Stream.Null };"),
        ("latinRegular", "PdfFont latinRegular = null!;"),
        ("latinBold", "PdfFont latinBold = null!;"),
        ("latinItalic", "PdfFont latinItalic = null!;"),
        ("latinBoldItalic", "PdfFont latinBoldItalic = null!;"),
        ("chineseRegular", "PdfFont chineseRegular = null!;"),
        ("chineseBold", "PdfFont chineseBold = null!;"),
        ("chineseItalic", "PdfFont chineseItalic = null!;"),
        ("chineseBoldItalic", "PdfFont chineseBoldItalic = null!;"),
    ];

    [Test]
    public void DocumentationContainsTheExpectedCSharpSnippets()
    {
        var snippets = Snippets();
        Assert.That(snippets.Select(s => s.Source), Does.Contain("README.md"),
            "README's quick start must stay a compiled ```csharp block");
        Assert.That(snippets.Single(s => s.Source == "README.md").Code, Does.Contain("using FactsPDF;"),
            "README's quick start is a complete program and must import the SDK namespace");
        Assert.That(snippets, Has.Count.GreaterThanOrEqualTo(8));
    }

    [TestCaseSource(nameof(SnippetCases))]
    public void DocumentationSnippetCompiles(string source, int line, string code)
    {
        var errors = CompileErrors(code, source, line);
        Assert.That(errors, Is.Empty, $"```csharp block at {source}:{line} no longer compiles");
    }

    [TestCase("```csharp", true)]
    [TestCase("  ```cs", true)]
    [TestCase("```C#", true)]
    [TestCase("~~~csharp", true)]
    [TestCase("~~~ cs", true)]
    [TestCase("```c", false)]
    [TestCase("```css", false)]
    public void CSharpFenceSpellingsAreRecognised(string line, bool isCSharp) =>
        Assert.That(CSharpFence().IsMatch(line), Is.EqualTo(isCSharp));

    [Test]
    public void SnippetUsingARemovedMemberFailsToCompile()
    {
        var errors = CompileErrors("var pdf = Pdf.Write().WithPageNumberz();");
        Assert.That(errors, Has.Some.Contains("CS1061"));
    }

    [Test]
    public void ProgramWithUsingsButWithoutTheSdkNamespaceFailsToCompile()
    {
        var errors = CompileErrors("using System.Text;\nvar pdf = Pdf.Write();");
        Assert.That(errors, Has.Some.Contains("CS0103"));
    }

    [Test]
    public void UsingDeclarationStaysAStatement()
    {
        var errors = CompileErrors("using var stream = new MemoryStream();\nPdf.Write().ToStream(\"<p>x</p>\", stream);");
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void SnippetCannotSeeTestHostAssemblies()
    {
        var errors = CompileErrors("NUnit.Framework.Assert.Fail();");
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void ErrorsReportTheMarkdownLocation()
    {
        var errors = CompileErrors("var a = 1;\nvar pdf = Pdf.Write().WithPageNumberz();", "README.md", 40);
        Assert.That(errors, Has.Some.StartsWith("README.md(41,"));
    }

    [Test]
    public void SnippetWithASyntaxErrorFailsToCompile()
    {
        var errors = CompileErrors("var pdf = Pdf.Write(;");
        Assert.That(errors, Is.Not.Empty);
    }

    private static IEnumerable<TestCaseData> SnippetCases() =>
        Snippets().Select(s => new TestCaseData(s.Source, s.Line, s.Code)
            .SetName($"DocumentationSnippetCompiles({s.Source}:{s.Line})"));

    private static List<(string Source, int Line, string Code)> Snippets()
    {
        var root = RepositoryRoot();
        var files = new List<string> { Path.Combine(root, "README.md") };
        var records = Path.Combine(root, "docs", "superpowers") + Path.DirectorySeparatorChar;
        files.AddRange(Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(records, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal));
        var snippets = new List<(string, int, string)>();
        for (var f = 0; f < files.Count; f++)
        {
            var lines = File.ReadAllLines(files[f]);
            var relative = Path.GetRelativePath(root, files[f]).Replace('\\', '/');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!CSharpFence().IsMatch(lines[i])) continue;
                Assert.That(lines[i].TrimEnd(), Is.EqualTo("```csharp"),
                    $"C# fence at {relative}:{i + 1} must be exactly ```csharp so it is compiled");
                var start = i + 1;
                var body = new StringBuilder();
                for (i = start; i < lines.Length && lines[i].TrimEnd() != "```"; i++) body.AppendLine(lines[i]);
                Assert.That(i, Is.LessThan(lines.Length), $"unterminated ```csharp block at {relative}:{start}");
                snippets.Add((relative, start, body.ToString()));
            }
        }

        return snippets;
    }

    private static List<string> CompileErrors(string snippet, string source = "snippet", int line = 1)
    {
        // The implicit usings of `dotnet new console`. A complete program brings its own
        // `using FactsPDF;`; a fragment without using directives gets it, as its page assumes.
        var usings = new StringBuilder(
            "using System;\nusing System.Collections.Generic;\nusing System.IO;\nusing System.Linq;\n" +
            "using System.Net.Http;\nusing System.Threading;\nusing System.Threading.Tasks;\n");
        var body = new StringBuilder();
        var hasUsings = false;
        foreach (var raw in snippet.Split('\n'))
        {
            var current = raw.TrimEnd('\r');
            if (UsingDirective().IsMatch(current))
            {
                usings.AppendLine(current.Trim());
                hasUsings = true;
                body.AppendLine();
            }
            else body.AppendLine(current);
        }

        if (!hasUsings) usings.AppendLine("using FactsPDF;");

        var text = body.ToString();
        var preamble = new StringBuilder();
        foreach (var (name, declaration) in FreeNames)
        {
            var used = Regex.IsMatch(text, $@"\b{name}\b");
            var declared = Regex.IsMatch(text, $@"\b(var|PdfFont|string)\s+{name}\b");
            if (used && !declared) preamble.AppendLine(declaration);
        }

        var program = $"{usings}{preamble}#line {line} \"{source}\"\n{text}";
        var tree = CSharpSyntaxTree.ParseText(program, new CSharpParseOptions(LanguageVersion.Latest));
        // Only what a user's app sees: the shared framework and FactsPDF, not the test host's NUnit
        // or Roslyn.
        var framework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => string.Equals(Path.GetDirectoryName(path), framework, StringComparison.Ordinal))
            .Append(typeof(Pdf).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "DocumentationSnippet",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        return result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
    }

    [GeneratedRegex(@"^\s*(```|~~~)\s*(csharp|c#|cs)(?!\w)", RegexOptions.IgnoreCase)]
    private static partial Regex CSharpFence();

    [GeneratedRegex(@"^using\s+(static\s+)?[\w.]+(\s*=\s*[\w.<>]+)?;\s*$")]
    private static partial Regex UsingDirective();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("FactsPDF.slnx not found above the test output");
    }
}
