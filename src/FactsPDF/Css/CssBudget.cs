namespace FactsPDF;

internal sealed class CssBudget(PdfOptions options, CancellationToken token)
{
    private long characters, selectors, declarations, work;
    internal CancellationToken Token => token;
    /// <summary>The non-strict conversion's warnings; null in strict mode, where every rejected construct fails.</summary>
    internal CssWarnings? Warnings => options.CssWarnings;
    /// <summary>Whether any compiled declaration is a custom property or holds var().</summary>
    internal bool UsesVariables { get; set; }
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
    private HashSet<string>? customProperties;
    /// <summary>Records a custom property name; a name beyond <see cref="PdfOptions.MaxCssCustomProperties"/>
    /// distinct names fails FPDF1205.</summary>
    internal void CustomProperty(string name, int offset)
    {
        customProperties ??= new HashSet<string>(StringComparer.Ordinal);
        if (customProperties.Add(name))
            Check(customProperties.Count <= options.MaxCssCustomProperties, CustomPropertyBudgetMessage, offset);
    }
    internal const string CustomPropertyBudgetMessage = "CSS custom property budget exceeded (MaxCssCustomProperties distinct names).";
    internal void Work(int offset, int count = 1)
    { token.ThrowIfCancellationRequested(); work += count; Check(work <= options.MaxCssMatchOperations, "CSS matching/work budget exceeded.", offset); }
    internal static void Check(bool condition, string message, int offset)
    { if (!condition) throw new FactsPdfException("FPDF1205", message, offset); }
}
