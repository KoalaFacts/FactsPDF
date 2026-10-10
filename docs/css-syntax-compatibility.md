# CSS Syntax compatibility and conformance scope

## What is actually measured

The repository's `CssSyntaxWptCases` has **12 selected original NUnit
assertions** inspired by observable behavior in
`web-platform-tests/wpt` at pinned commit
`521d168d63dbd206ea7fbe0b74ddd5760e6e668e`
(10 October 2026 reference), distributed across preprocessing,
tokenization and parser recovery.

Upstream repository: https://github.com/web-platform-tests/wpt
WPT licensing information: https://github.com/web-platform-tests/wpt/blob/master/LICENSE.md
Pinned test directory: https://github.com/web-platform-tests/wpt/tree/521d168d63dbd206ea7fbe0b74ddd5760e6e668e/css/css-syntax

The relevant upstream test paths are recorded in the
`CssSyntaxWptCases.Cases()` method. Individual assertions use new
FactsPDF test inputs and outcomes; no testharness.js source, copied WPT
test body, third-party library or font files are bundled in FactsPDF.
Some upstream WPT cases exercise CSSOM observables, not AST shape. Our
tests are *inspired by* these behaviors, not a claim of one-for-one
porting or executing the full original tests.

The CI job runs only the selected `CssSyntaxConformance` test category
and emits `conformance.json` with per-feature passed, failed and
skipped counts. It deliberately fails if the expected 12 selected cases
are absent or skipped. Other NUnit coverage (source maps, budget limits,
selector cascade, malformed inputs and PDF rendering) is required on the
full test matrix and is not counted as WPT cases.

**No browser compatibility percentage is currently justified.**
Passing 12 handpicked cases should not be shown as "100% CSS compatibility";
release-grade claims require broader upstream cases, browser-oracle
differential behavior tests, independent implementations and a larger
document corpus.

## Parsed versus computed versus rendered

| Capability | Syntax tree | Strict renderer |
| --- | --- | --- |
| Unicode CSS identifiers and escapes | Parses | Supported only where existing selector/property decoder can interpret them |
| Numbers, dimensions, %, strings and functions | Parses | Existing documented value grammar only |
| Unknown property `display:grid` | Retains declaration | Rejects `FPDF1201` |
| `@media print`, `@page` | Retains at-rule | Rejects `FPDF1204` |
| `calc()` / `var()` | Retains nested function tree | Does not evaluate |
| Nested CSS rules | Retains ordered rule/declaration contents | Does not evaluate nesting |
| Comments and malformed constructs | Tokenizes/recovery diagnostics | Strict mode rejects recovered errors |
| Existing eight supported properties | Parses | Computes and renders |
| PDF width/padding/border/background | Parse as unknown valid declarations | Not implemented; planned after parser stabilization |

## Resource measurements

The `FactsPDF.CssSyntax.Benchmarks` binary runs as a real Linux x64 Native
AOT executable in CI, with 96 fixed generated CSS rules, 10 untimed
warm-ups and 30 measured iterations. **One iteration** includes source
preprocessing, tokenization and AST parsing; it does not include HTML,
selector matching, cascading, layout, fonts or writing a PDF.

`median_current_thread_allocated_bytes` means *cumulative* managed
allocations, not resident or peak process memory.
`peak_process_working_set_bytes` includes process startup, warmups and
measurement overhead; it is not parser-only peak RSS. Timings are from
a shared CI runner with warm OS caches. None of these measurements alone
supports a universal performance comparison against Chromium or an
unmeasured old parser.

## Remaining gates

Before claiming high CSS Syntax coverage: pin a larger differential suite
with exact expected outcomes and document test adaptation, fuzz malformed
constructs and build an explicit supported/unsupported matrix for each
parser feature. Before production: independent code review, trusted-input
assumptions audit, sustained memory profiling, and full HTML/layout work.

The currently selected block-box design stays in separate PR #5.
