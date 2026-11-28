namespace FactsPDF;

/// <summary>
/// The element states selectors read that the document itself decides: its language, whether it is a link
/// and whether it is editable. A printed page has no user-action, focus or form state, so the pseudo-classes
/// for those never match (see <see cref="CssPseudoKind.Never"/>). Every element the reader accepts is a plain
/// content element: no form control, details or dialog, so :enabled, :disabled, :checked, :required,
/// :optional, :valid, :invalid, :default, :indeterminate, :in-range, :out-of-range and :placeholder-shown
/// match nothing. An element added to the reader later that has such a state must be modelled here.
/// </summary>
internal static class CssElementState
{
    /// <summary>:lang(range): the element's language, from the lang attribute of the nearest ancestor-or-self
    /// that has one, is the range or begins with it followed by a hyphen (ASCII case-insensitively). An empty
    /// lang attribute means no language and matches nothing; so does a document without any lang.</summary>
    internal static bool Language(string range, int node, in CssTree tree)
    {
        for (var current = node; current >= 0; current = tree.Parent(current))
        {
            tree.Budget.Work(tree.Offset);
            if (!tree.Element(current).Attributes.TryGetValue("lang", out var language)) continue;
            if (language.Length == 0) return false;
            if (language.Length < range.Length) return false;
            if (!language.AsSpan(0, range.Length).Equals(range, StringComparison.OrdinalIgnoreCase)) return false;
            return language.Length == range.Length || language[range.Length] == '-';
        }
        return false;
    }

    /// <summary>:link and :any-link: an a or area element with an href attribute, or an svg a with href or xlink:href.
    /// No page was visited, so :visited never matches.</summary>
    internal static bool Link(in CssElement element, int node, in CssTree tree)
    {
        if (!(element.Name is "a" or "area")) return false;
        if (element.Attributes.ContainsKey("href")) return true;
        return element.Attributes.ContainsKey("xlink:href") && CssSelectorMatcher.IsSvg(element, node, tree);
    }

    /// <summary>Whether the element is editable: the nearest ancestor-or-self whose contenteditable attribute is
    /// valid (empty, true, plaintext-only or false) decides; another value inherits.</summary>
    internal static bool Editable(int node, in CssTree tree)
    {
        for (var current = node; current >= 0; current = tree.Parent(current))
        {
            tree.Budget.Work(tree.Offset);
            if (!tree.Element(current).Attributes.TryGetValue("contenteditable", out var value)) continue;
            if (value.Length == 0 || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("plaintext-only", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        }
        return false;
    }
}
