namespace FactsPDF;

/// <summary>
/// The document as selector matching sees it. A node is the element's start tag index in the reader's token
/// list when the document's <see cref="HtmlElementPositions"/> exist (they hold its parent, siblings and
/// children), and otherwise the element's depth in the ancestor path, which then is all there is.
/// </summary>
internal readonly struct CssTree(IReadOnlyList<CssElement> path, HtmlElementPositions? positions, CssBudget budget, int offset)
{
    internal CssBudget Budget => budget;
    internal int Offset => offset;
    internal HtmlElementPositions? Positions => positions;

    internal int NodeOfDepth(int depth) => positions is null ? depth : path[depth].Token;

    internal int Parent(int node) => positions is null ? node - 1 : positions.Parent(node);

    internal CssElement Element(int node) => positions is null ? path[node] : positions.Element(node);

    internal HtmlElementPositions Required => positions
        ?? throw new InvalidOperationException("Structural pseudo-classes and sibling combinators need the document's element positions.");

    internal int PreviousSibling(int node) => Required.PreviousSibling(node);
}

/// <summary>Matches compiled selectors against elements. Plain selectors (compounds joined by descendant and child
/// combinators whose pseudo-classes read the element alone) are matched along the ancestor path without
/// backtracking; selectors that read siblings or descendants are matched from the right through the document
/// tree, every (compound, element) pair tried once. Every step is charged to the CSS match budget.</summary>
internal static class CssSelectorMatcher
{
    internal static bool Matches(CssSelector selector, IReadOnlyList<CssElement> path, CssBudget budget, HtmlElementPositions? positions)
    {
        if (path.Count == 0) return false;
        var tree = new CssTree(path, positions, budget, selector.Offset);
        return selector.NeedsTree
            ? MatchesInTree(selector, tree.NodeOfDepth(path.Count - 1), -1, tree)
            : MatchesAlongPath(selector, path, tree);
    }

    private static bool MatchesAlongPath(CssSelector selector, IReadOnlyList<CssElement> path, in CssTree tree)
    {
        var parts = selector.Parts;
        var last = path.Count - 1;
        if (!MatchCompound(parts[^1], path[last], tree.NodeOfDepth(last), tree, -1)) return false;
        if (parts.Length == 1) return true;
        if (parts.Length > path.Count) return false;
        // Dynamic programming preserves all possible ancestors in mixed child/descendant chains.
        // Work is bounded by compounds * ancestry; there is no exponential backtracking.
        Span<bool> candidates = path.Count <= 256 ? stackalloc bool[path.Count] : new bool[path.Count];
        Span<bool> next = path.Count <= 256 ? stackalloc bool[path.Count] : new bool[path.Count];
        candidates.Clear(); next.Clear(); candidates[^1] = true;
        for (var p = parts.Length - 2; p >= 0; p--)
        {
            next.Clear(); var below = false; var any = false;
            for (var n = path.Count - 2; n >= 0; n--)
            {
                tree.Budget.Work(selector.Offset); below |= candidates[n + 1];
                var reachable = parts[p + 1].Combinator == CssCombinator.Child ? candidates[n + 1] : below;
                if (reachable && MatchCompound(parts[p], path[n], tree.NodeOfDepth(n), tree, -1)) { next[n] = true; any = true; }
            }
            if (!any) return false;
            next.CopyTo(candidates);
        }
        return true;
    }

    // A selector that reads the tree matches from the right through nodes of the element tree. A
    // (compound, node) pair that failed once fails again whatever reached it, so each pair is tried once: the
    // work is at most the compounds times the elements near the node, every step charged to the match budget.
    // anchor: the element a relative selector's leading placeholder stands for (-1 for an ordinary selector).
    private static bool MatchesInTree(CssSelector selector, int node, int anchor, in CssTree tree)
    {
        var walk = new Walk(selector, anchor, tree);
        return walk.Matches(node);
    }

    private struct Walk(CssSelector selector, int anchor, CssTree tree)
    {
        private HashSet<long>? failed;
        private readonly CssCompound[] parts = selector.Parts;

        internal bool Matches(int node)
        {
            // The rightmost compound decides most candidates without any bookkeeping.
            var element = tree.Element(node);
            if (!MatchCompound(parts[^1], element, node, tree, anchor)) return false;
            return parts.Length == 1 || Related(parts.Length - 1, node);
        }

        private bool Reach(int part, int node)
        {
            var key = ((long)part << 32) | (uint)node;
            if (failed is not null && failed.Contains(key)) return false;
            tree.Budget.Work(tree.Offset);
            var found = MatchCompound(parts[part], tree.Element(node), node, tree, anchor)
                && (part == 0 || Related(part, node));
            if (!found) (failed ??= []).Add(key);
            return found;
        }

        // Whether the compound before parts[part] matches an element related to the node.
        private bool Related(int part, int node)
        {
            switch (parts[part].Combinator)
            {
                case CssCombinator.Child:
                    return tree.Parent(node) is >= 0 and var parent && Reach(part - 1, parent);
                case CssCombinator.Descendant:
                    for (var ancestor = tree.Parent(node); ancestor >= 0; ancestor = tree.Parent(ancestor))
                        if (Reach(part - 1, ancestor)) return true;
                    return false;
                case CssCombinator.NextSibling:
                    return tree.PreviousSibling(node) is >= 0 and var previous && Reach(part - 1, previous);
                default:
                    for (var sibling = tree.PreviousSibling(node); sibling >= 0; sibling = tree.PreviousSibling(sibling))
                        if (Reach(part - 1, sibling)) return true;
                    return false;
            }
        }
    }

    // Whether any selector of a list matches the node's element.
    private static bool MatchesAny(CssSelector[] selectors, in CssElement element, int node, in CssTree tree)
    {
        foreach (var selector in selectors)
        {
            tree.Budget.Work(tree.Offset);
            if (selector.Parts.Length == 1 && !selector.NeedsTree
                ? MatchCompound(selector.Parts[0], element, node, tree, -1)
                : MatchesInTree(selector, node, -1, tree))
                return true;
        }
        return false;
    }

    // anchor: the node a relative selector's placeholder compound matches (-1 elsewhere).
    private static bool MatchCompound(CssCompound part, in CssElement element, int node, in CssTree tree, int anchor)
    {
        var budget = tree.Budget; var offset = tree.Offset;
        budget.Work(offset);
        if (part.Anchor) return node == anchor;
        if (part.Type is not null && !part.Type.Equals(element.Name, StringComparison.OrdinalIgnoreCase)) return false;
        if (part.Namespace != CssNamespaceFilter.Any && !InNamespace(part.Namespace, element, node, tree)) return false;
        foreach (var id in part.Ids) { budget.Work(offset); if (!id.Equals(element.Id, StringComparison.Ordinal)) return false; }
        foreach (var name in part.Classes) { budget.Work(offset); if (!element.Classes.Contains(name)) return false; }
        foreach (var attribute in part.Attributes)
        {
            budget.Work(offset);
            if (!MatchesAttribute(attribute, element, node, tree)) return false;
        }
        foreach (var pseudo in part.PseudoClasses)
            if (!MatchesPseudoClass(pseudo, element, node, tree)) return false;
        return true;
    }

    // ---- Namespaces ----

    // An element is in the SVG namespace when it is an svg element or a child of one that is, except inside
    // the HTML integration points of svg (foreignObject, desc and title), whose children are HTML.
    internal static bool IsSvg(in CssElement element, int node, in CssTree tree)
    {
        if (element.Name.Equals("svg", StringComparison.Ordinal)) return true;
        var parent = tree.Parent(node);
        if (parent < 0) return false;
        var parentElement = tree.Element(parent);
        return !(parentElement.Name is "foreignobject" or "desc" or "title") && IsSvg(parentElement, parent, tree);
    }

    private static bool InNamespace(CssNamespaceFilter filter, in CssElement element, int node, in CssTree tree)
        => filter switch
        {
            CssNamespaceFilter.XHtml => !IsSvg(element, node, tree),
            CssNamespaceFilter.Svg => IsSvg(element, node, tree),
            CssNamespaceFilter.Never => false,
            _ => true
        };

    // ---- Attributes ----

    private static bool MatchesAttribute(CssAttributeMatch attribute, in CssElement element, int node, in CssTree tree)
    {
        string? actual;
        switch (attribute.Namespace)
        {
            case CssAttributeNamespace.None:
                if (!element.Attributes.TryGetValue(attribute.Name, out actual)) return false;
                // The attributes of an svg element the HTML parser puts in the xlink, xml and xmlns namespaces are
                // not in the null namespace: [xlink\:href] does not select them.
                if (attribute.Name.Contains(':') && ForeignPrefix(attribute.Name) is not null && IsSvg(element, node, tree)) return false;
                break;
            case CssAttributeNamespace.Any:
                if (!element.Attributes.TryGetValue(attribute.Name, out actual))
                {
                    if (!IsSvg(element, node, tree)) return false;
                    if (!element.Attributes.TryGetValue("xlink:" + attribute.Name, out actual)
                        && !element.Attributes.TryGetValue("xml:" + attribute.Name, out actual)
                        && !element.Attributes.TryGetValue("xmlns:" + attribute.Name, out actual))
                        return false;
                }
                break;
            case CssAttributeNamespace.Never:
                return false;
            default:
                if (!IsSvg(element, node, tree)) return false;
                var prefix = attribute.Namespace switch { CssAttributeNamespace.Xlink => "xlink:", CssAttributeNamespace.Xml => "xml:", _ => "xmlns:" };
                if (!element.Attributes.TryGetValue(prefix + attribute.Name, out actual)) return false;
                break;
        }
        if (attribute.Operator == CssAttributeOperator.Presence) return true;
        var fold = attribute.IgnoreCase || attribute.HtmlFold && !IsSvg(element, node, tree);
        return MatchesValue(attribute, actual, fold, tree);
    }

    private static string? ForeignPrefix(string name)
        => name.StartsWith("xlink:", StringComparison.Ordinal) ? "xlink"
            : name.StartsWith("xml:", StringComparison.Ordinal) ? "xml"
            : name.StartsWith("xmlns:", StringComparison.Ordinal) ? "xmlns" : null;

    private static bool MatchesValue(CssAttributeMatch attribute, string actual, bool fold, in CssTree tree)
    {
        var expected = attribute.Value!;
        var budget = tree.Budget; var offset = tree.Offset;
        // An empty operand never matches with ^=, $=, *= and ~=, and |= matches only an empty value.
        return attribute.Operator switch
        {
            CssAttributeOperator.Equals => Equal(actual.AsSpan(), expected.AsSpan(), fold, budget, offset),
            CssAttributeOperator.StartsWith => expected.Length != 0 && actual.Length >= expected.Length &&
                Equal(actual.AsSpan(0, expected.Length), expected.AsSpan(), fold, budget, offset),
            CssAttributeOperator.EndsWith => expected.Length != 0 && actual.Length >= expected.Length &&
                Equal(actual.AsSpan(actual.Length - expected.Length), expected.AsSpan(), fold, budget, offset),
            CssAttributeOperator.Contains => expected.Length != 0 && Contains(actual, expected, fold, budget, offset),
            CssAttributeOperator.Includes => expected.Length != 0 && Includes(actual, expected, fold, budget, offset),
            CssAttributeOperator.Dash => Equal(actual.AsSpan(), expected.AsSpan(), fold, budget, offset) ||
                actual.Length > expected.Length && actual[expected.Length] == '-' &&
                Equal(actual.AsSpan(0, expected.Length), expected.AsSpan(), fold, budget, offset),
            _ => throw new InvalidOperationException("Unsupported attribute operator.")
        };
    }

    // The i flag folds ASCII letters only, never locale or Unicode mappings.
    private static bool EqualChar(char a, char b, bool ignoreCase)
    {
        if (!ignoreCase) return a == b;
        if (a is >= 'A' and <= 'Z') a = (char)(a + 32);
        if (b is >= 'A' and <= 'Z') b = (char)(b + 32);
        return a == b;
    }

    // Charge every compared character, including long ancestor attributes
    // repeatedly checked for descendant selectors.
    private static bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right, bool ignoreCase, CssBudget budget, int offset)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            budget.Work(offset);
            if (!EqualChar(left[i], right[i], ignoreCase)) return false;
        }
        return true;
    }

    private static bool Contains(string actual, string expected, bool fold, CssBudget budget, int offset)
    {
        // KMP bounds repeated-prefix searches to O(attribute + needle) work
        // instead of repeated O(needle) comparisons for each candidate offset.
        // Charge both prefix preparation and every actual comparison to the
        // same CSS work budget used for the rest of selector matching.
        var prefix = new int[expected.Length];
        for (int i = 1, matched = 0; i < expected.Length;)
        {
            budget.Work(offset);
            if (EqualChar(expected[i], expected[matched], fold))
                prefix[i++] = ++matched;
            else if (matched > 0)
                matched = prefix[matched - 1];
            else
                prefix[i++] = 0;
        }
        for (int i = 0, matched = 0; i < actual.Length;)
        {
            budget.Work(offset);
            if (EqualChar(actual[i], expected[matched], fold))
            {
                i++; matched++;
                if (matched == expected.Length) return true;
            }
            else if (matched > 0)
                matched = prefix[matched - 1];
            else
                i++;
        }
        return false;
    }

    private static bool Includes(string actual, string expected, bool fold, CssBudget budget, int offset)
    {
        // ~= matches complete ASCII-whitespace-separated words, not substrings.
        foreach (var ch in expected)
        {
            budget.Work(offset);
            if (HtmlTokens.Space(ch)) return false;
        }
        var i = 0;
        while (i < actual.Length)
        {
            while (i < actual.Length && HtmlTokens.Space(actual[i]))
            {
                budget.Work(offset);
                i++;
            }
            var start = i;
            while (i < actual.Length && !HtmlTokens.Space(actual[i]))
            {
                budget.Work(offset);
                i++;
            }
            if (start < i)
            {
                budget.Work(offset);
                if (Equal(actual.AsSpan(start, i - start), expected.AsSpan(), fold, budget, offset)) return true;
            }
        }
        return false;
    }

    // ---- Pseudo-classes ----

    private static bool MatchesPseudoClass(CssPseudoClass pseudo, in CssElement element, int node, in CssTree tree)
    {
        tree.Budget.Work(tree.Offset);
        switch (pseudo.Kind)
        {
            // :root is the html element with no parent: the reader always fills the first entry of the ancestor
            // path with html (implied by the HTML parser when the markup omits it).
            case CssPseudoKind.Root: return tree.Parent(node) < 0 && element.Name.Equals("html", StringComparison.Ordinal);
            case CssPseudoKind.Never: return false;
            case CssPseudoKind.Always: return true;
            case CssPseudoKind.Not: return !MatchesAny(pseudo.Selectors!, element, node, tree);
            case CssPseudoKind.Is or CssPseudoKind.Where: return MatchesAny(pseudo.Selectors!, element, node, tree);
            case CssPseudoKind.Has: return MatchesHas(pseudo, node, tree);
            case CssPseudoKind.Lang: return CssElementState.Language(pseudo.Text!, node, tree);
            case CssPseudoKind.Link: return CssElementState.Link(element, node, tree);
            case CssPseudoKind.ReadOnly: return !IsSvg(element, node, tree) && !CssElementState.Editable(node, tree);
            case CssPseudoKind.ReadWrite: return !IsSvg(element, node, tree) && CssElementState.Editable(node, tree);
        }
        var positions = tree.Required;
        if (pseudo.Selectors is not null && pseudo.Kind is CssPseudoKind.NthChild or CssPseudoKind.NthLastChild)
            return MatchesNthOf(pseudo, element, node, tree);
        var position = positions[node];
        return pseudo.Kind switch
        {
            CssPseudoKind.Empty => position.Empty,
            CssPseudoKind.NthChild => Nth(pseudo, position.Index),
            CssPseudoKind.NthLastChild => Nth(pseudo, position.Count - position.Index + 1),
            CssPseudoKind.NthOfType => Nth(pseudo, position.TypeIndex),
            CssPseudoKind.NthLastOfType => Nth(pseudo, position.TypeCount - position.TypeIndex + 1),
            CssPseudoKind.OnlyChild => position.Count == 1,
            CssPseudoKind.OnlyOfType => position.TypeCount == 1,
            _ => throw new InvalidOperationException("Unsupported pseudo-class kind.")
        };
    }

    // Whether some n >= 0 gives A×n+B = index.
    private static bool Nth(CssPseudoClass pseudo, long index)
    {
        var (a, b) = (pseudo.A, pseudo.B);
        if (a == 0) return index == b;
        var steps = index - b;
        return steps % a == 0 && steps / a >= 0;
    }

    // :nth-child(An+B of S): the element's place among the siblings that match S, which it must match itself.
    private static bool MatchesNthOf(CssPseudoClass pseudo, in CssElement element, int node, in CssTree tree)
    {
        var cache = tree.Budget.PseudoResults;
        var key = (pseudo.Id, node);
        if (cache.TryGetValue(key, out var known)) return known;
        var result = false;
        if (MatchesAny(pseudo.Selectors!, element, node, tree))
        {
            var index = 1L;
            var positions = tree.Required;
            var reverse = pseudo.Kind == CssPseudoKind.NthLastChild;
            for (var sibling = reverse ? positions.NextSibling(node) : positions.PreviousSibling(node); sibling >= 0;
                 sibling = reverse ? positions.NextSibling(sibling) : positions.PreviousSibling(sibling))
            {
                if (MatchesAny(pseudo.Selectors!, tree.Element(sibling), sibling, tree)) index++;
            }
            result = Nth(pseudo, index);
        }
        cache[key] = result;
        return result;
    }

    // :has(): some relative selector matches from the element. The candidates are the element's descendants (for
    // the descendant and child combinators) or its following siblings and their descendants (for + and ~);
    // each is matched from the right with the leftmost placeholder tied to the element. The answer is kept per
    // (pseudo-class, element): descendant selectors ask it of the same ancestors for every element below.
    private static bool MatchesHas(CssPseudoClass pseudo, int anchor, in CssTree tree)
    {
        var cache = tree.Budget.PseudoResults;
        var key = (pseudo.Id, anchor);
        if (cache.TryGetValue(key, out var known)) return known;
        var positions = tree.Required;
        var result = false;
        foreach (var selector in pseudo.Selectors!)
        {
            var walk = new Walk(selector, anchor, tree);
            var lead = selector.Parts[1].Combinator;
            var single = selector.Parts.Length == 2;
            if (lead is CssCombinator.Descendant or CssCombinator.Child)
            {
                var onlyChildren = lead == CssCombinator.Child && single;
                for (var candidate = positions.FirstChild(anchor); candidate >= 0;
                     candidate = onlyChildren ? positions.NextSibling(candidate) : NextInSubtree(positions, candidate, anchor))
                {
                    tree.Budget.Work(tree.Offset);
                    if (walk.Matches(candidate)) { result = true; break; }
                }
            }
            else
            {
                var onlyNext = lead == CssCombinator.NextSibling && single;
                for (var sibling = positions.NextSibling(anchor); sibling >= 0 && !result; sibling = positions.NextSibling(sibling))
                {
                    for (var candidate = sibling; candidate >= 0; candidate = NextInSubtree(positions, candidate, sibling))
                    {
                        tree.Budget.Work(tree.Offset);
                        if (walk.Matches(candidate)) { result = true; break; }
                        if (onlyNext) break;
                    }
                    if (onlyNext) break;
                }
            }
            if (result) break;
        }
        cache[key] = result;
        return result;
    }

    // The element after `node` in document order within the subtree of `root`, or -1 when `node` was the last.
    private static int NextInSubtree(HtmlElementPositions positions, int node, int root)
    {
        var child = positions.FirstChild(node);
        if (child >= 0) return child;
        while (node != root)
        {
            var sibling = positions.NextSibling(node);
            if (sibling >= 0) return sibling;
            node = positions.Parent(node);
            if (node < 0) return -1;
        }
        return -1;
    }
}
