namespace FactsPDF;

/// <summary>
/// The box of an inline element that has more than its text style: padding, a border, margins, a
/// background, a vertical-align or a font size or line height of its own. The reader brackets the
/// element's runs with an <see cref="InlineBoxRun"/> start and end; line layout turns them into
/// edges that take room in the line, a baseline shift for the text inside and, for each line the
/// box spans, one painted fragment (box-decoration-break:slice).
/// </summary>
internal sealed class InlineBox(BoxStyle box, TextStyle style, TextStyle parentStyle, int sourceOffset)
{
    /// <summary>The element's computed box: padding, border, margins, background and vertical-align.</summary>
    internal BoxStyle Box { get; } = box;

    /// <summary>The element's computed text style, whose font and line height size its content area and its leading.</summary>
    internal TextStyle Style { get; } = style;

    /// <summary>The style of the element's parent, whose font size sub and super shift by and whose font box text-top, text-bottom and middle align to.</summary>
    internal TextStyle ParentStyle { get; } = parentStyle;

    internal int SourceOffset { get; } = sourceOffset;

    internal InlineVerticalAlign Align => Box.InlineAlign;

    /// <summary>Set by the reader when this box's start created the paragraph that holds it.</summary>
    internal bool OpenedParagraph { get; set; }

    /// <summary>
    /// Whether the computed box has padding, a border, a margin, a background or a vertical-align,
    /// which a blockified flex or grid item would have to lay out as a block.
    /// </summary>
    internal static bool HasBoxFeatures(BoxStyle box) =>
        box.InlineAlign.Kind != InlineAlignKind.Baseline || box.InlineAlign.Shift != 0
        || HasHorizontalEdgeOf(box) || box.BackgroundColor.HasValue
        || box.PaddingTop.Value != 0 || box.PaddingBottom.Value != 0
        || box.BorderTop.EffectiveWidth > 0 || box.BorderBottom.EffectiveWidth > 0;

    /// <summary>
    /// Whether the box has any horizontal margin, border or padding, which gives the line it sits in
    /// content even when the box holds nothing (CSS 2 section 9.4.2).
    /// </summary>
    internal bool HasHorizontalEdge => HasHorizontalEdgeOf(Box);

    internal static bool HasHorizontalEdgeOf(BoxStyle box) =>
        box.PaddingLeft.Value != 0 || box.PaddingRight.Value != 0 || box.MarginLeft.Value != 0 || box.MarginRight.Value != 0
        || box.BorderLeft.EffectiveWidth > 0 || box.BorderRight.EffectiveWidth > 0;

    /// <summary>
    /// Whether an inline element is bracketed by an inline box: it has padding, a border, margins or a
    /// background to lay out or paint, a vertical-align other than baseline, or a font size or line
    /// height that differs from its parent's (so that even an empty element changes the line's height).
    /// </summary>
    internal static bool Needed(BoxStyle box, TextStyle style, TextStyle parentStyle) =>
        box.InlineAlign.Kind != InlineAlignKind.Baseline || box.InlineAlign.Shift != 0
        || HasHorizontalEdgeOf(box) || box.BackgroundColor.HasValue
        || box.PaddingTop.Value != 0 || box.PaddingBottom.Value != 0
        || box.BorderTop.EffectiveWidth > 0 || box.BorderBottom.EffectiveWidth > 0
        || style.FontSize != parentStyle.FontSize || style.LineHeight != parentStyle.LineHeight
        || style.AbsoluteLineHeight != parentStyle.AbsoluteLineHeight;

    /// <summary>The widths of a box's edges against a containing block of the given width.</summary>
    internal readonly record struct Edges(double MarginLeft, double BorderLeft, double PaddingLeft,
        double PaddingRight, double BorderRight, double MarginRight, double PaddingTop, double PaddingBottom,
        double BorderTop, double BorderBottom)
    {
        internal double Start => MarginLeft + BorderLeft + PaddingLeft;
        internal double End => PaddingRight + BorderRight + MarginRight;
    }

    /// <summary>
    /// The box's edges for a containing block of width <paramref name="containingWidth"/>; NaN (the
    /// measure of automatic table layout, where percentages have nothing to resolve against) fails
    /// for a percentage.
    /// </summary>
    internal Edges Resolve(double containingWidth)
    {
        double Of(CssLength length)
        {
            if (!length.IsPercent) return length.Value;
            if (double.IsNaN(containingWidth))
                throw new FactsPdfException("FPDF1302",
                    "Percentage inline margins and padding are not supported in automatic table layout; give every column an explicit col width.",
                    SourceOffset);
            return length.Resolve(containingWidth);
        }
        return new(Box.MarginLeftAuto ? 0 : Of(Box.MarginLeft), Box.BorderLeft.EffectiveWidth, Math.Max(0, Of(Box.PaddingLeft)),
            Math.Max(0, Of(Box.PaddingRight)), Box.BorderRight.EffectiveWidth, Box.MarginRightAuto ? 0 : Of(Box.MarginRight),
            Math.Max(0, Of(Box.PaddingTop)), Math.Max(0, Of(Box.PaddingBottom)),
            Box.BorderTop.EffectiveWidth, Box.BorderBottom.EffectiveWidth);
    }
}
