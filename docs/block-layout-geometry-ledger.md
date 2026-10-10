# M6 — Block Layout Geometry verification ledger

- Repository: `KoalaFacts/FactsPDF`.
- Only active feature PR: [#12](https://github.com/KoalaFacts/FactsPDF/pull/12), branch `feat/block-layout-geometry`.
- Parent `main`: `2e33d8accbd0e19902482373188a304be15a3ae5` (merged structural M5).
- Strict scope: width, padding and nested text geometry. No background, border, fragment painting, licensing or release changes.

## TDD execution (real GitHub Actions)

1. **RED** commit `b70994420ea3195ce307ba0541609ce1163e1e80` introduced the new geometry-specific NUnit tests before implementation. [CI run 38047024456](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047024456), Ubuntu job `114198497984`, failed at compile time (missing `BlockNode.BoxStyle`), as required.
2. Initial implementation `58950cf08ad4b90e87de2f66aa00b39909554504` added `CssBoxValues`, per-side cascade, `BoxStyle`, the block-geometry walker and width-aware text line wrapping. [Run 38047179930](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047179930) uncovered a C# local-name shadowing diagnostic `CS0136`; this was an actual compile bug, not a passing run.
3. Fix `fc2dbc67291a4a8863e01cb2dc2e410898f5525e` renamed the conflicting closing-block local. [CI run 38047231249](https://github.com/KoalaFacts/FactsPDF/actions/runs/38047231249) succeeded; Ubuntu NUnit reported **361/361 passed**, with Linux Native AOT and all current browser/syntax/stylesheet/subsetting workflows green.
4. Final hardening: unitless decimal zero, inheritance/padding-boundary/page-break regressions, and design docs. **Recheck exact last head and all CI before treating milestone as verified.**

No merge or package release is authorized by this ledger. For visual oracle cross-engine comparison, future M7/M8 must implement painting and page fragments; the M6 suite correctly tests text geometry and keeps paint-related declarations unsupported.
