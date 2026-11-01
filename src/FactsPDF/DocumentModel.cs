namespace FactsPDF;

internal enum TextAlignment { Left, Center, Right, Justify }
// Table-cell content placement within a measured whole row; not CSS inline baseline alignment.
internal enum CellVerticalAlign { Top, Middle, Bottom }
internal enum TextDecoration { None, Underline, LineThrough, Overline }
internal enum TextDecorationPattern { Solid, Dashed, Dotted }
internal enum TextTransform { None, Uppercase, Lowercase, Capitalize }
// This is the renderer's bounded outside-list marker selection, not CSS counters.
internal enum ListMarkerKind { Disc, Circle, Square, Decimal, LowerAlpha, UpperAlpha, LowerRoman, UpperRoman, None }
internal readonly record struct Rgb(double R, double G, double B);
internal sealed record TextStyle(double FontSize, double LineHeight, Rgb Color, TextAlignment Alignment,
    double MarginBefore = 0, double MarginAfter = 0, bool BreakBefore = false, bool BreakAfter = false,
    bool Bold = false, bool Italic = false, bool KeepWithNext = false,
    TextDecoration Decoration = TextDecoration.None, double? AbsoluteLineHeight = null,
    bool NoWrap = false, CssLength FirstLineIndent = default, double LetterSpacing = 0, double WordSpacing = 0, TextTransform Transform = TextTransform.None,
    bool BreakWords = false, bool PreserveLineBreaks = false, bool PreserveSpaces = false,
    Rgb? DecorationColor = null, TextDecorationPattern DecorationPattern = TextDecorationPattern.Solid,
    bool BreakAll = false);
// Diagnostic provenance is separate from TextStyle equality: adjacent identical
// computed styles must still coalesce regardless of their source elements.
internal sealed record TextRun(string Text, TextStyle Style, bool IsBreak = false, int SourceOffset = -1);
internal sealed record Paragraph(TextStyle Style, int SourceOffset = -1)
{
    public List<TextRun> Runs { get; } = [];
}
internal sealed record PlacedText(string Text, double X, double Baseline, TextStyle Style, PdfFont? Font = null, double Width = 0);
internal sealed record PlacedDisc(double X, double Y, double Radius, Rgb Color, ListMarkerKind Kind = ListMarkerKind.Disc);
internal sealed record PlacedImage(PdfImage Image, double X, double Top, double Width, double Height);
internal sealed class LayoutPage
{
    public List<PlacedText> Runs { get; } = [];
    public List<PaintedBox> PaintBoxes { get; } = [];
    public List<PlacedDisc> Discs { get; } = [];
    public List<PlacedImage> Images { get; } = [];
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
            if (Style.HasRoundedCorners)
                return Width > 0 && Height > 0
                    ? (Style.BackgroundColor.HasValue ? 1 : 0)
                      + (Style.BorderLeft.EffectiveWidth > 0 ? 1 : 0)
                    : 0;
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
// The pattern affects PDF strokes, while the existing Solid bit continues to
// distinguish painted borders from CSS none without changing legacy boxes.
internal enum BorderPattern { Solid, Dashed, Dotted }
internal readonly record struct BorderEdge(double Width, bool Solid, Rgb? Color)
{
    internal BorderPattern Pattern { get; init; } = BorderPattern.Solid;
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
    // Report panel vertical margins are outside paint and have no general CSS
    // collapsing behaviour. Existing paragraph/heading vertical margins remain
    // in TextStyle, where their historical overlap rules are preserved.
    public CssLength MarginTop { get; init; }
    public CssLength MarginBottom { get; init; }
    // Outside the paragraph/heading border box; percentages use the containing width.
    public CssLength MarginLeft { get; init; }
    public CssLength MarginRight { get; init; }
    // Auto is distinguished from an explicit zero without changing legacy lengths.
    public bool MarginLeftAuto { get; init; }
    public bool MarginRightAuto { get; init; }
    // Explicit width includes padding/borders when requested; defaults unchanged.
    public bool BorderBoxSizing { get; init; }
    // Only ordinary block display:none is modelled. The parser removes the
    // complete validated subtree before any PDF layout or paint commands.
    public bool DisplayNone { get; init; }
    // Only td/th cells consume this property; rows needing fragmentation reject middle/bottom.
    public CellVerticalAlign VerticalAlign { get; init; } = CellVerticalAlign.Top;
    // Unlike painted box sides, list-style-type is inherited through li and
    // other containers. The ol/ul HTML defaults are applied in CSS computing.
    public ListMarkerKind MarkerKind { get; init; } = ListMarkerKind.Disc;
    // Opt-in width constraints. Percentages resolve against the containing
    // content width; the constrained measure follows box-sizing.
    public CssLength? MinWidth { get; init; }
    public CssLength? MaxWidth { get; init; }
    // Nonnegative minimum content-box height, or border-box with box-sizing.
    // Only ordinary report panels consume it during PDF page placement.
    public CssLength? MinHeight { get; init; }
    // Only explicitly bound images consume a physical maximum height.
    public CssLength? MaxHeight { get; init; }
    // Fixed, non-fragmented report panels reject overflow instead of clipping.
    public CssLength? Height { get; init; }
    // Explicit before/after page boundaries for ordinary report panels.
    // These never inherit unless the winning CSS keyword is inherit.
    public bool BreakBefore { get; init; }
    public bool BreakAfter { get; init; }
    public Rgb? BackgroundColor { get; init; } // null: transparent
    // Rounded-corner shorthand computes TL / TR / BR / BL physical radii.
    // CornerRadius keeps the original TL member so one-value reports retain
    // their source layout and exact legacy PDF bytes.
    public double CornerRadius { get; init; }
    public double CornerTopRightRadius { get; init; }
    public double CornerBottomRightRadius { get; init; }
    public double CornerBottomLeftRadius { get; init; }
    public bool HasRoundedCorners => CornerRadius > 0 || CornerTopRightRadius > 0 ||
        CornerBottomRightRadius > 0 || CornerBottomLeftRadius > 0;
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
    // Bounded HTML ordered-list sequence metadata. These are parsed values,
    // never generated child nodes; a tree may be laid out repeatedly.
    public int? ListStart { get; init; }
    public int? ListValue { get; init; }
    public bool ListReversed { get; init; }
}

internal sealed record ParagraphNode(Paragraph Paragraph, bool IsAnonymous) : BlockChild;

internal sealed record ImageNode(PdfImage Image, TextStyle Style, BoxStyle BoxStyle,
    int SourceOffset, string? Alt) : BlockChild;

internal sealed class DocumentRoot
{
    public List<BlockChild> Children { get; } = [];
    public bool HasLists { get; set; }
}
