namespace FactsPDF;

/// <summary>
/// The form controls FactsPDF prints the way Chrome prints them: <c>button</c>, the push-button
/// inputs (submit, reset, button), the text-field inputs (text, email, url, tel, password and an
/// unknown type) and <c>input type=hidden</c>. A control is an atomic inline vector box (its rounded
/// background and border) with its label, value or placeholder as ordinary text on top.
/// </summary>
/// <remarks>
/// Geometry measured against headless Chrome (all in CSS px, the control's border box at the origin):
/// <list type="bullet">
/// <item>A push button is the user-agent 2px border and <c>padding:1px 6px</c>; its text is centred and its
/// width is 16 plus the text advance. Painted as a rounded rectangle path inset 0.5px with radius 2: filled
/// #efefef, then stroked 1px #767676.</item>
/// <item>A text field is the 2px border and <c>padding:1px 2px</c> around a content box
/// <c>size * avg + (max - avg)</c> wide (size 20 by default), where max is the font's bounding box width and
/// avg its average character width; painted the same way with a white fill and a #767676 stroke at 0.75
/// alpha. Text starts 4px in.</item>
/// <item>Both are 6px taller than one line of <c>line-height:normal</c>, and the text baseline lies 3px below
/// the top plus the line's ascent.</item>
/// <item>A placeholder is #757575; a password shows one U+2022 bullet per character.</item>
/// </list>
/// Everything else (other input types, author sizing, padding, borders or backgrounds, disabled and
/// readonly states) fails explicitly rather than printing something Chrome would not.
/// </remarks>
internal static class FormControls
{
    internal static bool Tag(string name) => name is "input" or "button";

    private const double PixelsPerPoint = 4d / 3;
    private const double PointsPerPixel = 0.75;
    // The user-agent border and padding of a control, in CSS px.
    private const double Border = 2, PushPaddingX = 6, TextPaddingX = 2, PaddingY = 1;
    private const int DefaultSize = 20;
    private const int MaxSize = 512;
    // Chrome's average character width for the DejaVu faces, measured as 0.6 em for DejaVu Sans where the font's
    // OS/2 xAvgCharWidth is 0.5068 em.
    private const double AverageCharacterWidthScale = 1.1838;

    /// <summary>The attributes a button or input accepts without changing what is printed.</summary>
    internal static bool Attribute(string element, string attribute)
    {
        if (attribute is "name" or "form" or "autofocus" or "formaction" or "formenctype" or "formmethod"
            or "formnovalidate" or "formtarget" or "popovertarget" or "popovertargetaction" or "value")
            return true;
        return element == "input" && attribute is ("type" or "placeholder" or "size" or "required" or "autocomplete"
            or "maxlength" or "minlength" or "pattern" or "list" or "spellcheck" or "inputmode" or "autocapitalize")
            || element == "button" && attribute == "type";
    }

    /// <summary>Builds the control's box, or null for <c>input type=hidden</c>, which has none.</summary>
    internal static ImageNode? Build(string name, HtmlAttributes attributes, string? label, TextStyle style, BoxStyle box,
        int offset, PdfOptions options, SvgDocument svg, Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved)
    {
        var type = attributes.GetValueOrDefault("type")?.ToLowerInvariant();
        bool push;
        if (name == "button")
        {
            push = type is null or "" or "submit" or "reset" or "button"
                ? true : throw new FactsPdfException("FPDF1103", "button type must be submit, reset or button.", offset);
        }
        else
        {
            switch (type)
            {
                case "hidden": return null;
                case "submit": case "reset": case "button": push = true; break;
                case null: case "": case "text": case "email": case "url": case "tel": case "password": push = false; break;
                default:
                    throw new FactsPdfException("FPDF1103",
                        $"input type '{type}' is not supported yet; supported types are text, email, url, tel, password, submit, reset, button and hidden.", offset);
            }
        }
        var unstyled = BoxStyle.Initial with { InlineAlign = box.InlineAlign, MarkerKind = box.MarkerKind,
            MarkerInside = box.MarkerInside, BorderBoxSizing = box.BorderBoxSizing };
        if (box != unstyled)
            throw new FactsPdfException("FPDF1201",
                "Author sizing, spacing, borders, backgrounds and display on a form control replace its native look, which is not supported yet; only vertical-align is.", offset);
        var fonts = FontRoles.Chain(options, FontRoles.Role(style), offset);
        if (fonts.Count == 0)
            throw new FactsPdfException("FPDF1301", "A form control prints in a supplied font; supply an explicit font chain.", offset);
        if (!style.HasNormalLineHeight || style.AbsoluteLineHeight is not null)
            throw new FactsPdfException("FPDF1201", "line-height on a form control is not supported yet.", offset);
        var font = fonts[0];
        var fontPixels = style.FontSize * PixelsPerPoint;
        var ascent = Math.Round(font.Ascent1000 / 1000 * fontPixels, MidpointRounding.AwayFromZero);
        var descent = Math.Round(-font.Descent1000 / 1000 * fontPixels, MidpointRounding.AwayFromZero);
        var gap = Math.Round(font.LineGap1000 / 1000 * fontPixels, MidpointRounding.AwayFromZero);
        var lineHeight = ascent + descent + gap;
        var baselineFromContent = ascent + Math.Floor(gap / 2);

        string text;
        var textStyle = style;
        if (push)
        {
            text = name == "button" ? label! : attributes.GetValueOrDefault("value") ?? type switch
            {
                "reset" => "Reset", "button" => "", _ => "Submit"
            };
            if (text.Length == 0)
                throw new FactsPdfException("FPDF1103", "A button without a label is not supported yet.", offset);
        }
        else
        {
            var value = attributes.GetValueOrDefault("value") ?? "";
            if (value.AsSpan().IndexOfAny('\r', '\n', '\t') >= 0)
                throw new FactsPdfException("FPDF1103", "A text field value cannot contain line breaks or tabs.", offset);
            if (type == "password") value = new string('•', value.EnumerateRunes().Count());
            var placeholder = value.Length == 0 && attributes.GetValueOrDefault("placeholder") is { Length: > 0 } hint ? hint : null;
            if (placeholder is not null)
            {
                if (placeholder.AsSpan().IndexOfAny('\r', '\n', '\t') >= 0)
                    throw new FactsPdfException("FPDF1103", "A placeholder cannot contain line breaks or tabs.", offset);
                text = placeholder;
                textStyle = style with { Color = new Rgb(117 / 255d, 117 / 255d, 117 / 255d) };
            }
            else text = value;
        }

        var glyphs = TextLines.ShapeText(text, textStyle, offset, options, resolved);
        var textWidth = 0d;
        foreach (var glyph in glyphs) textWidth += glyph.Width;
        var textPixels = textWidth * PixelsPerPoint;
        double boxWidth, textX;
        var boxHeight = Border * 2 + PaddingY * 2 + lineHeight;
        if (push)
        {
            boxWidth = Math.Round(Border * 2 + PushPaddingX * 2 + textPixels, MidpointRounding.AwayFromZero);
            textX = (boxWidth - textPixels) / 2;
        }
        else
        {
            var size = DefaultSize;
            if (attributes.GetValueOrDefault("size") is { } sizeValue)
            {
                // The size attribute is a positive integer; an invalid or zero value falls back to 20.
                if (sizeValue.Length is > 0 and <= 4 && sizeValue.All(char.IsAsciiDigit))
                {
                    var parsed = int.Parse(sizeValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (parsed > 0) size = parsed;
                }
                if (size > MaxSize)
                    throw new FactsPdfException("FPDF1103", $"A text field size above {MaxSize} is not supported.", offset);
            }
            var average = AverageCharacterWidth(font, fontPixels);
            var maximum = Math.Round((font.XMax1000 - font.XMin1000) / 1000 * fontPixels, MidpointRounding.AwayFromZero);
            var content = Math.Ceiling(size * average + (maximum - average));
            boxWidth = Border * 2 + TextPaddingX * 2 + content;
            textX = Border + TextPaddingX;
            if (textPixels > content + 0.001)
                throw new FactsPdfException("FPDF1302",
                    "A text field value wider than the field is clipped and scrolled by a browser, which is not supported; widen it with size.", offset);
        }

        var fill = push ? new Rgb(239 / 255d, 239 / 255d, 239 / 255d) : new Rgb(1, 1, 1);
        var stroke = push ? new Rgb(118 / 255d, 118 / 255d, 118 / 255d) : new Rgb(118 / 255d, 118 / 255d, 118 / 255d, 0.75);
        var path = RoundedRectangle(0.5, 0.5, boxWidth - 1, boxHeight - 1, 2);
        var shapes = new[]
        {
            new SvgShape { Path = path, Transform = SvgMatrix.Identity, Fill = fill, SourceOffset = offset },
            new SvgShape { Path = path, Transform = SvgMatrix.Identity, Stroke = stroke, StrokeWidth = 1, MiterLimit = 4, SourceOffset = offset },
        };
        var graphic = svg.Add(new SvgGraphic
        {
            ViewBox = (0, 0, boxWidth, boxHeight), Shapes = shapes, CommandCount = shapes.Length * (path.Length + 1)
        });
        var texts = text.Length == 0 ? null : new[]
        {
            new ControlText(glyphs, textWidth, textX * PointsPerPixel, (Border + PaddingY + baselineFromContent) * PointsPerPixel)
        };
        var sized = BoxStyle.Initial with
        {
            Width = new CssLength(boxWidth * PointsPerPixel), Height = new CssLength(boxHeight * PointsPerPixel),
            InlineAlign = box.InlineAlign
        };
        return new ImageNode(null, style, sized, offset, null, graphic, texts,
            (Border + PaddingY + baselineFromContent) * PointsPerPixel);
    }

    // Blink's text-control character width: a multiple of the font's average character width, which for
    // the supplied TrueType font is derived from OS/2 xAvgCharWidth.
    private static double AverageCharacterWidth(PdfFont font, double fontPixels)
    {
        return font.AverageCharWidth1000 > 0
            ? font.AverageCharWidth1000 / 1000 * AverageCharacterWidthScale * fontPixels : 0.6 * fontPixels;
    }

    // A rounded rectangle as four lines and four corner curves, clockwise from the left edge's top end.
    private static SvgSegment[] RoundedRectangle(double x, double y, double width, double height, double radius)
    {
        const double Kappa = 0.5522847498;
        var k = radius * Kappa;
        double right = x + width, bottom = y + height;
        return
        [
            SvgSegment.Move(x, y + radius),
            SvgSegment.Cubic(x, y + radius - k, x + radius - k, y, x + radius, y),
            SvgSegment.Line(right - radius, y),
            SvgSegment.Cubic(right - radius + k, y, right, y + radius - k, right, y + radius),
            SvgSegment.Line(right, bottom - radius),
            SvgSegment.Cubic(right, bottom - radius + k, right - radius + k, bottom, right - radius, bottom),
            SvgSegment.Line(x + radius, bottom),
            SvgSegment.Cubic(x + radius - k, bottom, x, bottom - radius + k, x, bottom - radius),
            SvgSegment.Close,
        ];
    }
}
