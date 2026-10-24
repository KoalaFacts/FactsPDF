# Spanning and flat rich table cells

Image-cell prerequisite #54 is merged as `c97b6bd3d797734a2a4e873ca301adf57caba092`. This is the next table increment in the approved commercial functionality sequence. Implementation starts after post-merge verification of that main commit.

## Goal and boundary

Support invoice headings/notes spanning explicit columns, and useful description cells containing text, paragraphs and images in source order. Keep current short-table movement and long-table whole-row pagination. Oversized rows still fail `FPDF1302`; row splitting is the following PR B. No public .NET option, CLI argument, WASM resource protocol or PDF serializer format changes.

## HTML contract

- Existing explicit table structure remains: 1–16 explicit columns, optional single header row, nonempty body, explicit table closing tags. No implicit columns, rowspan, nesting, caption or footer group.
- Accept `colspan` only on `td` and `th`. Omission is 1. Trim only `HtmlTokens.Space`; require one or more ASCII digits representing 1 through the explicit column count. Leading zeros are accepted. Signs, decimals, non-ASCII digits, zero, empty and overflow fail `FPDF1103` at the attribute/cell source location. Do bounded accumulation; no unchecked integer overflow or browser clamping.
- Cells occupy consecutive tracks. Reject a cell exceeding the remaining tracks immediately and an underfilled row at `</tr>`, both `FPDF1101`. Coverage, rather than cell count, defines row validity.
- Each cell contains a flat ordered flow: direct text/inline runs, direct `p`, and direct `img`. Direct runs permit existing span/b/strong/i/em/br. A p permits only text and those inline tags. Images occupy separate vertical items and must be direct children of td/th.
- Explicitly close cell paragraphs. Reject nested p, image inside p/inline wrappers, headings, div/section/article, lists and nested tables with `FPDF1101`. Do not run ordinary p auto-close/repair before cell structure validation.
- Formatting whitespace/comments between items creates no anonymous paragraph. NBSP is content. A direct br creates its existing line box. Direct inline text before/after p/img becomes an anonymous paragraph with cell paragraph style and zero margins. Empty inline wrappers create no geometry and must not escape the cell.
- Existing direct text cells retain the containing cell paragraph style, including alignment, even when the first run is a styled span. Preserve current direct-text wrapping and inline whitespace behavior. Empty legacy text cells retain their one empty paragraph in the compatibility projection; image-only cells do not gain an empty paragraph. Rich cells emit only their actual text items in source order.

## Styles and spacing

- Cell/table/column/structural-element CSS applicability stays as today. Cell borders, padding and background remain cell-owned.
- Cell p supports text properties and margin-top/margin-bottom with existing value/global-keyword semantics; default top 0, bottom 8pt. Its text-align controls its own lines.
- On a p inside a table cell, reject ALL box properties (width, padding, border, background) and break-before/break-after, including auto/inherit/initial/unset, with `FPDF1201`. Check every matched declaration before cascade winner selection in CssStylesheets.Compute; a later legal winner must not conceal inapplicable declarations. Ordinary p outside cells remains unchanged.
- Images retain #54 CSS, size/resource and decoration contracts. Cell text-align positions the complete image outer box; per-image text-align does not override placement. Images have zero flow margins.
- Resolve spacing using a pending nonnegative margin. Before an occupied text/image item, add max(pending previous bottom margin, this item's top margin); after it set pending to its bottom margin. Empty text paragraphs have no line height and fold their top/bottom margins into pending by maximum. At cell end add pending once. Thus margins between consecutive paragraphs collapse by maximum; all-empty paragraphs contribute the maximum pending margin once. Margins never collapse through cell boundaries. An explicit br line is occupied even without a draw command.
- Anonymous paragraphs have zero margins. A whitespace-only explicit p contributes its margins but creates no text/font requirement. Image-only cells retain no phantom paragraph, line or font requirement.

## Geometry and internal interfaces

Recommended normalized model:

- TableCell owns TextStyle Style, BoxStyle BoxStyle, int SourceOffset, int ColumnSpan, and List<TableCellContent> Contents.
- TableCellContent has exactly two variants: TableCellText(Paragraph Paragraph) and TableCellImage(ImageNode Image). No generic BlockChild subtree in a cell.
- TableMarkup.Start accepts the validated span for td/th (default 1 elsewhere), tracks covered columns, and validates parent-sensitive cell children. HtmlDocumentReader owns cell-local current paragraph capture/flush. A flush attaches text to the cell, never surrounding CurrentChildren(). p frames retain CSS ancestry but never create outer BlockNode children.
- BlockTreeTraversal emits every cell paragraph in source order, skips images, and does not demand fonts for empty text. BlockLayout continues yielding one LayoutTable.
- Keep MeasuredTable and MeasuredTableRow run/box/image ranges from #54 unchanged. TextLayout and TablePagination fit behavior should not change.

For each cell sum ColumnSpan tracks, then resolve padding percentages against that sum and subtract horizontal edges once. Image percentages resolve against the resulting content width. Use shared ImageLayout.Resolve. Measure the ordered flow with shared TextLines.Wrap; line data is borrowed and must be copied by AppendLine before advancing. Check accumulated height, including pending margins and bottom edges, against usable height throughout. Preserve existing source diagnostics on wrapping errors; explicit p errors can use p offset, anonymous text cell offset.

Cell text is top-aligned. Row height is max(cell content plus edges/margins). Paint one full-height rectangle per cell across its complete span; no internal spanned-track boundary. Paint table, then all cell boxes, then image decorations; retain #54 image/text placement conventions. Keep image PdfImage reference identity.

## Diagnostics and bounds

- FPDF1101: invalid child/parent structure and row track coverage.
- FPDF1103: invalid/misplaced colspan and existing image attribute/name failures.
- FPDF1201: inapplicable CSS, including an overridden declaration.
- FPDF1302: existing width/geometry errors and a complete row/header/required-decoration combination that cannot fit a page. This PR does NOT split cells or rows.
- FPDF1303/1401/1507/1604 and other existing font/CSS/image diagnostics retain meanings.

Retain all existing input, element, depth, CSS, image, font, page, command and output limits. Charge text/image/paint before retention, and repeated header commands again on placement. Ensure allowance before wrapping every text-bearing item. Cancellation applies in parser content loops, span accumulation, per-item measure, wrapping, paint and traversal. Margin-only content is bounded by input/elements and creates no uncharged text command. Input/layout failures preserve destination bytes/position and allow successful subsequent conversion.

## Literal acceptance geometry

All coordinates below are positive distances from page top; default margin 36pt. Use fontless normal text at 10pt, line-height 1.2, so a single line is 12pt and baseline is 9pt below its top. Use short ASCII strings to avoid wrapping; th must explicitly set font-weight:normal.

1. Table width 200pt; columns 40/60/100pt. First cell colspan=2 with 4pt padding and 1pt borders has outer width 100pt, content width 90pt, content X=41pt. The sibling starts X=136pt. With one line and no paragraph margins, row height=22pt, line top=41pt, baseline=50pt. There is no cell border at X=76pt in this row. A following unspanned row does have its ordinary boundary there.
2. Same cell, p1 margin-top=2pt/margin-bottom=6pt and p2 margin-top=4pt/margin-bottom=3pt, each one line: row height=45pt; text tops=43pt and 61pt; baselines=52pt and 70pt. This distinguishes max(6,4) from summing margins.
3. Same cell, p1 one line with default margins, 32x16 image at width:50% with 2pt padding/1pt border, then p2 one line default margins: image pixels 45x22.5pt, image outer 51x28.5pt; row height=78.5pt. First text top=41pt; image outer top=61pt; pixel top=64pt; second text top=89.5pt. Left image pixel X=44pt; centered pixel X=63.5pt. Cell alignment applies to image outer box.
4. One empty default p in that cell: row height=18pt (10pt edges + 8pt pending margin); two empty default p elements still 18pt. Empty p with margin-top=11pt and margin-bottom=7pt contributes 11pt once. Bare whitespace cell retains 10pt edges only. br-only content retains its actual line height.

## Acceptance and compatibility gates

- Span values/coverage, percentages, first/last/all columns, 16-column limit, and omitted span versus span=1.
- Mixed text→image→p, image→text, consecutive images, empty wrappers, NBSP, explicit breaks, whitespace and empty p; no content escapes into document siblings.
- CSS ancestry/selectors and inherited styles; illegal matched properties fail before winners; ordinary paragraphs unaffected.
- Spanned/rich header repeats intact with the same image identity, no phantom font demand, exact command-budget tests, whole-table movement and whole-row order.
- Oversized rich rows still reject before destination output; recovery succeeds.
- Capture and compare exact base bytes for existing plain-text short/multipage tables, image-only short/multipage tables and standalone images. Keep existing error cases except deliberate replacement of old mixed-image and spanning rejections.
- Independent actual PDF geometry/raster inspection, CLI/Native AOT/browser equivalence and mutation rejection. Engineering evidence does not constitute user reference approval.

## Adopted design decisions

Ruling: Empty cell paragraphs fold top/bottom margins into one pending maximum and create no text line — keeps one bounded margin algorithm and avoids phantom font requirements — templates that used empty paragraphs to accumulate blank space must use explicit margins or line breaks.
Ruling: Cell paragraphs support text properties and vertical margins; reject box decoration/width and all break declarations before cascade winners — keeps table ownership of cell geometry and pagination — broad paragraph CSS may require narrower selectors for cells.
Ruling: colspan accepts ASCII leading zeros after existing HTML formatting-whitespace trimming; no signs/non-ASCII digits/zero/out-of-range values — follows the specified positive digit grammar without clamping — tightening this later would affect such templates.
