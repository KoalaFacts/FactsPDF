namespace FactsPDF;

internal sealed record CssElement(string Name, string? Id, CssClassList Classes,
    IReadOnlyDictionary<string, string> Attributes)
{
    internal static CssElement From(HtmlToken token)
    {
        token.Attributes!.TryGetValue("id", out var id);
        var classes = token.Attributes.TryGetValue("class", out var text) ? CssClassList.Read(text) : default;
        return new(token.Value, id, classes, token.Attributes);
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
}
internal enum CssAttributeOperator { Presence, Equals, StartsWith, EndsWith, Contains, Includes, Dash }
internal readonly record struct CssAttributeMatch(
    string Name, CssAttributeOperator Operator, string? Value, bool IgnoreCase);
internal sealed record CssCompound(string? Type, string[] Ids, string[] Classes,
    CssAttributeMatch[] Attributes, bool ChildOfPrevious);

internal sealed class CssSelector(CssCompound[] parts, CssSpecificity specificity, int offset)
{
    private static bool IsSymbol(CssToken token, string value)
        => token.Kind == CssTokenKind.Symbol && token.Is(value);

    internal CssCompound Rightmost => parts[^1];
    internal CssSpecificity Specificity => specificity;
    internal int Offset => offset;

    internal static CssSelector Parse(List<CssToken> tokens, ref int i, CssBudget budget)
    {
        var offset = tokens[i].Offset; budget.Selector(offset);
        var parts = new List<CssCompound>(); var idCount = 0; var classCount = 0; var typeCount = 0; var child = false;
        while (true)
        {
            budget.Token.ThrowIfCancellationRequested();
            CssBudget.Check(parts.Count < 32, "A selector may contain at most 32 compounds.", offset);
            string? type = null; var ids = new List<string>(); var classes = new List<string>();
            var attrs = new List<CssAttributeMatch>(); var simple = 0;
            if (tokens[i].Kind == CssTokenKind.Identifier) { type = tokens[i++].Text.ToLowerInvariant(); simple++; typeCount++; }
            else if (IsSymbol(tokens[i], "*")) { i++; simple++; }
            while (true)
            {
                var current = tokens[i];
                if (simple > 0 && current.LeadingSpace) break;
                if (IsSymbol(current, "."))
                {
                    i++;
                    if (tokens[i].Kind != CssTokenKind.Identifier || tokens[i].LeadingSpace) throw CssTokens.Invalid("Expected a class identifier adjacent to '.'.", current.Offset);
                    classes.Add(tokens[i++].Text); classCount++;
                }
                else if (current.Kind == CssTokenKind.Hash)
                {
                    var id = current.Text[1..];
                    if (!CssTokens.IdentifierStart(id, 0)) throw CssTokens.Invalid("Escaped/numeric ID selectors are not supported.", current.Offset);
                    ids.Add(id); idCount++; i++;
                }
                else if (IsSymbol(current, "["))
                {
                    // Match actual HTML attributes, not an inferred CSS class.
                    i++;
                    if (tokens[i].Kind != CssTokenKind.Identifier)
                        throw CssTokens.Invalid("Expected an attribute name.", current.Offset);
                    var name = tokens[i++].Text.ToLowerInvariant();
                    var supported = name is "id" or "class" or "title" or "lang"
                        or "href" or "src" or "alt"
                        || name.StartsWith("data-", StringComparison.Ordinal) && name.Length > 5;
                    if (!supported)
                        throw CssTokens.Invalid("Only known static HTML attributes and data-* can be selected.", current.Offset);
                    var op = CssAttributeOperator.Presence;
                    string? expected = null;
                    var ignoreCase = false;
                    if (tokens[i].Kind == CssTokenKind.Symbol && tokens[i].Text is
                        ("=" or "^" or "$" or "*" or "~" or "|"))
                    {
                        var operatorToken = tokens[i++];
                        op = operatorToken.Text switch
                        {
                            "=" => CssAttributeOperator.Equals,
                            "^" => CssAttributeOperator.StartsWith,
                            "$" => CssAttributeOperator.EndsWith,
                            "*" => CssAttributeOperator.Contains,
                            "~" => CssAttributeOperator.Includes,
                            "|" => CssAttributeOperator.Dash,
                            _ => throw new InvalidOperationException()
                        };
                        // CSS Syntax supplies separate delim tokens for ^=, $=,
                        // *=, ~= and |=. Whitespace inside the operator is invalid.
                        if (op != CssAttributeOperator.Equals)
                        {
                            if (!IsSymbol(tokens[i], "=") || tokens[i].LeadingSpace)
                                throw CssTokens.Invalid("Expected an adjacent '=' in attribute operator.", operatorToken.Offset);
                            i++;
                        }
                        if (tokens[i].Kind is not (CssTokenKind.Identifier or CssTokenKind.String))
                            throw CssTokens.Invalid("Attribute comparison requires an identifier or quoted string.", operatorToken.Offset);
                        expected = tokens[i++].Text;
                        // Empty non-equality operands are outside this bounded subset.
                        if (op != CssAttributeOperator.Equals && expected.Length == 0)
                            throw CssTokens.Invalid("Empty substring/word/language attribute operands are unsupported.", operatorToken.Offset);
                        if (tokens[i].Kind == CssTokenKind.Identifier)
                        {
                            if (tokens[i].Text.Equals("i", StringComparison.OrdinalIgnoreCase))
                                ignoreCase = true;
                            else if (!tokens[i].Text.Equals("s", StringComparison.OrdinalIgnoreCase))
                                throw CssTokens.Invalid("Only the CSS attribute-value i/s flags are supported.", tokens[i].Offset);
                            i++;
                        }
                    }
                    if (!IsSymbol(tokens[i], "]"))
                        throw CssTokens.Invalid("Malformed or unsupported attribute selector.", current.Offset);
                    i++;
                    attrs.Add(new(name, op, expected, ignoreCase)); classCount++;
                }
                else break;
                CssBudget.Check(++simple <= 32, "A compound selector may contain at most 32 simple terms.", offset);
            }
            if (simple == 0) throw CssTokens.Invalid("Expected a supported selector.", tokens[i].Offset);
            parts.Add(new(type, ids.ToArray(), classes.ToArray(), attrs.ToArray(), child));
            if (IsSymbol(tokens[i], ",") || IsSymbol(tokens[i], "{")) break;
            if (tokens[i].Kind == CssTokenKind.End) throw CssTokens.Invalid("Expected a CSS declaration block.", tokens[i].Offset);
            if (IsSymbol(tokens[i], ">")) { child = true; i++; }
            else if (tokens[i].LeadingSpace) child = false;
            else throw CssTokens.Invalid("Unsupported or malformed selector/combinator.", tokens[i].Offset);
        }
        return new(parts.ToArray(), new(idCount, classCount, typeCount), offset);
    }

    internal bool Matches(IReadOnlyList<CssElement> path, CssBudget budget)
    {
        if (path.Count == 0 || !MatchesCompound(parts[^1], path[^1], budget)) return false;
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
                budget.Work(offset); below |= candidates[n + 1];
                var reachable = parts[p + 1].ChildOfPrevious ? candidates[n + 1] : below;
                if (reachable && MatchesCompound(parts[p], path[n], budget)) { next[n] = true; any = true; }
            }
            if (!any) return false;
            next.CopyTo(candidates);
        }
        return true;
    }
    private bool MatchesCompound(CssCompound part, CssElement element, CssBudget budget)
    {
        budget.Work(offset);
        if (part.Type is not null && !part.Type.Equals(element.Name, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var id in part.Ids) { budget.Work(offset); if (!id.Equals(element.Id, StringComparison.Ordinal)) return false; }
        foreach (var name in part.Classes) { budget.Work(offset); if (!element.Classes.Contains(name)) return false; }
        foreach (var attribute in part.Attributes)
        {
            budget.Work(offset);
            if (!element.Attributes.TryGetValue(attribute.Name, out var actual) ||
                !MatchesAttribute(attribute, actual, budget))
                return false;
        }
        return true;
    }

    private bool MatchesAttribute(CssAttributeMatch attribute, string actual, CssBudget budget)
    {
        if (attribute.Operator == CssAttributeOperator.Presence) return true;
        var expected = attribute.Value!;
        var fold = attribute.IgnoreCase;
        return attribute.Operator switch
        {
            CssAttributeOperator.Equals => Equal(actual.AsSpan(), expected.AsSpan(), fold, budget),
            CssAttributeOperator.StartsWith => actual.Length >= expected.Length &&
                Equal(actual.AsSpan(0, expected.Length), expected.AsSpan(), fold, budget),
            CssAttributeOperator.EndsWith => actual.Length >= expected.Length &&
                Equal(actual.AsSpan(actual.Length - expected.Length), expected.AsSpan(), fold, budget),
            CssAttributeOperator.Contains => Contains(actual, expected, fold, budget),
            CssAttributeOperator.Includes => Includes(actual, expected, fold, budget),
            CssAttributeOperator.Dash => Equal(actual.AsSpan(), expected.AsSpan(), fold, budget) ||
                actual.Length > expected.Length && actual[expected.Length] == '-' &&
                Equal(actual.AsSpan(0, expected.Length), expected.AsSpan(), fold, budget),
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
    private bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right,
        bool ignoreCase, CssBudget budget)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            budget.Work(offset);
            if (!EqualChar(left[i], right[i], ignoreCase)) return false;
        }
        return true;
    }

    private bool Contains(string actual, string expected, bool fold, CssBudget budget)
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

    private bool Includes(string actual, string expected, bool fold, CssBudget budget)
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
                if (Equal(actual.AsSpan(start, i - start), expected.AsSpan(), fold, budget)) return true;
            }
        }
        return false;
    }
}
