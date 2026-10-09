namespace FactsPDF;

internal sealed class CssBudget(PdfOptions options, CancellationToken token)
{
    private long characters, selectors, declarations, work;
    internal CancellationToken Token => token;
    internal void Characters(int count, int offset)
    { token.ThrowIfCancellationRequested(); characters += count; Check(characters <= options.MaxCssCharacters, "CSS character budget exceeded.", offset); }
    internal void Selector(int offset)
    { token.ThrowIfCancellationRequested(); Check(++selectors <= options.MaxCssSelectors, "CSS selector budget exceeded.", offset); }
    internal int Declaration(int offset)
    { token.ThrowIfCancellationRequested(); Check(++declarations <= options.MaxCssDeclarations, "CSS declaration budget exceeded.", offset); return (int)declarations; }
    internal void Work(int offset, int count = 1)
    { token.ThrowIfCancellationRequested(); work += count; Check(work <= options.MaxCssMatchOperations, "CSS matching/work budget exceeded.", offset); }
    internal static void Check(bool condition, string message, int offset)
    { if (!condition) throw new FactsPdfException("FPDF1205", message, offset); }
}
