namespace FactsPDF;

/// <summary>
/// Reads an inline svg element and its content from the document's tokens into an
/// <see cref="SvgGraphic"/>. Supported: g, defs, clipPath (one shape, user-space units), path,
/// rect, circle, ellipse, line, polyline, polygon, title and desc; the paint presentation
/// attributes with SVG inheritance; transform lists; and clip-path references. Every other element
/// fails FPDF1102 and every other attribute or value FPDF1103, so nothing in a supplied graphic is
/// silently dropped.
/// </summary>
internal static class SvgReader
{
    internal const string Namespace = "http://www.w3.org/2000/svg";
    private const string XlinkNamespace = "http://www.w3.org/1999/xlink";

    private enum PaintKind : byte { None, Color, CurrentColor }
    private readonly record struct Paint(PaintKind Kind, Rgb Color);

    // The computed SVG properties an element passes to its children (fill, stroke and their
    // details, color, visibility and clip-rule inherit) plus what applies to its own subtree: the
    // accumulated transform, the clip references and whether it is rendered at all.
    // fill-opacity and stroke-opacity inherit; Opacity is the element's own opacity (not inherited)
    // until its attributes are read, when it becomes a group around the element's subtree.
    private readonly record struct State(Paint Fill, SvgFillRule FillRule, Paint Stroke, double StrokeWidth,
        byte LineCap, byte LineJoin, double MiterLimit, Rgb Color, bool CssColor, bool Visible,
        SvgFillRule ClipRule, bool Render, SvgMatrix Transform, SvgClipReference? Clips,
        double FillOpacity = 1, double StrokeOpacity = 1, double Opacity = 1, SvgOpacityGroup? Group = null);

    private sealed record Frame(string Name, State State, int TokenIndex, int Offset, List<SvgClip>? ClipShapes);

    /// <summary>Attributes the HTML reader accepts on an outer svg element and leaves to this reader.</summary>
    internal static bool RootAttribute(string name) => name is "width" or "height" or "viewbox"
        or "preserveaspectratio" or "xmlns" or "xmlns:xlink" or "version" or "x" or "y"
        || PaintAttribute(name) || Accessibility(name);

    private static bool PaintAttribute(string name) => name is "fill" or "fill-rule" or "fill-opacity" or "stroke"
        or "stroke-width" or "stroke-linecap" or "stroke-linejoin" or "stroke-miterlimit" or "stroke-opacity"
        or "stroke-dasharray" or "stroke-dashoffset" or "opacity" or "color" or "clip-rule" or "visibility"
        or "shape-rendering";

    // Non-rendering accessibility attributes: they change nothing on paper.
    private static bool Accessibility(string name) => name is "role" or "focusable"
        || name.StartsWith("aria-", StringComparison.Ordinal);

    private static bool Shape(string name) => name is "path" or "rect" or "circle" or "ellipse" or "line"
        or "polyline" or "polygon";

    /// <summary>
    /// Reads the svg element whose start tag is <c>tokens[t]</c> through its end tag, leaving
    /// <paramref name="t"/> on the end tag (or on the start tag when it is self-closed).
    /// <paramref name="path"/> already ends with the svg element; <paramref name="depth"/> is the
    /// number of open elements around it.
    /// </summary>
    internal static SvgGraphic Read(List<HtmlToken> tokens, ref int t, SvgDocument document, CssStylesheets sheets,
        List<CssElement> path, int depth, int maxDepth, Rgb color)
    {
        var root = tokens[t];
        var offset = root.Offset;
        document.SvgDepth = path.Count - 1;
        var attributes = root.Attributes!;
        CssLength? widthHint = null;
        double? heightHint = null;
        (double X, double Y, double Width, double Height)? viewBox = null;
        var align = SvgAspectAlign.XMidYMid;
        var slice = false;
        var state = new State(new(PaintKind.Color, new(0, 0, 0)), SvgFillRule.NonZero, new(PaintKind.None, default), 1,
            0, 0, 4, color, false, true, SvgFillRule.NonZero, true, SvgMatrix.Identity, null);
        foreach (var (name, value) in attributes)
        {
            switch (name)
            {
                case "width":
                    if (!IsAuto(value)) widthHint = SizeHint(value, offset, allowPercent: true);
                    break;
                case "height":
                    if (!IsAuto(value)) heightHint = SizeHint(value, offset, allowPercent: false).Value;
                    break;
                case "viewbox":
                    viewBox = ViewBox(value, offset);
                    break;
                case "preserveaspectratio":
                    (align, slice) = AspectRatio(value, offset);
                    break;
                case "xmlns":
                    if (value != Namespace)
                        throw new FactsPdfException("FPDF1103", $"svg xmlns must be '{Namespace}'.", offset);
                    break;
                case "xmlns:xlink":
                    if (value != XlinkNamespace)
                        throw new FactsPdfException("FPDF1103", $"svg xmlns:xlink must be '{XlinkNamespace}'.", offset);
                    break;
                // The outermost svg in HTML is positioned by CSS: x and y have no effect, and the
                // version attribute is not processed.
                case "version" or "x" or "y":
                    break;
                // A presentation attribute is a hint below every author declaration: CSS color on
                // the svg (already in its computed colour) wins over a color attribute.
                case "color" when sheets.SvgContent(path, (root as StyledHtmlToken)?.InlineDeclarations ?? default).Color >= 0:
                    _ = Apply(state, name, value, offset, document, sheets);
                    break;
                default:
                    if (PaintAttribute(name)) state = Apply(state, name, value, offset, document, sheets);
                    break;
            }
        }
        document.Groups.Clear();
        state = Grouped(state, offset, document);
        var shapes = new List<SvgShape>();
        if (root.SelfClosing)
            return document.Add(new SvgGraphic { WidthHint = widthHint, HeightHint = heightHint, ViewBox = viewBox,
                Align = align, Slice = slice, Shapes = [] });
        var frames = new List<Frame> { new("svg", state, t, offset, null) };
        var metadata = 0; // open title/desc elements, whose text is never rendered
        for (t++; t < tokens.Count; t++)
        {
            document.Limits.Cancellation.ThrowIfCancellationRequested();
            var token = tokens[t];
            if (token.Kind == HtmlTokenKind.Text)
            {
                // Text outside a text element is not rendered by SVG; references are still validated.
                _ = HtmlTokens.Decode(token.Value, token.Offset);
                continue;
            }
            if (token.Kind != HtmlTokenKind.Start && token.Kind != HtmlTokenKind.End)
                throw new FactsPdfException("FPDF1102", "SVG element 'style' is not supported.", token.Offset);
            var name = token.Value;
            var top = frames[^1];
            if (token.Kind == HtmlTokenKind.End)
            {
                if (name != top.Name)
                    throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{name}' inside svg.", token.Offset);
                Close(top, document);
                frames.RemoveAt(frames.Count - 1);
                if (name is "title" or "desc") metadata--;
                // The svg element itself stays on the path for the HTML reader to remove.
                else if (frames.Count > 0) path.RemoveAt(path.Count - 1);
                if (frames.Count == 0)
                {
                    foreach (var group in document.Groups) group.Check(document.Limits.Cancellation);
                    return document.Add(new SvgGraphic { WidthHint = widthHint, HeightHint = heightHint, ViewBox = viewBox,
                        Align = align, Slice = slice, Shapes = [.. shapes] });
                }
                continue;
            }
            if (metadata > 0)
                throw new FactsPdfException("FPDF1101", "SVG title and desc may contain only text.", token.Offset);
            if (depth + frames.Count > maxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            if (name is "title" or "desc")
            {
                foreach (var (attribute, _) in token.Attributes!)
                    if (!(attribute is "id" or "class" or "lang" || attribute.StartsWith("data-", StringComparison.Ordinal)))
                        throw new FactsPdfException("FPDF1103", $"SVG attribute '{attribute}' is not supported on '{name}'.", token.Offset);
                if (!token.SelfClosing) { frames.Add(new(name, top.State, t, token.Offset, null)); metadata++; }
                continue;
            }
            if (Shape(top.Name))
                throw new FactsPdfException("FPDF1102", "SVG shapes may contain only title and desc.", token.Offset);
            var inClip = top.ClipShapes is not null;
            if (!(name is "g" or "defs" or "clippath" || Shape(name)) || inClip && !Shape(name))
                throw new FactsPdfException("FPDF1102", name == "svg"
                    ? "Nested svg elements are not supported."
                    : inClip ? $"SVG element '{name}' is not supported inside clipPath; use one basic shape or path."
                    : $"SVG element '{name}' is not supported.", token.Offset);
            path.Add(CssElement.From(token));
            var css = sheets.SvgContent(path, (token as StyledHtmlToken)?.InlineDeclarations ?? default);
            if (css.Display >= 0)
                throw new FactsPdfException("FPDF1201", "CSS display on SVG content elements is not supported.", css.Display);
            if (css.Size >= 0 && name == "rect")
                throw new FactsPdfException("FPDF1201", "CSS width and height on SVG rect elements are not supported; use the attributes.", css.Size);
            var element = Element(top.State, token, name, inClip, document, sheets);
            if (css.Color >= 0) element = element with { CssColor = true };
            List<SvgClip>? clipShapes = null;
            if (name == "clippath")
                clipShapes = [];
            else if (name == "defs")
                element = element with { Render = false };
            if (Shape(name))
            {
                var segments = Geometry(token, name, document.Limits);
                if (inClip)
                    top.ClipShapes!.Add(new([.. segments], element.ClipRule, element.Transform));
                else if (element.Render && element.Visible && segments.Exists(s => s.Kind != SvgSegmentKind.Move))
                {
                    // A line encloses no area, so only its stroke paints. Each paint's alpha is its
                    // colour's alpha times its fill- or stroke-opacity times every enclosing
                    // opacity (exact because each opacity group's paints never overlap, see
                    // SvgOpacityGroup); a paint whose alpha rounds to 0 paints nothing.
                    var groupAlpha = 1d;
                    for (var group = element.Group; group is not null; group = group.Outer) groupAlpha *= group.Opacity;
                    var fill = Translucent(name == "line" ? null : Resolve(element.Fill, element, token.Offset),
                        element.FillOpacity * groupAlpha);
                    var stroke = Translucent(element.StrokeWidth > 0 ? Resolve(element.Stroke, element, token.Offset) : null,
                        element.StrokeOpacity * groupAlpha);
                    if (fill is not null || stroke is not null)
                    {
                        document.Limits.Command(token.Offset);
                        var shape = new SvgShape
                        {
                            Path = [.. segments],
                            Transform = element.Transform,
                            Fill = fill,
                            FillRule = element.FillRule,
                            Stroke = stroke,
                            StrokeWidth = element.StrokeWidth,
                            LineCap = element.LineCap,
                            LineJoin = element.LineJoin,
                            MiterLimit = element.MiterLimit,
                            Clips = element.Clips,
                            SourceOffset = token.Offset,
                        };
                        if (shape.SplitPaint) document.Limits.Command(token.Offset, 1 + shape.Path.Length);
                        shapes.Add(shape);
                        for (var group = element.Group; group is not null; group = group.Outer) group.Add(shape);
                    }
                }
            }
            var frame = new Frame(name, element, t, token.Offset, clipShapes);
            if (token.SelfClosing)
            {
                Close(frame, document);
                path.RemoveAt(path.Count - 1);
            }
            else frames.Add(frame);
        }
        throw new FactsPdfException("FPDF1101", "Unclosed element 'svg'.", offset);
    }

    // A closing clipPath registers its geometry under its start tag's token index.
    private static void Close(Frame frame, SvgDocument document)
    {
        if (frame.ClipShapes is not { } clipShapes) return;
        if (clipShapes.Count > 1)
            throw new FactsPdfException("FPDF1103", "A clipPath with more than one shape is not supported.", frame.Offset);
        // A clipPath whose shape encloses nothing (no shape, a zero size, only movetos) clips
        // everything away.
        document.Clips[frame.TokenIndex] = clipShapes.Count == 0 || !Array.Exists(clipShapes[0].Path, s => s.Kind != SvgSegmentKind.Move)
            ? new SvgClip([], SvgFillRule.NonZero, SvgMatrix.Identity)
            : clipShapes[0] with { Local = frame.State.Transform.Then(clipShapes[0].Local) };
    }

    private static Rgb? Resolve(Paint paint, State state, int offset)
    {
        if (paint.Kind == PaintKind.None) return null;
        if (paint.Kind == PaintKind.Color) return paint.Color;
        if (state.CssColor)
            throw new FactsPdfException("FPDF1201",
                "CSS color on SVG content elements is not supported where currentColor paints; set color on the svg element.", offset);
        return state.Color;
    }

    // The element's computed state from its parent's and its own attributes.
    private static State Element(State parent, HtmlToken token, string name, bool inClip, SvgDocument document, CssStylesheets sheets)
    {
        var offset = token.Offset;
        // Clip references and the clipPath's transform are relative to the element's own user
        // space, so they are applied after the transform; transform does not inherit.
        // opacity is not inherited: each element starts without one of its own.
        var state = parent with { Opacity = 1 };
        if (inClip)
            state = state with { Transform = SvgMatrix.Identity, Clips = null };
        else if (name == "clippath")
            state = state with { Transform = SvgMatrix.Identity, Render = false };
        string? clipPath = null;
        foreach (var (attribute, value) in token.Attributes!)
        {
            if (attribute is "id" or "class" or "style" or "lang" or "xml:space" || attribute.StartsWith("data-", StringComparison.Ordinal)
                || Accessibility(attribute))
                continue;
            if (Shape(name) && GeometryAttribute(name, attribute))
                continue;
            if (name == "clippath" && attribute == "clippathunits")
            {
                if (value.Trim() != "userSpaceOnUse")
                    throw new FactsPdfException("FPDF1103", "clipPath supports only clipPathUnits=\"userSpaceOnUse\".", offset);
                continue;
            }
            if (attribute == "transform")
            {
                if (name == "defs") throw Unsupported(attribute, name, offset);
                var transform = state.Transform.Then(Transform(value, offset));
                SvgLimits.CheckMatrix(transform, offset);
                state = state with { Transform = transform };
                continue;
            }
            if (attribute == "clip-path")
            {
                if (inClip || name == "clippath") throw Unsupported(attribute, name, offset);
                clipPath = value;
                continue;
            }
            if (attribute == "display")
            {
                var keyword = Keyword(value);
                if (inClip) throw Unsupported(attribute, name, offset);
                if (keyword == "none") state = state with { Render = false };
                else if (keyword is not ("inline" or "block" or "inline-block" or "inherit" or "initial" or "unset"))
                    throw new FactsPdfException("FPDF1103", "SVG display supports none, inline and block.", offset);
                continue;
            }
            if (inClip && attribute == "visibility") throw Unsupported(attribute, name, offset);
            if (!PaintAttribute(attribute)) throw Unsupported(attribute, name, offset);
            state = Apply(state, attribute, value, offset, document, sheets);
        }
        if (clipPath is not null && Keyword(clipPath) != "none")
            state = state with { Clips = new(ClipId(clipPath, offset), state.Transform, state.Clips) };
        return inClip ? state with { Opacity = 1 } : Grouped(state, offset, document);
    }

    // An element's opacity below 1 groups its subtree's paints (see SvgOpacityGroup).
    private static State Grouped(State state, int offset, SvgDocument document)
    {
        if (state.Opacity >= 1) return state;
        var group = new SvgOpacityGroup(state.Opacity, offset, state.Group);
        document.Groups.Add(group);
        return state with { Opacity = 1, Group = group };
    }

    // The colour with its alpha multiplied by an opacity; null when nothing would paint.
    private static Rgb? Translucent(Rgb? color, double opacity)
    {
        if (color is not { } paint) return null;
        var alpha = new Rgb(paint.R, paint.G, paint.B, paint.A * opacity);
        return PdfAlphaStates.AlphaByte(alpha) == 0 ? null : alpha;
    }

    private static FactsPdfException Unsupported(string attribute, string element, int offset) =>
        new("FPDF1103", $"SVG attribute '{attribute}' is not supported on '{element}'.", offset);

    private static bool GeometryAttribute(string element, string attribute) => element switch
    {
        "path" => attribute is "d" or "pathlength",
        "rect" => attribute is "x" or "y" or "width" or "height" or "rx" or "ry",
        "circle" => attribute is "cx" or "cy" or "r",
        "ellipse" => attribute is "cx" or "cy" or "rx" or "ry",
        "line" => attribute is "x1" or "y1" or "x2" or "y2",
        _ => attribute == "points",
    };

    // One inherited paint property from a presentation attribute. inherit keeps the parent value,
    // which the state already holds.
    private static State Apply(State state, string attribute, string value, int offset, SvgDocument document, CssStylesheets sheets)
    {
        var keyword = Keyword(value);
        if (keyword == "inherit") return state;
        switch (attribute)
        {
            case "fill":
                return state with { Fill = PaintValue(value, keyword, offset, document, sheets) };
            case "stroke":
                return state with { Stroke = PaintValue(value, keyword, offset, document, sheets) };
            case "fill-rule":
                return state with { FillRule = Rule(keyword, attribute, offset) };
            case "clip-rule":
                return state with { ClipRule = Rule(keyword, attribute, offset) };
            case "stroke-width":
                return state with { StrokeWidth = Length(value, offset, attribute, nonNegative: true) };
            case "stroke-linecap":
                return state with
                {
                    LineCap = keyword switch
                    {
                        "butt" => 0, "round" => 1, "square" => 2,
                        _ => throw new FactsPdfException("FPDF1103", "stroke-linecap must be butt, round or square.", offset)
                    }
                };
            case "stroke-linejoin":
                return state with
                {
                    LineJoin = keyword switch
                    {
                        "miter" => 0, "round" => 1, "bevel" => 2,
                        _ => throw new FactsPdfException("FPDF1103", "stroke-linejoin must be miter, round or bevel.", offset)
                    }
                };
            case "stroke-miterlimit":
            {
                var limit = Number(value, offset, attribute);
                if (limit < 1) throw new FactsPdfException("FPDF1103", "stroke-miterlimit must be at least 1.", offset);
                return state with { MiterLimit = limit };
            }
            case "opacity":
                return state with { Opacity = AlphaValue(value, attribute, offset) };
            case "fill-opacity":
                return state with { FillOpacity = AlphaValue(value, attribute, offset) };
            case "stroke-opacity":
                return state with { StrokeOpacity = AlphaValue(value, attribute, offset) };
            case "stroke-dasharray":
                if (keyword != "none")
                    throw new FactsPdfException("FPDF1103", "Dashed SVG strokes (stroke-dasharray) are not supported.", offset);
                return state;
            case "stroke-dashoffset":
                _ = Length(value, offset, attribute, nonNegative: false);
                return state;
            case "color":
                return keyword == "currentcolor" ? state
                    : state with { Color = document.Color(value, offset, sheets), CssColor = false };
            case "visibility":
                return state with
                {
                    Visible = keyword switch
                    {
                        "visible" => true, "hidden" or "collapse" => false,
                        _ => throw new FactsPdfException("FPDF1103", "visibility must be visible, hidden or collapse.", offset)
                    }
                };
            case "shape-rendering":
                // An anti-aliasing hint; PDF viewers rasterize the vector paths themselves.
                if (keyword is not ("auto" or "optimizespeed" or "crispedges" or "geometricprecision"))
                    throw new FactsPdfException("FPDF1103", "shape-rendering must be auto, optimizeSpeed, crispEdges or geometricPrecision.", offset);
                return state;
            default:
                throw new FactsPdfException("FPDF1103", $"SVG attribute '{attribute}' is not supported.", offset);
        }
    }

    private static Paint PaintValue(string value, string keyword, int offset, SvgDocument document, CssStylesheets sheets)
    {
        if (keyword is "none" or "transparent") return new(PaintKind.None, default);
        if (keyword == "currentcolor") return new(PaintKind.CurrentColor, default);
        if (keyword.StartsWith("url(", StringComparison.Ordinal))
            throw new FactsPdfException("FPDF1103", "SVG paint servers (url() gradients and patterns) are not supported.", offset);
        return new(PaintKind.Color, document.Color(value, offset, sheets));
    }

    private static SvgFillRule Rule(string keyword, string attribute, int offset) => keyword switch
    {
        "nonzero" => SvgFillRule.NonZero,
        "evenodd" => SvgFillRule.EvenOdd,
        _ => throw new FactsPdfException("FPDF1103", $"{attribute} must be nonzero or evenodd.", offset)
    };

    // An alpha value: a number or a percentage, clamped to 0–1 as CSS clamps it.
    private static double AlphaValue(string value, string attribute, int offset)
    {
        var text = value.Trim();
        var percent = text.EndsWith('%');
        if (percent) text = text[..^1];
        var position = 0;
        var number = Scan(text, ref position, offset, attribute);
        if (position != text.Length)
            throw new FactsPdfException("FPDF1103", $"SVG {attribute} must be a number or a percentage.", offset);
        return Math.Clamp(percent ? number / 100 : number, 0, 1);
    }

    private static string ClipId(string value, int offset)
    {
        var text = value.AsSpan().Trim();
        if (text.StartsWith("url(", StringComparison.Ordinal) && text.EndsWith(")"))
        {
            var inner = text[4..^1].Trim();
            if (inner.Length >= 2 && inner[0] is '"' or '\'' && inner[^1] == inner[0]) inner = inner[1..^1];
            if (inner.Length > 1 && inner[0] == '#')
                return inner[1..].ToString();
        }
        throw new FactsPdfException("FPDF1103", "clip-path must be none or url(#id) naming an element of this document.", offset);
    }

    private static string Keyword(string value) => value.Trim().ToLowerInvariant();

    private static bool IsAuto(string value) => Keyword(value) == "auto";

    // An svg width or height attribute: a nonnegative number of CSS px (with or without px),
    // converted to points, or a width percentage of the containing block.
    private static CssLength SizeHint(string value, int offset, bool allowPercent)
    {
        var text = value.Trim();
        if (text.EndsWith('%'))
        {
            if (!allowPercent)
                throw new FactsPdfException("FPDF1103", "A percentage svg height is not supported.", offset);
            var position = 0;
            var percent = Scan(text.AsSpan(0, text.Length - 1), ref position, offset, "width");
            if (position != text.Length - 1 || percent < 0)
                throw new FactsPdfException("FPDF1103", "svg width must be a nonnegative length or percentage.", offset);
            return new(percent, IsPercent: true);
        }
        return new(Length(text, offset, "svg size", nonNegative: true) * 0.75);
    }

    // A user-space length: a number, optionally in px (one user unit is one CSS px).
    private static double Length(string value, int offset, string attribute, bool nonNegative)
    {
        var text = value.AsSpan().Trim();
        if (text.EndsWith("px", StringComparison.Ordinal)) text = text[..^2];
        var position = 0;
        var number = Scan(text, ref position, offset, attribute);
        if (position != text.Length)
            throw new FactsPdfException("FPDF1103", $"SVG {attribute} must be a number of user units or px; other units and percentages are not supported.", offset);
        if (nonNegative && number < 0)
            throw new FactsPdfException("FPDF1103", $"SVG {attribute} must not be negative.", offset);
        return number;
    }

    private static double Number(string value, int offset, string attribute)
    {
        var text = value.AsSpan().Trim();
        var position = 0;
        var number = Scan(text, ref position, offset, attribute);
        if (position != text.Length) throw new FactsPdfException("FPDF1103", $"SVG {attribute} must be a number.", offset);
        return number;
    }

    private static double Scan(ReadOnlySpan<char> text, ref int position, int offset, string attribute)
    {
        if (position >= text.Length || !(char.IsAsciiDigit(text[position]) || text[position] is '+' or '-' or '.'))
            throw new FactsPdfException("FPDF1103", $"SVG {attribute} must be a number.", offset);
        return SvgPathData.Scan(text, ref position, offset);
    }

    // A whitespace- and/or comma-separated list of numbers, as in viewBox and points.
    private static List<double> Numbers(string value, int offset, string attribute)
    {
        var text = value.AsSpan();
        var numbers = new List<double>();
        var position = 0;
        SvgPathData.SkipSpace(text, ref position);
        while (position < text.Length)
        {
            numbers.Add(Scan(text, ref position, offset, attribute));
            SvgPathData.SkipSpace(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++;
                SvgPathData.SkipSpace(text, ref position);
                if (position == text.Length) throw new FactsPdfException("FPDF1103", $"SVG {attribute} has a trailing comma.", offset);
            }
        }
        return numbers;
    }

    private static (double, double, double, double) ViewBox(string value, int offset)
    {
        var numbers = Numbers(value, offset, "viewBox");
        if (numbers.Count != 4 || numbers[2] <= 0 || numbers[3] <= 0)
            throw new FactsPdfException("FPDF1103", "svg viewBox needs four numbers with a positive width and height.", offset);
        return (numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static (SvgAspectAlign, bool) AspectRatio(string value, int offset)
    {
        var parts = value.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries);
        var align = parts.Length is 1 or 2 ? parts[0] switch
        {
            "none" => SvgAspectAlign.None,
            "xMinYMin" => SvgAspectAlign.XMinYMin, "xMidYMin" => SvgAspectAlign.XMidYMin, "xMaxYMin" => SvgAspectAlign.XMaxYMin,
            "xMinYMid" => SvgAspectAlign.XMinYMid, "xMidYMid" => SvgAspectAlign.XMidYMid, "xMaxYMid" => SvgAspectAlign.XMaxYMid,
            "xMinYMax" => SvgAspectAlign.XMinYMax, "xMidYMax" => SvgAspectAlign.XMidYMax, "xMaxYMax" => SvgAspectAlign.XMaxYMax,
            _ => (SvgAspectAlign?)null,
        } : null;
        if (align is null || parts.Length == 2 && parts[1] is not ("meet" or "slice"))
            throw new FactsPdfException("FPDF1103", "preserveAspectRatio must be none or an xMin/xMid/xMax-YMin/YMid/YMax alignment with optional meet or slice.", offset);
        return (align.Value, parts.Length == 2 && parts[1] == "slice");
    }

    /// <summary>A transform list: matrix, translate, scale, rotate, skewX and skewY, applied
    /// left to right as nested coordinate systems.</summary>
    internal static SvgMatrix Transform(string value, int offset)
    {
        var text = value.AsSpan();
        var result = SvgMatrix.Identity;
        var position = 0;
        Span<double> arguments = stackalloc double[6];
        SvgPathData.SkipSpace(text, ref position);
        if (text[position..].SequenceEqual("none")) return result;
        while (position < text.Length)
        {
            var start = position;
            while (position < text.Length && char.IsAsciiLetter(text[position])) position++;
            var function = text[start..position].ToString();
            SvgPathData.SkipSpace(text, ref position);
            if (position >= text.Length || text[position] != '(') throw InvalidTransform(offset);
            position++;
            var count = 0;
            while (true)
            {
                SvgPathData.SkipSpace(text, ref position);
                if (position < text.Length && text[position] == ')') { position++; break; }
                if (count > 0 && position < text.Length && text[position] == ',')
                {
                    position++;
                    SvgPathData.SkipSpace(text, ref position);
                }
                if (count == 6) throw InvalidTransform(offset);
                arguments[count++] = Scan(text, ref position, offset, "transform");
            }
            var a = arguments;
            var step = (function, count) switch
            {
                ("matrix", 6) => new SvgMatrix(a[0], a[1], a[2], a[3], a[4], a[5]),
                ("translate", 1) => new SvgMatrix(1, 0, 0, 1, a[0], 0),
                ("translate", 2) => new SvgMatrix(1, 0, 0, 1, a[0], a[1]),
                ("scale", 1) => new SvgMatrix(a[0], 0, 0, a[0], 0, 0),
                ("scale", 2) => new SvgMatrix(a[0], 0, 0, a[1], 0, 0),
                ("rotate", 1) => Rotation(a[0]),
                ("rotate", 3) => new SvgMatrix(1, 0, 0, 1, a[1], a[2]).Then(Rotation(a[0])).Then(new SvgMatrix(1, 0, 0, 1, -a[1], -a[2])),
                ("skewX", 1) => new SvgMatrix(1, 0, Math.Tan(a[0] * Math.PI / 180), 1, 0, 0),
                ("skewY", 1) => new SvgMatrix(1, Math.Tan(a[0] * Math.PI / 180), 0, 1, 0, 0),
                _ => throw InvalidTransform(offset),
            };
            result = result.Then(step);
            SvgLimits.CheckMatrix(result, offset);
            SvgPathData.SkipSpace(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++;
                SvgPathData.SkipSpace(text, ref position);
                if (position == text.Length) throw InvalidTransform(offset);
            }
        }
        return result;
    }

    private static SvgMatrix Rotation(double degrees)
    {
        var radians = degrees % 360 * Math.PI / 180;
        var (sin, cos) = Math.SinCos(radians);
        return new(cos, sin, -sin, cos, 0, 0);
    }

    private static FactsPdfException InvalidTransform(int offset) => new("FPDF1103",
        "SVG transform must be a list of matrix(6), translate(1-2), scale(1-2), rotate(1 or 3), skewX(1) or skewY(1).", offset);

    // A basic shape's outline as path segments, per the SVG 2 equivalent paths. A shape with a
    // zero size renders nothing; a negative size is an error.
    private static List<SvgSegment> Geometry(HtmlToken token, string name, SvgLimits limits)
    {
        var offset = token.Offset;
        var attributes = token.Attributes!;
        var segments = new List<SvgSegment>();
        double Get(string attribute, bool nonNegative = false) =>
            attributes.TryGetValue(attribute, out var value) ? Length(value, offset, attribute, nonNegative) : 0;
        double? Optional(string attribute)
        {
            if (!attributes.TryGetValue(attribute, out var value) || IsAuto(value)) return null;
            return Length(value, offset, attribute, nonNegative: true);
        }
        void Add(SvgSegment segment)
        {
            limits.Command(offset);
            segments.Add(segment);
        }
        switch (name)
        {
            case "path":
                if (attributes.TryGetValue("pathlength", out var pathLength) && Number(pathLength, offset, "pathLength") < 0)
                    throw new FactsPdfException("FPDF1103", "SVG pathLength must not be negative.", offset);
                if (attributes.TryGetValue("d", out var data)) SvgPathData.Parse(data, offset, limits, segments);
                break;
            case "rect":
            {
                var x = Get("x"); var y = Get("y");
                var width = Get("width", true); var height = Get("height", true);
                if (width == 0 || height == 0) break;
                var rx = Optional("rx"); var ry = Optional("ry");
                var radiusX = Math.Min(rx ?? ry ?? 0, width / 2);
                var radiusY = Math.Min(ry ?? rx ?? 0, height / 2);
                if (radiusX == 0 || radiusY == 0)
                {
                    Add(SvgSegment.Move(x, y)); Add(SvgSegment.Line(x + width, y));
                    Add(SvgSegment.Line(x + width, y + height)); Add(SvgSegment.Line(x, y + height));
                    Add(SvgSegment.Close);
                    break;
                }
                Add(SvgSegment.Move(x + radiusX, y));
                Add(SvgSegment.Line(x + width - radiusX, y));
                SvgPathData.Arc(segments, limits, offset, x + width - radiusX, y, radiusX, radiusY, 0, false, true, x + width, y + radiusY);
                Add(SvgSegment.Line(x + width, y + height - radiusY));
                SvgPathData.Arc(segments, limits, offset, x + width, y + height - radiusY, radiusX, radiusY, 0, false, true, x + width - radiusX, y + height);
                Add(SvgSegment.Line(x + radiusX, y + height));
                SvgPathData.Arc(segments, limits, offset, x + radiusX, y + height, radiusX, radiusY, 0, false, true, x, y + height - radiusY);
                Add(SvgSegment.Line(x, y + radiusY));
                SvgPathData.Arc(segments, limits, offset, x, y + radiusY, radiusX, radiusY, 0, false, true, x + radiusX, y);
                Add(SvgSegment.Close);
                break;
            }
            case "circle":
            {
                var r = Get("r", true);
                Ellipse(Get("cx"), Get("cy"), r, r);
                break;
            }
            case "ellipse":
            {
                var rx = Optional("rx"); var ry = Optional("ry");
                Ellipse(Get("cx"), Get("cy"), rx ?? ry ?? 0, ry ?? rx ?? 0);
                break;
            }
            case "line":
                Add(SvgSegment.Move(Get("x1"), Get("y1")));
                Add(SvgSegment.Line(Get("x2"), Get("y2")));
                break;
            default:
            {
                if (!attributes.TryGetValue("points", out var points)) break;
                var numbers = Numbers(points, offset, "points");
                if (numbers.Count % 2 != 0)
                    throw new FactsPdfException("FPDF1103", "SVG points needs an even number of coordinates.", offset);
                for (var i = 0; i < numbers.Count; i += 2)
                    Add(i == 0 ? SvgSegment.Move(numbers[i], numbers[i + 1]) : SvgSegment.Line(numbers[i], numbers[i + 1]));
                if (name == "polygon" && numbers.Count > 0) Add(SvgSegment.Close);
                break;
            }
        }
        return segments;

        void Ellipse(double cx, double cy, double rx, double ry)
        {
            if (rx == 0 || ry == 0) return;
            Add(SvgSegment.Move(cx + rx, cy));
            SvgPathData.Arc(segments, limits, offset, cx + rx, cy, rx, ry, 0, false, true, cx, cy + ry);
            SvgPathData.Arc(segments, limits, offset, cx, cy + ry, rx, ry, 0, false, true, cx - rx, cy);
            SvgPathData.Arc(segments, limits, offset, cx - rx, cy, rx, ry, 0, false, true, cx, cy - ry);
            SvgPathData.Arc(segments, limits, offset, cx, cy - ry, rx, ry, 0, false, true, cx + rx, cy);
            Add(SvgSegment.Close);
        }
    }
}

/// <summary>
/// Every inline svg of one conversion: the shared display-command budget, the clipPaths read so
/// far (keyed by the token index of their start tag) and a cache of the paint colours already
/// compiled. <see cref="Resolve"/> binds clip-path references once the whole document is read,
/// because a reference may name a clipPath later in the page or in another svg.
/// </summary>
internal sealed class SvgDocument(int maxCommands, CancellationToken cancellation)
{
    private readonly List<SvgGraphic> graphics = [];
    private readonly Dictionary<string, Rgb> colors = new(StringComparer.Ordinal);
    internal SvgLimits Limits { get; } = new(maxCommands, cancellation);
    internal Dictionary<int, SvgClip> Clips { get; } = [];
    // The opacity groups of the svg being read, checked when it closes.
    internal List<SvgOpacityGroup> Groups { get; } = [];

    internal SvgGraphic Add(SvgGraphic graphic)
    {
        graphics.Add(graphic);
        return graphic;
    }

    // The depth of the svg element being read on the CSS element path, where var() in its paint
    // attributes takes its custom properties from.
    internal int SvgDepth { get; set; }

    internal Rgb Color(string value, int offset, CssStylesheets sheets)
    {
        // A var() value depends on the svg's custom properties, so only literal colours are cached.
        var literal = !value.Contains("var(", StringComparison.OrdinalIgnoreCase);
        if (literal && colors.TryGetValue(value, out var color)) return color;
        color = sheets.SvgColor(value, offset, SvgDepth);
        if (literal) colors.Add(value, color);
        return color;
    }

    /// <summary>
    /// Binds every clip-path reference as a browser does: url(#id) names the first element in the
    /// document with that id. When that element is a clipPath, its shape clips; when no element has
    /// the id, or it is not a clipPath, the reference is ignored and nothing is clipped (CSS
    /// Masking, clip-path). Then fixes each graphic's display command count.
    /// </summary>
    internal void Resolve(List<HtmlToken> tokens)
    {
        Dictionary<string, int>? first = null;
        foreach (var graphic in graphics)
        {
            var commands = 0L;
            foreach (var shape in graphic.Shapes)
            {
                Limits.Cancellation.ThrowIfCancellationRequested();
                if (shape.Clips is not null)
                {
                    first ??= FirstIds(tokens);
                    var resolved = new List<(SvgClip, SvgMatrix)>();
                    for (var reference = shape.Clips; reference is not null; reference = reference.Outer)
                    {
                        if (!first.TryGetValue(reference.Id, out var index) || !Clips.TryGetValue(index, out var clip)) continue;
                        if (clip.Path.Length == 0) shape.Visible = false;
                        Limits.Command(shape.SourceOffset, clip.Path.Length);
                        resolved.Add((clip, reference.Space.Then(clip.Local)));
                    }
                    shape.ResolvedClips = [.. resolved];
                }
                if (!shape.Visible) continue;
                var shapeCommands = 1L + shape.Path.Length;
                foreach (var (clip, _) in shape.ResolvedClips) shapeCommands += clip.Path.Length;
                // A fill and a stroke of different alpha are painted as two shapes.
                commands += shape.SplitPaint ? 2 * shapeCommands : shapeCommands;
            }
            graphic.CommandCount = (int)Math.Min(int.MaxValue, Math.Max(1, commands));
        }
    }

    private Dictionary<string, int> FirstIds(List<HtmlToken> tokens)
    {
        var first = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Count; i++)
        {
            Limits.Cancellation.ThrowIfCancellationRequested();
            if (tokens[i].Kind == HtmlTokenKind.Start && tokens[i].Attributes!.TryGetValue("id", out var id))
                first.TryAdd(id, i);
        }
        return first;
    }
}
