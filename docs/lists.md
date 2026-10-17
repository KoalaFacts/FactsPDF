# Bounded lists (C2)

The shared core accepts `ol`, `ul` and `li`, with at most two list levels. Ordered lists use decimal markers starting at 1 for each list. Unordered lists use filled discs at both levels. Existing .NET options and CLI font arguments are sufficient; no new public configuration is required.

```html
<ol>
  <li>Review the report. 核对报告。
    <ul><li>Check the source. 检查来源。</li></ul>
  </li>
  <li><p style="break-before:page">Continue on a new page.</p></li>
</ol>
```

Use [the bilingual report](../examples/lists-report.html) with explicitly supplied licensed normal Latin/CJK fonts and `--subset-fonts`. ASCII lists also work without supplied fonts. Decimal markers are searchable text and follow the item font role/fallback. Discs are PDF vectors; text extraction does not contain a bullet character. This does not add tagged-PDF accessibility semantics.

Each list reserves a uniform marker gutter inside its content box: `max(2 × list font size, widest direct-item marker + 0.5 × list font size)`. Decimal width is measured with the actual selected fonts. Markers align to the gutter's right edge. Item boxes begin after the gutter; their padding, borders and percentage widths resolve against that reduced width. Body alignment applies within the item, while wrapped lines and later paragraphs retain the item indent.

The marker stays with its item's first line and appears only once if that item continues across pages. Empty items still have a marker line and count in ordered numbering. An empty first paragraph gets a local strut line to hold its marker; an item starting with a nested list gets its own marker line before the child list. Separate lists restart numbering. Existing sliced backgrounds/borders apply to lists and items; the list background includes its marker gutter, while an item background starts after the gutter.

Lists contain only direct `li` elements and whitespace. Items may contain the existing supported text/blocks and nested lists. Optional `li` end tags work before a sibling item or the enclosing list end, without repairing unclosed inline/ordinary containers. Third-level lists, orphan items and invalid direct children fail with `FPDF1101`; `start`, `reversed`, `type` and `value` fail with `FPDF1103`. CSS is still validated before HTML structure.

There is no list CSS, counters, image/custom markers, marker pseudo-element, inside placement, whole-item keep, widows or orphans support. `ol/ul/li` retain container CSS applicability: margin and explicit page-break properties belong on an inner paragraph/heading. A line can occur alone at a page edge. Unsupported CSS fails explicitly. Gutter/box width failures use `FPDF1302`, page limits `FPDF1303`, and marker text runs/discs count towards `MaxDisplayCommands` (`FPDF1401`). Existing font diagnostics, cancellation and output protection remain.

The [design](superpowers/specs/2026-10-17-bounded-lists-design.md) and [plan](superpowers/plans/2026-10-17-bounded-lists.md) specify the precise acceptance contract. Local evidence includes 627 passing core tests and a five-page report from direct API, CLI and freshly built Linux Native AOT with identical PDF bytes. All five first-render pages were opened; independent qpdf/text/geometry/font checks and deliberately corrupted checker inputs passed. These are observed local results, not user reference approval or evidence of completed final-head CI/PR review. Protected browser results and final exact-head gates belong to the PR evidence.

`scripts/verify_lists.py` checks the authored complete text, 17 decimal markers, two discs, nested gutter coordinates, actual font outlines/advances/permissions, page bounds and CLI/API/AOT parity. Its direct API host checks ten failure/cancellation cases and recovery; managed/native CLI checks cover stdout, existing files and new destinations. Browser verification uses the existing protected WASM workflow and normal PDF viewer interaction, with no printing or policy changes. The old nine-product-target denominator is unchanged; the small T-list smoke is only a candidate, and full C2 evidence is separate.
