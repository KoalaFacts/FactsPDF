# CSS Syntax Core implementation and verification ledger

**Plan:** `docs/superpowers/plans/2026-10-10-css-syntax-core.md`
**Spec:** `docs/superpowers/specs/2026-10-10-css-syntax-core-design.md`
**Base:** `main` `8c303bda7a0fe1875f5376839a4bc7f4ebf8dc6e`
**Feature branch:** `feat/css-syntax-core`, draft PR #7.
**Date:** 10 October 2026, Australia/Sydney.
**Status:** implementation and CI verification performed; independent human/fresh-context code review not completed. **Not merged or released.**

## Architecture implemented

The new `FactsPDF.CssSyntax` module contains source preprocessing with UTF-16
source maps, bounded CSS tokenizer, component values, declarations, ordered
qualified/at-rules, CSS parsing recovery diagnostics and cancellation checks.
The old production CSS tokenizer and one-token declaration parser were
replaced by a semantic adapter to the independent grammar; the StrictPdf
renderer continues to reject unsupported features before output. The
existing HTML RAWTEXT boundary, selectors, cascade, Unicode and font
subsetting remain on the same shared renderer core.

See [technical guide](css-syntax-core.md) and
[compatibility scope](css-syntax-compatibility.md). Parsing the AST is **not**
equivalent to browser CSS layout support.

## Test-first sequence and observed CI

- Initial incremental feature branch began with tests and intentional stub
  contracts. Its red/green commits for preprocessing, numeric tokens,
  identifiers/escapes, string/URL tokens, AST, and adapter remain in PR #7's
  commit history.
- `37d87db`: CSS runtime switched away from the obsolete tokenizer.
  [CI 38013875265](https://github.com/KoalaFacts/FactsPDF/actions/runs/38013875265)
  passed all platform jobs, with the inspected Ubuntu log reporting **284/284
  NUnit cases passed**; CSS syntax selection, stylesheet equivalence and
  subsetting workflows also passed on the exact commit.
- `178911e`: added seven focused stress/standards regressions. Inspected
  Ubuntu job 114108009579 in
  [CI 38016556354](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016556354)
  reports **289 passed / 2 failed**, including a real nested at-rule parent
  boundary defect. The other failure used an insufficiently tight MaxNodes
  test threshold; its test was corrected rather than declaring an engine bug.
- `675a372` and `497fdac`: corrected the node-budget test, then fixed
  nested at-rule termination before a parent `}`. Exact-head
  [CI 38016640501](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016640501)
  passed Windows, macOS, Ubuntu and actual Linux x64 AOT; **291 tests**
  passed. The stylesheet/subsetting evidence jobs passed.
- `5ac3981`: added top-level stray-right-brace regression from
  CSS Syntax Level 3. Inspected Ubuntu job 114108799156 in
  [CI 38016812470](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016812470)
  reported **291 passed / 1 failed**: the parser had incorrectly dropped
  the unmatched brace instead of retaining it in the invalid qualified-rule
  prelude.
- `0fc0444`: corrected qualified-rule parsing to distinguish nested and
  top-level `}`. All four jobs passed on
  [CI 38016873659](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016873659),
  including **292 NUnit cases**. Dedicated syntax, stylesheet and font
  subset-equivalence workflows passed.
- `423888d`: added an independent **Linux x64 Native AOT syntax-only
  measurement harness**. All four workflows passed, including
  [CSS Syntax evidence 38016966004](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016966004)
  and [CI 38016965946](https://github.com/KoalaFacts/FactsPDF/actions/runs/38016965946).
  Its downloaded artifact `11656931928` has SHA-256
  `2ecaa37dee2ecc1c65015daf29e7f646578d6b445b33827deec78a3db3fcc76d`.
- `9210c59`: added syntax/render capability documentation and preserved the
  README licensing text byte-for-byte. Post-documentation
  [CI 38017054645](https://github.com/KoalaFacts/FactsPDF/actions/runs/38017054645)
  passed Windows/macOS/Linux and Linux x64 Native AOT; the inspected Ubuntu
  log reported **292/292 passed**. Dedicated syntax
  [run 38017054675](https://github.com/KoalaFacts/FactsPDF/actions/runs/38017054675),
  stylesheet [run 38017054691](https://github.com/KoalaFacts/FactsPDF/actions/runs/38017054691),
  and native subsetting [run 38017054789](https://github.com/KoalaFacts/FactsPDF/actions/runs/38017054789)
  all passed.

These are **actual GitHub Actions executions**, not claims based only on
workflow YAML. Compiler/AOT execution occurred in CI because the editing
container lacked a .NET SDK and outbound network access.

## Selected WPT-inspired behavior checks

Pinned upstream WPT commit:
`521d168d63dbd206ea7fbe0b74ddd5760e6e668e`.
All **12 original NUnit assertions** inspired by pinned upstream test paths
passed: preprocessing 1/1, tokenizer 5/5, parser 6/6. None failed or were
skipped. The original browser WPT suite was **not** run; these assertions
are not a full coverage percentage or standards certification.

## Native syntax-only measurement snapshot (run 38016966004)

- Input: 96 generated selector/declaration rules, 10,070 UTF-16 characters.
- Platform: Linux x64 Native AOT, .NET runtime 10.0.12, Ubuntu 24.04.5 LTS.
- 10 untimed warm-ups + 30 measured iterations.
- Median time per iteration (preprocess + tokenize + parse AST): **0.4776 ms**.
- Median cumulative current-thread managed allocations: **1,417,912 bytes**.
- Whole-process peak working set: **18,845,696 bytes** (about 18.0 MiB).

The 30 raw samples, runtime metadata and measurement scope are in
`benchmark.json` inside artifact `css-syntax-evidence`. This is not a
cold-start measurement, total PDF conversion metric, parser-only RSS value,
or a controlled A/B comparison with Chromium or the prior FactsPDF parser.
Treat it as a first reproducible, single-corpus baseline only.

## Review notes and limits

Self-review identified and test-first repaired two parsing boundaries:
nested at-rules leaving the parent delimiter intact, and top-level stray
closing delimiters remaining in the invalid qualified prelude. A wrongly
chosen node-budget test threshold was corrected separately.

**Review level: implementer self-review.** No fresh-context reviewer or
independent human reviewer was available for this pass; independent qpdf,
Poppler and tests check generated PDF behavior, not the implementation's
complete security or standards coverage. A further independent code review,
larger WPT differential corpus, fuzzing, source-map security review and
document-level performance baselines are still required before production.

The CSS core remains logically internal to the shared FactsPDF assembly,
adds no native/third-party runtime CSS parser and performs no network or
filesystem fetching. The benchmark is a separate test-only executable;
no benchmark executable, font or NuGet package is shipped.

## Rulings

- **Ruling:** The existing `feat/css-syntax-core` branch/PR #7 was already
  populated with working TDD commits when native execution resumed. Reused
  and verified that existing work rather than overwriting commits with a
  second implementation. **Cost if wrong:** those earlier commits require
  independent review to validate details not exercised by the current
  expanded regression suite.
- **Ruling:** The node-budget regression was recalibrated from 12 to 3 so
  the AST fixture actually exceeds the limit. A passing budget assertion
  should not be invented from insufficient fixture work. **Cost if wrong:**
  the maximum-node boundary requires a larger corpus or direct budget unit
  test (also present) to demonstrate exact exhaustion counts.
- **Ruling:** Keep the selected WPT tests described as *WPT-inspired* until
  the original browser harness or a documented differential oracle is run.
  **Cost if wrong:** lower confidence than a full upstream conformance
  suite; no CSS compatibility percentage can be claimed.

## Release and box-model boundary

This feature PR stays **draft** and is not merged or packaged. `main`
remains at the verified `8c303bda` baseline unless someone independently
updates it. Existing FactsPDF Community License 1.1, Commercial License,
contribution requirements and legal release gates remain unchanged.

Block Box Tree / box-model design is in separate [PR #5](https://github.com/KoalaFacts/FactsPDF/pull/5)
and must be rechecked against this AST adapter after parser integration.
