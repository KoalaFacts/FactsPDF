namespace FactsPDF;

/// <summary>
/// Text decorations propagate to descendant inline boxes (CSS Text Decoration 3): a descendant
/// cannot remove an ancestor's line (<c>text-decoration:none</c> only declares no line of its own),
/// its own lines are added to the ancestors', and every line is painted in the colour, style and
/// thickness of the element that declared it. A text style carries one set of decoration paint, so
/// a descendant that adds lines in a different paint than an ancestor's that stay painted would need
/// two layers: that fails rather than repainting the ancestor's lines in the wrong colour.
/// </summary>
internal static class DecorationPropagation
{
    /// <summary>The decoration lines a winning text-decoration declaration names for the element itself.</summary>
    internal static TextDecoration DeclaredLines(string? value, int offset, TextStyle parent) => value switch
    {
        null or "none" or "initial" or "unset" or "revert" or "revert-layer" => TextDecoration.None,
        "inherit" => parent.Decoration,
        _ => InlineCss.ParseDecorationLines(value, offset),
    };

    private static double Thickness(TextStyle style) => style.DecorationThickness ?? Math.Max(0.3, style.FontSize * 0.06);

    /// <summary>
    /// Finishes the decoration of an element whose parent is decorated: the element's computed style
    /// (copied first when it is shared) paints the inherited lines as their declaring element does,
    /// not in the element's own colour and size.
    /// </summary>
    internal static void Resolve(ref TextStyle style, ref bool owned, TextStyle parent, TextDecoration declared, int offset)
    {
        if (parent.Decoration == TextDecoration.None) return;
        var inheritedColor = parent.DecorationColor ?? parent.Color;
        var parentThickness = Thickness(parent);
        if (declared == TextDecoration.None)
        {
            // Declaring no lines declares no paint either: the ancestors' lines keep their own.
            if (style.DecorationColor == inheritedColor && style.DecorationPattern == parent.DecorationPattern
                && style.DecorationThickness == parentThickness) return;
            if (!owned) { style = style with { }; owned = true; }
            style.DecorationColor = inheritedColor;
            style.DecorationPattern = parent.DecorationPattern;
            style.DecorationThickness = parentThickness;
            return;
        }
        var color = style.DecorationColor ?? style.Color;
        var samePaint = color == inheritedColor && style.DecorationPattern == parent.DecorationPattern
            && Thickness(style) == parentThickness;
        if (samePaint || (parent.Decoration & ~declared) == TextDecoration.None) return;
        throw new FactsPdfException("FPDF1201",
            "A decorated element inside another decorated element may add lines only in the colour, style and thickness of the lines it inherits (or repeat all of them); two layers of decoration paint are not supported.",
            offset);
    }
}
