# FactsPDF — Standards-Based CSS Syntax Core

**Status:** Architecture specification for review; no renderer or parser implementation is implied.
**Decision:** The user selected this work ahead of the approved Block Box Tree / box-model design on 10 October 2026.
**Base:** \`KoalaFacts/FactsPDF\` \`main\`, \`8c303bda7a0fe1875f5376839a4bc7f4ebf8dc6e\`.
**Normative syntax reference:** CSS Syntax Module Level 3, W3C Candidate Recommendation Draft 1 October 2026, https://www.w3.org/TR/2026/CRD-css-syntax-3-20261001/ . Follow the normative algorithms and documented test behavior; refer to the living editor draft for clarifications, not unannounced silent spec changes.

## 1. Intent, priority and success

Build **one browser-grade *CSS syntax* parser** for the shared FactsPDF core before adding \`width\`, \`padding\`, \`border\` or \`background-color\` to the layout engine. Browser-grade here means parsing standard CSS token/rule structures and recovering from malformed CSS as the specification defines. It does **not** mean implementing all modern selectors, property grammars, media queries, Grid/Flexbox, CSSOM APIs, a DOM, or browser-grade PDF layout.

Acceptance means: syntactically valid but *unknown* CSS constructs can be represented without syntax-layer failure; malformed constructs are recovered with structured diagnostics; the renderer remains safe and explicit about what it can actually render; no browser engine, JavaScript or network access is added; and the 223 pre-existing tests plus their PDF/Native AOT workflows continue to pass.

**Execution order:** This specification -> approved implementation plan -> CSS syntax-core implementation and integration -> updated box-model design/plan -> box-model implementation. PR #5 (Block Box Tree) remains a separate subsequent design; none of its layout work is silently combined into this parser milestone.

## 2. Architectural choice and alternatives

**Chosen:** Create an independently testable internal module in \`src/FactsPDF/CssSyntax/\` (namespace \`FactsPDF.CssSyntax\`) with a one-way dependency toward primitive .NET types only. It does not reference \`HtmlTokens\`, \`InlineCss\`, \`CssStylesheets\`, \`PdfConverter\`, fonts or PDF serialization. The existing \`Css\` adapter depends on this module, never the reverse. Compile into the existing FactsPDF runtime assembly for now to avoid a new shipped DLL or package dependency; project/assembly extraction is a later explicit packaging decision, not an accidental breaking change.

**Rejected A:** Keep enlarging the current six-token \`CssTokens\` and single-value \`CssDeclarations\`. This entangles syntax with property support, makes CSS functions/shorthands/variables awkward, and prevents browser-like error recovery.

**Rejected B:** Depend on a browser, native Rust CSS engine, JavaScript, or a third-party CSS renderer at runtime. This could offer greater coverage quickly but introduces AOT/FFI, cross-platform distribution, licensing and footprint complexity before the own-core approach is measured.

The internal module must be designed for future extraction without claiming public or cross-language parser APIs today. Other language bindings continue using the *same* FactsPDF core.

\`\`\`text
HTML <style> RAWTEXT / HTML-decoded style attribute
                      |
              CSS Syntax Core
       preprocessor -> tokenizer -> rule parser
                      |
         immutable token/component/rule tree
           + structured parse diagnostics
                      |
              Semantic Adapter
       selectors | known property decoders
         unsupported-feature policy
                      |
       Existing Cascade & Computed Text Styles
                      |
            Existing Text Layout
                      |
           Existing PDF Serializers
\`\`\`

Syntax **never** needs an HTML node, target page size, network resource or PDF surface.

## 3. Input contract and source locations

Entry points: \`ParseStylesheet\` (for \`<style>\` content), \`ParseDeclarations\` (for \`style="..."\`, using the specification's block-content/declaration parsing rules without accepting arbitrary style rules as declarations), and direct \`Tokenize\` for tests/internal diagnostics. Accept a managed string or read-only character span and explicit options/cancellation; do not open files or fetch URLs.

- The HTML tokenizer continues to decide when an HTML \`</style>\` RAWTEXT end tag occurs, including within CSS comments/strings. The CSS parser **must not** search the surrounding HTML or reinterpret HTML entities. The HTML attribute tokenizer's decoded \`style\` value is the declaration-list input.
- Work with Unicode scalar values, but report \`SourceSpan\` (start/length in **original UTF-16 code units**, absolute HTML offsets provided by the adapter). CRLF/CR/FF preprocess to LF; NUL and lone surrogates preprocess to U+FFFD. Preserve a mapping to original positions when normalization changes length; line/column are derived on demand.
- A CSS BOM/encoding decision is not the parser's job when it receives already-decoded HTML strings. External CSS byte-stream encoding and \`@charset\` handling are out of scope because external stylesheet loading is not supported.
- Preserve original slices or normalized token values as appropriate. Token names (including custom-property names) retain their casing; ASCII case-folding for ordinary property matching belongs to the semantic adapter. CSS identifiers can contain non-ASCII characters and spec-conforming escapes.

## 4. Tokenizer: CSS Syntax Level 3 first

Tokenization must implement the specification's consume-token, identifier/number, escape, string, URL and comment handling, not a custom handful of CSS regular expressions.

At minimum support the normative token families: identifiers, functions, at-keywords, hashes (including ID-type flag), strings and bad-strings, URLs and bad-URLs, numeric/integer number, percentage, dimension tokens and their units, whitespace, delim, colon/semicolon/comma, match/prefix/suffix/substring operators, CDO/CDC, opening/closing parentheses/brackets/braces, EOF, and other tokens defined by the pinned specification.

Numerical token values use invariant parsing with finite/safe representation and retain the original lexeme for exact diagnostics. A valid CSS number token can use a sign, decimal point and exponent. Distinguish \`12px\`, \`12%\`, \`-0.5\` and \`1e3\` before any property validation. Reject impossible numeric conversion in the **semantic value** layer, not by erasing the syntax token.

Implement:
- CSS escapes including hexadecimal escapes, optional trailing escape whitespace, escaped delimiters and invalid escaped codepoints replaced by U+FFFD.
- Quoted strings with escaped newlines/characters, newline and EOF bad-string behavior.
- CSS comments removed according to spec; removal itself does **not** create descendant-combinator whitespace or concatenate token text in violation of token boundaries.
- \`url()\` versus function-token behavior and bad-url recovery; never dereference URLs.
- Comment, whitespace, BOM and UTF-16 normalization edge cases with offset fidelity.

All scanning loops check cancellation and increment finite token/work budgets. No regex with potentially uncontrolled backtracking; no dynamic compilation or reflection required.

## 5. Parser: rule/declaration/component-value grammar and recovery

Consume tokens into a typed, read-only syntax tree. Follow **October 2026 CSS Syntax Level 3** algorithm structure, including stylesheet contents, at-rules, qualified rules, blocks, declarations and nested contents. Do not constrain a declaration value to one token.

Internal result types (names can be refined in the implementation plan without changing contracts):
- \`CssSyntaxTree\` with ordered top-level \`CssRuleNode\`s plus diagnostics.
- \`CssQualifiedRule\`: component-value prelude, ordered body contents, source span.
- \`CssAtRule\`: case-preserving at-keyword name, prelude, optional block/termination information.
- \`CssDeclarationNode\`: case-preserving name, *component values*, optional \`!important\` flag, original span/order.
- \`CssComponentValue\`: a preserved token, a nested simple block or a function with ordered children, each with source spans.
- \`CssSyntaxDiagnostic\`: code, severity, source location and recovery action. Diagnostics never demand an HTML/PDF dependency.

Block content retains the **relative order** of declarations and nested rules per the 2026 spec; do not flatten all declarations in front of rules (which would prevent future CSS Nesting support).

Recover from malformed declarations, mismatched/unterminated blocks, bad-string/url tokens, unknown at-rules and missing delimiters as prescribed by the pinned spec. Parse errors and *unknown property/value/rule support* are **different things**: the syntax tree may hold a perfectly valid \`@media\`, \`display:grid\`, \`var(--x)\` or \`calc(100% - 2px)\` without asserting that FactsPDF can evaluate or render it.

\`!important\` extraction follows the declaration grammar, handles optional whitespace/comments and only treats the terminal important marker specially. Store custom properties such as \`--Theme\` with their exact case and token stream. Do not substitute variables, resolve functions or calculate layout in this milestone.

Parser failures should not throw for ordinary invalid CSS under tolerant syntax parsing; emit diagnostics and advance. **Only** invalid API contracts, explicit cancellation, and input/node/depth/work budgets may terminate core parsing. The adapter may elect to reject recovered syntax before PDF output.

## 6. Renderer bridge and compatibility modes

Replace the renderer's use of \`CssTokens.Read\`/\`CssDeclarations.Parse\` with a semantic adapter consuming the new syntax AST. Parsing syntactically valid CSS must not be coupled to whether \`TextLayout\` supports the property.

**Default: \`StrictPdf\` (behavioral-compatibility gate).** Existing FactsPDF strict rendering behavior remains the baseline:
- Recovered syntax errors -> existing \`FPDF1203\` or corresponding diagnostic before any PDF bytes.
- Unsupported selectors -> \`FPDF1203\`; unsupported/unknown property -> \`FPDF1201\`; unsupported at-rule -> \`FPDF1204\`; unsupported values -> \`FPDF1202\`.
- As today, the strict renderer may reject unsupported declarations even in nonmatching rules. Do not silently change this safety contract while migrating. Preserve existing file input, CSS budgets, error offsets and fail-before-write guarantees.

**Future opt-in: \`CompatibleSyntax\` (separate follow-up from core migration).** The syntax parser returns recovered trees and diagnostics; adapter can ignore invalid and unknown declarations in the browser manner, but a still-unsupported *layout-critical matched declaration* must either produce an explicit incomplete-render warning/signal or fail according to a documented safety policy. Do not market this as complete BrowserCompatible layout. Do not turn such a mode on by default in this milestone.

This staging cleanly separates three outcomes:
1. **Valid syntax + supported semantics** -> existing rendered behavior.
2. **Valid syntax + unsupported semantics** -> core accepts; strict renderer rejects with precise diagnostic.
3. **Invalid syntax** -> core recovers and records error; strict renderer rejects before output.

Do not add \`PdfOptions\` compatibility switches until the renderer has a tested, user-visible diagnostic policy. Keep the source-available/Commercial License files unchanged.

## 7. Existing CSS selector and cascade preservation

\`CssSelector\`, \`CssStylesheets\` and \`InlineCss\` currently depend on the old six-way token abstraction. Introduce a thin adapter to the new component tokens and migrate the consumer **without widening selector/property support accidentally**. In particular, preserve:
- type, class, ID, compound, descendant, child and comma-list selectors;
- per-property specificity, \`!important\`, inline precedence and source order;
- ordinary text-property inheritance and \`inherit\`/\`initial\`/\`unset\`;
- later \`<style>\` blocks affecting earlier document elements;
- CSS work limits, cancellation and per-conversion isolation.

Unicode and escaped identifier syntax is accepted by the tokenizer, but the semantic selector engine may initially reject selectors it cannot evaluate. The design explicitly avoids the false claim that parsing \`:is()\`, \`:has()\`, \`@layer\`, \`@page\`, CSS Nesting or \`var()\` implies implementing their behavior. Expansion of those semantic features gets separate acceptance tests and implementation milestones.

Do not rewrite or replace \`HtmlTokens\` or the HTML5 tree model as part of this CSS-only milestone.

## 8. Limits, security and AOT constraints

Preserve current \`MaxCssCharacters\` (262,144), \`MaxCssSelectors\` (4,096), \`MaxCssDeclarations\` (32,768), \`MaxCssMatchOperations\` (5,000,000), \`MaxElements\` and HTML depth budgets.

Add optional, bounded \`PdfOptions.MaxCssSyntaxNodes\` (default 131,072) and \`PdfOptions.MaxCssSyntaxDepth\` (default 64); validate both even when a stylesheet is empty, and carry the values into the syntax module through a minimal parser-options record. Token consumption work must be proportional to input size plus a documented bounded grammar-traversal factor. Node limits count nested component values, declarations and rule nodes, including recovered/unsupported constructs; do not let unknown at-rules evade budgets.

No external style fetching, file imports, \`@import\` network access, JS execution, arbitrary code eval, global shared mutable parser state, or user-supplied regex. Cache only immutable **per-document** results if useful; any cross-document cache needs a later explicit invalidation/eviction design and memory measurements. Re-entrant concurrent conversions should not leak styles or diagnostics. Include maliciously nested input, escape floods, huge numbers, unterminated constructs and cancellation in tests. Do not claim the core sanitizes hostile HTML/fonts or enforces a global memory ceiling.

## 9. Test and conformance strategy

**A. Unit tests of CSS Syntax Core**, against spec-grounded cases, with canonical output of token kind/value/span, rule tree and diagnostic/recovery position. Cover CRLF/FF, NUL/lone surrogates, Unicode identifiers, escape hex termination, numeric exponent/dimension/percentage, strings, bad URLs, nested function/block combinations, unknown at-rules, \`!important\`, custom-property case and parser recovery.

**B. Standards-based differential corpus.** Start from \`web-platform-tests/wpt/css/css-syntax/\` and selected CSSOM/Selector tests. Pin exact upstream revision(s), record case IDs, supported/unsupported classification and test expectations. WPT browser tests are not necessarily standalone AST fixtures: use a documented independent oracle/adaptation to compare **observable parser behavior**, not pretend the internal AST equals the browser's CSSOM objects. Respect source test license/attribution for any imported fixtures (WPT's BSD-3-Clause terms), and do not mislabel adapted tests as authoritative W3C certification.

**C. Renderer golden tests.** All **223** existing NUnit tests, including prior CSS error codes, selectors/cascade, font loading/fallback, Unicode/CJK, font subsetting and CLI protections remain green. Add equivalent before/after generated-PDF comparisons for supported templates and a strict-mode test proving no partial output on parsing errors or unsupported semantic values.

**D. CI:** Windows/Linux/macOS unit suites; Linux x64 **actual Native AOT publish and CLI execution**; existing PDF structure/text/page rendering checks and subsetting equivalence; dependency and exact-license package inspection. Add parser-focused mutation/fuzz/stress tests that complete within bounded time.

**E. Coverage reports:** report number of selected conformance cases (passed/failed/skipped) **by syntax feature**, rather than claiming a browser-compatibility percentage on a tiny handpicked set. Future 99% goals are not current FactsPDF measurements or release promises. Record representative parser throughput, allocation and peak RSS versus current parser under identical inputs on a specified runner; do not claim performance until measured.

## 10. Delivery boundary and migration sequence

After the written spec and implementation plan are separately approved:
1. Start red NUnit unit tests for tokenization and recovery *without changing the renderer*.
2. Implement spec-based tokenizer and source mapping. Verify token families and work/depth budgets.
3. Implement stylesheet/declaration parse entry points and AST/diagnostics. Test recovery and ordered nested content.
4. Build the old-subset semantic adapter with explicit unsupported-feature handling. Retain old externally observable strict mode and exact CSS cascade semantics; do not leave two competing parsers in the production path.
5. Add WPT-based differential tests, Unicode/boundary fuzz cases and real Native AOT/CI/package verification; gather resource baselines.
6. Publish a support matrix separating **syntax parsed**, **selector/property computed** and **layout rendered**. Update PR #5 dependency documentation and resume Block Box Tree implementation only after syntax migration is verified.

**Non-goals of this PR:** box-model implementation; new PDF shapes; expanded selector semantics; CSS variable evaluation; \`calc()\` arithmetic; \`@page\`, CSS Nesting evaluation, media-query evaluation, arbitrary style downloads, browser DOM/CSSOM APIs; WASM/non-.NET bindings; production-release/paid-package publication.

## 11. Documentation, ownership and license gates

- New CSS Syntax source is independently implemented from public standards, **not pasted** from Servo, Lightning CSS or browser source trees. No new runtime license obligations are silently introduced.
- Any copied/adapted WPT fixtures retain required upstream attribution/license. Record sources, revisions and any modifications in test fixture metadata. Do not distribute third-party font files in the repo or package.
- Existing FactsPDF Community License 1.1, Commercial License documentation and modification-disclosure policies are left intact.
- This architecture spec is reviewable in a draft **design PR**, not a production implementation. No merges, packages or releases by default.

### Reference documents
- Normative snapshot: https://www.w3.org/TR/2026/CRD-css-syntax-3-20261001/
- Living editor draft: https://drafts.csswg.org/css-syntax/
- WPT tests: https://github.com/web-platform-tests/wpt/tree/master/css/css-syntax
- WPT documentation: https://web-platform-tests.org/
- W3C test-suite license guidance: https://www.w3.org/copyright/test-suites-licenses/
