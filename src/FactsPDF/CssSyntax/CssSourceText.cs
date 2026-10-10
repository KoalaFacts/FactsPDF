namespace FactsPDF.CssSyntax;

/// <summary>Normalized Unicode text with a map to original UTF-16 source boundaries.</summary>
internal sealed class CssSourceText
{
    internal string Normalized => "";
    internal static CssSourceText Create(
        string source, int absoluteStart, CssSyntaxLimits limits,
        IReadOnlyList<int>? originalOffsets = null)
        => throw new NotImplementedException("TDD baseline: CSS preprocessing is not implemented.");
    internal CssSourceSpan Span(int normalizedStart, int normalizedLength)
        => throw new NotImplementedException("TDD baseline: source mapping is not implemented.");
}
