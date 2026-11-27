using System.Text;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>What a custom property declaration (<c>--name: value</c>) says: its value as written, or one
/// of the CSS-wide keywords <c>inherit</c>, <c>initial</c> or <c>unset</c>.</summary>
internal sealed record CssCustomValue(CssSyntaxComponent[] Values, string? Keyword);

/// <summary>A declaration of a supported property whose value holds <c>var()</c>: it is compiled for each
/// element after substitution, with the host selectors or style-attribute host it was written for.</summary>
internal sealed record CssPendingValue(CssDeclarationNode Node, CssSelector[]? HostSelectors, bool Inline);

/// <summary>A custom property's computed value: its tokens with every <c>var()</c> substituted, and how many
/// syntax nodes they hold.</summary>
internal sealed record CssVariableValue(CssSyntaxComponent[] Values, int Nodes);

/// <summary>
/// The computed custom properties of one element. Immutable once built; an element that declares no
/// custom property shares its parent's environment. A name that is absent has the guaranteed-invalid
/// value (never declared, <c>initial</c>, part of a reference cycle, or an invalid substitution).
/// </summary>
internal sealed class CssCustomEnvironment
{
    internal static readonly CssCustomEnvironment Empty = new(new Dictionary<string, CssVariableValue>(StringComparer.Ordinal));
    private readonly Dictionary<string, CssVariableValue> values;
    private CssCustomEnvironment(Dictionary<string, CssVariableValue> values) => this.values = values;
    internal bool TryGet(string name, out CssVariableValue value) => values.TryGetValue(name, out value!);
    internal static CssCustomEnvironment From(Dictionary<string, CssVariableValue> values) => new(values);
    internal Dictionary<string, CssVariableValue> Copy() => new(values, StringComparer.Ordinal);
}

/// <summary>
/// CSS Custom Properties Level 1: <c>var()</c> syntax checks at parse time, custom property resolution
/// in dependency order with cycle detection, and substitution into property values. Every component
/// placed by a substitution is charged to the CSS work budget, a substituted value may hold at most
/// <c>MaxCssSyntaxNodes</c> syntax nodes and a chain of references at most <c>MaxCssSyntaxDepth</c>
/// links (both FPDF1205), so repeated doubling (<c>--b: var(--a) var(--a)</c>) stays bounded.
/// </summary>
internal static class CssVariables
{
    internal static bool IsCustomPropertyName(string name) =>
        name.Length > 2 && name.StartsWith("--", StringComparison.Ordinal);

    private static bool IsVar(CssFunctionComponent function) =>
        function.Name.Equals("var", StringComparison.OrdinalIgnoreCase);

    private static bool IsWhitespace(CssSyntaxComponent component) =>
        component is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace };

    internal static bool ContainsVar(IReadOnlyList<CssSyntaxComponent> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            switch (values[i])
            {
                case CssFunctionComponent function when IsVar(function) || ContainsVar(function.Values): return true;
                case CssBlockComponent block when ContainsVar(block.Values): return true;
            }
        }
        return false;
    }

    /// <summary>Fails FPDF1202 at the first <c>var()</c> that is not <c>var(--name)</c> or
    /// <c>var(--name, fallback)</c>, including inside fallbacks, functions and blocks.</summary>
    internal static void ValidateReferences(IReadOnlyList<CssSyntaxComponent> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            switch (values[i])
            {
                case CssFunctionComponent function when IsVar(function):
                    var at = Reference(function, out _, out var fallback);
                    if (at < 0)
                        throw new FactsPdfException("FPDF1202",
                            "var() takes a custom property name (--name) and an optional fallback after a comma.",
                            function.Span.Start);
                    if (fallback >= 0) ValidateReferences(Slice(function.Values, fallback));
                    break;
                case CssFunctionComponent function:
                    ValidateReferences(function.Values);
                    break;
                case CssBlockComponent block:
                    ValidateReferences(block.Values);
                    break;
            }
        }
    }

    // The index of the name token inside var(), or -1 when malformed; fallback is the index of the
    // first component after the comma, or -1 without a comma.
    private static int Reference(CssFunctionComponent function, out string name, out int fallback)
    {
        name = ""; fallback = -1;
        var values = function.Values;
        var i = 0;
        while (i < values.Count && IsWhitespace(values[i])) i++;
        if (i == values.Count || values[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token }
            || !IsCustomPropertyName(token.Value))
            return -1;
        var at = i++;
        name = token.Value;
        while (i < values.Count && IsWhitespace(values[i])) i++;
        if (i == values.Count) return at;
        if (values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) return -1;
        fallback = i + 1;
        return at;
    }

    private static CssSyntaxComponent[] Slice(IReadOnlyList<CssSyntaxComponent> values, int start)
    {
        var end = values.Count;
        while (start < end && IsWhitespace(values[start])) start++;
        while (end > start && IsWhitespace(values[end - 1])) end--;
        var slice = new CssSyntaxComponent[end - start];
        for (var i = start; i < end; i++) slice[i - start] = values[i];
        return slice;
    }

    /// <summary>The CSS-wide keyword a custom property declares, or null; <c>revert</c> and
    /// <c>revert-layer</c> fail FPDF1202 like elsewhere in the renderer.</summary>
    internal static string? Keyword(IReadOnlyList<CssSyntaxComponent> values, int offset)
    {
        CssSyntaxToken? only = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (IsWhitespace(values[i])) continue;
            if (only is not null || values[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token }) return null;
            only = token;
        }
        if (only is not { } keyword) return null;
        var text = keyword.Value.ToLowerInvariant();
        if (text is "revert" or "revert-layer")
            throw new FactsPdfException("FPDF1202", $"CSS-wide keyword '{text}' is not supported on custom properties.", offset);
        return text is "inherit" or "initial" or "unset" ? text : null;
    }

    internal static int CountNodes(IReadOnlyList<CssSyntaxComponent> values)
    {
        var count = 0;
        for (var i = 0; i < values.Count; i++)
        {
            count++;
            switch (values[i])
            {
                case CssFunctionComponent function: count += CountNodes(function.Values); break;
                case CssBlockComponent block: count += CountNodes(block.Values); break;
            }
        }
        return count;
    }

    /// <summary>The CSS text of components, for messages: tokens as written, functions and blocks with
    /// their brackets, cut at <paramref name="limit"/> characters.</summary>
    internal static string Text(IReadOnlyList<CssSyntaxComponent> values, int limit = 120)
    {
        var text = new StringBuilder();
        Append(text, values, limit);
        return text.Length > limit ? text.ToString(0, limit) + "…" : text.ToString();
    }

    private static void Append(StringBuilder text, IReadOnlyList<CssSyntaxComponent> values, int limit)
    {
        for (var i = 0; i < values.Count && text.Length <= limit; i++)
        {
            switch (values[i])
            {
                case CssTokenComponent token:
                    text.Append(token.Token.Kind == CssSyntaxTokenKind.Whitespace ? " " : token.Token.Raw);
                    break;
                case CssFunctionComponent function:
                    text.Append(function.Name).Append('(');
                    Append(text, function.Values, limit);
                    text.Append(')');
                    break;
                case CssBlockComponent block:
                    var (open, close) = block.Opening switch
                    {
                        CssSyntaxTokenKind.OpenSquare => ('[', ']'),
                        CssSyntaxTokenKind.OpenBrace => ('{', '}'),
                        _ => ('(', ')')
                    };
                    text.Append(open);
                    Append(text, block.Values, limit);
                    text.Append(close);
                    break;
            }
        }
    }

    /// <summary>
    /// The computed custom properties of an element: its parent's, with the winning custom property
    /// declarations of the element applied and their <c>var()</c> references resolved in dependency order.
    /// The properties of a reference cycle, and any whose substitution fails, get the guaranteed-invalid
    /// value, as CSS Custom Properties Level 1 defines; that is never an error by itself.
    /// </summary>
    internal static CssCustomEnvironment Resolve(CssCustomEnvironment parent,
        IReadOnlyDictionary<string, (CssCustomValue Value, int Offset)> declared, CssBudget budget, CssSyntaxLimits limits)
    {
        var values = parent.Copy();
        var pending = new Dictionary<string, (CssCustomValue Value, int Offset)>(StringComparer.Ordinal);
        foreach (var (name, (value, offset)) in declared)
        {
            switch (value.Keyword)
            {
                case "inherit" or "unset": break;
                case "initial": values.Remove(name); break;
                default:
                    if (ContainsVar(value.Values)) { values.Remove(name); pending.Add(name, (value, offset)); }
                    else values[name] = new CssVariableValue(value.Values, CountNodes(value.Values));
                    break;
            }
        }
        if (pending.Count > 0)
            new Resolver(values, pending, budget, limits).ResolveAll();
        return CssCustomEnvironment.From(values);
    }

    private sealed class Resolver(Dictionary<string, CssVariableValue> values,
        Dictionary<string, (CssCustomValue Value, int Offset)> pending, CssBudget budget, CssSyntaxLimits limits)
    {
        private readonly HashSet<string> visiting = new(StringComparer.Ordinal);
        private readonly HashSet<string> done = new(StringComparer.Ordinal);
        private readonly List<string> stack = [];
        private readonly HashSet<string> cyclic = new(StringComparer.Ordinal);

        internal void ResolveAll()
        {
            // Sorted, so which cycle member is found first never depends on dictionary order.
            var names = pending.Keys.ToArray();
            Array.Sort(names, StringComparer.Ordinal);
            foreach (var name in names) Resolve(name);
        }

        private bool TryLookup(string name, out CssVariableValue value)
        {
            if (pending.ContainsKey(name))
            {
                if (visiting.Contains(name))
                {
                    // Every property on the stack from the first visit of name is in the cycle.
                    for (var i = stack.LastIndexOf(name); i < stack.Count; i++) cyclic.Add(stack[i]);
                    value = null!;
                    return false;
                }
                Resolve(name);
            }
            return values.TryGetValue(name, out value!);
        }

        private void Resolve(string name)
        {
            if (done.Contains(name)) return;
            var (declared, offset) = pending[name];
            CssBudget.Check(stack.Count < limits.MaxDepth,
                "CSS var() references nest deeper than MaxCssSyntaxDepth.", offset);
            visiting.Add(name); stack.Add(name);
            var output = new List<CssSyntaxComponent>();
            var nodes = 0;
            var valid = Substitute(declared.Values, output, ref nodes, offset, stack.Count, out _);
            stack.RemoveAt(stack.Count - 1); visiting.Remove(name); done.Add(name);
            if (valid && !cyclic.Contains(name)) values[name] = new CssVariableValue(output.ToArray(), nodes);
        }

        internal bool Substitute(IReadOnlyList<CssSyntaxComponent> source, List<CssSyntaxComponent> output,
            ref int nodes, int offset, int depth, out string? missing)
            => CssVariables.Substitute(source, output, ref nodes, offset, depth, budget, limits,
                (string name, out CssVariableValue value) => TryLookup(name, out value), out missing);
    }

    internal delegate bool Lookup(string name, out CssVariableValue value);

    /// <summary>
    /// Appends <paramref name="source"/> to <paramref name="output"/> with every <c>var()</c> replaced by
    /// the referenced value or its fallback. Returns false, naming the property in
    /// <paramref name="missing"/>, when a reference has the guaranteed-invalid value and no fallback.
    /// </summary>
    internal static bool Substitute(IReadOnlyList<CssSyntaxComponent> source, List<CssSyntaxComponent> output,
        ref int nodes, int offset, int depth, CssBudget budget, CssSyntaxLimits limits, Lookup lookup, out string? missing)
    {
        missing = null;
        CssBudget.Check(depth <= limits.MaxDepth, "CSS var() references nest deeper than MaxCssSyntaxDepth.", offset);
        for (var i = 0; i < source.Count; i++)
        {
            var component = source[i];
            if (component is CssFunctionComponent function && IsVar(function))
            {
                Reference(function, out var name, out var fallback);
                if (lookup(name, out var value))
                {
                    Charge(ref nodes, value.Nodes, offset, budget, limits);
                    output.AddRange(value.Values);
                    continue;
                }
                if (fallback < 0) { missing = name; return false; }
                if (!Substitute(Slice(function.Values, fallback), output, ref nodes, offset, depth + 1, budget, limits, lookup, out missing))
                    return false;
                continue;
            }
            if (component is CssFunctionComponent call && ContainsVar(call.Values))
            {
                var inner = new List<CssSyntaxComponent>();
                if (!Substitute(call.Values, inner, ref nodes, offset, depth + 1, budget, limits, lookup, out missing)) return false;
                Charge(ref nodes, 1, offset, budget, limits);
                output.Add(new CssFunctionComponent(call.Name, inner.ToArray(), call.Span));
                continue;
            }
            if (component is CssBlockComponent block && ContainsVar(block.Values))
            {
                var inner = new List<CssSyntaxComponent>();
                if (!Substitute(block.Values, inner, ref nodes, offset, depth + 1, budget, limits, lookup, out missing)) return false;
                Charge(ref nodes, 1, offset, budget, limits);
                output.Add(new CssBlockComponent(block.Opening, inner.ToArray(), block.Span));
                continue;
            }
            Charge(ref nodes, 1 + component switch
            {
                CssFunctionComponent other => CountNodes(other.Values),
                CssBlockComponent other => CountNodes(other.Values),
                _ => 0
            }, offset, budget, limits);
            output.Add(component);
        }
        return true;
    }

    private static void Charge(ref int nodes, int count, int offset, CssBudget budget, CssSyntaxLimits limits)
    {
        budget.Work(offset, count);
        nodes += count;
        CssBudget.Check(nodes <= limits.MaxNodes,
            "A value after CSS var() substitution exceeds MaxCssSyntaxNodes.", offset);
    }

    /// <summary>Whether components are exactly one CSS-wide keyword, which var() substitution may not produce.</summary>
    internal static bool IsWideKeyword(IReadOnlyList<CssSyntaxComponent> values)
    {
        string? only = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (IsWhitespace(values[i])) continue;
            if (only is not null || values[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token }) return false;
            only = token.Value.ToLowerInvariant();
        }
        return only is "inherit" or "initial" or "unset" or "revert" or "revert-layer";
    }
}
