# Fixed-column tables (E1 / E2)

The existing converter, CLI, Native AOT and browser WASM adapter share this table path. Supply explicit columns; the renderer does not infer or shrink them. Tables that fit an empty usable page remain indivisible, preserving E1 placement. In taller tables, rows that fit a fresh page remain whole; oversized body rows continue at complete text-line and image boundaries. Each table fragment repeats the optional header.

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
- Every row covers the same explicit columns, using consecutive cells and optional `colspan`. Header rows contain `th`; body rows contain `td`. Table-related closing tags must be explicit. Structural whitespace and comments are allowed.
- Cells accept an ordered flat flow of text/inline runs, direct `p` paragraphs and direct images. Text uses `span`, `b/strong`, `i/em`, `br`. Lists, nested tables, headings/containers inside cells, captions and `rowspan` are unsupported.

## Widths, rows and paint

Every column needs a positive CSS `width` in pt, px or percent. Percentages use the resolved table content width. Resolved columns must sum to that width within **0.001pt**; accepted differences are not redistributed. Missing/auto/zero widths, incompatible sums and insufficient content width are errors.

An explicit table width describes its content box. Its own padding/borders sit outside that width and the outer box must fit its parent. As for existing blocks, omitted/auto table width fills the parent with the outer box, subtracting the table's own padding/borders to find its content width.

A cell's covered column tracks include its horizontal padding and borders. Cell content width is their sum minus those decorations and must stay positive. Cell padding percentages, including vertical padding, resolve against that summed width. Table padding percentages use the parent content width.

Cells wrap with the same font metrics and word/CJK break rules as paragraphs. The tallest wrapped cell plus its vertical decorations determines the row height. Every cell paints to that height, with shorter text at the top. Empty or whitespace-only cells have zero text-line height plus their decorations; `br` creates the existing explicit line box. Separate cell border boxes touch without spacing; adjacent borders remain distinct strokes.

A short table (its complete height including decorations fits an empty usable page) moves intact to the next page if it cannot fit the remaining space. Required leading ancestor decoration must still fit with it, as in E1.

Only a table taller than the usable page height enters multi-page pagination. It uses remaining page space when the header and body content fit; otherwise both move to the next page. Each continuation repeats the single optional `thead` row with identical columns, text, styles and complete header-cell borders. Body content stays in source order exactly once. Rows that fit a fresh fragment with the header and required decoration move as a whole; only otherwise unplaceable body rows split as described below. Without `thead`, only body content is placed.

The table's own background and side borders continue across page fragments; top padding/border occurs only on the first fragment and bottom padding/border only on the last, matching existing sliced block decoration. A continuing table fragment extends its background/side borders to the usable page bottom. Whole rows retain complete cell borders; continued rows use sliced cell borders. Ancestor decoration and following content use the existing block pagination rules.

A header, atomic line/image or required leading/terminal decoration that cannot fit its fresh fragment fails `FPDF1302` before destination output. A complete short table still stays indivisible even when ancestor leading padding makes it impossible to place. General keep-together and page furniture are separate features.

## Image-only cells and branded layouts

A cell can contain one direct `img`, with formatting whitespace or comments
around it. The existing explicit image binding accepts supported JPEG and PNG,
including transparency. Put company text in an adjacent cell:

```html
<table style="width:480pt">
  <colgroup><col style="width:120pt"><col style="width:360pt"></colgroup>
  <tbody><tr>
    <td style="padding:6pt;background-color:#e4f3f0"><img src="logo" style="width:108pt"></td>
    <td style="padding:6pt">Example Company<br>Monthly report<br>Report FP-2026-024</td>
  </tr></tbody>
</table>
```

Cells may also contain direct text, paragraphs and multiple direct images in
source order, as described below. Every image occupies its own vertical item;
an image inside a `span` or `p` fails `FPDF1101`. A nonbreaking space is content,
not formatting whitespace. An unbound image name still fails `FPDF1604`.

Image width percentages and its padding percentages use the cell's content
width, after cell padding/borders have been subtracted from the column track.
Intrinsic size and aspect ratio follow the [image guide](images.md). The cell's
`text-align` positions the image's complete outer box left, center or right;
per-image text alignment does not override it. There is no automatic shrinking
or vertical centering. The tallest decorated image or text cell determines the
row height, with all cell contents aligned at the top.

Cell backgrounds paint below the image's own background/borders and pixels.
Transparent pixels reveal that background. Short tables move intact as before;
long tables repeat an image-only header cell and its decorations on each page.
Repeated placements share one PDF image and optional mask, while every draw and
decoration is charged against the display-command limit. An image-only `th`
needs no bold font until it actually contains text.

The [branded business report](../examples/branded-business-report.html) is a
complete example. Generate its synthetic logo with `scripts/png_fixtures.py`
and bind it as `--image logo=path/to/brand-mark.png`. The independent
`scripts/verify_table_images.py` checks authored image/text geometry, alpha over
colored cells, decoration layering, whole-table movement and three-page repeated
image headers. It also rejects deliberately displaced/removed image draws and
checks failure preservation/recovery through the real CLI. The browser workflow
runs the same cases through actual Native AOT and browser conversion.

## Rich and spanning cells

`colspan` is accepted on `td` and `th`. Omitted means one column. After trimming
HTML formatting whitespace, its value must contain only ASCII digits and name
1 through the table's explicit column count. Leading zeros are accepted; signs,
decimals, zero, non-ASCII digits and out-of-range values fail `FPDF1103`. Every
row must cover exactly all columns; overfill or underfill fails `FPDF1101`.
A spanned cell paints one rectangle across the covered tracks, without an
internal border between them. Other rows retain their own column boundaries.

```html
<table style="width:300pt;font-size:10pt">
  <colgroup><col style="width:120pt"><col style="width:80pt"><col style="width:100pt"></colgroup>
  <tbody><tr>
    <td colspan="2" style="padding:6pt;border:1pt solid #24445a">
      <p style="margin-bottom:4pt">Installation details</p>
      <p>Prepare the report and validate the supplied resources.</p>
      <img src="logo" style="width:72pt">
      <p style="font-size:9pt;margin-top:4pt;margin-bottom:0">Integration preview</p>
    </td>
    <td style="padding:6pt;text-align:right">600.00</td>
  </tr></tbody>
</table>
```

A cell's paragraphs and images flow vertically in source order. Direct text
before or after an item forms an anonymous paragraph with zero margins and the
cell's text alignment. Formatting whitespace between items adds no paragraph;
`br` keeps its explicit line-box behavior. Images must be direct cell children.
Cell paragraphs must be explicitly closed and cannot contain another paragraph,
an image, a list, heading, block container or table.

Cell paragraphs support text styling and `margin-top`/`margin-bottom`. An
unstyled paragraph has top margin zero and bottom margin 8pt. Between occupied
items, the larger adjacent margin is used. The first top and final bottom margin
remain inside the cell; no margin collapses through cell edges. Empty paragraphs
create no text line and combine their pending margins by maximum. Two empty
unstyled paragraphs therefore contribute 8pt, not two blank lines. Explicit
`initial` margin values retain the existing zero initial value.

Paragraph width, padding, borders, backgrounds and all break declarations are
unsupported inside cells and fail `FPDF1201`, including a matched declaration
that another rule overrides. Apply decoration to the cell or image, and narrow
global paragraph selectors when necessary. Ordinary paragraphs outside tables
retain their existing box and pagination behavior.

The spanned content width controls wrapping and image percentages. Paragraph
alignment positions its text; the cell alignment positions image outer boxes.
The tallest complete cell flow determines the measured row height. Rich and
spanned headers repeat intact. Rows that fit a fresh fragment remain whole;
oversized body descriptions use the continuation rules below.

The [detailed service report](../examples/business-table-rich-cells.html) combines
a spanning section heading, paragraph/image/caption detail, a two-column total
and a full-width note. Generate its `logo` with `scripts/png_fixtures.py` and bind
`brand-mark.png` through the existing `--image` option. Independent acceptance
uses `scripts/verify_table_rich_cells.py` for actual text/image geometry, spanning
border pixels, paragraph margins, repeated headers, failures and mutations.

## Long descriptions across pages

Automatic continuation applies only inside a table taller than the usable page.
A body row qualifies when it cannot fit a fresh page together with its repeated
header and required first or final decoration. A row may qualify even if its own
height is less than the page height. A row that fits fresh moves intact from a
short page tail. Headers and complete short tables never split.

Each cell advances independently through complete wrapped text lines and whole
image items. Item numbers, quantities and amounts in shorter cells appear once;
those cells are blank on later fragments. Text before and after an image keeps
its order. An image can wait for the next page while an adjacent text cell
continues, but its pixels, padding and borders stay together. The optional header
repeats above every occupied table fragment. After a continued row finishes,
following rows and totals can use the remaining space on that page.

Cell widths, spans, horizontal padding and alignment remain unchanged. The first
fragment includes the cell's top edge; the final fragment includes its bottom
edge. Backgrounds and side borders cover each shared occupied fragment height,
including cells whose content has finished. No horizontal border is added at an
internal page cut. Continued cell boxes stop at their occupied extent; the
enclosing table background and side borders continue to the usable page bottom.
Any intervening gap therefore shows the table background.

An original initial paragraph margin stays with that cell's first occupied item,
even if another cell starts on an earlier page. Before any neighboring cell
advances, the original cell top edge, initial margin and first unit must fit the
fresh first fragment. Later inter-item paragraph gaps are trimmed when their
following item starts that cell's content on a continuation fragment; gaps within
the same fragment retain the normal maximum-margin rule. Explicit `br` lines
retain their positive height, including lines with no visible glyphs. Empty
paragraphs, margins and padding do not create separate continuation pages.

Final paragraph margins and cell bottom edges are accounted when the logical row
finishes. The row height uses the largest final cell extent. Final table padding
and borders stay with real body content: the layout can reserve a last line or
image for the next fragment, rather than emitting a decoration-only final page.
An atomic unit and its unavoidable final edges that cannot fit fresh fail
`FPDF1302`. Already emitted pages are not reflowed.

The [long service report](../examples/business-table-long-description.html)
contains a long numbered description, an image and caption, a following service
item and totals. Bind the same generated `brand-mark.png` as `logo`. Independent
verification in `scripts/verify_table_row_fragments.py` checks source tokens,
exact page positions, cell/table color gaps, terminal borders, image identity,
budgets and failure recovery through actual entry points. Deliberately defective
PDFs must fail the corresponding content or geometry assertions.

## Supported styles

| Element | Supported CSS |
| --- | --- |
| `table` | Existing container width, padding, solid borders, background-color and inherited text properties |
| `td`, `th` | Padding, solid borders, background-color and text properties; no width, margin or break declarations |
| `p` inside a cell | Text properties and vertical margins; no width, padding, border, background or break declarations |
| `col` | Width only |
| `colgroup`, `thead`, `tbody`, `tr` | Inherited text properties only |

Existing selectors and global attributes apply. Matched inapplicable declarations fail even if another declaration overrides them. `table-layout`, `border-collapse`, `border-spacing`, vertical alignment and cell height controls are not implemented.

## Limits and diagnostics

All existing input, element, depth, CSS, font, page, display-command and output-byte limits apply. Structural table tags count as elements. Copied text/image commands are charged before retention; actual cell/table paint and repeated headers are also charged. Split candidates retain bounded geometry descriptors, and only emitted cell slices count as paint commands. Cancellation is checked during parsing, wrapping, measuring, fitting and placement. These are bounded-work controls, not a process-memory ceiling.

Rows known to stay whole keep their existing early command checks. A trailing
group whose status depends on final table decoration can defer its cell paint
charge until that status is known. For these groups, the first reported failure
can differ when both a later content error and a paint-budget error are present.
Successful output has the same exact command budget, and failures still preserve
the caller's destination.

| Code | Table-specific cause |
| --- | --- |
| `FPDF1101` | Invalid structure/placement, incorrect column coverage or more than 16 columns |
| `FPDF1103` | Invalid/misplaced `colspan`, unsupported attributes including `rowspan` |
| `FPDF1201` | Unsupported CSS or tag-specific applicability |
| `FPDF1302` | Missing/invalid resolved widths, cell content too narrow, table too wide, or an unplaceable short table/header/atomic unit/required-decoration combination |
| `FPDF1303` | Table movement or continuation would exceed MaxPages |
| `FPDF1401` | Display-command or serialized output budget exceeded |
| `FPDF1507` | A required bold/italic/bold-italic font chain is missing |

Existing CSS value, glyph and font diagnostics remain applicable. Source offsets identify the available declaration, column, cell or table location. Input/layout/font errors leave caller destination bytes and position unchanged. Final-copy I/O/cancellation limitations remain those of the [public API](development.md).

## Examples and verification

The [bilingual report](../examples/tables-report.html) uses all four real font roles. The [page movement example](../examples/tables-page-movement.html) places a whole table after 24 numbered records inside a continued decorated container. Supply your own appropriately licensed Latin/Chinese static TrueType chains for all four roles and use `--subset-fonts`.

`scripts/verify_tables.py` exercises actual public API, CLI and freshly built Native AOT output. It checks the 11 unchanged authored fixtures, exact errors and destination preservation, PDF structure, cell text/order/bounds, authored point/percent geometry, font outlines/advances and raster backgrounds. Actual mutated PDFs with missing or displaced table text must be rejected by the inspector. The existing protected browser WASM workflow compares generated bytes, text coordinates and every page raster with CLI output, and exercises PDF search/copy through its reader. It does not use browser printing.

Historical E1 preparation receipts recorded unsupported-table rejection. They remain historical evidence only; the functional manifest now contains actual success/error expectations. Passing scripts or CI alone does not establish visual acceptance: every generated page still requires inspection. User reference approval and release budgets remain separate gates.

The original `colspan.html` fixture still overfills its three explicit tracks and
now expects the coverage error `FPDF1101`. The API probe uses `colspan="0"` to
retain malformed-span `FPDF1103` and destination-preservation coverage. Valid
spans are checked separately by the rich-cell fixtures above.

E2 native verification uses `scripts/verify_table_pagination.py` and authored `tests/fixtures/tables-e2/` inputs: 10/30/100 rows, a page-tail start, a headerless table and bilingual text. It compares public API, CLI and Native AOT bytes; qpdf and Poppler check every page's row order, repeated header, text bounds and exact 28pt cell geometry. Missing-header and displaced-text mutations must fail inspection. See the [E2 verification record](table-pagination-verification.md). Browser printing is not part of this native acceptance path.
