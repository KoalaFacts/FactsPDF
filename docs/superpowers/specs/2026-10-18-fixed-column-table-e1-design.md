# E1 proposal: explicit fixed columns in a single-page table

Date: 2026-10-18. Status: **approved and implemented; final verification state is recorded in [PR #33](https://github.com/KoalaFacts/FactsPDF/pull/33)**.
Approval: user reply “先接受” at 2026-10-18 05:02:34 UTC to the explicit E1 scope and this linked proposal (message `Sentinel_ee33008619008191b911a380e3431361`, replying to `Sentinel_3e9cdec088888191be74122848231791`). See the [implementation plan](../plans/2026-10-18-fixed-column-table-e1.md).
Implementation baseline: merged D1 `a1a334937869c764b5248fdf729b4596676e88e1`.

The [accepted release scope](2026-10-11-first-usable-release-scope-design.md) places fixed-width tables after images and before page furniture. C1/C2/D1 are merged. New targeted composition tests and two real 2/3-page documents did not reproduce an image/list/font defect. This proposal originally prepared the next existing milestone without adding table support or widening image formats. The approved E1 implementation and functional verification are now described in the [table guide](../../tables.md); the preparation discussion below is retained as the original contract.

## Decision to review

**Use one explicit `colgroup`, with a positive CSS `width` on every `col`, as the sole source of column widths; retain the existing converter/options/CLI APIs.**

```html
<table style="width:480pt;font-size:12pt;line-height:1.5">
  <colgroup><col style="width:80pt"><col style="width:140pt"><col style="width:260pt"></colgroup>
  <thead><tr><th>ID</th><th>Category</th><th>Description</th></tr></thead>
  <tbody><tr><td>R001</td><td>Overview</td><td>Explicit fixed columns</td></tr></tbody>
</table>
```

All other scope below is the proposed contract accompanying that decision. Approval should cover this written contract before an implementation plan is executed. No separate image-format, font API or platform decision is needed for E1.

Alternatives considered:

| Approach | Consequence |
| --- | --- |
| Explicit `colgroup` (recommended) | Every column has an independent declaration; changing header text cannot change widths. |
| Widths inferred from the first row's cells | Requires precedence rules when later cells disagree and ties layout metadata to content. |
| A new table/options object outside HTML | Adds a public resource/interface contract despite the accepted HTML input workflow. |

## Exact first-stage contract

- A table is a standalone block in the document/body or an ordinary block container. It is not allowed in a paragraph, inline element, list item or another table in E1.
- Strict child order: exactly one `colgroup` containing 1–16 void `col` elements; optional `thead` containing exactly one `tr`; exactly one `tbody` containing one or more `tr`. Every row has the same cell count as the column group. Headers use `th`; body rows use `td`. Whitespace/comments are permitted between these structural nodes. Optional table-related closing-tag repair is not included.
- A cell accepts text, existing `span/b/strong/i/em/br`, whitespace normalization and existing text styling. Paragraphs, lists, images and nested tables inside cells are deferred. Header text defaults to the existing bold role; callers supply bold fonts or explicitly set `font-weight:normal`. There is no synthetic bold or implicit font discovery.
- The table's CSS width is optional and otherwise fills its containing content width. It uses the existing pt/px/percentage length rules. Every column must have a positive width (pt/px or percentage of the resolved table content width). Resolved widths must sum to that table width within 0.001pt. There is no inferred width, leftover redistribution, intrinsic sizing or auto-shrink.
- A resolved column width is the complete cell track width, including that cell's left/right border and padding. A cell's text width is `track − left/right padding − left/right borders` and must be positive. Cell width declarations are rejected so two sources cannot conflict.
- Cell padding, solid borders, background, text alignment and text/font styles reuse existing supported values. Cells align at their row's top. Separate cell border boxes touch with zero inter-cell spacing; adjoining borders remain two separate strokes. Collapsed borders, spacing controls, captions, vertical alignment controls, independent height, colspan and rowspan are excluded.
- CSS applicability is explicit: `table` accepts the existing container width/padding/border/background and inherited text properties; `td/th` accept padding/border/background and text properties, with no width/margin/break declarations; `col` accepts width only; `colgroup/thead/tbody/tr` accept inherited text properties only. A structural tag does not silently ignore an inapplicable declaration. Existing global id/class/title attributes and selectors remain available; cell padding percentages resolve against that cell's track width. Table padding/borders are outside its declared content width, as for existing blocks, and the resulting outer width must fit the parent.
- `table-layout`, `border-collapse` and `border-spacing` declarations are not added as general CSS features in E1. The explicit fixed-track/zero-spacing subset is documented directly. Unsupported declarations continue to fail rather than being ignored.
- The row height is the largest complete cell outer height after wrapping. Every cell paints to that row height; shorter text remains top-aligned. The table height is the sum of all row heights plus any allowed table padding/border. No cell or row is split.
- In E1 the whole table is an indivisible single-page block. If it fits an empty page but not remaining space, move it once. If its whole geometry plus required leading decoration cannot fit an empty page, fail `FPDF1302` before destination output. Splitting rows across pages, repeating headers and many-page tables belong to E2.

The single-page table is deliberately narrower than the eventual 10/30/100-record targets. No existing target changes status until real table PDF assertions pass. E1 success alone cannot complete the repeat-header target.

## Internal integration and bounded work

Proposed internal `TableNode` stores ordered columns and header/body rows; it is a leaf for ordinary block traversal. Parsing validates strict ancestry/shape before retaining ambiguous structures. No new public class is needed.

Resolve columns against the parent content box, then measure/wrap each cell with the existing role/fallback, Unicode, line-height and word-break logic. Reuse that line machinery through a focused internal helper instead of implementing a second text layout engine. Retain only the measured table's bounded rows/cell lines until it is placed; translate cell-local positions once. The table's begin/end placement participates in the same occupied-page, pending padding and ancestor slice rules exercised by the new image-composition tests.

`MaxInputChars`, `MaxElements`, `MaxDepth`, existing CSS budgets and font limits remain applicable. Every structural table tag counts as an element. The fixed 16-column cap bounds each row; cell/row count is additionally bounded by `MaxElements`. Glyph/line/cell loops must check cancellation and charge each retained text/paint command against `MaxDisplayCommands` before accumulation, including during measure-before-place. Exact final PDF output remains constrained by `MaxOutputBytes`; page movement uses `MaxPages`. Layout failure, cancellation and unsupported content must leave the caller's destination unchanged.

Use existing diagnostics: `FPDF1101` for invalid shape/placement (including over 16 columns), `FPDF1103` for unsupported attributes, `FPDF1201` for unsupported CSS, `FPDF1302` for missing/invalid resolved column geometry or a table too tall/wide, existing resource/font/page diagnostics for their corresponding limits. CSS preflight precedence remains unchanged. Preserve available source offsets at the declaration/cell/table responsible for the failure.

## Authored acceptance preparation

`tests/fixtures/tables-e1/` contains proposed positive/negative HTML and an explicit manifest. The positive geometry uses fontless Courier at 12pt and line-height 1.5 (18pt line boxes), 4pt cell padding and 1pt borders: track widths 80/140/260pt, content widths 70/130/250pt; header and short row are each 28pt, a body row with an explicit second line is 46pt, total 102pt. This is arithmetic from the authored input, not frozen renderer output.

Point and percentage declarations describe the same 480pt table. Future functional acceptance must compare their actual text, geometry and bytes where deterministic. Negatives isolate missing column width, mismatched row cells, spanning/nested markup, more than 16 columns, sum mismatch and insufficient cell content width. Explicit page/command/output boundaries and a real-font bilingual table must be added during implementation with RED→GREEN evidence.

The preparation script validates fixture hashes and arithmetic, then calls the **current** CLI and records actual `FPDF1102` rejection with unchanged output for all table inputs. That result demonstrates the implementation gap and fixture readiness only; it is not table functionality or visual acceptance. No fake table PDF is generated from unrelated drawing code.

After this design decision: write the implementation plan; introduce failing structural/layout/limit tests; implement the shared table path; generate real API/CLI/Native/WASM PDFs with explicit fonts; inspect qpdf/text/geometry and every page; independent review; normal PR/CI/comment gates and main verification. User page-reference approval and numeric performance/release budgets remain separate.
