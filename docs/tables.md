# Tables: fixed and automatic columns (E1 / E2)

The existing converter, CLI, Native AOT and browser WASM adapter share this table path. Supply explicit columns, or leave the widths out and let [automatic table layout](#automatic-column-widths) size the columns from their content, as a browser does. Tables that fit an empty usable page remain indivisible, preserving E1 placement. In taller tables, rows that fit a fresh page remain whole; oversized body rows continue at complete text-line and image boundaries. Each table fragment repeats the optional header.

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

This fontless example explicitly resets header weight to normal. A `th` otherwise defaults to **bold** and requires a supplied bold font chain, set with `WithFonts(f => f.Bold(…))` or repeated `--font-bold` flags. Cells use the existing explicit normal/bold/italic/bold-italic fallback chains and text restrictions; there is no synthetic font styling or implicit discovery. See [font styles](font-styles.md).

## Structure and placement

- A table is a block in the document/body, an ordinary block container, a list item, a flex or grid container ([tables as flex and grid items](#tables-as-flex-and-grid-items)) or after inline text. A table start tag closes an open `p`, as the HTML parser does, so text before it is its own line box and the `p`'s own end tag after the table fails `FPDF1101` (a browser would make an empty paragraph of it). Inside an inline element of a block, list item or body (`<div><span>text<table>…</table>more</span></div>`) the table splits the inline element: the text before it and after it form their own lines and keep the element's style. A table cannot appear inside a heading, `pre`, an inline element of a paragraph, another table or a table cell (`FPDF1101`). A list item that starts with a table and has a marker fails `FPDF1201`, because a browser aligns that marker with the table's first row in a way FactsPDF does not model; put text before the table or use `list-style:none`.
- A table contains optionally one `colgroup` with 1–16 void `col` elements, optionally one `thead` with exactly one `tr`, exactly one `tbody` with at least one `tr`, and optionally one `tfoot` with at least one `tr`, in that order. Without a `colgroup`, the cells of the first row (with their `colspan`) set the column count, at most 16.
- Every row covers the same columns, using consecutive cells and optional `colspan`. Any row may mix `th` and `td` cells, so a body row can pair a label `th` with a value `td`; `th` text is bold by default (it needs a bold font chain) and, as in Chrome's user-agent stylesheet (`text-align:-internal-center`), centred unless its parent's computed `text-align` is set: a `tr`, row group, `table` or ancestor `text-align` other than `start` is inherited instead, and `text-align` on the `th` itself wins. Footer rows are laid out, painted and paginated exactly like body rows placed after the `tbody` rows, once; they are not repeated on each fragment. Rows written directly in the `table` (or after its `colgroup`, `caption` or `thead`) go into a `tbody` the HTML parser implies, closed by the next row group or the table's end, so they render exactly like an explicit `tbody`. Other table-related closing tags must be explicit. Structural whitespace and comments are allowed.
- Cells accept an ordered flat flow of text/inline runs, direct `p` paragraphs and direct images, and `ul`, `ol` and `menu` lists ([lists in cells](#lists-in-cells)). Text uses `span`, `b/strong`, `i/em`, `br`. Nested tables, headings/containers inside cells and `rowspan` are unsupported. An optional `caption` may come first; see [captions](#captions).

## Lists in cells

A `td` or `th` may hold `ul`, `ol` or `menu` lists (two levels of nesting, as everywhere), alone or mixed with text, paragraphs and images. A cell is a formatting-context root, so a cell that holds a list is laid out as one block-flow fragment at the cell's content width on one page, by the ordinary block, list and flex layout:

- markers (discs, squares, circles or numbers) and the marker gutter follow [the list rules](lists.md); a list's `padding-left`, zero included, is its whole indentation, as in Chrome, so `ul{padding:0}` puts the items at the cell's left edge with the markers hanging outside it;
- the margins of the list and its items collapse with each other and with the neighbouring paragraphs inside the cell but never out of it: a list's `margin-bottom` still lengthens the row and its `margin-top` still opens it;
- `li{display:flex}` items (a label and a value, say) are flex rows without markers, with `justify-content` and `column-gap` as in [flex rows](css-stylesheets.md#current-source-single-line-flex-rows);
- an automatic column is as wide as the widest list line (and gutter) it holds, a fixed column or a cell width limits it as before, and `vertical-align`, padding, borders, backgrounds, `colspan` and collapsed borders apply to the cell as they do to text;
- a row with a list is measured like any other row and moves whole to the next page when it fits a page, with its markers and the repeated header's markers. A browser would also split such a row between its list items, the existing difference described under [row splitting](#long-descriptions-across-pages); a row that holds a list and would have to split because it is taller than a page (or because a bottom table border makes its last row split) fails `FPDF1302`, as does a cell with a list taller than a page.

A cell with a list takes an image only with left-aligned text (`FPDF1201`). Tables, headings and other containers inside a list in a cell, `display`, borders, outlines and `border-radius` on the list items and percentage margins still fail as they do for any list (`FPDF1201`/`FPDF1101`). Chrome probes of two dozen cell-list shapes agree within 0.75pt (margins, mixed text, nested lists, flex rows, padding, colspan); the columns of automatic tables with lists agree within 0.05pt in the `i1`–`i3` fixtures of `tests/fixtures/tables-auto/` (`scripts/table-auto-layout-oracle.ts`). Pinned by `TableCellListTests`.

## Table width constraints

`min-width` and `max-width` on a `table` limit its used width as Chrome does: the width the table would have (its `width`, or with `width:auto` its max-content width capped at the container) is limited by `max-width` and then raised by `min-width`, so `min-width` wins over `max-width` and over `width`. The columns' content minimums still floor it (a `max-width` below them leaves the table as narrow as its content allows). Like `width`, the limits describe the content box (the border box with `box-sizing:border-box`, and always including the outer half borders of a collapsed table), and percentages use the container's width. The extra width is shared as in [automatic layout](#automatic-column-widths). A table whose columns all have explicit widths grows past their sum by sharing the extra in proportion to the widths (the explicit columns become constrained columns); a `max-width` below the sum of those widths fails `FPDF1302` (Chrome shrinks fixed columns toward their content minimums), and a `min-width` wider than the container fails `FPDF1302` like any wide table. `min-width` and `max-width` on cells, rows, row groups and columns still fail `FPDF1201`. Chrome agrees within 0.02pt on the `g1`–`g7` fixtures of `tests/fixtures/tables-auto/`. Pinned by `TableMinMaxWidthTests`.

Chrome's user-agent sheet gives `table` `box-sizing:border-box`, so a table with its own padding or border and an explicit `width` or `min-width`/`max-width` is wider than Chrome's by that padding and border unless it also sets `box-sizing:border-box`; FactsPDF keeps the documented content-box meaning of the table width (above) for all three.

## Tables as flex and grid items

A `table` directly inside a `display:flex` or `display:grid` container is an item; the table box itself is the item, where a browser wraps it in an anonymous table wrapper box:

- its width is `flex-basis`, else its `width` (a percentage of the container), else its max-content width; it grows with `flex-grow`, shrinks to its min-content width (the sum of its column minimums) and takes `min-width`/`max-width`, `order`, `align-self` and the container's `column-gap`, `justify-content` and `align-items` like any item. The table is then laid out at that width as the table's own width, so its columns share the item's extra width as in automatic layout; a fixed-column table must still sum to it (`FPDF1302`);
- in a grid a table with `justify-self` stretch (the default) fills its grid area, and `start`, `end` and `center` give it its max-content width in the area; auto and `fr` tracks use the table's min-content and max-content widths;
- the table's vertical margins sit inside its item, and its first text line is the item's first baseline;
- a table stretched by `align-items:stretch` (the default) to a taller line or grid row grows its body rows by the extra height in proportion to their own heights, as Chrome does; its header rows (`thead`), footer rows (`tfoot`) and caption keep theirs, and cells keep their `vertical-align` inside the grown rows;
- a flex or grid row is placed whole like any other: it moves to the next page only when every item is a single short block, so a table of more than three lines in an item that does not fit the rest of the page fails `FPDF1302` (as for any item a browser would split), and a row taller than a page fails `FPDF1302`.

Percentage vertical margins on a table item still fail `FPDF1201`. Chrome probes of two dozen flex and grid shapes agree within 0.75pt except `flex-grow` with a table between two items, which Chrome leaves 1pt wider; the `h1`–`h6` fixtures of `tests/fixtures/tables-auto/` agree within 0.09pt. Pinned by `TableFlexGridItemTests` and `TablePlacementTests`.

## Widths, rows and paint

A table uses fixed columns when every `col` has a width and no cell has one, and [automatic column widths](#automatic-column-widths) otherwise. A fixed column needs a positive CSS `width` in an accepted print length, percentage, or bounded `em`/`rem`. Percentages use the resolved table **content** width; `em` uses the column's inherited computed font size (from `colgroup`/table), and `rem` uses the computed HTML root font size. A column's own `font-size` declaration remains unsupported. Resolved fixed columns must sum to the table content width within **0.001pt**; accepted differences are not redistributed. Zero widths, incompatible sums and insufficient content width are errors.

An explicit table width describes its content box. Table `width` and padding accept bounded `em` (the table's final computed font size) and `rem` (the computed HTML root font size), alongside existing print lengths and percentages. Its own padding/borders sit **outside** that content width and the outer box must fit its parent. As for existing blocks, omitted/auto table width fills the parent with the outer box, subtracting the table's own padding/borders to find its content width. Neither font-relative values nor percentages trigger automatic track redistribution; overwide outer boxes fail rather than paint approximately.

A cell's covered column tracks include its horizontal padding and borders. Cell content width is their sum minus those decorations and must stay positive. Cell padding percentages, including vertical padding, resolve against that summed width. Table padding percentages use the parent content width.

Cells wrap with the same font metrics and word/CJK break rules as paragraphs. The tallest wrapped cell plus its vertical decorations determines the row height. Every cell paints to that height, with shorter text at the top. Empty or whitespace-only cells have zero text-line height plus their decorations; `br` creates the existing explicit line box. Separate cell border boxes touch without spacing; adjacent borders remain distinct strokes.

A short table (its complete height including decorations fits an empty usable page) moves intact to the next page if it cannot fit the remaining space. Required leading ancestor decoration must still fit with it, as in E1.

Only a table taller than the usable page height enters multi-page pagination. It uses remaining page space when the header and body content fit; otherwise both move to the next page. Each continuation repeats the single optional `thead` row with identical columns, text, styles and complete header-cell borders. Body content stays in source order exactly once. Rows that fit a fresh fragment with the header and required decoration move as a whole; only otherwise unplaceable body rows split as described below. Without `thead`, only body content is placed.

The table's own background and side borders continue across page fragments; top padding/border occurs only on the first fragment and bottom padding/border only on the last, matching existing sliced block decoration. A continuing table fragment extends its background/side borders to the usable page bottom. Whole rows retain complete cell borders; continued rows use sliced cell borders. Ancestor decoration and following content use the existing block pagination rules.

A header, atomic line/image or required leading/terminal decoration that cannot fit its fresh fragment fails `FPDF1302` before destination output. A complete short table still stays indivisible even when ancestor leading padding makes it impossible to place. General keep-together and page furniture are separate features.

## Automatic column widths

A table without a `colgroup`, with a `col` that lacks a width (or sets
`width:auto`), or with a `td`/`th` `width`, sizes its columns from their content
and those widths with the automatic table layout browsers use:

```html
<style>td,th { padding:3pt 5pt; border:1px solid #5b6b78 } th { font-weight:normal }</style>
<table>
  <thead><tr><th>Item</th><th>Description</th><th>Amount</th></tr></thead>
  <tbody><tr><td>A100</td><td>Paper, box of 5 reams</td><td>72.40</td></tr></tbody>
  <tfoot><tr><td>Total</td><td>One line</td><td>72.40</td></tr></tfoot>
</table>
```

1. Every cell has a **min-content** width (its widest unbreakable piece: a word,
   or under `word-break:break-all`, `word-break:break-word` or
   `overflow-wrap:anywhere` a single character, or a whole line under
   `white-space:nowrap`; a direct image's outer width) and a **max-content**
   width (its widest line when only `br`, paragraph ends and preserved newlines
   break lines), each plus the cell's horizontal padding and borders. They use
   the same font advances, letter/word spacing and break opportunities (spaces
   and CJK characters) as cell wrapping, so a column is never too narrow for its
   own text. A cell's first-line `text-indent` counts on its first line.
   `overflow-wrap:break-word` does not change either width, as in CSS.
2. A column's minimum and maximum are the largest of its single-column cells'
   widths. Then each `colspan` cell, narrowest spans first and in source order
   within one span, widens the columns it covers: its min-content width is
   shared out over their minimums and maximums exactly as a table width is in
   step 4, and each column keeps the larger minimum; its max-content width then
   raises their maximums the same way.
3. The table's content width is its explicit width, or with `width:auto` the
   sum of the column maximums capped at the available width (the table shrinks
   to its content). It never drops below the sum of the minimums: an explicit
   width that is too small grows to that sum, and when the sum does not fit the
   containing block the table fails `FPDF1302` with both numbers.
4. At the sum of the minimums every column gets its minimum. Between the two
   sums each column gets its minimum plus a share of the extra width in
   proportion to its own max-minus-min range, and exactly its maximum when the
   width equals the sum of the maximums. Above the sum of the maximums the extra
   width goes to the columns in proportion to their maximums (equally when every
   maximum is zero). The last column absorbs any rounding remainder.

Rows, cell content, alignment, pagination, repeated headers, `tfoot` and long
row continuation then work exactly as for fixed columns. A column whose cells
are all empty and undecorated is zero wide; a table whose every column is zero
wide (and has no padding or borders) fails `FPDF1302` like any zero-width table,
where Chrome would draw nothing. The explicit table width keeps its
content-box meaning (border-box with `box-sizing:border-box`).

Measured against Chrome 158 with the same TrueType font on both sides (kerning
and ligatures off in Chrome, which FactsPDF does not apply), every cell's left
edge and width in the 26 synthetic fixtures under
`tests/fixtures/tables-auto/` agree within **0.15pt** (largest measured
difference 0.11pt, where a percentage column sets the table width and so
multiplies Chrome's 1/64px rounding; every other fixture is within 0.08pt). Run the comparison with
`node scripts/table-auto-layout-oracle.ts --cli <FactsPDF.Cli> --chrome <chrome> --font <regular.ttf> --font-bold <bold.ttf>`.
Two browser behaviours fall outside this contract: Chrome rounds border widths
to whole device pixels (so `1pt` borders, 1.33px, draw 1px wide there), and it
breaks lines after hyphens and some punctuation, where FactsPDF does not, so
FactsPDF's min-content can be wider for such text.

Explicit widths join in as in browsers. A `col` or cell `width` in pt, px or
another print length makes its column *constrained*: a `col` width is the
column's starting minimum and maximum, and a cell width (a content width plus
the cell's padding and borders, or the border box with `box-sizing:border-box`)
is that cell's maximum; a constrained column ignores the maximums of cells
without a width. A percentage `col` or cell `width` asks for that share of the
table (the largest per column; a running total past 100% is cut to what is
left), and an auto-width table widens until percentage columns get their share
and the other columns their maximums. The table width is then shared out in
Chrome's order: minimums; percentage columns up to their share; constrained
columns up to their maximums; auto columns up to theirs; then any extra to auto
columns by their maximums, else to constrained columns by their maximums, else
to percentage columns by their percentages. A `colspan` cell's widths spread
the same way over the columns it covers, except that past their maximums its
extra goes to percentage columns before constrained ones, as in Chrome.

Automatic layout fails `FPDF1302` for percentage cell padding, image sizes or
`text-indent` (they would depend on the width being computed) and for a
`colspan` cell with its own `width`. Cell `em`/`rem` widths and `min-width`/
`max-width` fail `FPDF1201`. `table-layout` is not implemented and fails
`FPDF1201`: `fixed` sizes columns from the first row alone and lets wider
content overflow its column, which FactsPDF would have to clip or reject.
[Example report](../examples/report-auto-table-layout.html).

## Vertical alignment in whole table rows

For fixed-column `td` and `th`, bounded CSS `vertical-align:top | middle | bottom`
places the content of a shorter cell within the measured height of its
**complete row**. Text lines and their actual font baselines, direct images
and their own painted decorations move together; adjacent cell backgrounds
and borders retain the full unchanged row height.

```html
<tr><td style="vertical-align:bottom">INV-004</td>
    <td>Description line one<br>Description line two<br>Final status</td></tr>
```

The default `top` retains the original PDF bytes; `middle` centres
the whole ordered content flow and `bottom` uses its entire available
vertical free space. Standard selector/inline cascade, `!important`
and `inherit/initial/unset` apply. Cell alignment is non-inherited;
`initial` and `unset` resolve to `top` and `inherit` reads the
parent's computed alignment (normally `top`).

This is not unrestricted CSS `vertical-align`. On a cell, `baseline`, `sub`,
`super`, lengths and percentages are rejected (`FPDF1202`). Use
outside `td/th` fails `FPDF1201`, except the initial `baseline`, which is
validated and dropped there because it changes nothing. The whole-row behavior applies in
multi-page tables and intact repeated header rows, but body rows that
require **independent cell continuation**, including terminal-decoration
fragmentation, must not request `middle` or `bottom`: those fail
`FPDF1302` and retain the previous output. `top` continues to use
the existing split-row renderer. This does not implement `rowspan`,
CSS table automatic layout or inline baseline alignment.

[40-row example](../examples/report-table-vertical-align.html)
and [separately specified physical-padding control](../examples/report-table-vertical-align-oracle.html)
exercise real HTML-to-PDF glyph placement, repeats and page breaks.
`TableVerticalAlignmentTests.cs` covers actual text, image and box
geometry, cascade and error recovery; the native feature verifier
checks independent PDF bboxes and protected output. This is
development source, not a new private binary, approved PDF or licence.

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
1 through the table's column count (with no `colgroup`, up to 16 in the first row). Leading zeros are accepted; signs,
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

Cell paragraphs support text styling, `margin-top`/`margin-bottom` and padding on
all four sides. An unstyled paragraph has top margin zero and bottom margin 8pt.
Between occupied items, the larger adjacent margin is used. The first top and
final bottom margin remain inside the cell: a cell is its own formatting context,
so no margin collapses through its edges. Empty paragraphs
create no text line and combine their pending margins by maximum. Two empty
unstyled paragraphs therefore contribute 8pt, not two blank lines. Explicit
`initial` margin values retain the existing zero initial value. Paragraph padding
(lengths, `em` and `rem`) adds to the cell's content height, narrows the
paragraph and counts in automatic column widths; unlike margins it never
collapses with the margin above or below, so a 5pt margin followed by a paragraph
with `padding-top:7pt` leaves 12pt (Chrome's distance). A percentage padding fails
`FPDF1201`.

A table itself takes `margin-top`/`margin-bottom` (the margins collapse with those
of the blocks around it, see
[block margin collapsing](css-stylesheets.md#current-source-block-margin-collapsing-and-list-and-table-margins))
and zero horizontal margins; a nonzero left or right margin, such as
`margin:0 auto`, fails `FPDF1201`.

Paragraph width, borders, backgrounds and all break declarations are
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

`tr{break-inside:avoid}` (or `page-break-inside:avoid`) and the same on `thead`
change nothing for rows that already move whole, so such a page is
byte-identical to the page without it. A kept body row that would qualify for
continuation follows Chrome's printed pages instead of starting on the page
tail: it first breaks before itself, so it starts on a fresh page under the
repeated header, and only then continues at complete text-line and image
boundaries. A kept first body row breaks before the whole table instead, which
never leaves the header behind on its own. A kept row that already starts where
a fresh page would place it (the first body row of a table at the top of a page,
or the first row of a continuation fragment) splits exactly like the row without
the declaration. A kept last row that fits a fresh fragment and is split only
to carry the table's bottom padding and border still fails `FPDF1302`: Chrome
keeps that row whole and paints the table's bottom padding past the page bottom
into the margin and onto the next page (dropping a bottom border there), and
FactsPDF never paints outside the usable page. See
[CSS table rows](css-stylesheets.md#table-rows) and the
[kept-row maintenance log](../examples/report-table-kept-rows.html).

The Chrome evidence (Chrome 158 headless `page.pdf`, A4, 36pt margins, Arial
10pt on 12pt lines, `border-spacing:0`, every text line located with PyMuPDF)
for a kept row, with FactsPDF's page index of every line equal to Chrome's:

| Case | Chrome lines per page | FactsPDF before | FactsPDF now |
| --- | --- | --- | --- |
| 20-line kept row, 38pt page tail | 0, 20 | 0, 20 | 0, 20 |
| 100-line kept row after ten two-line rows | 0, 63, 37 | `FPDF1302` | 0, 63, 37 |
| 100-line kept first row, table at page top | 63, 37 | `FPDF1302` | 63, 37 |
| 62-line first row under 40pt table top padding, page top | 59, 3 | `FPDF1302` | 59, 3 |
| the same after ten lines of text (table moves) | 0, 59, 3 | `FPDF1302` | 0, 59, 3 |
| 60-line last row, 40pt bottom padding | 0, 60, then padding overflows | `FPDF1302` | `FPDF1302` |

Matching lines differ by at most 0.71pt vertically (Chrome's pixel rounding);
the split point is the same complete line in every case. Without the
declaration Chrome also splits a row that fits a fresh page (3 lines on the
tail, 17 on the next page), while FactsPDF moves it whole; that existing
difference is unchanged.

Each cell of a row that splits keeps its paragraphs' `orphans` and `widows`
(initial value 2) as Chrome prints them: a later paragraph of a cell that cannot
keep its orphans moves to the next page within its cell, a paragraph that starts
its cell's fragment and cannot keep them makes the whole row break before itself
when content precedes it on the page, and every break moves earlier for the
widows. See [orphans and widows](css-stylesheets.md#orphans-and-widows).

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
| `table` | Existing container width and padding (including computed em/rem), `min-width` and `max-width` ([width constraints](#table-width-constraints)), solid borders, background-color, `order`, `flex-grow` and the other flex item properties as a [flex or grid item](#tables-as-flex-and-grid-items), and inherited text properties |
| `td`, `th` | Padding, solid borders, background-color, text properties, `vertical-align` (above) and print-length or percentage `width` (automatic layout); no `em`/`rem` width, min/max width, margin or break declarations |
| `p` inside a cell | Text properties and vertical margins; no width, padding, border, background or break declarations |
| `col` | Explicit width only (including bounded em via inherited column-group text size and rem via HTML root), or no width on every `col` for automatic layout |
| `colgroup`, `thead`, `tbody`, `tfoot`, `tr` | Inherited text properties; on `thead`, `tbody`, `tfoot` and `tr` also `background-color`, painted under every cell of the row or group that has no background of its own (the row's colour before its group's), exactly where Chrome paints row and group backgrounds with separated borders, and repeated with the header; borders only in a [collapsed-border table](#collapsed-borders); no padding; `tr` and `thead` also accept `break-inside`/`page-break-inside:auto\|avoid` (below) |
| `caption` | Text properties (centred by default), padding, solid/dashed/dotted borders, background-color, zero margins, `display:table-caption`; no width or non-zero margins |

Existing selectors and global attributes apply. Matched inapplicable declarations fail even if another declaration overrides them. Each table part accepts the `display` value that names what it already renders, and nothing changes: `thead{display:table-header-group}`, `tbody` and `tfoot{display:table-row-group}`, `tr{display:table-row}` and `td,th{display:table-cell}`. `tfoot{display:table-footer-group}` fails `FPDF1202`, because footer rows are placed once and never repeated on each fragment; every other `display` value on a table part still fails. `table-layout` and cell heights are not implemented. `empty-cells:show` (empty cells always paint their background and borders) and `caption-side:top` (a caption is always above its table) are validated and dropped; other values fail `FPDF1202`. Cells use the separated border model with zero spacing by default, so `border-spacing` is accepted only at one or two zero lengths (validated and dropped, on any element) and non-zero spacing fails `FPDF1202`, also in a collapsed table where Chrome ignores it. `border-collapse:collapse` selects the [collapsing border model](#collapsed-borders).

## Captions

```html
<table>
  <caption style="font-weight:bold;padding:3pt;border-top:1pt solid #24445a">Quarterly totals</caption>
  <thead><tr><th>Item</th><th>Amount</th></tr></thead>
  <tbody><tr><td>Paper roll</td><td>18.40</td></tr></tbody>
</table>
```

A `caption`, as the table's first child (at most one), is laid out as Chrome lays out
`caption-side:top`: a block above the table, as wide as the table's border box, with its own
padding, borders and background inside that width and its inline text (`span`, `b/strong`,
`i/em`, `br`) wrapped and aligned like cell text. Like `th` it is centred unless its parent's
`text-align` is set. The caption belongs to the table's first page fragment: a short table
moves to the next page with it, and a long table places it once, above the first fragment,
without repeating it. `caption-side:bottom` fails `FPDF1202`; `width` and non-zero margins on
a caption fail `FPDF1201`; a `p` or image inside it, a second caption or a caption after the
`colgroup` fails `FPDF1101`. Chrome widens a table to a caption's min-content width; FactsPDF
fails such a caption with `FPDF1302` instead.

## Collapsed borders

`border-collapse:collapse` on the table (it is inherited, and only the table's computed value
counts) switches to the CSS 2.1 collapsing border model as Chrome implements it:

1. Every edge of the cell grid takes one border from the boxes that share it: the cells on
   either side, their rows, the row groups (`thead`, `tbody`, `tfoot`) at the group's first and
   last row and on its sides, and the table on its outer edges. `border-style:hidden` on any of
   them removes the edge; otherwise `none` loses to everything, the wider border wins, then
   `solid` over `dashed` over `dotted`; a remaining tie goes to the cell, then the row, the row
   group and the table, and between two cells (or rows, or groups) to the one further left or
   further up. A colour left as `currentcolor` takes the owning box's text colour.
2. Each cell lays out with half of each winning edge as its border (the widest segment along a
   spanning cell's edge) and the table with half of the widest outer edge on each side; table
   padding is ignored, as in Chrome. The column widths, automatic layout and row heights use
   those halves.
3. The borders are painted after the cell backgrounds, centred on the grid lines: a horizontal
   segment reaches over half of the widest vertical border at each end, and a vertical segment
   covers a joint when it is at least as wide as the horizontal borders there.

Zebra rules such as `tbody tr{border-bottom:1px solid #d8dee9}` with a heavier rule under the
last row therefore look as they do in Chrome. Across pages the table keeps Chrome's fragments:
a fragment that ends between two rows paints the upper half of the line between them and the
next fragment its lower half (below its top edge, or in full under a repeated header), and a
repeated header repeats the table's top border with it. A row that must split across pages
fails `FPDF1302` in a collapsed table, `col`/`colgroup` borders fail `FPDF1201`, and Chrome's
rounding of border widths to whole device pixels is not copied (FactsPDF paints the exact
width).

The automatic-layout oracle fixtures `e1` to `e6` (collapsed borders) and `f1`, `f2`
(captions) in `tests/fixtures/tables-auto/` compare every caption and cell rectangle with
Chrome; in a collapsed table a cell's rectangle runs from grid line to grid line, as Chrome
reports it. [A collapsed zebra table with a caption](../examples/report-table-caption-collapse.html)
shows both features.

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
| `FPDF1101` | Invalid structure/placement, incorrect column coverage (every row must cover the `colgroup` columns or the first row's count) or more than 16 columns |
| `FPDF1103` | Invalid/misplaced `colspan`, unsupported attributes including `rowspan` |
| `FPDF1201` | Unsupported CSS or tag-specific applicability |
| `FPDF1302` | Invalid resolved widths, automatic-layout input it does not support, automatic columns wider than the available width, cell content too narrow, table too wide, or an unplaceable short table/header/atomic unit/required-decoration combination |
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
