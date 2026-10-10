namespace FactsPDF.CssSyntax;

/// <summary>Compiling behavioral-red baseline for CSS syntax tree and recovery.</summary>
internal static class CssSyntaxParser
{
    internal static CssSyntaxResult ParseStylesheet(CssSourceText source, CssSyntaxLimits limits)
    {
        limits.Cancellation.ThrowIfCancellationRequested();
        return new([], [], []);
    }
    internal static CssSyntaxResult ParseDeclarations(CssSourceText source, CssSyntaxLimits limits)
    {
        limits.Cancellation.ThrowIfCancellationRequested();
        return new([], [], []);
    }
}
