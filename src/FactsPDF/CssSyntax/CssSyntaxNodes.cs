namespace FactsPDF.CssSyntax;

internal abstract record CssSyntaxComponent(CssSourceSpan Span);
internal sealed record CssTokenComponent(CssSyntaxToken Token) : CssSyntaxComponent(Token.Span);
internal sealed record CssBlockComponent(
    CssSyntaxTokenKind Opening, IReadOnlyList<CssSyntaxComponent> Values, CssSourceSpan Span)
    : CssSyntaxComponent(Span);
internal sealed record CssFunctionComponent(
    string Name, IReadOnlyList<CssSyntaxComponent> Values, CssSourceSpan Span)
    : CssSyntaxComponent(Span);

internal abstract record CssSyntaxContent(CssSourceSpan Span);
internal abstract record CssRuleNode(CssSourceSpan Span) : CssSyntaxContent(Span);
internal sealed record CssQualifiedRuleNode(
    IReadOnlyList<CssSyntaxComponent> Prelude,
    IReadOnlyList<CssSyntaxContent> Contents, CssSourceSpan Span) : CssRuleNode(Span);
internal sealed record CssAtRuleNode(
    string Name, IReadOnlyList<CssSyntaxComponent> Prelude,
    IReadOnlyList<CssSyntaxContent>? Contents, CssSourceSpan Span) : CssRuleNode(Span);
internal sealed record CssDeclarationNode(
    string Name, IReadOnlyList<CssSyntaxComponent> Values,
    bool Important, CssSourceSpan Span) : CssSyntaxContent(Span);

internal sealed record CssSyntaxResult(
    IReadOnlyList<CssRuleNode> Rules,
    IReadOnlyList<CssDeclarationNode> Declarations,
    IReadOnlyList<CssSyntaxDiagnostic> Diagnostics);
