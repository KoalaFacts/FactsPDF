# M6 — Block Layout Geometry (width / padding)

This milestone extends the already-merged structural Block Box Tree with **real containing-block geometry for text only**. It does not paint backgrounds/borders or generate page box fragments.

## Inputs and internal data

- The CSS Syntax Core remains the only token/declaration parser. Its strict adapter now accepts `width`, `padding`, and `padding-top/right/bottom/left` on `body/div/section/article/p/h1..h6`. Those declarations on `html/span/br` remain invalid (no inline box decoration).
- `width` supports `auto`, finite nonnegative `pt`, `px` (0.75 PDF pt), percentages, and numeric zero. All padding sides accept the same lengths other than `auto`; 1–4 whitespace-separated padding values expand top/right/bottom/left. Unitless nonzero lengths, functions, negative values, or unsupported declarations fail before writing.
- `inherit`, `initial`, `unset` work for the new non-inherited box longhands. Shorthand expansion is budgeted as four declarations. Longhand winners use existing `!important`, inline precedence, selector specificity and source order.
- `BlockNode.BoxStyle` retains these specified values independently from `BlockNode.Style` (existing text style), including percentages until parent content width is known. No new public API is added.

## Resolution and layout

At the root, horizontal available width is `PageWidth - 2 * Margin`. Each block resolves percentage width and **all four percentages of padding** against its parent's content width, never its page height. Declared width is the **content box**, not the outer width. Auto content width subtracts left/right padding. Explicit outer widths extending beyond the parent fail with `FPDF1302` and the block source offset rather than silently clipping.

`BlockLayout.Steps()` iteratively traverses actual blocks without creating a global list of paragraphs. It yields block begin/end vertical padding and text leaves carrying content X and content width. `TextLayout` still handles the existing glyph widths, wrapping, line-box height, paragraph margins and page placement, now with the leaf's own width and X. The existing legacy list-based test overload shares the same layout logic.

Vertical padding is advanced in document order. If it and the first line cannot fit on the current page, the **next box's top padding** moves with the first line to the next page, while the **previous box's bottom padding** stays on the preceding page rather than contaminating the next line's baseline. The same separation applies to explicit `break-before` and `break-after`. This milestone does **not** implement box-fragment boundaries across pages or paint the padding; a cumulative leading-pad/line requirement exceeding an entire usable page fails rather than faking a fragment.

## Boundaries

Excluded by design: `border`, `background-color`, box-fragment decoration, horizontal margins, margin collapsing beyond previous paragraph behavior, arbitrary `height`, flex/grid, table/image, inline span boxes, full browser layout semantics. The five controlled Chrome box oracle fixtures continue in **reference-only** mode, still expecting `FPDF1201` for remaining unsupported box-paint declarations. Passing that suite is not evidence of box painting.

No external browser/font/runtime dependency, native library, public API, license file, CSS Syntax tokenizer or PDF serializer changed. StrictPdf errors remain fail-before-output; existing resource budgets and cancellations still apply.

## Tests

`BlockGeometryTests` cover nested percentage widths, percentage vertical padding relative to parent width, inherited vs non-inherited box values, stylesheet vs inline important ordering, unit parsing, wrapping, empty boxes, vertical spacing and explicit page breaks, source offsets, overflow rejection, cancellation, budget limits and locale invariance. All inherited NUnit, stylesheet, ASCII/CJK PDF and Native AOT evidence workflows must stay green.

PR: [#12](https://github.com/KoalaFacts/FactsPDF/pull/12). A merge requires a separate approval after current-head verification.
