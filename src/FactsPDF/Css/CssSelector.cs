namespace FactsPDF;

// A value in the reader's ancestor path: an element costs no object of its own, only its slot in
// the path list, which is as deep as the document. Token is the index of its start tag in the
// reader's token list (see HtmlElementPositions), or -1 when it was read on its own.
internal readonly record struct CssElement(string Name, string? Id, CssClassList Classes,
    HtmlAttributes Attributes, int Token = -1)
{
    internal static CssElement From(HtmlToken token, int index = -1)
    {
        token.Attributes!.TryGetValue("id", out var id);
        var classes = token.Attributes.TryGetValue("class", out var text) ? CssClassList.Read(text) : default;
        return new(token.Value, id, classes, token.Attributes, index);
    }
}

/// <summary>An element's distinct class names in first-occurrence order. Elements without
/// classes share one empty array and a lone class reuses the attribute text; a long list also
/// keeps a set so selector matching stays linear in the selector's class count. Equality is
/// by storage (the record's generated equality would otherwise compare fields by reflection).</summary>
internal readonly struct CssClassList : IEquatable<CssClassList>
{
    private const int LinearLimit = 16;
    private static readonly string[] None = [];
    private readonly string[]? names;
    private readonly HashSet<string>? lookup;

    private CssClassList(string[] names, HashSet<string>? lookup) { this.names = names; this.lookup = lookup; }

    internal ReadOnlySpan<string> Names => names ?? None;

    internal bool SharesStorageWith(CssClassList other) => ReferenceEquals(names ?? None, other.names ?? None);

    public bool Equals(CssClassList other) => SharesStorageWith(other);
    public override bool Equals(object? obj) => obj is CssClassList other && Equals(other);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(names ?? None);

    internal bool Contains(string name)
    {
        if (lookup is not null) return lookup.Contains(name);
        var list = names;
        if (list is null) return false;
        for (var i = 0; i < list.Length; i++)
            if (string.Equals(list[i], name, StringComparison.Ordinal)) return true;
        return false;
    }

    internal static CssClassList Read(string text)
    {
        var count = 0; var first = 0; var firstEnd = 0;
        for (var i = 0; i < text.Length;)
        {
            while (i < text.Length && HtmlTokens.Space(text[i])) i++;
            var start = i; while (i < text.Length && !HtmlTokens.Space(text[i])) i++;
            if (i > start && count++ == 0) { first = start; firstEnd = i; }
        }
        if (count == 0) return default;
        if (count == 1) return new([first == 0 && firstEnd == text.Length ? text : text[first..firstEnd]], null);
        var found = new string[count];
        var lookup = count > LinearLimit ? new HashSet<string>(count, StringComparer.Ordinal) : null;
        var distinct = 0;
        for (var i = 0; i < text.Length;)
        {
            while (i < text.Length && HtmlTokens.Space(text[i])) i++;
            var start = i; while (i < text.Length && !HtmlTokens.Space(text[i])) i++;
            if (i == start) continue;
            var span = text.AsSpan(start, i - start);
            if (lookup is not null)
            {
                var name = span.ToString();
                if (lookup.Add(name)) found[distinct++] = name;
                continue;
            }
            var seen = false;
            for (var j = 0; j < distinct && !seen; j++) seen = span.SequenceEqual(found[j]);
            if (!seen) found[distinct++] = span.ToString();
        }
        if (distinct < count) Array.Resize(ref found, distinct);
        return new(found, lookup);
    }
}

internal readonly record struct CssSpecificity(int Ids, int Classes, int Types) : IComparable<CssSpecificity>
{
    public int CompareTo(CssSpecificity other)
    { var c = Ids.CompareTo(other.Ids); if (c != 0) return c; c = Classes.CompareTo(other.Classes); return c != 0 ? c : Types.CompareTo(other.Types); }

    internal CssSpecificity Add(CssSpecificity other) => new(Ids + other.Ids, Classes + other.Classes, Types + other.Types);
}
internal enum CssAttributeOperator { Presence, Equals, StartsWith, EndsWith, Contains, Includes, Dash }

// Which namespace an element selector accepts. HTML documents hold XHTML-namespace elements and, inside
// an svg element, SVG-namespace ones; every other namespace (no namespace, MathML, an undeclared URI)
// has no element, so a selector in it never matches.
internal enum CssNamespaceFilter : byte { Any, XHtml, Svg, Never }

// The namespace of an attribute selector's name: none (the usual null namespace), any (*|name), or one
// of the namespaces the HTML parser gives the foreign attributes of an svg element (xlink:href, xml:lang, xmlns:xlink).
internal enum CssAttributeNamespace : byte { None, Any, Xlink, Xml, Xmlns, Never }

/// <summary>An attribute selector. HtmlFold: the attribute is one whose value HTML matches ASCII
/// case-insensitively on an HTML element (type, dir, target, rel and the other enumerated attributes).</summary>
internal readonly record struct CssAttributeMatch(
    string Name, CssAttributeOperator Operator, string? Value, bool IgnoreCase,
    CssAttributeNamespace Namespace = CssAttributeNamespace.None, bool HtmlFold = false);

// :first-child is :nth-child(1), :last-of-type is :nth-last-of-type(1), and so on. Never is a pseudo-class a
// printed page can never satisfy (:hover, :focus, :visited, :target, :host and the other user-action, dynamic
// and shadow tree pseudo-classes); Always is one every element satisfies (:defined, :dir(ltr): every run is
// laid out left to right).
internal enum CssPseudoKind : byte
{
    NthChild, NthLastChild, NthOfType, NthLastOfType, OnlyChild, OnlyOfType, Empty, Not, Root,
    Is, Where, Has, Lang, Never, Always, Link, ReadOnly, ReadWrite
}

/// <summary>A pseudo-class. The nth kinds match the positions A×n+B for n ≥ 0 (among the siblings that match
/// <see cref="Selectors"/> when the argument has an "of S" part); Not, Is, Where and Has hold their selector
/// lists (Has: relative selectors anchored at the element); Lang holds its language range in
/// <see cref="Text"/>.</summary>
internal sealed record CssPseudoClass(CssPseudoKind Kind, long A = 0, long B = 0, CssSelector[]? Selectors = null, string? Text = null)
{
    private static int nextId;

    /// <summary>A number unique to this pseudo-class, which keys its cached results per element.</summary>
    internal int Id { get; } = Interlocked.Increment(ref nextId);
}

// How a compound relates to the one before it: the descendant (white space), child (>),
// next-sibling (+) and subsequent-sibling (~) combinators.
internal enum CssCombinator : byte { Descendant, Child, NextSibling, SubsequentSibling }

/// <summary>A compound selector. Namespace is the namespace the compound's element must be in; Nesting marks a
/// nesting selector (&amp;) until the enclosing rule's selectors replace it; Anchor is the leftmost, always
/// matching placeholder of a relative selector (the element :has() is tested on).</summary>
internal sealed record CssCompound(string? Type, string[] Ids, string[] Classes,
    CssAttributeMatch[] Attributes, CssPseudoClass[] PseudoClasses, CssCombinator Combinator, bool Nesting = false,
    CssNamespaceFilter Namespace = CssNamespaceFilter.Any, bool Anchor = false);

// The pseudo-element a selector ends in: generated ::before and ::after boxes (also written
// :before and :after) are styled by their own rules, never the element's.
internal enum CssPseudoElement { None, Before, After }

internal sealed class CssSelector
{
    // An+B values beyond this fail: Chrome stops matching some valid selectors near the 32-bit
    // limits (for example -n+2147483647), so larger values cannot follow the browser.
    internal const long MaxNthValue = 1_000_000_000;

    private readonly CssCompound[] parts;

    internal CssSelector(CssCompound[] parts, int offset, CssPseudoElement pseudoElement = CssPseudoElement.None)
    {
        this.parts = parts;
        Offset = offset;
        PseudoElement = pseudoElement;
        var specificity = default(CssSpecificity);
        foreach (var part in parts)
        {
            if (part.Combinator is CssCombinator.NextSibling or CssCombinator.SubsequentSibling)
            { UsesStructure = true; NeedsTree = true; }
            if (part.Nesting) NestingCount++;
            if (part.Type is not null) specificity = specificity.Add(new(0, 0, 1));
            specificity = specificity.Add(new(part.Ids.Length, part.Classes.Length + part.Attributes.Length, 0));
            foreach (var pseudo in part.PseudoClasses) specificity = specificity.Add(Analyze(pseudo));
        }
        if (parts.Length > 1 && parts[0].Anchor) NeedsTree = true;
        if (NeedsTree) UsesStructure = true;
        if (pseudoElement != CssPseudoElement.None) specificity = specificity.Add(new(0, 0, 1));
        Specificity = specificity;
    }

    // The specificity a pseudo-class adds, and what it requires of matching.
    private CssSpecificity Analyze(CssPseudoClass pseudo)
    {
        var weight = new CssSpecificity(0, 1, 0);
        switch (pseudo.Kind)
        {
            case CssPseudoKind.NthChild or CssPseudoKind.NthLastChild or CssPseudoKind.NthOfType or CssPseudoKind.NthLastOfType
                or CssPseudoKind.OnlyChild or CssPseudoKind.OnlyOfType or CssPseudoKind.Empty:
                UsesStructure = true;
                break;
            case CssPseudoKind.Where:
                weight = default;
                break;
        }
        if (pseudo.Selectors is null) return weight;
        var best = default(CssSpecificity);
        foreach (var argument in pseudo.Selectors)
        {
            if (argument.Specificity.CompareTo(best) > 0) best = argument.Specificity;
            NestingCount += argument.NestingCount;
            UsesStructure |= argument.UsesStructure;
            // An argument with combinators, or one that reads siblings or descendants, is matched through the document tree.
            if (argument.NeedsTree || argument.parts.Length > 1) NeedsTree = true;
        }
        if (pseudo.Kind is CssPseudoKind.Has) NeedsTree = true;
        // "of S": positions among matching siblings are read through the document tree.
        if (pseudo.Kind is CssPseudoKind.NthChild or CssPseudoKind.NthLastChild) NeedsTree = true;
        return pseudo.Kind switch
        {
            CssPseudoKind.Where => default,
            CssPseudoKind.NthChild or CssPseudoKind.NthLastChild => weight.Add(best),
            _ => best
        };
    }

    internal CssCompound Rightmost => parts[^1];
    internal CssCompound[] Parts => parts;
    /// <summary>How many nesting selectors (&amp;, written or implied) the selector holds, in its compounds and
    /// in the arguments of its pseudo-classes, before it is resolved against its parent rule.</summary>
    internal int NestingCount { get; private set; }
    internal CssSpecificity Specificity { get; }
    internal int Offset { get; }
    /// <summary>Whether matching reads an element's position among its siblings, its emptiness or other elements of the document tree.</summary>
    internal bool UsesStructure { get; private set; }
    /// <summary>Whether matching walks the document tree (siblings, :has(), "of S", selectors with combinators inside
    /// :is() and :not()), not only the element and its ancestors.</summary>
    internal bool NeedsTree { get; private set; }
    internal CssPseudoElement PseudoElement { get; }

    /// <summary>The parent a top-level &amp; stands for: the root element (:scope in a stylesheet).</summary>
    internal static readonly CssSelector RootParent = new(
        [new(null, [], [], [], [new CssPseudoClass(CssPseudoKind.Root)], CssCombinator.Descendant)], 0);

    /// <summary>
    /// The selectors a nested rule stands for under its parent rule. The nesting selector means the
    /// parent's selector list (:is(), whose specificity is that of its most specific selector). A single
    /// parent selector is merged into the compound the nesting selector is written in (and, when it has
    /// several compounds and the nesting selector starts the nested selector, its combinators come first);
    /// a parent list, or any other place, becomes an :is() pseudo-class of the compound.
    /// </summary>
    internal static CssSelector[] ResolveNesting(CssSelector[] nested, CssSelector parent, CssBudget budget, int offset)
        => ResolveNesting(nested, [parent], budget, offset);

    internal static CssSelector[] ResolveNesting(CssSelector[] nested, IReadOnlyList<CssSelector> parents, CssBudget budget, int offset)
    {
        foreach (var parent in parents)
            if (parent.PseudoElement != CssPseudoElement.None)
                throw new FactsPdfException("FPDF1203", "A nested rule cannot be written inside a rule whose selector ends in a pseudo-element.", offset);
        var result = new CssSelector[nested.Length];
        for (var n = 0; n < nested.Length; n++)
        {
            var selector = nested[n];
            budget.Selector(selector.Offset);
            result[n] = selector.NestingCount == 0 ? selector : Resolve(selector, parents, budget);
        }
        return result;
    }

    private static CssSelector Resolve(CssSelector selector, IReadOnlyList<CssSelector> parents, CssBudget budget)
    {
        var flat = parents.Count == 1 ? parents[0] : null;
        var resolved = new List<CssCompound>(selector.parts.Length + 4);
        foreach (var part in selector.parts)
        {
            var own = part.PseudoClasses.Any(p => p.Selectors is not null && p.Selectors.Any(s => s.NestingCount > 0))
                ? part with { PseudoClasses = [.. part.PseudoClasses.Select(p => ResolveArguments(p, parents, budget))] }
                : part;
            if (!part.Nesting) { resolved.Add(own); continue; }
            if (flat is not null && TryMerge(flat, own, resolved.Count == 0, resolved)) continue;
            resolved.Add(own with
            {
                Nesting = false,
                PseudoClasses = [.. own.PseudoClasses, new CssPseudoClass(CssPseudoKind.Is, Selectors: [.. parents])]
            });
        }
        return new(resolved.ToArray(), selector.Offset, selector.PseudoElement);
    }

    private static CssPseudoClass ResolveArguments(CssPseudoClass pseudo, IReadOnlyList<CssSelector> parents, CssBudget budget)
        => pseudo.Selectors is null || pseudo.Selectors.All(s => s.NestingCount == 0) ? pseudo
            : pseudo with { Selectors = ResolveNesting(pseudo.Selectors, parents, budget, pseudo.Selectors[0].Offset) };

    // Replaces the nesting compound `own` by the single parent selector: its last compound merged into `own`, and
    // (for a parent with combinators, which can only start the nested selector) its earlier compounds before it.
    private static bool TryMerge(CssSelector parent, CssCompound own, bool first, List<CssCompound> resolved)
    {
        var last = parent.parts[^1];
        if (parent.parts.Length > 1 && !first) return false;
        if (last.Anchor || last.Nesting) return false;
        if (last.Type is not null && own.Type is not null && !last.Type.Equals(own.Type, StringComparison.Ordinal)) return false;
        if (last.Namespace != CssNamespaceFilter.Any && own.Namespace != CssNamespaceFilter.Any && last.Namespace != own.Namespace) return false;
        for (var p = 0; p < parent.parts.Length - 1; p++) resolved.Add(parent.parts[p]);
        resolved.Add(new(last.Type ?? own.Type, [.. last.Ids, .. own.Ids], [.. last.Classes, .. own.Classes],
            [.. last.Attributes, .. own.Attributes], [.. last.PseudoClasses, .. own.PseudoClasses],
            parent.parts.Length > 1 ? last.Combinator : own.Combinator, false,
            last.Namespace != CssNamespaceFilter.Any ? last.Namespace : own.Namespace));
        return true;
    }

    /// <summary>Whether the selector matches the element at the end of <paramref name="path"/> (the
    /// document's elements from the root down to it). <paramref name="positions"/> is needed (and
    /// supplied by the document) when <see cref="UsesStructure"/> is true.</summary>
    internal bool Matches(IReadOnlyList<CssElement> path, CssBudget budget, HtmlElementPositions? positions = null)
        => CssSelectorMatcher.Matches(this, path, budget, positions);
}
