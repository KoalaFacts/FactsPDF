namespace FactsPDF;

/// <summary>
/// Global attributes that change nothing a browser prints, accepted on every element. Each was
/// checked against what Chrome paints for it: identity and styling hooks (id, class, title, lang,
/// data-*), event handlers (FactsPDF runs no script and a printed page receives no events),
/// accessibility semantics (role, aria-*), focus and editing hints that apply only to interaction
/// (tabindex, accesskey, inert, autocapitalize, autocorrect, enterkeyhint, inputmode, spellcheck,
/// writingsuggestions, draggable, translate), microdata (itemscope, itemprop, itemtype, itemid,
/// itemref), shadow-tree names (slot, part, exportparts) and nonce. dir and hidden have their own
/// rules; contenteditable (it changes line breaking), popover (display:none until shown) and
/// autofocus (a focused element may print a focus ring) change the page and are not accepted.
/// </summary>
internal static class HtmlGlobalAttributes
{
    internal static bool NoEffect(string attribute) => attribute is "id" or "class" or "title" or "lang" or "role"
        or "tabindex" or "accesskey" or "inert" or "autocapitalize" or "autocorrect" or "enterkeyhint" or "inputmode"
        or "spellcheck" or "writingsuggestions" or "draggable" or "translate" or "itemscope" or "itemprop" or "itemtype"
        or "itemid" or "itemref" or "slot" or "part" or "exportparts" or "nonce"
        || attribute.StartsWith("data-", StringComparison.Ordinal)
        || attribute.Length > 5 && attribute.StartsWith("aria-", StringComparison.Ordinal)
        || attribute.Length > 2 && attribute.StartsWith("on", StringComparison.Ordinal);

    /// <summary>The value an xmlns attribute may have on html in an HTML document: the HTML
    /// namespace, which the parser already gives every element, so it changes nothing.</summary>
    internal const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
}
