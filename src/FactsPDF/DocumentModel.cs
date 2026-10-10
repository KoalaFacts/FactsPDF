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
    public List<PaintedBox> PaintBoxes { get; } = [];
}

internal sealed record PaintedBox(double X, double Top, double Width, double Height,
    BoxStyle Style, Rgb TextColor, int Order,
    bool IsFirstFragment = true, bool IsLastFragment = true)
{
    // A page fragment can be shorter than its specified border. Use the same
    // clipped geometry for serialization and display-command accounting.
    internal double TopBorderHeight => IsFirstFragment
        ? Math.Min(Math.Max(0, Height), Style.BorderTop.EffectiveWidth) : 0;
    internal double BottomBorderHeight => IsLastFragment
        ? Math.Min(Math.Max(0, Height), Style.BorderBottom.EffectiveWidth) : 0;

    // Exactly the nonzero rectangles emitted by PdfPaintSerializer.Rect.
    // An empty background or vertical edge of a zero-height box costs 0.
    public int CommandCount
    {
        get
        {
            var top = TopBorderHeight;
            var bottom = BottomBorderHeight;
            var middleHeight = Math.Max(0, Height - top - bottom);
            return (Width > 0 && Height > 0 && Style.BackgroundColor.HasValue ? 1 : 0)
                + (Width > 0 && top > 0 ? 1 : 0)
                + (Width > 0 && bottom > 0 ? 1 : 0)
                + (middleHeight > 0 && Style.BorderLeft.EffectiveWidth > 0 ? 1 : 0)
                + (middleHeight > 0 && Style.BorderRight.EffectiveWidth > 0 ? 1 : 0);
        }
    }
}

// M5 retains the box tree; M6 computes geometry; M7 paints single-page
// borders/backgrounds; M8 slices continued decorations across page fragments.
internal readonly record struct CssLength(double Value, bool IsPercent = false)
{
    internal double Resolve(double containingWidth) => IsPercent ? containingWidth * Value / 100 : Value;
}

// Specified values are retained until the containing block's content width is known.
// All properties in BoxStyle are non-inherited by default.
internal readonly record struct BorderEdge(double Width, bool Solid, Rgb? Color)
{
    internal static BorderEdge Initial => new(2.25, false, null); // CSS medium=3px
    internal double EffectiveWidth => Solid ? Width : 0;
}

internal sealed record BoxStyle(
    CssLength? Width = null,
    CssLength PaddingTop = default,
    CssLength PaddingRight = default,
    CssLength PaddingBottom = default,
    CssLength PaddingLeft = default)
{
    public Rgb? BackgroundColor { get; init; } // null: transparent
    public BorderEdge BorderTop { get; init; } = BorderEdge.Initial;
    public BorderEdge BorderRight { get; init; } = BorderEdge.Initial;
    public BorderEdge BorderBottom { get; init; } = BorderEdge.Initial;
    public BorderEdge BorderLeft { get; init; } = BorderEdge.Initial;
    public bool HasPaint => BackgroundColor.HasValue ||
        BorderTop.EffectiveWidth > 0 || BorderRight.EffectiveWidth > 0 ||
        BorderBottom.EffectiveWidth > 0 || BorderLeft.EffectiveWidth > 0;
}

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
