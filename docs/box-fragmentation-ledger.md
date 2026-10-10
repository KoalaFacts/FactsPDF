# M8 — Box Fragmentation engineering ledger

- Repository: `KoalaFacts/FactsPDF`; sole feature PR [#14](https://github.com/KoalaFacts/FactsPDF/pull/14)
- Base merged M7 `main`: `34b0ddd28f8eb4a6d7b20aee20ed2b598827ea87`
- Intended scope: page-local first/middle/last box fragments, background and side-border continuation, no repeated horizontal edges, real visual comparison.

## Actual execution sequence

1. **RED** commit `37bf8a6655fde66c8901eb5a11e2ea451e915553`: new `BoxFragmentationTests` before implementation. Expected missing model flags/compiler failure; cross-engine fixture still in M7 mode. [RED main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054630819).
2. Implementation `abcbd94b2f4722ebe7f79bba9ff8395dfd68d83e`: default-on `PaintedBox` first/last flags, per-page fragment geometry in `TextLayout`, sliced-edge logic in shared PDF painter. [Ubuntu CI 38054829389](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054829389) compiled with **444/445 tests passing**; the sole failure was an obsolete M7 test asserting cross-page painted boxes must fail.
3. Fixture gate `d336bebeba508d773d3287d49d18b35a3a90c842`: converts that obsolete test into positive continuation assertions and changes the independent Chrome visual workflow from four-page-case `m7` mode to **full five-fixture `compare`**, including the three-page sliced box. [CI run 38054940384](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054940384) reports **445/445 passing on Ubuntu**. Verify all other exact-head jobs and the actual M8 visual comparison report separately.

Every later commit must update its exact-head CI and independent-review evidence before merge. No merge/release/M9 is authorized by this ledger without verification. No licensing changes.
