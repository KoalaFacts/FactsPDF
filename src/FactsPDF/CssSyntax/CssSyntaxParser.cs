namespace FactsPDF.CssSyntax;

/// <summary>CSS Syntax Level 3 rule and component-value parsing, independent of rendered CSS support.</summary>
internal static class CssSyntaxParser
{
    internal static CssSyntaxResult ParseStylesheet(CssSourceText source, CssSyntaxLimits limits)
        => new State(source, limits).Stylesheet();

    internal static CssSyntaxResult ParseDeclarations(CssSourceText source, CssSyntaxLimits limits)
        => new State(source, limits).Declarations();

    private sealed class State
    {
        private readonly IReadOnlyList<CssSyntaxToken> tokens;
        private readonly CssSyntaxLimits limits;
        private readonly CssSyntaxBudget budget;
        private readonly List<CssSyntaxDiagnostic> diagnostics = [];
        private int position;

        internal State(CssSourceText source, CssSyntaxLimits limits)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(limits);
            limits.Cancellation.ThrowIfCancellationRequested();
            this.limits = limits;
            budget = new CssSyntaxBudget(limits);
            var lexical = CssSyntaxTokenizer.TokenizeWithDiagnostics(source, limits);
            tokens = lexical.Tokens;
            diagnostics.AddRange(lexical.Diagnostics);
        }

        private CssSyntaxToken Current => tokens[position];
        private bool End => Current.Kind == CssSyntaxTokenKind.Eof;
        private CssSyntaxToken Take()
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            return tokens[position++];
        }
        private static bool Whitespace(CssSyntaxToken token) => token.Kind == CssSyntaxTokenKind.Whitespace;
        private void SkipSpace()
        {
            while (!End && Whitespace(Current)) Take();
        }
        private CssSourceSpan Range(int start)
        {
            var end = position == 0 ? start : tokens[position - 1].Span.Start + tokens[position - 1].Span.Length;
            return new CssSourceSpan(start, Math.Max(0, end - start));
        }
        private void Error(string message, CssSourceSpan span, string recovery = "skip")
            => diagnostics.Add(new("CSS_SYNTAX", message, span, recovery));

        internal CssSyntaxResult Stylesheet()
        {
            var rules = new List<CssRuleNode>();
            while (!End)
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                if (Current.Kind is CssSyntaxTokenKind.Whitespace
                    or CssSyntaxTokenKind.Cdo or CssSyntaxTokenKind.Cdc)
                { Take(); continue; }
                // A top-level stray '}' starts an invalid qualified-rule
                // prelude. Do not discard it and accidentally accept the next
                // selector as if the stray token had never existed.
                CssRuleNode? rule = Current.Kind == CssSyntaxTokenKind.AtKeyword
                    ? ReadAtRule() : ReadQualifiedRule();
                if (rule is not null) rules.Add(rule);
            }
            return new(rules.ToArray(), [], diagnostics.ToArray());
        }

        internal CssSyntaxResult Declarations()
        {
            var contents = ReadBlockContents(inline: true);
            return new([], contents.OfType<CssDeclarationNode>().ToArray(), diagnostics.ToArray());
        }

        private List<CssSyntaxContent> ReadBlockContents(bool inline = false)
        {
            var result = new List<CssSyntaxContent>();
            while (!End)
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                if (Current.Kind == CssSyntaxTokenKind.CloseBrace)
                {
                    if (!inline) break;
                    Error("Unexpected closing CSS declaration block.", Take().Span);
                    continue;
                }
                if (Current.Kind is CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Semicolon)
                { Take(); continue; }
                if (Current.Kind == CssSyntaxTokenKind.AtKeyword)
                {
                    var at = ReadAtRule(nested: !inline);
                    if (inline) Error("CSS at-rule is not a declaration.", at.Span);
                    else result.Add(at);
                    continue;
                }
                if (Current.Kind == CssSyntaxTokenKind.Ident)
                {
                    var customProperty = Current.Value.StartsWith("--", StringComparison.Ordinal);
                    if (customProperty || !HasOpeningBlockBeforeTerminator())
                    {
                        var declaration = ReadDeclaration();
                        if (declaration is not null) result.Add(declaration);
                        continue;
                    }
                }
                if (inline)
                {
                    // Invalid declaration recovery is bounded by the next
                    // *top-level semicolon*. A qualified-rule recovery here
                    // would consume later perfectly valid declarations.
                    var bad = Current.Span;
                    RecoverBadDeclaration();
                    Error("Invalid CSS style-attribute declaration.", bad, "semicolon");
                }
                else
                {
                    var nested = ReadQualifiedRule(nested: true);
                    if (nested is not null) result.Add(nested);
                }
            }
            return result;
        }

        private bool HasOpeningBlockBeforeTerminator()
        {
            // The CSS Nesting ambiguity rule permits a declaration whose
            // value starts with a single {} component block.
            var cursor = position + 1;
            while (cursor < tokens.Count && Whitespace(tokens[cursor])) cursor++;
            if (cursor < tokens.Count && tokens[cursor].Kind == CssSyntaxTokenKind.Colon)
            {
                cursor++;
                while (cursor < tokens.Count && Whitespace(tokens[cursor])) cursor++;
                if (cursor < tokens.Count && tokens[cursor].Kind == CssSyntaxTokenKind.OpenBrace)
                    return false;
            }
            var nesting = 0;
            for (var at = position; at < tokens.Count; at++)
            {
                if ((at & 1023) == 0) limits.Cancellation.ThrowIfCancellationRequested();
                var kind = tokens[at].Kind;
                if (kind is CssSyntaxTokenKind.Function or CssSyntaxTokenKind.OpenParen or CssSyntaxTokenKind.OpenSquare)
                { nesting++; continue; }
                if (kind is CssSyntaxTokenKind.CloseParen or CssSyntaxTokenKind.CloseSquare)
                { nesting = Math.Max(0, nesting - 1); continue; }
                if (nesting > 0) continue;
                if (kind == CssSyntaxTokenKind.OpenBrace) return true;
                if (kind is CssSyntaxTokenKind.Semicolon or CssSyntaxTokenKind.CloseBrace or CssSyntaxTokenKind.Eof)
                    return false;
            }
            return false;
        }

        private CssAtRuleNode ReadAtRule(bool nested = false)
        {
            var beginning = Take();
            var prelude = new List<CssSyntaxComponent>();
            IReadOnlyList<CssSyntaxContent>? contents = null;
            IReadOnlyList<CssSyntaxComponent>? rawBlock = null;
            while (!End)
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                if (Current.Kind == CssSyntaxTokenKind.Semicolon) { Take(); break; }
                // In a block's contents a bare '}' terminates the containing
                // block; the nested at-rule must return without consuming it.
                // At stylesheet level the same token is part of the prelude.
                if (nested && Current.Kind == CssSyntaxTokenKind.CloseBrace) break;
                if (Current.Kind == CssSyntaxTokenKind.OpenBrace)
                {
                    var open = Take();
                    budget.Enter(open.Span);
                    try
                    {
                        // Syntax Level 3 defines unknown at-rule blocks as
                        // generic sequences of component values. Only for
                        // familiar grouping rules do we additionally expose
                        // parsed declarations/nested rules to test adapters.
                        if (beginning.Value.Equals("media", StringComparison.OrdinalIgnoreCase) ||
                            beginning.Value.Equals("supports", StringComparison.OrdinalIgnoreCase) ||
                            beginning.Value.Equals("container", StringComparison.OrdinalIgnoreCase) ||
                            beginning.Value.Equals("layer", StringComparison.OrdinalIgnoreCase) ||
                            beginning.Value.Equals("scope", StringComparison.OrdinalIgnoreCase))
                            contents = ReadBlockContents().ToArray();
                        else
                        {
                            var components = new List<CssSyntaxComponent>();
                            while (!End && Current.Kind != CssSyntaxTokenKind.CloseBrace)
                            {
                                limits.Cancellation.ThrowIfCancellationRequested();
                                components.Add(ReadComponent());
                            }
                            rawBlock = components.ToArray();
                        }
                        if (Current.Kind == CssSyntaxTokenKind.CloseBrace) Take();
                        else Error("Unclosed CSS at-rule block.", open.Span, "eof");
                    }
                    finally { budget.Leave(); }
                    break;
                }
                prelude.Add(ReadComponent());
            }
            budget.Node(beginning.Span);
            return new(beginning.Value, prelude.ToArray(), contents, Range(beginning.Span.Start), rawBlock);
        }

        private CssQualifiedRuleNode? ReadQualifiedRule(bool nested = false)
        {
            var beginning = Current;
            var prelude = new List<CssSyntaxComponent>();
            while (!End && Current.Kind != CssSyntaxTokenKind.OpenBrace)
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                if (Current.Kind == CssSyntaxTokenKind.CloseBrace)
                {
                    Error("Unexpected closing brace in a qualified rule.", Current.Span);
                    if (nested) return null; // parent block consumes its own closer
                    prelude.Add(ReadComponent()); // top-level: invalid selector prelude
                    continue;
                }
                if (nested && Current.Kind == CssSyntaxTokenKind.Semicolon)
                {
                    Error("Invalid nested qualified-rule prelude.", beginning.Span, "semicolon");
                    Take();
                    return null;
                }
                prelude.Add(ReadComponent());
            }
            if (End)
            {
                Error("Qualified rule has no opening block.", beginning.Span, "eof");
                return null;
            }
            var open = Take();
            budget.Enter(open.Span);
            IReadOnlyList<CssSyntaxContent> contents;
            try
            {
                contents = ReadBlockContents().ToArray();
                if (Current.Kind == CssSyntaxTokenKind.CloseBrace) Take();
                else Error("Unclosed CSS qualified-rule block.", open.Span, "eof");
            }
            finally { budget.Leave(); }
            budget.Node(beginning.Span);
            return new(prelude.ToArray(), contents, Range(beginning.Span.Start));
        }

        private CssDeclarationNode? ReadDeclaration()
        {
            var beginning = Take();
            SkipSpace();
            if (Current.Kind != CssSyntaxTokenKind.Colon)
            {
                Error("Expected a colon after a CSS property name.", beginning.Span, "semicolon");
                RecoverBadDeclaration();
                return null;
            }
            Take(); // colon
            var values = new List<CssSyntaxComponent>();
            while (!End && Current.Kind is not (CssSyntaxTokenKind.Semicolon or CssSyntaxTokenKind.CloseBrace))
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                values.Add(ReadComponent());
            }
            if (Current.Kind == CssSyntaxTokenKind.Semicolon) Take();
            var important = ExtractImportant(values);
            budget.Node(beginning.Span);
            return new(beginning.Value, values.ToArray(), important, Range(beginning.Span.Start));
        }

        private void RecoverBadDeclaration()
        {
            while (!End && Current.Kind is not (CssSyntaxTokenKind.Semicolon or CssSyntaxTokenKind.CloseBrace))
            {
                limits.Cancellation.ThrowIfCancellationRequested();
                _ = ReadComponent();
            }
            if (Current.Kind == CssSyntaxTokenKind.Semicolon) Take();
        }

        private static bool ExtractImportant(List<CssSyntaxComponent> values)
        {
            while (values.Count > 0 && IsWhitespace(values[^1])) values.RemoveAt(values.Count - 1);
            if (values.Count < 2 || values[^1] is not CssTokenComponent ident ||
                ident.Token.Kind != CssSyntaxTokenKind.Ident ||
                !ident.Token.Value.Equals("important", StringComparison.OrdinalIgnoreCase)) return false;
            var mark = values.Count - 2;
            while (mark >= 0 && IsWhitespace(values[mark])) mark--;
            if (mark < 0 || values[mark] is not CssTokenComponent delimiter ||
                delimiter.Token.Kind != CssSyntaxTokenKind.Delim || delimiter.Token.Value != "!") return false;
            values.RemoveRange(mark, values.Count - mark);
            while (values.Count > 0 && IsWhitespace(values[^1])) values.RemoveAt(values.Count - 1);
            return true;
        }

        private static bool IsWhitespace(CssSyntaxComponent component)
            => component is CssTokenComponent token && Whitespace(token.Token);

        private CssSyntaxComponent ReadComponent()
        {
            var token = Take();
            budget.Node(token.Span);
            if (token.Kind is CssSyntaxTokenKind.BadString or CssSyntaxTokenKind.BadUrl)
                Error("Malformed CSS string or URL.", token.Span, "token");
            if (token.Kind == CssSyntaxTokenKind.String && token.Raw.Length > 0 &&
                token.Raw[0] is '\'' or '"' && token.Raw[^1] != token.Raw[0])
                Error("Unclosed CSS string.", token.Span, "eof");
            if (token.Kind == CssSyntaxTokenKind.Url &&
                !token.Raw.EndsWith(')'))
                Error("Unclosed CSS URL.", token.Span, "eof");
            if (token.Kind is not (CssSyntaxTokenKind.Function or CssSyntaxTokenKind.OpenBrace
                or CssSyntaxTokenKind.OpenParen or CssSyntaxTokenKind.OpenSquare))
                return new CssTokenComponent(token);

            var closing = token.Kind switch
            {
                CssSyntaxTokenKind.Function or CssSyntaxTokenKind.OpenParen => CssSyntaxTokenKind.CloseParen,
                CssSyntaxTokenKind.OpenSquare => CssSyntaxTokenKind.CloseSquare,
                _ => CssSyntaxTokenKind.CloseBrace
            };
            budget.Enter(token.Span);
            try
            {
                var values = new List<CssSyntaxComponent>();
                while (!End && Current.Kind != closing)
                {
                    limits.Cancellation.ThrowIfCancellationRequested();
                    // Nonmatching closing tokens are ordinary component
                    // values here; only the matching delimiter closes
                    // this function/block (CSS Syntax consume simple block).
                    values.Add(ReadComponent());
                }
                if (Current.Kind == closing) Take();
                else Error("Unclosed CSS function or component block.", token.Span, "eof");
                var span = Range(token.Span.Start);
                return token.Kind == CssSyntaxTokenKind.Function
                    ? new CssFunctionComponent(token.Value, values.ToArray(), span)
                    : new CssBlockComponent(token.Kind, values.ToArray(), span);
            }
            finally { budget.Leave(); }
        }
    }
}
