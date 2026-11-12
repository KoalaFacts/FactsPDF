namespace FactsPDF;

internal sealed class CssBudget(PdfOptions options, CancellationToken token)
{
    private long characters, selectors, declarations, work;
    internal CancellationToken Token => token;
    internal void Characters(int count, int offset)
    { token.ThrowIfCancellationRequested(); characters += count; Check(characters <= options.MaxCssCharacters, "CSS character budget exceeded.", offset); }
    internal void Selector(int offset)
    { token.ThrowIfCancellationRequested(); Check(++selectors <= options.MaxCssSelectors, CssSyntax.CssSyntaxLimits.SelectorBudgetMessage, offset); }
    /// <summary>The selectors still within <see cref="PdfOptions.MaxCssSelectors"/>.</summary>
    internal int SelectorsLeft => (int)Math.Max(0, options.MaxCssSelectors - selectors);
    internal int Declaration(int offset)
    { token.ThrowIfCancellationRequested(); Check(++declarations <= options.MaxCssDeclarations, CssSyntax.CssSyntaxLimits.DeclarationBudgetMessage, offset); return (int)declarations; }
    /// <summary>The declarations still within <see cref="PdfOptions.MaxCssDeclarations"/>.</summary>
    internal int DeclarationsLeft => (int)Math.Max(0, options.MaxCssDeclarations - declarations);
    internal void Work(int offset, int count = 1)
    { token.ThrowIfCancellationRequested(); work += count; Check(work <= options.MaxCssMatchOperations, "CSS matching/work budget exceeded.", offset); }
    internal static void Check(bool condition, string message, int offset)
    { if (!condition) throw new FactsPdfException("FPDF1205", message, offset); }
}
