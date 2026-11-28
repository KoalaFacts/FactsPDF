namespace FactsPDF;

internal enum CssProperty
{
    FontSize, FontWeight, FontStyle, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter, TextDecoration,
    WhiteSpace, OverflowWrap, TextIndent, LetterSpacing, WordSpacing, TextTransform, Width, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft, BackgroundColor,
    BorderTopWidth, BorderRightWidth, BorderBottomWidth, BorderLeftWidth,
    BorderTopStyle, BorderRightStyle, BorderBottomStyle, BorderLeftStyle,
    BorderTopColor, BorderRightColor, BorderBottomColor, BorderLeftColor,
    MarginRight, MarginLeft, BoxSizing, MinWidth, MaxWidth, Display, ListStyleType, VerticalAlign,
    TextDecorationColor, TextDecorationStyle, BorderRadius, MinHeight, Height,
    BorderTopLeftRadius, BorderTopRightRadius, BorderBottomRightRadius, BorderBottomLeftRadius,
    WordBreak, MaxHeight, BreakInside, TextDecorationThickness, TextUnderlineOffset, ListStylePosition,
    OutlineWidth, OutlineStyle, OutlineColor, OutlineOffset, TextAlignLast, BorderCollapse, Orphans, Widows,
    FlexDirection, FlexWrap, JustifyContent, AlignItems, AlignSelf, FlexGrow, FlexShrink, FlexBasis,
    ColumnGap, RowGap, Order, Content, GridTemplateColumns, JustifyItems, JustifySelf,
    BackgroundClip, TableLayout, Hyphens, ObjectFit, ObjectPosition, CounterReset, CounterIncrement, CounterSet, PageName,
    // Never a cascade slot: a custom property declaration (--name, Payload CssCustomValue) and a
    // supported property whose value holds var() (Payload CssPendingValue), which the cascade
    // compiles for each element after substitution.
    Custom, Pending
}
/// <summary>
/// One compiled declaration: what it says (property, name, value, importance) plus its cascade
/// order and source offset. A repeated style attribute is not compiled again: its element reuses
/// the first compile's declarations, and the cascade views each one at the element's own order and
/// offset through a <see cref="Slot"/> it retargets for every element (see
/// <c>CssStylesheets.CompileStyleAttribute</c> and <c>CssStylesheets.Compute</c>), so replaying a
/// style allocates nothing per declaration.
/// </summary>
internal abstract class CssDeclaration
{
    private CssDeclaration(int order, int offset) { Order = order; Offset = offset; }
    internal abstract CssProperty Property { get; }
    internal abstract string Name { get; }
    internal abstract string Value { get; }
    internal abstract bool Important { get; }
    internal int Order { get; private set; }
    internal int Offset { get; private set; }
    internal static CssDeclaration Create(CssProperty property, string name, string value, bool important, int order, int offset) =>
        new Compiled(property, name, value, important, order, offset);
    internal static CssDeclaration Create(CssProperty property, string name, string value, bool important, int order, int offset, object payload) =>
        new Deferred(property, name, value, important, order, offset, payload);
    /// <summary>The <see cref="CssCustomValue"/> or <see cref="CssPendingValue"/> of a
    /// <see cref="CssProperty.Custom"/> or <see cref="CssProperty.Pending"/> declaration.</summary>
    internal object? Payload => (Source as Deferred)?.Carried;
    /// <summary>The compiled declaration this one says the same as; never a <see cref="Slot"/>.</summary>
    internal CssDeclaration Shared => Source;
    private protected abstract Compiled Source { get; }

    private protected class Compiled(CssProperty property, string name, string value, bool important, int order, int offset)
        : CssDeclaration(order, offset)
    {
        internal override CssProperty Property { get; } = property;
        internal override string Name { get; } = name;
        internal override string Value { get; } = value;
        internal override bool Important { get; } = important;
        private protected override Compiled Source => this;
    }

    // A custom property or var() declaration; a separate type so ordinary declarations stay as small.
    private sealed class Deferred(CssProperty property, string name, string value, bool important, int order, int offset, object payload)
        : Compiled(property, name, value, important, order, offset)
    {
        internal object Carried { get; } = payload;
    }

    /// <summary>
    /// A reusable view of a compiled declaration at another cascade order and source offset. The
    /// cascade owns its slots and retargets them for each element, so a slot is only valid until
    /// the next element is computed; anything kept longer keeps <see cref="Shared"/> instead.
    /// </summary>
    internal sealed class Slot() : CssDeclaration(0, 0)
    {
        private Compiled source = null!;
        internal Slot Retarget(CssDeclaration declaration, int order, int offset)
        {
            source = declaration.Source; Order = order; Offset = offset;
            return this;
        }
        internal override CssProperty Property => source.Property;
        internal override string Name => source.Name;
        internal override string Value => source.Value;
        internal override bool Important => source.Important;
        private protected override Compiled Source => source;
    }
}

internal static class CssDeclarations
{
    // Assigns into style, which must be the caller's private copy (see TextStyle), and returns it.
    internal static TextStyle Apply(TextStyle style, TextStyle parent, TextStyle initial, CssDeclaration declaration, bool paragraph, double rootFontSize)
    {
        var property = declaration.Property;
        if (!paragraph && property is CssProperty.MarginTop or CssProperty.MarginBottom or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore or CssProperty.BreakAfter or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent)
            throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings.", declaration.Offset);
        if (declaration.Value is not ("inherit" or "initial" or "unset"))
        {
            if (property == CssProperty.FontSize)
            {
                var keyword = InlineCss.KeywordFontSize(declaration.Value, parent.FontSize);
                if (keyword.HasValue)
                {
                    var computed = keyword.Value;
                    if (!double.IsFinite(computed) || computed is < 1 or > 144)
                        throw new FactsPdfException("FPDF1202",
                            "Computed keyword font-size must be between 1pt and 144pt.", declaration.Offset);
                    style.FontSize = computed;
                    return style;
                }
                var rem = InlineCss.RootRelativeScale(declaration.Value, declaration.Offset, 144);
                if (rem.HasValue)
                {
                    var computed = rootFontSize * rem.Value;
                    if (!double.IsFinite(computed) || computed is < 1 or > 144)
                        throw new FactsPdfException("FPDF1202",
                            "Computed root-relative font-size must be between 1pt and 144pt.", declaration.Offset);
                    style.FontSize = computed;
                    return style;
                }
                var relative = InlineCss.RelativeFontSizeScale(declaration.Value, declaration.Offset);
                if (relative.HasValue)
                {
                    // Relative to the actual computed parent, never the UA
                    // heading size multiplier or a previous losing declaration.
                    var computed = parent.FontSize * relative.Value;
                    if (!double.IsFinite(computed) || computed is < 1 or > 144)
                        throw new FactsPdfException("FPDF1202",
                            "Computed relative font-size must be between 1pt and 144pt.", declaration.Offset);
                    style.FontSize = computed;
                    return style;
                }
            }
            if (property == CssProperty.LineHeight)
            {
                var rem = InlineCss.RootRelativeScale(declaration.Value, declaration.Offset, 1440);
                if (rem.HasValue)
                {
                    var computed = rootFontSize * rem.Value;
                    if (!double.IsFinite(computed) || computed is < 0.1 or > 1440)
                        throw new FactsPdfException("FPDF1202",
                            "Computed root-relative line-height must be between 0.1pt and 1440pt.", declaration.Offset);
                    style.AbsoluteLineHeight = computed;
                    return style;
                }
            }
            if (property is CssProperty.LetterSpacing or CssProperty.WordSpacing or CssProperty.TextIndent
                && declaration.Value.EndsWith("em", StringComparison.Ordinal))
            {
                // Compute the winner after font-size; inherit fixed-point metrics.
                var value = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true).Value;
                if (property is CssProperty.LetterSpacing or CssProperty.WordSpacing && value > 24)
                    throw new FactsPdfException("FPDF1202",
                        "Computed letter/word spacing must be from 0pt to 24pt.", declaration.Offset);
                switch (property)
                {
                    case CssProperty.LetterSpacing: style.LetterSpacing = value; break;
                    case CssProperty.WordSpacing: style.WordSpacing = value; break;
                    case CssProperty.TextIndent: style.FirstLineIndent = new CssLength(value); break;
                    default: throw new InvalidOperationException();
                }
                return style;
            }
            if ((property is CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith("em", StringComparison.Ordinal))
            {
                // Compute only the winning margin: rem from the root,
                // em from this element's final font-size, both as points.
                var margin = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true).Value;
                if (property == CssProperty.MarginTop) { style.MarginBefore = margin; style.PercentMarginBefore = null; }
                else { style.MarginAfter = margin; style.PercentMarginAfter = null; }
                return style;
            }
            if ((property is CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith('%'))
            {
                // CSS vertical percentage margins resolve against containing content width.
                var length = CssBoxValues.Length(declaration.Value, declaration.Offset);
                if (property == CssProperty.MarginTop) { style.MarginBefore = 0; style.PercentMarginBefore = length; }
                else { style.MarginAfter = 0; style.PercentMarginAfter = length; }
                return style;
            }
            if (property == CssProperty.TextDecorationThickness)
            {
                // Compute the winning length after font-size, just as for other
                // relative text metrics; 1em uses this element, 1rem the root.
                if (declaration.Value == "auto")
                {
                    style.DecorationThickness = null;
                    return style;
                }
                var width = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true);
                if (width.IsPercent || width.Value is < 0.2 or > 12)
                    throw new FactsPdfException("FPDF1202",
                        "Computed text-decoration-thickness must be 0.2pt to 12pt; percentages are unsupported.",
                        declaration.Offset);
                style.DecorationThickness = width.Value;
                return style;
            }
            if (property == CssProperty.TextUnderlineOffset)
            {
                // Positive CSS offset is the distance below the alphabetic baseline.
                // Compute after the final font-size winner, never from a UA/default
                // size or from an overridden declaration.
                if (declaration.Value == "auto")
                {
                    style.UnderlineOffset = null;
                    return style;
                }
                var offset = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true);
                if (offset.IsPercent || offset.Value > 24)
                    throw new FactsPdfException("FPDF1202",
                        "Computed text-underline-offset must be from 0pt to 24pt; percentages are unsupported.",
                        declaration.Offset);
                style.UnderlineOffset = offset.Value;
                return style;
            }
            return InlineCss.Apply(style, declaration.Name, declaration.Value, paragraph, inline: false, declaration.Offset);
        }
        var inherited = property is CssProperty.FontSize or CssProperty.FontWeight or CssProperty.FontStyle or CssProperty.Color or CssProperty.LineHeight or CssProperty.TextAlign or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent or CssProperty.LetterSpacing or CssProperty.WordSpacing or CssProperty.TextTransform or CssProperty.TextUnderlineOffset or CssProperty.TextAlignLast or CssProperty.BorderCollapse
            or CssProperty.Orphans or CssProperty.Widows or CssProperty.Hyphens;
        var source = declaration.Value == "inherit" || declaration.Value == "unset" && inherited ? parent : initial;
        switch (property)
        {
            case CssProperty.FontSize: style.FontSize = source.FontSize; break;
            case CssProperty.FontWeight: style.Bold = source.Bold; break;
            case CssProperty.FontStyle: style.Italic = source.Italic; break;
            case CssProperty.Color: style.Color = source.Color; break;
            case CssProperty.LineHeight: style.LineHeight = source.LineHeight; style.AbsoluteLineHeight = source.AbsoluteLineHeight; break;
            case CssProperty.TextAlign: style.Alignment = source.Alignment; break;
            case CssProperty.TextAlignLast: style.LastLineAlignment = source.LastLineAlignment; break;
            case CssProperty.BorderCollapse: style.CollapseBorders = source.CollapseBorders; break;
            case CssProperty.Hyphens: style.NoHyphens = source.NoHyphens; break;
            case CssProperty.Orphans: style.Orphans = source.Orphans; break;
            case CssProperty.Widows: style.Widows = source.Widows; break;
            case CssProperty.TextDecoration: style.Decoration = source.Decoration; break;
            case CssProperty.TextDecorationColor: style.DecorationColor = source.DecorationColor; break;
            case CssProperty.TextDecorationStyle: style.DecorationPattern = source.DecorationPattern; break;
            case CssProperty.TextDecorationThickness: style.DecorationThickness = source.DecorationThickness; break;
            case CssProperty.TextUnderlineOffset: style.UnderlineOffset = source.UnderlineOffset; break;
            case CssProperty.WhiteSpace: style.NoWrap = source.NoWrap; style.PreserveLineBreaks = source.PreserveLineBreaks; style.PreserveSpaces = source.PreserveSpaces; break;
            case CssProperty.OverflowWrap: style.BreakWords = source.BreakWords; style.BreakAnywhere = source.BreakAnywhere; break;
            case CssProperty.WordBreak: style.BreakAll = source.BreakAll; style.BreakWordsByWordBreak = source.BreakWordsByWordBreak; break;
            case CssProperty.TextIndent: style.FirstLineIndent = source.FirstLineIndent; break;
            case CssProperty.LetterSpacing: style.LetterSpacing = source.LetterSpacing; break;
            case CssProperty.WordSpacing: style.WordSpacing = source.WordSpacing; break;
            case CssProperty.TextTransform: style.Transform = source.Transform; break;
            case CssProperty.MarginTop: style.MarginBefore = source.MarginBefore; style.PercentMarginBefore = source.PercentMarginBefore; break;
            case CssProperty.MarginBottom: style.MarginAfter = source.MarginAfter; style.PercentMarginAfter = source.PercentMarginAfter; break;
            case CssProperty.BreakBefore: style.BreakBefore = source.BreakBefore; break;
            case CssProperty.BreakAfter: style.BreakAfter = source.BreakAfter; style.KeepWithNext = source.KeepWithNext; break;
            default: throw new InvalidOperationException("Unrecognized CSS property.");
        }
        return style;
    }
    internal static bool IsBoxProperty(CssProperty property) => property >= CssProperty.Width && property <= CssProperty.MaxWidth || property is CssProperty.BorderRadius or CssProperty.MinHeight or CssProperty.Height or CssProperty.MaxHeight
        or CssProperty.BorderTopLeftRadius or CssProperty.BorderTopRightRadius
        or CssProperty.BorderBottomRightRadius or CssProperty.BorderBottomLeftRadius
        or CssProperty.OutlineWidth or CssProperty.OutlineStyle or CssProperty.OutlineColor or CssProperty.OutlineOffset
        or CssProperty.BackgroundClip or CssProperty.TableLayout or CssProperty.ObjectFit or CssProperty.ObjectPosition
        or CssProperty.CounterReset or CssProperty.CounterIncrement or CssProperty.CounterSet
        || CssFlexValues.IsFlexOrGridProperty(property);

    // Vertical panel margins have a distinct box-level meaning: they sit
    // outside decorations. Paragraphs/headings retain TextStyle margins.
    // Assigns into current, which must be the caller's private copy (see BoxStyle), and returns it.
    internal static BoxStyle ApplyPanelVerticalMargin(BoxStyle current, BoxStyle parent,
        CssDeclaration declaration, double rootFontSize, double elementFontSize)
    {
        var wide = declaration.Value is "inherit" or "initial" or "unset";
        var inherited = declaration.Value == "inherit" ? parent : BoxStyle.Initial;
        var length = wide ? default : CssBoxValues.Length(declaration.Value, declaration.Offset,
            rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
        // Preserve percentage units until the box layout knows the actual
        // containing content width; rem/em have already resolved to points.
        switch (declaration.Property)
        {
            case CssProperty.MarginTop: current.MarginTop = wide ? inherited.MarginTop : length; break;
            case CssProperty.MarginBottom: current.MarginBottom = wide ? inherited.MarginBottom : length; break;
            default: throw new InvalidOperationException("Expected vertical panel margin.");
        }
        return current;
    }

    // Assigns into current, which must be the caller's private copy (see BoxStyle), and returns it.
    internal static BoxStyle ApplyBox(BoxStyle current, BoxStyle parent, CssDeclaration declaration,
        double rootFontSize, double elementFontSize, Rgb elementColor)
    {
        var property = declaration.Property;
        if (!IsBoxProperty(property)) throw new InvalidOperationException("Expected a box property.");
        var value = declaration.Value;
        var wide = value is "inherit" or "initial" or "unset";
        var from = value == "inherit" ? parent : BoxStyle.Initial;

        if (CssFlexValues.IsFlexOrGridProperty(property))
        {
            CssFlexValues.Apply(current, parent, declaration, rootFontSize, elementFontSize);
            return current;
        }
        if (property is CssProperty.BorderTopLeftRadius or CssProperty.BorderTopRightRadius
            or CssProperty.BorderBottomRightRadius or CssProperty.BorderBottomLeftRadius)
        {
            var radius = wide ? property switch
            {
                CssProperty.BorderTopLeftRadius => from.CornerRadius,
                CssProperty.BorderTopRightRadius => from.CornerTopRightRadius,
                CssProperty.BorderBottomRightRadius => from.CornerBottomRightRadius,
                _ => from.CornerBottomLeftRadius
            } : CssPaintValues.BorderRadius(value, declaration.Offset);
            switch (property)
            {
                case CssProperty.BorderTopLeftRadius: current.CornerRadius = radius; break;
                case CssProperty.BorderTopRightRadius: current.CornerTopRightRadius = radius; break;
                case CssProperty.BorderBottomRightRadius: current.CornerBottomRightRadius = radius; break;
                default: current.CornerBottomLeftRadius = radius; break;
            }
            return current;
        }
        if (property == CssProperty.BorderRadius)
        {
            var corners = wide
                ? (from.CornerRadius, from.CornerTopRightRadius,
                    from.CornerBottomRightRadius, from.CornerBottomLeftRadius)
                : CssPaintValues.BorderRadii(value, declaration.Offset);
            {
                current.CornerRadius = corners.Item1;
                current.CornerTopRightRadius = corners.Item2;
                current.CornerBottomRightRadius = corners.Item3;
                current.CornerBottomLeftRadius = corners.Item4;
                return current;
            }
        }
        if (property == CssProperty.MinHeight)
        {
            CssLength? length = wide ? null : CssBoxValues.Length(value, declaration.Offset,
                rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            if (length?.IsPercent == true)
                throw new FactsPdfException("FPDF1202",
                    "Report panel min-height does not support percentages.", declaration.Offset);
            current.MinHeight = wide ? from.MinHeight : length;
            return current;
        }
        if (property == CssProperty.Height)
        {
            CssLength? length = wide || value == "auto" ? null
                : CssBoxValues.Length(value, declaration.Offset, rootFontSize, allowRem: true,
                    elementFontSize: elementFontSize, allowEm: true);
            if (length?.IsPercent == true)
                throw new FactsPdfException("FPDF1202",
                    "Report panel height does not support percentages.", declaration.Offset);
            current.Height = wide ? from.Height : length;
            return current;
        }
        if (property == CssProperty.MaxHeight)
        {
            CssLength? length = wide || value == "none" ? null
                : CssBoxValues.Length(value, declaration.Offset, rootFontSize,
                    allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            if (length?.IsPercent == true)
                throw new FactsPdfException("FPDF1202",
                    "Image max-height does not support percentages.", declaration.Offset);
            current.MaxHeight = wide ? from.MaxHeight : length;
            return current;
        }
        if (property == CssProperty.BoxSizing)
        {
            current.BorderBoxSizing = wide ? from.BorderBoxSizing : value == "border-box";
            return current;
        }
        if (property == CssProperty.Width)
        {
            current.Width = wide ? from.Width : CssBoxValues.Width(value, declaration.Offset, rootFontSize, elementFontSize);
            return current;
        }
        if (property == CssProperty.MinWidth)
        {
            current.MinWidth = wide ? from.MinWidth : value == "auto" ? null : CssBoxValues.Length(value, declaration.Offset,
                rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            return current;
        }
        if (property == CssProperty.MaxWidth)
        {
            current.MaxWidth = wide ? from.MaxWidth : value == "none" ? null : CssBoxValues.Length(value, declaration.Offset,
                rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            return current;
        }
        if (property >= CssProperty.PaddingTop && property <= CssProperty.PaddingLeft)
        {
            var length = wide ? default : CssBoxValues.Length(value, declaration.Offset, rootFontSize, allowRem: true,
                elementFontSize: elementFontSize, allowEm: true);
            switch (property)
            {
                case CssProperty.PaddingTop: current.PaddingTop = wide ? from.PaddingTop : length; break;
                case CssProperty.PaddingRight: current.PaddingRight = wide ? from.PaddingRight : length; break;
                case CssProperty.PaddingBottom: current.PaddingBottom = wide ? from.PaddingBottom : length; break;
                case CssProperty.PaddingLeft: current.PaddingLeft = wide ? from.PaddingLeft : length; break;
                default: throw new InvalidOperationException();
            }
            return current;
        }
        if (property is CssProperty.MarginLeft or CssProperty.MarginRight)
        {
            var auto = !wide && value == "auto";
            var length = wide || auto ? default : CssBoxValues.Length(value, declaration.Offset,
                rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            if (property == CssProperty.MarginLeft)
            {
                current.MarginLeft = wide ? from.MarginLeft : length;
                current.MarginLeftAuto = wide ? from.MarginLeftAuto : auto;
            }
            else
            {
                current.MarginRight = wide ? from.MarginRight : length;
                current.MarginRightAuto = wide ? from.MarginRightAuto : auto;
            }
            return current;
        }
        if (property is CssProperty.CounterReset or CssProperty.CounterIncrement or CssProperty.CounterSet)
        {
            var source = value == "inherit" ? parent.Counters : wide ? CounterOps.None : null;
            var list = source is null ? CounterOps.Parse(value) : null;
            current.Counters = property switch
            {
                CssProperty.CounterReset => current.Counters with { Reset = source is null ? list : source.Reset },
                CssProperty.CounterIncrement => current.Counters with { Increment = source is null ? list : source.Increment },
                _ => current.Counters with { Set = source is null ? list : source.Set }
            };
            return current;
        }
        if (property == CssProperty.TableLayout)
        {
            current.FixedTableLayout = value == "inherit" ? parent.FixedTableLayout : !wide && value == "fixed";
            return current;
        }
        if (property == CssProperty.ObjectFit)
        {
            current.ObjectPlacement = current.ObjectPlacement with
            {
                Fit = value == "inherit" ? parent.ObjectPlacement.Fit : wide ? ObjectFit.Fill : CssObjectValues.Fit(value, declaration.Offset)
            };
            return current;
        }
        if (property == CssProperty.ObjectPosition)
        {
            if (value == "inherit")
                current.ObjectPlacement = current.ObjectPlacement with { X = parent.ObjectPlacement.X, Y = parent.ObjectPlacement.Y };
            else if (wide)
                current.ObjectPlacement = current.ObjectPlacement with { X = ObjectAxis.Centre, Y = ObjectAxis.Centre };
            else
            {
                var (x, y) = CssObjectValues.Position(value, declaration.Offset, rootFontSize, elementFontSize);
                current.ObjectPlacement = current.ObjectPlacement with { X = x, Y = y };
            }
            return current;
        }
        if (property == CssProperty.BackgroundClip)
        {
            current.BackgroundClip = value switch
            {
                "inherit" => parent.BackgroundClip,
                "padding-box" => BackgroundClip.PaddingBox,
                "content-box" => BackgroundClip.ContentBox,
                _ => BackgroundClip.BorderBox
            };
            return current;
        }
        if (property == CssProperty.BackgroundColor)
        {
            // CSS Color: an inherited currentColor background is a retained
            // token, not the parent's already-painted RGB. The child resolves
            // it against its own computed text ink. A literal inherited RGB
            // remains literal, and initial/unset/transparent clear the marker.
            var usesCurrent = wide ? from.BackgroundUsesCurrentColor : value == "currentcolor";
            {
                current.BackgroundUsesCurrentColor = usesCurrent;
                current.BackgroundColor = usesCurrent ? elementColor : wide ? from.BackgroundColor : CssPaintValues.Background(value, declaration.Offset, elementColor);
                return current;
            }
        }

        // Outline is a separate painted box decoration, not a layout edge.
        if (property is CssProperty.OutlineWidth or CssProperty.OutlineStyle
            or CssProperty.OutlineColor or CssProperty.OutlineOffset)
        {
            if (property == CssProperty.OutlineOffset)
            {
                current.OutlineOffset = wide ? from.OutlineOffset : CssPaintValues.OutlineOffset(value, declaration.Offset);
                return current;
            }
            var edge = current.Outline;
            var outlineInherited = from.Outline;
            var outlineChanged = property switch
            {
                CssProperty.OutlineWidth => edge with { Width = wide ? outlineInherited.Width
                    : CssPaintValues.OutlineWidth(value, declaration.Offset) },
                CssProperty.OutlineStyle => edge with
                {
                    Solid = wide ? outlineInherited.Solid : value != "none",
                    Pattern = wide ? outlineInherited.Pattern : CssPaintValues.BorderStyle(value, declaration.Offset)
                },
                CssProperty.OutlineColor => edge with { Color = wide ? outlineInherited.Color
                    : CssPaintValues.BorderColor(value, declaration.Offset) },
                _ => throw new InvalidOperationException()
            };
            {
                current.Outline = outlineChanged;
                return current;
            }
        }

        var index = (int)property - (int)CssProperty.BorderTopWidth;
        var side = index % 4;
        var component = index / 4;
        BorderEdge old = side switch
        {
            0 => current.BorderTop, 1 => current.BorderRight, 2 => current.BorderBottom,
            3 => current.BorderLeft, _ => throw new InvalidOperationException()
        };
        BorderEdge inherited = side switch
        {
            0 => from.BorderTop, 1 => from.BorderRight, 2 => from.BorderBottom,
            3 => from.BorderLeft, _ => throw new InvalidOperationException()
        };
        var changed = component switch
        {
            0 => old with { Width = wide ? inherited.Width : CssPaintValues.BorderWidth(value,
                declaration.Offset, allowRelative: true, rootFontSize: rootFontSize, elementFontSize: elementFontSize) },
            1 => old with
            {
                Solid = wide ? inherited.Solid : value is not ("none" or "hidden"),
                Hidden = wide ? inherited.Hidden : value == "hidden",
                Pattern = wide ? inherited.Pattern : CssPaintValues.EdgeStyle(value, declaration.Offset)
            },
            2 => old with
            {
                Color = wide ? inherited.Color : CssPaintValues.EdgeColor(value, declaration.Offset),
                Transparent = wide ? inherited.Transparent : value == "transparent"
            },
            _ => throw new InvalidOperationException("Unrecognized border property.")
        };
        switch (side)
        {
            case 0: current.BorderTop = changed; break;
            case 1: current.BorderRight = changed; break;
            case 2: current.BorderBottom = changed; break;
            case 3: current.BorderLeft = changed; break;
            default: throw new InvalidOperationException();
        }
        return current;
    }
}
