# Bounded lists (C2)

The shared core accepts `ol`, `ul` and `li`, with at most two list levels. Ordered lists default to decimal markers starting at 1; unordered lists use filled discs. `menu`, which HTML defines as a toolbar list with the same default style as `ul`, renders exactly like `ul` everywhere in this page, including nesting with `ul`/`ol` and CSS (`MenuElementTests.cs`); it adds no menu semantics. Existing .NET options and CLI font arguments are sufficient; no new public configuration is required.

```html
<ol>
  <li>Review the report. 核对报告。
    <ul><li>Check the source. 检查来源。</li></ul>
  </li>
  <li><p style="break-before:page">Continue on a new page.</p></li>
</ol>
```

Use [the bilingual report](../examples/lists-report.html) with explicitly supplied licensed normal Latin/CJK fonts and `--subset-fonts`. ASCII lists also work without supplied fonts. Decimal markers are searchable text and follow the item font role/fallback. Discs are PDF vectors; text extraction does not contain a bullet character. This does not add tagged-PDF accessibility semantics.

A list indents its items by its own padding, which the [user-agent stylesheet](css-stylesheets.md#user-agent-stylesheet) sets to 40px (30pt) on the left, as Chrome does, and gives 1em margins above and below (none when the list is nested in another). The marker hangs outside the item: a text marker ends half the list's font size to the left of the item's content, a disc, circle or square is centred in the same space. There is no separate gutter, so a list with `padding-left:0` has its text at the container's edge and its markers left of it, exactly as in a browser; a marker wider than the padding extends past the container. Decimal width is measured with the actual selected fonts. Item boxes begin at the list's padding edge; their padding, borders and percentage widths resolve against the list's content width. Body alignment applies within the item, while wrapped lines and later paragraphs retain the item indent. Nested `ul` and `menu` lists use `circle` markers, a nested `ol` stays `decimal`.

The marker stays with its item's first line and appears only once if that item continues across pages. Empty items still have a marker line and count in ordered numbering. An empty first paragraph gets a local strut line to hold its marker; an item starting with a nested list gets its own marker line before the child list. Separate lists restart numbering. Existing sliced backgrounds/borders apply to lists and items; the list background includes its padding, where the markers hang, while an item background starts at the padding edge.

Lists contain only direct `li` elements and whitespace. Items may contain existing supported text/blocks and nested lists. Optional `li` end tags work before siblings or a list end, without repairing unclosed inline/ordinary containers. Third-level lists, orphan items and invalid direct children fail with `FPDF1101`. Bounded `start`/`reversed` on `ol`, and `value` on a direct `ol > li`, now change the actual PDF sequence. The HTML `type` attribute selects the marker kind (see below). Wrong attribute targets and invalid values fail with `FPDF1103`. CSS is still validated before HTML structure.

## Current-source CSS marker selection (bounded)

`list-style-type` is supported **only** on `ol`, `ul` and `li`.
Accepted single identifiers: `disc`, `circle`, `square`, `decimal`,
`lower-alpha`, `upper-alpha`, `lower-roman`, `upper-roman`, `none`.
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
Unordered `disc` is the existing solid-colour PDF vector; `circle` paints
an outlined ring with an unfilled, background-transparent centre, and
`square` paints a same-colour filled rectangle. All three occupy the
same measured vector marker gutter, remain attached to the first body
line across pages and consume one display command each. `none` removes
the marker and its paint/display command while retaining the normal
list-item indentation, body text, and pagination. Existing documents
without the new declaration retain the old marker bytes.

`list-style-position:inside|outside` applies on `ol`, `ul` and `li`.
Inside markers occupy measured first-line space; a first word that fits
the item but not the marker line wraps to the next line, and a leading
block child gets a separate marker-only line outside its box.
Outside remains the byte-compatible default. The bounded `list-style`
shorthand accepts a marker kind, a position, or one of each in either
order; see the exact grammar below. Images, counters, custom glyphs,
`::marker`, duplicate terms and whole-item keep remain unsupported;
`orphans`/`widows` (initial value 2) apply to each item's lines and move a
too-short item with its marker ([orphans and widows](css-stylesheets.md#orphans-and-widows)). Invalid values reject `FPDF1202`, non-list
targets reject `FPDF1201`, and errors preserve existing PDF output.
Element/font limits, nesting and cancellation remain unchanged.

The [74-entry print report](../examples/report-list-marker-styles.html)
uses 40 alphabetic inspection rows, 25 Roman corrective actions,
eight marker-free notes and one vector-disc override. The new
`ListMarkerStyleTests.cs` exercises true PDF bytes, measured marker
positions, multi-page layout, cascading rules, font contexts and
protected-output failures. `scripts/verify_lists.py` independently
checks report label/marker attachment and consistent gutters from actual
managed/native PDF extraction. Original text-marker source `e137e523264d1250702656ee1968c5ff2f61e8fe`;
vector shape implementation source `7665f2fbafa34e14f9d792e7e7728d67f268e0e3`;
[80-entry cross-page vector register](../examples/report-list-vector-shapes.html)
and Linux/Windows Native AOT Poppler/PDF-path evidence in
`scripts/verify_native_feature_paths.py`. This still
is not the historical private 0.1.0-preview.1 candidate,
a public NuGet release, or human approval.

Other list CSS, counters, image/custom markers, marker pseudo-elements and whole-item keep remain unsupported; `orphans`/`widows` (initial value 2) apply to item lines. `ol`, `ul`, `menu` and `li` take `margin-top`/`margin-bottom` (lengths, `em`/`rem`, CSS-wide keywords; a percentage fails `FPDF1201`) and horizontal margins, which collapse with the margins of the blocks around them ([block margin collapsing](css-stylesheets.md#current-source-block-margin-collapsing-and-list-and-table-margins)). A list has Chrome's user-agent margin of 1em above and below (in its own font size), none when nested in another list; explicit page-break properties still belong on an inner paragraph/heading. An `li` whose first content is a nested list still takes a marker-only line, where Chrome puts its marker on the first nested line. Only `orphans:1`/`widows:1` lets a line stand alone at a page edge. Unsupported CSS fails explicitly. Gutter/box width failures use `FPDF1302`, page limits `FPDF1303`, and marker text runs/discs count towards `MaxDisplayCommands` (`FPDF1401`). Existing font diagnostics, cancellation and output protection remain.

The [design](superpowers/specs/2026-10-17-bounded-lists-design.md) specifies the precise acceptance contract. Local evidence includes 629 passing core tests and a five-page report from direct API, CLI and freshly built Linux Native AOT with identical PDF bytes. All five first-render pages were opened; independent qpdf/text/geometry/font checks and deliberately corrupted checker inputs passed. These are observed local results, not user reference approval or evidence of completed final-head CI/PR review. Protected browser results and final exact-head gates belong to the PR evidence.

`scripts/verify_lists.py` checks the authored complete text, 17 decimal markers, two discs, nested gutter coordinates, actual font outlines/advances/permissions, page bounds and CLI/API/AOT parity. Its direct API host checks ten failure/cancellation cases and recovery; managed/native CLI checks cover stdout, existing files and new destinations. Browser verification uses the existing protected WASM workflow and normal PDF viewer interaction, with no printing or policy changes. The old nine-product-target denominator is unchanged; the small T-list smoke is only a candidate, and full C2 evidence is separate.

## HTML `type` attribute

`type` on `ol`, `ul` and `li` is the presentational hint the HTML rendering
section defines: it sets `list-style-type` on that element below every author
declaration, so a stylesheet or `style` attribute `list-style-type` or
`list-style` (normal or `!important`, including `inherit` and `initial`) on the
same element wins, as in a browser.

| Element | Value | Marker |
| --- | --- | --- |
| `ol`, `li` | `1` | `decimal` |
| `ol`, `li` | `a` / `A` | `lower-alpha` / `upper-alpha` |
| `ol`, `li` | `i` / `I` | `lower-roman` / `upper-roman` |
| `ul`, `li` | `disc`, `circle`, `square`, `none` | the same keyword |

The `ol` values match case-sensitively (`a` and `A` differ); the `ul` keywords
match in any ASCII case. `<ol type="a">` renders byte-identical to
`<ol style="list-style-type:lower-alpha">` and `<ol type="1">` byte-identical
to a plain `<ol>`. Like the CSS property, the hint inherits to the items, while
a nested `ol`/`ul` without its own `type` keeps its default. Any other value
(`b`, `""`, `01`, ` a`, `disc` on `ol`, `a` on `ul`, `decimal` on `li`) and
`type` on any other element, including `menu`, fail `FPDF1103`. Attribute
selectors on `type` remain unsupported (`FPDF1203`), and the two-level nesting
limit is unchanged (`ListTypeAttributeTests.cs`).

## Current-source ordered-list sequencing and bounded shorthand

An ordered business action register may use `<ol start="21">`, count down
using `<ol reversed>` (or `reversed="reversed"`), and restart the next
ordinal at an individual direct item with `<li value="43">`. Without an
explicit `start`, `reversed` starts from the number of direct items.
The counter advances for every item, **including `list-style:none`**.
Nested and separate lists compute independent counters. This is a real
marker sequence change, not merely permitting HTML syntax.

Only **positive ASCII decimal numbers from 1 through 999999** are accepted
for `start` and `value` (surrounding HTML whitespace and leading zeros
are allowed). Zero, sign characters, decimals, exponents, Unicode digits,
arithmetic overflow and numbering past the positive limit fail with
`FPDF1103`. A reversed sequence that reaches zero also fails with
`FPDF1103`. Roman marker types retain their existing 1–3999 limit,
with `FPDF1202` for unrepresentable Roman ordinals.

The restricted CSS `list-style` shorthand accepts a supported
`list-style-type` marker kind (`disc`, `circle`, `square`, `decimal`,
`lower-alpha`, `upper-alpha`, `lower-roman`, `upper-roman`, `none`),
one `inside`/`outside` position, or both in either order. A single
`inherit`, `initial` or `unset` resolves both longhands.
Omitted type/position reset to `disc`/`outside`, respectively, even
on `ol`. Both participate in specificity, source order and
`!important` with actual PDF geometry and pagination.
Images, URLs, custom counters, duplicate kinds/positions, more than
two tokens and non-list targets reject explicitly.

The [77-entry pagination register](../examples/report-list-continuations.html)
has an independent ordered-marker and Poppler word-position oracle under
`scripts/verify_native_feature_paths.py`, executed by the existing
Linux/Windows Native AOT CI. `ReportListControlsTests.cs` covers PDF bytes,
role/cascade inheritance, nested/reversed sequences, invalid contexts,
page/cancellation limits and output-file preservation/recovery. Source code
commit `bea39e6d6a55f8a9e77ef8156bf6ca6529494738`; this is **not** an update to the retained preview binary.
