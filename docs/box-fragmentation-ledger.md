# M8 — Box Fragmentation engineering ledger

- Repository: `KoalaFacts/FactsPDF`; sole feature PR [#14](https://github.com/KoalaFacts/FactsPDF/pull/14)
- Base merged M7 `main`: `34b0ddd28f8eb4a6d7b20aee20ed2b598827ea87`
- Intended scope: page-local first/middle/last box fragments, background and side-border continuation, no repeated horizontal edges, real visual comparison.

## Actual execution sequence

1. **RED** commit `37bf8a6655fde66c8901eb5a11e2ea451e915553`: new `BoxFragmentationTests` before implementation. Expected missing model flags/compiler failure; cross-engine fixture still in M7 mode. [RED main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054630819).
2. Implementation `abcbd94b2f4722ebe7f79bba9ff8395dfd68d83e`: default-on `PaintedBox` first/last flags, per-page fragment geometry in `TextLayout`, sliced-edge logic in shared PDF painter. [Ubuntu CI 38054829389](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054829389) compiled with **444/445 tests passing**; the sole failure was an obsolete M7 test asserting cross-page painted boxes must fail.
3. Fixture gate `d336bebeba508d773d3287d49d18b35a3a90c842`: converts that obsolete test into positive continuation assertions and changes the independent Chrome visual workflow from four-page-case `m7` mode to **full five-fixture `compare`**, including the three-page sliced box. [CI run 38054940384](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054940384) reports **445/445 passing on Ubuntu**. Verify all other exact-head jobs and the actual M8 visual comparison report separately.

4. Commit `32450b6d811e029278ad1cd2626cfabe467847dc`: adds Unicode/ToUnicode fragmentation, exact per-page display command budgets, invisible style byte parity, nested paint order and output-limit tests. [Ubuntu CI #38055176265](https://github.com/KoalaFacts/FactsPDF/actions/runs/38055176265) reported **450/450 passing**, with final cross-platform/AOT and visual runs to be checked on the exact head.
5. [Real cross-engine run #38054940484](https://github.com/KoalaFacts/FactsPDF/actions/runs/38054940484): `--mode compare` independently produced both PDFs for **all five fixtures**; the four single-page panels and **the actual three-page first/middle/last sliced decorated block** were marked `compared`. The independent raster checks include background and side-border paints on each page, first-only top and last-only bottom, matching text/page counts, page boundary checks and pixel/ink difference images. This was verified on `d336bebe`; a subsequent exact-head verification is still required after later commits.

Every later commit must update its exact-head CI and independent-review evidence before merge. No merge/release/M9 is authorized by this ledger without verification. No licensing changes.
