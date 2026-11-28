namespace FactsPDF;

/// <summary>
/// The Tagged PDF structure type an HTML element becomes. <see cref="None"/> elements (html, body, span,
/// b, i ...) are transparent: their content belongs to the nearest enclosing structural element.
/// <see cref="Artifact"/> is not a structure type: it marks a decorative img or svg whose paint is an
/// artifact. Dt and Dd are the two halves of a description list entry and are written as LI.
/// </summary>
internal enum StructureRole : byte
{
    None, Artifact,
    Art, Sect, Aside, Div, BlockQuote, Caption, P, H1, H2, H3, H4, H5, H6,
    L, LI, Dt, Dd, Table, THead, TBody, TFoot, TR, TH, TD,
    Span, Strong, Em, Quote, Code, Link, Figure,
}

internal static class StructureRoles
{
    /// <summary>The PDF name of the structure type, without the slash (Strong and Em go through the RoleMap).</summary>
    internal static string Name(StructureRole role) => role switch
    {
        StructureRole.Art => "Art", StructureRole.Sect => "Sect", StructureRole.Aside => "Aside", StructureRole.Div => "Div",
        StructureRole.BlockQuote => "BlockQuote", StructureRole.Caption => "Caption", StructureRole.P => "P",
        StructureRole.H1 => "H1", StructureRole.H2 => "H2", StructureRole.H3 => "H3",
        StructureRole.H4 => "H4", StructureRole.H5 => "H5", StructureRole.H6 => "H6",
        StructureRole.L => "L", StructureRole.LI or StructureRole.Dt or StructureRole.Dd => "LI",
        StructureRole.Table => "Table", StructureRole.THead => "THead", StructureRole.TBody => "TBody",
        StructureRole.TFoot => "TFoot", StructureRole.TR => "TR", StructureRole.TH => "TH", StructureRole.TD => "TD",
        StructureRole.Span => "Span", StructureRole.Strong => "Strong", StructureRole.Em => "Em",
        StructureRole.Quote => "Quote", StructureRole.Code => "Code", StructureRole.Link => "Link",
        StructureRole.Figure => "Figure",
        _ => throw new InvalidOperationException("The role has no structure type."),
    };

    /// <summary>The PDF 1.7 structure type a marked-content tag and the RoleMap use for the role: Strong and Em are
    /// Span, Aside (a PDF 2.0 type, as Chrome writes it) is Sect.</summary>
    internal static string StandardName(StructureRole role) => role switch
    {
        StructureRole.Strong or StructureRole.Em => "Span",
        StructureRole.Aside => "Sect",
        _ => Name(role),
    };

    /// <summary>The structure type for an element tag, or None when the element is transparent.</summary>
    internal static StructureRole FromTag(string tag) => tag switch
    {
        "div" or "header" or "footer" or "main" or "nav" or "hgroup" or "search" or "address" or "figure" or "details" or "form" or "fieldset" => StructureRole.Div,
        "aside" => StructureRole.Aside,
        "section" => StructureRole.Sect,
        "article" => StructureRole.Art,
        "blockquote" => StructureRole.BlockQuote,
        "figcaption" or "caption" or "legend" => StructureRole.Caption,
        "p" or "pre" => StructureRole.P,
        "h1" => StructureRole.H1, "h2" => StructureRole.H2, "h3" => StructureRole.H3,
        "h4" => StructureRole.H4, "h5" => StructureRole.H5, "h6" => StructureRole.H6,
        "ul" or "ol" or "menu" or "dl" => StructureRole.L,
        "li" => StructureRole.LI, "dt" => StructureRole.Dt, "dd" => StructureRole.Dd,
        "table" => StructureRole.Table, "thead" => StructureRole.THead, "tbody" => StructureRole.TBody,
        "tfoot" => StructureRole.TFoot, "tr" => StructureRole.TR, "th" => StructureRole.TH, "td" => StructureRole.TD,
        "strong" => StructureRole.Strong, "em" => StructureRole.Em, "q" => StructureRole.Quote,
        "code" or "kbd" or "samp" => StructureRole.Code, "a" => StructureRole.Link,
        "img" or "svg" => StructureRole.Figure,
        _ => StructureRole.None,
    };

    /// <summary>Block-level grouping types that cannot hold marked content of their own: text directly
    /// inside one goes into an implied P.</summary>
    internal static bool IsGrouping(StructureRole role) => role is StructureRole.Art or StructureRole.Sect
        or StructureRole.Aside or StructureRole.Div or StructureRole.BlockQuote or StructureRole.L or StructureRole.LI or StructureRole.Dt
        or StructureRole.Dd or StructureRole.Table or StructureRole.THead or StructureRole.TBody
        or StructureRole.TFoot or StructureRole.TR;

    /// <summary>The heading level 1-6 of H1-H6, otherwise 0.</summary>
    internal static int HeadingLevel(StructureRole role) => role is >= StructureRole.H1 and <= StructureRole.H6
        ? role - StructureRole.H1 + 1 : 0;
}
