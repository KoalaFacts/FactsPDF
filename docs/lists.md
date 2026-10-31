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

## Current-source CSS marker selection (bounded)

`list-style-type` is supported **only** on `ol`, `ul` and `li`.
Accepted single identifiers: `disc`, `decimal`, `lower-alpha`,
`upper-alpha`, `lower-roman`, `upper-roman`, `none`.
Default HTML list kinds remain `ol` decimal and `ul` disc. A specified
`list-style-type` inherits from a parent list or item; the child
`ul`/`ol` HTML default overrides ordinary inherited kind unless
the child explicitly requests `inherit` or `unset`.
`initial` resets to `disc` (the supported CSS initial value).
Stylesheet specificity, source order, `!important` and inline style
arbitrate as with existing CSS, and every supplied declaration is strictly
validated even when it loses the cascade or selector match.

`lower-alpha`/`upper-alpha` count 1=a/A through 26=z/Z,
27=aa/AA; `lower-roman`/`upper-roman` count 1=i/I,
4=iv/IV, 9=ix/IX and use standard subtractive notation up to 3999.
A Roman marker at 4000 fails `FPDF1202`, not a silently incorrect
label. Individual `li` overrides change just their own marker, not
the ordered-list item numbering. Text markers are measured with the
item's explicit font/style chain and tracking, placed at their real PDF
coordinates and printed once at the first line when an item spans pages.
Unordered `disc` is the existing coloured PDF vector. `none` removes
the marker and its paint/display command while retaining the normal
list-item indentation, body text, and pagination. Existing documents
without the new declaration retain the old marker bytes.

Unsupported `list-style` shorthand, `list-style-position`,
`square`/`circle`, list images, CSS counters, custom glyph markers,
`::marker`, inside placement, whole-item keep, widows or orphans still
fail as unsupported. Invalid values reject with `FPDF1202`; declarations
on non-list elements reject with `FPDF1201`, and errors preserve
existing PDF output. Element and font limits, list nesting and
cancellation retain their existing contracts.

The [74-entry print report](../examples/report-list-marker-styles.html)
uses 40 alphabetic inspection rows, 25 Roman corrective actions,
eight marker-free notes and one vector-disc override. The new
`ListMarkerStyleTests.cs` exercises true PDF bytes, measured marker
positions, multi-page layout, cascading rules, font contexts and
protected-output failures. `scripts/verify_lists.py` independently
checks report label/marker attachment and consistent gutters from actual
managed/native PDF extraction. Engine source commit
`e137e523264d1250702656ee1968c5ff2f61e8fe`;
this is not the historical private 0.1.0-preview.1 candidate,
a public NuGet release, or human approval.

There is no other list CSS, counters, image/custom markers, marker pseudo-element, inside placement, whole-item keep, widows or orphans support. `ol/ul/li` retain container CSS applicability: margin and explicit page-break properties belong on an inner paragraph/heading. A line can occur alone at a page edge. Unsupported CSS fails explicitly. Gutter/box width failures use `FPDF1302`, page limits `FPDF1303`, and marker text runs/discs count towards `MaxDisplayCommands` (`FPDF1401`). Existing font diagnostics, cancellation and output protection remain.

The [design](superpowers/specs/2026-10-17-bounded-lists-design.md) and [plan](superpowers/plans/2026-10-17-bounded-lists.md) specify the precise acceptance contract. Local evidence includes 629 passing core tests and a five-page report from direct API, CLI and freshly built Linux Native AOT with identical PDF bytes. All five first-render pages were opened; independent qpdf/text/geometry/font checks and deliberately corrupted checker inputs passed. These are observed local results, not user reference approval or evidence of completed final-head CI/PR review. Protected browser results and final exact-head gates belong to the PR evidence.

`scripts/verify_lists.py` checks the authored complete text, 17 decimal markers, two discs, nested gutter coordinates, actual font outlines/advances/permissions, page bounds and CLI/API/AOT parity. Its direct API host checks ten failure/cancellation cases and recovery; managed/native CLI checks cover stdout, existing files and new destinations. Browser verification uses the existing protected WASM workflow and normal PDF viewer interaction, with no printing or policy changes. The old nine-product-target denominator is unchanged; the small T-list smoke is only a candidate, and full C2 evidence is separate.
