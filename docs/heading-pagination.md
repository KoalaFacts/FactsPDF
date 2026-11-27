# Keeping a heading with its first paragraph (F1)

Opt in on headings using standard CSS:

```html
<style>h1, h2, h3 { break-after: avoid; }</style>
<h2>Quarterly results</h2>
<p>The heading moves with its paragraph when fewer than two of its lines fit after it.</p>
```

FactsPDF keeps the heading with the start of its first non-empty-text paragraph as Chrome prints it: if that paragraph cannot leave at least `orphans` lines (2 by default) after the heading at the current page tail, the heading and paragraph move to the next page; otherwise the heading stays and the paragraph continues on the next page.

Placement follows Chrome with the paragraph's `orphans` and `widows` (initial value 2, [orphans and widows](css-stylesheets.md#orphans-and-widows)): the group moves to the next page exactly when fewer than `orphans` lines of its paragraph would follow the headings at the page tail, even when the group is taller than a page, because Chrome then breaks before the heading. When at least `orphans` lines fit, the heading stays and the paragraph breaks inside it, even if the whole group would fit an empty page. On the fresh page a moved paragraph breaks where the page ends rather than leave its heading alone.

The heading and paragraph must be direct siblings in the same ordinary container. Consecutive headings marked `avoid` form one group. Empty, whitespace-only and `<br>`-only paragraphs between them participate with their normal spacing and line boxes. Unmarked headings, tables, images, lists, anonymous text, container boundaries and end-of-container stop the search. Without a following eligible paragraph, layout remains unchanged. A chain moves as a whole; shorter suffixes are not retried.

Explicit `break-before: page` / `break-after: page` take priority, including on intervening empty paragraphs. Heading/paragraph padding, borders and internal collapsed margins are measured; the final paragraph's external bottom margin is excluded. Moving uses existing automatic-page rules: preceding trailing space and the first heading's top margin are normally discarded, while required unconsumed ancestor top decoration travels with it. Exception: when an unconsumed panel with nonzero outside top margin moves together with its first kept heading, the heading's own authored top margin also travels; the heading's 12pt-style gap is not silently dropped. Existing ancestor closing-decoration limits still apply.

`break-after: avoid` is supported only on h1–h6 outside lists. Headings nested inside containers within a list item are also excluded. Other applicability fails `FPDF1201`, even if a later declaration overrides it. `auto`, `page`, `inherit`, `initial`, `unset`, stylesheet selectors and `!important` retain normal cascade rules. Keep is not implicitly inherited. `break-before: avoid`, `break-inside`, general paragraph/container keeps, page headers/footers and page numbers are not added.

No public API or CLI option is added. Existing font, page, display-command, output-byte and cancellation limits apply. Lookahead uses the existing font-aware wrapper and bounded sibling ranges; it can validate a complete paragraph before seeing its first line. It does not retain a second display list, charge the actual display budget or preempt ordinary layout's diagnostics. Existing conversion atomicity applies.

The reproducible native corpus is in `scripts/verify_heading_keep.py`; it verifies API/CLI/Native AOT bytes, qpdf validity, page text/order/bounds, and rejects altered-title/geometry mutations. Rendered pages also require visual inspection. No browser printing is required.
