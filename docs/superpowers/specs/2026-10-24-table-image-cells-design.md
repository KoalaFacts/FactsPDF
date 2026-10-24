# Image-only table cells

## Goal

Support a transparent logo beside company details, and repeated branded table
headers, through the existing HTML, explicit image resources and shared renderer.
This follows PNG support in #53. It adds a bounded composition primitive without
changing the public API or implementing a general browser layout engine.

## Content contract

A `td` or `th` may contain one direct `img` as its entire content. Formatting
whitespace and comments may surround it. Formatting whitespace has the existing
`HtmlTokens.Space` definition; a nonbreaking space is content. Existing text cells
retain their current inline-text contract. Image cells cannot also contain text,
`br`, inline wrappers (even empty ones), another image, paragraphs, lists or a
nested table. Both orders of mixed content fail `FPDF1101` with a source location.
An image inside an inline wrapper remains invalid.

The parser tracks content shape, rather than deriving it only from visible
paragraph runs. Attach the image to its cell without flushing or adding it to the
surrounding block tree. The text-only projection skips image cells. Retaining the
cell's empty paragraph for computed style does not imply a text line or a font
requirement.

Image lookup, attributes, CSS applicability and diagnostics remain shared with
standalone images. Invalid names remain `FPDF1103`; missing bindings remain
`FPDF1604`. All existing JPEG/PNG byte and resource-map limits remain unchanged.

## Geometry and paint

The column track includes cell padding and borders. The remaining positive width
is the image's containing width. Reuse standalone image geometry: natural size
is pixels times 0.75pt, explicit width retains aspect ratio, percentages (including
image padding) use containing width. Image padding, solid borders and backgrounds
are supported; image margins and height controls are not added.

The cell's computed `text-align` aligns the complete image outer box left, center
or right within the cell content area. Per-image `text-align` does not override
this positioning. Standalone images retain their existing left alignment.
Images and text remain vertically top aligned. Row height is the maximum cell
content height including its decorations. There is no automatic shrinking or
clipping; invalid or excessive geometry fails `FPDF1302`.

Painting must retain table decoration, cell decoration, image decoration, then
image pixels. Image decorations belong to their row's box range. Text-only table
geometry and output ordering remain unchanged.

## Pagination, resources and bounds

Short tables remain indivisible. Long tables keep whole rows, repeat the optional
single header row including its image and decorations, and preserve body order.
No image or row splits across pages. Existing row/header/required-decoration
checks and `MaxPages` apply. The pagination fit algorithm should not change.

Retain table-relative image positions with positive top coordinates. Translate
them in both short-table placement and paginated row placement. Reuse the same
`PdfImage` instance so repeated placements share one PDF image and optional mask.
Charge every retained image draw before storing it; charge repeated header draws
and decorations again before storage. Existing cancellation and failure-before-
output behavior applies to each path.

## Acceptance

- Real PNG logo beside multiline company information, including transparent and
  partial-alpha pixels over a colored cell.
- Natural, point and percent image widths; cell and image decorations; left,
  center and right alignment; inherited cell alignment and unchanged standalone
  geometry.
- Formatting whitespace accepted; text, nonbreaking space, empty wrappers,
  breaks, multiple images and nested content rejected in either order.
- Whole-table movement and multipage repeated-image headers, with one embedded
  image/mask identity and exact command-budget boundaries.
- Overlarge images/rows and invalid resources preserve destination output;
  successful recovery uses the same entry points.
- Existing text tables, JPEG/PNG, font roles and shared API/CLI/WASM paths retain
  their contracts. Independent PDF geometry and raster checks cover actual CLI,
  Native AOT and browser output.

## Delivery boundary

One reviewed PR with exact-head CI, followed by merge and main verification.
Provide a synthetic branded business report and reproducible commands. New PDF
reference approval remains separate from engineering checks. This increment does
not add spans, cell splitting, automatic columns, Flex/Grid, HTML page furniture,
package publication, pricing or license changes.
