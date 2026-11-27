using System.Globalization;
using System.Text;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// The page context of CSS Paged Media, compiled from every <c>@page</c> rule (top level or inside
/// <c>@media print</c>/<c>all</c>): <c>size</c>, <c>margin</c> and its longhands, and the six top and
/// bottom page-margin boxes with <c>content</c> strings, <c>counter(page)</c>, <c>counter(pages)</c>
/// and text styles. The rules cascade by importance and source order like element rules. They resolve
/// against the writer's options once the document is read (the page context inherits from the html
/// element), and the margin boxes render through the structured page furniture.
/// </summary>
internal sealed class CssPageRules
{
    private const double Tolerance = 0.000001;
    private static readonly double Millimetre = 72d / 25.4d;
    private static readonly string[] BoxNames = ["top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right"];
    private static readonly string[] UnsupportedBoxNames = ["top-left-corner", "top-right-corner", "bottom-left-corner",
        "bottom-right-corner", "left-top", "left-middle", "left-bottom", "right-top", "right-middle", "right-bottom"];

    private readonly record struct Cascaded<T>(T Value, bool Important, int Order, int Offset);
    // Width and Height are set together by lengths or a keyword; Landscape by an orientation keyword.
    private readonly record struct PageSize(bool Auto, double? Width, double? Height, bool? Landscape);
    private sealed class MarginBox
    {
        // A null template is content:none (or normal), which generates no box.
        internal Cascaded<string?>? Content;
        internal readonly List<CssDeclaration> Styles = [];
    }

    private Cascaded<PageSize>? size;
    // Top, right, bottom, left.
    private readonly CssDeclaration?[] margins = new CssDeclaration?[4];
    private readonly MarginBox?[] boxes = new MarginBox?[6];

    private static bool Wins(bool important, int order, bool currentImportant, int currentOrder)
        => important != currentImportant ? important : order >= currentOrder;

    internal void Add(CssAtRuleNode rule, CssBudget budget)
    {
        if (rule.Contents is null)
            throw new FactsPdfException("FPDF1204", "@page needs a declaration block.", rule.Span.Start);
        if (HasPrelude(rule.Prelude))
            throw new FactsPdfException("FPDF1204",
                "Page selectors (:first, :left, :right, :blank) and named pages are not supported; use @page without a selector.",
                rule.Span.Start);
        var marginNodes = new List<CssDeclarationNode>();
        foreach (var content in rule.Contents)
        {
            budget.Token.ThrowIfCancellationRequested();
            switch (content)
            {
                case CssDeclarationNode declaration:
                    var name = declaration.Name.ToLowerInvariant();
                    if (name == "size")
                    {
                        var order = budget.Declaration(declaration.Span.Start);
                        var value = ParseSize(declaration);
                        if (size is not { } current || Wins(declaration.Important, order, current.Important, current.Order))
                            size = new(value, declaration.Important, order, declaration.Span.Start);
                    }
                    else if (name is "margin" or "margin-top" or "margin-right" or "margin-bottom" or "margin-left")
                        marginNodes.Add(declaration);
                    else
                        throw new FactsPdfException("FPDF1201",
                            $"CSS property '{declaration.Name}' is not supported in @page; only size, margin and margin-top/right/bottom/left are.",
                            declaration.Span.Start);
                    break;
                case CssAtRuleNode box:
                    AddBox(box, budget);
                    break;
                default:
                    throw new FactsPdfException("FPDF1204",
                        "@page contains only declarations and page-margin boxes.", content.Span.Start);
            }
        }
        foreach (var declaration in CssSyntaxAdapter.CompileKnownDeclarations(marginNodes, budget))
        {
            var side = declaration.Property switch
            {
                CssProperty.MarginTop => 0, CssProperty.MarginRight => 1,
                CssProperty.MarginBottom => 2, CssProperty.MarginLeft => 3,
                _ => throw new InvalidOperationException("Unexpected @page margin property.")
            };
            if (margins[side] is not { } current || Wins(declaration.Important, declaration.Order, current.Important, current.Order))
                margins[side] = declaration;
        }
    }

    private void AddBox(CssAtRuleNode box, CssBudget budget)
    {
        var name = box.Name.ToLowerInvariant();
        var index = Array.IndexOf(BoxNames, name);
        if (index < 0)
            throw new FactsPdfException("FPDF1204", Array.IndexOf(UnsupportedBoxNames, name) >= 0
                ? $"Page-margin box @{name} is not supported; use @top-left, @top-center, @top-right, @bottom-left, @bottom-center or @bottom-right."
                : $"@{box.Name} is not supported inside @page.", box.Span.Start);
        if (box.Contents is null || HasPrelude(box.Prelude))
            throw new FactsPdfException("FPDF1204", $"@{name} needs a declaration block and no prelude.", box.Span.Start);
        var target = boxes[index] ??= new();
        var styles = new List<CssDeclarationNode>();
        foreach (var content in box.Contents)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (content is not CssDeclarationNode declaration)
                throw new FactsPdfException("FPDF1204", "A page-margin box contains only declarations.", content.Span.Start);
            var property = declaration.Name.ToLowerInvariant();
            if (property == "content")
            {
                var order = budget.Declaration(declaration.Span.Start);
                var template = ParseContent(declaration);
                if (target.Content is not { } current || Wins(declaration.Important, order, current.Important, current.Order))
                    target.Content = new(template, declaration.Important, order, declaration.Span.Start);
            }
            else if (property is "color" or "font-size" or "font-weight" or "font-style" or "font-family")
                styles.Add(declaration);
            else
                throw new FactsPdfException("FPDF1201",
                    $"CSS property '{declaration.Name}' is not supported in a page-margin box; only content, color, font-size, font-weight, font-style and font-family are.",
                    declaration.Span.Start);
        }
        target.Styles.AddRange(CssSyntaxAdapter.CompileKnownDeclarations(styles, budget));
    }

    private static bool HasPrelude(IReadOnlyList<CssSyntaxComponent> prelude)
    {
        foreach (var component in prelude)
            if (component is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) return true;
        return false;
    }

    private static List<CssSyntaxComponent> Values(IReadOnlyList<CssSyntaxComponent> values)
    {
        var result = new List<CssSyntaxComponent>(values.Count);
        foreach (var value in values)
            if (value is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) result.Add(value);
        return result;
    }

    // auto | <length>{1,2} | <page-size> || [ portrait | landscape ]
    private static PageSize ParseSize(CssDeclarationNode declaration)
    {
        var offset = declaration.Span.Start;
        var values = Values(declaration.Values);
        var tokens = new List<CssSyntaxToken>(values.Count);
        foreach (var value in values)
            if (value is CssTokenComponent { Token: var token }) tokens.Add(token);
            else throw InvalidSize(offset);
        if (tokens.Count is < 1 or > 2) throw InvalidSize(offset);
        if (tokens.TrueForAll(t => t.Kind == CssSyntaxTokenKind.Ident))
        {
            if (tokens.Count == 1 && tokens[0].Value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return new(true, null, null, null);
            (double Width, double Height)? keyword = null; bool? landscape = null;
            foreach (var token in tokens)
            {
                var word = token.Value.ToLowerInvariant();
                if (word is "portrait" or "landscape" && landscape is null) landscape = word == "landscape";
                else if (keyword is null && PageSizeKeyword(word) is { } paper) keyword = paper;
                else throw InvalidSize(offset);
            }
            return new(false, keyword?.Width, keyword?.Height, landscape);
        }
        var lengths = new double[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != CssSyntaxTokenKind.Dimension || token.Unit is null
                || token.Unit.ToLowerInvariant() is not ("pt" or "px" or "mm" or "cm" or "in" or "pc" or "q")
                || !CssBoxValues.TryLength(token.Value + token.Unit.ToLowerInvariant(), out var length, out _))
                throw new FactsPdfException("FPDF1202",
                    "@page size lengths need pt, px, mm, cm, in, pc or q; a length cannot be combined with a page-size keyword or orientation.",
                    offset);
            if (length.Value is < 72 or > 14_400)
                throw new FactsPdfException("FPDF1202", "@page size sides must be from 72 to 14400 points.", offset);
            lengths[i] = length.Value;
        }
        return new(false, lengths[0], lengths[^1], null);
    }

    private static FactsPdfException InvalidSize(int offset) => new("FPDF1202",
        "@page size takes auto, one or two lengths, or a page-size keyword (A5, A4, A3, B5, B4, JIS-B5, JIS-B4, letter, legal, ledger) with or without portrait or landscape.",
        offset);

    // Portrait sizes in points. A4 and letter are the writer's own A4() and Letter() sizes, so a
    // matching writer page size and CSS keyword agree exactly.
    private static (double Width, double Height)? PageSizeKeyword(string word) => word switch
    {
        "a5" => (148 * Millimetre, 210 * Millimetre),
        "a4" => (595.28, 841.89),
        "a3" => (297 * Millimetre, 420 * Millimetre),
        "b5" => (176 * Millimetre, 250 * Millimetre),
        "b4" => (250 * Millimetre, 353 * Millimetre),
        "jis-b5" => (182 * Millimetre, 257 * Millimetre),
        "jis-b4" => (257 * Millimetre, 364 * Millimetre),
        "letter" => (612, 792),
        "legal" => (612, 1008),
        "ledger" => (792, 1224),
        _ => null
    };

    // none | normal | [ <string> | counter(page) | counter(pages) ]+, as a page furniture template.
    private static string? ParseContent(CssDeclarationNode declaration)
    {
        var offset = declaration.Span.Start;
        var values = Values(declaration.Values);
        if (values.Count == 1 && values[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } keyword }
            && keyword.Value.ToLowerInvariant() is "none" or "normal")
            return null;
        if (values.Count == 0) throw InvalidContent(offset);
        var template = new StringBuilder();
        foreach (var value in values)
        {
            switch (value)
            {
                case CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.String } text }:
                    foreach (var c in text.Value) template.Append(c is '{' or '}' ? new string(c, 2) : c.ToString());
                    break;
                case CssFunctionComponent function when function.Name.Equals("counter", StringComparison.OrdinalIgnoreCase):
                    template.Append(Counter(function, offset));
                    break;
                default:
                    throw InvalidContent(offset);
            }
        }
        return template.ToString();
    }

    private static string Counter(CssFunctionComponent function, int offset)
    {
        var arguments = Values(function.Values);
        if (arguments.Count is not (1 or 3)
            || arguments[0] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } name }
            || name.Value is not ("page" or "pages")
            || arguments.Count == 3 && (arguments[1] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }
                || arguments[2] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } style }
                || !style.Value.Equals("decimal", StringComparison.OrdinalIgnoreCase)))
            throw new FactsPdfException("FPDF1202",
                "Page-margin counters support only counter(page) and counter(pages), in decimal.", offset);
        return name.Value == "page" ? "{page}" : "{pages}";
    }

    private static FactsPdfException InvalidContent(int offset) => new("FPDF1202",
        "Page-margin content supports none, normal, strings, counter(page) and counter(pages).", offset);

    /// <summary>
    /// The writer's options with this page context applied. The page context inherits from the
    /// html element (<paramref name="rootStyle"/>), or from the writer's initial text without one.
    /// A CSS value the writer also set explicitly must agree with it (FPDF1206), and margin boxes
    /// cannot be combined with writer page furniture, page numbers or header/footer text.
    /// </summary>
    internal PdfOptions Apply(PdfOptions options, TextStyle? rootStyle)
    {
        var initial = new TextStyle(options.FontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var context = rootStyle ?? initial;

        var (width, height) = (options.PageWidth, options.PageHeight);
        if (size is { Value.Auto: false } declared)
        {
            var value = declared.Value;
            var (w, h) = value.Width is { } fixedWidth ? (fixedWidth, value.Height!.Value) : (width, height);
            if (value.Landscape == true && w < h || value.Landscape == false && w > h) (w, h) = (h, w);
            if (options.PageSizeExplicit && (Math.Abs(w - width) > Tolerance || Math.Abs(h - height) > Tolerance))
                throw new FactsPdfException("FPDF1206", string.Create(CultureInfo.InvariantCulture,
                    $"@page size {w:0.###} x {h:0.###}pt conflicts with the writer's page size {width:0.###} x {height:0.###}pt; set the page size in one place."),
                    declared.Offset);
            (width, height) = (w, h);
        }

        var sides = new double[4];
        for (var i = 0; i < sides.Length; i++)
        {
            sides[i] = options.Margin;
            if (margins[i] is not { } declaration || declaration.Value is "initial" or "unset" or "inherit" or "revert") continue;
            if (!CssBoxValues.TryLength(declaration.Value, out var length, out _, context.FontSize, allowRem: true,
                    context.FontSize, allowEm: true) || length.IsPercent)
                throw new FactsPdfException("FPDF1202",
                    "@page margins need a nonnegative length in pt, px, mm, cm, in, pc, q, em or rem.", declaration.Offset);
            if (options.MarginExplicit && Math.Abs(length.Value - options.Margin) > Tolerance)
                throw new FactsPdfException("FPDF1206", string.Create(CultureInfo.InvariantCulture,
                    $"@page {declaration.Name} {length.Value:0.###}pt conflicts with the writer's margin {options.Margin:0.###}pt; set the margins in one place."),
                    declaration.Offset);
            sides[i] = length.Value;
        }
        if (sides[0] + sides[2] >= height || sides[1] + sides[3] >= width)
            throw new FactsPdfException("FPDF1202", "@page margins leave no page area.",
                margins.FirstOrDefault(m => m is not null)?.Offset ?? size?.Offset ?? -1);
        var uniform = sides[0] == sides[1] && sides[1] == sides[2] && sides[2] == sides[3];
        options = options with
        {
            PageWidth = width,
            PageHeight = height,
            Margin = uniform ? sides[0] : options.Margin,
            SideMargins = uniform ? null : new PageMargins(sides[0], sides[1], sides[2], sides[3]),
        };

        var texts = new PdfPageText?[boxes.Length];
        var firstBoxOffset = -1;
        for (var i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] is not { Content: { Value: { } template } content } box) continue;
            if (firstBoxOffset < 0) firstBoxOffset = content.Offset;
            if (template.Length > 1024)
                throw new FactsPdfException("FPDF1001", $"@{BoxNames[i]} content exceeds 1024 UTF-16 units.", content.Offset);
            var style = context with { };
            foreach (var declaration in box.Styles.OrderBy(d => d.Important).ThenBy(d => d.Order))
                // Every page-margin box property is inherited, so revert (rolling the author origin
                // back) takes the page context's value, like inherit.
                style = CssDeclarations.Apply(style, context, initial, declaration.Value == "revert"
                    ? CssDeclaration.Create(declaration.Property, declaration.Name, "inherit", declaration.Important, declaration.Order, declaration.Offset)
                    : declaration, paragraph: false, context.FontSize);
            if (!double.IsFinite(style.FontSize) || style.FontSize is < 1 or > 144)
                throw new FactsPdfException("FPDF1202", "Page-margin font-size must be from 1 to 144 points.", content.Offset);
            texts[i] = new PdfPageText
            {
                Template = template,
                FontSize = style.FontSize,
                Color = new(Channel(style.Color.R), Channel(style.Color.G), Channel(style.Color.B)),
                Bold = style.Bold,
                Italic = style.Italic,
            };
        }
        if (firstBoxOffset < 0) return options;
        if (options.PageFurniture is not null || options.PageNumbers || options.HeaderText is not null || options.FooterText is not null)
            throw new FactsPdfException("FPDF1206",
                "@page margin boxes cannot be combined with writer page furniture, page numbers or header/footer text; use one of them.",
                firstBoxOffset);
        return options with
        {
            PageFurniture = new()
            {
                Default = new()
                {
                    HeaderLeft = texts[0], HeaderCenter = texts[1], HeaderRight = texts[2],
                    FooterLeft = texts[3], FooterCenter = texts[4], FooterRight = texts[5],
                }
            }
        };
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
