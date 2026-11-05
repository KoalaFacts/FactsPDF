namespace FactsPDF;

internal sealed record CssElement(string Name, string? Id, HashSet<string> Classes,
    IReadOnlyDictionary<string, string> Attributes)
{
    internal static CssElement From(HtmlToken token)
    {
        token.Attributes!.TryGetValue("id", out var id); var classes = new HashSet<string>(StringComparer.Ordinal);
        if (token.Attributes.TryGetValue("class", out var text))
        {
            var i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && HtmlTokens.Space(text[i])) i++;
                var start = i; while (i < text.Length && !HtmlTokens.Space(text[i])) i++;
                if (i > start) classes.Add(text[start..i]);
            }
        }
        return new(token.Value, id, classes, token.Attributes);
    }
}
internal readonly record struct CssSpecificity(int Ids, int Classes, int Types) : IComparable<CssSpecificity>
{
    public int CompareTo(CssSpecificity other)
    { var c = Ids.CompareTo(other.Ids); if (c != 0) return c; c = Classes.CompareTo(other.Classes); return c != 0 ? c : Types.CompareTo(other.Types); }
}
internal readonly record struct CssAttributeMatch(string Name, string? Value);
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
                    string? expected = null;
                    if (IsSymbol(tokens[i], "="))
                    {
                        i++;
                        if (tokens[i].Kind is not (CssTokenKind.Identifier or CssTokenKind.String))
                            throw CssTokens.Invalid("Attribute equality requires an identifier or quoted string.", current.Offset);
                        expected = tokens[i++].Text;
                    }
                    if (!IsSymbol(tokens[i], "]"))
                        throw CssTokens.Invalid("Only [attribute] and [attribute=value] are supported.", current.Offset);
                    i++;
                    attrs.Add(new(name, expected)); classCount++;
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
                attribute.Value is not null && !attribute.Value.Equals(actual, StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
