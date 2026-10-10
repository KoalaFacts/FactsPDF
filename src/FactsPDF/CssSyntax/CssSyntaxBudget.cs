namespace FactsPDF.CssSyntax;

/// <summary>Per-parse node and depth limits. Compiling TDD stub.</summary>
internal sealed class CssSyntaxBudget(CssSyntaxLimits limits)
{
    internal void Node(CssSourceSpan span)
        => throw new NotImplementedException("TDD baseline: CSS node accounting is not implemented.");
    internal void Enter(CssSourceSpan span)
        => throw new NotImplementedException("TDD baseline: CSS depth accounting is not implemented.");
    internal void Leave() { }
}
