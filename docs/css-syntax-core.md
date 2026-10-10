# CSS Syntax Core — development support

FactsPDF now has a logically independent CSS Syntax Level 3 implementation in
`src/FactsPDF/CssSyntax/`, compiled into the same shared renderer assembly.
This is an **experimental syntax implementation**, not a browser CSS engine.
It does not perform layout, network I/O, HTML parsing, CSS custom-property
substitution or calculated-style resolution.

## Two different levels of CSS support

1. **CSS syntax**: preprocessing, tokenizer, component values, functions,
   simple blocks, ordered qualified and at-rules, declaration lists and
   recoverable diagnostics. The tokenizer accepts Unicode names and CSS
   escapes; the parser can retain valid-but-unknown CSS syntax in its AST.
2. **Rendered CSS semantics**: the current strict adapter still supports
   only the documented selectors and eight text/paragraph properties.
   Valid `display:grid`, `@media`, `var()` or `calc()` syntax does
   *not* imply that FactsPDF can render or evaluate it.

### Runtime behavior

The core syntax layer records parse recoveries instead of throwing for
ordinary invalid CSS. **The renderer remains strict by default**: malformed
stylesheet syntax, unsupported selectors, unknown declarations or unsupported
property values fail conversion *before the final PDF is written*. This is
intentional: suppressing an unsupported layout property could silently
produce a misleading PDF.

Existing diagnostics are preserved: `FPDF1201` (unsupported property),
`FPDF1202` (unsupported value), `FPDF1203` (unsupported selector or
syntax recovery), `FPDF1204` (unsupported at-rule) and `FPDF1205`
(CSS syntax resource budget). Source offsets use the original HTML's
UTF-16 positions, including decoded inline style attributes.

HTML `<style>` text is handled as RAWTEXT by `HtmlTokens`; inline `style`
attributes are HTML-decoded before they enter CSS. CSS Syntax does not fetch
URLs, read imported stylesheets, use dynamic code or discover local fonts.

### Resource limits

| Option | Default |
| --- | ---: |
| `MaxCssCharacters` | 262,144 |
| `MaxCssSyntaxNodes` | 131,072 |
| `MaxCssSyntaxDepth` | 64 |
| `MaxCssSelectors` | 4,096 |
| `MaxCssDeclarations` | 32,768 |
| `MaxCssMatchOperations` | 5,000,000 |

The limits bound named operations and graph size; they are not a global
peak-memory or hostile-document sandbox guarantee. Per-conversion state
and cancellation prevent cross-document state leaks.

## Compatibility boundaries

The semantic layer continues to support type/universal/class/ID/compound
selectors and descendant/child combinators, `!important`, source order
and text-style inheritance. The renderer does not yet implement
`@media`, `@page`, Nesting evaluation, arbitrary pseudo/attribute/sibling
selectors, `var()` substitution, `calc()` arithmetic, width, borders,
padding, backgrounds, Flex/Grid, images or tables. These features may be
represented in the syntax AST while still being explicitly rejected in
StrictPdf mode.

The next major stage is the nested Block Box Tree and basic box model
design in [PR #5](https://github.com/KoalaFacts/FactsPDF/pull/5);
syntax-core compatibility is a prerequisite, not proof that layout is
ready.

## Conformance and performance evidence

The normative reference for implementation is
[CSS Syntax Module Level 3, W3C CRD 1 October 2026](https://www.w3.org/TR/2026/CRD-css-syntax-3-20261001/).
The selected testing corpus uses
`web-platform-tests/wpt@521d168d63dbd206ea7fbe0b74ddd5760e6e668e`.
These are original **WPT-inspired NUnit assertions** mapped to pinned test
paths, *not* the full upstream WPT browser harness or W3C certification.
See [compatibility and coverage](css-syntax-compatibility.md).

The CSS-syntax workflow builds a **native Linux x64 AOT benchmark harness**
and records its narrow parser-only measurements in JSON, independently from
the end-to-end PDF/CLI performance benchmarks. It records a warm-run
single-corpus median, raw samples, current-thread managed allocation and
whole-process peak working set. These are observations of shared CI hardware,
not guaranteed throughput, startup time or lower resource use across all
stylesheets; see the generated run artifact and development ledger.

No extra runtime parser, browser, JavaScript or PDF engine dependency has
been added. Existing Community/Commercial licensing and contributor
policies are unchanged.
