using System.Globalization;

namespace FactsPDF;

/// <summary>
/// The flexbox values the report renderer lays out: a single-line, row-direction flex container
/// (flex-direction:row, flex-wrap:nowrap) with its justify-content, align-items and gaps, and the
/// flex item properties. Every other value fails with FPDF1202 instead of being approximated.
/// </summary>
internal static class CssFlexValues
{
    internal static bool IsFlexProperty(CssProperty property) =>
        property is >= CssProperty.FlexDirection and <= CssProperty.Order;

    // Validates one keyword or number of a flex longhand, as written (CSS-wide keywords excluded).
    internal static void Validate(string name, string value, int offset)
    {
        switch (name)
        {
            case "flex-direction":
                if (value != "row")
                    throw new FactsPdfException("FPDF1202",
                        "flex-direction supports only row: column and reverse flex directions are not implemented.", offset);
                break;
            case "flex-wrap":
                if (value != "nowrap")
                    throw new FactsPdfException("FPDF1202",
                        "flex-wrap supports only nowrap: multi-line flex containers are not implemented.", offset);
                break;
            case "justify-content": _ = Justify(value, offset); break;
            case "align-items": _ = Align(value, offset, self: false); break;
            case "align-self": _ = Align(value, offset, self: true); break;
            case "flex-grow" or "flex-shrink": _ = Factor(value, offset); break;
            case "flex-basis": _ = Basis(value, offset, null, null); break;
            case "column-gap" or "row-gap": _ = Gap(value, offset, null, null); break;
            case "order": _ = Order(value, offset); break;
            default: throw new InvalidOperationException("Not a flex longhand.");
        }
    }

    internal static FlexJustify Justify(string value, int offset) => value switch
    {
        // normal and stretch behave as flex-start in a flex container; left and right are the
        // physical start and end of a left-to-right row.
        "normal" or "flex-start" or "start" or "left" or "stretch" => FlexJustify.Start,
        "flex-end" or "end" or "right" => FlexJustify.End,
        "center" => FlexJustify.Center,
        "space-between" => FlexJustify.SpaceBetween,
        "space-around" => FlexJustify.SpaceAround,
        "space-evenly" => FlexJustify.SpaceEvenly,
        _ => throw new FactsPdfException("FPDF1202",
            "justify-content supports normal, flex-start, start, left, flex-end, end, right, center, stretch, space-between, space-around and space-evenly.", offset)
    };

    // null is align-self:auto.
    internal static FlexAlign? Align(string value, int offset, bool self) => value switch
    {
        "auto" when self => null,
        "normal" or "stretch" => FlexAlign.Stretch,
        "flex-start" or "start" or "self-start" => FlexAlign.Start,
        "flex-end" or "end" or "self-end" => FlexAlign.End,
        "center" => FlexAlign.Center,
        "baseline" => FlexAlign.Baseline,
        _ => throw new FactsPdfException("FPDF1202", (self ? "align-self supports auto, " : "align-items supports ")
            + "normal, stretch, flex-start, start, self-start, flex-end, end, self-end, center and baseline.", offset)
    };

    internal static double Factor(string value, int offset)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor)
            || !double.IsFinite(factor) || factor < 0 || factor > 1_000_000)
            throw new FactsPdfException("FPDF1202",
                "flex-grow and flex-shrink require a nonnegative number.", offset);
        return factor;
    }

    // (null, false) is auto, (null, true) is content.
    internal static (CssLength? Basis, bool Content) Basis(string value, int offset, double? rootFontSize, double? elementFontSize)
    {
        if (value == "auto") return (null, false);
        if (value == "content") return (null, true);
        return (CssBoxValues.Length(value, offset, rootFontSize, allowRem: true,
            elementFontSize: elementFontSize, allowEm: true), false);
    }

    // null is normal.
    internal static CssLength? Gap(string value, int offset, double? rootFontSize, double? elementFontSize)
        => value == "normal" ? null : CssBoxValues.Length(value, offset, rootFontSize, allowRem: true,
            elementFontSize: elementFontSize, allowEm: true);

    internal static int Order(string value, int offset)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var order)
            || order is < -1_000_000 or > 1_000_000)
            throw new FactsPdfException("FPDF1202", "order requires an integer from -1000000 to 1000000.", offset);
        return order;
    }

    // Assigns one computed flex declaration into current, the caller's private box copy.
    internal static void Apply(BoxStyle current, BoxStyle parent, CssDeclaration declaration,
        double rootFontSize, double elementFontSize)
    {
        var value = declaration.Value;
        var wide = value is "inherit" or "initial" or "unset";
        var from = (value == "inherit" ? parent : BoxStyle.Initial).Flex;
        var flex = current.Flex with { };
        switch (declaration.Property)
        {
            // Only row and nowrap exist, which are also the initial values.
            case CssProperty.FlexDirection or CssProperty.FlexWrap: return;
            case CssProperty.JustifyContent:
                flex.JustifyContent = wide ? from.JustifyContent : Justify(value, declaration.Offset); break;
            case CssProperty.AlignItems:
                flex.AlignItems = wide ? from.AlignItems : Align(value, declaration.Offset, self: false)!.Value; break;
            case CssProperty.AlignSelf:
                flex.AlignSelf = wide ? from.AlignSelf : Align(value, declaration.Offset, self: true); break;
            case CssProperty.FlexGrow:
                flex.Grow = wide ? from.Grow : Factor(value, declaration.Offset); break;
            case CssProperty.FlexShrink:
                flex.Shrink = wide ? from.Shrink : Factor(value, declaration.Offset); break;
            case CssProperty.FlexBasis:
                (flex.Basis, flex.BasisContent) = wide ? (from.Basis, from.BasisContent)
                    : Basis(value, declaration.Offset, rootFontSize, elementFontSize);
                break;
            case CssProperty.ColumnGap:
                flex.ColumnGap = wide ? from.ColumnGap : Gap(value, declaration.Offset, rootFontSize, elementFontSize); break;
            case CssProperty.RowGap:
                flex.RowGap = wide ? from.RowGap : Gap(value, declaration.Offset, rootFontSize, elementFontSize); break;
            case CssProperty.Order:
                flex.Order = wide ? from.Order : Order(value, declaration.Offset); break;
            default: throw new InvalidOperationException("Expected a flex property.");
        }
        if (flex != current.Flex) current.Flex = flex;
    }
}
