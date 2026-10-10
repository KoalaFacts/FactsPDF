# M7 — Border / Background verification ledger

- Repository: `KoalaFacts/FactsPDF`
- Only feature PR: [#13](https://github.com/KoalaFacts/FactsPDF/pull/13)
- Branch: `feat/block-border-background-paint`
- Base merged M6 `main`: `aa793d6f51cd97c94a775cf3d9bd815c07a2e52e`
- Intended merge boundary: pass final-head CI, visual comparison, independent review; **then merge M7 before any M8 work**, as the user requires.
- License, contribution and package publication: unchanged, none.

## Observed staged work
1. RED: `4bfe16d8` adds M7 CSS paint and PDF contract tests before new types/serializer support. [CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/38049718466) expected compile errors for missing `BackgroundColor`, `BorderTop`, `PaintBoxes` and `MaxDisplayCommands`.
2. `4d60bd7c`: computed `BoxStyle`, per-side border declarations/cascade, strict paint-value decoder.
3. `4e7a2dbb`: border-aware containing box geometry, page-local painted box records, single shared ASCII/Unicode PDF painter, bounded display commands. Initial [CI run 38050103363](https://github.com/KoalaFacts/FactsPDF/actions/runs/38050103363) compiled and ran, but found **three old-suite failures**: one obsolete `border` rejection assertion and two CSS diagnostic offset regressions.
4. `0ef90b0e`: restored CSS token value offset, while replacing now-obsolete border-rejection test with a real border-positive assertion. [CI run 38050210625](https://github.com/KoalaFacts/FactsPDF/actions/runs/38050210625) reported **398/398 Ubuntu tests passing**.
5. `9bc63a1d`: introduced staged real browser/Native-AOT box painting comparison (`--mode m7`) and explicit M8 fragmentation-error gate; unlike old `--mode reference`, this **cannot pass without actual FactsPDF rendered colors**.
6. `d31decf1`: added real CJK glyph-backed painted PDF, invisible paint byte parity and budget regression tests.


## Independent review and reproducible fixes

GitHub [Codex review on `4567c2a`](https://github.com/KoalaFacts/FactsPDF/pull/13) reported four actionable **P2** correctness issues:
- Painted paragraph rectangles incorrectly included paragraph `margin-bottom` and painted an empty paragraph's default 8pt margin.
- `break-before` (or previous `break-after`) on an otherwise one-page painted paragraph incorrectly treated the box as page-spanning.
- Invalid later shorthand terms returned the declaration's source start instead of the actual failing CSS value token offset.
- Display-command count charged zero-area primitive rectangles which the painter does not emit.

Commit `1f84b545` added these independent reproduction tests; follow-up `604f3855` corrected the naturally overflowing one-page fixture's page geometry. The [RED CI 38051083283](https://github.com/KoalaFacts/FactsPDF/actions/runs/38051083283) compiled but reported **401 passed / 11 failed** (test variants including four shorthand offsets). Commit `f38811a4` implements fixes: excludes margins from paint height, re-anchors unconsumed block starts on page breaks, preserves shorthand token spans, and charges only rectangles actually emitted. [GREEN CI 38051203301](https://github.com/KoalaFacts/FactsPDF/actions/runs/38051203301) passed **412/412 NUnit** on Ubuntu. Cross-platform, Native AOT, real browser visual comparison and fresh independent review must be verified on the **final head**, not inferred from this individual run.

## Remaining gates
- Inspect exact-head Chrome M7 visual reports: four paired native PDFs must have colored fills/borders and matching text/page count; three-page case must produce real pre-write `FPDF1302`, not a PDF.
- All current-head Windows/Ubuntu/macOS, Linux x64 Native AOT, stylesheet/subsetting/CSS Syntax, old PDF differential and M7 oracles.
- Independent Codex PR review with all P1/P2 findings resolved. Merge only the verified PR into `main`, then verify merged-head CI. Do not open M8 concurrently.
