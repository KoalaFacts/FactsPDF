using FactsPDF.CssSyntax;

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
internal readonly record struct CssAttributeMatch(
    string Name, CssAttributeOperator Operator, string? Value, bool IgnoreCase);

// :first-child is :nth-child(1), :last-of-type is :nth-last-of-type(1), and so on.
internal enum CssPseudoKind : byte { NthChild, NthLastChild, NthOfType, NthLastOfType, OnlyChild, OnlyOfType, Empty, Not, Root, Lang }

/// <summary>A pseudo-class. The nth kinds match the positions A×n+B for n ≥ 0; Not matches an
/// element that none of its compounds match; Lang matches an element whose language (its own or
/// its nearest ancestor's lang attribute) is Language or starts with Language and a hyphen.</summary>
internal sealed record CssPseudoClass(CssPseudoKind Kind, long A = 0, long B = 0, CssCompound[]? Not = null, string? Language = null);

// How a compound relates to the one before it: the descendant (white space), child (>),
// next-sibling (+) and subsequent-sibling (~) combinators.
internal enum CssCombinator : byte { Descendant, Child, NextSibling, SubsequentSibling }

internal sealed record CssCompound(string? Type, string[] Ids, string[] Classes,
    CssAttributeMatch[] Attributes, CssPseudoClass[] PseudoClasses, CssCombinator Combinator, bool Nesting = false);

// The pseudo-element a selector ends in: generated ::before and ::after boxes (also written
// :before and :after) are styled by their own rules, never the element's.
internal enum CssPseudoElement { None, Before, After }

internal sealed class CssSelector(CssCompound[] parts, CssSpecificity specificity, int offset, bool structural,
    CssPseudoElement pseudoElement = CssPseudoElement.None, bool siblings = false, int nestingCount = 0)
{
    // An+B values beyond this fail: Chrome stops matching some valid selectors near the 32-bit
    // limits (for example -n+2147483647), so larger values cannot follow the browser.
    internal const long MaxNthValue = 1_000_000_000;
    private const int MaxNotDepth = 32;

    private static bool IsSymbol(CssToken token, string value)
        => token.Kind == CssTokenKind.Symbol && token.Is(value);

    internal CssCompound Rightmost => parts[^1];
    /// <summary>How many nesting selectors (&amp;, written or implied) the selector holds before it is
    /// resolved against its parent rule; zero for an ordinary selector.</summary>
    internal int NestingCount => nestingCount;
    internal CssCompound[] Parts => parts;
    /// <summary>Whether the selector holds a next-sibling or subsequent-sibling combinator.</summary>
    internal bool HasSiblings => siblings;

    /// <summary>The parent a top-level &amp; stands for: the root element (:scope in a stylesheet).</summary>
    internal static readonly CssSelector RootParent = new(
        [new(null, [], [], [], [new CssPseudoClass(CssPseudoKind.Root)], CssCombinator.Descendant)],
        new CssSpecificity(0, 1, 0), 0, false);
    internal CssSpecificity Specificity => specificity;
    internal int Offset => offset;
    /// <summary>Whether matching reads an element's position among its siblings or its emptiness.</summary>
    internal bool UsesStructure => structural;
    internal CssPseudoElement PseudoElement => pseudoElement;

    internal static CssSelector Parse(List<CssToken> tokens, ref int i, CssBudget budget, bool nested = false)
    {
        var offset = tokens[i].Offset; budget.Selector(offset);
        var parts = new List<CssCompound>(); var specificity = default(CssSpecificity); var combinator = CssCombinator.Descendant; var structural = false;
        var siblings = false; var nestingCount = 0;
        // Inside a style rule a selector may begin with a combinator, which then relates the rule's own
        // element (the nesting selector, implied here) to the compound that follows.
        if (nested && (IsSymbol(tokens[i], ">") || IsSymbol(tokens[i], "+") || IsSymbol(tokens[i], "~")))
        {
            parts.Add(new(null, [], [], [], [], CssCombinator.Descendant, Nesting: true));
            nestingCount++;
            combinator = tokens[i].Text == ">" ? CssCombinator.Child : tokens[i].Text == "+" ? CssCombinator.NextSibling : CssCombinator.SubsequentSibling;
            siblings = combinator != CssCombinator.Child;
            i++;
        }
        var pseudo = CssPseudoElement.None;
        while (true)
        {
            budget.Token.ThrowIfCancellationRequested();
            CssBudget.Check(parts.Count < 32, "A selector may contain at most 32 compounds.", offset);
            var compound = ParseCompound(tokens, ref i, budget, offset, combinator, 0, ref specificity, ref structural);
            if (compound.Nesting) nestingCount++;
            parts.Add(compound);
            // ::before and ::after (or the legacy :before and :after) end the selector with the
            // specificity of a type selector; any other pseudo-element fails in ParseCompound.
            if (IsSymbol(tokens[i], ":") && PseudoElementAt(tokens, i) is var element and not CssPseudoElement.None)
            {
                var colon = tokens[i].Offset;
                pseudo = element;
                i += IsSymbol(tokens[i + 1], ":") ? 3 : 2;
                specificity = specificity.Add(new(0, 0, 1));
                if (!(IsSymbol(tokens[i], ",") || IsSymbol(tokens[i], "{")))
                    throw CssTokens.Invalid("A ::before or ::after pseudo-element must end its selector.", colon);
            }
            if (IsSymbol(tokens[i], ",") || IsSymbol(tokens[i], "{")) break;
            if (tokens[i].Kind == CssTokenKind.End) throw CssTokens.Invalid("Expected a CSS declaration block.", tokens[i].Offset);
            if (IsSymbol(tokens[i], ">")) { combinator = CssCombinator.Child; i++; }
            else if (IsSymbol(tokens[i], "+")) { combinator = CssCombinator.NextSibling; siblings = true; i++; }
            else if (IsSymbol(tokens[i], "~")) { combinator = CssCombinator.SubsequentSibling; siblings = true; i++; }
            else if (tokens[i].LeadingSpace) combinator = CssCombinator.Descendant;
            else throw CssTokens.Invalid("Unsupported or malformed selector/combinator.", tokens[i].Offset);
        }
        // A relative selector without a nesting selector is a descendant of the parent rule's element.
        if (nested && nestingCount == 0)
        {
            parts.Insert(0, new(null, [], [], [], [], CssCombinator.Descendant, Nesting: true));
            nestingCount++;
        }
        return new(parts.ToArray(), specificity, offset, structural || siblings, pseudo, siblings, nestingCount);
    }

    // The ::before or ::after (or :before or :after) starting at the colon tokens[i], or None.
    private static CssPseudoElement PseudoElementAt(List<CssToken> tokens, int i)
    {
        var at = i + 1;
        if (IsSymbol(tokens[at], ":") && !tokens[at].LeadingSpace) at++;
        if (tokens[at].Kind != CssTokenKind.Identifier || tokens[at].LeadingSpace) return CssPseudoElement.None;
        return tokens[at].Text.ToLowerInvariant() switch
        {
            "before" => CssPseudoElement.Before,
            "after" => CssPseudoElement.After,
            _ => CssPseudoElement.None
        };
    }

    private static CssCompound ParseCompound(List<CssToken> tokens, ref int i, CssBudget budget, int offset,
        CssCombinator combinator, int depth, ref CssSpecificity specificity, ref bool structural)
    {
        string? type = null; var ids = new List<string>(); var classes = new List<string>();
        var attrs = new List<CssAttributeMatch>(); List<CssPseudoClass>? pseudos = null; var simple = 0;
        var idCount = 0; var classCount = 0; var typeCount = 0;
        var nesting = false;
        if (tokens[i].Kind == CssTokenKind.Identifier) { type = tokens[i++].Text.ToLowerInvariant(); simple++; typeCount++; }
        else if (IsSymbol(tokens[i], "*")) { i++; simple++; }
        else if (IsSymbol(tokens[i], "&"))
        {
            if (depth > 0) throw CssTokens.Invalid("The nesting selector cannot appear inside :not().", tokens[i].Offset);
            nesting = true; i++; simple++;
        }
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
                var supported = name is "href" or "src" or "alt" or "hidden"
                    // id, class, title, lang, data-*, role, aria-* and the other global attributes
                    // that print nothing themselves but can be selected.
                    || HtmlGlobalAttributes.NoEffect(name) && name != "data-"
                    // A custom element takes any attribute (the page's own, such as variant or open).
                    || type is not null && HtmlDocumentReader.IsCustomElement(type);
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
            else if (IsSymbol(current, ":"))
            {
                // A ::before or ::after ends the compound; Parse takes it (never inside :not()).
                if (depth == 0 && PseudoElementAt(tokens, i) != CssPseudoElement.None)
                {
                    if (simple == 0) simple++;
                    break;
                }
                i++;
                var name = tokens[i];
                if (name.LeadingSpace)
                    throw CssTokens.Invalid("Expected a pseudo-class name adjacent to ':'.", current.Offset);
                if (IsSymbol(name, ":"))
                    throw CssTokens.Invalid("Pseudo-elements are not supported.", current.Offset);
                var weight = new CssSpecificity(0, 1, 0);
                (pseudos ??= []).Add(ParsePseudoClass(name, budget, offset, current.Offset, depth, ref weight, ref structural));
                i++;
                idCount += weight.Ids; classCount += weight.Classes; typeCount += weight.Types;
            }
            else break;
            CssBudget.Check(++simple <= 32, "A compound selector may contain at most 32 simple terms.", offset);
        }
        if (simple == 0) throw CssTokens.Invalid("Expected a supported selector.", tokens[i].Offset);
        specificity = specificity.Add(new(idCount, classCount, typeCount));
        if (nesting && type is not null) throw CssTokens.Invalid("A type selector cannot be combined with the nesting selector.", offset);
        return new(type, ids.ToArray(), classes.ToArray(), attrs.ToArray(), pseudos?.ToArray() ?? [], combinator, nesting);
    }

    // weight starts as one pseudo-class (0,1,0); :not() replaces it with its most specific argument.
    private static CssPseudoClass ParsePseudoClass(CssToken name, CssBudget budget, int offset, int colon, int depth,
        ref CssSpecificity weight, ref bool structural)
    {
        if (name.Kind == CssTokenKind.Identifier)
        {
            var simple = Keyword(name.Text, "first-child") ? new CssPseudoClass(CssPseudoKind.NthChild, 0, 1)
                : Keyword(name.Text, "last-child") ? new CssPseudoClass(CssPseudoKind.NthLastChild, 0, 1)
                : Keyword(name.Text, "only-child") ? new CssPseudoClass(CssPseudoKind.OnlyChild)
                : Keyword(name.Text, "first-of-type") ? new CssPseudoClass(CssPseudoKind.NthOfType, 0, 1)
                : Keyword(name.Text, "last-of-type") ? new CssPseudoClass(CssPseudoKind.NthLastOfType, 0, 1)
                : Keyword(name.Text, "only-of-type") ? new CssPseudoClass(CssPseudoKind.OnlyOfType)
                : Keyword(name.Text, "empty") ? new CssPseudoClass(CssPseudoKind.Empty)
                : Keyword(name.Text, "root") ? new CssPseudoClass(CssPseudoKind.Root)
                : throw CssTokens.Invalid($"Unsupported pseudo-class ':{name.Text}'.", colon);
            // :root needs only the element's place in the ancestor path, not sibling positions.
            structural |= simple.Kind != CssPseudoKind.Root;
            return simple;
        }
        if (name.Kind != CssTokenKind.Function)
            throw CssTokens.Invalid("Expected a pseudo-class name after ':'.", colon);
        CssPseudoKind? nth = Keyword(name.Text, "nth-child") ? CssPseudoKind.NthChild
            : Keyword(name.Text, "nth-last-child") ? CssPseudoKind.NthLastChild
            : Keyword(name.Text, "nth-of-type") ? CssPseudoKind.NthOfType
            : Keyword(name.Text, "nth-last-of-type") ? CssPseudoKind.NthLastOfType
            : null;
        if (nth is { } nthKind)
        {
            var (a, b) = ParseNth(name.Arguments!, name.Offset);
            structural = true;
            return new(nthKind, a, b);
        }
        if (Keyword(name.Text, "lang")) return new(CssPseudoKind.Lang, Language: ParseLanguageRange(name.Arguments!, name.Offset));
        if (!Keyword(name.Text, "not"))
            throw CssTokens.Invalid($"Unsupported pseudo-class ':{name.Text}()'.", colon);
        CssBudget.Check(depth < MaxNotDepth, ":not() may nest at most 32 levels.", name.Offset);
        // A list of compound selectors. Chrome also accepts complex selectors here; they fail.
        var tokens = new List<CssToken>();
        var trailing = CssSyntaxAdapter.SelectorTokens(name.Arguments!, budget, tokens);
        tokens.Add(new(CssTokenKind.End, "", trailing, name.Offset));
        var compounds = new List<CssCompound>();
        var best = default(CssSpecificity);
        for (var i = 0; ;)
        {
            if (tokens[i].Kind == CssTokenKind.End)
                throw CssTokens.Invalid(":not() requires a selector before and after every comma.", name.Offset);
            CssBudget.Check(compounds.Count < 32, ":not() may contain at most 32 selectors.", name.Offset);
            var argument = default(CssSpecificity);
            compounds.Add(ParseCompound(tokens, ref i, budget, offset, CssCombinator.Descendant, depth + 1, ref argument, ref structural));
            if (argument.CompareTo(best) > 0) best = argument;
            if (tokens[i].Kind == CssTokenKind.End) break;
            if (IsSymbol(tokens[i], ",")) { i++; continue; }
            if (IsSymbol(tokens[i], ">") || IsSymbol(tokens[i], "+") || IsSymbol(tokens[i], "~") || tokens[i].LeadingSpace)
                throw CssTokens.Invalid("Only compound selectors are supported inside :not(); combinators are not.", tokens[i].Offset);
            throw CssTokens.Invalid("Malformed selector inside :not().", tokens[i].Offset);
        }
        weight = best;
        return new(CssPseudoKind.Not, Not: compounds.ToArray());
    }

    // :lang() with one language range that is a primary language subtag: 1 to 8 ASCII letters, as an
    // identifier or a string. For such a range basic and extended filtering (the two matchings
    // browsers have used) agree, so a match does not depend on which one Chrome applies; a range
    // with subtags, a wildcard or a list fails instead.
    private static string ParseLanguageRange(IReadOnlyList<CssSyntaxComponent> arguments, int offset)
    {
        const string Unsupported = ":lang() supports one primary language subtag (1 to 8 ASCII letters), such as :lang(en) or :lang(de).";
        string? range = null;
        foreach (var argument in arguments)
        {
            if (argument is not CssTokenComponent { Token: var token }) throw CssTokens.Invalid(Unsupported, argument.Span.Start);
            if (token.Kind == CssSyntaxTokenKind.Whitespace) continue;
            if (range is not null || token.Kind is not (CssSyntaxTokenKind.Ident or CssSyntaxTokenKind.String))
                throw CssTokens.Invalid(Unsupported, token.Span.Start);
            range = token.Value;
        }
        if (range is null || range.Length is 0 or > 8 || !range.All(char.IsAsciiLetter)) throw CssTokens.Invalid(Unsupported, offset);
        return range;
    }

    /// <summary>Whether an element's language matches a primary-subtag range: the language is the
    /// range or starts with it and a hyphen, ASCII case-insensitively. An empty lang attribute means
    /// the language is unknown, which no range matches.</summary>
    internal static bool LanguageMatches(string? language, string range) =>
        language is not null && language.StartsWith(range, StringComparison.OrdinalIgnoreCase)
        && (language.Length == range.Length || language[range.Length] == '-');

    /// <summary>The language of the element at <paramref name="depth"/> of the ancestor path (or of a
    /// sibling of it standing in for that entry): its own lang attribute, else the nearest
    /// ancestor's; null when no element sets one.</summary>
    internal static string? Language(in CssElement element, IReadOnlyList<CssElement> path, int depth)
    {
        if (element.Attributes.TryGetValue("lang", out var own)) return own;
        for (var d = Math.Min(depth, path.Count) - 1; d >= 0; d--)
            if (path[d].Attributes.TryGetValue("lang", out var inherited)) return inherited;
        return null;
    }

    // Pseudo-class and An+B keywords are ASCII case-insensitive; no other case mapping applies.
    private static bool Keyword(string text, string lower)
    {
        if (text.Length != lower.Length) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is >= 'A' and <= 'Z') c = (char)(c + 32);
            if (c != lower[i]) return false;
        }
        return true;
    }

    // The An+B microsyntax of CSS Syntax 3: odd, even, B, An and An+B with the sign and
    // whitespace rules of its token grammar. At most four tokens are read; "of S" is unsupported.
    private static (long A, long B) ParseNth(IReadOnlyList<CssSyntaxComponent> arguments, int offset)
    {
        const string Malformed = "Malformed An+B argument.";
        Span<int> found = stackalloc int[4];
        Span<bool> spaced = stackalloc bool[4];
        var count = 0; var space = false;
        for (var k = 0; k < arguments.Count; k++)
        {
            if (arguments[k] is not CssTokenComponent { Token: var token })
                throw CssTokens.Invalid(Malformed, arguments[k].Span.Start);
            if (token.Kind == CssSyntaxTokenKind.Whitespace) { space = true; continue; }
            if (token.Kind == CssSyntaxTokenKind.Ident && Keyword(token.Value, "of"))
                throw CssTokens.Invalid("The \"of S\" form of the nth pseudo-classes is not supported.", token.Span.Start);
            if (count == found.Length) throw CssTokens.Invalid(Malformed, token.Span.Start);
            found[count] = k; spaced[count++] = space; space = false;
        }
        if (count == 0) throw CssTokens.Invalid("An+B argument is empty.", offset);
        var first = TokenAt(arguments, found[0]);
        long a; string form; var next = 1;
        switch (first.Kind)
        {
            case CssSyntaxTokenKind.Ident when count == 1 && Keyword(first.Value, "odd"): return (2, 1);
            case CssSyntaxTokenKind.Ident when count == 1 && Keyword(first.Value, "even"): return (2, 0);
            case CssSyntaxTokenKind.Ident:
                a = first.Value.StartsWith('-') ? -1 : 1;
                form = a < 0 ? first.Value[1..] : first.Value;
                break;
            case CssSyntaxTokenKind.Delim when first.Value == "+" && count > 1 && !spaced[1]
                && TokenAt(arguments, found[1]) is { Kind: CssSyntaxTokenKind.Ident } plus && !plus.Value.StartsWith('-'):
                a = 1; form = plus.Value; next = 2;
                break;
            case CssSyntaxTokenKind.Number when first.IsInteger && count == 1:
                return (0, Integer(first.Value, first.Span.Start));
            case CssSyntaxTokenKind.Dimension when first.IsInteger:
                a = Integer(first.Value, first.Span.Start); form = first.Unit!;
                break;
            default: throw CssTokens.Invalid(Malformed, first.Span.Start);
        }
        if (form.Length == 0 || form[0] is not ('n' or 'N')) throw CssTokens.Invalid(Malformed, first.Span.Start);
        var rest = count - next;
        if (form.Length == 1)
        {
            if (rest == 0) return (a, 0);
            var b = TokenAt(arguments, found[next]);
            if (rest == 1 && IsInteger(b, signed: true)) return (a, Integer(b.Value, b.Span.Start));
            if (rest == 2 && b.Kind == CssSyntaxTokenKind.Delim && b.Value is "+" or "-"
                && TokenAt(arguments, found[next + 1]) is var magnitude && IsInteger(magnitude, signed: false))
            {
                var value = Integer(magnitude.Value, magnitude.Span.Start);
                return (a, b.Value == "-" ? -value : value);
            }
            throw CssTokens.Invalid(Malformed, b.Span.Start);
        }
        if (form[1] != '-') throw CssTokens.Invalid(Malformed, first.Span.Start);
        if (form.Length == 2)
        {
            if (rest == 1 && TokenAt(arguments, found[next]) is var b && IsInteger(b, signed: false))
                return (a, -Integer(b.Value, b.Span.Start));
            throw CssTokens.Invalid(Malformed, first.Span.Start);
        }
        for (var k = 2; k < form.Length; k++)
            if (!char.IsAsciiDigit(form[k])) throw CssTokens.Invalid(Malformed, first.Span.Start);
        if (rest != 0) throw CssTokens.Invalid(Malformed, TokenAt(arguments, found[next]).Span.Start);
        return (a, -Integer(form[2..], first.Span.Start));
    }

    private static CssSyntaxToken TokenAt(IReadOnlyList<CssSyntaxComponent> arguments, int index)
        => ((CssTokenComponent)arguments[index]).Token;

    private static bool IsInteger(CssSyntaxToken token, bool signed)
        => token.Kind == CssSyntaxTokenKind.Number && token.IsInteger && (token.Value[0] is '+' or '-') == signed;

    // An optionally signed run of ASCII digits; reading stops as soon as the value passes the bound.
    private static long Integer(string text, int offset)
    {
        var negative = text[0] == '-';
        var k = text[0] is '+' or '-' ? 1 : 0;
        long value = 0;
        for (; k < text.Length; k++)
        {
            if (!char.IsAsciiDigit(text[k])) throw CssTokens.Invalid("Malformed An+B argument.", offset);
            value = value * 10 + (text[k] - '0');
            if (value > MaxNthValue)
                throw CssTokens.Invalid("An+B values beyond ±1000000000 are not supported.", offset);
        }
        return negative ? -value : value;
    }

    /// <summary>
    /// The selectors a nested rule stands for under its parent rule. The nesting selector means the
    /// parent's selector list (:is(), whose specificity is that of its most specific selector): each
    /// resolved selector replaces it with one parent selector, merging it into the compound it is
    /// written in. A parent selector of several compounds can replace the nesting selector only in the
    /// first compound of the nested selector (and then the parent's own combinators come first); any
    /// other place would need :is() to match a complex selector and fails.
    /// </summary>
    internal static CssSelector[] ResolveNesting(CssSelector[] nested, CssSelector parent, CssBudget budget, int offset)
        => ResolveNesting(nested, [parent], budget, offset);

    internal static CssSelector[] ResolveNesting(CssSelector[] nested, IReadOnlyList<CssSelector> parents, CssBudget budget, int offset)
    {
        var result = new List<CssSelector>();
        var maxParent = default(CssSpecificity);
        foreach (var parent in parents)
        {
            if (parent.PseudoElement != CssPseudoElement.None)
                throw new FactsPdfException("FPDF1203", "A nested rule cannot be written inside a rule whose selector ends in a pseudo-element.", offset);
            if (parent.Specificity.CompareTo(maxParent) > 0) maxParent = parent.Specificity;
        }
        foreach (var selector in nested)
        {
            budget.Selector(selector.Offset);
            if (selector.NestingCount == 0) { result.Add(selector); continue; }
            var combinations = 1L;
            for (var k = 0; k < selector.NestingCount; k++)
            {
                combinations *= parents.Count;
                CssBudget.Check(combinations <= 64, "A nested selector with several & expands to at most 64 selectors.", selector.Offset);
            }
            for (var combination = 0L; combination < combinations; combination++)
            {
                var rest = combination;
                var parts = new List<CssCompound>(selector.Parts.Length + 4);
                var structural = selector.UsesStructure; var siblings = selector.HasSiblings;
                foreach (var part in selector.Parts)
                {
                    if (!part.Nesting) { parts.Add(part); continue; }
                    var parent = parents[(int)(rest % parents.Count)];
                    rest /= parents.Count;
                    structural |= parent.UsesStructure; siblings |= parent.HasSiblings;
                    if (parent.Parts.Length > 1)
                    {
                        if (parts.Count != 0)
                            throw new FactsPdfException("FPDF1203",
                                "The nesting selector can stand for a selector with combinators only at the start of the nested selector.", selector.Offset);
                        for (var p = 0; p < parent.Parts.Length - 1; p++) parts.Add(parent.Parts[p]);
                    }
                    var last = parent.Parts[^1];
                    parts.Add(Merge(last, part, parent.Parts.Length > 1 ? last.Combinator : part.Combinator, selector.Offset));
                }
                var specificity = selector.Specificity;
                for (var k = 0; k < selector.NestingCount; k++) specificity = specificity.Add(maxParent);
                result.Add(new(parts.ToArray(), specificity, selector.Offset, structural, selector.PseudoElement, siblings, 0));
            }
        }
        return result.ToArray();
    }

    // The compound a nesting selector written inside `own` becomes when it stands for `parent`.
    private static CssCompound Merge(CssCompound parent, CssCompound own, CssCombinator combinator, int offset)
    {
        if (parent.Type is not null && own.Type is not null && !parent.Type.Equals(own.Type, StringComparison.Ordinal))
            throw new FactsPdfException("FPDF1203", "A nesting selector merged with a different type selector never matches.", offset);
        return new(parent.Type ?? own.Type, [.. parent.Ids, .. own.Ids], [.. parent.Classes, .. own.Classes],
            [.. parent.Attributes, .. own.Attributes], [.. parent.PseudoClasses, .. own.PseudoClasses], combinator);
    }

    internal bool Matches(IReadOnlyList<CssElement> path, CssBudget budget, HtmlElementPositions? positions = null)
    {
        if (siblings) return MatchesWithSiblings(path, budget, positions);
        if (path.Count == 0 || !MatchesCompound(parts[^1], path[^1], path.Count == 1, budget, positions, path, path.Count - 1)) return false;
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
                var reachable = parts[p + 1].Combinator == CssCombinator.Child ? candidates[n + 1] : below;
                if (reachable && MatchesCompound(parts[p], path[n], n == 0, budget, positions, path, n)) { next[n] = true; any = true; }
            }
            if (!any) return false;
            next.CopyTo(candidates);
        }
        return true;
    }
    // A selector with a sibling combinator matches from the right through nodes of the element tree: a
    // node is an element of the path or a sibling of one (same ancestors, an earlier position), found
    // through the document's element positions. A (compound, node) pair that failed once fails
    // again whatever reached it, so each pair is tried once: the work is at most the compounds
    // times the elements near the path, every step charged to the match budget.
    private bool MatchesWithSiblings(IReadOnlyList<CssElement> path, CssBudget budget, HtmlElementPositions? positions)
    {
        if (path.Count == 0 || !MatchesCompound(parts[^1], path[^1], path.Count == 1, budget, positions, path, path.Count - 1)) return false;
        if (parts.Length == 1) return true;
        if (positions is null || path[^1].Token < 0)
            throw new InvalidOperationException("Sibling combinators need the document's element positions.");
        HashSet<long>? failed = null;
        bool Reach(int part, int depth, int node)
        {
            var key = ((long)part << 32) | (uint)node;
            if (failed is not null && failed.Contains(key)) return false;
            budget.Work(offset);
            var element = depth < path.Count && path[depth].Token == node ? path[depth] : positions.Element(node);
            var found = MatchesCompound(parts[part], element, depth == 0 && path[0].Token == node, budget, positions, path, depth)
                && (part == 0 || Related(part, depth, node));
            if (!found) (failed ??= []).Add(key);
            return found;
        }
        // Whether the compound before parts[part] matches an element related to the node.
        bool Related(int part, int depth, int node)
        {
            switch (parts[part].Combinator)
            {
                case CssCombinator.Child:
                    return depth > 0 && Reach(part - 1, depth - 1, path[depth - 1].Token);
                case CssCombinator.Descendant:
                    for (var d = depth - 1; d >= 0; d--)
                        if (Reach(part - 1, d, path[d].Token)) return true;
                    return false;
                case CssCombinator.NextSibling:
                    return positions.PreviousSibling(node) is >= 0 and var previous && Reach(part - 1, depth, previous);
                default:
                    for (var sibling = positions.PreviousSibling(node); sibling >= 0; sibling = positions.PreviousSibling(sibling))
                        if (Reach(part - 1, depth, sibling)) return true;
                    return false;
            }
        }
        return Related(parts.Length - 1, path.Count - 1, path[^1].Token);
    }

    // first: the element is the first entry of the ancestor path, the document's root element.
    // depth: the path entry the element is (or, for a sibling, stands beside), for its ancestors.
    private bool MatchesCompound(CssCompound part, in CssElement element, bool first, CssBudget budget, HtmlElementPositions? positions,
        IReadOnlyList<CssElement> path, int depth)
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
        foreach (var pseudo in part.PseudoClasses)
            if (!MatchesPseudoClass(pseudo, element, first, budget, positions, path, depth)) return false;
        return true;
    }

    private bool MatchesPseudoClass(CssPseudoClass pseudo, in CssElement element, bool first, CssBudget budget, HtmlElementPositions? positions,
        IReadOnlyList<CssElement> path, int depth)
    {
        budget.Work(offset);
        // :root is the html element with no parent: the first entry of the ancestor path, which the
        // reader always fills with html (implied by the HTML parser when the markup omits it).
        if (pseudo.Kind == CssPseudoKind.Root) return first && element.Name.Equals("html", StringComparison.Ordinal);
        if (pseudo.Kind == CssPseudoKind.Not)
        {
            foreach (var compound in pseudo.Not!)
                if (MatchesCompound(compound, element, first, budget, positions, path, depth)) return false;
            return true;
        }
        if (pseudo.Kind == CssPseudoKind.Lang) return LanguageMatches(Language(element, path, depth), pseudo.Language!);
        if (positions is null)
            throw new InvalidOperationException("Structural pseudo-classes need the document's element positions.");
        var position = positions[element.Token];
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
