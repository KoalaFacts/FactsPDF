namespace FactsPDF.CssSyntax;

/// <summary>Cooperative, per-parse component node and nesting limits.</summary>
internal sealed class CssSyntaxBudget
{
    private readonly CssSyntaxLimits limits;
    private int nodeCount;
    private int depth;

    internal CssSyntaxBudget(CssSyntaxLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxCharacters < 1 || limits.MaxNodes < 1 || limits.MaxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(limits), "All CSS syntax budgets must be positive.");
        this.limits = limits;
    }

    internal void Node(CssSourceSpan span)
    {
        limits.Cancellation.ThrowIfCancellationRequested();
        if (nodeCount >= limits.MaxNodes)
            throw new CssSyntaxLimitException("CSS syntax node count exceeds MaxNodes.", span);
        nodeCount++;
    }

    internal void Enter(CssSourceSpan span)
    {
        limits.Cancellation.ThrowIfCancellationRequested();
        if (depth >= limits.MaxDepth)
            throw new CssSyntaxLimitException("CSS syntax nesting exceeds MaxDepth.", span);
        depth++;
    }

    internal void Leave()
    {
        if (depth == 0) throw new InvalidOperationException("Unbalanced CSS syntax nesting.");
        depth--;
    }
}
