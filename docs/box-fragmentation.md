# FactsPDF M8 — Paginated Box Fragmentation

**Repository:** `KoalaFacts/FactsPDF`  
**Implementation:** [PR #14](https://github.com/KoalaFacts/FactsPDF/pull/14)  
**Base:** merged M7 `main` commit `34b0ddd28f8eb4a6d7b20aee20ed2b598827ea87`

## Outcome

Previously, a painted `body`, `div`, `section`, `article` or paragraph box that crossed a page boundary failed with `FPDF1302`. M8 retains M5/M6 block topology and M7 layout/painting but emits **one paint fragment per occupied page** for a decorated ancestor that spans more than one page. Text remains searchable; its existing normal line wrapping, margins and font fallback continue to determine page positions.

The CSS fragmentation default is **slice**, not clone. A spanning block is a continuous box:

- First page: paint its background, its top border and its two side borders, from the block top through the page's bottom content margin.
- Intermediate page(s): background and side borders from top to bottom of the usable page; no top/bottom border or repeated top/bottom padding.
- Final page: background, both side borders, and bottom border; do not repeat the top border or top padding.
- A one-page box remains exactly the M7 box (both horizontal edges). Top and bottom margins of paragraph boxes remain **outside their own** painted rectangles.
- Nested painted blocks retain their X/width, source order and parent-before-child compositing on every page. A painted ancestor may also span pages because of an otherwise unpainted but padded child.

## Internal implementation

`PaintedBox` adds default-on flags `IsFirstFragment` and `IsLastFragment`, preserving existing M7 constructor behavior. Its `CommandCount` and the *shared* `PdfPaintSerializer` both respect these flags, so the output byte and command budgets count only the rectangles actually emitted. The painter remains shared by ASCII and Unicode PDF serializers, uses bounded graphic commands and restores PDF graphics state around each fill.

`TextLayout` retains each open block's originating page, X, outer width, style, color, order and border-box start. On closing a decorated block it projects its occupied interval to page-local `PaintedBox` slices:

- `[block start, page usable bottom]` on the first page;
- `[page usable top, page usable bottom]` on intermediate pages;
- `[page usable top, block end]` on the final page.

Coordinates use PDF points and the configured `PdfOptions` geometry. If a box's computed start/end lies outside a supported printable area, the engine continues to return a meaningful `FPDF1302`, rather than clipping or generating a phantom page. Explicit `break-before/after:page` remains supported for paragraphs/headings; both within-box and repeated explicit page transitions are covered.

The unchanged `BlockLayout` traversal remains lazy and iterative. CSS syntax/cascade, fonts, text encoding, license files, public converter contract and Native AOT dependency policy are unchanged.

## Test and visual acceptance

`BoxFragmentationTests` cover first/middle/last flags and command counts, exact usable page bounds, per-page backgrounds/side edges, a 3-page box, explicit page breaks, a long naturally paginated paragraph, nested painted containers, unpainted padding before a page break, no trailing phantom page, `MaxPages`, `MaxDisplayCommands`, cancellation and no-output-on-failure. Existing M7 strict-negative tests for painted boxes that now legitimately span pages have become positive regression assertions.

CI's `css-box-visual.yml` now runs **`--mode compare` on all five** original HTML fixtures against *real Linux Native AOT FactsPDF and independent Chrome PDFs*: solid background, four borders, asymmetric padding, 70% nested width and a 3-page sliced container. The three-page test must verify text/pages, actual fill colors on every page, side edges on all pages, and exactly the first-page top and last-page bottom horizontal edge. The comparison records independent pixel metrics and page overlays but does not claim browser-level typography fidelity from a handful of samples. The prior M7 reference-only expected-error mode is **not** an M8 acceptance gate.

## Explicit exclusions

No clone-style border repeat, `box-decoration-break` declaration support, arbitrary CSS page rules, widows/orphans, keep-together, tables, images, inline box decoration, Flex/Grid, float/position, scroll clipping or general browser HTML5 tree construction. Unresolvable geometry and indivisible top+first-line requirements remain explicit errors; there is no promise of unrestricted browser fragmentation.

**Process:** only PR #14 active. Exact-head NUnit on Windows/Ubuntu/macOS, Linux Native AOT, independent PDFs/Chrome visual oracle, CSS/font/subsetting workflows, independent code review and resolved findings before merging. Merge and verify `main` before any later milestone. No release/package publication.
