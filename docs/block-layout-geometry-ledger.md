# M6 — Block Layout Geometry verification ledger

- Repository: `KoalaFacts/FactsPDF`.
- Only active feature PR: [#12](https://github.com/KoalaFacts/FactsPDF/pull/12), branch `feat/block-layout-geometry`.
- Parent `main`: `2e33d8accbd0e19902482373188a304be15a3ae5` (merged structural M5).
- Strict scope: width, padding and nested text geometry. No background, border, fragment painting, licensing or release changes.

## TDD execution (real GitHub Actions)

1. **RED** commit `b70994420ea3195ce307ba0541609ce1163e1e80` introduced the new geometry-specific NUnit tests before implementation. [CI run 38047024456](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047024456), Ubuntu job `114198497984`, failed at compile time (missing `BlockNode.BoxStyle`), as required.
2. Initial implementation `58950cf08ad4b90e87de2f66aa00b39909554504` added `CssBoxValues`, per-side cascade, `BoxStyle`, the block-geometry walker and width-aware text line wrapping. [Run 38047179930](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047179930) uncovered a C# local-name shadowing diagnostic `CS0136`; this was an actual compile bug, not a passing run.
3. Fix `fc2dbc67291a4a8863e01cb2dc2e410898f5525e` renamed the conflicting closing-block local. [CI run 38047231249](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047231249) succeeded; Ubuntu NUnit reported **361/361 passed**, with Linux Native AOT and all current browser/syntax/stylesheet/subsetting workflows green.
4. Hardening commit `4c1f9686ca42861b5d2caa9433ac596e6beb3730` added unitless decimal zero, inheritance/padding-boundary/page-break regressions and design docs. [CI run 38047413657](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047413657) passed **366/366** NUnit, Windows/macOS/Ubuntu and real Linux Native AOT. Commit `ceffa79bfa1abc62b059f86b964fa5786fa02aa5` added an assertion against the actual ASCII PDF content stream; [CI run 38047549622](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047549622) passed **367/367** NUnit and all eight workflows.
5. Independent GitHub Codex review on `ceffa79b`, [review](https://github.com/KoalaFacts/FactsPDF/pull/12#pullrequestreview-5478753941), identified a real **P2**: a closed block's `padding-bottom` was carried onto the next page after `break-before/after`. Independent tests on commit `7339d9524944e068bc95d9795a85750701b17396` reproduced two failures, including overflow pagination: [RED CI 38047910271](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047910271) reported **368 passed / 2 failed**. The review's forced-break example is now a dedicated regression.
6. Fix `48c7fa31c347d9bfc6cb4f719555116d5e954d90` separates pending top from pending bottom padding in text layout. Starting padding can travel with the next content line; previous box's closing padding is discarded at a page boundary. **Recheck exact last head and all CI before accepting the fix or merging.**

No merge or package release is authorized by this ledger. For visual oracle cross-engine comparison, future M7/M8 must implement painting and page fragments; the M6 suite correctly tests text geometry and keeps paint-related declarations unsupported.
