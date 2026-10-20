# F2: opt-in decimal page numbers

Baseline: F1 main `8c22cde00253b8cc441d5749b1b7e411e11028f7`. Scope is only page numbers from the accepted release F route. The user explicitly authorizes choosing a minimal compatible implementation after independent design review, then RED→GREEN, native validation, review and normal merge. No new approval gate is inferred for routine choices inside this scope.

## Minimal public contract

Add `bool PdfOptions.PageNumbers { get; init; }`, default false, and CLI `--page-numbers`. Existing `PdfOptions` already carries document geometry and renderer switches; this single opt-in flag is smaller and clearer than a new formatting object or partial CSS Paged Media interpreter. HTML/CSS remains unchanged. Default documents must remain byte-identical. The alternative `@page { @bottom-center { content: counter(page) } }` requires new nested at-rule applicability/cascade semantics and is deferred with general page furniture. Custom data attributes would add a separate document convention and are rejected.

When enabled, append one searchable decimal page number to every actual output page, beginning at 1. Use invariant ASCII digits, normal (not bold/italic) black 10pt text with line-height 1.2; resolve through the existing explicit normal font fallback chain, or existing Courier when that chain is empty. Do not inherit body CSS or discover fonts. No total count, custom format/start/skip, locale-specific numerals, custom font/size/position or headers/footers.

Center the number horizontally by its measured advance width on the page. Center its occupied vertical extent within the existing bottom margin. Its line metrics and the global declared bounding boxes of the fonts actually drawing the digits conservatively bound vertical extent; leave at least 2pt from the body content area and physical bottom edge. Union each glyph bounding box at its cumulative advance origin, including every fallback font actually used. Verify horizontal glyph bounds remain at least 2pt inside physical page edges, in addition to the existing content-width check. This uses trusted font bounds, not outline sanitization. Before appending, also require 2pt vertical separation from every existing body run (declared font bottom bound), disc, image and nonzero paint box on that page. A body font may paint beyond its line box; reject potential overlap with FPDF1302 instead of moving the footer or changing existing body layout. This conservative vertical-only check may reject a font whose declared box is much larger than its actual used glyphs. Extreme font bounds may require a larger margin or fail rather than silently clipping. Fontless Courier uses existing line metrics plus its standard bounding box. Geometry that cannot fit fails existing `FPDF1302`; no auto-shrink, body-area reduction or repagination. `Margin = 0` remains valid with the default flag, but cannot host enabled page numbers.

Empty HTML still has the existing one output page, now numbered 1. Page breaks, table continuations, F1 heading moves, lists and images determine the actual body pages before numbering; no page is created for a footer or a trailing break. Number transitions (9→10, 99→100) recenter using measured width. Body geometry and text commands are otherwise unchanged.

## Implementation and errors

- Add a small internal page-number compositor after native body layout and existing body display-command verification, before choosing the PDF serializer. No layout/PDF-writer architecture rewrite.
- Reuse `TextLines.Wrap` for existing glyph/fallback/width behavior and `AppendLine` for normal searchable text and exact per-font command charging. Numbers are bounded by the digits of the actual int page count. Share only a small local resolution cache.
- Checks already executed before composition (body parsing/layout/font/page/display-command limits) retain their precedence. Page-number errors occur before MaxOutputBytes, which is checked in serialization; do not serialize the body twice to change that order. With numbering enabled, check remaining display commands before resolving the next page's number. Charge each actual text run, including split fallback runs; no free footer commands. A missing digit in supplied normal fonts yields existing `FPDF1504`; never silently fall back to Courier.
- Existing MaxPages remains a body-page bound. Existing MaxDisplayCommands and MaxOutputBytes include numbering. Cancellation remains cooperative before and during each page; failure before final copy preserves stream/file destinations. In-memory page mutation before failure is not externally visible.
- CLI rejects repeated flags as existing options do, updates help and supports stdout/atomic files. No new package, DB, publication or runtime dependency. Geometry uses existing numeric tolerances; emitted coordinates may round by approximately 0.001pt (ASCII serializer) or 0.000001pt (Unicode text serializer); graphical paint commands remain approximately 0.001pt in both paths. Boundary inspection accounts for that rounding without permitting visible overlap.

## Acceptance before delivery

1. Disabled/default bytes match baseline. Enabling changes only footer runs and resources, not body geometry/page count.
2. One/many/empty pages; explicit and trailing breaks; F1 and E2 boundaries; 9→10 and 99→100; invariant formatting under non-default culture.
3. Exact Courier placement, real fonts, normal-font role independence, fallback runs, missing digits, large declared font bounds and small/zero margins; no body overlap or physical-page clipping.
4. Exact display/page/output budgets, source-independent late errors, cancellation, preserved stream/file destinations, repeat/recovery behavior.
5. CLI/API/actual Linux Native AOT matching PDFs; qpdf and independent per-page text/number/bounds checks; view every distinct page in the bounded visual corpus. Larger counter-transition corpus checked exhaustively for page/text/geometry.
6. Independent design review precedes RED and implementation. Independent code review, final Release build/full tests, normal CI, zero unresolved comments and normal protected merge; verify actual main after merge.

No local Chrome printing, sandbox changes or Library upload retries. Browser printing is not required for native acceptance. No general footer template, combined report, release package or next-stage work in this increment.

Independent design review completed before RED. Its font/body-overhang, diagnostic-order and exact-geometry findings are incorporated above. Courier global bounds come from Adobe AFM as preserved at https://raw.githubusercontent.com/apache/pdfbox/trunk/pdfbox/src/main/resources/org/apache/pdfbox/resources/afm/Courier.afm ; no font asset is added.
