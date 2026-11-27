using System.Text;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>What a custom property declaration (<c>--name: value</c>) says: its value as written, or one
/// of the CSS-wide keywords <c>inherit</c>, <c>initial</c> or <c>unset</c>. A value without <c>var()</c>
/// is its own computed value, built once here and shared by every element the declaration applies to.</summary>
internal sealed record CssCustomValue(CssSyntaxComponent[] Values, string? Keyword)
{
    internal CssVariableValue? Literal { get; } = Keyword is null && !CssVariables.ContainsVar(Values)
        ? new CssVariableValue(Values, CssVariables.CountNodes(Values, out var depth), depth, 0) : null;
}

/// <summary>A declaration of a supported property whose value holds <c>var()</c>: it is compiled for each
/// element after substitution, with the host selectors or style-attribute host it was written for.</summary>
internal sealed record CssPendingValue(CssDeclarationNode Node, CssSelector[]? HostSelectors, bool Inline);

/// <summary>A custom property's computed value: its tokens with every <c>var()</c> substituted, how many
/// syntax nodes they hold, how deeply functions and blocks nest in them, and the length of the longest
/// chain of custom property references it was computed through (0 for a value without <c>var()</c>).</summary>
internal sealed record CssVariableValue(CssSyntaxComponent[] Values, int Nodes, int Depth, int Chain);

/// <summary>What a substitution has produced so far: syntax nodes, nesting depth and reference chain length.</summary>
internal struct CssSubstitution
{
    internal int Nodes;
    internal int Depth;
    internal int Chain;
}

/// <summary>
/// The computed custom properties of one element, as a layer over its parent's: the layer holds only
/// the names the element itself declares, so an element costs its own declarations, never a copy of
/// everything it inherits. Immutable once resolved; an element whose declarations change nothing shares
/// its parent's environment. A name that is absent from every layer has the guaranteed-invalid value
/// (never declared); a layer records <c>initial</c>, a reference cycle or an invalid substitution as an
/// entry without a value, which hides the ancestors' value.
/// </summary>
internal sealed class CssCustomEnvironment
{
    internal static readonly CssCustomEnvironment Empty = new(null, new Dictionary<string, CssVariableValue?>(0, StringComparer.Ordinal));
    private readonly CssCustomEnvironment? parent;
    private readonly Dictionary<string, CssVariableValue?> own;
    internal CssCustomEnvironment(CssCustomEnvironment? parent, Dictionary<string, CssVariableValue?> own)
    {
        this.parent = parent;
        this.own = own;
    }

    /// <summary>The value of <paramref name="name"/>, searching this layer and then its ancestors. Every
    /// ancestor layer read is charged to the CSS work budget, so a lookup costs at most the number of
    /// declaring ancestors (bounded by <c>MaxDepth</c>) and the total stays within
    /// <c>MaxCssMatchOperations</c>.</summary>
    internal bool TryGet(string name, CssBudget budget, int offset, out CssVariableValue value)
    {
        var layers = 0;
        for (var layer = this; layer is not null; layer = layer.parent)
        {
            if (layer.own.TryGetValue(name, out var found))
            {
                if (layers > 0) budget.Work(offset, layers);
                value = found!;
                return found is not null;
            }
            layers++;
        }
        if (layers > 1) budget.Work(offset, layers - 1);
        value = null!;
        return false;
    }
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

    /// <summary>The syntax nodes in <paramref name="values"/>, and in <paramref name="depth"/> how deeply
    /// functions and blocks nest in them (0 for tokens only).</summary>
    internal static int CountNodes(IReadOnlyList<CssSyntaxComponent> values, out int depth)
    {
        var count = 0;
        depth = 0;
        for (var i = 0; i < values.Count; i++)
        {
            count++;
            int inner;
            switch (values[i])
            {
                case CssFunctionComponent function: count += CountNodes(function.Values, out inner); depth = Math.Max(depth, inner + 1); break;
                case CssBlockComponent block: count += CountNodes(block.Values, out inner); depth = Math.Max(depth, inner + 1); break;
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
        // The element's own layer: only the names it declares. inherit and unset keep the parent's value,
        // so they add nothing; initial and every var() value start without a value, hiding the parent's.
        var own = new Dictionary<string, CssVariableValue?>(declared.Count, StringComparer.Ordinal);
        Dictionary<string, (CssCustomValue Value, int Offset)>? pending = null;
        foreach (var (name, (value, offset)) in declared)
        {
            switch (value.Keyword)
            {
                case "inherit" or "unset": break;
                case "initial": own[name] = null; break;
                default:
                    own[name] = value.Literal;
                    if (value.Literal is null)
                        (pending ??= new Dictionary<string, (CssCustomValue Value, int Offset)>(StringComparer.Ordinal)).Add(name, (value, offset));
                    break;
            }
        }
        if (own.Count == 0) return parent;
        var environment = new CssCustomEnvironment(ReferenceEquals(parent, CssCustomEnvironment.Empty) ? null : parent, own);
        if (pending is not null)
            new Resolver(environment, own, pending, budget, limits).ResolveAll();
        return environment;
    }

    private sealed class Resolver(CssCustomEnvironment environment, Dictionary<string, CssVariableValue?> own,
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

        private bool TryLookup(string name, int offset, out CssVariableValue value)
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
            return environment.TryGet(name, budget, offset, out value);
        }

        private void Resolve(string name)
        {
            if (done.Contains(name)) return;
            var (declared, offset) = pending[name];
            // Bounds the resolver's own recursion; the length of a chain resolved in any order is
            // bounded by each value's Chain in Substitute.
            CssBudget.Check(stack.Count < limits.MaxDepth, DepthMessage, offset);
            visiting.Add(name); stack.Add(name);
            var output = new List<CssSyntaxComponent>();
            var shape = new CssSubstitution();
            var valid = CssVariables.Substitute(declared.Values, output, ref shape, offset, stack.Count, budget, limits,
                (string reference, out CssVariableValue value) => TryLookup(reference, offset, out value), out _);
            stack.RemoveAt(stack.Count - 1); visiting.Remove(name); done.Add(name);
            if (valid && !cyclic.Contains(name)) own[name] = new CssVariableValue(output.ToArray(), shape.Nodes, shape.Depth, shape.Chain);
        }
    }

    internal delegate bool Lookup(string name, out CssVariableValue value);

    /// <summary>
    /// Appends <paramref name="source"/> to <paramref name="output"/> with every <c>var()</c> replaced by
    /// the referenced value or its fallback. Returns false, naming the property in
    /// <paramref name="missing"/>, when a reference has the guaranteed-invalid value and no fallback.
    /// </summary>
    internal static bool Substitute(IReadOnlyList<CssSyntaxComponent> source, List<CssSyntaxComponent> output,
        ref CssSubstitution shape, int offset, int depth, CssBudget budget, CssSyntaxLimits limits, Lookup lookup, out string? missing)
        => Substitute(source, output, ref shape, offset, depth, 0, budget, limits, lookup, out missing);

    // depth counts fallbacks and functions or blocks entered (the recursion); nesting is how many
    // functions and blocks enclose the output position, so a spliced value's own nesting adds to it.
    private static bool Substitute(IReadOnlyList<CssSyntaxComponent> source, List<CssSyntaxComponent> output,
        ref CssSubstitution shape, int offset, int depth, int nesting, CssBudget budget, CssSyntaxLimits limits, Lookup lookup, out string? missing)
    {
        missing = null;
        CssBudget.Check(depth <= limits.MaxDepth, DepthMessage, offset);
        for (var i = 0; i < source.Count; i++)
        {
            var component = source[i];
            if (component is CssFunctionComponent function && IsVar(function))
            {
                Reference(function, out var name, out var fallback);
                if (lookup(name, out var value))
                {
                    // A reference chain is one link longer than the longest chain of the value it
                    // uses, however the chain was resolved, and a spliced value keeps its nesting.
                    Nest(ref shape, nesting + value.Depth, offset, limits);
                    shape.Chain = Math.Max(shape.Chain, value.Chain + 1);
                    CssBudget.Check(shape.Chain <= limits.MaxDepth, DepthMessage, offset);
                    Charge(ref shape, value.Nodes, offset, budget, limits);
                    output.AddRange(value.Values);
                    continue;
                }
                if (fallback < 0) { missing = name; return false; }
                if (!Substitute(Slice(function.Values, fallback), output, ref shape, offset, depth + 1, nesting, budget, limits, lookup, out missing))
                    return false;
                continue;
            }
            if (component is CssFunctionComponent call && ContainsVar(call.Values))
            {
                Nest(ref shape, nesting + 1, offset, limits);
                var inner = new List<CssSyntaxComponent>();
                if (!Substitute(call.Values, inner, ref shape, offset, depth + 1, nesting + 1, budget, limits, lookup, out missing)) return false;
                Charge(ref shape, 1, offset, budget, limits);
                output.Add(new CssFunctionComponent(call.Name, inner.ToArray(), call.Span));
                continue;
            }
            if (component is CssBlockComponent block && ContainsVar(block.Values))
            {
                Nest(ref shape, nesting + 1, offset, limits);
                var inner = new List<CssSyntaxComponent>();
                if (!Substitute(block.Values, inner, ref shape, offset, depth + 1, nesting + 1, budget, limits, lookup, out missing)) return false;
                Charge(ref shape, 1, offset, budget, limits);
                output.Add(new CssBlockComponent(block.Opening, inner.ToArray(), block.Span));
                continue;
            }
            var count = 1;
            var nested = 0;
            switch (component)
            {
                case CssFunctionComponent other: count += CountNodes(other.Values, out nested); nested++; break;
                case CssBlockComponent other: count += CountNodes(other.Values, out nested); nested++; break;
            }
            Nest(ref shape, nesting + nested, offset, limits);
            Charge(ref shape, count, offset, budget, limits);
            output.Add(component);
        }
        return true;
    }

    private const string DepthMessage = "CSS var() references nest deeper than MaxCssSyntaxDepth.";

    private static void Nest(ref CssSubstitution shape, int nesting, int offset, CssSyntaxLimits limits)
    {
        shape.Depth = Math.Max(shape.Depth, nesting);
        CssBudget.Check(nesting <= limits.MaxDepth,
            "A value after CSS var() substitution nests functions and blocks deeper than MaxCssSyntaxDepth.", offset);
    }

    private static void Charge(ref CssSubstitution shape, int count, int offset, CssBudget budget, CssSyntaxLimits limits)
    {
        budget.Work(offset, count);
        shape.Nodes += count;
        CssBudget.Check(shape.Nodes <= limits.MaxNodes,
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
