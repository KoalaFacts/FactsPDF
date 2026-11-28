using System.Collections.Frozen;

namespace FactsPDF;

/// <summary>
/// Which attributes a page may carry that change nothing when it is printed. Every element accepts attributes of
/// any name (the HTML parser keeps them all, and attribute selectors can match them), but an attribute that
/// HTML or SVG defines may change the rendering of some element, so only those verified to change no printed
/// output on any element (checked by printing each on a paragraph, link, division and list in Chrome 158)
/// are accepted by name; every other defined attribute fails unless the reader models it.
/// </summary>
internal static class HtmlAttributeNames
{
    /// <summary>Whether the attribute is accepted and ignored: a custom name, or a global attribute with no printed effect.</summary>
    internal static bool Ignored(string name) => NoPrintedEffect(name) || !Defined.Contains(name);

    /// <summary>Global attributes that only affect interaction, editing, accessibility, microdata or scripting.</summary>
    internal static bool NoPrintedEffect(string name)
        => name.StartsWith("aria-", StringComparison.Ordinal) || name.StartsWith("data-", StringComparison.Ordinal)
            || name.Length > 2 && name.StartsWith("on", StringComparison.Ordinal) || Inert.Contains(name);

    private static readonly FrozenSet<string> Inert = new[]
    {
        "role", "tabindex", "accesskey", "autofocus", "contenteditable", "draggable", "spellcheck", "translate", "inert",
        "enterkeyhint", "inputmode", "autocapitalize", "autocorrect", "nonce", "slot", "is", "part", "exportparts",
        "itemscope", "itemtype", "itemprop", "itemid", "itemref", "writingsuggestions", "virtualkeyboardpolicy",
        "elementtiming", "focusgroup",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Every attribute name HTML (including its obsolete presentational attributes) and SVG define. A name outside
    // this set is a custom attribute and can change nothing; a name in it may.
    private static readonly FrozenSet<string> Defined = new[]
    {
        // HTML
        "abbr", "accept", "accept-charset", "accesskey", "action", "allow", "allowfullscreen", "alt", "as", "async",
        "autocapitalize", "autocomplete", "autofocus", "autoplay", "blocking", "charset", "checked", "cite", "class",
        "color", "cols", "colspan", "content", "contenteditable", "controls", "coords", "crossorigin", "data", "datetime",
        "decoding", "default", "defer", "dir", "dirname", "disabled", "download", "draggable", "enctype", "enterkeyhint",
        "fetchpriority", "for", "form", "formaction", "formenctype", "formmethod", "formnovalidate", "formtarget",
        "headers", "height", "hidden", "high", "href", "hreflang", "http-equiv", "id", "imagesizes", "imagesrcset",
        "inert", "inputmode", "integrity", "is", "ismap", "itemid", "itemprop", "itemref", "itemscope", "itemtype",
        "kind", "label", "lang", "list", "loading", "loop", "low", "max", "maxlength", "media", "method", "min",
        "minlength", "multiple", "muted", "name", "nomodule", "nonce", "novalidate", "open", "optimum", "pattern", "ping",
        "placeholder", "playsinline", "popover", "popovertarget", "popovertargetaction", "poster", "preload", "readonly",
        "referrerpolicy", "rel", "required", "reversed", "rows", "rowspan", "sandbox", "scope", "selected",
        "shadowrootclonable", "shadowrootdelegatesfocus", "shadowrootmode", "shadowrootserializable", "shape", "size",
        "sizes", "slot", "span", "spellcheck", "src", "srcdoc", "srclang", "srcset", "start", "step", "style", "tabindex",
        "target", "title", "translate", "type", "usemap", "value", "width", "wrap", "writingsuggestions",
        // HTML, obsolete presentational and legacy
        "align", "alink", "archive", "axis", "background", "bgcolor", "border", "bordercolor", "bottommargin", "cellpadding",
        "cellspacing", "char", "charoff", "classid", "clear", "code", "codebase", "codetype", "compact", "declare", "direction",
        "event", "face", "frame", "frameborder", "hspace", "language", "leftmargin", "link", "longdesc", "lowsrc", "marginheight",
        "marginwidth", "nohref", "noresize", "noshade", "nowrap", "object", "profile", "prompt", "rev", "rightmargin", "rules",
        "scheme", "scrolling", "standby", "summary", "text", "topmargin", "valign", "valuetype", "version", "vlink", "vspace",
        "contextmenu", "manifest", "xmlns",
        // SVG (presentation attributes, geometry, paint servers, filters, animation, xlink and xml)
        "accent-height", "accumulate", "additive", "alignment-baseline", "alphabetic", "amplitude", "arabic-form", "ascent",
        "azimuth", "basefrequency", "baseline-shift", "baseprofile", "bbox", "begin", "bias", "by", "calcmode", "cap-height",
        "clip", "clip-path", "clip-rule", "clippathunits", "color-interpolation", "color-interpolation-filters", "color-profile",
        "color-rendering", "contentscripttype", "contentstyletype", "cursor", "cx", "cy", "d", "decelerate", "descent",
        "diffuseconstant", "display", "divisor", "dominant-baseline", "dur", "dx", "dy", "edgemode", "elevation",
        "enable-background", "end", "exponent", "externalresourcesrequired", "fill", "fill-opacity", "fill-rule", "filter",
        "filterres", "filterunits", "flood-color", "flood-opacity", "font-family", "font-size", "font-size-adjust",
        "font-stretch", "font-style", "font-variant", "font-weight", "format", "fr", "from", "fx", "fy", "g1", "g2",
        "glyph-name", "glyph-orientation-horizontal", "glyph-orientation-vertical", "glyphref", "gradienttransform",
        "gradientunits", "hanging", "horiz-adv-x", "horiz-origin-x", "ideographic", "image-rendering", "in", "in2", "intercept",
        "k", "k1", "k2", "k3", "k4", "kernelmatrix", "kernelunitlength", "kerning", "keypoints", "keysplines", "keytimes",
        "lengthadjust", "letter-spacing", "lighting-color", "limitingconeangle", "local", "marker-end", "marker-mid",
        "marker-start", "markerheight", "markerunits", "markerwidth", "mask", "maskcontentunits", "maskunits", "mathematical",
        "mode", "numoctaves", "offset", "opacity", "operator", "order", "orient", "orientation", "origin", "overflow",
        "overline-position", "overline-thickness", "paint-order", "panose-1", "path", "pathlength", "patterncontentunits",
        "patterntransform", "patternunits", "pointer-events", "points", "pointsatx", "pointsaty", "pointsatz", "preservealpha",
        "preserveaspectratio", "primitiveunits", "r", "radius", "refx", "refy", "rendering-intent", "repeatcount", "repeatdur",
        "requiredextensions", "requiredfeatures", "restart", "result", "rotate", "rx", "ry", "scale", "seed", "shape-rendering",
        "slope", "spacing", "specularconstant", "specularexponent", "speed", "spreadmethod", "startoffset", "stddeviation",
        "stemh", "stemv", "stitchtiles", "stop-color", "stop-opacity", "strikethrough-position", "strikethrough-thickness",
        "string", "stroke", "stroke-dasharray", "stroke-dashoffset", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
        "stroke-opacity", "stroke-width", "surfacescale", "systemlanguage", "tablevalues", "targetx", "targety", "text-anchor",
        "text-decoration", "text-rendering", "textlength", "to", "transform", "transform-origin", "u1", "u2",
        "underline-position", "underline-thickness", "unicode", "unicode-bidi", "unicode-range", "units-per-em",
        "v-alphabetic", "v-hanging", "v-ideographic", "v-mathematical", "values", "vector-effect", "vert-adv-y",
        "vert-origin-x", "vert-origin-y", "viewbox", "viewtarget", "visibility", "widths", "word-spacing", "writing-mode",
        "x", "x-height", "x1", "x2", "xchannelselector", "xlink:href", "xlink:title", "xlink:type", "xlink:role",
        "xlink:arcrole", "xlink:show", "xlink:actuate", "xml:lang", "xml:space", "xml:base", "xmlns:xlink", "y", "y1", "y2",
        "ychannelselector", "z", "zoomandpan", "focusable", "crossorigin", "tabindex", "lang", "direction",
    }.ToFrozenSet(StringComparer.Ordinal);
}
