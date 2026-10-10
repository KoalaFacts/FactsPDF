# M7 — Single-page border and background painting

**Repository:** `KoalaFacts/FactsPDF`  
**PR:** [#13](https://github.com/KoalaFacts/FactsPDF/pull/13)  
**Base:** M6 `main` `aa793d6f51cd97c94a775cf3d9bd815c07a2e52e`

## Supported
M7 implements painted **single-page** nested blocks on the retained M5 tree and M6 content geometry. It adds no browser runtime, public HTML DOM, font files, package dependency or external resource access.

- `background-color`: `transparent` or the documented text-color palette (`#rgb`, `#rrggbb`, existing named colors).
- `border` and `border-top/right/bottom/left`: order-insensitive width/style/color shorthands; width `pt`, `px`, unitless zero and `thin`/`medium`/`thick`; styles `solid`/`none`; colors from the same limited palette or `currentColor`.
- `border-width`, `border-style`, `border-color`: 1–4 value top/right/bottom/left expansion, plus every side's longhand width/style/color.
- CSS-wide `inherit`, `initial` and `unset` for each paint longhand. Paint properties are non-inherited. Shorthands expand **before** per-property winner resolution, retaining `!important`, inline vs stylesheet specificity and declaration source order. A shorthand's derived declarations count against the existing CSS declaration budget.
- Side with `none` style has zero effective border width regardless of a specified width. Border `currentColor` resolves to the block's final computed text color when painting. Missing background is transparent.
- Border widths participate in M6 containing-block geometry (content-box width, child content X and overflow detection); padding percentages still resolve against containing content **width**.

## Painting model
`BlockLayout.Steps()` carries outer-box X, width, effective top/bottom border widths, final style and source offset. `TextLayout` records block top/bottom and saves complete single-page `PaintedBox` geometry. `PdfPaintSerializer` emits **one shared graphics stream** of `q R G B rg x y width height re f Q` rectangle operations. The ASCII and Unicode serializers call the *same* painter before their text operators and preserve existing font selection, Unicode maps, PDF objects and font subsetting.

Backgrounds are painted before four separately colored solid edges. Ancestors paint before children; paint commands as a whole precede text for deterministic backgrounds behind searchable words. Unpainted documents are serialized through the same original path with no graphics commands, preserving representative legacy PDF bytes.

`PdfOptions.MaxDisplayCommands` (default 200,000) bounds **actually emitted** text and primitive rectangle operations across all pages; zero-height/zero-width rectangles are not charged. Overflow fails `FPDF1401` before caller-stream writes.

## Explicit M8 boundary
This increment does **not** claim CSS Fragmentation Level 3. A decorated box that occupies more than one page, or extends beyond the printable vertical area, throws `FPDF1302` **before any output bytes**. Existing unpainted multi-page text remains supported. A single-page painted box close to the page bottom is not automatically moved intact to the next page in this increment; avoid designs that require break-inside avoidance until M8.

Paragraph margins are **outside** backgrounds and borders, including the default `p` bottom margin and explicitly empty paragraphs. A painted paragraph with `break-before:page` (or following a sibling `break-after:page`) may start on the new page intact. Similarly, when the first text line forces a new page, the box's unconsumed top padding travels with it; a box with text already laid out on a prior page still fails `FPDF1302` rather than claiming M8 fragmentation.

CSS validation preserves the original source position of **each shorthand component** (including later invalid border colors/padding terms), not merely the start of the shorthand declaration.

The five pre-existing Chrome/PDF oracles now support `--mode m7`: independently compare the **four single-page** original HTML templates against real Native-AOT FactsPDF PDFs, including actual fills, four colored borders, asymmetric padding and nested 70% width. The three-page box remains an explicitly verified `FPDF1302`/no-output case, *not* a fake cross-engine pass. `--mode compare` remains reserved for M8 when real fragmentation is implemented.

## Deliberate exclusions
No box `height`, margin-left/right, table, image, Flex/Grid, border-radius, dashed/dotted borders, background images, inline span decoration, full HTML5 DOM, SVG or cross-page filled fragments. Unsupported properties/values produce existing strict errors with retained CSS source offsets.

## Quality gate
NUnit tests cover CSS value grammar, shorthands and overrides, border geometry, colored PDF rectangles, nested paint order, empty painted boxes, no-op paint parity, Unicode/ASCII stream parity, max command/declaration budgets, cancellation, locale invariance and rejection of decorated multi-page boxes. Cross-platform .NET 10 CI, real Linux x64 Native AOT, existing stylesheet/font/subsetting regression suites, independent real PDF/Chrome paint probes, and code review must all pass before merging. M8 begins only **after** M7 merges to `main`.

