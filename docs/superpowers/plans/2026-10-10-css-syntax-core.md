# FactsPDF CSS Syntax Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Replace the renderer-coupled CSS tokenizer with an independently testable standards-based CSS Syntax Level 3 tokenizer, parser, recovery/diagnostics pipeline and strict-compatibility adapter, before implementing the Block Box Tree.

**Architecture:** New \`FactsPDF.CssSyntax\` types perform Unicode input preprocessing, tokenization, component/block/rule parsing and recoverable diagnostics without depending on HTML/layout/PDF code. An adapter in \`FactsPDF.Css\` interprets only currently supported selectors/properties and preserves the existing StrictPdf behavior, including fail-before-PDF-output guarantees.

**Tech Stack:** .NET 10, Native AOT-compatible managed C#, NUnit 4, existing no-third-party-runtime-dependency FactsPDF core, real GitHub Actions Windows/Linux/macOS and Linux x64 Native AOT; spec-grounded independent WPT fixtures.

**Spec:** \`docs/superpowers/specs/2026-10-10-css-syntax-core-design.md\`

## Global Constraints

- W3C CSS Syntax Level 3 **Candidate Recommendation Draft, 1 October 2026**: https://www.w3.org/TR/2026/CRD-css-syntax-3-20261001/ . Do not silently substitute a newer editor's draft.
- Current verified \`main\` baseline: \`8c303bda7a0fe1875f5376839a4bc7f4ebf8dc6e\`; **223 existing NUnit cases** must remain passing.
- No browser, JavaScript runtime, native third-party CSS library, internet/file access by the parser, reflection-based runtime generation, dynamically loaded code or external font files.
- Retain \`PdfConverter.Convert(string, Stream, PdfOptions?, CancellationToken)\`, CLI behavior, original CSS error codes \`FPDF1201\`–\`FPDF1205\`, user-visible rendering and six licensing/contribution-policy files.
- Existing CSS limits: \`MaxCssCharacters=262_144\`, \`MaxCssSelectors=4096\`, \`MaxCssDeclarations=32768\`, \`MaxCssMatchOperations=5_000_000\`.
- Add configurable \`PdfOptions.MaxCssSyntaxNodes=131_072\`, \`PdfOptions.MaxCssSyntaxDepth=64\`. When parser runs independently, the same defaults apply. Zero or negative configured limits fail before any PDF output.
- Per-document state only. All core spans use **original UTF-16 code-unit positions**; absolute HTML positions are supplied by the adapter, including decoded inline attributes.
- Core recovers CSS syntax problems into diagnostics. Existing **StrictPdf** semantic adapter still rejects recovered errors, unsupported selectors/at-rules/properties/values before copying PDF bytes.
- Code/tests stay in a **draft feature PR**, with small commits and measured verification. No merge, NuGet publish, release or legal-policy changes.
- WPT reference revision **\`521d168d63dbd206ea7fbe0b74ddd5760e6e668e\`** from \`web-platform-tests/wpt\` \`master\`, captured 10 October 2026. If a fixture is adapted/copied, record its original path, revision and BSD-3-Clause attribution; do not ship standalone third-party binaries/fonts.
- CSS Syntax compatibility is not CSS selector/property support, layout support or full HTML5/browser conformance.

## Review Focus

Five error-prone inputs with exact regression owners:
1. Decoded inline CSS entities, e.g. \`style="color:r&#101;d"\`: syntax spans/diagnostics must resolve to original HTML offsets, not shortened decoded positions — **Task 9**.
2. \`p/**/span\` must stay two adjacent identifiers, **not** create a descendant combinator or merge into \`pspan\` — **Task 3 / Task 8**.
3. A bad URL/string or an unsupported \`@media\` must **not** fetch a URL, crash, or hide a later valid rule; the parser recovers, while StrictPdf rejects unsupported semantics — **Task 4 / Task 6 / Task 9**.
4. \`!important\` inside \`var(...)\`, a string, or a nested block must **not** be treated as a declaration-level importance marker — **Task 7**.
5. Nested input at 65 levels, token/node floods and cancellation mid-recovery must stop via deterministic budget/cancellation, not stack overflow or unbounded allocation — **Task 5 / Task 10**.

---

## File/Interface Map

No new project/package or assembly in this increment. Add to existing \`src/FactsPDF/FactsPDF.csproj\` through SDK compile globs.

| File | Responsibility |
| --- | --- |
| \`src/FactsPDF/CssSyntax/CssSyntaxTypes.cs\` | Immutable \`CssSourceSpan\`, \`CssSyntaxTokenKind\`, \`CssSyntaxToken\`, diagnostics, parser limits |
| \`src/FactsPDF/CssSyntax/CssSourceText.cs\` | Unicode/CRLF/FF preprocessing and original UTF-16 source mapping |
| \`src/FactsPDF/CssSyntax/CssSyntaxBudget.cs\` | Bounded node/depth/work accounting; own \`CssSyntaxLimitException\`, no dependency on \`FactsPdfException\` |
| \`src/FactsPDF/CssSyntax/CssSyntaxTokenizer.cs\` | CSS Syntax spec tokenization and consume algorithms |
| \`src/FactsPDF/CssSyntax/CssSyntaxNodes.cs\` | Components, functions, blocks, rules, declarations, stylesheet and diagnostic results |
| \`src/FactsPDF/CssSyntax/CssSyntaxParser.cs\` | Ordered rule/content/declaration parsing, recovery, importance detection |
| \`src/FactsPDF/Css/CssSyntaxAdapter.cs\` | Converts supported new tokens/preludes/declarations to the legacy selector/property subset with strict diagnostics |
| \`src/FactsPDF/Css/CssSelector.cs\` | Selector matching stays unchanged; replace only parser entry with a small adapter |
| \`src/FactsPDF/Css/CssStylesheets.cs\` | One syntax parsing path for style blocks/attributes; preserve index, cascade, budgets and order |
| \`src/FactsPDF/Css/CssDeclarations.cs\` | Apply known properties; consume new declaration component lists; preserve CSS-wide keywords |
| \`src/FactsPDF/Css/CssTokens.cs\` | Delete only after all renderer paths migrate; no hidden second production parser |
| \`src/FactsPDF/HtmlTokens.cs\` | Add narrow attribute-origin metadata (not a new HTML parser) for mapped inline-style source spans |
| \`src/FactsPDF/Api.cs\` | Add the two exact syntax-limit options |
| \`tests/FactsPDF.Tests/CssSyntax/*Tests.cs\` | Independent core tests |
| \`tests/FactsPDF.Tests/CssSyntaxIntegrationTests.cs\` | Renderer regression/strict contract |
| \`tests/FactsPDF.Tests/CssSyntaxWptCases.cs\` | Pinned WPT-derived case IDs and attribution |
| \`docs/css-syntax-core.md\` | Supported syntax vs accepted semantics vs rendered CSS matrix |
| \`.github/workflows/css-syntax.yml\` | Independent corpus, AOT/CI budget tests, resource measurements without publishing |

### Contract between tasks (authoritative names)

\`\`\`csharp
namespace FactsPDF.CssSyntax;

internal readonly record struct CssSourceSpan(int Start, int Length);
internal sealed record CssSyntaxLimits(
    int MaxCharacters, int MaxNodes, int MaxDepth, CancellationToken Cancellation);
internal sealed class CssSyntaxLimitException(string reason, CssSourceSpan span) : Exception(reason)
{
    internal CssSourceSpan Span { get; } = span;
}
internal sealed record CssSyntaxDiagnostic(
    string Code, string Message, CssSourceSpan Span, string Recovery);
internal enum CssSyntaxTokenKind
{
    Ident, Function, AtKeyword, Hash, String, BadString, Url, BadUrl,
    Delim, Number, Percentage, Dimension, Whitespace, Cdo, Cdc,
    Colon, Semicolon, Comma, OpenSquare, CloseSquare,
    OpenParen, CloseParen, OpenBrace, CloseBrace, Eof
}
internal readonly record struct CssSyntaxToken(
    CssSyntaxTokenKind Kind, string Value, string Raw, CssSourceSpan Span,
    bool IsInteger = false, bool HashIsId = false, string? Unit = null);
internal sealed class CssSourceText
{
    internal static CssSourceText Create(
        string source, int absoluteStart, CssSyntaxLimits limits,
        IReadOnlyList<int>? originalOffsets = null);
    internal string Normalized { get; }
    internal CssSourceSpan Span(int normalizedStart, int normalizedLength);
}
internal static class CssSyntaxTokenizer
{
    internal static IReadOnlyList<CssSyntaxToken> Tokenize(CssSourceText source, CssSyntaxLimits limits);
}
internal static class CssSyntaxParser
{
    internal static CssSyntaxResult ParseStylesheet(
        CssSourceText source, CssSyntaxLimits limits);
    internal static CssSyntaxResult ParseDeclarations(
        CssSourceText source, CssSyntaxLimits limits);
}
\`\`\`

\`originalOffsets\` is an optional **decoded UTF-16 boundary-to-original-absolute-index** map with \`source.Length + 1\` entries; core source mapping remains independent of \`HtmlTokens\`. \`CssSyntaxResult\` contains \`IReadOnlyList<CssRuleNode> Rules\`, \`IReadOnlyList<CssDeclarationNode> Declarations\` and \`IReadOnlyList<CssSyntaxDiagnostic> Diagnostics\`, with exactly one entry point's collection authoritative. AST nodes are immutable after parse. Component nodes have \`CssTokenComponent\`, \`CssBlockComponent\` and \`CssFunctionComponent\`, plus \`CssAtRuleNode\` and \`CssQualifiedRuleNode\`; each exposes a \`CssSourceSpan\` and ordered children.

Use \`dotnet test tests/FactsPDF.Tests/FactsPDF.Tests.csproj -c Release --filter "FullyQualifiedName~<FixtureName>"\` for focused steps. A missing .NET SDK in an editor environment is **not** a green test: push a TDD baseline to GitHub Actions and inspect the actual logs.

## Task 1: Source preprocessing, mapping and parser limits

**Files:** Create \`CssSyntax/CssSyntaxTypes.cs\`, \`CssSyntax/CssSourceText.cs\`, \`CssSyntax/CssSyntaxBudget.cs\`; add \`tests/FactsPDF.Tests/CssSyntax/CssSourceTextTests.cs\`; modify \`Api.cs\` for default syntax limits.

**Interfaces:** Implement \`CssSourceText.Create(...)\`, \`Normalized\`, \`Span(start,length)\`, \`CssSyntaxBudget.Node(span)\`, \`CssSyntaxBudget.Enter(span)\`, \`CssSyntaxBudget.Leave()\`. A budget exception has only CSS syntax types, not renderer types.

- [ ] **Step 1: Add failing \`CssSourceTextTests\`** for:
  \`\`\`csharp
  var text = CssSourceText.Create("a\\r\\nb\\fc\\0d\\ud800", 50, limits);
  Assert.That(text.Normalized, Is.EqualTo("a\\nb\\nc\\uFFFDd\\uFFFD"));
  Assert.That(text.Span(2, 1), Is.EqualTo(new CssSourceSpan(53, 1)));
  Assert.That(() => CssSourceText.Create("x", 0, limits with { MaxNodes = 0 }),
      Throws.TypeOf<ArgumentOutOfRangeException>());
  \`\`\`
  Also assert a valid non-BMP character retains its Unicode scalar and two original UTF-16 boundaries; \`originalOffsets\` can map to noncontiguous encoded attribute positions without losing original spans.
- [ ] **Step 2: Run focused tests**, observe preprocessor/API failures (red).
- [ ] **Step 3: Implement the three types**, normalizing CRLF/CR/FF, NUL and isolated surrogate code units exactly once; keep a \`source.Length + 1\` original-boundary map and handle \`originalOffsets\`. Limit raw characters before allocating normalized copies. Return precise source offsets even after shortening CRLF.
- [ ] **Step 4: Focused tests green**, then \`dotnet test FactsPDF.slnx -c Release\` for original 223.
- [ ] **Step 5: Commit** \`feat(css-syntax): add Unicode input and mapped limits\`.

## Task 2: Primitive CSS tokens and numeric grammar

**Files:** Create \`CssSyntax/CssSyntaxTokenizer.cs\`; test \`tests/FactsPDF.Tests/CssSyntax/CssNumericTokenTests.cs\`.

**Interfaces:** Implement \`CssSyntaxTokenizer.Tokenize(CssSourceText,CssSyntaxLimits)\` returning immutable/separately owned token data with EOF.

- [ ] **Step 1: Red tests** for \`"1e3px -0.5% +2. 12px 4.25 0"\` kinds \`Dimension, Percentage, Number, Dimension, Number, Number\` and \`IsInteger\` semantics, delimiter \`>\`, \`:\`, \`;\`, commas, \`[](){}\`, CDO \`<!--\`/CDC \`-->\`, comments removed while preserving token adjacency and true whitespace. Assert \`Raw\`, \`Value\`, \`Unit\` and exact UTF-16 spans.
- [ ] **Step 2: Run focused tests red** (tokenization missing).
- [ ] **Step 3: Implement spec tokenizer primitive dispatch and \`consume-number\`**, including optional sign, decimal and exponent; do not use regex or resolve CSS property values. Check cancellation/work limits per token/scanned character.
- [ ] **Step 4: Focused and baseline suites green**, check stable output under \`fr-FR\` culture.
- [ ] **Step 5: Commit** \`feat(css-syntax): tokenize numbers and punctuation\`.

## Task 3: Identifiers, Unicode escapes, hashes, keywords and comments

**Files:** Modify \`CssSyntaxTokenizer.cs\`; test \`CssIdentifierTokenTests.cs\`.

**Interfaces:** Extend the same \`Tokenize\`, no public API change.

- [ ] **Step 1: Red tests** \`"p/**/span"\` -> adjacent \`Ident(p)\`, \`Ident(span)\` without inserted whitespace; \`"p /*x*/ span"\` -> whitespace token between; \`"café"\` -> ident; \`"\\31 23"\` -> ident value \`123\`; \`"#x @media --Theme"\` -> hash, at-keyword, case-preserved ident; invalid escaped NUL or scalar -> U+FFFD; escaped EOF behavior checked. Include escaped hash ID classification.
- [ ] **Step 2: Run focused tests red**.
- [ ] **Step 3: Implement spec \`would-start-ident\`, \`consume-name\`, \`consume-escaped-code-point\`, hash type and at-keyword transitions**, allowing non-ASCII and correct max six-digit hex escapes; no CSS selector semantics in the tokenizer.
- [ ] **Step 4: Focused + original test suites green**.
- [ ] **Step 5: Commit** \`feat(css-syntax): support Unicode names and escapes\`.

## Task 4: Strings, URLs, bad tokens and safe recovery

**Files:** Modify \`CssSyntaxTokenizer.cs\`; test \`CssStringUrlTokenTests.cs\`.

**Interfaces:** Tokenizer emits \`String\`/\`BadString\`/\`Url\`/\`BadUrl\` without throwing for ordinary CSS syntax faults; parser will turn bad tokens into diagnostics in Task 5.

- [ ] **Step 1: Red tests** for \`"'a\\'b'"\`, escaped line continuation, \`"url(test.png)"\`, \`"url('a b')"\` as function + string, \`"url(a b) p{color:red}"\` bad-url then later rule tokens, unterminated string/newline, EOF inside url, and deeply escaped URL payload that must never cause file/network access.
- [ ] **Step 2: Focused tests red**.
- [ ] **Step 3: Implement CSS \`consume-string-token\`, \`consume-url-token\` and \`consume-remnants-of-bad-url\`**, returning spec tokens and progressing monotonically. Do not construct \`Uri\`, download, or resolve paths.
- [ ] **Step 4: Focused + baseline green**, verify no unexpected exceptions under cancellation.
- [ ] **Step 5: Commit** \`feat(css-syntax): parse and recover CSS strings and URLs\`.

## Task 5: Component values, nested blocks, limits and diagnostics

**Files:** Create \`CssSyntax/CssSyntaxNodes.cs\`, \`CssSyntax/CssSyntaxParser.cs\`; test \`CssComponentValueTests.cs\`.

**Interfaces:** Define the AST types documented above. \`CssSyntaxParser.ParseStylesheet\` and \`ParseDeclarations\` become compilable, returning \`CssSyntaxResult\`. Implement internal \`ConsumeComponentValue\` and \`ConsumeSimpleBlock\` with \`CssSyntaxBudget.Enter/Leave\`.

- [ ] **Step 1: Red tests** for \`calc(100% - var(--x, 2px))\` nested function children; \`a{color:red}\` qualified-rule prelude/body; \`a{border:1px solid red\` unterminated bracket recovery diagnostic with a finite span; 65 nested brackets -> \`CssSyntaxLimitException\` at default depth 64; cancellation during recovery; malformed closing delimiters do not loop.
- [ ] **Step 2: Run focused tests red**.
- [ ] **Step 3: Implement iterative token consumption with explicit stack/depth accounting**, ordered component lists and immutable node snapshots. Record bad/mismatched/unclosed syntax diagnostics; do not evaluate component functions. Check budget before append.
- [ ] **Step 4: Focused + baseline green**.
- [ ] **Step 5: Commit** \`feat(css-syntax): build component-value AST with bounded recovery\`.

## Task 6: Qualified rules, at-rules and ordered block contents

**Files:** Modify \`CssSyntaxParser.cs\`, \`CssSyntaxNodes.cs\`; test \`CssRuleParserTests.cs\`.

**Interfaces:** \`CssSyntaxResult.Rules\`, \`CssQualifiedRuleNode.Prelude/Contents\`, \`CssAtRuleNode.Name/Prelude/Block\`; rule contents retain ordered \`CssDeclarationNode\` and nested \`CssRuleNode\` items.

- [ ] **Step 1: Red tests** for \`@media print { p{color:red} } p{color:blue}\` -> at-rule plus qualified-rule with *no syntax error*; \`p{color:red; & span{color:blue} background:white}\` declaration/nested-rule/declaration ordering; unsupported future at-rule preserves prelude; bad CSS rule still allows a later valid rule. A malformed \`@media\` cannot cause a network fetch or recursion explosion.
- [ ] **Step 2: Run focused tests red**.
- [ ] **Step 3: Implement spec stylesheet / at-rule / qualified-rule / block-content algorithms**, including semicolon terminators and recovery points. Avoid incorrectly flattening declarations ahead of nested rules; scope nested-rule *syntax*, not its selector semantics.
- [ ] **Step 4: Focused + baseline green**; inspect diagnostic spans for a recovered rule.
- [ ] **Step 5: Commit** \`feat(css-syntax): parse ordered stylesheet rules and at-rules\`.

## Task 7: Declaration lists, terminal importance and custom properties

**Files:** Modify \`CssSyntaxParser.cs\` and nodes; test \`CssDeclarationSyntaxTests.cs\`.

**Interfaces:** \`CssSyntaxParser.ParseDeclarations\` returns ordered \`CssDeclarationNode\`s; each has \`string Name\`, \`IReadOnlyList<CssSyntaxComponent> Values\`, \`bool Important\`, \`CssSourceSpan Span\`.

- [ ] **Step 1: Red tests**: \`"color:red !important; --Theme: var(--x, red); width:calc(100% - 2px)"\` produces three declarations with exact property case and nested values; \`"x: var(--a, !important)"\` keeps \`Important=false\`; \`"content:'!important'"\` false; \`"color:red ! /*c*/ IMPORTANT"\` true. Invalid declaration \`"bad:value without-semicolon; color:blue"\` recovers and preserves the next valid declaration, not a thrown parser exception. Inline declaration input containing an unexpected qualified rule is diagnosed rather than rendered as CSS.
- [ ] **Step 2: Run focused tests red**.
- [ ] **Step 3: Implement declaration grammar, \`!important\` extraction using only terminal top-level tokens**, and error recovery with ordered values. Treat unknown properties and custom properties as syntactically valid; never expand \`var()\`, \`calc()\` or nested rules.
- [ ] **Step 4: Focused + baseline green**.
- [ ] **Step 5: Commit** \`feat(css-syntax): parse declarations and importance\`.

## Task 8: Strict selector bridge without widening selector support

**Files:** Create \`Css/CssSyntaxAdapter.cs\`; modify \`Css/CssSelector.cs\`; tests \`CssSyntaxIntegrationTests.cs\` (selector cases).

**Interfaces:** \`CssSyntaxAdapter.CompileSelector(IReadOnlyList<CssSyntaxComponent> prelude, CssBudget budget) : IReadOnlyList<CssSelector>\`. Add \`CssSelector.ParseSupportedComponents(...)\` or an equivalent private token-adapter to existing match tree; preserve \`CssSelector.Matches\`.

- [ ] **Step 1: Red tests**: \`p.note > span\` matches as before, \`p/**/span\` fails \`FPDF1203\`, comment with true whitespace still yields descendant selection, comma groups and compound selectors preserve specificity, \`p:hover\` remains unsupported \`FPDF1203\`, and a valid \`@media\` AST is rejected by the strict renderer as \`FPDF1204\` (not by core).
- [ ] **Step 2: Run focused integration tests red** *after switching only the selector call path*. This must be a real behavioral red test, not simply a nonexistent-type compilation error.
- [ ] **Step 3: Translate new token/component sequences to existing compound/child/descendant selector data**, preserving whitespace boundaries and original offsets; do not add pseudo/attribute/sibling selector support. Respect the \`CssBudget.Selector\` and \`Work\` limits.
- [ ] **Step 4: Focused tests and existing selector regression green**.
- [ ] **Step 5: Commit** \`refactor(css): consume syntax AST for selectors\`.

## Task 9: Strict declaration bridge and mapped inline source locations

**Files:** Modify \`Css/CssSyntaxAdapter.cs\`, \`Css/CssDeclarations.cs\`, \`Css/CssStylesheets.cs\`, \`HtmlTokens.cs\`, \`Css/CssTokens.cs\` (remove only once unused), and \`Api.cs\`; test \`CssSyntaxIntegrationTests.cs\` and \`CssInlineSourceMapTests.cs\`.

**Interfaces:** \`CssSyntaxAdapter.CompileKnownDeclarations(IReadOnlyList<CssDeclarationNode>, CssBudget) : CssDeclaration[]\`; \`HtmlToken.AttributeSourceOffsets\` (optional dictionary from name to decoded-character origin-boundary list); \`CssStylesheets.Collect(string,PdfOptions,CancellationToken)\` public/internal signature unchanged.

- [ ] **Step 1: Red tests**: all existing \`font-size\`, \`color\`, \`line-height\`, \`text-align\`, margin and break declarations give byte-identical PDFs, including legacy \`!important\` precedence and later style blocks. Unknown \`display:grid\` rejects \`FPDF1201\` without touching output; unknown \`calc()\` for known property rejects \`FPDF1202\`; \`@media\` rejects \`FPDF1204\`; syntax recovery rejects \`FPDF1203\`. Inline \`style="color:r&#101;d; bad!x"\` error spans map to original \`!x\` absolute HTML index despite decoded entity shortening. Inline entities in valid color values still produce correct text color.
- [ ] **Step 2: Run focused red integration tests** against the syntax AST adapter wiring, preserving a record of existing 223 passing tests before the migration.
- [ ] **Step 3: Switch to a single parse/adapter path** for stylesheet and inline inputs; retain CSS character/declaration/selector/matching budgets. Extend HTML attribute metadata just enough to map decoded \`style\` values back to original UTF-16 positions; do not replace the HTML tokenizer. Parse all CSS before layout; errors happen before serializer final output. Map \`CssSyntaxLimitException\` to \`FPDF1205\`, parsing diagnostics to \`FPDF1203\`, unsupported semantic constructs to existing respective codes. Delete unused legacy \`CssTokens\` *after* all consumers migrate.
- [ ] **Step 4: \`dotnet test FactsPDF.slnx -c Release\` green**; assert the entire original 223-case suite remains green, and new source map tests pass.
- [ ] **Step 5: Commit** \`refactor(css): route stylesheet and inline rendering through syntax core\`.

## Task 10: Adversarial budgets, cancellation and deterministic memory

**Files:** Test \`CssSyntaxStressTests.cs\`; modify core budget/parser only for proven failures.

**Interfaces:** Limits \`MaxCssCharacters/MaxCssSyntaxNodes/MaxCssSyntaxDepth\`; exceptions remain structured and do not leak partially rendered PDF bytes.

- [ ] **Step 1: Red/stress tests**: 65 nested \`var(\` levels hit depth 64; at least 131073 tiny nodes hit node budget 131072 (within/maximize character budget as needed); literal NUL/escaped EOF/huge exponent numbers recover without exception; 50000 malformed declarations terminate within a finite work allowance; pre-cancelled and mid-scan cancellation propagate; repeated concurrent conversions do not share diagnostics or mutable AST; invariant and \`fr-FR\` results match.
- [ ] **Step 2: Run focused stress tests red** for the missing budget/loop behavior; do not use flaky wall-clock time as the only assertion.
- [ ] **Step 3: Fix only concrete pathological cases**, using explicit node-depth-work counters and no unbounded recursion/allocations; keep dictionary/list capacity bounded by configured limits.
- [ ] **Step 4: Focused and full suites green**, then run CLI cancellation and input/output safety regressions.
- [ ] **Step 5: Commit** \`test(css-syntax): harden parser limits and reentrancy\`.

## Task 11: Pinned WPT differential fixtures, Native AOT and support matrix

**Files:** Create \`tests/FactsPDF.Tests/CssSyntax/CssSyntaxWptCases.cs\`, \`docs/css-syntax-core.md\`, \`.github/workflows/css-syntax.yml\`; update \`README.md\` *technical* support section (do not edit licensing).

**Interfaces:** Deterministic test corpus maps each selected upstream WPT test to an independently asserted parser outcome; a \`syntax-results.json\` artifact groups passed/failed/skipped **by syntax feature**. Non-renderable CSS stays outside rendered feature claims.

- [ ] **Step 1: Add failing/evidence tests** based on WPT revision \`521d168d63dbd206ea7fbe0b74ddd5760e6e668e\`: \`css/css-syntax/input-preprocessing.html\`, \`escaped-eof.html\`, \`decimal-points-in-numbers.html\`, \`non-ascii-codepoints.html\`, \`invalid-nested-rules.html\`, \`at-rule-in-declaration-list.html\`, \`unclosed-constructs.html\`, \`url-whitespace-consumption.html\`. Pin source links + fixture adaptations + license/attribution; distinguish observable CSSOM behavior from internal AST equivalence.
- [ ] **Step 2: Run focused/WPT suite red** for any differences; record exact failing case IDs instead of assuming Chromium and internal AST node types are directly comparable.
- [ ] **Step 3: Fix supported syntax deviations; add GitHub workflow** to run core/WPT suites plus Windows/Linux/macOS tests and real Linux x64 Native AOT CLI output, qpdf/Poppler PDF checks and package/license check. Record parser throughput, managed allocations and whole-process peak RSS under fixed input sets/runner specs against the unchanged \`main\` baseline.
- [ ] **Step 4: Require all old/new tests green and inspect workflow jobs/logs for the exact final head SHA.** Confirm no runtime library or standalone font accidentally entered the package; compare representative PDF bytes/extracted Unicode text/page render against the original feature baseline. WPT coverage reports must show selected case counts.
- [ ] **Step 5: Commit** \`test(css-syntax): pin WPT cases and verify Native AOT compatibility\`.

## Task 12: Review gate and handoff to Block Box Tree

**Files:** Create \`docs/css-syntax-development-ledger.md\`; amend \`docs/superpowers/specs/2026-10-10-block-box-model-design.md\` **only on its own design branch / separate PR**, not by overwriting a different working branch.

**Interfaces:** No new runtime API. The final release boundary remains syntax parser only.

- [ ] **Step 1: Inspect the full feature diff** for unintended scope, changed licenses, implicit browser/selector/variable claims, hidden secondary parsers and newly added third-party dependencies. Add targeted regression tests first if review finds new defects.
- [ ] **Step 2: Run exact-head full test matrix and actual AOT PDF generation**; record 223 baseline plus new-case counts, WPT revision and results, output equivalence and explicit unsupported features.
- [ ] **Step 3: Write a concise development ledger** with red/green commits, final workflows, source tree hash, performance measurement scope, remaining standards gaps and no release/merge claim.
- [ ] **Step 4: Open/update a draft feature PR for review**, retaining all code and tests and referencing this spec/plan. Mark box model PR #5 blocked until this PR merges and verifies.
- [ ] **Step 5: Commit** \`docs(css-syntax): record verified parser migration and compatibility limits\`.

## Plan Self-Review Map

- **Spec §§1–2**: architecture/independence and sequencing — Tasks 1–12.
- **§3**: Unicode/HTML offsets/RAW TEXT — Tasks 1, 9.
- **§4**: tokenizer all token families and error types — Tasks 2–4.
- **§5**: AST, nested ordered contents and recoverable errors — Tasks 5–7.
- **§6**: strict/unknown/invalid separation and future compatible mode — Tasks 8–9; no opt-in compatibility switch in this milestone.
- **§7**: selector/cascade, inline, later styles — Tasks 8–9.
- **§8**: cancellation, budgets, reentrancy — Tasks 1, 5, 10.
- **§9**: WPT, golden PDF, all platforms, real AOT, resource data — Tasks 9–11.
- **§§10–11**: migration, support matrix, licensing, separate box-model PR — Tasks 11–12.
- **Review Focus #1–#5** each has an explicitly named owning test above.

## Execution Handoff

Only this plan and the approved design travel into the implementation branch. Do not treat a green tokenizer unit test as proof of full CSS parsing or PDF compatibility. Preserve the existing \`main\` as the stable integration baseline until code and independent verification are reviewed. Recommended execution: **Native task-by-task** with TDD, frequent commits and a final fresh whole-branch review, because the parser, legacy selector adapter and source-offset contracts are tightly coupled.
