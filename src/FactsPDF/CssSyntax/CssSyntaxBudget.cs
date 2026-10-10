namespace FactsPDF.CssSyntax;

/// <summary>Per-parse node and depth limits. Compiling TDD stub.</summary>
internal sealed class CssSyntaxBudget(CssSyntaxLimits limits)
{
    private readonly CssSyntaxLimits limits = limits;
    internal void Node(CssSourceSpan span)
        => throw new NotImplementedException("TDD baseline: CSS node accounting is not implemented; limit = " + limits.MaxNodes + ".");
    internal void Enter(CssSourceSpan span)
        => throw new NotImplementedException("TDD baseline: CSS depth accounting is not implemented; limit = " + limits.MaxDepth + ".");
    internal void Leave() { }
}
