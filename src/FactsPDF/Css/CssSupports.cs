using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// The condition of an <c>@supports</c> rule (CSS Conditional 3 and 4): <c>not</c>, <c>and</c>,
/// <c>or</c>, nested parentheses, declarations and <c>selector()</c>. Chrome answers a declaration
/// test by its own grammar, so the answer here must be Chrome's: a declaration the specifications
/// prove invalid (<see cref="CssSpecValidity"/>) is false; a declaration FactsPDF compiles is valid
/// CSS (the renderer accepts only valid CSS) and is true; a custom property or a value holding
/// <c>var()</c> is true, as it is valid when parsed. A declaration that is neither proven invalid nor
/// compiled may be valid in Chrome, so its answer is unknown and the rule fails <c>FPDF1204</c> in
/// both modes rather than guess. <c>selector()</c> is decided the same way. A general-enclosed
/// condition, <c>font-tech()</c> and <c>font-format()</c> also fail <c>FPDF1204</c>.
/// </summary>
internal static class CssSupports
{
    private const int MaxTerms = 512;

    /// <summary>A prelude the grammar does not allow (a browser drops the whole rule).</summary>
    internal sealed class MalformedException(string message) : Exception(message);

    /// <summary>Decides a declaration test: true, false, or null when the answer is unknown.</summary>
    internal delegate bool? DeclarationTest(CssDeclarationNode declaration);

    /// <summary>Decides a selector() test: true, false, or null when the answer is unknown.</summary>
    internal delegate bool? SelectorTest(IReadOnlyList<CssSyntaxComponent> selector, int offset);

    /// <summary>Evaluates an <c>@supports</c> prelude; throws <see cref="MalformedException"/> for a malformed one.</summary>
    internal static bool Evaluate(IReadOnlyList<CssSyntaxComponent> prelude, DeclarationTest declaration, SelectorTest selector, int offset)
    {
        var items = NonWhitespace(prelude);
        if (items.Count == 0) throw new MalformedException("@supports needs a condition.");
        if (items.Count > MaxTerms) throw new MalformedException("The @supports condition is too long.");
        var terms = items.Count;
        var i = 0;
        var result = Condition(items, ref i, declaration, selector, offset, ref terms);
        if (i != items.Count) throw new MalformedException("The @supports condition has extra terms.");
        return result;
    }

    private static List<CssSyntaxComponent> NonWhitespace(IReadOnlyList<CssSyntaxComponent> values)
    {
        var items = new List<CssSyntaxComponent>(values.Count);
        foreach (var value in values)
            if (value is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) items.Add(value);
        return items;
    }

    private static bool Keyword(CssSyntaxComponent item, string keyword)
        => item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } && token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    private static bool Condition(List<CssSyntaxComponent> items, ref int i, DeclarationTest declaration, SelectorTest selector, int offset, ref int terms)
    {
        if (Keyword(items[i], "not"))
        {
            i++;
            if (i >= items.Count) throw new MalformedException("The @supports condition ends after 'not'.");
            return !InParens(items[i++], declaration, selector, offset, ref terms);
        }
        var result = InParens(items[i++], declaration, selector, offset, ref terms);
        string? joiner = null;
        while (i < items.Count && (Keyword(items[i], "and") || Keyword(items[i], "or")))
        {
            var word = ((CssTokenComponent)items[i]).Token.Value.ToLowerInvariant();
            if (joiner is not null && joiner != word) throw new MalformedException("An @supports condition mixes 'and' and 'or' without parentheses.");
            joiner = word;
            i++;
            if (i >= items.Count) throw new MalformedException($"The @supports condition ends after '{word}'.");
            // Every operand is evaluated, so an unknown test fails wherever it is written.
            var next = InParens(items[i++], declaration, selector, offset, ref terms);
            result = word == "and" ? result & next : result | next;
        }
        return result;
    }

    private static bool InParens(CssSyntaxComponent item, DeclarationTest declaration, SelectorTest selector, int offset, ref int terms)
    {
        if (item is CssFunctionComponent function)
        {
            if (function.Name.Equals("selector", StringComparison.OrdinalIgnoreCase))
            {
                var list = NonWhitespace(function.Values);
                // selector() takes one complex selector: a list is not one.
                if (list.Count == 0 || list.Any(x => x is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma })) return false;
                return selector(function.Values, function.Span.Start)
                    ?? throw new FactsPdfException("FPDF1204", "@supports selector(...) names a selector FactsPDF cannot decide Chrome's support for.", offset);
            }
            throw new FactsPdfException("FPDF1204", $"@supports {function.Name}(...) is not supported.", offset);
        }
        if (item is not CssBlockComponent { Opening: CssSyntaxTokenKind.OpenParen } block)
            throw new MalformedException("An @supports condition needs parentheses.");
        var content = NonWhitespace(block.Values);
        if ((terms += content.Count) > MaxTerms) throw new MalformedException("The @supports condition is too long.");
        if (content.Count == 0)
            throw new FactsPdfException("FPDF1204", "The empty general-enclosed @supports condition () is not supported.", offset);
        if (Keyword(content[0], "not") || content[0] is CssBlockComponent { Opening: CssSyntaxTokenKind.OpenParen }
            || content[0] is CssFunctionComponent && content.Count > 1)
        {
            var i = 0;
            var nested = Condition(content, ref i, declaration, selector, offset, ref terms);
            if (i != content.Count) throw new MalformedException("A nested @supports condition has extra terms.");
            return nested;
        }
        if (content.Count == 1 && content[0] is CssFunctionComponent)
            return InParens(content[0], declaration, selector, offset, ref terms);
        if (content.Count < 2 || content[0] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } name }
            || content[1] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon })
            throw new FactsPdfException("FPDF1204", "A general-enclosed @supports condition (not a declaration) is not supported.", offset);
        // The declaration: value components between the colon and an optional trailing !important.
        var values = new List<CssSyntaxComponent>();
        var seenColon = false;
        foreach (var value in block.Values)
        {
            if (!seenColon) { seenColon = value is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon }; continue; }
            values.Add(value);
        }
        var important = false;
        var tail = NonWhitespace(values);
        if (tail.Count >= 2 && tail[^2] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "!" } }
            && Keyword(tail[^1], "important"))
        {
            important = true;
            var bang = values.LastIndexOf(tail[^2]);
            values.RemoveRange(bang, values.Count - bang);
        }
        while (values.Count > 0 && values[0] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) values.RemoveAt(0);
        while (values.Count > 0 && values[^1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) values.RemoveAt(values.Count - 1);
        return declaration(new CssDeclarationNode(name.Value, values, important, block.Span))
            ?? throw new FactsPdfException("FPDF1204",
                $"@supports ({name.Value}: ...) tests a value FactsPDF does not implement, so it cannot tell whether Chrome supports it.", offset);
    }
}
