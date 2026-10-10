using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssSourceTextTests
{
    private static readonly CssSyntaxLimits Defaults = new();

    [Test]
    public void NormalizesNewlinesNullsAndIsolatedSurrogatesWithoutLosingOriginalOffsets()
    {
        var input = "a\r\nb\fc\0d" + new string((char)0xD800, 1);
        var text = CssSourceText.Create(input, 50, Defaults);
        Assert.That(text.Normalized, Is.EqualTo("a\nb\nc\uFFFDd\uFFFD"));
        Assert.That(text.Span(2, 1), Is.EqualTo(new CssSourceSpan(53, 1)));
        Assert.That(text.Span(1, 1), Is.EqualTo(new CssSourceSpan(51, 2)));
        Assert.That(text.Span(0, text.Normalized.Length), Is.EqualTo(new CssSourceSpan(50, input.Length)));
    }

    [Test]
    public void RetainsSupplementaryScalarAsTwoUtf16CodeUnits()
    {
        var text = CssSourceText.Create("\U0001F600", 88, Defaults);
        Assert.That(text.Normalized, Is.EqualTo("\U0001F600"));
        Assert.That(text.Span(0, 2), Is.EqualTo(new CssSourceSpan(88, 2)));
    }

    [Test]
    public void UsesCallerProvidedDecodedAttributeOrigins()
    {
        var text = CssSourceText.Create("red", 0, Defaults, [100, 101, 107, 108]);
        Assert.That(text.Span(1, 1), Is.EqualTo(new CssSourceSpan(101, 6)));
        Assert.That(text.Span(0, 3), Is.EqualTo(new CssSourceSpan(100, 8)));
    }

    [Test]
    public void RejectsInvalidBoundaryMapsWithoutSilentlyLosingOffsets()
    {
        Assert.Throws<ArgumentException>(
            () => CssSourceText.Create("red", 0, Defaults, [5, 4, 7, 8]));
        Assert.Throws<ArgumentException>(
            () => CssSourceText.Create("red", 0, Defaults, [5, 6]));
    }

    [Test]
    public void ChecksCharacterLimitsAndConfigurationBeforeAllocation()
    {
        var limits = Defaults with { MaxCharacters = 3 };
        Assert.Throws<CssSyntaxLimitException>(() => CssSourceText.Create("four", 0, limits));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CssSourceText.Create("", 0, Defaults with { MaxNodes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CssSourceText.Create("", 0, Defaults with { MaxDepth = 0 }));
    }

    [Test]
    public void HonorsCancellationEvenForEmptyInput()
    {
        var limits = Defaults with { Cancellation = new CancellationToken(true) };
        Assert.Throws<OperationCanceledException>(() => CssSourceText.Create("", 0, limits));
    }

    [Test]
    public void NodeAndDepthLimitsFailIndependently()
    {
        var nodes = new CssSyntaxBudget(Defaults with { MaxNodes = 2 });
        nodes.Node(new CssSourceSpan(0, 1));
        nodes.Node(new CssSourceSpan(1, 1));
        Assert.Throws<CssSyntaxLimitException>(() => nodes.Node(new CssSourceSpan(2, 1)));

        var depth = new CssSyntaxBudget(Defaults with { MaxDepth = 2 });
        depth.Enter(new CssSourceSpan(0, 1));
        depth.Enter(new CssSourceSpan(1, 1));
        Assert.Throws<CssSyntaxLimitException>(() => depth.Enter(new CssSourceSpan(2, 1)));
        depth.Leave();
        depth.Leave();
    }

    [Test]
    public void NewPdfOptionBudgetsHaveDocumentedDefaultsAndAreValidated()
    {
        Assert.That(new PdfOptions().MaxCssSyntaxNodes, Is.EqualTo(131_072));
        Assert.That(new PdfOptions().MaxCssSyntaxDepth, Is.EqualTo(64));
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfConverter.Convert(
            "<p>A</p>", output, new PdfOptions { MaxCssSyntaxDepth = 0 }));
        Assert.That(output.Length, Is.Zero);
    }
}
