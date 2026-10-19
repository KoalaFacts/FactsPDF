# Fixed-column tables (E1 / E2)

The existing converter, CLI, Native AOT and browser WASM adapter share this table path. Supply explicit columns; the renderer does not infer or shrink them. Tables that fit an empty usable page remain indivisible, preserving E1 placement. Taller tables paginate at whole-row boundaries and repeat the optional header on each fragment.

```html
<style>
  td,th { padding:4pt; border:1pt solid #24445a }
  th { font-weight:normal; background-color:#eef3f7 }
</style>
<table style="width:480pt;font-size:12pt;line-height:1.5">
  <colgroup>
    <col style="width:80pt"><col style="width:140pt"><col style="width:260pt">
  </colgroup>
  <thead><tr><th>ID</th><th>Category</th><th>Description</th></tr></thead>
  <tbody>
    <tr><td>R001</td><td>Overview</td><td>One complete row</td></tr>
    <tr><td>R002</td><td>Detail</td><td>First line<br>Second line</td></tr>
  </tbody>
</table>
```

This fontless example explicitly resets header weight to normal. A `th` otherwise defaults to **bold** and requires a supplied bold font chain, using `PdfOptions.BoldFonts` or repeated `--font-bold` flags. Cells use the existing explicit normal/bold/italic/bold-italic fallback chains and text restrictions; there is no synthetic font styling or implicit discovery. See [font styles](font-styles.md).

## Structure and placement

- Tables are standalone blocks in the document/body or ordinary block containers. They cannot appear in paragraphs, headings, inline elements, list items or another table.
- A table contains exactly one `colgroup` with 1–16 void `col` elements, optionally one `thead` with exactly one `tr`, and exactly one `tbody` with at least one `tr`, in that order.
- Every row has the same cell count as the column group. Header rows contain `th`; body rows contain `td`. Table-related closing tags must be explicit. Structural whitespace and comments are allowed.
- Cells accept text and `span`, `b/strong`, `i/em`, `br`. Cell paragraphs, lists, images, nested tables, captions, `colspan` and `rowspan` are unsupported.

## Widths, rows and paint

Every column needs a positive CSS `width` in pt, px or percent. Percentages use the resolved table content width. Resolved columns must sum to that width within **0.001pt**; accepted differences are not redistributed. Missing/auto/zero widths, incompatible sums and insufficient content width are errors.

An explicit table width describes its content box. Its own padding/borders sit outside that width and the outer box must fit its parent. As for existing blocks, omitted/auto table width fills the parent with the outer box, subtracting the table's own padding/borders to find its content width.

A column track includes each cell's horizontal padding and borders. Cell text width is the track minus those decorations and must stay positive. Cell padding percentages, including vertical padding, resolve against that cell's track width. Table padding percentages use the parent content width.

Cells wrap with the same font metrics and word/CJK break rules as paragraphs. The tallest wrapped cell plus its vertical decorations determines the row height. Every cell paints to that height, with shorter text at the top. Empty or whitespace-only cells have zero text-line height plus their decorations; `br` creates the existing explicit line box. Separate cell border boxes touch without spacing; adjacent borders remain distinct strokes.

A short table (its complete height including decorations fits an empty usable page) moves intact to the next page if it cannot fit the remaining space. Required leading ancestor decoration must still fit with it, as in E1.

Only a table taller than the usable page height enters E2 pagination. It uses remaining page space when the header and at least one body row fit; otherwise both move to the next page. Each continuation repeats the single optional `thead` row with identical columns, text, styles and complete cell borders. Body rows stay in source order exactly once. A wrapped row moves as a whole; cells never split. Without `thead`, only body rows are placed.

The table's own background and side borders continue across page fragments; top padding/border occurs only on the first fragment and bottom padding/border only on the last, matching existing sliced block decoration. A continuing fragment extends its background/side borders to the usable page bottom. Cell borders remain complete on every row. Ancestor decoration and following content use the existing block pagination rules.

A row, repeated header plus row, or required first/last decoration that cannot fit an empty page fails `FPDF1302` before destination output. This does not add general keep-together, page furniture or cell splitting.

| Element | Supported CSS |
| --- | --- |
| `table` | Existing container width, padding, solid borders, background-color and inherited text properties |
| `td`, `th` | Padding, solid borders, background-color and text properties; no width, margin or break declarations |
| `col` | Width only |
| `colgroup`, `thead`, `tbody`, `tr` | Inherited text properties only |

Existing selectors and global attributes apply. Matched inapplicable declarations fail even if another declaration overrides them. `table-layout`, `border-collapse`, `border-spacing`, vertical alignment and cell height controls are not implemented.

## Limits and diagnostics

All existing input, element, depth, CSS, font, page, display-command and output-byte limits apply. Structural table tags count as elements. Measurement charges each retained text or cell paint command before storing it; placement additionally charges each repeated header and actual table decoration fragment; cancellation is checked during parsing, wrapping, measuring and placement. These are bounded-work controls, not a process-memory ceiling.

| Code | Table-specific cause |
| --- | --- |
| `FPDF1101` | Invalid structure/placement, wrong cell counts or more than 16 columns |
| `FPDF1103` | Unsupported attributes, including spanning |
| `FPDF1201` | Unsupported CSS or tag-specific applicability |
| `FPDF1302` | Missing/invalid resolved widths, cell content too narrow, table too wide, or an indivisible row/header/required-decoration combination too tall |
| `FPDF1303` | Table movement or continuation would exceed MaxPages |
| `FPDF1401` | Display-command or serialized output budget exceeded |
| `FPDF1507` | A required bold/italic/bold-italic font chain is missing |

Existing CSS value, glyph and font diagnostics remain applicable. Source offsets identify the available declaration, column, cell or table location. Input/layout/font errors leave caller destination bytes and position unchanged. Final-copy I/O/cancellation limitations remain those of the [public API](development.md).

## Examples and verification

The [bilingual report](../examples/tables-report.html) uses all four real font roles. The [page movement example](../examples/tables-page-movement.html) places a whole table after 24 numbered records inside a continued decorated container. Supply your own appropriately licensed Latin/Chinese static TrueType chains for all four roles and use `--subset-fonts`.

`scripts/verify_tables.py` exercises actual public API, CLI and freshly built Native AOT output. It checks the 11 unchanged authored fixtures, exact errors and destination preservation, PDF structure, cell text/order/bounds, authored point/percent geometry, font outlines/advances and raster backgrounds. Actual mutated PDFs with missing or displaced table text must be rejected by the inspector. The existing protected browser WASM workflow compares generated bytes, text coordinates and every page raster with CLI output, and exercises PDF search/copy through its reader. It does not use browser printing.

Historical E1 preparation receipts recorded unsupported-table rejection. They remain historical evidence only; the functional manifest now contains actual success/error expectations. Passing scripts or CI alone does not establish visual acceptance: every generated page still requires inspection. User reference approval and release budgets remain separate gates.

E2 native verification uses `scripts/verify_table_pagination.py` and authored `tests/fixtures/tables-e2/` inputs: 10/30/100 rows, a page-tail start, a headerless table and bilingual text. It compares public API, CLI and Native AOT bytes; qpdf and Poppler check every page's row order, repeated header, text bounds and exact 28pt cell geometry. Missing-header and displaced-text mutations must fail inspection. See the [E2 verification record](table-pagination-verification.md). Browser printing is not part of this native acceptance path.
