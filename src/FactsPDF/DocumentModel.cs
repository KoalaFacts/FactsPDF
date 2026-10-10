namespace FactsPDF;

internal enum TextAlignment { Left, Center, Right }
internal readonly record struct Rgb(double R, double G, double B);
internal sealed record TextStyle(double FontSize, double LineHeight, Rgb Color, TextAlignment Alignment,
    double MarginBefore = 0, double MarginAfter = 0, bool BreakBefore = false, bool BreakAfter = false);
internal sealed record TextRun(string Text, TextStyle Style, bool IsBreak = false);
internal sealed record Paragraph(TextStyle Style)
{
    public List<TextRun> Runs { get; } = [];
}
internal sealed record PlacedText(string Text, double X, double Baseline, TextStyle Style, PdfFont? Font = null);
internal sealed class LayoutPage
{
    public List<PlacedText> Runs { get; } = [];
}

// M5 retained the structural box tree; M6 adds independently computed
// width/padding. Painting and paginated box fragments remain future features.
internal readonly record struct CssLength(double Value, bool IsPercent = false)
{
    internal double Resolve(double containingWidth) => IsPercent ? containingWidth * Value / 100 : Value;
}

// Specified values are retained until the containing block's content width is known.
// All properties in BoxStyle are non-inherited by default.
internal sealed record BoxStyle(
    CssLength? Width = null,
    CssLength PaddingTop = default,
    CssLength PaddingRight = default,
    CssLength PaddingBottom = default,
    CssLength PaddingLeft = default);

internal abstract record BlockChild;

internal sealed record BlockNode(string Name, TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChild
{
    public List<BlockChild> Children { get; } = [];
}

internal sealed record ParagraphNode(Paragraph Paragraph, bool IsAnonymous) : BlockChild;

internal sealed class DocumentRoot
{
    public List<BlockChild> Children { get; } = [];
}
