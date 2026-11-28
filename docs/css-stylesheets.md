## Fixed-column table-cell inherited text wrapping (source candidate)

Explicitly bounded `td/th` can declare `white-space`, `word-break` and `overflow-wrap` on the cell itself. Computed winners reach its anonymous text and flat `p` by inheritance; child paragraphs can override or reset them. Measured column content widths and existing `TextLines.Wrap` produce real PDF spaces, glyph positions, wraps, repeat headers and page breaks, rather than silently accepting declarations. This removes the need to insert a styled `p` in every table cell of a commercial report.

Bounded values only: `white-space:normal/nowrap/pre-line/pre-wrap/pre`, `word-break:normal/break-all/break-word` and `overflow-wrap:normal/break-word/anywhere`. No break-spaces/keep-all, structural `table/col/colgroup/tr/thead/tbody/tfoot`, inline spans or list CSS. Invalid values/contexts and unfittable nowrap cells fail without modifying prior PDF bytes or stream position.

A [24-row real report](../examples/report-cell-text-flow.html) and independent [paragraph-style control](../examples/report-cell-text-flow-oracle.html) are covered by [NUnit geometry, multipage, cascade and recovery tests](../tests/FactsPDF.Tests/TableCellTextFlowTests.cs). Engine source [19c6e35eb2987aa84fc7b5ff16b2ccad57630c60](https://github.com/KoalaFacts/FactsPDF/commit/19c6e35eb2987aa84fc7b5ff16b2ccad57630c60); exact CI, independent review, main verification and release are distinct. All three Can I Render records remain partial.

## First-line indentation in fixed-column table cells (source candidate)

`text-indent` can now be declared on explicit fixed-track `td`/`th` cells and their flat `p` descendants. The cell's value is inherited by anonymous text and nested paragraphs. The winning nonnegative physical, `%`, `em` or `rem` length changes the measured first PDF line, wrapping, alignment and page continuations; later broken/wrapped lines do not re-indent and the cell background stays in place. Percentages use the **actual measured cell/paragraph content width**, not page width. Existing cascade, `!important` and `inherit/initial/unset` apply. This extends the existing partial `text-indent` behavior rather than creating a full CSS Text implementation.

Only explicit-column table cells and their flat paragraphs are in scope; `table/col/colgroup/tr/thead/tbody`, lists and inline `span` do not gain text-indent. Negative/hanging, `calc()`, each-line/hanging flags and impossible first-line geometry still fail with preserved previous PDF bytes and stream position.

The [multi-page service register](../examples/report-cell-text-indent.html) has an [independent computed-point control](../examples/report-cell-text-indent-oracle.html). Real PDF byte parity, glyph X positions, inherited/important styles, repeated headers and protected failures are exercised in [TableCellTextIndentTests](../tests/FactsPDF.Tests/TableCellTextIndentTests.cs). Source [7282c0839235443ab49935cb0f2e8092117786a1](https://github.com/KoalaFacts/FactsPDF/commit/7282c0839235443ab49935cb0f2e8092117786a1); exact-head CI, independent review and main merge remain separate gates.

## Fixed-table computed em/rem widths and padding (source implementation)

Within the existing explicit-track table renderer, a `table` may use computed `em`/`rem` for `width` and padding; a `col` may use those units for `width` only. The table's `em` is its own final computed font size, the column's `em` is its inherited `colgroup` font size, and `rem` uses the computed `html` root. Cascade and `!important` choose the winning declarations before converting these units to points. The strict table content-width sum, cell geometry, painting, repeated headers and pagination operate on the resolved lengths.

This does not allow `font-size` on a void `col`, missing/auto columns, cell `width`, relative row/group box properties, `calc()`, negative values, or automatic track sizing. Unsupported inputs remain explicit errors with protected prior PDF output. The same-source [multi-page invoice](../examples/report-table-relative-tracks.html) and its independent [point-based oracle](../examples/report-table-relative-tracks-oracle.html) exercise actual HTML-to-PDF layout.

## Bounded attribute selectors for report templates (source implementation)

The renderer matches accepted HTML attributes with `[name]`,
`[name=value]`, `[name^=value]` (prefix), `[name$=value]` (suffix),
`[name*=value]` (substring), `[name~=value]` (complete whitespace-separated
word), and `[name|=value]` (exact or followed by a hyphen). Values may be CSS
identifiers or quoted strings, with optional `i` (ASCII-case-insensitive) or
`s` (case-sensitive) flags on comparisons. The default is case-sensitive.
HTML attributes are decoded before matching. `~=` uses ASCII whitespace only;
`|=` is useful for `lang` such as `en` or `en-AU`. Empty non-equality
operands deliberately reject. Empty equality strings are valid.

Supported attribute names are `id`, `class`, `title`, `lang`, `href`,
`src`, `alt`, `hidden` and nonempty `data-*` names. Attribute names are
ASCII-insensitive; attribute values with the `i` flag fold **ASCII letters
only**, not arbitrary Unicode. All operators compose with supported type,
class, ID, comma, descendant and direct-child selectors and contribute
class-level specificity. Existing `!important`, cascade, inheritance and
match-operation budgets remain effective. Real PDF text, borders, padding,
images, and continued report boxes consume the winning styles.

Unsupported HTML attributes, other operators (`!=`), multiple/unknown flags,
malformed/whitespace-split operators, empty non-equality operands, unsupported
pseudo-classes, sibling combinators and nested selectors fail explicitly with FPDF1203;
no network access or full CSS Selectors claim. This remains a **partial**
selector capability. [The 44-entry operator invoice example](../examples/report-attribute-operators.html)
and [independent inline control](../examples/report-attribute-operators-oracle.html)
are compared in [real PDF, cascade, image, TrueType and fail-safe tests](../tests/FactsPDF.Tests/AttributeOperatorReportTests.cs).
Initial presence/equality implementation: `d7fee5d311f9abebccf37a58c3c9198de78750e8`;
operator-family engine source: `845ab1799149e8b833306f2e49b3f8b240d699c8`. CI, independent review
and the actual merged main are separate evidence.

## Bounded PDF last-line alignment (source implementation)
 
A controlled report paragraph/heading or flat fixed-column table cell may set
`text-align-last:center`, `right`, `left`, `start`, `end`, or `justify`.
The winning inherited value applies to the *last and explicit-break lines*
in the actual PDF: X coordinates use measured glyph widths; `justify` uses
the same U+0020 word-space glyph advances as the existing auto-wrap path.
`auto` is the default and preserves the previous final-line positioning and
PDF bytes. Wrapped interior lines keep ordinary `text-align`. CSS specificity,
`!important`, and `inherit/initial/unset` remain supported.

This is horizontal LTR only; no CSS Text Level 3/general browser alignment,
RTL/bidi, CJK inter-character justification, inline-span declarations,
unsupported values or new table layout. Invalid input fails without
replacing the existing destination. The
[44-record multi-page input](../examples/report-last-line-alignment.html),
[independent manually broken control](../examples/report-last-line-alignment-oracle.html),
and [NUnit PDF, cascade, table, geometry and recovery checks](../tests/FactsPDF.Tests/LastLineAlignmentReportTests.cs)
exercise actual content and fail-safe behavior. Initial implementation
[61da678f](https://github.com/KoalaFacts/FactsPDF/commit/09a186b4e6f33355db3014ef0b0fcd61df224bc3).
 
## Bounded physical outlines for report panels

### Report-panel outline paint (source-only)

Use `outline:2pt dotted #285d91;outline-offset:3pt` on an ordinary
`div/section/article/blockquote` outside list/table ancestry. The
`outline-width/style/color` longhands are independent cascade winners;
shorthand omission resets width/style/ink to medium/none/currentColor.
`!important` and CSS-wide `inherit/initial/unset` are supported.
The visible PDF stroke is outside the border box: it does not consume
layout width, shift searchable text or change page count. Side outlines
continue on page fragments, with top/bottom strokes only on first/last
fragments. Default/none preserves existing PDF bytes.

Widths are zero or 0.2–6pt; offsets 0–12pt. Both use physical
pt/px/mm/cm/in/pc/q only. Solid/dashed/dotted/none and any supported colour,
including [translucent colours](#translucent-colours), are accepted; negative, percentage, gradient and
rounded-corner outlines reject, preserving output. Outside scoped panels no outline is ever painted,
so resets that remove one (`a{outline:none}`, `img{outline:0}`,
`outline-style:none|initial|unset`, any width, colour or offset) are dropped
and leave the PDF bytes unchanged; an outline style that could paint there
(`solid`, `dashed`, `dotted`, `inherit`) still fails `FPDF1201`
(`OutlineNoneResetTests.cs`). This is not full CSS Outline.

[44-record report](../examples/report-outlines.html),
[independent longhand oracle](../examples/report-outlines-oracle.html),
[NUnit/CLI/TrueType/negative tests](../tests/FactsPDF.Tests/ReportOutlineTests.cs),
and native AOT/qpdf/Poppler verification exercise the actual PDFs.
Core source `bc30878b275c6dc3f6fdf0fe2b33366277a4c30c`.


## Bounded combined PDF text-decoration lines (source only)

Controlled business report paragraphs, headings, inline text and flat table
cell text accept `text-decoration-line` with standalone `none` or **1–3
distinct** `underline`/`line-through`/`overline` values in any order.
The `text-decoration` shorthand accepts the same line set, optionally one
colour ([translucent colours](#translucent-colours) included), one solid/dashed/dotted stroke and one bounded 0.2–12pt
computed thickness. Omitted shorthand terms reset as before, and
`inherit`/`initial`/`unset`, specificity and `!important` apply to
the line set as one CSS property. Underline offset changes only the underline.

Each selected line paints an independent, page-local, measured-width PDF
path on searchable text and consumes one `MaxDisplayCommands` slot;
legacy single-line and none PDF bytes remain unchanged. Duplicate lines,
`none` mixed with a line, extra width/colour/style terms, unknown lines,
unsupported CSS values and unmatched invalid rules reject `FPDF1202`.
Standalone images reject `FPDF1201`. Failures preserve existing output.

The [44-entry quote](../examples/report-combined-text-decorations.html) matches the
[independently written longhand control](../examples/report-combined-text-decorations-oracle.html) byte-for-byte in
[NUnit PDF/TrueType/CLI/negative tests](../tests/FactsPDF.Tests/CombinedTextDecorationReportTests.cs).
Engine source [`2d18399e`](https://github.com/KoalaFacts/FactsPDF/commit/2d18399e25c65267431f0decc7fe716dde82688a);
not full CSS Text Decoration.

### Bounded PDF underline offset

The independent CSS property `text-underline-offset` moves the **actual
measured PDF underline** below the text baseline, without moving glyphs,
overlines or strike-throughs. `auto` (the default) preserves the legacy
font-relative underline position and byte-identical output. A nonnegative
`pt/px/mm/cm/in/pc/q` value or computed `em` (final element font size) /
`rem` (final HTML root font size) must resolve to **0–24pt**. For example,
`text-decoration:underline dotted #285d91;text-underline-offset:2pt`
draws the dotted underline 2pt below the baseline.

This bounded, inherited CSS longhand uses ordinary source order,
specificity, `!important`, `inherit`/`unset` and `initial` reset.
It applies to already-supported underline-bearing paragraph, heading, inline
text and flat table-cell text; line wrapping and page-local decoration
fragments continue through existing layout and paint. It does not add CSS underline-position or browser propagation.
Negative offsets, percentages, `calc()`, `from-font`, computed values above
24pt and standalone-image use fail explicitly; conversion errors preserve the
previous destination PDF.

The [44-row, multipage quotation](../examples/report-underline-offset.html)
uses computed `em`/`rem` and has an
[independent absolute-point PDF control](../examples/report-underline-offset-oracle.html).
[Actual PDF geometry, font, CSS, CLI and recovery tests](../tests/FactsPDF.Tests/UnderlineOffsetReportTests.cs)
are tied to engine source
[`b8f9b938`](https://github.com/KoalaFacts/FactsPDF/commit/b8f9b938ec42b108fb6f325715a4dd9b692e027d).


## Bounded printable text-decoration thickness (source only)

For business quotations and approval corrections, `text-decoration-thickness`
now controls the **actual measured PDF stroke width** of one existing
underline, overline or strike-through. The value is `auto` (legacy
font-relative width), or a positive physical `pt/px/mm/cm/in/pc/q`,
computed element `em` or root `rem` length, bounded **0.2–12pt**.
For example, `text-decoration:underline dashed #285d91 1pt` includes the
optional fourth shorthand component, and
`text-decoration-thickness:0.1em` computes from the final element font.
A shorthand without an authored width resets it to `auto`.
Specificity, source order, independent `!important` and
`inherit/initial/unset` work across the existing supported text targets.

The renderer paints the computed width as a PDF solid rectangle or
page-local clipped dashed/dotted stroke, with searchable real glyphs and
true line/page wrapping. Default `auto` retains exact legacy PDF bytes.
No CSS `from-font`, percentages, `calc()`, arbitrary offsets, unbounded
widths or general browser propagation.
Out-of-range/unsupported declarations, including unmatched selector rules,
reject explicitly and preserve any pre-existing PDF.

The [44-record register](../examples/report-decoration-thickness.html)
matches a separately authored
[explicit-point oracle](../examples/report-decoration-thickness-oracle.html).
[NUnit PDF/CLI/TrueType/negative tests](../tests/FactsPDF.Tests/DecorationThicknessReportTests.cs)
and [Native AOT qpdf/Poppler verification](../scripts/verify_stylesheets.py)
exercise the complete source path. Engine source
[1e9c86ea](https://github.com/KoalaFacts/FactsPDF/commit/1e9c86ea98ed3233b49e552b734823ef9bf158f9)
plus [shorthand/report follow-up](https://github.com/KoalaFacts/FactsPDF/commit/34ffb2cbbd8d555b8167ec8ced71b3609ac981e6).
No public package, updated private candidate, human approval or commercial grant.

## Scoped inline hyperlink styling (source-only)

The bounded `a[href]` element uses normal text and CSS declaration handling:
its initial text ink is opaque blue (`#0000cc`) and it draws the existing
solid underline. Authored `color`, `text-decoration`, font size/weight,
`!important`, inheritance and `initial`/`unset` can override those defaults;
none changes its validated destination. It creates actual per-page PDF URI
annotation rectangles after glyph measurement, including across line/page
breaks and in permitted flat table-cell text. This does not add CSS properties
or element applicability. `text-align:justify/start/end` on an inline anchor
is rejected rather than silently applied. The single supported destination
contract is HTTPS (ASCII, no credentials) or plain single mailto, max 2048
characters; unsupported URLs and extra anchor attributes fail explicitly (an unsupported
`href` only warns in non-strict mode: the text is drawn as a link with no annotation, see
[feature notes](feature-notes.md#clickable-https-and-mailto-report-references-source-only)), except
`target` and hyperlink `rel` types, which are validated and ignored
(`AnchorTargetRelTests.cs`).

See [report link example](../examples/report-clickable-orders.html),
`ReportHyperlinksTests.cs` and `verify_stylesheets.py`. Implementation source
`9a19096a69f323bda3c7678c590a96b19ad98864` (unreleased source).

## User-agent stylesheet

Every supported element starts from Chrome's default style. The defaults are a real
user-agent stylesheet, the subset of Chrome's `html.css` (the HTML Standard's rendering
section) for the supported elements, written as CSS in `src/FactsPDF/Css/UserAgentStyleSheet.cs`,
parsed and compiled once with the engine's own parser and applied as the lowest cascade
origin: every author rule, style attribute, `!important` and presentational hint (the list `type`
attribute) wins over it, and a winning `revert` computes the property as the sheet declares it.
Nothing about an element's default look lives anywhere else.

| Element | Default (Chrome's value, at a 12pt font) |
| --- | --- |
| `body` | margin 8px (6pt) on every side; it collapses with the first and last child's margin like any block margin. The writer's page margin is separate. |
| `p`, `pre`, `dl` | margin 1em above and below; `pre` also `white-space:pre` |
| `h1` to `h6` | bold; font size 2, 1.5, 1.17, 1, .83 and .67em; margins .67, .83, 1, 1.33, 1.67 and 2.33em above and below |
| `blockquote`, `figure` | margin 1em above and below, 40px (30pt) left and right |
| `dd` | margin-left 40px |
| `ul`, `ol`, `menu` | margin 1em above and below (none when nested in a list), padding-left 40px (30pt); markers `disc` for `ul`/`menu`, `decimal` for `ol`, `circle` for a `ul`/`menu` nested in a list. The marker hangs outside the item, half an em left of its content; there is no extra gutter, so `ul{padding:0}` puts the text at the list's edge and the marker left of it |
| `hr` | margin .5em above and below; a 1px border on all four sides painted as Chrome's inset shades (top and left `#9a9a9a`, bottom and right `#eee`), 1.5pt tall in all; `color:gray` |
| `table` | `box-sizing:border-box`, `border-collapse:separate`, `text-indent:initial` (border-spacing 2px is not implemented) |
| `td`, `th` | padding 1px; `vertical-align:middle`; `th` is also bold and centred |
| `tr`, `thead`, `tbody`, `tfoot` | `vertical-align:middle` |
| `address`, `i`, `em`, `cite`, `var`, `dfn` | italic |
| `b`, `strong` | bold |
| `small`, `big` | `font-size:smaller` and `larger` (1/1.2 and 1.2 times the parent) |
| `u`, `ins`; `s`, `del` | underline; line-through |
| `a` | colour `#0000ee`, underline |
| `abbr[title]` | dotted underline |

The line height of every element is `line-height:normal`, as in Chrome: a line is as tall as the first
font's ascent, descent and line gap (hhea, or the OS/2 typographic values when the font sets
USE_TYPO_METRICS), each rounded to whole CSS pixels at the font size, with half the gap above
the rounded ascent. At 12pt DejaVu Sans that is 15 + 4 pixels, 14.25pt. A unitless, length or
percentage `line-height` overrides it, and the built-in font (no font supplied) keeps 1.2.

What the sheet leaves to the document model, because every element has it by construction: the
`display` of each element, the centring of `th` and `caption` (the user-agent `-internal-center`,
which gives way to an inherited alignment), the quotation marks of `q`, and the metadata elements
that render nothing. Headings and `th` are bold by default and behave exactly like `b` and `strong`: FactsPDF never synthesizes bold, so
without a bold font chain (or with the built-in font, which has no bold) they fail `FPDF1507`, and
`font-weight:normal` keeps the regular face.

An unmodified `ul{padding:0}` or `*{margin:0;padding:0}` reset now behaves as in Chrome: the list text sits at the
container's edge and the markers hang to its left, where earlier versions kept a marker gutter inside the list.

Known differences from Chrome, each with its reason in
`tests/fixtures/ua-defaults/known-differences.json` ([development](development.md#user-agent-defaults-against-chrome)):
tables have no `border-spacing` geometry (Chrome's 2px between and around cells; zero is accepted),
`hr` paints its inset border as flat edges of the two shades (author `border-style:inset`
fails `FPDF1202`), `tfoot` is the last rows of the row group, `monospace` has no face of its own
(`code`, `kbd`, `samp`, `pre` use the supplied font), and `mark`, `sub` and `sup` are not supported.

## Bounded semantic inline marks for business reports

Exactly `small`, `big`, `u`, `s`, `del` and `ins` join the existing text-only
inline tag scope. Small uses five-sixths of the computed parent font size and big six-fifths;
u/ins use the current single underline; s/del use the current
strike-through. These are real layout and painting defaults, the user-agent
stylesheet above, **below** the supported CSS cascade. CSS `font-size`, `text-decoration`, colour
and decoration style can override them, including stylesheet
specificity, `!important`, `inherit`/`initial`/`unset`.

Existing explicit font chains, glyph measurement, searchable PDF content,
line breaking, pagination, element budgets, paint budgets and failure
protection continue to apply. Out-of-range computed font sizes fail with
`FPDF1202`, unsupported attributes including del/ins `datetime`/`cite`
fail with `FPDF1103`, and block nesting fails with `FPDF1101`.
Deleted text remains selectable and searchable, and is never a redaction.
This is not change-tracking annotation, auto-font support or PDF
semantic history metadata.

`code`, `kbd`, `samp`, `var`, `cite` and `dfn` are inline text too, so
documentation, runbook and citation pages render. `code`, `kbd` and `samp`
keep the supplied regular font chain: there is no generic monospace face, as
for `pre`, so they look like the surrounding text unless CSS styles them.
`var`, `cite` and `dfn` are italic, as the HTML rendering section's
user-agent sheet makes them, and need an italic font chain like `em`
(`FPDF1507` otherwise). Everything else above applies: the cascade can
override the defaults, unsupported attributes fail `FPDF1103`, block
children `FPDF1101`, and they are accepted in `pre`, headings, lists and
table cells (`PhrasingElementTests.cs`). `time` and `data` are inline text
with no default style; `time` accepts `datetime` and `data` accepts `value`
as machine-readable metadata that is never drawn (any other element with
those attributes fails `FPDF1103`; `TimeDataElementTests.cs`). `abbr` is
inline text with the user-agent sheet's `abbr[title]` dotted underline (CSS can
override it; an `abbr` without `title` has no default style, and the title is
never drawn; `AbbrElementTests.cs`). `q` is inline text wrapped in the English
marks of the user-agent sheet's `quotes:auto` (curly double quotes, curly single
quotes when nested), drawn in the `q`'s own text style; normal text with no
supplied font uses the ASCII `"` and `'`, and `cite` is metadata only
(`QuoteElementTests.cs`). `bdi` renders like `span`: every run is laid out left
to right, so it isolates nothing (`BdiElementTests.cs`). `mark`, `sub` and `sup`
still fail `FPDF1102`; `big` is inline text with `font-size:larger`.

The [44-change source](../examples/report-inline-changes.html)
and [independent explicit CSS control](../examples/report-inline-changes-oracle.html)
are checked by `SemanticInlineReportTests.cs` and the real Native AOT
qpdf/Poppler report parity and original-output recovery verifier.
Engine source [a1300f39](https://github.com/KoalaFacts/FactsPDF/commit/a1300f3943efcf92426836b963e72d28b078e192).
No binary release, manual page signoff or
independent review is inferred from the implementation source.

## Bounded `@media print` / `@media all` for PDF report stylesheets (source-only)

An embedded `<style>` may contain top-level `@media print { ... }` or
`@media all { ... }`. The single case-insensitive media type has no
conditions; rules inside the block must be ordinary supported selector
rules. Their declarations use exactly the existing supported properties,
element applicability, values, source order, specificity, `!important`,
inheritance, PDF paint, font metrics, page breaks and budgets. Relative
to surrounding ordinary rules they take their actual **document order**,
not a separate CSS origin or special print priority. Later `<style>`
blocks still affect earlier document content.

Both media types are active in this PDF-only renderer. All unsupported
at-rules, `screen`, comma lists, expressions such as `(min-width:600px)`,
other at-rule blocks and loose declarations inside a media block fail
`FPDF1204`, rather than silently being skipped; `@keyframes` (see
[Animations at print time](#animations-at-print-time)), `@page` (see
[CSS `@page`](#css-page-paper-margins-and-margin-boxes)), `@layer` (see
[Cascade layers](#cascade-layers)) and a nested `@media print`/`all` are the
only at-rules allowed inside. Invalid or
unsupported declarations in an unmatched nested selector still fail
`FPDF1202`. No media feature evaluation, browser screen rendering,
user-origin cascade, automatic fetching or full CSS Media
Queries is implied.

The [44-entry printable register](../examples/report-print-media.html) and
[independently authored inline control](../examples/report-print-media-oracle.html)
exercise actual PDF ink/boxes/text, TrueType, CLI, pagination, qpdf/Poppler,
negative input and output recovery. NUnit: `PrintMediaReportTests.cs`.
Source implementation [`70f37e7e`](https://github.com/KoalaFacts/FactsPDF/commit/70f37e7e881bade30851ecaa06528a27813f0403).
Exact-head CI and independent review, private software, licensing,
human reference approval and publication are separate gates.

## CSS `@page`: paper, margins and margin boxes

An embedded `<style>` may hold `@page { ... }` at top level or inside
`@media print`/`@media all`. Every `@page` rule applies to every page; its
declarations cascade by `!important` and then source order, like element
rules, and count toward the CSS declaration budget.

```css
@media print {
  @page {
    size: A4 portrait;
    margin: 1cm;
    @bottom-center { content: counter(page) ' / ' counter(pages); font-size: 0.8em; }
  }
}
@page { @bottom-center { content: none; } } /* a later rule removes the footer */
```

| Declaration | Accepted values |
| --- | --- |
| `size` | `auto`; a page-size keyword `A5`, `A4`, `A3`, `B5`, `B4`, `JIS-B5`, `JIS-B4`, `letter`, `legal`, `ledger` (ASCII case-insensitive), with or without `portrait`/`landscape` in either order; `portrait` or `landscape` alone, which turns the writer's paper; one length (square) or two lengths (width height) in `pt`, `px`, `mm`, `cm`, `in`, `pc` or `q`, each side 72–14400pt |
| `margin`, `margin-top`, `margin-right`, `margin-bottom`, `margin-left` | A nonnegative length in `pt`, `px`, `mm`, `cm`, `in`, `pc`, `q`, `em` or `rem` (both relative to the html element's font size); `initial`, `unset`, `inherit` or `revert` keep the writer's margin on that side |
| `@top-left`, `@top-center`, `@top-right`, `@bottom-left`, `@bottom-center`, `@bottom-right` | Page-margin boxes holding only declarations |
| `content` (in a margin box) | `none` or `normal` (no box); otherwise a sequence of strings, `counter(page)` and `counter(pages)` (optionally `, decimal`) |
| `color`, `font-size`, `font-weight`, `font-style`, `font-family` (in a margin box) | The values and errors the body accepts; `font-family` is validated and dropped, since fonts come from the writer's font chains |

`A4` and `letter` are exactly the writer's `A4()` (595.28 × 841.89pt) and
`Letter()` (612 × 792pt); the other keywords are their exact millimetre or inch
sizes. A side that `@page` does not set keeps the writer's margin (36pt by
default). Different sides are real asymmetric margins: the body starts at the
left and top margins, wraps in the page width minus both side margins and
paginates in the height between the top and bottom margins.

Margin boxes are the [structured page furniture](page-furniture-cli.md) under
another name: `@top-left`/`center`/`right` are `HeaderLeft`/`Center`/`Right`
and the bottom boxes the footer regions, with the same single-line, glyph,
clearance and collision rules (`FPDF1302`, `FPDF1305`). As in
Chrome, the page context inherits from the `html` element: a margin box takes
the html element's computed font size, colour, weight and style, and `em`, `%`
and `larger`/`smaller` font sizes are relative to it. Left boxes start at the
left margin, right boxes end at the right margin and centre boxes centre on the
page area between the side margins; each band is centred vertically in its
margin. Strings keep `{` and `}` literally; spaces collapse like normal white
space. A box whose content is longer than 1024 UTF-16 units fails `FPDF1001`.

Writer options never lose silently to `@page`, and `@page` never overrides
them silently:

- An explicit writer page size (`WithPage(p => p.Size/A4/Letter/Landscape/Portrait)`,
  or the CLI's `--page-size`, `--landscape`, `--page-width`/`--page-height`)
  together with a non-`auto` `@page` size must give the same paper, or the
  conversion fails `FPDF1206`. The same holds for an explicit writer margin
  (`p.Margin(...)`, `--margin`) and each margin side `@page` sets. A writer
  option the page rule does not set still applies.
- Margin boxes with content cannot be combined with writer page furniture,
  page numbers or header/footer text (`WithPageFurniture`, `WithPageNumbers`,
  `WithHeaderText`, `WithFooterText`, `--page-furniture`, `--page-numbers`,
  `--header-text`, `--footer-text`): `FPDF1206`. When the cascade leaves every
  box at `content:none`, writer page text applies normally.
- Without page options the CLI sets none, so a template's `@page` decides; the
  writer's defaults (A4, 36pt) apply to whatever `@page` leaves unset.

Page selectors (`:first`, `:left`, `:right`, `:blank`) and named pages fail
`FPDF1204`, as do the corner and side margin boxes (`@top-left-corner`,
`@left-middle` and the others), other at-rules or nested rules inside `@page`,
and `@page` inside `@media screen`. Other properties in `@page` (`padding`,
`bleed`, `marks`, logical margins) or in a margin box (`text-align`,
`vertical-align`, `width`, borders) fail `FPDF1201`. Other `size` forms,
percentage or `auto` margins, margins that leave no page area, `counter()` of
any other counter or style, `counters()`, `attr()`, `string()` and other
content values fail `FPDF1202`. `CssPageRuleTests.cs` pins byte equality with
the equivalent writer options and furniture, cascade, inheritance, geometry,
precedence and every failure.

Measured against Chrome 156 printing the same synthetic pages through
Playwright `page.pdf({ preferCSSPageSize: true })`: Chrome rounds the page box
to 1/300 inch, so its A4 is 594.96 × 841.92pt (FactsPDF 595.28 × 841.89), its
A5 landscape 594.96 × 420pt (595.276 × 419.528) and 150mm × 100mm 425.04 ×
282.96pt (425.197 × 283.465). Body and margin-box edges sit within 0.35pt of
Chrome (Chrome snaps them to whole CSS pixels: a 1cm left margin starts text at
28.50pt, FactsPDF 28.35pt). With Arial supplied to FactsPDF and named on the
html element for Chrome, a 9.6pt `@bottom-center` in a 1cm margin has its
baseline 10.74pt above the page edge (Chrome 10.92pt); 12pt top boxes in a
20mm margin sit 32.42pt below the top edge (Chrome 32.25pt), a 9pt bottom box
in a 25mm margin 32.22pt above the bottom edge (Chrome 33.00pt) and a 15pt bold
top box in a 1cm margin 19.27pt below the top (Chrome 18.75pt): FactsPDF
centres line height 1.2 and the font's declared glyph box, Chrome its
line-height-normal box. Chrome draws margin boxes in the html element's
`font-family` (Times when none is set); FactsPDF uses the writer's font chains.
Without an `@page` margin Chrome uses the print call's margins (Playwright's
default 0, which also leaves margin boxes no room); FactsPDF uses the writer's
margin.

## LTR logical border component longhands (source-only)

On ordinary horizontal left-to-right report elements that already accept
physical borders, the 18 bounded names `border-inline-width/style/color`,
`border-block-width/style/color`, and `border-inline-start/end-{width,style,color}`
or `border-block-start/end-{width,style,color}` lower to physical left/right
or top/bottom independent cascade slots. The two paired forms on each axis
accept **one or two** widths, line styles or colours; a named single
side accepts **one**. A lone CSS-wide keyword `inherit`, `initial` or
`unset` applies to all targeted physical sides. Per-side widths actually
change report content geometry, solid/dashed/dotted strokes paint PDF bytes,
and authored colours including opaque rgb/hsl/currentColor reach PDF paint.
Existing font layout, stylesheet specificity, `!important`, source order,
fragmented boxes and explicit error/output preservation are unchanged.

No RTL/vertical text, unlisted border properties, double
styles, `calc()`, mixed CSS-wide components or new inline/table contexts.
Unmatched invalid declarations still fail `FPDF1202`; inapplicable
elements fail `FPDF1201`. See [44-entry logical HTML](../examples/report-logical-border-components.html),
[physical longhand oracle](../examples/report-logical-border-components-oracle.html),
`LogicalBorderComponentTests.cs` and the real Native AOT verifier.
Introduced at [`99328f07`](https://github.com/KoalaFacts/FactsPDF/commit/99328f071395d5d7cc1eedad9138f05ee118fb0f);
source changes are not independent review, final CI, a private/public
binary, human PDF approval or customer licence.

## Bounded LTR logical border shorthands for report boxes (source-only)

On the same report boxes that already accept physical `border-*`, the
shorthands `border-inline` (left and right), `border-block` (top and
bottom), `border-inline-start` (left), `border-inline-end` (right),
`border-block-start` (top), and `border-block-end` (bottom) reuse the
existing border widths, colours, and solid/none/dashed/dotted strokes.
Up to one of each width/style/colour is accepted in any order, with physical
defaults for omitted terms; a single CSS-wide keyword applies to all three
components of each affected side. Shorthands lower to independent longhand
cascade slots (including source order, specificity and `!important`),
then feed actual painted PDF edges, content geometry and page fragments.

This scope assumes **LTR horizontal** writing. The renderer rejects unknown
logical border forms, direction/RTL/vertical writing, double styles,
calc and mixed CSS-wide terms rather than pretending
to implement them. Existing unsupported inline/table/list/image contexts
and strict output-preserving failure behaviour remain. The
[44-entry source HTML](../examples/report-logical-borders.html) and
[explicit physical-edge oracle](../examples/report-logical-borders-oracle.html)
are checked by `LogicalBorderReportTests.cs` and native qpdf/Poppler
PDF-byte, multi-page text/position and protected retry probes.
Implementation [`6916ef5d`](https://github.com/KoalaFacts/FactsPDF/commit/6916ef5da96068c167690ef3a0b5f1c94d41f255);
exact-head CI, independent review and publication are separate.

## Standard opaque CSS named colours for printable reports (source-only)

## Scoped currentColor report ink (source-only; 2026-11-04)

`color:currentColor` selects the inherited parent computed opaque text
colour. On existing supported report blocks, `background-color:currentColor`
and single-colour `background:currentColor` paint the element's **final**
computed text ink, after stylesheet/inline cascade and `!important`.
The pre-existing border/text-decoration currentColor behaviour is preserved.
CSS-wide resets, inheritance, actual PDF background fragments, font metrics
and page continuation use the ordinary renderer paths. A translucent text
colour keeps its alpha ([translucent colours](#translucent-colours)). No
system colours, gradients, URLs, additional elements, arbitrary CSS Color or
new layout model is accepted.

Source `3dbcded4f5d39b16b79bbff7aac4132305659d4a` with [48-entry HTML](../examples/report-current-colour.html)
and [separately authored fixed-HEX PDF control](../examples/report-current-colour-oracle.html).
Positive and negative regression scope: true multi-page text order and
geometry, background colour and nested inheritance, TrueType, managed CLI,
source-bound native AOT, Poppler/qpdf PDF parsing/coordinates and protection of
prior PDF on invalid input. CI, independent review, human PDF approval,
private candidate and customer distribution are distinct milestones.


Controlled templates may now use all **148 standard opaque CSS named colours** in
the existing supported colour slots. Examples: `color:rebeccapurple`,
`background:aliceblue`, `border:2pt solid slategray` and
`text-decoration:underline dotted teal`. Aliases such as
`gray/grey`, `aqua/cyan` and `fuchsia/magenta` resolve to the same
fixed RGB values. They affect real PDF text, background fills, borders and
decorations, not only CSS parsing. Existing inheritance, specificity,
font metrics, page geometry, resource limits and fragmentation still apply.

The [64-record report](../examples/report-named-colours.html) and
[independently authored HEX oracle](../examples/report-named-colours-oracle.html)
must render identical PDF bytes. Source NUnit tests every name against explicit
hex values, including supplied TrueType font roles, cascading, managed CLI
and protected failures. Linux/Windows Native AOT checks additionally use
qpdf/Poppler for searchability, positions, pages and recovery.

This is a bounded colour *value* extension, not 148 new CSS properties or full
CSS Color: `background:transparent` retains its previous limited meaning;
system/theme colours, arbitrary `currentColor`, `opacity`,
gradients and unsupported selectors/elements still reject.
Implementation source [`c9138808`](https://github.com/KoalaFacts/FactsPDF/commit/c9138808d785986712f17e977fa2371854cb6aed).
Exact-head CI, independent review, private software, human reference approval
and customer permission remain separate; no public release is implied.

## Bounded percentage RGB colours for business reports (source-only)

Report templates may now use `rgb(20%,40%,60%)`, `rgb(10% 25% 50%)`
and `rgba(0%,50%,0%,1)` in the existing colour, background, border and
text-decoration paint slots. Exactly **three percentage channels** (0–100%,
fractional values allowed) are converted to 8-bit opaque PDF RGB; midpoint
conversion rounds to the nearest byte with ties up. The existing three
integer-channel forms still work. CSS specificity, `!important`, inheritance,
real PDF text/paint, pagination, resources and previous-output protection
use the same established engine paths.

Mixed percentage/integer channels, decimal *numeric* channels, nesting, `calc()` and general
CSS Color remain unsupported;
unrecognized syntax rejects with `FPDF1202`, including unmatched rules.
This is not a general CSS Colors implementation; alpha is covered by
[translucent colours](#translucent-colours).

[48-entry real paginated report](../examples/report-rgb-percent-colours.html) and
[independently authored HEX control](../examples/report-rgb-percent-colours-oracle.html)
target exact PDF byte, searchable body, page/position, managed API/CLI,
Linux/Windows Native AOT qpdf/Poppler and failure/recovery equivalence.
Implementation source [`6a79f3f6`](https://github.com/KoalaFacts/FactsPDF/commit/6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d).
Actual CI, independent review, retained preview binary, human approval
and customer distribution are separate checks; no release is implied.

## Bounded opaque HSL report colours (source-only)

Business-report templates can use `hsl(210,60%,40%)`,
`hsl(210deg 60% 40%)` or `hsla(120,100%,25%,1)` as scoped
`color`, `background`/`background-color`, border colours and
text-decoration colour. Hue is a finite number or an angle in `deg`,
`grad`, `rad` or `turn` from -3600 through 3600 degrees (wrapped by 360); saturation/lightness accept
0–100% including fractions. HSL is converted to opaque 8-bit RGB
before the existing PDF ink/background/border paths, retaining CSS
cascade, font/layout geometry, pagination and resource protection.

The [48-entry colour report](../examples/report-hsl-colours.html)
and [independent fixed-hex oracle](../examples/report-hsl-colours-oracle.html)
must produce identical actual PDF bytes. Source NUnit, managed CLI
and Linux/Windows Native AOT qpdf/Poppler check searchability, multi-page
geometry and original-output recovery. Alpha paints as [translucent colours](#translucent-colours); no
nesting, gradients or general CSS Color: unsupported values
fail `FPDF1202`. Implemented in source `ee5af1a67d8aa88fa23da281ea6ab23414f9ef22`; exact-head CI,
independent review, private software, human approval and customer
distribution remain separate.

## Bounded bolder/lighter font roles for report typography (source-only)

`font-weight:bolder` and `font-weight:lighter` are now accepted on existing styled report text, including headings, paragraphs and spans. They select the caller's **real supplied bold or normal font chain**, respectively. In FactsPDF's explicit two-role model, a bolder request at bold stays bold and a lighter request at normal stays normal. Nested inheritance, source-order cascade, selectors, `!important` and CSS-wide `inherit/initial/unset` retain their existing rules. A relative value is computed from the supported parent role ladder, not a fabricated intermediate face; previously supported numeric 100–900 values continue to resolve to normal (100–500) and bold (600–900).

```html
<section style="font-weight:bold">
  <p style="font-weight:lighter">Service item <span style="font-weight:bolder">APPROVED</span></p>
</section>
```

The font choices affect embedded TrueType glyph widths, actual PDF text, wrapping and pagination. A missing requested bold chain still raises `FPDF1507`; unsupported values, unmatched invalid declarations and 650-like numeric weights fail `FPDF1202`, without replacing an existing PDF. This is **not** a full CSS font-weight relative algorithm over nine font weights, variable axes, synthetic bold, system font discovery, or `font-family` selection.

A 48-entry paginated report and an independently specified explicit normal/bold role oracle are in `examples/report-relative-font-weights.html` and `examples/report-relative-font-weights-oracle.html`. `RelativeFontWeightTests.cs` and Native AOT/qpdf/Poppler report/role-byte, pagination, real-font and protected-output checks target source `e94987af9b07c2d7502c25b41625b85aa393f63f`. CI and independent review must be checked on the final head; this does not create a new preview binary, public release, human PDF approval or customer licence.

## Bounded font-size keywords for printable business reports (source-only)

For existing report text and inline spans, `font-size` accepts seven absolute
keywords: `xx-small` = 7.2pt, `x-small` = 9pt, `small` = 10pt,
`medium` = 12pt, `large` = 14.4pt, `x-large` = 18pt, and
`xx-large` = 24pt. `smaller` and `larger` multiply the **computed
parent** size by 5/6 and 6/5, after normal CSS specificity and
`!important`. This deliberate 12pt reference is not complete browser
font sizing.

```html
<section style="font-size:15pt">
  <h2 style="font-size:large">Inspection summary</h2>
  <p style="font-size:smaller">Work order <span style="font-size:larger">Complete</span></p>
</section>
```

The values change actual PDF text metrics, wrapping, line placement and
page breaks. Existing `inherit/initial/unset`, supported element scopes and
source-order cascade remain intact. Unsupported keywords, `calc()` and
relative results outside 1–144pt reject with `FPDF1202`, preserving
existing PDF bytes and output position. This is neither font family
matching, an arbitrary `font` shorthand nor general CSS Fonts support.

The [40-entry report](../examples/report-font-size-keywords.html) and
[independently authored absolute-point oracle](../examples/report-font-size-keywords-oracle.html)
feed NUnit, fontless/TrueType PDF-byte comparisons and a Linux Native AOT
qpdf/Poppler PDF and protected-recovery gate.
Implemented in source [`b95f1bd3`](https://github.com/KoalaFacts/FactsPDF/commit/b95f1bd3c5d8275bff21f399c9cf59a2d413c024).
Actual CI, independent review, private software build, human page approval
and customer distribution permission remain separate.

## Physical pica and quarter-millimetre CSS print units (source-only)

Controlled business reports now accept `pc` and `q` alongside `pt`, `px`, `mm`, `cm` and `in` in **existing, applicable physical CSS length slots**. One pica is **12 PDF points** and one `q` is **0.25mm** (so `4q` matches `1mm`). Example:

```html
<style>
  .report { width:14pc; padding:4q 1pc; border:4q solid #23456a; margin:1pc 0 2pc }
  .report h2 { font-size:1pc; line-height:1pc; margin:0 }
</style>
<article class="report"><h2>Service register</h2><p>Equipment checked.</p></article>
```

These units feed the same real PDF text/font-size, line-height, text-indent,
letter/word spacing, existing report-panel and text margin/padding
shorthands/longhands (including LTR logical sides), width/height constraints
and painted border/corner geometry as equivalent physical lengths. Existing
per-property applicability, box sizing, cascade, `!important`, inheritance,
fragmentation, resource limits and error codes are unchanged. For example,
`font-size:13pc` exceeds 144pt and `padding:-1q` rejects `FPDF1202`,
preserving any existing PDF output.

This does **not** support unknown CSS properties/elements, `calc()`,
negative box distances, general Paged Media or CLI page-geometry flags
such as `--margin 1pc`. The 40-row
[pc/q source report](../examples/report-print-pica-q.html) and
[independently authored point/mm oracle](../examples/report-print-pica-q-oracle.html)
are verified by `ReportPrintSpacingTests.cs` and the real Linux/Windows
Native AOT stylesheet validator with qpdf/Poppler, text, page and
PDF-byte equality plus failure/recovery.

Implementation commit: `ca3aef91e20792782edb49004369dfbf1e9712b3`. This source-only addition is not a
relabelled retained private candidate, public package, human PDF approval
or customer distribution grant.

## Current-source font-relative report image sizing (candidate, PR #196)

Bound JPEG/PNG `img` elements can use computed `em/rem` image dimensions
(`width`, `min/max-width`, `height`, `min/max-height`) and
`padding`. These values affect the real PDF image matrix, including
direct fixed-track table-cell image items. `em` uses the final computed
element font and `rem` the root font. Existing `!important`, CSS-wide
winners, box-sizing, aspect ratio, page geometry and protected output remain
enforced. Height percentages, `calc`, image URL loading, table structural
`em/rem` and general CSS Sizing remain explicitly unsupported.

The physical-only image notes below record earlier milestones; their
historical prohibition on image `em/rem` does not apply to this newer,
still unmerged source candidate. Refer to
[the bounded image contract](images.md#font-relative-image-boxes-for-reports-candidate-source-pr-196)
and [real PDF oracle tests](../tests/FactsPDF.Tests/FontRelativeImageSizingTests.cs).
Source `145b1bd5dc7ceb4d6b90fbe6c6c32259b0c2f430`; CI and independent
review are distinct from implementation.

## Current-source physical CSS height for bound report images

The existing `height` name now affects actual PDF image matrices on
explicitly supplied JPEG/PNG `img`, standalone or as direct fixed-column
cell items: `auto` or bounded `pt/px/mm/cm/in`. Image percentages, em/rem,
calc and object-fit remain rejected. Height alone derives width using the
resource pixel ratio; explicit width and height set the PDF axes separately.
Min/max-width affect width only; border-box deducts vertical edges once.
Real text/image placement, page movement, PNG reuse, strict diagnostics and
protected output are tested with the [36-record report](../examples/report-image-heights.html)
and its [independent point-width control](../examples/report-image-heights-oracle.html)
through NUnit and Linux/Windows Native AOT/qpdf/Poppler. This is bounded
source `7afae6b56ba8ddbaad2f434cfb4a9d35f8b7a2dd`, not a new private candidate, human approval or release.
[Full image scope](images.md#explicit-physical-image-heights).

## Current-source bounded responsive report image widths

The existing `min-width`, `max-width` and `box-sizing` CSS subset now
also applies to explicitly supplied standalone images and direct fixed-column
cell images, and `min-width`/`max-width` to a table's used width
([table width constraints](tables.md#table-width-constraints)). `max-width:100%` caps the **actual PDF content/image matrix**
to available parent/cell content width, and `box-sizing:border-box`
includes the image's padding/borders in the used width. `min-width`
can enlarge the used image width and wins above `max-width` when both
conflict. Image height always preserves resource aspect ratio. CSS-wide
keywords, selector specificity, source order and `!important` use the
existing block property pipeline. No implicit shrink, image height,
intrinsic keywords, `calc()`, `em/rem` image dimensions, automatic resource
fetch, table/column sizing or image splitting. Explicit errors preserve
the existing destination. [Image contract](images.md#responsive-standalone-and-cell-image-widths)
and the [36-record actual PDF comparison](../examples/report-responsive-images.html)
are at source `b6bebe8e3b97f13190148c6f53bed49ec2678ede`.
This is not a private package rebuild, publication or full CSS Sizing.

## Current-source word-break for invoice identifiers

**Bounded supported values:** `word-break:normal | break-all | break-word` on explicit
paragraphs/headings, including flat `p` in fixed-width cells. Normal preserves
existing wrapping and PDF bytes. Break-all offers a potential soft line break
between **each supported simple glyph** rather than only after the whole word;
it fills available width even when the word would fit on a fresh line. PDF text
positions, page counts and font-measured advances really change; the same
pagination, font, display-command and output-byte limits continue to apply.
`white-space:nowrap` and `pre` still disallow automatic breaking. The legacy
`break-word` value (Bootstrap's `.text-break`) is, per CSS Text 3, `normal` plus
`overflow-wrap:anywhere` whatever `overflow-wrap` computes to; with no
min-content sizing here it wraps byte-for-byte like `overflow-wrap:break-word`,
even next to `overflow-wrap:normal`, and a later `normal`/`break-all` replaces it.

Every recognized declaration is validated even in an unmatched stylesheet or
when losing the cascade. Existing selector specificity, `!important`,
source order, CSS-wide `inherit/initial/unset`, per-paragraph applicability
and inherited text style are used; there is no silent fallback to a browser.
Unlike `overflow-wrap:break-word`, `break-all` is not an emergency-only
wrap. Combining characters, shaping, bidi, grapheme clusters and hyphenation
are **not** implemented. `word-break:keep-all` and the rest of the line breaking and
overflow rules are described under [Overflow, clipping and line breaking](#overflow-clipping-and-line-breaking);
a glyph wider than its box now overflows it instead of failing `FPDF1302`; unsupported values/contexts
fail `FPDF1202/1201`; missing font glyphs and unsupported Unicode retain their existing explicit errors.
All pre-output failures preserve the caller's destination bytes/position.

The [48-record source report](../examples/report-word-break-identifiers.html)
and [manual-line-break oracle](../examples/report-word-break-identifiers-oracle.html)
are compared byte-for-byte by `WordBreakTests.cs` and the actual Linux/Windows
Native AOT CLI/qpdf/Poppler gate; source ac6cec7000b308b403dd95abcd14441240b1f791.
No new private installable binary, reference-page approval, release, licence
or customer access is implied. This is not full CSS Text conformance.

## Current-source fixed-height report panels

**Exact scope:** Controlled `div`, `section`, `article`, `blockquote` report
cards outside lists and tables accept `height:auto` (initial), unitless
exact zero or bounded nonnegative `pt/px/mm/cm/in`, own-final-font `em`
and root `rem`. Winning declarations use the established selector
specificity, source order, `!important` and `inherit/initial/unset`
pipeline. The default is `auto`; it is not inherited implicitly. All
declarations, including losing and unmatched ones, are validated. Unsupported
`height` contexts are never ignored as a best-effort browser fallback.

**Real PDF geometry:** A `height:85pt` short inspection card reserves 85pt
of **content** height with `box-sizing:content-box`, plus its actual
top/bottom padding and border. `box-sizing:border-box` includes those
decorations in 85pt (but never lets them shrink below their thickness).
The existing `min-height` takes the larger lower bound when both are
declared. Backgrounds and borders paint the reserved box, and following
text moves by its physical height; an empty unpainted panel still reserves
space. A card that fits a fresh PDF page is moved whole when it cannot fit
in the remaining space. No silent overflow or clipping is allowed.

**Fail-closed boundaries:** If natural content would exceed that
chosen fixed/minimum outer height, including authored line and paragraph
spacing, or would need a second page, conversion fails `FPDF1302`,
preserving the existing PDF bytes and output position. `height:auto`
retains the previous natural, fragmentable layout. Percentages,
negative numbers, `calc()`, `max-height`, `vh/lh`, height on paragraphs/
headings/body/inline, images, tables, lists or nested list/table panels
fail explicitly with `FPDF1201`/`FPDF1202`. This is not general CSS
Sizing, overflow, flex/grid or arbitrary HTML-to-PDF support.

The [40-card facilities report](../examples/report-fixed-height-cards.html)
has a [separate minimum-height physical layout
control](../examples/report-fixed-height-cards-oracle.html), checked in
real Linux/Windows Native AOT CLI PDF bytes, qpdf integrity, Poppler text
positions and protected failure/recovery paths. Source NUnit
`ReportPanelFixedHeightTests.cs` checks cascade, computed font units,
paint, page movement, overflow and managed CLI/API parity.
Source implementation at `9f141fbc33bb2c4a3764b12c6c8c52b8c2bd00bc`
does not update an old private binary, satisfy human page review or grant
customer distribution rights.

## Current-source minimum-height report panels

**Supported:** Ordinary `div`, `section`, `article` and `blockquote` outside
lists and tables can set `min-height` to a bounded nonnegative `pt`, `px`,
`mm`, `cm` or `in` length, unitless exactly zero, a root-computed `rem`
or own-final-font `em`. All lengths resolve to PDF points before layout.
Only actual CSS winners resolve against computed font sizes; unmatched and
losing declarations still undergo syntax/value/applicability validation.
The default initial `min-height` imposes no minimum. It does not inherit by
default. Explicit `inherit` takes the direct parent's computed minimum;
`initial` and `unset` reset to no minimum. Ordinary selector specificity,
source order and `!important` apply without a separate cascade.

**Visible PDF behavior:** The chosen minimum reserves actual vertical page
space for the panel, including empty unpainted panels. For
`box-sizing:content-box` (the default), the specified minimum is inside
vertical padding and borders; with `box-sizing:border-box`, the minimum
includes those decorations (never less than their combined thickness).
Normal content that naturally occupies more height remains the winner:
text lines, fonts, backgrounds, borders and page continuations keep existing
behavior. A short minimum-height panel moves whole to a fresh page when it
cannot fit at the current cursor. A minimum too tall for a fresh page and
its required leading spacing fails `FPDF1302` rather than silently
overflowing. Naturally long boxes can still fragment across pages, with
no extra phantom minimum-height pages. Existing `MaxPages`,
`MaxDisplayCommands`, CSS work and destination protection remain active.

**Excluded:** Unrestricted `height` and `max-height` remain unsupported;
a separate one-page fixed-`height` report card mode exists above, not
a general CSS sizing engine. Percentages (indefinite paged content height),
`auto`, negative values, `calc()`, viewport/`lh` lengths, implicit
table/list/image/inline sizing, and `min-height` on `p`/headings/body,
tables or any list/table descendant explicitly fail
`FPDF1201`/`FPDF1202`. Overlarge or unplaceable minimum geometry rejects
`FPDF1302`; the previous caller PDF bytes/position are preserved.
A scoped [inspection example](../examples/report-min-height-cards.html),
`ReportPanelMinHeightTests.cs`, and
[a 40-card Native AOT comparison](../examples/report-min-height-cards-native.html)
against an independently specified `padding-top` PDF oracle verify true
text/paint geometry, font-unit parity, page movement, native output,
failures and recovery. This source addition does not rebuild the retained
preview, approve unseen reference pages or grant customer rights.


## Current-source rounded business-report cards (2026-11-01)

Ordinary `p`/`pre`/`h1`–`h6`/`div`/`section`/`article`/`blockquote`
outside lists/tables support the one-to-four-value physical `border-radius`
shorthand. The physical `border-top-left-radius`,
`border-top-right-radius`, `border-bottom-right-radius` and
`border-bottom-left-radius` longhands each set one independent corner.
Every value is zero or 0.25–72pt in `pt`, `px`, `mm`, `cm`, or `in`. Two values alternate opposite corners (TL/BR and TR/BL); three values
assign TL, TR/BL, BR; four values assign TL, TR, BR, BL clockwise. Actual
opaque PDF backgrounds and uniform solid border rings use separate cubic
curves at each corner. Overlapping arcs are proportionally fitted to the
available fragment. Neither text metrics nor outer dimensions change.
On continued report boxes only the first fragment's top and the final
fragment's bottom receive curved corners; continuation sides stay straight.
The shorthand is expanded into four separate cascade candidates **before**
matching. A later/more-specific longhand overrides its single corner;
`!important` competes per corner. An important shorthand beats a normal
longhand, and an important longhand overrides a normal shorthand only on
its own corner. `inherit` takes the corresponding corner from the direct
parent; `initial` and `unset` reset that corner without implicitly
inheriting other values. Unmatched declarations and losing values are
still validated. Output budget and destination protection remain active.

Percentages, elliptical `/` radii (including two axes in one corner
longhand), `em`/`rem`, box radii on table cells, images, lists or inline
elements, nonuniform/missing,
dashed/dotted borders, CSS background clipping, full CSS Backgrounds &
Borders and browser layout are **not** implemented; incompatible input
fails explicitly. `border-radius:0` preserves the old PDF bytes.
This is source capability, not part of the previously retained private
preview binary, human PDF approval or an evaluation grant. The
[54-card authored sample](../examples/report-rounded-panels.html) and
[separate 16px/12pt projection](../examples/report-rounded-panels-oracle.html),
plus the [50-card four-corner report](../examples/report-asymmetric-rounded-panels.html)
and [separately authored physical-length oracle](../examples/report-asymmetric-rounded-panels-oracle.html),
plus the [50-card corner-longhand report](../examples/report-corner-longhands.html),
exercise actual PDF bytes and pagination; NUnit and Linux/Windows
Native AOT/Poppler/qpdf tests verify painted curves, byte-for-byte
shorthand/longhand equivalence and protected-error recovery.

## Current-source font-relative padding on fixed-column table cells (candidate)

Invoice templates with fixed column tracks may use nonnegative, bounded
`em` and `rem` in `padding`, its four physical side longhands,
and LTR `padding-inline` / `padding-block` (including their longhands)
on actual `td` and `th` cells. `em` uses the cell's final computed
font size after the cascade; `rem` uses the computed HTML root size
(or configured initial font when the root is absent). The result is a
physical cell inset before row measurement. It changes real PDF glyph
positions, wrapping, backgrounds, fixed-track content widths and
page continuation/repeated headings, rather than accepting CSS syntax alone.
Supported percentages still use the track width as before.

Example: with a 16pt root and a 10pt cell, `padding:0.2em 0.25rem`
is `padding:2pt 4pt`. Cascade, source order, `!important` and
CSS-wide keywords continue to use the existing computed side values.
The [24-row invoice](../examples/report-cell-relative-padding.html)
has an [independently authored point-value oracle](../examples/report-cell-relative-padding-oracle.html);
[actual HTML-to-PDF equality, page/header, geometry, font and protected-error
tests](../tests/FactsPDF.Tests/TableCellRelativePaddingTests.cs)
exercise the same fixed table layout and paint path.

This does not add font-relative sizing to `table`, `col`, rows,
table groups or table cell widths, or give nested cell paragraphs their own
box insets. Negative/oversized values, `calc()`, impossible track geometry
and unsupported elements still fail, preserving any existing PDF output.
It is not generic CSS Tables, Grid, or Flex; candidate only until
review, exact-head CI and normal integration.

## Current-source bounded vertical alignment for fixed-column table cells

Fixed-column `td` and `th` accept the real PDF-rendered values
`vertical-align:top`, `middle` and `bottom` for an intact table row.
The shorter cell's text and direct image placements move by half/all
the available vertical gap, while backgrounds, borders and column
dimensions remain unchanged. Whole rows and repeated headers work in
multi-page reports, with the existing selector cascade and `!important`.

`inherit` reads the computed parent; `initial`/`unset` return to
non-inherited `top`. No alignment is silently ignored: split-body-row
middle/bottom fails `FPDF1302`, baseline/sub/super/length/percentage
values on a cell fail `FPDF1202`, and non-cell targets fail `FPDF1201` —
except the initial value `baseline`, which changes nothing outside cells and
is validated and dropped there, even on void elements such as `br` (resets such as `*{vertical-align:baseline}`
render while no table cell matches).
This does not implement general CSS inline vertical alignment or
CSS Tables. See [the table contract](tables.md#vertical-alignment-in-whole-table-rows),
[the source tests](../tests/FactsPDF.Tests/TableVerticalAlignmentTests.cs)
and [the 40-row report](../examples/report-table-vertical-align.html).
This is source-only, not a rebuilt private candidate or authorised distribution.

# Embedded CSS stylesheets and selectors


## Current-source colored and patterned report text decorations

Controlled invoice corrections, inspections and approval notes support one text
decoration line: `none`, `underline`, `line-through` or `overline`.
`text-decoration-line` chooses the line; `text-decoration-style` supports
`solid`, `dashed` or `dotted`, and `text-decoration-color` accepts `currentcolor`
or opaque `#rgb`, `#rrggbb`, named/RGB colours from the existing palette.
The `text-decoration` shorthand takes exactly one line keyword plus optionally
one supported stroke, one colour and optionally one bounded length for the
decoration thickness, in any order. `none` stands alone.
Shorthand omission resets stroke to `solid`, ink to `currentcolor` and
thickness to `auto`.
Normal declaration order, specificity, `!important` and CSS-wide
`inherit`/`initial`/`unset` work through the existing cascade.

```html
<style>
  .old { text-decoration: line-through dashed #c02020; }
  .new { text-decoration: underline dotted #008000; }
</style>
<p class='old'>Original estimate AUD 3,450.00</p>
<p class='new'>Reviewed estimate AUD 3,200.00</p>
<p style='color:blue;text-decoration:overline dashed currentcolor'>Signed off</p>
```

On supported paragraph/heading and inline text runs, the selected ink and
pattern paint an actual PDF path at a font-proportional height, clipped to
the **real measured glyph width**. Wrapped lines and subsequent pages paint
their own runs, including explicit TrueType faces. Existing solid
underline/strike-through output remains byte-identical by default.

This is a deliberately **bounded** CSS Text Decoration subset: no simultaneous
lines, `double`, `wavy`, unbounded thickness/offset,
general HTML edit semantics (though u/s now render actual lines),
browser-style descendant decoration propagation, bidi,
or complex shaping. Invalid values and targets raise `FPDF1202`/`FPDF1201`,
unpaintable geometry `FPDF1302`, and insufficient paint/byte budget `FPDF1401`.
Failures preserve the original PDF destination. Tests cover API/CLI, actual
PDF paint streams, TrueType glyph widths, inline overrides and multipage flow.
See [printable 60-entry report](../examples/report-text-decoration-paint.html)
and [regressions](../tests/FactsPDF.Tests/TextDecorationPaintTests.cs).
Exact-head CI, independent review, retained binary and human PDF approval
remain separate from this source implementation.
## Current-source computed font-relative border widths (candidate)

The existing bordered-report path accepts `em` (own final computed
`font-size`) and `rem` (computed HTML root) on all supported physical and
LTR logical border-width declarations, including `border` and `border-width`
shorthands. The winning length is converted once to a fixed point width before
box measurement, PDF paint and pagination. CSS selector specificity, inline
styles, `!important`, `inherit/initial/unset` and independently resolved
side values are preserved. Unmatched invalid declarations still fail closed.

Scoped to previously painted ordinary blocks/paragraphs/headings, bound
images, and explicit fixed-column tables / cells. The element's computed font
must be known; col/row/inline structural border declarations remain
inapplicable. Percentage/negative/`calc()` border widths, `em/rem` outline
widths and font-relative corner radii remain unsupported. Physical border
widths and the default unstyled output retain their existing behavior.

## Current-source bounded patterned report borders

The existing CSS box targets (ordinary report boxes, images and fixed-layout
tables/cells where borders were already implemented) now accept
`solid`, `none`, `dashed` and `dotted` for `border-style`,
`border-top/right/bottom/left-style`, and the corresponding `border` and
individual side shorthands. The 1–4-value `border-style` expansion and CSS
importance, specificity, source order and per-side CSS-wide keywords continue
to use the existing cascade. A style is non-inherited unless `inherit` wins.
Border widths remain the existing nonnegative, bounded absolute values and
thin/medium/thick; colours use the existing RGB (with alpha) and `currentcolor`
rules. There is no new element applicability, font synthesis or layout mode.

A nonzero dashed edge paints a PDF stroke with butt caps and an on/off
pattern of three times the resolved border width each. A dotted edge uses
round caps and a zero-approaching on segment with an off gap of twice the
width, creating visible circular marks. Each stroke is contained in the
existing border rectangle, charged as one display command, and enclosed
in PDF `q/Q` graphics state. Fill backgrounds and legacy solid rectangles
are unchanged. Patterned edges consume the *same* measured geometry as solid
ones: padding, text X/line wrapping, nested boxes and table/image positioning
are not estimates. At page boundaries vertical strokes restart their pattern;
top/bottom strokes appear only on first/last decorated fragments. There is
no global dash continuity across pages or CSS background clipping.

Only the supported patterns are painted. `double`, `hidden`, `groove`,
`ridge`, `inset`, `outset`, arbitrary `border-image`,
`border-radius` with dashed/dotted/asymmetric borders, invalid mixed shorthand terms and out-of-scope elements
fail with existing `FPDF1201/1202`; impossible widths with `FPDF1302`,
display-command limits with `FPDF1401`. Sub-millipoint patterned strokes
that would round to an invisible PDF linewidth fail `FPDF1302` instead
of silently turning into a hairline. Invalid input never overwrites a
previous PDF file; subsequent valid conversions recover normally.

The [76-record multipage report](../examples/report-patterned-borders.html)
is verified through the real Native AOT CLI, uncompressed PDF stroke
commands, Poppler item positions, page bounds and failure/recovery checks
in `scripts/verify_native_feature_paths.py`; source NUnit cases separately
check cascade, all sides, exact solid byte preservation, cross-page borders,
resource limits and invalid values. That is scoped engineering evidence,
**not** human page approval, release, or full CSS Borders conformance.

## Current-source inherited report text flow

Existing bounded `overflow-wrap:normal|break-word|anywhere`,
`word-break:normal|break-all` and nonnegative `text-indent` may now be
declared on `html`, `body` and ordinary `div`/`section`/`article`/`blockquote`
report containers outside list/table structures. The computed values inherit
to descendant `p`/headings; they affect the measured glyphs, wrapping,
first-line placement and page flow, rather than merely parsing CSS. A child
declaration, `!important` cascade or CSS-wide keyword continues to win
according to the existing scoped CSS rules. Percent indentation resolves
against the final paragraph content width; `em` uses the declaring host font.

A flat paragraph inside a fixed-column table cell may inherit these text
behaviours from an outer report container. The first line's `text-indent`
is included in the actual cell glyph X position and remaining width for
left/centre/right alignment, while later wrapped or explicit-break lines do
not indent again. Direct `text-indent` on the cell paragraph and CSS
declarations on table/list structural elements or inline spans still reject
with `FPDF1201`. Unsupported words/units and unfit glyph geometry keep the
existing `FPDF1202/1302` protection. This does not implement general
CSS Text, `word-break:keep-all`, RTL or complex
Unicode shaping. The independent HTML-to-PDF coordinate, byte and
failure-recovery tests are in
[`InheritedReportTextFlowTests.cs`](../tests/FactsPDF.Tests/InheritedReportTextFlowTests.cs).

## Current-source inherited white-space in business report containers

A declaration of `white-space:normal`, `nowrap`, `pre-line`, `pre-wrap`
or `pre` on `html`, `body`, or an ordinary
`div`/`section`/`article`/`blockquote` now sets inherited
whitespace behavior for nested paragraphs and headings. The final paragraph
uses the same measured glyph geometry, wrapping and PDF page breaks as if
that paragraph declared the computed value itself. Thus a whole report section
can opt into multiline preserved text without repeating CSS on every `p`.
The existing CSS cascade and `!important` select the container's winner;
children may override it or reset with `initial`. Both style attributes and
stylesheets take the same path. This does not authorize declarations on
inline elements, list/table structural elements or image boxes, nor new
whitespace keywords, tabs, shaping or general browser layout.

```html
<style>.customer-note { white-space: pre-line }</style>
<section class="customer-note"><p>Work completed
Follow up Friday</p><p style="white-space:normal">Invoice settled</p></section>
```

The [inherited-mode PDF tests](../tests/FactsPDF.Tests/InheritedWhiteSpaceReportTests.cs)
use explicit-child style oracles, a 48-entry paginated report and protected
invalid-input recovery. Existing paragraph/table-cell limits remain unchanged.

## Current-source multiline report notes

Explicit `p` / `h1`–`h6` report text, including an explicit paragraph in
a fixed-width table cell, accepts `white-space:pre-line`. This **changes actual
PDF glyph baselines and pagination**, not only the CSS parser. ASCII space and
tab runs collapse like `normal`; LF, CRLF (one break), CR, and form feed retain
logical line breaks. Spaces adjacent to those breaks disappear; a final authored
segment break is ignored, but consecutive interior breaks create empty lines.
HTML `<br>` remains an explicit break, including after a source break.
Soft wrapping, word measurement, font fallback, `overflow-wrap:break-word`,
`text-align`, first-line indent and all existing box resource limits still apply.

```html
<p style="white-space:pre-line">Asset 018
  Inspection complete
  Follow-up next week</p>
```

Stylesheet selectors, inline declarations, `!important`, `inherit`,
`initial`, `unset`, and element computed inheritance follow the same
whole-paragraph cascade as `normal`/`nowrap`. `pre-line` does **not** retain
runs of spaces or tabs, allow arbitrary preformatted text, or enable RTL,
vertical writing, bidi, complex shaping, negative length or new font sources.
Explicit declarations on spans, images and table structural elements
still fail `FPDF1201`; ordinary document/report containers can now set inherited white-space. `break-spaces`, functions and mixed
keyword values still fail `FPDF1202`, including unmatched selector declarations.
Overwide unbreakable tokens fail `FPDF1302`; missing fonts retain their
existing resource errors. Failed CLI/API output preserves a previous PDF.

The [72-record authored note report](../examples/report-pre-line-notes.html)
and [separate HTML-br oracle](../examples/report-pre-line-notes-oracle.html)
exercise actual output bytes and paginated glyph placement, including
Linux/Windows Native AOT and Poppler checks via
`scripts/verify_native_feature_paths.py`. Initial source implementation
commit: `6ab5d2c8ff3c10c76e928b6ad192d9ac46965c89`. This source scope
does not imply current package publication or human page approval.

## Current-source preformatted audit blocks

The explicitly closed `<pre>` element is a bounded **standalone report
paragraph**. It has the same default 1em margins as a
plain `p`, but its default whitespace flags are equivalent to
`white-space:pre`: authored U+0020 spaces (including leading, consecutive
and trailing), CRLF/CR/LF/form-feed line breaks and explicit `<br>` become
measured PDF advances/lines. A line never automatically wraps at words,
spaces or CJK opportunities. Each line must fit the computed content width
and first-line indent, otherwise `FPDF1302` leaves the previous PDF intact.
Only glyphs supplied by the caller's font roles or built-in Courier can paint;
there is no default browser font family or inferred mono-style substitution.

```html
<pre>REF001   OK
REF002   NEEDS REVIEW
TOTAL    002</pre>
```

`white-space:pre` also applies on `p`/headings including explicitly styled
`p` inside a fixed-column table cell; author CSS on `pre` may choose other
supported whole-paragraph modes. Selector/inline cascade, `!important`,
CSS-wide `inherit`/`initial`/`unset`, real font metrics, page continuation,
text colours and output recovery use the existing paragraph engine.
No `pre` within a table/list, no nested block elements within `pre`, no
unclosed `pre`, tab stops, complex shaping, RTL or `break-spaces`.
In preserving modes, unsupported control whitespace (including tab)
raises `FPDF1305`. Inline `white-space` declarations remain unsupported
(`FPDF1201`); invalid values raise `FPDF1202`.

The [72-row source example](../examples/report-preformatted-audit.html)
and [separately authored pre-wrap equivalent](../examples/report-preformatted-audit-oracle.html)
must produce byte-identical PDFs while every authored line fits. Native
AOT/Poppler geometry checks measure the extra two-space x advance and
confirm page text, overflow failure and output restoration. Source commit
`d8d1a9e083bfc83829275ec08f6038261b4e1663`; this is not full HTML
`pre` support, a released CLI or a human-approved reference PDF.

## Current-source preserved-space report notes

For authored notes with meaningful horizontal spacing, explicit paragraphs
and headings can opt into `white-space:pre-wrap`. Unlike `pre-line`, each
literal U+0020 space is measured and painted as a glyph, including spaces at
the start/end of a line and repeated spaces between labels. CRLF (including
across styled spans), CR, LF and form feed create real line boxes. `br`
continues to force a break. Ordinary soft word wrapping still happens at
spaces and existing CJK opportunities, with actual page continuation and
full destination-file protection on failed conversion. Leading spaces count
towards the available line width; when necessary they wrap onto another
line instead of disappearing. CSS `text-align`, inline font metrics,
font fallback, `overflow-wrap:break-word`, and existing resource budgets
operate on the measured output.

```html
<p style="white-space:pre-wrap">REF001  ASSET   CHECK
  OWNER:  AB</p>
```

Boundaries: only whole explicit `p`/`h1`–`h6` (including explicit
paragraphs in fixed-width table cells). Container/span/structural-table
declarations remain `FPDF1201`. Tab characters and other unimplemented
control whitespace need tab-stop/text processing and fail `FPDF1305` rather
than collapsing silently. `pre`, `break-spaces`, CSS Text shorthand
combinations, RTL and complex shaping are unsupported (`FPDF1202` for
unsupported values). Extremely long words, or a single space wider than
the measured line, fail `FPDF1302`. This is a documented bounded PDF
report behavior, not general browser CSS `pre-wrap` conformance or a
released preview binary.

The [72-entry preserved-space report](../examples/report-preserved-space-notes.html)
has a [separately authored collapsed-space control](../examples/report-preserved-space-notes-oracle.html);
`WhiteSpacePreWrapTests.cs` checks exact glyph runs, TrueType metrics,
CSS cascade, page continuation, table cells and negative recovery.
`verify_native_feature_paths.py` checks actual Native AOT Poppler word
offsets against that control, unique references, equal line baselines,
pagination and preserved output across failed-then-successful conversion.
Source SHA of the implementation remains distinct from published binaries.

## Current-source LTR text-align start/end

The engine renders `text-align:start` and `text-align:end` on the same
supported paragraphs, headings and inherited text containers as physical
`text-align:left/right`. In the **existing left-to-right horizontal-only**
report model, `start` resolves to `Left` and `end` to `Right` as computed
text styles. Embedded selectors, inline styles, specificity, `!important`,
inheritance and `inherit/initial/unset` continue to select the resulting
physical side. No declaration is accepted then ignored: the measured
per-line PDF glyph start X, font advances, padding/width/indent and
pagination use the same renderer as explicit left/right. Default/no-CSS
output bytes remain unchanged.

Scope intentionally excludes RTL, bidirectional or vertical writing:
`direction` other than its initial `ltr` (FPDF1202) and HTML `dir` other than `ltr` (FPDF1103) are still rejected, and `text-align:start/end`
do not request either writing mode. Inline spans cannot specify start/end or justify;
unknown/multi-token values such as `match-parent`, `justify-all` and
`start end` fail `FPDF1202`, `direction:rtl` fails `FPDF1202`, inline alignment
fails `FPDF1201`, and geometric failures retain `FPDF1302`. The prior
destination PDF and stream position remain protected. Existing
`text-align:justify` behavior and its final-line/space limitations are
unchanged; this is **not** full CSS Text support.

The [46-entry paginated LTR example](../examples/report-logical-text-align.html)
and separately authored [left/right oracle](../examples/report-logical-text-align-oracle.html)
are compared as real equal PDF bytes by Native AOT with qpdf, searchable
first/last markers and protected-error recovery. Source
`LogicalTextAlignmentTests.cs` also covers glyph coordinates, cascade,
inheritance, explicit TrueType metrics, page continuation and negatives.
Exact-head CI, independent review, private candidate distribution and
human page approval remain separate gates.

## Current-source bounded quoted-report panels

`blockquote` is a **real block container** at report root or inside ordinary
body/div/section/article/blockquote contexts. It accepts direct text,
explicit paragraphs/headings and their existing permitted inline children,
with the same block CSS properties and positional painting as a `div`.
Its default outside margins (which collapse like any block margin) are **1em top/bottom and 40px (30pt)
left/right**, Chrome's. Those values are actual PDF layout insets, not ornamental
metadata; an unstyled quote is byte-equivalent to a `div` using
`margin:1em 40px`. The normal per-property cascade can override the
defaults with physical or LTR logical margin/padding declarations; explicit
`initial`/ `unset` reset to zero and `inherit` reads the computed parent
side. Inherited text colour/fonts, backgrounds, solid per-side borders,
width/min/max-width, `box-sizing:border-box`, alignment and multiline
pagination reuse their already bounded element contracts.

Unsupported: quote-in-list, quote-in-table/cell, quote-in-inline; `cite`
and browser citation metadata; CSS generated quote marks; `q`/`cite`
elements; RTL/vertical writing; negative margins; vertical
margin percentages/auto; height, arbitrary display, floating and elliptical/per-corner
rounded border painting. A single physical radius with a uniform solid ring
is separately supported. Existing unsupported CSS and unplaceable geometry reject
with `FPDF1101/1103/1201/1202/1302`, preserving the original PDF output
on conversion failure. Quoted content is **not** a tagged PDF quotation.
No separate font download or asset is involved.

[44-item paginated quoted service report](../examples/report-blockquotes.html)
and its [physical div oracle](../examples/report-blockquotes-oracle.html)
prove real PDF-byte equivalence, searchable text, page fragments and protected
failure recovery via `BlockquoteReportTests.cs` and
`scripts/verify_stylesheets.py` on supported actual CI platforms. New
source evidence is not a human PDF approval or private candidate rebuild.

## Current-source LTR logical report spacing

Four logical shorthands are supported for **normal left-to-right horizontal
writing only**: `margin-inline` maps inline-start/inline-end to
`margin-left`/`margin-right`; `margin-block` maps block-start/end to
`margin-top`/`margin-bottom`; `padding-inline` and `padding-block`
map to their analogous physical sides. One value supplies both sides; two
values supply start and end in that order. A CSS-wide `inherit`, `initial`
or `unset` must be alone; `!important` and the existing stylesheet/
inline per-longhand cascade apply normally. Expansion counts both emitted
declarations against `MaxCssDeclarations`, including unmatched rules.

This is a **rendered geometry feature**, not an accepted-but-ignored alias:
on `p`/`h1`–`h6`, `margin-block` uses nonnegative
`pt/px/mm/cm/in/rem/em` (or zero), and `margin-inline` uses the existing
bounded nonnegative physical lengths plus percentage/auto outside table cells.
On normal `div`/`section`/`article` panels and standalone `hr`, the
same physical outside-box margins are applied: horizontal `auto` can centre
an explicit width; vertical percentages/auto are rejected. Padding maps to
the existing box model on permitted painted boxes, using nonnegative
`pt/px/mm/cm/in/%/rem/em`, with vertical percentages resolved from the
containing **width**. Relative `em` uses the element's final computed font,
and `rem` the computed root font. Zero values retain original geometry.
All limits, table-cell restrictions and prohibited elements remain those of
the equivalent physical side. `inherit` reads the appropriate computed
parent side; neither margins nor padding become implicitly inherited.

The box widths, painted PDF backgrounds/borders, text starting X positions
and pagination all use the existing longhand layout. Unsupported values
(negative lengths, mixed wide keywords, three-plus terms, `calc()`,
vertical `auto`/`%` margins or impossible geometry) fail with
`FPDF1201/1202/1205/1302` as appropriate and preserve any existing
output. This feature does **not** implement `direction` beyond `ltr`, RTL, vertical
writing, logical properties beyond the eight scoped side longhands, general
`gap`, Flex/Grid or Paged Media. Vertical margins collapse as in
[block margin collapsing](#current-source-block-margin-collapsing-and-list-and-table-margins).

[Logical 36-entry report](../examples/report-logical-spacing.html) and
[separately specified physical-longhand oracle](../examples/report-logical-spacing-oracle.html)
exercise equivalent actual PDF bytes; `LogicalReportSpacingTests.cs`
covers full conversion, geometry, cascade, computed lengths, CLI parity,
fragmentation, invalid input and output recovery. Existing SDK/native CI
remains the authoritative platform gate for the exact source commit.

### Individual LTR logical margin/padding sides (eight names)

Current source additionally renders all eight **one-value** logical side
longhands: `margin-inline-start/end`, `margin-block-start/end`,
`padding-inline-start/end`, and `padding-block-start/end`. The parser lowers
**each** to exactly one existing physical side in LTR horizontal writing:
left/right for inline-start/end and top/bottom for block-start/end. They may
appear in embedded stylesheets or inline CSS alongside physical longhands and
one/two-value logical shorthands. Normal property-by-property specificity,
source order, `!important` and `inherit/initial/unset` apply; no shorthand
silently overwrites an unrelated side. A single emitted declaration consumes
one `MaxCssDeclarations` slot. All other supported element/value restrictions
are **exactly** those of the equivalent physical side, including the bounded
font-relative `rem/em` and percentage geometry.

Numeric exact-zero spellings (`-0`, `0.0`, `0e999`) normalize to zero.
Unitless nonzero or underflowing numeric forms, negative physical lengths,
multiple values, `calc()`, unsupported elements, vertical `auto/%` margins,
RTL and vertical writing reject with the existing `FPDF1201/1202/1205` or
unfit geometry `FPDF1302`, preserving any previous destination bytes. The
existing physical length parser also rejects nonzero unit values that would
round to zero in a floating-point conversion, rather than accepting them as
accidental zero box lengths. No new CSS layout model, browser
logical properties or private software candidate is introduced.

The [40-reference source report](../examples/report-logical-longhands.html)
and independently written [physical longhand oracle](../examples/report-logical-longhands-oracle.html)
are compared as **real multipage PDF bytes** by Linux Native AOT, with
qpdf, searchable text markers and protected invalid-target recovery;
`LogicalReportLonghandTests.cs` also asserts actual run X, painted boxes,
per-side cascade, root/element fonts, page continuation, CLI parity and
strict declaration budgets. Exact-head CI and truly independent code review
remain separate merge gates; this is source, not a public binary release.

## Current-source HTML horizontal report rules

A standalone void `<hr>` or `<hr />` in an ordinary report box now
produces a **visible PDF separator**: a 1px border on all four sides in Chrome's
inset shades (1.5pt tall in all) over the containing content width, with .5em outside margins
above and below. Its box is really painted and affects following text
positioning. The rule creates no text runs, new font resource or remote load.

A controlled rule accepts the existing scoped box `width`, `min-width`,
`max-width`, `box-sizing`, `padding`, `background` and solid
`border` properties, `color` and four-sided `margin` with nonnegative
`pt/px/mm/cm/in/rem/em` top/bottom and nonnegative horizontal
`pt/px/mm/cm/in/%/rem/em` or `auto`. Only `font-size` is accepted
from typography, as a scale for rule-local `em` values. The existing
selector/inline cascade, importance and `inherit/initial/unset` apply.
The rule's actual height is vertical padding plus effective top/bottom
solid border widths. `border-top:none` removes the top edge of the default
ink and `border:0` all of it; no minimum synthetic content height is added.

A separator moves as one box to a fresh page if it cannot fit after prior
report content. A rule too tall for a fresh page fails `FPDF1302`
without altering any existing target PDF. Its painting is charged against
display command limits and preserves containing panel fragments.

`hr` is rejected inside paragraphs/headings, spans, lists or tables
(`FPDF1101`), as are explicit `</hr>` closing tags; unrecognised
attributes fail `FPDF1103`. Text-only declarations such as
`text-align`, `font-weight` or `line-height` reject with
`FPDF1201`. Negative/percentage vertical margins, unsupported functions
and unsupported dashed/rounded border combinations fail `FPDF1201/1202`. This does not
implement `height`, pseudo-elements, `display`,
complex rules, table/inline separators or general CSS conformance.

The [44-entry report](../examples/report-horizontal-rules.html) and
[independent painted-div oracle](../examples/report-horizontal-rules-oracle.html)
are compared via native CLI PDF bytes, qpdf, extracted text, pagination and
protected-output failure in `scripts/verify_stylesheets.py`.
`HorizontalRuleTests.cs` covers managed PDF equality, exact geometry,
selector precedence, computed lengths, page edges and budgets. New source,
CI and review evidence do not relabel old private candidates or provide
human page approval.

## break-inside: avoid

`break-inside:avoid` and the legacy `page-break-inside:avoid` keep a block
box on one page the way Chrome prints it. They apply to panels (`div`,
`section`, `article`, `header`, `footer`, `main`, `nav`, `aside`, `hgroup`,
`search`, `blockquote`, `figure`, `figcaption`, `address`, `dl`, `dt`, `dd`),
paragraphs (`p`, `pre`), headings, lists (`ul`, `ol`, `menu`), list items and
`table`, with any block content inside: paragraphs, headings, lists, tables,
nested panels, images, flex and grid rows, and boxes with padding, borders, a
background, a `min-height` or their own margins. `auto` is the default; the
aliases share one cascade slot, and `inherit`, `initial`, `unset` and
`!important` behave as for any box property.

```html
<div style="break-inside:avoid">
  <h3>Findings</h3>
  <table>...</table>
</div>
```

The rules, each checked against Chrome (headless, `--print-to-pdf`) on
synthetic pages:

- A box that is not the first thing on its page and does not fit the rest of
  it moves whole to the next page. The box is laid out where it stands; when
  the first break inside it is one the browser may avoid (a line, a block or a
  table row that does not fit, the bottom padding or border, a flex or grid
  row that cannot be split), the layout is rolled back and the box starts the
  next page. The margin above it is truncated at that unforced break and its
  closing margin is kept, like any block at a page break.
- A box that fits stays: no page is added and the page is byte-identical to
  the page without the declaration. A box that fits exactly stays.
- A box taller than a page still moves to the next page when it does not start
  one, and then breaks normally there (lines honour orphans and widows, tables
  repeat their header). Avoid is ignored only for the part that cannot fit.
- A box that already starts its page never moves, so no empty page appears.
- A forced break (`break-before`/`break-after` on the box or on a child)
  is honoured where it falls. Only the first break inside the box decides: when
  it is forced the box stays, when it is an unforced break the box moves; a
  later unforced break after a forced one is an ordinary break.
- Nested boxes each decide for themselves; an outer box that moves takes its
  inner boxes along, and an inner box that does not fit its own page moves
  alone inside a plain parent.
- A table with `break-inside:avoid` moves whole (caption, header and rows),
  and when taller than a page continues with its repeated header. A kept row
  keeps the behaviour in [Table rows](#table-rows).

**Not supported:** `break-inside:avoid` on a flex or grid container (a flex
or grid row is already kept whole), on inline elements, `tbody`, `tfoot`,
`td`, `th` and other table parts, and anywhere inside a table cell, which
break by cell lines (`FPDF1201`); other values such as `avoid-page` and
`avoid-column` (`FPDF1202`). A kept box that cannot be placed even on a fresh
page (a fixed `height` taller than the page, a row taller than the page that
FactsPDF keeps whole) fails `FPDF1302`. A `break-after:avoid` heading before a
kept box does not travel with it (Chrome moves the heading too); put the
heading inside the box. Failures leave the existing output unchanged.
Tests: `tests/FactsPDF.Tests/BreakInsideAvoidTests.cs`; the printable
[kept-card template](../examples/report-keep-together.html) and its
[forced-page control](../examples/report-keep-together-oracle.html) still render
as before.

### Table rows

`tr` and `thead` also accept `break-inside:auto|avoid` and the legacy
`page-break-inside`, as report stylesheets write
`.report-table tr{break-inside:avoid;page-break-inside:avoid}`. A table row
that fits a fresh page fragment with the repeated header already moves whole,
and the header never splits, so the page renders byte-identical to the page
without the declaration. A kept row that FactsPDF must split (taller than a
fresh fragment with its header, or a first body row split only to carry the
table's top padding/border or its container's leading decoration) breaks before
itself as Chrome does, or before the whole table when it is the first body row,
and then continues from the fresh page at line boundaries; where it already
starts where a fresh page would put it, it splits like the row without the
declaration. A kept last row split only to carry the table's bottom
padding/border fails `FPDF1302` at the row, because Chrome keeps it whole and
paints that decoration past the page bottom. Rows without the declaration keep
splitting as described in [tables](tables.md#long-descriptions-across-pages).
`tbody`, `tfoot`, `td`, `th` and `table` still fail `FPDF1201`;
`avoid-page`, `avoid-column` and other values fail `FPDF1202`
(`TableRowBreakInsideTests.cs`).

## Orphans and widows

`orphans` and `widows` (CSS Fragmentation 3) are accepted with the CSS
inherited cascade, `!important` and `inherit`/`initial`/`unset`, as in
`p, h2, h3 { orphans: 3; widows: 3; }`. They act on the line boxes of every
paragraph FactsPDF paginates: `p`, `pre`, `h1`–`h6`, anonymous text in report
panels, list items (the marker moves with the first line) and paragraphs in
table cells of rows that split across pages. A value is a positive CSS
`<integer>` (`3`, `+3`, `03`); values above 32767 compute to 32767, as Chrome
stores them. `0`, negative numbers, `3.0`, `3e0`, lengths, keywords other than
the CSS-wide ones and `calc()` fail `FPDF1202`.

**Every paragraph uses the CSS initial value `orphans:2;widows:2`**, as Chrome
does. Earlier versions broke a paragraph wherever the page ended, keeping a
single line at the bottom of a page or at the top of the next; such pages now
move that line as Chrome prints them (`orphans:1;widows:1` restores the old
breaks).

Placement matches Chrome, verified by printing synthetic pages in Chrome:

- A paragraph that cannot leave at least `orphans` lines on the current page
  moves whole to the next page, with its panel's leading padding and border,
  when the page already holds content. At the top of a page, or when the move
  would separate it from a `break-after:avoid` heading, it breaks where the
  page runs out instead.
- Otherwise the break moves earlier so that at least `widows` lines start the
  next page, but never before `orphans` lines: when a paragraph has fewer lines
  than both need, orphans are kept and widows are not.
- The same rules apply on every page of a paragraph longer than a page; each
  continued page counts its own lines. A paragraph's painted box ends at the
  page bottom when its text breaks early, as in Chrome.
- A `break-after:avoid` heading group moves to the next page exactly when the
  paragraph after it cannot leave its orphans after the headings, even when
  the group is taller than a page; otherwise the paragraph breaks inside and the
  heading stays ([heading pagination](heading-pagination.md)).
- A `break-inside:avoid` box moves whole when its first break would be an
  unforced one, so a paragraph that cannot leave its orphans there moves with
  it; inside the box, once placed, paragraphs apply orphans and widows as
  usual. Forced `break-before`/`break-after` come first.
- In a fixed-column table row that splits across pages, each cell continues
  independently and each cell paragraph keeps its orphans and widows: a later
  paragraph of a cell that cannot keep its orphans moves to the next page within
  its cell, and a paragraph that starts a cell's fragment and cannot keep them
  makes the whole row break before itself when content precedes it on the page.
  Rows that fit a fresh page still move whole (an existing difference from
  Chrome, which also splits those; see [tables](tables.md)).
- Inline text, `br`, `img`, `hr` and `col` hold no line box of their own and no
  block can nest in them, so the declaration is validated and dropped there.

Tests: `OrphansWidowsTests.cs` (Chrome's printed page splits for paragraphs,
lists, headings and table cells, byte equality, `FPDF1202`).

## Current-source print section page boundaries

For controlled LTR reports, standalone `div`, `section`, `article` and
`blockquote` outside lists/tables accept `break-before:page|auto` and
`break-after:page|auto`. Page boundaries occur at the **real panel box
boundary**, before its first background/padding/text or after its last
fragment; an opening forced break does not manufacture an empty first page.
Nested painted panels continue across the correct PDF page slices.

Existing explicit `p/pre/h1–h6` breaks remain. The legacy
`page-break-before` and `page-break-after` **names** additionally accept
`always` as the exact synonym of `page`, or `auto`; for the already
supported heading-only `break-after:avoid`, legacy
`page-break-after:avoid` is the same bounded heading hint.
Normal CSS cascade, source order, `!important`, individual
`inherit`/`initial`/`unset` and non-inheritance apply. A modern and
legacy declaration compete for the same physical break slot.

```css
.inspection { page-break-before: always; break-after: page; }
.signoff { break-before: auto; }
```

Not left/right/recto/verso, column boundaries, named pages or CSS Paged
Media. `break-inside:avoid` is defined in [its own section](#break-inside-avoid). Unsupported values and
applicability reject with `FPDF1201/1202` before replacing a customer's
PDF. Source `67566764419232585632d8ea6f2106aeeeff2f70` adds
`SectionPageBreakTests.cs`, the
[facilities report](../examples/report-section-page-breaks.html),
[independent modern syntax oracle](../examples/report-section-page-breaks-oracle.html)
and a native qpdf/Poppler PDF geometry/page/text/failure witness.
Real source implementation and a test route are not a new installable
candidate, legal rights or human PDF approval. Check exact-head CI and
independent review separately.

FactsPDF now accepts embedded CSS stylesheets in addition to inline styles. This is a bounded, strict implementation for controlled document templates, not full browser CSS. It does not add a new markup language or a browser dependency. The previous font, Unicode and optional subsetting paths are reused.

## Example

```html
<html>
<head>
<style>
  body { font-size: 12pt; line-height: 1.5; color: #222; }
  h1, h2 { color: #1d4568; }
  .report > p.lead { font-size: 16pt; }
  .report .accent { color: #008000; }
  #summary { color: #1d4568 !important; }
  p.next { break-before: page; }
</style>
</head>
<body>
  <section class="report">
    <h1>Report</h1>
    <p class="lead">Hello, world.</p>
    <p id="summary">A paragraph with <span class="accent">shared styling</span>.</p>
    <p class="next">A new page.</p>
  </section>
</body>
</html>
```

No new command-line flag is required to enable stylesheets. With the .NET 10 SDK and your own trusted, appropriately licensed static TrueType fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Font paths are examples, not shipped files. The fontless ASCII mode still works. Library callers use the same `Pdf.Write()` writer, for example `writer.ToBytes(html)`.

## Sources and selectors

| Area | Supported |
| --- | --- |
| Stylesheet source | Embedded `style` at head, document or supported container level |
| Type and media | type absent/empty/text/css; media absent/empty/all/print |
| Simple selectors | `p`, `*`, `.note`, `#summary` |
| Compound selectors | `p.note.active#summary`, `*.note` |
| Selector lists | `h1, h2, p.note` |
| Descendants | `.report p`, `.report .accent` |
| Direct children | `.report > p`, mixed child/descendant chains |
| Root pseudo-class | `:root`, `:root p`, `:root > body`, `html:root` |
| Structural pseudo-classes | `tr:nth-of-type(even)`, `li:first-child`, `p:last-of-type`, `.divider:empty` ([contract](#structural-pseudo-classes-and-not)) |
| Negation | `.divider:not(:empty)`, `p:not(.note, #summary)` (compound arguments only) |
| Pseudo-elements | `.note::before`, `.divider::after`, legacy `:before`/`:after` with string `content` |
| Inline declarations | Existing `style="..."` attributes with the same supported values |

HTML tag selectors are ASCII case-insensitive; class/ID values are case-sensitive. Class attributes are split on HTML whitespace after attribute entity decoding. CSS identifiers in this increment are ASCII; escaped and non-ASCII selector syntax is rejected. A synthetic document root is not an element and cannot match `*`. The existing partial HTML tree-construction rules still apply; no browser DOM conformance is claimed.

Style elements and style attributes inside a `<template>` are inert and never compiled, because the template and its contents are dropped (`TemplateElementTests.cs`); so are style markup and CSS text inside `<script>` data, which is skipped (`ScriptElementTests.cs`). All other style elements are compiled before content styling, so later styles can affect earlier content. Style contents use HTML RAWTEXT: entities are not decoded and fake markup inside CSS comments is not parsed as elements. The actual HTML `</style>` delimiter still ends the element even inside a CSS quote/comment. CSS comments separate lexical tokens without becoming descendant-combinator whitespace or concatenating identifiers.

No `link` stylesheet fetching, external files, `@import` or at-rules other than `@media print`/`all`, `@page` (see [CSS `@page`](#css-page-paper-margins-and-margin-boxes)) and `@keyframes` (validated and dropped) are implemented. `media="print"` on a style element is not the same as supporting a CSS `@media` rule. `@layer` is supported ([cascade layers](#cascade-layers)), as are the sibling combinators ([below](#sibling-combinators)) and [nesting](#css-nesting). Pseudo-classes other than `:root`, the structural ones and `:not()`, pseudo-elements other than `::before`/`::after` ([generated content](#current-source-before-and-after-generated-content)), escapes and other selector functions remain unsupported. Custom properties and `var()` are supported as described in [custom properties](#custom-properties-and-var).

`:root` matches the document's root element, which in HTML is the `html` element: it renders exactly like the same rule written on `html`, except that it has pseudo-class specificity (0,1,0), so `:root{color:navy}` beats a later `html{color:maroon}` (0,0,1) and ties with a class on `html`, where source order decides. The pseudo-class name is ASCII case-insensitive and composes with type, class, ID, attribute, descendant and child selectors (`:root .note`, `html:root`). A page without an `html` tag has the implied `html` element as its root, so `:root` matches it there too, exactly like `html{}` (see [implied html, head and body](#implied-html-head-and-body)). `:root` composes with the structural pseudo-classes and `:not()` (`:root:first-child`, `:not(:root)`); other pseudo-classes and pseudo-elements (`:hover`, `::before`) still fail with `FPDF1203` (`RootPseudoClassTests.cs`). Style blocks within a paragraph/span/title are rejected rather than silently reparented.

### Structural pseudo-classes and :not()

`:first-child`, `:last-child`, `:only-child`, `:first-of-type`, `:last-of-type`,
`:only-of-type`, `:nth-child()`, `:nth-last-child()`, `:nth-of-type()`,
`:nth-last-of-type()`, `:empty` and `:not()` match as Chrome matches them
(`StructuralPseudoClassTests.cs`), so report stylesheets can write zebra rows
(`.table tbody tr:nth-of-type(even) td`) and first/last row rules.

- Names are ASCII case-insensitive and count as one class in specificity.
  `:not()` takes the specificity of its most specific argument, so
  `p:not(.a, #b)` is (1,0,1).
- The nth arguments use the CSS Syntax An+B grammar: `odd`, `even`, `B`, `An`
  and `An+B`, with its sign and whitespace rules (`2n + 1` and `-n+3` are valid,
  `- n+3`, `+ n` and `2n+ +1` fail). Values beyond ±1,000,000,000 fail: Chrome
  stops matching some valid selectors near the 32-bit limits. The `of S` form
  fails.
- Every element counts as a sibling, as in the browser's DOM: `style`, `br`,
  `hr`, `col`, and `script` and `template` elements that are dropped before
  rendering. Implied `p` and `li` end tags close siblings as the HTML parser
  does, and the [document structure](#implied-html-head-and-body) is the one
  the HTML parser builds.
- `:empty` matches an element with no child element and no text. As in Chrome,
  whitespace-only text is content and a comment is not; the line feed the HTML
  parser drops after `<pre>` does not count.
- `:not()` takes a list of compound selectors (`:not(.a, p.b:first-child)`,
  `:not(:root)`, nested `:not()` up to 32 levels and 32 arguments, `FPDF1205`
  beyond). A combinator inside it fails, although Chrome accepts complex
  arguments.

Matching is charged to `MaxCssMatchOperations` (`FPDF1205`); positions are
computed in one pass over the reader's own tokens with the reader's own implied
end tags, only when a stylesheet uses a structural pseudo-class. Invalid An+B,
empty or complex `:not()` arguments, `:is()`, `:where()`, `:has()`, `:hover`
and other pseudo-classes, and pseudo-elements (`::before`, `:before`) fail
`FPDF1203`. A matched rule still fails like any other when its declaration is
unsupported on the element: `tr:nth-of-type(even){background-color:#f7f7f7}`
fails `FPDF1201` and `.divider:not(:empty){gap:1rem}` `FPDF1202`, as without
the pseudo-class.

### Sibling combinators

`+` (the next sibling) and `~` (any later sibling) select by element siblings under one parent: text and comments never count, `script` and `template` elements (which render nothing) do, an implied `p`, `li`, `td` or `html`/`head`/`body` is an element as the HTML parser builds it, and a left operand may be any compound (a type, classes, attributes, `:nth-child()`, `:not()`). They compose with `>` and the descendant combinator in any chain (`section > p + span ~ b`) and add nothing to specificity (the compounds' own do). Matching walks the element positions the structural pseudo-classes already use, charges every step to `MaxCssMatchOperations` and tries each (compound, element) pair once, so a hostile document costs a bounded number of operations (`FPDF1205` past the budget). Inside an svg the same positions apply to its shapes. A doubled, leading or trailing combinator, or one inside `:not()`, is an invalid selector (`FPDF1203`). Chrome oracle: the cases in `SiblingCombinatorTests.cs` (text, comments, scripts, templates, chains, specificity ties).

### CSS nesting

A style rule may hold declarations and rules: `.card{color:navy; .title{font-weight:bold} &.wide{width:200pt} > p{margin:0} + .note{color:gray} .dark &{color:white}}`. The nesting selector `&` (written, or implied before a selector without one, as a descendant) stands for the parent's selector list as `:is()` does: each resolved selector replaces it with one parent selector, merged into the compound it is written in, and carries the specificity of the most specific parent selector. Declarations after a nested rule apply at their own position in the cascade order. `@media print`, `@media all` and `@layer` nest inside style rules (their declarations apply to the parent selector) and in one another; at the top level `&` is the root element. A parent selector with combinators (`div > p{& span{...}}`) can be replaced only by a leading `&` (it resolves to `div > p span`); `.x &`, `& + &` and other uses would need `:is()` to match a complex selector and fail `FPDF1203`, as do a type selector merged with `&` (`span&`), `&` inside `:not()` and a rule nested under a pseudo-element. Several `&` expand to at most 64 selectors and each resolved selector counts against the selector budget. Other nested at-rules (`@supports`, `@container`, `@scope`) fail `FPDF1203`. NUnit: `CssNestingTests.cs`.

### Cascade layers

`@layer a, b;` (statement), `@layer a { ... }` (block), `@layer { ... }` (anonymous, each one distinct) and dotted sublayers (`a.b`) order the cascade as Chrome does. A layer's position is fixed where its name first appears in document order (across `<style>` elements, and inside `@media print`); a layer's own declarations rank above its sublayers; every layer ranks below the unlayered declarations, whatever their specificity. `!important` reverses the order (the earliest layer wins, a layered important declaration beats an unlayered one); the style attribute beats layers for normal declarations and loses to important ones. `revert-layer` rolls a property back to the best declaration in the layers below the declaration's own (the unlayered ones and the style attribute roll back to the last layer; with nothing below, to the user-agent style, keeping presentational hints as Chrome does). `@layer a, b {}`, a bare `@layer;` and a malformed name are invalid: `FPDF1204`, dropped when non-strict. `@import ... layer()` stays unsupported with `@import`. NUnit: `CascadeLayerTests.cs`.

### Math functions and viewport units

`calc()`, `min()`, `max()` and `clamp()` (and `-webkit-calc()`) work where the plain value does, with numbers, lengths (`pt px pc in cm mm q em rem`) and percentages, nested to 32 levels, `+ - * /` (white space around `+` and `-` is required, as in CSS), the constants `pi` and `e`, and `none` as a `clamp()` bound. Viewport units (`vw vh vmin vmax` and the `s`/`l`/`d` and `i`/`b` spellings) are percentages of the page area, the page size less the margins: with `@page{size:300pt 400pt;margin:20pt}`, `50vw` is 130pt and `25vh` 90pt, matching Chrome's print. The expression resolves when the element is computed, in the same place the plain value would: `em` is the element's own font size (the parent's inside `font-size`), `rem` the root's, and a percentage stays a percentage of whatever the property resolves it against (the containing block width for boxes, the parent's font size inside `font-size`, the element's own inside `line-height`, where it folds to points). A percentage plus a length (`calc(100% - 40pt)`) is kept as both parts and resolved against the containing block at layout. A result below zero is clamped where the property's range is non-negative (`width`, `padding`, border widths, `font-size`), as Chrome clamps it; a bare number is accepted where a number is (`line-height`, `flex-grow`). The expression is checked when the rule is read; the declaration is compiled for each element it applies to, so a value the property cannot take (a `letter-spacing` outside 0 to 24pt) fails for that element exactly as the folded value would.

Invalid expressions (a missing operator or white space, a number added to a length, a quotient with a unit on the right, an empty argument, `calc(2 * 3)` for a length) fail `FPDF1202` and are dropped when non-strict. Valid expressions that cannot be computed before layout fail `FPDF1202` in both modes: a division by zero (Chrome computes infinity), `min()`/`max()`/`clamp()` comparing a percentage with a length, units whose size the renderer does not know (`ex`, `ch`, `lh`), a negative margin result, and any math or viewport length inside the `font` shorthand or inside another function such as `rgb()`. NUnit: `CssMathTests.cs`.

### Modern colour functions

`lab()`, `lch()`, `oklab()`, `oklch()` and `color()` (`srgb`, `srgb-linear`, `display-p3`, `a98-rgb`, `prophoto-rgb`, `rec2020`, `xyz`, `xyz-d50`, `xyz-d65`) take space-separated channels (numbers or percentages, `none` for a missing channel that paints as zero, hue as a number or an angle) with an optional `/ alpha`, wherever a colour is accepted. They convert to sRGB the way Chrome writes them into its PDF: through XYZ with the Bradford D50/D65 adaptation and the sRGB transfer curve, and an out-of-gamut colour is clipped channel by channel (no chroma reduction), within half a byte of Chrome's PDF operands. Relative colour syntax (`from`), dashed `color()` profiles and the commas of the legacy syntax fail `FPDF1202`; the SVG paint attributes still take `rgb()`/`hsl()` only. NUnit: `ModernColorTests.cs`.

### Animations at print time

A page is printed at animation time zero, so an animation that names a `@keyframes` rule shows that keyframe's state, not nothing. A running animation shows the keyframe its direction starts from (`from` for `normal`/`alternate`, `to` for `reverse`/`alternate-reverse`); one waiting for a delay shows it only with a backwards fill; one that has ended (zero duration, or a negative delay past its end) shows its last keyframe only with a forwards fill, picked by the last iteration's direction. The state is laid over the cascade as the animations origin: above every normal declaration (the style attribute included), below `!important`; later animations in the list override earlier ones, and the longhands (`animation-name`, `-duration`, `-delay`, `-iteration-count`, `-direction`, `-fill-mode`, `-timing-function`) cascade one by one. Only the `from`/`0%` and `to`/`100%` keyframes can show; a keyframe declaration marked `!important` is invalid and ignored; a value that is the identity transform (`rotate(0deg)`, a whole turn, `translate(0)`, `scale(1)`) changes nothing. A state between the keyframes (a negative delay that ends inside an iteration, a fractional iteration count that ends at its fraction, `steps(2, start)`) would need interpolation and fails `FPDF1202`, as does a shown keyframe holding a value the renderer cannot honour (`opacity:0` in a fade-in: `FPDF1202` naming the keyframes rule). `animation:inherit` fails when keyframes exist. The keyframes are compiled only when an animation shows them, so unused keyframes (and keyframes for animations that never reach a state) stay free. Chrome oracle: 30 animation configurations compared pixel for pixel. NUnit: `AnimationKeyframeStateTests.cs`, `AnimationTests.cs`.

### Implied html, head and body

Every page has the `html`, `head` and `body` elements the HTML parser builds,
written or not (`ImpliedDocumentElementTests.cs`), so `body > p`, `html body p`,
`:root p` and `body{color:navy}` match a page without those tags as in Chrome.
An omitted `html` is implied first; `style`, `script`, `template`, `title`,
`meta`, `link` and `base` before any body content go into `head` (also after an
explicit `</head>`, as the parser reopens it); the first other element or
non-whitespace text implies `body`; content after `</body>` or `</html>` is
appended to `body`. Implied elements count against no element or depth limit
and carry no source position, and an implied `body` without box declarations
adds no box, so such a page's read tree and PDF are unchanged unless a rule
styles it. A repeated or misplaced `html`, `head` or `body` start tag is ignored
as the parser ignores it; with attributes, which the parser would merge into
the existing element, it fails `FPDF1101`.

A `tr` directly in a `table` (or after its `colgroup`, `caption` or `thead`) is put in a `tbody` the parser implies (the HTML "in table" insertion mode), closed by the next `thead`, `tbody` or `tfoot` or the table's end tag; it takes part in selectors like an explicit `tbody`.

`background-color` on `html` is the page canvas: as Chrome prints it, it covers each whole sheet, margins included, under every other paint. Opaque white and transparent paint nothing; other box properties on `html` still fail `FPDF1201`. With `html{display:flex}` the `body` is a flex item, so a `body` wider than the page content box shrinks to it (`flex-shrink` 1, never below its min-content width) and lays out like an auto-width body; a `body` that cannot shrink (`flex-shrink:0`, a `min-width`, or min-content wider than the page) fails `FPDF1201`.

## Custom properties and var()

A declaration whose name starts with two dashes (`--brand-700: #b3261e`) is a custom property. It is accepted on every element, in stylesheets and `style` attributes, with any value; the name is case-sensitive (`--Brand` and `--brand` differ), `--` alone is not a name (`FPDF1201`), and its value is the declared token sequence with leading and trailing white space trimmed (it may be empty: `--none:;`). Custom properties cascade by name like other declarations (`!important`, style attribute, specificity, source order), are inherited by default, and accept `initial` (the guaranteed-invalid value), `inherit` and `unset` (both the parent's value); `revert` and `revert-layer` roll the author origin back, so a custom property inherits its parent's value, as with `unset` (Chrome 158).

`var(--name)` and `var(--name, fallback)` may appear in the value of every supported property, including shorthands (`padding: 0 var(--space-6)`, `border-top: 2px solid var(--brand)`, `margin: var(--space-10) 0`), inside functions (`rgb(var(--r), 20, 30)`), with `!important`, and in custom properties (`--brand: var(--red-700)`). The function name is ASCII case-insensitive. For each element a declaration applies to, references are substituted with the element's computed custom properties (a custom property's own references are resolved first, in dependency order), the fallback is used when the referenced property has no value, and the substituted value is then parsed and checked exactly as the same literal value would be: the page renders byte-identical to the page with every `var()` written out ([example](../examples/report-custom-properties.html), [literal oracle](../examples/report-custom-properties-oracle.html), `CustomPropertyTests.cs`). A shorthand with `var()` expands into its longhands after substitution, at the shorthand's place in the cascade. `var()` in SVG presentation attributes, selectors or at-rule preludes is not supported.

Syntax is checked while the stylesheet is parsed: a `var()` that is not `var(--name)` or `var(--name, fallback)` (`var(brand)`, `var(--a --b)`, `var()`) fails `FPDF1202` even in an unmatched rule, and an unsupported property still fails `FPDF1201` first. The rest of a value holding `var()` can only be checked after substitution, so an unmatched rule's value is not checked.

**CSS-wide keywords from substitution.** A substitution that yields exactly one CSS-wide keyword applies as that keyword, as in Chrome 158, in both modes: `color: var(--x, inherit)` renders like `color: inherit`, `var(--x, revert)` like `revert`, and a custom property whose own substitution yields a keyword takes it (`--c: var(--x, inherit)` inherits `--c`; `initial` makes it guaranteed-invalid). A keyword the property cannot render still fails like the literal (`display: var(--x, initial)` is `display: initial`, which computes to `inline`); a keyword next to other components (`var(--x, initial) red`) is invalid at computed-value time.

**Invalid at computed-value time (decision).** A browser treats a declaration as unset when, after substitution, a reference has no value and no fallback (undefined, `initial`, part of a cycle, or itself invalid) or the property rejects the substituted value. In strict mode FactsPDF fails instead, with `FPDF1202` (or the code the literal value would get, such as `FPDF1201` for padding on inline text) at the declaration that holds `var()`, and the message names the reference or shows the substituted value. Reason: the renderer already rejects invalid and inapplicable values that browsers drop, including in losing declarations, instead of rendering something that differs from the author's intent; silently unsetting would render a page that differs from the literal stylesheet without saying so. Like every other matched declaration, a losing declaration is substituted and checked too. What browsers handle without any visible effect is accepted: a custom property that is never used, an unused reference cycle, and a custom property that is invalid but only referenced with a fallback (the fallback applies, as in a browser). [Non-strict mode](#strict-and-non-strict-css) treats a declaration that is invalid at computed-value time as `unset`, as a browser does, when the reference has no value or the substituted value is invalid per the specifications; a valid substituted value the renderer does not implement still fails.

## Strict and non-strict CSS

FactsPDF has one switch for CSS it rejects. **Strict mode is the default** and is exactly the behaviour described everywhere else on this page: every construct the renderer does not render exactly fails with a `FactsPdfException`. **Non-strict mode** is opt-in, `Pdf.Write().WithStrict(false)` in the SDK and `--strict false` (or `--strict=false`) in the CLI; `WithStrict(true)`, `--strict true` and `--strict=true` name the default. It changes one thing: CSS that is **invalid per the CSS specifications**, which a browser silently ignores, is ignored exactly as a browser ignores it, and each occurrence is reported as a warning carrying the code, message and source offset strict mode would have thrown. **Valid CSS FactsPDF does not implement still fails**, because ignoring it would render something different from a browser, and so does every resource limit.

The table below is the contract (class A: invalid, dropped as a browser drops it; B: valid but unsupported, fails in both modes; C: limits, fail in both modes).

| Class | Code | What | Non-strict mode |
| --- | --- | --- | --- |
| A | `FPDF1201` | A property name neither the CSS specifications nor Chrome know (`colr`, `-moz-transform`, `-o-user-select`, `--` alone) | The declaration is dropped |
| A | `FPDF1202` | A value its property's grammar cannot match: an unknown keyword (`color:bogus`, `text-align:middle`, `float:lefty`), a value out of the grammar's range (`padding:-4pt`, `font-size:-1pt`, `font-weight:1001`), an unknown unit (`5foo`), too many or repeated components (five `margin` values, two border styles), a CSS-wide keyword with other components (`inherit red`), an empty value, an unknown function, or a malformed `var()` (`var(brand)`) | The declaration is dropped; a shorthand drops all of its longhands |
| A | `FPDF1202` | A `var()` declaration invalid at computed-value time: a reference without a value and without a fallback (undefined, `initial`, in a cycle) or a substituted value its property's grammar cannot match (`--w:-2pt; padding:var(--w)`) | The declaration still wins the cascade and computes as `unset` (inherited or initial value), for each element it applies to |
| A | `FPDF1203` | CSS Syntax errors: a bad string or URL, an unmatched `)`, `]` or `}`, a stray `}` at the top level, a name without a colon, a `{}` block mixed into a value; and a string, URL, block or comment still open at the end of the style | CSS Syntax error recovery: the declaration or rule the parser drops (a bad string drops its declaration up to `;`; a stray `}` starts a rule that never gets a block); constructs open at the end are closed and kept |
| A | `FPDF1203` | An invalid selector list: an empty selector (`h1,,p`, a trailing comma), a leading, trailing or doubled combinator, `.` without a class name, an ID that is not an identifier (`#1a`), a malformed attribute selector (`[a b]`, `[a=b x]`), an unknown pseudo-class or pseudo-element (`:frobnicate`, `::frobnicate`), a page pseudo-class outside `@page` (`:first`, `:left`, `:right`), a compound selector after a pseudo-element | The whole style rule is dropped, declarations included |
| A | `FPDF1203` | An unknown at-rule inside a style rule (`p{color:navy;@frobnicate;font-size:9pt}`) | The at-rule is dropped; the rule's declarations stay |
| A | `FPDF1204` | An unknown at-rule at the top level or inside `@media` (`@frobnicate{…}`, `@frobnicate;`), and `@media` without a block (`@media print;`) | The at-rule is dropped with its block, or up to its `;` |
| A | `FPDF1204` | A `style` element whose `type` is neither empty nor an ASCII case-insensitive match for `text/css`: HTML does not process it as a stylesheet (Chrome 158 ignores `text/less`, and also `text/css; charset=utf-8` and ` text/css`: parameters and white space are not stripped) | The whole element is ignored, with its other attributes |
| A | `FPDF1204` | A malformed `@keyframes` rule (no name, `none` or a CSS-wide keyword as the name, a keyframe selector other than `from`, `to` or a percentage) | Dropped; no animation can use it |
| A | `FPDF1204` | A malformed `@layer` rule (`@layer a, b {}`, `@layer;`, a name that is not identifiers joined by dots) | Dropped whole |
| A | `FPDF1203` | A nested rule with an invalid selector list (`p{..x{…}}`) | The nested rule is dropped; the declarations and other nested rules stay |
| A | `FPDF1202` | An invalid math expression (`calc(10pt+5pt)`, `calc(10pt + 2)`, `calc(2 * 3)` for a length) | Dropped |
| A | `FPDF1202` | `padding` with `auto` and a border width as a percentage (the whole declaration, including the shorthands) | Dropped; the message says Invalid CSS |
| B | `FPDF1201` | A property the specifications or Chrome know but FactsPDF does not render (`grid-template-columns`, `table-layout`), and every applicability rule (box properties on inline text, list properties outside lists, margins in table cells, …) | Fails |
| B | `FPDF1202` | A valid value FactsPDF does not render: `position:absolute`, `display:flex`, `font-style:oblique`, `transform:rotate(5deg)`, `background:url(…)`, a computed size outside the renderer's range, a CSS-wide keyword the property cannot render (`display:initial`, also from `var(--x, initial)`), and a `var()` declaration whose `unset` the renderer cannot render (`display:var(--missing)` computes to `inline`) | Fails |
| B | `FPDF1203` | Valid selectors FactsPDF does not match: other pseudo-classes and pseudo-elements (`:hover`, `::before`, `:not(…)`), `&` needing `:is()` to match a complex selector | Fails |
| B | `FPDF1204` | Known at-rules (`@font-face`, `@import`, `@supports`, `@layer`, `@container`, `@namespace`, `@charset`, …), `@page` forms outside [its supported subset](#css-page-paper-margins-and-margin-boxes) (page selectors such as `@page :first`), media queries other than one `print` or `all` (`@media screen`, also as a `style` element's `media`), declarations directly inside `@media`, and `link` stylesheets | Fails |
| B | any | What cannot be proven invalid: values of properties without a grammar here (`box-shadow`, `background`, `transform`, `filter`, transitions, animations, grid, …), values holding a known function (`calc()`, `rgb()`), values that are valid in quirks mode (unitless lengths, hashless hex colours), arguments of functional pseudo-classes (`:is(…)`, `:nth-child(…)`), namespace prefixes and the nesting selector | Fails, as in strict mode |
| C | `FPDF1205` and the other limit codes | Every resource limit (`MaxCssCharacters`, `MaxCssSelectors`, `MaxCssDeclarations`, `MaxCssMatchOperations`, `MaxCssSyntaxNodes`, `MaxCssSyntaxDepth`, element, depth, page and output limits) | Fails |

**Declarations directly inside `@media` (decision: they keep failing).** CSS Syntax's current error recovery parses `@media print { color: red; p { … } }` as a dropped declaration followed by a valid rule, and FactsPDF's parser builds that tree, but Chrome 158 (whose `@media` block still uses the older rule-list parsing) drops `color: red; p { … }` together as one invalid rule, so the following rule never applies. Dropping only the declaration would render that rule where Chrome does not, and dropping both would contradict the specification and every other browser that follows it; the input fails `FPDF1204` in both modes instead.

**How invalidity is decided.** A construct is dropped only when it is proven invalid. Property names come from Chrome 158's property list (every CSSOM property, alias and `-webkit-` spelling confirmed with `CSS.supports(name, "inherit")`) plus the properties of current CSS specifications Chrome does not implement; pseudo-classes, pseudo-elements and at-rules likewise. Values are matched against a grammar per property written as a superset of what the specifications and Chrome accept: Chrome-only and legacy `-webkit-` keywords, quirks-mode unitless lengths and hashless colours are accepted, and any known function may stand for any value type, so a value the grammar rejects is rejected by Chrome and by the specifications. Chrome 158 accepted none of the 25,957 property-value and selector vectors the grammars reject ([`NonStrictCssTests`](../tests/FactsPDF.Tests/NonStrictCssTests.cs) pins examples). Dropping never changes how the rest of the stylesheet parses: the parse tree is the one CSS Syntax error recovery produces in strict mode too.

**Output.** A non-strict page renders byte-identical to the same page with every dropped declaration, rule or at-rule removed (a declaration invalid at computed-value time: with its value replaced by `unset`); warnings never change the output. A page without class A CSS renders identically in both modes.

**Warnings.** `WithWarnings(handler)` receives a `PdfWarnings` once per successful conversion, before the output is written, for `ToBytes`, `ToStream`, `ToStreamAsync`, `ToBufferWriter`, `ToFile` and `ToFileAsync` alike (empty in strict mode). `Items` holds `PdfWarning(Code, Message, SourceOffset)` in source order; the message has no code prefix and `ToString()` returns `FPDF####: message`, the strict exception's message. A declaration dropped for several elements (an invalid `var()` substitution) is recorded once; a repeated `style` attribute is recorded for each element. At most `MaxCssWarnings` warnings (default 1,000, set with `WithLimits(l => l.MaxCssWarnings(n))`) are stored; later ones are only counted in `TotalCount`, so hostile input cannot allocate unbounded diagnostics. A failed conversion reports no warnings: its exception explains the failure. The CLI prints each warning to stderr as `warning FPDF####: message Location: line L, column C.`, then a count of any warnings past the limit, and still exits 0.

Limits (`FPDF1205`): each value after substitution (a custom property's computed value or a property value) holds at most `MaxCssSyntaxNodes` syntax nodes and nests functions and blocks at most `MaxCssSyntaxDepth` deep (a spliced value keeps its own nesting), a chain of references between custom properties is at most `MaxCssSyntaxDepth` links long however it is resolved (a chain declared in resolution order counts the same as one resolved recursively), and every component placed by a substitution is charged to `MaxCssMatchOperations`. A doubling chain (`--b: var(--a) var(--a)`, `--c: var(--b) var(--b)`, …) therefore fails quickly with bounded memory instead of growing exponentially. A page may use at most `MaxCssCustomProperties` (default 4,096) distinct custom property names across its stylesheets and style attributes; a repeated name counts once.

Cost: an element's computed custom properties are a layer over its parent's that holds only the names the element itself declares, so an element pays for its own custom property declarations (already charged to `MaxCssMatchOperations` when offered), never for a copy of everything it inherits. A reference is looked up through the layers of the element's declaring ancestors (at most `MaxDepth`), and every ancestor layer read is charged to `MaxCssMatchOperations`. 4,000 tokens on `:root` with 20,000 elements that each declare a custom property render in about 22 MiB of allocation (previously about 2.2 GiB, one copy of the inherited tokens per element; `CustomPropertyBudgetTests.cs`).

## Cascade and inheritance

Winners are selected independently for each property:

1. Important declarations beat normal declarations.
2. Within the same importance level, inline declarations beat stylesheet declarations.
3. Stylesheet declarations use lexicographic ID/class/type specificity, then source order.

Only actually matching arms of a grouped selector contribute specificity. Repeating a simple selector increases specificity. Duplicate declarations use the last winner at the appropriate importance. This author cascade sits above the renderer's existing defaults; it does not implement user-origin styles, cascade layers, animations or transitions.

Inherited values are the parent's computed values; the parent's importance and selector specificity do not compete with declarations on a child. `inherit`, `initial` and `unset` are supported for the text properties below. `revert` and `revert-layer` (with no cascade layers, the same) are accepted for every property, in stylesheets, `style` attributes and from `var()`: the property computes as if no author declaration had been written for it, so the renderer's user-agent defaults (heading sizes, paragraph gaps, list markers) or inheritance apply, and, as in Chrome 158, the presentational hints of that property are discarded too (`ol{list-style-type:revert}` ignores `type="a"`, `div{display:revert}` shows a `hidden` div) ([`RevertKeywordTests`](../tests/FactsPDF.Tests/RevertKeywordTests.cs)). `unset` resolves to inheritance or the initial value, not a lower-priority heading/paragraph default.

Renderer initial values are the writer's base font size (`WithFonts(f => f.Size(…))`, normally 12pt), line-height normal (the first font's metrics, see the [user-agent stylesheet](#user-agent-stylesheet)), black, left alignment, zero margins and automatic breaks. The element defaults (body margin, paragraph and heading margins, heading sizes and weight, list padding and the rest) are the user-agent stylesheet's, which every author declaration overrides.

## Property scope

| Property | Values / applicability |
| --- | --- |
| font-size | pt/px/mm/cm/in lengths, positive parent-relative percentages and `em`, or positive `rem` relative to the computed `html` root font (configured initial font when absent); computed 1–144pt |
| font-weight | normal/bold, numeric hundred steps 100–900 (100–500 normal role; 600–900 bold role), or bounded bolder/lighter (selects explicit bold/normal role, saturating at endpoints), including equivalent numeric CSS tokens; real matching font chains required, no synthetic intermediate weights |
| font-style | normal/italic; explicit role resources required |
| font | The CSS Fonts shorthand `[font-style \|\| font-variant \|\| font-weight \|\| font-stretch]? font-size [/ line-height]? font-family#`, with at most four `normal` keywords among the leading ones, a CSS-wide keyword (`inherit`, `initial`, `unset`, `revert`, `revert-layer`) or a system font keyword. It expands into the longhands at the declaration's own position, with its specificity, `!important` and origin, and every part is validated and compiled by the code of its longhand (`font-style`, `font-weight`, `font-size` and `line-height` change the face, size and line height; `font-variant`, `font-stretch` and `font-family` keep no state and are validated and dropped), so the page bytes equal the page with the equivalent longhand declarations (`font:bold italic 14px/2 F` is `font-style:italic;font-weight:bold;font-size:14px;line-height:2`). The longhands the shorthand covers but does not mention are reset to their initial value: `font-style`, `font-weight` and `line-height` to `normal` (a later `font:12pt F` overrides an earlier `font-weight:bold` and `line-height:40pt`, and a later `font-weight:bold` still overrides the earlier `font`); `font-variant`, `font-stretch`, `font-size-adjust`, `font-kerning`, `font-feature-settings`, `font-variation-settings`, `font-optical-sizing`, `font-language-override` and the `font-variant-*` longhands hold no state here, so their reset changes nothing. A CSS-wide keyword applies to every covered longhand. `var()`, inline `style` and tables and lists work as for the longhands. Values the longhand cannot draw fail exactly as it does, at the offending token, in strict and non-strict mode: `small-caps` (`FPDF1202`, font-variant), a stretch keyword such as `condensed` (`FPDF1202`, font-stretch), `oblique` (`FPDF1202`, font-style), a numeric weight that is not a hundred, a zero or out-of-range size, and an empty family string. System font keywords (`caption`, `icon`, `menu`, `message-box`, `small-caption`, `status-bar`, any case, and Chrome's `-webkit-small-control`, `-webkit-mini-control`, `-webkit-control`) need a platform font and fail `FPDF1202`. A value that is invalid per the specifications (no size or no family, a repeated keyword, more than four `normal`, a weight outside 1-1000, a stray or doubled comma, a CSS-wide keyword or `default` among the families, a keyword together with a system font) fails `FPDF1202` in strict mode and is dropped with a warning in non-strict mode, as Chrome drops it. `font` is not accepted inside a page-margin box (`FPDF1201`; use `font-size`, `font-weight`, `font-style` and `font-family` there). `line-height:normal` stays the fixed 1.2 ([`FontShorthandTests`](../tests/FactsPDF.Tests/FontShorthandTests.cs)) |
| line-height | Unitless 1–10, `normal` (the default: the first font's ascent, descent and line gap; 1.2 for the built-in font), positive `%`/`em` (at most 10× element font), positive `rem` (computed root font), or `pt/px/mm/cm/in` lengths; computed 0.1–1440pt, inherited as fixed points except unitless/normal |
| color | #rgb/#rrggbb (and #rgba/#rrggbbaa); 148 named colours, transparent; bounded rgb()/rgba()/hsl()/hsla() with alpha ([translucent colours](#translucent-colours)) |
| text-align | left/center/right; bounded `justify` distributes retained U+0020 spaces only on automatically wrapped report lines via measured PDF `TJ` gaps; final/explicit-break lines stay ragged; start/end, RTL and CJK inter-character justification unsupported |
| white-space | `normal` (default), `nowrap`, scoped `pre-line`, bounded `pre-wrap` and unwrapped `pre` on explicit `p` / `h1`–`h6` or supported `<pre>`, inherited from `html`/`body`/ordinary report `div`/`section`/`article`/`blockquote`; `pre-line` collapses spaces, `pre-wrap` retains each space and soft-wraps, `pre` retains each space and source break without auto-wrap; tab stops fail `FPDF1305` in preserving modes |
| letter-spacing | Supported report text and inline spans: `normal`, nonnegative 0–24pt print lengths, or scoped `em` (own computed font) / `rem` (computed root); resolved to fixed points before real PDF glyph advances, wrapping, centring and decorations; negative/%/calc unsupported |
| word-spacing | Supported text and inline spans: `normal`, 0–24pt nonnegative print lengths or scoped `em` (own final font) / `rem` (root font); computed extra advance after each collapsed U+0020 affects real PDF placement, wrapping, alignment, decorations and pagination; negative/%/calc rejected. Not full CSS Text. |
| text-indent | Explicit `p` / `h1`–`h6` outside table cells, or inherited from `html`/`body` and ordinary report containers (including flat fixed-column cell paragraphs): nonnegative `pt/px/mm/cm/in/%`, scoped `em` / `rem`, or zero; percentages use the final paragraph content width; only the first logical line indents (with cell alignment handled in glyph placement), later wrap/`br`/page fragments retain normal x; direct table-cell declarations still reject; unfit words fail `FPDF1302` without replacing the output |
| text-decoration, text-decoration-line | none/underline/line-through as a single value; real PDF text-run decoration, current text colour, no full browser decoration features |
| margin-top, margin-bottom | Ordinary p/pre/headings, div/section/article/blockquote report panels and standalone hr support nonnegative print/rem/em/% lengths or zero. Vertical percentage bases are containing content widths, affecting real PDF outside margins and pagination. Direct percentage margins in lists/tables and table-cell p rem/em remain unsupported. `ul`, `ol`, `menu`, `li` and `table` also take lengths, `em`/`rem` and CSS-wide keywords; vertical margins of all block boxes collapse as in CSS 2 and are truncated at unforced page breaks ([block margin collapsing](#current-source-block-margin-collapsing-and-list-and-table-margins)). |
| margin-left, margin-right | Paragraphs/headings only outside table cells; nonnegative pt/px/mm/cm/in/%/rem/em or zero, plus `auto`; percentages use containing width, rem uses computed html root, em uses own computed font; auto side takes remaining free space |
| margin (bounded shorthand) | Ordinary p/pre/headings and scoped div/section/article/blockquote panels; 1–4 TRBL values with nonnegative vertical print/rem/em/%/zero, horizontal print/rem/em/%/zero/`auto`. Vertical percentages use containing content width, not height. `margin:0 auto` centres explicit-width boxes; CSS-wide keywords and per-side cascade work. List/table paragraphs retain their restrictions. |
| break-before, break-after; page-break-before/after aliases | Explicit p/pre/headings and ordinary div/section/article/blockquote report panels outside lists/tables; auto/page. Legacy aliases accept auto/always; heading-only break-after:avoid remains a bounded keep hint. Real page boundaries, not general CSS Fragmentation. [Precise print-section scope](#current-source-print-section-page-boundaries) and [heading pagination](heading-pagination.md) |
| break-inside; page-break-inside alias | `auto` / `avoid` on panels, paragraphs, headings, lists, list items, tables and table rows (`tr`, `thead`): a box that does not fit the rest of the page moves whole to the next page, a box taller than a page moves and then breaks normally, a box that starts a page or whose first break is forced stays. Flex/grid containers, inline elements, other table parts and anything inside a table cell fail `FPDF1201`; other values `FPDF1202`. [Exact scope](#break-inside-avoid) |
| orphans, widows | Positive integers, inherited, on any block (`html`, `body`, report panels, `p`/`pre`/headings, lists and list items, tables and cells), initial value 2 everywhere: the minimum number of a paragraph's lines kept at the bottom of a page before a break and at the top of the next page, placed as Chrome places them, including cell paragraphs of split table rows. Inline text, `br`, `img`, `hr` and `col` drop it. `0`, negative, non-integer and other values fail `FPDF1202`. [Exact contract](#orphans-and-widows) |
| position, float, clear, overflow, overflow-x, overflow-y, visibility, z-index, opacity, box-shadow, text-shadow, transform, filter, background-image, clip-path, backdrop-filter, mix-blend-mode, isolation, font-feature-settings (also `-webkit-`/`-moz-`), font-variation-settings, font-variant-ligatures, font-variant-numeric, font-stretch, font-optical-sizing, font-size-adjust, font-variant, font-variant-caps, font-variant-east-asian, font-variant-alternates, font-variant-position, font-language-override, font-variant-emoji, font-palette, text-emphasis, text-emphasis-style, direction, unicode-bidi, writing-mode, text-orientation, text-combine-upright, column-count, column-width, columns, column-gap, row-gap, gap, column-span, column-fill, column-rule, column-rule-style, text-overflow, line-clamp (and `-webkit-`), box-decoration-break (and `-webkit-`), rotate, scale, translate, perspective, transform-style, mask, mask-image (both also `-webkit-`), shape-outside, hanging-punctuation, hyphenate-character, text-spacing-trim, text-underline-position, background-attachment, background-blend-mode, background-origin, container-type, container, aspect-ratio, mask-repeat, mask-size, mask-origin, mask-clip, mask-mode, mask-composite, mask-type, animation-composition, animation-timeline, animation-range, animation-range-start, animation-range-end, scroll-timeline, scroll-timeline-name, scroll-timeline-axis, view-timeline, view-timeline-name, view-timeline-axis, view-timeline-inset, timeline-scope, view-transition-name, view-transition-class, anchor-name, anchor-scope, position-area, position-try, position-try-fallbacks, position-try-order, overlay, interpolate-size, reading-flow, field-sizing, dynamic-range-limit, caret, caret-animation, caret-shape, text-wrap-style, text-box, text-box-trim, text-box-edge, paint-order, math-style, math-shift, contain-intrinsic-size, contain-intrinsic-width, contain-intrinsic-height, contain-intrinsic-block-size, contain-intrinsic-inline-size, transform-box, top, right, bottom, left, inset, inset-block, inset-inline, inset-block-start, inset-block-end, inset-inline-start, inset-inline-end, border-image, border-image-source, border-collapse, border-spacing, empty-cells, caption-side, offset-path, offset-rotate, offset-anchor, initial-letter, text-emphasis-color, zoom, contain, content-visibility, line-break, text-justify, image-rendering | Initial value only, on any element: `position:static`, `float:none`, `clear:none`, `overflow*:visible`, `visibility:visible`, `z-index:auto`, `opacity:1` or `100%`, `box-shadow`/`text-shadow`/`transform`/`filter`/`background-image`/`clip-path`/`backdrop-filter:none`, `mix-blend-mode:normal`, `isolation:auto`, `font-feature-settings`/`font-variation-settings`/`font-variant-ligatures`/`font-variant-numeric`/`font-stretch:normal`, `font-optical-sizing:auto`, `font-size-adjust:none`, `font-variant`/`font-variant-caps`/`font-variant-east-asian`/`font-variant-alternates`/`font-variant-position:normal`, `font-language-override`/`font-variant-emoji`/`font-palette:normal`, `text-emphasis`/`text-emphasis-style:none`, `direction:ltr`, `unicode-bidi:normal`, `writing-mode:horizontal-tb`, `text-orientation:mixed`, `text-combine-upright:none`, `column-count`/`column-width`/`columns:auto`, `column-gap:normal`, `column-span`/`column-rule`/`column-rule-style:none`, `column-fill:balance`, `text-overflow:clip`, `line-clamp`/`-webkit-line-clamp:none`, `box-decoration-break`/`-webkit-box-decoration-break:slice`, `rotate`/`scale`/`translate`/`perspective:none`, `transform-style:flat`, `mask`/`mask-image`/`-webkit-mask`/`-webkit-mask-image`/`shape-outside:none`, `hanging-punctuation:none`, `hyphenate-character`/`text-underline-position:auto`, `text-spacing-trim:normal`, `background-attachment:scroll`, `background-blend-mode:normal`, `background-origin:padding-box`, `container-type:normal`, `container:none`, `aspect-ratio:auto`, `mask-repeat:repeat`, `mask-size:auto`, `mask-origin`/`mask-clip:border-box`, `mask-mode:match-source`, `mask-composite:add`, `mask-type:luminance`, `animation-composition:replace`, `animation-timeline`/`view-timeline-inset:auto`, `animation-range`/`animation-range-start`/`animation-range-end:normal`, `scroll-timeline`/`scroll-timeline-name`/`view-timeline`/`view-timeline-name`/`timeline-scope`/`view-transition-name`/`view-transition-class:none`, `scroll-timeline-axis`/`view-timeline-axis:block`, `anchor-name`/`anchor-scope`/`position-area`/`position-try`/`position-try-fallbacks`/`overlay:none`, `position-try-order:normal`, `interpolate-size:numeric-only`, `reading-flow:normal`, `field-sizing:fixed`, `dynamic-range-limit:no-limit`, `caret`/`caret-animation`/`caret-shape:auto`, `text-wrap-style`/`text-box-edge:auto`, `text-box-trim:none`, `text-box`/`paint-order`/`math-style`/`math-shift:normal`, `contain-intrinsic-size` and its `-width`/`-height`/`-block-size`/`-inline-size` longhands at `none`, `transform-box:view-box`, `top`/`right`/`bottom`/`left`/`inset` and the logical `inset-block`/`inset-inline` (with `-start`/`-end`) at `auto` (only `position:static` is accepted, so offsets never move a box), `border-image`/`border-image-source:none`, `border-collapse:separate` and `border-spacing` at one or two zero lengths (`0`, `0 0`; table cells already paint separate border boxes that touch without spacing, so `collapse` and non-zero spacing fail `FPDF1202`), `empty-cells:show` (empty cells already paint their background and borders) and `caption-side:top` (no `caption` element is accepted), `offset-path:none`, `offset-rotate`/`offset-anchor:auto` (no box is placed along a motion path), `initial-letter:normal` (no drop cap is drawn) and `text-emphasis-color:currentcolor` (no emphasis mark is painted), `zoom:normal` (or `1`/`100%`), `contain:none`, `content-visibility:visible`, `line-break`/`text-justify`/`image-rendering:auto` (and CSS-wide keywords, which compute to it). Normal flow already lays out exactly this, so the declaration is validated and dropped and the PDF bytes equal the undeclared page; common reset rules in stylesheets therefore no longer fail. Every other value (`position:absolute`, `float:left`, `overflow:hidden`, `visibility:hidden`, `z-index:1`, `opacity:0.5`, `box-shadow:0 0 2pt #000`, `transform:rotate(90deg)`, `background-image:url(…)`, `font-feature-settings:'tnum'`, `font-stretch:condensed`, `zoom:2`, `contain:layout`, `line-break:anywhere`, `background-attachment:fixed`, `container-type:inline-size`, `aspect-ratio:16 / 9`, `mask-size:cover`, ...) fails `FPDF1202`; no positioning, floats, clipping, hiding, stacking, transparency, shadows, transforms, filters, background images, OpenType feature or variation-axis selection, optical sizing, aspect adjustment, font width selection, zoom, containment, content skipping, alternative line breaking or justification rules, image interpolation control, background blending, container queries or preferred aspect ratios are implemented ([`InitialValueNoOpTests`](../tests/FactsPDF.Tests/InitialValueNoOpTests.cs)) |
| cursor, pointer-events, user-select (and its `-webkit-`/`-moz-`/`-ms-` spellings), resize, -webkit-user-drag, -webkit-touch-callout, -ms-overflow-style, speak, font-synthesis (and its `-weight`, `-style`, `-small-caps` and `-position` longhands), text-decoration-skip-ink, text-decoration-skip (and `-webkit-text-decoration-skip`), font-kerning, quotes | Any single keyword value, on any element: every CSS UI `cursor` keyword (`auto`, `default`, `pointer`, `text`, `not-allowed`, the `*-resize` cursors, ...), `pointer-events` (`auto`, `none` and the SVG keywords), `user-select` (`auto`, `text`, `none`, `contain`, `all`), `resize` (`none`, `both`, `horizontal`, `vertical`, `block`, `inline`), `-webkit-user-drag` (`auto`, `element`, `none`), `-webkit-touch-callout` (`default`, `none`) and `-ms-overflow-style` (`auto`, `none`, `scrollbar`, `-ms-autohiding-scrollbar`) and `speak` (`auto`, `never`, `always` and the CSS 2 aural `none`, `normal`, `spell-out` that icon-font stylesheets write), plus CSS-wide keywords. `text-decoration-skip-ink` accepts only `auto` or `none`, and `font-synthesis` and its four longhands only `none`: the renderer synthesizes no bold or italic face and never breaks an underline around descenders, which is what those values allow; `text-decoration-skip-ink:all` and other `font-synthesis` values (including `font-synthesis-weight:auto`) fail `FPDF1202`; the legacy `text-decoration-skip` (normalize.css 7 and Bootstrap 4 write `-webkit-text-decoration-skip:objects` on links) accepts `none`, `auto` or `objects`, since no image or other atomic inline can sit inside a line, while `ink`, `spaces`, `edges` and other values fail `FPDF1202`; `font-kerning` accepts only `auto` or `none`, since no glyph pair is ever kerned (no kern or GPOS data is read), while `font-kerning:normal` and other values fail `FPDF1202`; `quotes` accepts `auto`, `none`, `match-parent` or one or more pairs of strings, since no `q` element or `open-quote`/`close-quote` content is accepted and no quotation mark is ever inserted, while a single string and other values fail `FPDF1202`. They only change how a screen reacts to a pointer, a drag, a long press or a scrollbar, or how a screen reader speaks, or ask for nothing the renderer would do, so the declaration is validated and dropped and the PDF bytes equal the undeclared page. `-webkit-user-select`, `-moz-user-select` and `-ms-user-select` are aliases of `user-select`. Cursor images (`url(...)`), unknown keywords, several components and other prefixes (`-o-user-select`) still fail (`FPDF1202`, or `FPDF1201` for unknown names) ([`InteractionOnlyCssTests`](../tests/FactsPDF.Tests/InteractionOnlyCssTests.cs), [`ScreenGestureHintTests`](../tests/FactsPDF.Tests/ScreenGestureHintTests.cs)) |
| font-family | A comma-separated list of family names (one string, or a sequence of identifiers) and generic families (`sans-serif`, `monospace`, `system-ui`, ...), any case, `!important` and CSS-wide keywords, on any element; validated and dropped. No font is ever discovered or matched by name: the supplied font chain (or the standard Courier fallback) is the only installed font, so every list falls back to it, as a browser does when none of the named families is installed, and the PDF is byte-identical to the page without it. `monospace` also uses that chain. Empty entries (`Arial,`), a string mixed with identifiers (`'Open' Sans`), lengths, functions such as `var()`, `default` and CSS-wide keywords inside a list fail `FPDF1202`. `@font-face` remains unsupported; the family part of the `font` shorthand is validated and dropped the same way. |
| print-color-adjust; -webkit-print-color-adjust, legacy color-adjust and non-standard background-color-adjust aliases | `exact` or `economy` (and CSS-wide keywords), on any element. `economy` only permits a user agent to adjust colours; the renderer always paints backgrounds, borders and text colours exactly as declared, which both keywords allow, so the declaration is validated and dropped and the PDF bytes equal the undeclared page. Other values fail `FPDF1202`; the deprecated `color-adjust` name and the non-standard `background-color-adjust` name (written by print resets next to both) are aliases with the same values and errors ([`PrintColorAdjustTests`](../tests/FactsPDF.Tests/PrintColorAdjustTests.cs)) |
| tab-size; -moz-tab-size alias | A non-negative number of spaces or a non-negative length (`px`, `pt`, `pc`, `in`, `cm`, `mm`, `q`, `em`, `rem`, `ex`, `ch`), and CSS-wide keywords, on any element. No tab stop reaches the page (collapsed tabs become spaces; preserved tabs fail `FPDF1305`), so the declaration is validated and dropped and the PDF bytes equal the undeclared page. Negative values, percentages, other units, keywords, strings, functions and multiple components fail `FPDF1202` ([`TabSizeTests`](../tests/FactsPDF.Tests/TabSizeTests.cs)) |
| text-rendering; -webkit-font-smoothing; -moz-osx-font-smoothing | A single keyword (and CSS-wide keywords), on any element: `auto`, `optimizeSpeed`, `optimizeLegibility`, `geometricPrecision`; `auto`, `none`, `antialiased`, `subpixel-antialiased`; `auto`, `grayscale`. They only steer a screen rasteriser; a PDF stores glyph outlines and the viewer chooses anti-aliasing and hinting, so the declaration is validated and dropped and the PDF bytes equal the undeclared page. Other values fail `FPDF1202`; `font-smooth` and other prefixed text properties fail `FPDF1201` ([`FontSmoothingHintTests`](../tests/FactsPDF.Tests/FontSmoothingHintTests.cs)) |
| text-size-adjust; -webkit-, -ms- and -moz-text-size-adjust aliases | `auto`, `none` or a non-negative percentage (and CSS-wide keywords), on any element. It only controls mobile browsers' automatic font inflation, which never applies to a PDF page, so the declaration is validated and dropped and the PDF bytes equal the undeclared page. Other values fail `FPDF1202`; other prefixes such as `-ms-text-size-adjust` fail `FPDF1201` ([`TextSizeAdjustTests`](../tests/FactsPDF.Tests/TextSizeAdjustTests.cs)) |
| appearance; -webkit-appearance and -moz-appearance aliases | A single CSS UI 4 keyword (`none`, `auto`, `base`, `menulist-button`, `textfield` or a compat value such as `button`, `checkbox`, `menulist`) and CSS-wide keywords, on any element. It only switches native form-control styling; the renderer draws no native widgets, so every element already paints as `appearance:none`, and the declaration is validated and dropped (PDF bytes equal the undeclared page). Other values fail `FPDF1202`; other prefixes fail `FPDF1201` ([`AppearanceTests`](../tests/FactsPDF.Tests/AppearanceTests.cs)) |
| backface-visibility; -webkit-backface-visibility alias; touch-action | `visible` or `hidden`; one `touch-action` keyword (`auto`, `none`, `manipulation`, `pan-x`, `pan-y`, `pan-left`, `pan-right`, `pan-up`, `pan-down`, `pinch-zoom`); CSS-wide keywords; on any element. A back face only shows under a 3D transform (`transform` accepts only `none`) and touch gestures never reach a PDF, so the declaration is validated and dropped (PDF bytes equal the undeclared page). Combined `touch-action` values such as `pan-x pan-y` and other values fail `FPDF1202`; other prefixes fail `FPDF1201` ([`BackfaceTouchActionTests`](../tests/FactsPDF.Tests/BackfaceTouchActionTests.cs)) |
| will-change | `auto`, or a comma-separated list of feature names such as `transform, opacity`, `scroll-position` or `contents` (and CSS-wide keywords), on any element. It only tells a browser compositor what is about to animate; a written PDF page never changes, so the declaration is validated and dropped (PDF bytes equal the undeclared page). `none`, `auto` inside a list, CSS-wide keywords inside a list, space-separated or empty entries and non-identifiers fail `FPDF1202` ([`WillChangeTests`](../tests/FactsPDF.Tests/WillChangeTests.cs)) |
| transform-origin (also `-webkit-`) | One or two positions (`left`, `center`, `right`, `top`, `bottom`, a length, a percentage or `0`; a vertical keyword first is followed by a horizontal one) and an optional third length, or a CSS-wide keyword, on any element. The only accepted `transform` is `none`, so no box is ever moved and the origin it would pivot around changes nothing: the declaration is validated and dropped (PDF bytes equal the undeclared page). Two horizontal or two vertical keywords, a length after a vertical keyword, bare non-zero numbers, angles, a percentage or keyword as the third value, four values, commas and functions fail `FPDF1202` ([`TransformOriginTests`](../tests/FactsPDF.Tests/TransformOriginTests.cs)) |
| transition; transition-property, transition-duration, transition-timing-function, transition-delay, transition-behavior; `-webkit-` aliases of the first five | A comma-separated list of transitions (CSS Transitions 1/2): per layer at most one property name (`none` only in a single layer), a duration and a delay in `s`/`ms` (duration non-negative), one easing (`ease`, `linear`, `ease-in`, `ease-out`, `ease-in-out`, `step-start`, `step-end`, `cubic-bezier()` with x in 0–1, `steps(n[, position])`) and `normal`/`allow-discrete`; each longhand takes a list of its own values; CSS-wide keywords alone; on any element. A transition only animates a change of computed value on screen and a written PDF page is never restyled, so the declaration is validated and dropped (PDF bytes equal the undeclared page). Malformed lists, unitless or non-time durations, `linear()` easing, CSS-wide keywords, `revert` and `default` inside a list fail `FPDF1202`. |
| animation; animation-name, animation-duration, animation-timing-function, animation-delay, animation-iteration-count, animation-direction, animation-fill-mode, animation-play-state; their `-webkit-` aliases | A comma-separated list of animations (CSS Animations 1): per layer at most one keyframes name (identifier or string), a duration and a delay in `s`/`ms` (duration non-negative or `auto`), one easing (as for `transition`), one iteration count (`infinite` or a non-negative number), one direction, one fill mode and one play state; each longhand takes a list of its own values; CSS-wide keywords alone; on any element. A written PDF page shows an animation at time zero, so the longhands cascade and an animation that names a `@keyframes` rule shows its first or last keyframe ([Animations at print time](#animations-at-print-time)); with no matching keyframes the PDF bytes equal the undeclared page. Malformed lists, two names in one layer, negative durations or counts, `linear()` easing and CSS-wide keywords, `revert` or `default` inside a list fail `FPDF1202`. |
| scroll-behavior; overscroll-behavior, overscroll-behavior-x, overscroll-behavior-y, overscroll-behavior-inline, overscroll-behavior-block; overflow-anchor; -webkit-overflow-scrolling; scrollbar-width | One keyword (and CSS-wide keywords), on any element: `scroll-behavior` `auto` or `smooth`; each `overscroll-behavior` name `auto`, `contain` or `none` (the shorthand takes one keyword, not an x/y pair); `overflow-anchor` `auto` or `none`; `-webkit-overflow-scrolling` `auto` or `touch`; `scrollbar-width` `auto`, `thin` or `none`. They only steer a scroll container, and a PDF page never scrolls (`overflow` accepts only `visible`), so the declaration is validated and dropped. Other values, including lengths and two-value forms, fail `FPDF1202`; other prefixes still fail `FPDF1201` (`ScrollOnlyPropertyTests.cs`). |
| scroll-snap-type, scroll-snap-align, scroll-snap-stop | On any element: `scroll-snap-type` `none` or an axis (`x`, `y`, `block`, `inline`, `both`) optionally followed by `mandatory` or `proximity`; `scroll-snap-align` one or two of `none`, `start`, `end`, `center`; `scroll-snap-stop` `normal` or `always`; CSS-wide keywords too. Snapping only steers where a scroll container comes to rest, and a PDF page never scrolls, so the declaration is validated and dropped. Other values and orders fail `FPDF1202` (`ScrollOnlyPropertyTests.cs`). |
| scroll-margin (and -top, -right, -bottom, -left, -inline, -block, -inline-start, -inline-end, -block-start, -block-end); scroll-padding (and the same ten longhands) | On any element: `scroll-margin*` one length each (any sign; `px`, `pt`, `pc`, `in`, `cm`, `mm`, `q`, `em`, `rem`, `ex`, `ch`, `vh`, `vw`, `vmin`, `vmax`, or `0`), `scroll-padding*` `auto` or a non-negative length or percentage each; the shorthands take one to four components, the `-inline`/`-block` pairs one or two, every side one; CSS-wide keywords too. They only offset a scroll-into-view target, and a PDF page is never scrolled to an anchor, so the declaration is validated and dropped and the PDF bytes equal the undeclared page. `auto` or percentages for `scroll-margin*`, negative padding, non-zero bare numbers, functions such as `calc()` and too many components fail `FPDF1202` (`ScrollOnlyPropertyTests.cs`). |
| caret-color; accent-color; -webkit-tap-highlight-color; scrollbar-color; scrollbar-gutter | On any element (and CSS-wide keywords): `caret-color` and `accent-color` `auto` or one colour; `-webkit-tap-highlight-color` one colour; `scrollbar-color` `auto` or two colours (thumb, track); `scrollbar-gutter` `auto`, `stable` or `stable both-edges`. A colour is a named colour, `transparent`, `currentColor`, `#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`, or `rgb()`/`rgba()`/`hsl()`/`hsla()` of three or four numbers, percentages or angles; alpha is allowed because nothing is painted. They only style a text caret, native form controls, the WebKit tap flash and scrollbars, which a PDF page does not have, so the declaration is validated and dropped. Other values fail `FPDF1202`; other prefixes fail `FPDF1201` (`UiColourPropertyTests.cs`). |
| color-scheme; forced-color-adjust | On any element (and CSS-wide keywords): `color-scheme` `normal`, or one or more scheme names (`light`, `dark` or custom identifiers) with at most one `only` first or last; `forced-color-adjust` `auto`, `none` or `preserve-parent-color`. They only let a browser pick dark user-agent defaults or opt out of a forced high-contrast palette; a PDF page has no preferred or forced scheme and paints every colour as declared, so the declaration is validated and dropped (PDF bytes equal the undeclared page). `normal`, `only` or CSS-wide keywords inside a list, commas and other values fail `FPDF1202` ([`ColorSchemeHintTests`](../tests/FactsPDF.Tests/ColorSchemeHintTests.cs)) |

These properties also accept `inherit`, `initial`, `unset` and `!important`. Paragraph-only breaks and text styles remain inapplicable on containers/spans, even for losing declarations; report-panel margins are a separate bounded exception. Styling `br` is unsupported; a matching declaration errors, except `orphans`/`widows` (validated and dropped: a `br` has no line box of its own). Head/title metadata remains non-rendering and does not acquire presentation from stylesheet rules. Inline styling of metadata is still rejected.

This engine's bounded `text-indent` does **not** implement negative/hanging indentation, `each-line`, `hanging`, `calc()`, inline spans, direct table-cell paragraph declarations or arbitrary container declarations. Ordinary report containers and the document root may now declare inherited text-indent; a flat fixed-column cell paragraph can inherit it and position real PDF glyphs. It affects first-line wrapping and text placement, not painted box geometry; the scoped CSS cascade and wide-keyword resolution remain in effect.

The original stylesheet increment and later box-layout increments are distinct; current block width/padding/paint scope is documented in [development](development.md), and C1 style selection in [font styles](font-styles.md). Selectors alone do not make new layout features available. Width/height, padding, borders, background fills, flex/grid, tables, images and font-family/@font-face are not added by this increment. Unsupported properties or values fail explicitly even in an unmatched selector. In strict mode (the default) that deliberately differs from browser CSS error recovery; [non-strict mode](#strict-and-non-strict-css) recovers from CSS that is invalid per the specifications as a browser does, but still fails on valid CSS outside the documented subset.

## Current-source inside/outside list marker placement

The supported `ol`, `ul` and `li` report lists accept `list-style-position:outside` (default) and `inside`. For inside, the measured, searchable ordinal or existing vector bullet is placed at the first line's inline start, with the marker width plus half the item's font size reserved before content. The next wrapped line returns to the normal text edge, including when continuation falls on a new PDF page. Outside retains the existing fixed marker gutter and byte-identical output. The list marker's actual font, text transform, inherited text colour and size remain in force.

`list-style` supports one known marker type, one position keyword, or two tokens combining a supported marker type and `inside`/`outside` in either order (for example `list-style:upper-roman inside`). An omitted type resets to `disc`; an omitted position resets to `outside`, even on `ol`. A single `inherit`/`initial`/`unset` resets both longhands. Independent longhand winners participate in ordinary stylesheet/inline cascade and `!important`; both properties inherit through supported list ancestry, while initial/unset for position resolve to outside/parent respectively. Direct `li` overrides are permitted. Numbered lists retain bounded `start`, `value` and `reversed` semantics.

No `list-style-image`, CSS counters, custom marker glyphs, position changes on non-list elements, inline list items, general layout modes, RTL/vertical writing, more than two nested list levels or full CSS Lists conformance. Unsupported combinations and bad values, including in unmatched CSS, reject explicitly with FPDF1201/FPDF1202 rather than silently ignoring terms. The original output file is protected on error.

Evidence: `tests/FactsPDF.Tests/ListMarkerPositionTests.cs`, `examples/report-list-marker-inside.html`, an independently authored explicit-longhand oracle, an outside-control PDF, and `scripts/verify_native_feature_paths.py` (Native AOT/qpdf/Poppler positional witness and negative recovery).

## Current-source bounded list marker types

An `ol`, `ul`, or direct `li` may specify one CSS
`list-style-type` identifier from `disc`, `circle`, `square`,
`decimal`, `lower-alpha`, `upper-alpha`, `lower-roman`,
`upper-roman` and `none`. Defaults remain `ol` decimal and `ul` solid disc,
with no byte change for an unstyled existing list. CSS-wide
`inherit`/`unset` read the computed parent kind; `initial`
resolves to disc. An explicit nested list overrides the enclosing
list's kind by its own default unless it asks for inheritance. List
items inherit the parent kind, and individual item overrides do not
renumber later siblings. The existing specificity, source order,
inline origin and `!important` cascade apply. Even unmatched
or losing declarations must have an accepted value.

The marker is part of actual PDF layout, not just an accepted CSS
token: decimal/alphabetic/Roman glyphs use the item's caller-supplied
TrueType role or built-in Courier with fallback, measured width and
tracking. The marker gutter reserves each list's widest text marker;
the list's body X, line wrapping and page continuation are accordingly
computed. A marker occurs only once alongside its first body line,
including continued items; `disc` remains the original filled vector
circle, `circle` draws a hollow stroked outline without painting over
any underlying background, and `square` paints a solid vector box.
Each uses the same list gutter, item colour, pagination and display-command
budget. The default disc PDF bytes are unchanged. `none` removes the marker and paint command but leaves
the list-item's ordinary content/gutter geometry intact.
Alphabetic labels continue after `z` as `aa`; Roman notation is
subtractive and deliberately supported only up to 3999 items
(`FPDF1202` beyond). Missing requested-font marker glyphs fail
with the existing FPDF1504/1507 paths rather than being substituted.

This is not full CSS Lists: the shorthand and position are limited
to the bounded grammar above; images, counters, duplicate position/type
tokens, `::marker`, arbitrary nesting and non-list targets remain
unsupported and rejected. Invalid values reject FPDF1202,
invalid target elements FPDF1201, and failed output leaves an
existing PDF unchanged. Bounded HTML `ol start`/`li value` numbering
is documented in the current [list contract](lists.md). No new
fonts/platforms are implied. Earlier marker-type source implementation
`e137e523264d1250702656ee1968c5ff2f61e8fe` (initial list types),
`7665f2fbafa34e14f9d792e7e7728d67f268e0e3` (circle/square shapes),
with an [80-item Native AOT report](../examples/report-list-vector-shapes.html);
actual first-use evidence requires final CI/independent code review
on the completed PR head. See [list contract](lists.md),
[74-entry example](../examples/report-list-marker-styles.html),
`tests/FactsPDF.Tests/ListMarkerStyleTests.cs` and
`scripts/verify_lists.py`. Historical private binaries are unchanged.

## Current-source font-relative report text spacing

Controlled report templates can use `text-indent:0.4em`,
`letter-spacing:0.1em` and `word-spacing:0.15rem` alongside existing
fixed print lengths. `em` resolves against the **declaring element's final
computed font-size** after stylesheet cascade, even if a later or
higher-priority font-size declaration wins. `rem` uses the computed
`html` font-size. On `html` itself, spacing `rem` uses its
newly computed font-size; a document without `html` uses
the writer's base font size. Inherited spacing stays fixed in points rather
than recomputing on each child. A child's explicit declaration uses its
own newly computed font.

Actual PDF glyph advances, U+0020 word-gap `TJ` commands,
first-line inset, wrapping, alignment, decoration width and pagination
reflect those lengths. `normal` and zero preserve legacy PDF bytes.
Letter/word spacing remains **0–24pt after computation**;
indentation retains its nonnegative box-length limit and first-word-fit
`FPDF1302`. Invalid values fail before replacing a pre-existing output.
Percentage text-indent continues to use actual paragraph content width;
negative/percent letter/word spacing, `calc()`, hanging indentation,
indent on inline elements/containers/table-cell paragraphs, and browser
text shaping are unsupported. This is a bounded CSS Text increment,
not full relative-unit support for borders, table/image sizing, or
arbitrary browser stylesheets.

The [72-entry native report](../examples/report-relative-text-spacing.html),
[independently authored point oracle](../examples/report-relative-text-spacing-oracle.html),
`FontRelativeTextSpacingTests.cs` and Native AOT stylesheet verifier
exercise actual PDF parity, searchable text, pagination, TrueType glyphs
and target-preserving rejection. Source code, passing CI and independent
review remain distinct from an installable candidate, customer grant
or human PDF approval.

## Current-source soft-wrapped report justification

`text-align:justify` is an opt-in left-to-right report-text layout mode.
On each **automatically word-wrapped** line except the final or an explicit
`<br>` break, the remaining measured content width is divided evenly
across retained, collapsed U+0020 word spaces. Existing `word-spacing`
is included first. Indented first lines use their reduced actual content
width. The PDF emits the additional spacing using the existing Courier/CID
TrueType `TJ` path; actual glyph placement and text decoration widths
follow the expanded advances. The behavior continues across pages.
`left`, `center` and `right` retain their existing behavior and
default output remains byte-identical.

An unwrapped or `white-space:nowrap` line, explicit break, final
paragraph line or a line without retained spaces does not expand.
Explicit `text-align:justify` on inline spans rejects with `FPDF1201`;
inherited container alignment applies to the actual paragraph.
Unsupported `start`, `end`, `justify-all`, RTL and CJK
inter-character justification are not provided. Invalid values fail
`FPDF1202`, and overwide text retains its `FPDF1302` protection
of the existing target. This is not full browser CSS Text conformance.

## Current-source bounded word-spacing

`word-spacing:0.01in` (equal to `0.72pt`) is supported on report
paragraphs, headings and inline spans, including inherited and CSS-wide
computed values. HTML whitespace still collapses to one U+0020 between words;
only that retained space gains a fixed nonnegative extra advance, from zero
through 24pt. Glyph measurement, word wrapping, first-line indentation,
alignment, text-decoration widths and pagination use the same value.

Both Courier and embedded CID TrueType PDFs place word gaps using a
`TJ` displacement after the encoded space. PDF `Tw` alone does not apply
reliably to multibyte CID space codes. `normal` and all accepted zero values
preserve byte-identical prior text drawing commands. The actual printable
service report exercises this CSS through Linux Native AOT.

This is not general browser CSS Text: negative word spacing,
percentages, `calc()`, Unicode word segmentation, text shaping
and arbitrary whitespace preservation remain unsupported. Invalid values
fail with `FPDF1202`, an unbreakable overwide segment with `FPDF1302`;
both preserve the existing output.

## Current-source bounded letter-spacing

`letter-spacing:0.75pt` and positive `px/mm/cm/in` print lengths (up to 24pt)
on supported report paragraphs/headings, container-inherited text and inline
spans increase the PDF character advance. Each text run emits the matching
PDF `Tc` text-state command for both built-in ASCII Courier and the explicitly
supplied CID TrueType fonts. Since `Tc` persists across PDF text objects, a
transition back to zero emits an explicit `0 Tc` while documents with no
tracking preserve the old byte path. The same spacing is counted during word measurement,
normal/nowrap overflow checks, alignment, paragraph pagination and decoration
widths. No external font or browser layout is inferred. CJK fallback glyph
selection and content bytes are unchanged. Ordered list markers that inherit
tracking reserve their actual wider glyph advances in the list gutter, so
numbered markers do not intrude into adjacent list-item text.

`normal`, `0` and `0pt` are zero spacing and preserve the prior byte path.
CSS cascade, `!important`, `inherit`, `initial` and `unset` select the
computed fixed point spacing; nonnegative units remain fixed through children.
Negative tracking, percentages, `calc()`, and computed values above 24pt return
`FPDF1202` without altering the destination. This is fixed-positive tracking
for controlled reports, **not** full CSS Text letter-spacing, complex shaping,
kerning or word-spacing. The real printable report tracks an invoice reference
and the native oracle checks unit equivalence, actual `Tc` paint and recovery.

## Current-source nonwrapping report labels

For controlled report headings, totals and invoice references, `white-space:nowrap`
on an explicit `p` or `h1`–`h6` prevents automatic breaks at spaces and at the
existing CJK opportunities. The property operates in actual glyph-based PDF
line construction, not just CSS parsing. Whitespace is still collapsed as in
the existing normal text mode. An explicit HTML `<br>` still ends a line;
that line can continue onto another page using the ordinary pagination rules.

```html
<p style="white-space:nowrap">Reference: INV-2026-1042</p>
```

A normal wrapped paragraph and a `nowrap` paragraph with identical content
produce identical PDF bytes when the content fits on one line. If any forced-break
segment is wider than its actual containing content width, conversion raises
`FPDF1302` and preserves any existing destination, rather than clipping,
shrinking or silently inserting an automatic break. The same strict behaviour
applies to explicitly sized and nested report blocks. The current scope is
**actual paragraph/heading text only**, including text inheriting a mode
from an ordinary report container, `html` or `body`: explicit declaration on a
`span`, list/table structural element or image is rejected with `FPDF1201`. A paragraph inside a table cell still uses its actual cell width
and will fail when an unbreakable label does not fit. `break-spaces`,
other `word-break` values, Unicode grapheme shaping and non-breaking span fragments remain unsupported; `pre-line`
retains breaks but collapses spaces, `pre-wrap` preserves spaces with soft
wrapping, and `pre` preserves spaces/newlines without soft wrapping. Full CSS
Text conformance is not implemented. Unsupported values are
`FPDF1202` errors, including unmatched stylesheet declarations. Supported
CSS-wide `inherit`, `initial`, `unset`, precedence and `!important`
remain explicit; normal/default output does not change. See
`WhiteSpaceNowrapTests.cs` and the actual Native AOT verification in
`scripts/verify_stylesheets.py`.

This is a current-source feature, not an installable release or a promise to
accept arbitrary browser stylesheet content.

## Current-source background shorthand (colour only)

The current report renderer also accepts the familiar `background` shorthand **only
when it contains one supported colour**. For example,
`<div style="background:#cee8fb">Summary</div>` is byte-for-byte equivalent
to the same element with `background-color:#cee8fb`. It accepts the same
`#rgb` / `#rrggbb`, documented colour names, `transparent` and
`inherit` / `initial` / `unset` keywords, plus a single `none`, which resets the
fill like `transparent` (`background-color: none` still fails). Normal selector precedence,
`!important`, non-inheritance, element applicability and existing background
painting/fragmentation apply identically.

This is **not** general CSS background shorthand support: images, URL loading,
gradients, positioning, repeats, size, multiple layers and function colours
are still rejected with FPDF1202, apart from the rgb()/rgba() colours (opaque or translucent) described below. There is no change to old PDF bytes unless
the caller actually uses the newly accepted shorthand. A malformed or
unsupported declaration still leaves existing output files intact.

## Current-source opaque RGB colours for reports

Supported text, background and solid-border colour declarations now accept
`rgb(35, 68, 107)`, `rgb(35 68 107)` or `rgba(35,68,107,1)`. The
channel values are **integers 0–255**. As in CSS Color 4, `rgba()`/`hsla()`
are aliases of `rgb()`/`hsl()`: the comma form takes an optional fourth alpha
and the space form an optional `/ alpha` (`rgb(35 68 107 / 1)`,
`hsl(210 50% 40% / 100%)`); alpha must be **exactly 1 or 100%**. These opaque values are lowered
to the matching `#rrggbb` colour before normal CSS cascade, box painting and
PDF serialization. For the same computed colour and declarations, PDF bytes
and page-fragment painting match the existing hex path. This applies to
`color`, `background`/`background-color`, `border-color` and its four
longhands, and solid `border` and per-side shorthands.

Alpha other than 1 paints with PDF constant alpha, as described in
[translucent colours](#translucent-colours). Fractional numeric RGB channels,
gradients, colour mixing and nested `calc()` are
rejected with `FPDF1202`. Rejected conversions preserve existing outputs;
no CSS color-scheme, new colour spaces or implicit image loading is added.
See `OpaqueRgbCssTests.cs` and the real Linux Native AOT run of
`examples/report-print-spacing.html` with an independent hex projection.
This is a source-preview feature, not a public release or general CSS colour
conformance statement.

## Translucent colours

Every colour FactsPDF paints accepts alpha: `color`, `background` and
`background-color`, `border-color` and the border shorthands (solid, dashed,
dotted and rounded), `outline-color`, `text-decoration-color` and the list
markers that follow the text colour. The accepted spellings are:

- `rgba()`/`rgb()` and `hsla()`/`hsl()` with a comma alpha or a space-form
  `/ alpha`, the alpha being a number or a percentage
  (`rgba(164, 176, 195, 0.2)`, `rgb(35 68 107 / 50%)`, `hsl(210 50% 40% / .25)`);
- `#rgba` and `#rrggbbaa` (`#23446b80`, `#abc8`);
- `transparent`, now also for `color`, `text-decoration-color` and
  `outline-color`.

As in CSS, alpha is clamped to 0–1. It is then rounded to one byte (n/255) the
way Chrome's renderer rounds paint alpha, so `rgba(35,68,107,0.5)`,
`rgb(35 68 107 / 50%)` and `#23446b80` produce identical PDF bytes.

Each translucent paint is enclosed in `q`/`Q` and selects a constant-alpha
graphics state, `/Ann gs`, where `nn` is the alpha byte in hex. The page's
`/Resources` declare each alpha the page uses once, inline, as
`/ExtGState << /A80 << /CA 0.502 /ca 0.502 >> >>` (`/CA` for strokes, `/ca` for
fills). The colour therefore composites over whatever is already painted
below it (backgrounds, borders, images, text), as in Chrome. Within one box the
background is painted first and the border edges, which never overlap, over it.

- **Alpha 1** (`rgba(…,1)`, `/ 100%`, `#…ff`, or an alpha that rounds to 255
  such as `0.999` or `2`) produces exactly the bytes of the opaque colour, and
  a page without translucent colours has no `/ExtGState` entry, so existing
  PDFs are byte-identical.
- **Alpha 0** (`transparent`, `rgba(…,0)`, `#…00`, or an alpha that rounds to 0)
  paints nothing and adds no graphics state: no background rectangle, border
  stroke, outline, decoration or marker, and no glyphs for text. The text keeps
  its place in layout, and its own `text-decoration-color` still paints. A
  zero-alpha background on inline text is dropped like `background:transparent`.
- A translucent text run is enclosed in `q`/`Q` too, which also restores the
  character spacing (`Tc`); the next run sets its own tracking again.

`opacity` other than 1, blend modes and colour management are not implemented
and still fail `FPDF1202`, as do alpha written as `none` or with a unit
(`rgba(1,2,3,none)`, `rgb(1 2 3 / 1px)`). FactsPDF has no PDF/A or other
output mode that forbids transparency. Page furniture colours remain opaque
`#rrggbb`. See `TranslucentColourTests.cs`.

## Current-source computed report line-height

Business reports can specify `line-height:150%`, `line-height:1.5em`,
`line-height:18pt`, `line-height:24px` or a physical print length
such as `line-height:0.25in`. The renderer also accepts the existing
unitless `1–10` multiplier and `normal`, the default, which takes the first
font's ascent, descent and line gap rounded to whole CSS pixels as Chrome does
(1.2 for the built-in font, which has no metrics to read).

A percent or em line height is calculated **on the element where the winning
declaration is applied**, after that element's computed font-size. The result,
like a fixed absolute length, is inherited as a fixed point height by nested
spans and blocks. In contrast, a unitless multiplier is inherited as a number
and re-scales when a child changes font size. For example:

```css
body { font-size: 16pt; line-height: 150%; } /* 24pt inherited as fixed */
p { font-size: 12pt; }                       /* still 24pt */
span { font-size: 24pt; }                   /* still 24pt */
/* With line-height:1.5 instead, p gets 18pt and span gets 36pt. */
```

The resolved points feed the actual glyph line-box strut, inline metrics and
page continuation; they are not merely parsed. The runnable ASCII print report
and the real Linux Native AOT verifier compare three independent relative and
physical declarations with their handwritten absolute-point PDF projections,
check PDF structure/text and preserve existing destination bytes on invalid
zero height. Tests also cover computed inheritance, `inherit/initial/unset`,
precedence, fonts and legacy unitless/default byte stability.

Percentage/em multipliers must be positive and no greater than 10; absolute
computed line height is bounded to 0.1–1440pt. Negative/zero values,
`lh`, `rlh`, `calc()`, arbitrary CSS and browser text shaping
remain unsupported (a `line-height` inside the `font` shorthand follows the same rules) and fail explicitly (`FPDF1202`). The accepted subset
is not full CSS Line Height conformance, and the changed source has not been
published as an installable public release.

## Current-source parent-relative report typography

Controlled report headings, paragraphs and inline spans can use a positive
`font-size:125%` or `font-size:1.125em`. The scaling factor is applied to the
**computed font size of that element's actual parent** after the winning
declaration is chosen in the existing cascade. It is not multiplied by the
renderer heading's optional default size multiplier, nor by a losing
declaration. The calculated size must be between 1pt and 144pt inclusive.
Both inline and embedded stylesheet declarations work with the documented
selector/importance/inheritance semantics; `inherit`, `initial` and `unset`
retain their previous meanings.

For example, when `body { font-size:16pt }`, `h1 { font-size:125% }`
becomes 20pt, and `p { font-size:1.5em }` becomes 24pt. A nested inline
`span { font-size:125% }` inside that paragraph becomes 30pt. Actual glyph
width, wrapping, line boxes, page continuation and embedded font selection
use the resolved absolute point size. The runnable print report exercises
percent and em values against an explicitly authored point-value projection
in real Linux Native AOT PDF generation.

This does **not** implement `ex`, `ch`, `lh`, `rlh`, `calc()`, font-size
keywords or unrestricted relative layout units; separate bounded source slices below permit `rem` and element-relative `em` for ordinary report box sizing/padding and paragraph/heading margins, not borders or general browser layout.
Non-positive, malformed or out-of-range computed values are rejected with
`FPDF1202`, before replacing an existing output. Unmatched stylesheets are
still syntax-checked. This is source-preview capability, not public delivery.

## Current-source root-relative report typography

Controlled reports may now author `font-size:1.2rem` and
`line-height:1.5rem` on supported text elements, including paragraphs,
headings and inline spans. The base is the **computed font-size of the
actual `html` element**, not the enclosing `body` or the immediate parent.
If `html` is omitted, the base is the writer's configured base font size
(default 12pt). An `html` element using `font-size:2rem` resolves that
self-reference against the configured initial size; `line-height:1.5rem`
on the same root uses its newly computed font size. For example:

```css
html { font-size: 15pt; }
body { font-size: 11pt; }
h1 { font-size: 1.6rem; line-height: 2rem; } /* 24pt font, 30pt leading */
p { font-size: 1rem; line-height: 1.5rem; }  /* 15pt font, 22.5pt leading */
p span { font-size: 1.2rem; }                /* 18pt font, 22.5pt inherited leading */
```

Winning declarations obey the existing cascade, `!important`,
`inherit`/`initial`/`unset` and explicit stylesheet/inline semantics.
The size is resolved to 1–144pt; line height is computed to 0.1–1440pt,
then inherited as **fixed points**. Unmatched rules are still
syntax-validated without assuming an arbitrary root size. Out-of-range,
zero or unsupported values fail with `FPDF1202` and preserve an existing
destination; a layout that cannot fit keeps the original `FPDF1302`
guards. Defaults and existing absolute/percent/em values retain their
byte-stable paths.

The [63-record paginated business report](../examples/report-root-relative-typography.html)
and independently authored [fixed-point PDF oracle](../examples/report-root-relative-typography-oracle.html)
exercise actual line wrapping, pagination, text extraction and native
PDF byte equivalence, with managed PDF and explicit TrueType regressions.
This typography scope itself does not add general CSS Values conformance. A
separate bounded source implementation now permits `rem` for ordinary report
box `width`, `min-width`, `max-width` and padding (see below), while a distinct
scoped implementation supports paragraph/heading `rem` margins. General container vertical
margins, borders, `letter-spacing`, `word-spacing`, table/image/column dimensions,
`ex/ch/lh/rlh` and `calc()` remain unsupported.
Real current-head CI, independent code review, installable packaging
and human page approval are separate gates.

## Current-source root-relative report box lengths

Controlled report blocks, paragraphs and headings can now use
`width:13rem`, `min-width:10rem`, `max-width:14rem`,
`padding:0.5rem` and corresponding padding longhands. Each length resolves
to **the computed `html` font size** (or the writer's base font size when
`html` is absent), including a root self-reference resolved from the initial
font size. It is a fixed point length after cascade, not a percentage of the
parent, and does not change with the immediate parent's font size.
An `html` element cannot itself be a styled box; the root font only
establishes the scale.

```css
html { font-size:15pt; }
.report { box-sizing:border-box; width:13rem; max-width:14rem;
          padding:0.5rem; border:1pt solid #23446b; }
.reference { min-width:10rem; max-width:11rem; margin:0 auto; }
```

This affects real PDF content widths, line wraps, auto centring, background
and border painting, and page-fragment continuation. Existing percentage
width/padding and box-sizing/min-max semantics retain the **containing block
content width** as their own basis. CSS cascade, `!important`,
`inherit/initial/unset` and non-inheritance continue to apply independently
per longhand. The supported nonnegative resolved box length range remains
0–14400pt; invalid/unmatched values raise `FPDF1202` and unfit boxes
raise `FPDF1302`, leaving an existing output unchanged. Unsupported rem on
`img`, `table`, `col`, table structure/cells or their child paragraphs
raises `FPDF1201`, even if a different cascade declaration would win.

The original root box-length slice does not itself change margins, but a
separately scoped implementation below permits `rem` in paragraph/heading
margins. General container/table margins, borders, text spacing, image/table/column
lengths, `calc()`, negative values, full CSS Values/Sizing and browser layout
remain unsupported. A later bounded `em` source slice below covers actual
ordinary report box widths/padding and paragraph/heading margins. Legacy templates that do not opt in retain
their existing PDF bytes. The real
[69-record printable report](../examples/report-rem-box-lengths.html) and
[independently authored points-only projection](../examples/report-rem-box-lengths-oracle.html)
exercise multi-page boxes, searchable body and exact PDF equivalence through
`RootRelativeBoxLengthTests.cs` and the Linux Native AOT stylesheet gate.
This is unshipped source functionality; exact-head CI, independent review,
human PDF approval and packaging are separate requirements.

## Current-source print-length and vertical-margin authoring

Controlled invoices, maintenance reports and service records can use physical
print lengths in existing length-bearing CSS properties: `mm`, `cm`, `in`,
`pt` and `px`. Conversion is fixed at 72pt/in, 25.4mm/in and 96px/in.
`font-size` (1–144pt after conversion), `margin-top`/`margin-bottom`
(0–14400pt), `width`, `padding` and solid `border-width` (including
their existing shorthands) accept these units. `width`/`padding` retain
their existing percentage semantics; the other lengths do not gain percentage
support; `font-size` separately supports parent-relative percentages as scoped
above. All existing finite/bounds/containing-block and PDF size guards apply.

The paragraph/heading `margin` shorthand now expands into **all four** independently
cascaded sides. `margin-left` and `margin-right` also work as longhands and accept
nonnegative `pt/px/mm/cm/in/%` and unitless zero. Percentage sides resolve
against the original containing content-box width, including when the
paragraph has padding or an explicit width. Nonzero horizontal margins shift
the paragraph border/background and reduce line-wrap and alignment width
on every continued page. Opt-in heading keep also measures the inset width.

For example, `margin:2mm 5% 3mm 4mm`, `margin:5mm`, `margin:0 4mm`, and
`margin-left:5%` and `margin:0 auto` with an explicit `width` affect real PDF geometry. CSS-wide `inherit`, `initial` and
`unset` remain per-side; horizontal margins are not inherited by default.
On flat table-cell paragraphs, only **explicit zero** horizontal margin values
are allowed (preserving old vertical shorthand); `auto` or nonzero horizontal values error
with `FPDF1201`, not silently ignored. Span margins remain
unsupported; vertical margins on panels, lists and tables are described in
[block margin collapsing](#current-source-block-margin-collapsing-and-list-and-table-margins) and the bounded horizontal report-panel exception is below. Scoped paragraph/heading root `rem` is described below.
Negative margins, vertical `auto`/`%` and `calc()` are not implemented. A bounded `em` source slice below supports only paragraphs/headings. Geometry exceeding
the containing content rectangle fails with `FPDF1302` and preserves output.
Four-sided shorthand now counts four emitted declarations toward the existing
CSS budget, rather than the earlier two when both horizontal sides were zero.
For a ready-to-run example needing no external images or fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- \
  examples/report-print-spacing.html report-print-spacing.pdf
```

This is a renderer/source capability and not a public package release,
automatic print/page sizing or support for
`calc()`, negative or vertical `auto` margins.
The separate root-`rem` and element-`em` margin features cover only
paragraphs/headings outside tables.
Existing reports without these explicit new values retain their old rendering
path. Results must still respect the chosen page geometry and explicit
resource contracts.

## Current-source centred report panels

Controlled business-report `div`, `section` and `article` boxes can use
**horizontal** `margin-left`, `margin-right` and `margin:0 auto` on
real PDF layout. The 1–4-value shorthand expands independently cascaded
sides. The top/bottom values on these container kinds also accept nonnegative
physical lengths and scoped root-`rem`/element-`em` as **real spacing
outside painted borders**. This deliberately does not implement negative
margins, vertical percent/auto, `calc()`, `margin-block`, CSS margin
collapse or arbitrary browser flow. Matching invalid values still reject,
even if a stronger declaration would win.

With an explicit `width` or constrained `max-width`,
`margin:0 auto` divides the free space **outside the painted border
box** equally. Horizontal lengths accept bounded nonnegative
`pt`/`px`/`mm`/`cm`/`in`/`%` and scoped root-`rem` or
element-final-font-`em`; only horizontal sides accept `auto`.
Percentages resolve against the containing content width, before
margin subtraction. The existing `box-sizing:border-box`,
`min-width`/`max-width`, padding, borders, nested wrapping,
background and multi-page fragment layout all use this resolved X/width.

```css
.report { box-sizing:border-box; max-width:440pt; margin:0 auto;
          padding:8pt; border:1pt solid #23446b; background:#f0f6fa; }
.report article { width:300pt; margin-left:auto; margin-right:auto; }
```

Horizontal and vertical `inherit`/`initial`/`unset`, inline declarations,
selectors, `!important` and per-side cascade remain supported.
This exception does **not** extend to `body`, `html`, list
containers/items, table structure, images, inline spans or flat
table-cell paragraphs, which retain strict applicability. Negative
or calculated lengths (including vertical percent/auto) raise `FPDF1202`;
unsupported elements raise `FPDF1201`; an outer box too wide for its parent
raises `FPDF1302`, preserving an existing output destination.
`margin:0` on a panel produces byte-identical legacy PDF output.

The [50-entry paginated panel report](../examples/report-panel-margins.html)
and [independently authored fixed-point oracle](../examples/report-panel-margins-oracle.html)
use identical content but `margin:0 auto` versus explicit point insets.
Managed regressions exercise byte-equivalent PDF/box X/width, root-`rem`
and final-font-`em`, percentage sides, border-box, CSS cascade,
continued fragments and failure recovery. The Linux Native AOT gate
checks qpdf, PDF-byte equality, searchable first/last entries, page
progression and original-output protection. This is development-source
capability, not an updated private preview, approved reference page,
general browser CSS support, licensing grant or customer release.

## Current-source element-relative report box and margin lengths

A controlled business report can now use nonnegative `em` lengths for
`width`, `min-width`, `max-width` and all four `padding` sides on
ordinary blocks, paragraphs and headings. Paragraphs/headings outside table
cells additionally accept `em` in `margin-top/right/bottom/left` and
the 1–4 value `margin` shorthand, including horizontal `auto`.

Unlike `rem` (the computed `html` root), each `em` is computed from
**that element's own final computed font-size**, after selector specificity,
inline declarations and `!important` resolution. It is converted to bounded
0–14400pt before PDF layout. The same resolved point lengths feed content
width, soft wraps, horizontal placement/auto-centring, painted backgrounds
and borders, vertical gaps, heading keeps and page fragments.

```css
html { font-size: 15pt; }
.report { font-size: 18pt; box-sizing: border-box;
          width: 12em; padding: .5em; } /* 216pt width; 9pt padding */
.report p { font-size: 10pt; margin: .2em .5em .3em;
            padding: .1em; }             /* 2pt/5pt/3pt margins; 1pt padding */
```

Existing `rem`, percentage, `inherit`/`initial`/`unset`, scoped
`auto`, default values and zero-value byte stability remain distinct.
No new units apply to image/table/column/inline box sizing, table-cell
paragraph margins, borders, `text-indent`, `letter-spacing`,
`word-spacing`, or general container vertical margins. `calc()`, negative,
unbounded and invalid lengths fail with `FPDF1201`/`FPDF1202`; content
too wide for its resolved geometry fails with `FPDF1302`, leaving an
existing PDF destination unchanged. This is not full CSS Values/Sizing
or full browser layout.

The source-owned [32-entry service report](../examples/report-em-layout.html)
and its [independently authored point-value oracle](../examples/report-em-layout-oracle.html)
exercise actual paginated output. `ElementRelativeBoxLengthTests.cs` and
the Linux Native AOT stylesheet verifier check PDF-byte/geometry equivalence,
text and error recovery at the specific source commit; they are not a
customer-authorised binary release or human PDF approval.

## Current-source paragraph percentage vertical margins

Ordinary `p`, `pre` and `h1`–`h6` outside lists and tables accept
nonnegative vertical `margin-top`/`margin-bottom` percentages directly,
through a 1–4 side `margin` shorthand or LTR `margin-block`
(start/end). The percentage basis is the **containing block content
width**: a 180pt-wide report panel resolves `margin-top:10%` to 18pt,
not to a percentage of page height or the paragraph's own width.
Explicit `inherit` from a percent-margined report panel uses the child's
containing width; `!important`, `initial`, `unset` and per-side
cascade retain their normal behavior. The resolved values affect actual PDF
placement, backgrounds/borders, multi-page text, heading keep and
kept boxes.

Direct percentages in list/table paragraphs fail `FPDF1201`;
negative lengths, vertical `auto` and `calc()` fail `FPDF1202`;
unplaceable percentage outside margins fail `FPDF1302`. Previous
PDF bytes and stream position are protected. No RTL or general Paged
Media support. Actual HTML-to-PDF and recovery
tests: `tests/FactsPDF.Tests/ParagraphPercentageVerticalMarginTests.cs`,
source implementation `0f6c58655d651ee934cfb2c95272dde54f4c477e`. This is candidate source,
not proof of exact-head CI/review/main merge or a published binary.

## Current-source root-relative paragraph margins

Controlled report paragraphs and headings (`p`, `h1`–`h6`) outside tables now
accept `margin:0.25rem 0.5rem`, `margin-top:0.4rem` and one to four
top/right/bottom/left values, with existing horizontal `auto` and `%`
rules. Each `rem` is resolved against the actual computed `html` font
size (or the writer's base font size if `html` is absent).
An `html` root using `font-size:2rem` refers to the configured initial
font size for that self-reference. The immediate paragraph/heading or body
font size does not change the margin's root-relative length.

```css
html { font-size: 15pt; }
.report-title { margin: 0.25rem 0.5rem; } /* 3.75pt 7.5pt */
.summary { width: 9rem; margin: 0.5rem auto 0.25rem; }
```

Top/bottom margins become bounded points before PDF paragraph layout, with
real vertical gaps and page progression. Left/right margins move painted
boxes and text, reduce wrap width, combine with `auto` centring and remain
stable on continued pages. The normal cascade, `!important`, CSS-wide
keywords, and non-inheritance apply per side. Computed margins are bounded
to nonnegative 0–14400pt. Invalid/negative values, vertical
`auto` and `calc()` reject with `FPDF1202`; scoped paragraph percentages
are resolved by the source contract below. Oversized geometry
rejects with `FPDF1302`. General container/inline margins and any `rem`/`em`
margin inside table-cell paragraphs reject with `FPDF1201` even when
another declaration could win. Vertical margins collapse as described in
[block margin collapsing](#current-source-block-margin-collapsing-and-list-and-table-margins).

The [32-entry printable report](../examples/report-rem-margins.html) and
[independently authored point-value oracle](../examples/report-rem-margins-oracle.html)
exercise actual multi-page layout and searchable text; the
`RootRelativeMarginTests.cs` and Linux Native AOT stylesheet verification
compare rendered PDFs and protected-output failures. This source feature
is not a new installable candidate, licence grant or human PDF approval.

## Current-source underline and strike-through for reports

Report templates can use `text-decoration:underline`, `text-decoration:line-through`
and the `text-decoration-line` alias on supported text and inline spans.
The renderer paints actual solid PDF lines with each font-resolved glyph-run
width and current text colour, including wrapped lines and page fragments.
Unstyled and explicit `none` output still use the original PDF rendering.
These values participate in the normal cascade, `!important` and
`inherit`/`initial`/`unset` handling.

This is **bounded text decoration**, not complete browser CSS. Only one
decoration per computed run is available. Line offsets/thickness are estimated
from font size, not complex font metrics. No `overline`, wavy/dotted/dashed
line, compound decorations, decoration-colour/style/thickness options,
`<u>`/`<s>` HTML tags, browser subtree-decoration propagation or RTL shaping.
Unsupported values/attributes error even in unmatched rules. Paint commands
count against existing budgets; failing conversions preserve original files.
Actual feature evidence is in `TextDecorationTests.cs` and the print-spacing
example; there is no public package or automatic visual approval.


## Current-source border-box report panel widths

Ordinary blocks (`body`, `div`, `section`, `article`, `ol`, `ul`, `li`,
paragraphs and headings) accept `box-sizing:border-box`. With a specified
`width`, the declared width now includes the resolved padding and solid
borders; content width is reduced *before* text wrapping, auto-margin centring,
heading keeps, painting and continued-page fragmentation. Percent width and
percent padding still resolve against the original parent content width.
`width:auto` preserves the existing fill-available-width behaviour.

`box-sizing:content-box` is the non-inherited default and preserves existing
PDF bytes. `<html>` and inline text (`span`, `b`, `strong`, `i`, `em`,
`small`, `u`, `s`, `del`, `ins`, `a`, `code`, `kbd`, `samp`, `var`, `cite`,
`dfn`, `time`, `data`, `abbr`, `q`, `bdi`) also accept `box-sizing`, where it
changes nothing because no width or height applies there; on `<br>` it is
validated and dropped (any other declaration on `<br>` still fails `FPDF1103`), so the universal
resets `*{box-sizing:border-box}` and `html{box-sizing:border-box}
*{box-sizing:inherit}` reach every block (a child's `inherit` reads the root or
inline value, as in a browser). On a `<table>`, `box-sizing:border-box`
makes an explicit `width` include the table's padding and borders (column
percentages resolve against the remaining content width). Columns, column
groups, row groups, rows, cells and cell paragraphs accept it with no effect,
because none of them takes a width together with padding or borders there, so
universal resets also pass on pages with tables (`BorderBoxSizingTests`). The normal cascade, `!important`, and explicit `inherit`,
`initial` and `unset` work independently of width and padding. The legacy
`-webkit-box-sizing` and `-moz-box-sizing` spellings (which Bootstrap 3 and
older resets write before `box-sizing`) are aliases: they share the
`box-sizing` cascade slot (the later declaration wins), values, applicability
and errors (`BoxSizingPrefixAliasTests`). The same holds for the other legacy
engine spellings older themes still write: `-webkit-`/`-moz-border-radius`,
`-webkit-`/`-moz-box-shadow`, `-webkit-`/`-moz-`/`-ms-`/`-o-transform`,
`-webkit-filter`, `-webkit-clip-path` and `-webkit-backdrop-filter` behave
exactly like the standard name, so `-webkit-transform:none` is dropped while
`-webkit-box-shadow:0 1px 2px #000` fails `FPDF1202` (`LegacyPrefixAliasTests`);
`-webkit-text-decoration` and `-webkit-text-decoration-line`/`-color`/`-style`
are aliases of the text-decoration shorthand and longhands in the same way. For example:

    <section style="box-sizing:border-box;width:140mm;padding:5mm;border:0.25mm solid #23446b">
      <p>Service summary</p>
    </section>

This implementation does not extend the separate image/table sizing paths:
`img`, `table`, structural table elements, table-cell paragraphs, inline
spans and `html` reject the property with `FPDF1201`. Invalid `padding-box`,
`calc()` and multi-token values are `FPDF1202`, including in unmatched rules.
A border-box that cannot contain its own padding/borders or fit its containing
width fails with `FPDF1302` and leaves existing output intact. No general CSS
height, flex/grid, column sizing or full box-model conformance.
`BorderBoxSizingTests.cs` and the real Native AOT stylesheet gate compare actual
PDF geometry with an independently authored content-box projection; the
resource-free `examples/report-print-spacing.html` exercises the feature.

## Current-source bounded report min/max widths

Ordinary block boxes, paragraphs and headings can now use `min-width` and
`max-width` for real PDF layout. `min-width:auto` (the unchanged default)
and `max-width:none` (the unchanged default) impose no extra constraint;
bounded nonnegative `pt`, `px`, `mm`, `cm`, `in`, `%`, scoped root-relative
`rem`, scoped own-font `em` and unitless zero are accepted. Percentages resolve against the **containing block content
width**, including inside nested sections, not a post-margin box. Bounds are
resolved after `width` (including `auto`) and before auto margins are
distributed; max-width can therefore centre a content panel without requiring
a fixed width. `min-width` takes precedence if it exceeds `max-width`.

For `box-sizing:content-box`, min/max constrain the *content* width; for
`box-sizing:border-box`, they constrain the outer width including padding
and solid borders. The adjusted content width drives real text wrapping,
backgrounds, border geometry, heading keeps and page fragments, and does not
change unstyled or unconstrained output bytes. For example:

```html
<section style="max-width:146mm;padding:4mm">
  <p style="box-sizing:border-box;min-width:82mm;max-width:90mm;width:70mm">
    Reference: SR-2026-0142
  </p>
  <p style="max-width:95mm;margin:0 auto">Approved service summary</p>
</section>
```

The scope does **not** add intrinsic `min-content`/`max-content`/`fit-content`,
height constraints, negative lengths, `calc()`, text-overflow,
CSS flex/grid, image sizing, table and cell width constraints, or inline span
boxes. Unsupported values reject with `FPDF1202`, unsupported elements with
`FPDF1201`, and boxes that cannot fit their content area with `FPDF1302`;
conversion preserves any prior destination. The existing independent
stylesheet cascade, `!important`, `inherit`/`initial`/`unset`, and box
fragments apply. Scope is **source-only**, not a published engine release.

The resource-free [actual report](../examples/report-width-constraints.html),
[hand-authored fixed-width oracle](../examples/report-width-constraints-oracle.html),
`MinMaxWidthTests.cs` and Native AOT stylesheet check compare PDF bytes,
searchable content, layout positions, real multi-page painting and invalid
output recovery. Engine implementation starts at
`bc704aed90b1e5dbdc1deec65a7fd314b626467e`; introduction in a
distributed version remains unknown.

## Current-source ASCII report text-transform

Business invoice labels, report headings and status fields can opt into
`text-transform:uppercase`, `text-transform:lowercase` or
`text-transform:capitalize`; `none` is the unchanged inherited default. These declarations work on styled headings,
paragraphs, inline spans and text-bearing containers with normal selector
cascade, `!important`, `inherit`, `initial` and `unset` semantics.
A nested `span style="text-transform:none"` can preserve a customer's
proper name inside an uppercase label.

The conversion is deliberately **ASCII Latin letters A–Z/a–z only**.
`capitalize` uppercases only the first lowercase ASCII letter of a word;
the rest of that token retains its original case. A word here is a maximal
run of ASCII letters/digits. Hyphens, punctuation, whitespace and non-ASCII
scalars separate words, while leading digits belong to the word. Inline
spans do not create word boundaries, including spans with `none`; `<br>`
starts a new word. Existing alphabetic/Roman list markers also measure and
paint their first ASCII letter consistently under inherited `capitalize`. This is a deterministic report-token subset, **not**
CSS Text's Unicode word segmentation. Caseless Chinese, numbers and
punctuation remain unchanged.
The operation occurs before actual font/glyph fallback and width measurement:
searchable PDF glyph text, line wrapping, page positions, decoration widths
and pagination are based on the *transformed* characters. It does not rely
on postprocessing a rendered PDF or synthetic font faces. `none` and
unstyled content retain byte-identical PDF output.

Locale-specific casing, Unicode multi-character expansions, non-ASCII
cased letters, `full-width`, `full-size-kana`, language-specific
casing and general Unicode text transforms are **not** supported.
Non-ASCII cased letters and symbols with Unicode case mappings (including Roman numerals and circled Latin letters) under an active transformation explicitly raise
`FPDF1305` rather than silently producing incorrectly cased output;
unsupported CSS values raise `FPDF1202`, and applying this text property
to a standalone image raises `FPDF1201`. Normal missing-result-glyph
diagnostics (`FPDF1504`) still apply. All failures preserve an existing
destination. No CSS Text standard-compliance claim or publicly distributed
new engine binary is implied.

Run the fontless [case labels report](../examples/report-case-labels.html)
and [54-entry capitalized service register](../examples/report-capitalized-register.html)
from source; the [independent literal-case register oracle](../examples/report-capitalized-register-oracle.html)
and separately authored
[visible-text oracle](../examples/report-case-labels-oracle.html), NUnit
`TextTransformTests.cs` and the Linux Native AOT stylesheet verifier compare
actual PDF bytes, searchable text, mixed spans and negative recovery. The
original uppercase/lowercase support began at `8c946a29423ee3de6c171548fe6f633393ca76b6`;
ASCII capitalize extends it in source commit `06d0be5aab6fcdf15e20f8f1e6b7432e27a4ed35`.
Neither establishes a published version or new installable candidate.

## Budgets and diagnostics

`WithLimits(l => …)` sets per-conversion limits:

| Limit | Default |
| --- | ---: |
| MaxCssCharacters | 262144 across embedded and decoded inline CSS, including comments |
| MaxCssSelectors | 4096 selector arms, including comma groups and empty blocks |
| MaxCssDeclarations | 32768 across embedded and inline declarations |
| MaxCssMatchOperations | 5000000 charged candidates, matching steps, offered declarations, components placed by `var()` substitution and ancestor custom property layers read by a `var()` lookup |
| MaxCssCustomProperties | 4096 distinct custom property names (`--name`) across embedded and inline CSS |

Each selector is limited to 32 compounds with at most 32 simple terms per compound. Rightmost ID/class/type indices reduce unrelated candidates; mixed combinators use bounded dynamic programming over ancestry, not exponential backtracking. Parsing uses two HTML scans rather than a retained full DOM: lower retained tree scope is traded against a second scan. This is an architectural choice, not a benchmark result or a constant-memory guarantee.

Cancellation is cooperative and checked during source scanning, compilation and matching. CSS failures happen before PDF serialization, leaving the caller's output untouched. Existing final-copy I/O limitations still apply. Input limits and font trust requirements remain unchanged.

| Code | Meaning |
| --- | --- |
| FPDF1201 | Unsupported CSS property or unsupported element applicability |
| FPDF1202 | Invalid/unsupported value or declaration |
| FPDF1203 | Unsupported/malformed selector, token or stylesheet structure |
| FPDF1204 | Unsupported at-rule, style type or media |
| FPDF1205 | CSS resource/complexity budget exceeded |

Embedded stylesheet token offsets refer to the HTML source. Inline diagnostics are anchored to the element with decoded-value offsets; they are not an exact reverse mapping through HTML entities. Diagnostics remain development contracts, not complete browser parse-error classification.

## Verification

The NUnit suite checks selector matching, specificity, inline/important order, globals, inheritance, comments, late styles, HTML ancestry, RAWTEXT boundaries, budgets, cancellation and concurrent isolation. The two old tests rejecting all stylesheets and important declarations are replaced by positive PDF assertions, not silently removed without coverage.

The Native AOT fixture compares `examples/stylesheet-document.html` against an independently hand-authored `examples/stylesheet-inline-oracle.html`. Both use the same native FactsPDF executable and complete explicit fonts with native subsetting. Verification requires identical PDF bytes, identical extracted text matching visible HTML, page-contained text, and identical 120 DPI page pixels. qpdf and Poppler inspect outputs; no browser or third-party CSS renderer generates an expected PDF. This validates the authored cases, not every aspect of CSS conformance.

The artifact contains only the public two-page PDF, two PNG previews and a verification JSON record. No standalone font, executable or package is uploaded. Exact-head results belong in [the CSS ledger](css-development-ledger.md) and PR checks. Independent code review, broader standards-based test corpora, fuzzing and performance profiling remain necessary before production.

## Current-source bounded emergency wrapping for report identifiers

`overflow-wrap:break-word` is opt-in for explicit `p` and `h1`–`h6`
paragraphs/headings. It adds breaks at glyph boundaries **only** when a whole
otherwise-unbreakable token cannot fit the measured content width (or the
indented first line). A word that fits a fresh line instead moves whole after
its preceding collapsed space. Spaces still collapse, explicit `<br>` works
and `white-space:nowrap` continues to prohibit automatic breaking.
`overflow-wrap:normal` or omitted retains identical PDF drawing bytes.

Only `normal`, `break-word`, `anywhere` and single CSS-wide
`inherit`/`initial`/`unset` values are accepted. Per CSS Text 3, `anywhere`
breaks exactly like `break-word` and differs only in min-content sizing;
FactsPDF has no min-content sizing (table columns and boxes take explicit or
containing-block widths), so `anywhere` wraps byte-for-byte like `break-word`
in every context that accepts it (`OverflowWrapAnywhereTests.cs`). The legacy `word-wrap` name is
an alias: it shares the `overflow-wrap` cascade slot (the later declaration
wins), values and applicability. Explicit declarations on
inline spans, unsupported container and table-element targets (ordinary report containers outside the table/list structure are now supported for inheritance),
`break-all`, hyphen insertion, grapheme/complex-script shaping
and bidirectional text remain outside this scoped increment. A flat `p` inside
a fixed-column table cell may opt in for its own text, but this does not add
auto column widths or general table layout.
Unsupported declarations fail `FPDF1201/1202`; a glyph wider than its
usable line or an impossible first-line indent fails `FPDF1302` without
replacing any existing output. Breaks are scalar-glyph boundaries in the
already-supported simple horizontal text model, not full CSS Text support.

The [32-entry real report](../examples/report-overflow-wrap.html) has an
[independently authored manual-break oracle](../examples/report-overflow-wrap-oracle.html).
`OverflowWrapTests.cs` and the Native AOT stylesheet verifier test PDF bytes,
actual glyph positions, pagination, searchable content and error recovery.
Independent final-head review, audit-data update, candidate binaries and
human PDF approval remain separate requirements.


## Current-source real vertical report-panel margins

The bounded report-panel subset (`div`, `section`, `article`,
`blockquote` and standalone `hr`) supports nonnegative
`margin-top`/`margin-bottom` values in `pt/px/mm/cm/in`, final-font
`em`, computed-root `rem`, exact zero and **percentages**, such as
`margin-top:5%`. Vertical percentages use the **containing block's content
width**, not its height or the page height. The original percentage survives
CSS cascade/inheritance and resolves when the PDF layout knows that width.
The normal 1–4-value `margin`, bounded LTR `margin-block` 1–2-value
shorthand and physical/logical longhands retain their own cascade,
`!important` and CSS-wide keyword behavior.

These are actual outside-the-border PDF gaps: the first top margin advances
the painted background/border and its text, and the bottom gap follows the
last painted fragment. Nested percentages use each child's containing
width; continued page fragments do not repeat margin. Sibling vertical
margins collapse to the larger one. Zero/default inputs preserve their prior
byte output. Another panel can explicitly inherit the percentage and
resolve it in its own containing block.

**Limits:** A `p` or heading keeps its stricter nonnegative physical/rem/em
vertical margin contract. Vertical percentages on paragraph/heading
declarations fail `FPDF1202`; directly inheriting a percentage panel
margin into a paragraph/heading fails `FPDF1201` rather than accidentally
treating 10% as 10pt. Vertical `auto`, negative values, `calc()`,
out-of-range or underflow lengths, nonzero body/html margins, image/inline margins,
RTL/vertical writing, percentage margins on or inside lists and tables
and Paged Media are not
implemented. Geometry that cannot fit the PDF page fails `FPDF1302` and
preserves an existing PDF destination.

`<html>` and `<body>` have no margin of their own here (the page margin
options frame the content) and `<html>` has no padding, so a zero margin on
either (any zero length, `inherit`, `initial` or `unset`, including the
logical longhands) and a zero padding on `<html>` are validated and dropped:
`body{margin:0}` and `html,body{margin:0;padding:0}` render byte-identical to
the page without them. Any other value, such as `body{margin:8px}` or
`body{margin:0 auto}`, still fails `FPDF1201` (`RootZeroInsetTests`).

Inline text (`span`, `b`, `strong`, `i`, `em`, `small`, `u`, `s`, `del`, `ins`,
`a`, `code`, `kbd`, `samp`, `var`, `cite`, `dfn`, `time`, `data`, `abbr`, `q`, `bdi`) lays out no box of its own, so a zero margin or padding there (any zero
length, `initial` or `unset`, physical or logical longhands) and a
transparent background (`background-color:transparent`, `background:none`,
`background:transparent`, `initial`, `unset`) are validated and dropped too:
normalize.css's `a{background-color:transparent}` and
`span,a{margin:0;padding:0}` render byte-identical to the page without them.
A nonzero inset, `auto`, a real colour and `inherit` (which could carry a
parent block's inset or fill) still fail `FPDF1201` (`InlineZeroBoxResetTests`).

A zero margin (any zero length, `initial`, `unset`) is also the initial value
on `li` and on `tr`, `thead`, `tbody`, `tfoot`, `col`, `colgroup`, `td` and
`th`, so `*{margin:0}` reaches list and table pages without failing there.
Nonzero margins on those elements still fail `FPDF1201` (`TablePartMarginTests`).

`ul`, `ol` and `table` lay out with a zero margin, table rows, row groups,
columns and column groups take no padding, and `br` lays out no box, so a zero
margin there, a zero padding there and a zero margin or padding on `br` are
dropped too: `*{margin:0;padding:0}` now renders list and table pages, changing
only the elements with real insets (headings, paragraphs, list and cell
padding). Nonzero values and `inherit` still fail (`UnusedZeroInsetTests`).

The [50-entry percentage report](../examples/report-percentage-panel-margins.html)
and [independently authored physical-point oracle](../examples/report-percentage-panel-margins-oracle.html)
compare real PDF bytes at outer 26.164pt/15.6984pt and nested
15.12pt/7.56pt gaps, with complete searchable body, page continuations,
qpdf/Poppler and protected-output negative recovery in Linux/Windows Native
AOT. Source `0518b51d7a34457798c748db6753c4270ce20996` adds `PercentagePanelMarginTests.cs` and the
native `verify_stylesheets.py` acceptance block. Exact final-head CI,
independent review, human reference approval, private software and
commercial distribution rights are **separate**.

## Current-source block margin collapsing and list and table margins

Vertical margins on block boxes are laid out the way Chrome prints them. This
covers `p`, headings, `pre`, `div` and the other report panels, `blockquote`,
`ul`, `ol`, `menu`, `li`, `table` and `hr`. FactsPDF rejects negative lengths,
so a collapsed margin is the **largest** of the adjoining margins (CSS 2
section 8.3.1):

- adjacent siblings: a table or list margin meets the paragraph or panel
  margin beside it, so two tables with 20pt margins sit 20pt apart, not 40pt;
- a parent and its first child, when no padding or border separates them, and
  a parent and its last child, when none separates them and the parent's height
  is `auto` (no `height`, no `min-height`);
- an empty block, whose own top and bottom margins collapse through it.

Padding and borders keep margins apart: a parent with `padding-top:5pt` keeps
its own 20pt margin and its child's 10pt margin (20 + 5 + 10pt), and a parent
with bottom padding keeps its last child's margin inside its background. A
margin that collapses out through a parent's edge is not part of the parent's
painted box, so a background starts and ends at the content, as in Chrome.

Page breaks: margins adjoining an **unforced** break are truncated. A block
moved to a new page starts at the page top without its `margin-top` and the
trailing margins of the previous page are dropped; a margin that padding or a
border separates from the break (a child inside a padded panel) is kept. After a
**forced** break (`break-before:page`, `break-after:page`) the `margin-top` of
the next block is kept.

Table cells, flex items and grid items are formatting-context roots: the margins
of their children never collapse with the cell or item edge and count inside its
content height.

`ul`, `ol`, `menu`, `li` and `table` accept `margin-top` and `margin-bottom`
(lengths, `em`/`rem`, the `margin` shorthand and the CSS-wide keywords, as the
panels do), and `margin-left`/`margin-right` on lists and list items; a
percentage vertical margin on them fails `FPDF1201`. A table accepts only zero
horizontal margins (`margin:20pt 0` is the usual template form); a nonzero left or
right margin, including `margin:0 auto`, fails `FPDF1201`. `ul`, `ol` and
`menu` take Chrome's user-agent margin of **1em** above and below (in the list's
own font size) and none when nested inside another list, so their content
moves relative to earlier output; author margins, `revert` and the CSS-wide
keywords apply per side. Paragraph and heading user-agent margins (0 above, 8pt
below) are unchanged.

A table-cell paragraph keeps its own padding on all four sides (lengths, `em`
and `rem`; a percentage, border or background still fails `FPDF1201`). The
padding is added to the cell's content height and narrows the paragraph's
width, counts in automatic column min/max content widths, and never collapses
with the margins around it.

Remaining differences from Chrome: a `li` whose first content is a nested list
still takes a marker-only line (Chrome puts its marker on the first nested line);
the printed 1em of `p` and headings differs (above). A list in a table cell lays
out as a [cell list](tables.md#lists-in-cells): its margins collapse inside the
cell and stay there. Every distance above was measured against Chrome's printed PDF
and is pinned by `BlockMarginCollapseTests`.

## Current-source grid columns

`display:grid` on an ordinary report panel, an `li` or `body` places its
children — child blocks, images, paragraphs, and each inline child and text
run — row by row into the columns of `grid-template-columns`, creating
auto-sized rows as needed (CSS Grid Layout with auto-placement only):

- **Columns.** `grid-template-columns` takes one track per value: a length or
  percentage of the container's content width (`em`/`rem` too), `Nfr` or
  `auto`; `none` (the initial value) is one `auto` column. Widths follow the
  track sizing algorithm for single-span items: `auto` and `fr` tracks start at
  their items' min-content contributions, `auto` tracks then grow to their
  max-content contributions, `fr` tracks share the space left (a track whose
  minimum exceeds its share keeps its minimum), and with `justify-content`
  `normal` (or `stretch`) any space still free stretches the `auto` tracks.
  Other `justify-content` values place the columns (`start`, `end`, `center`,
  `space-between`, `space-around`, `space-evenly`).
- **Gaps.** `column-gap` separates columns; `row-gap` separates rows (a
  percentage row gap is zero, since the height is indefinite).
- **Items.** `justify-items`/`justify-self` (`normal`, `stretch`, `start`,
  `end`, `center`, `left`, `right`) place an item in its column: stretch fills
  it unless the item has a `width`, the others use the fit-content width.
  `align-items`/`align-self` place it in its row as in a flex line (`stretch`,
  `start`, `end`, `center`, `baseline`). An image keeps its own size and starts
  its area under the normal alignments. `order` changes the placement order. A
  grid `li` is not a list item: no marker, not numbered.
- **Pagination.** Each row is placed whole; a page may break between rows, and
  the row gap at that break is dropped. A row that does not fit the rest of the
  page moves to the next page when every item in it is a single paragraph of at
  most three lines, and otherwise fails `FPDF1302`, like a flex row.
- **Initial values accepted:** `grid-template-rows` and `grid-template-areas`
  `none`, `grid-auto-flow:row`, `grid-auto-rows`/`grid-auto-columns` `auto`,
  and `grid-column`, `grid-row`, `grid-area` and their `-start`/`-end`
  longhands at `auto`.
- **Not supported:** `repeat()`, `minmax()`, `fit-content()`,
  `min-content`/`max-content` tracks, named lines, explicit placement, spans,
  named areas, explicit rows, `dense` or column flow, sized implicit tracks and
  `inline-grid` (`FPDF1202`); grid on `html`, paragraphs or inside tables, a
  grid container with `height` or `min-height` and `break-inside:avoid` on one
  (`FPDF1201`); columns whose fixed sizes and content
  minimums cannot fit (`FPDF1302`). See
  [`GridLayoutTests`](../tests/FactsPDF.Tests/GridLayoutTests.cs).

## Current-source `::before` and `::after` generated content

A selector may end in `::before` or `::after` (also the legacy `:before` and
`:after`, any case); the pseudo-element counts as a type selector for
specificity. Its winning `content` and `display` decide whether it generates a
box:

- `content` accepts `none`, `normal` and one or more strings (joined, CSS
  escapes decoded). `none` and `normal`, or `display:none`, generate nothing,
  so rules without `content` — `*,*::before,*::after{box-sizing:border-box}` —
  change nothing. `content` on an ordinary element changes nothing, as in a
  browser, and an `img` generates no pseudo-element box.
- By default the string is **inline text** at the start (`::before`) or end
  (`::after`) of its element's content, on report blocks, list items,
  paragraphs, inline text and table cells. It inherits from its element and
  takes its own declarations (`font-weight`, `color`, …) like a `span`; box
  properties on inline generated text fail `FPDF1201`.
- In a `display:flex` container, or with `display:block` or `display:flex`
  on the pseudo-element, it is a **block box** — a flex item in a flex
  container — with the declarations of a `div`: `width`, `height`, `padding`,
  borders, backgrounds, `flex-grow` and the rest of the flex item properties.
  Block pseudo-elements are generated in report panels, list items and `body`
  outside tables; in paragraphs and tables they fail `FPDF1201`.

The centred divider of business templates works as written:

```html
<style>
.divider{display:flex;align-items:center;white-space:nowrap;gap:1rem}
.divider::before,.divider::after{content:"";flex-grow:1;height:1px;border-top:1px solid #edeff3}
</style>
<div class="divider">Account summary</div>
```

**Not supported:** `counter()`, `attr()`, `url()` and quotes (`open-quote`, …)
in `content`, and `display` other than `inline`, `block`, `flex` and `none`
(`FPDF1202`); `::marker`, `::first-line`, `::first-letter`, pseudo-classes
(including `:not(:empty)`) and selectors that continue after a pseudo-element
(`FPDF1203`); generated content on `hr`, `html`, `q` and table structure
(`FPDF1201`). See [`GeneratedContentTests`](../tests/FactsPDF.Tests/GeneratedContentTests.cs).

## Current-source single-line flex rows

`display:flex` lays out the children of an ordinary report panel (`div`,
`section`, `article`, `header`, `footer`, `main`, `nav`, `aside`, `hgroup`,
`search`, `blockquote`, `figure`, `figcaption`, `address`, `dl`, `dt`,
`dd`), an `li` or `body` as **one row of flex items**, following CSS Flexible
Box Layout §9 for a single-line, row-direction container:

- **Items.** Every child block, image and paragraph element is a flex item.
  Each inline child (`span`, `strong`, `a`, …) and each run of text between
  them becomes its own item, so `<li><span>Label</span><span>Value</span></li>`
  is a two-item row; whitespace-only text between items is dropped. A flex
  `li` is no longer a list item: it has no marker and is not numbered. A table
  is a flex item of its own, sized by its width and content
  ([tables as flex and grid items](tables.md#tables-as-flex-and-grid-items)).
- **Main axis.** The flex base size comes from `flex-basis` (length,
  percentage of the container's content width, `em`/`rem`, `content`, or
  `auto`), else a definite `width`, else the item's max-content width measured
  with the same glyph advances, letter/word spacing and break opportunities the
  line wrapper uses. Items grow by `flex-grow` or shrink by `flex-shrink` ×
  base size through the flexible-length loop, clamped by `min-width`/
  `max-width` and by the automatic minimum (the min-content width, or a
  smaller `width`). `auto` margins take the free space first; `justify-content`
  then places the items: `normal`, `flex-start`, `start`, `left`, `stretch`,
  `flex-end`, `end`, `right`, `center`, `space-between`, `space-around` and
  `space-evenly`. `column-gap` (or the `gap` shorthand's second value) inserts
  a fixed gap between items; `row-gap` has no effect on a single line. `order`
  reorders items (stable for equal values). The `flex` shorthand accepts
  `none`, `auto`, one number (grow, with shrink 1 and a `0%` basis), and
  `grow shrink basis` in the CSS grammar; `flex-flow` accepts `row` and
  `nowrap`.
- **Cross axis.** Each item is laid out by the ordinary block layout at its
  resolved width. The line is as tall as its tallest item (or the
  baseline-aligned extent), or the container's `height`, or at least its
  `min-height`. `align-items` and `align-self` accept `normal`/`stretch`
  (a box or image without a `height` is laid out again at the line height),
  `flex-start`/`start`/`self-start`, `flex-end`/`end`/`self-end`, `center` and
  `baseline` (first text baselines; an item without text uses its border-box
  bottom). `align-content` is validated and dropped: a single line fills the
  container.
- **Pagination.** A flex row is placed whole. When it does not fit the rest of
  the page it moves to the next page only if every item is a single paragraph
  of at most three lines — which a browser also moves whole under the default
  `orphans`/`widows` of 2 — and otherwise fails `FPDF1302`, because a browser
  would split taller items between pages. A row taller than a page, items that
  cannot shrink to the container and items taller than a fixed-height container
  fail `FPDF1302`.
- **`html{display:flex}`** only centres a body narrower than a browser window.
  It (with its `justify-content`, `align-items`, gaps and `width:100%`) is
  accepted where `body` is the single item and its max-content width fills the
  page, which makes it exactly the block layout; a narrower body fails
  `FPDF1201`.
- **Not supported:** `flex-wrap` other than `nowrap` and `flex-direction`
  other than `row` (`FPDF1202`), `inline-flex` (`FPDF1202`), `display:flex` on
  `p`/`pre`/headings, lists or inside tables, except on the items of a list in a
  table cell (`FPDF1201`), `break-inside:avoid`
  on a flex container (`FPDF1201`), percentage vertical margins on flex items
  and a percentage `text-indent` in a content-sized item (`FPDF1201`).

Flex properties on elements that are not flex containers or items change
nothing, as in a browser. See [`FlexLayoutTests`](../tests/FactsPDF.Tests/FlexLayoutTests.cs).

## Current-source bounded report display suppression

This source-only print-report subset accepts `display:none` and
`display:block` (single identifier each; `display:flex` is described in
[single-line flex rows](#current-source-single-line-flex-rows)) on ordinary `p`/`h1`–`h6`,
`div`/`section`/`article` and `blockquote`, outside lists/tables.
A winning `none` suppresses the entire parsed block subtree. `block`
retains that element's existing block formatting. No other formatting
context is implemented.

**CSS semantics:** Embedded `<style>` rules and inline declarations share
the existing specificity, source-order and `!important` cascade. `display`
does not inherit; a descendant `display:block` cannot resurrect an
ancestor whose whole subtree has been suppressed. CSS-wide `inherit`,
`initial`, `unset` are deliberately rejected (`FPDF1202`): their full
standard computed behavior would include unsupported inline formatting.
Unknown `display` values (`inline`, `inline-flex`, `grid`, `contents`, etc.),
multi-token values and functions also reject `FPDF1202`, including unmatched
selectors. All applicable declarations are validated even if they lose the
cascade. `html`/`body`, spans/emphasis, `img`, `hr`, all list/table
structures and ordinary paragraphs/blocks *inside* lists or tables reject
`FPDF1201` when they declare `display`.

**Table parts:** report stylesheets often restate the table-part display
values. Each table part accepts the one value that names what the table
already renders, and the page is byte-identical to the page without it:
`thead{display:table-header-group}` (the single header row repeats on every
table fragment), `tbody{display:table-row-group}`,
`tfoot{display:table-row-group}` (footer rows render once after the body
rows), `tr{display:table-row}` and `td,th{display:table-cell}`.
`tfoot{display:table-footer-group}` fails `FPDF1202` with a message naming
`table-row-group`: a footer group repeats at the foot of every page fragment
in browsers, which `tfoot` rows never do here. Every other pairing of these
five keywords with an element (`thead{display:table-row-group}`,
`p{display:table-cell}` …) fails `FPDF1202` with the same message and
location as before. The value is checked while the stylesheet is parsed, like
every other `display` value: it is accepted only in a `style` attribute of that
table part, or in a rule whose every selector names that table part as its
subject element (`thead`, `.table tfoot`, `td,th`). A rule whose subject has no
element name (`.row{display:table-row}`, `*{…}`) or names another element fails
`FPDF1202` even when it matches nothing, so the first error on a page is
reported exactly as before
([`TablePartDisplayTests`](../tests/FactsPDF.Tests/TablePartDisplayTests.cs)).

**Actual rendering:** The reader parses and validates a hidden block and all
descendants first, then removes it from the parent document tree when closing
the element. It never reaches heading-keep lookahead, PDF font/glyph
measurement, width/height layout, outside margins, paint-command accounting,
decorations or page fragments. Visible siblings have the same positions, page
count and PDF bytes as a separately authored document that omits the hidden
block. Nested hidden sections, suppressed quotes and headings leave no blank
space. However, invalid markup/declarations, unavailable image resources,
CSS/input/depth budgets and other errors inside hidden markup are **not**
ignored: they fail using the pre-existing error codes and preserve any prior
destination bytes and position. The feature hides rendered output, not the
original source data; it is not an information-security boundary for HTML
input documents.

**HTML `hidden` attribute:** an empty `hidden` or `hidden="hidden"` (any ASCII
case) on these same blocks is the user-agent rule `[hidden]{display:none}`: it
sits below every author declaration, so the block is removed exactly as above
unless author CSS sets `display:block` on it. `hidden="until-found"` and other
values, and `hidden` on any other element (inline text, `html`/`body`, `img`,
`hr`, lists, tables and blocks inside them) fail `FPDF1103`
([`HiddenAttributeTests`](../tests/FactsPDF.Tests/HiddenAttributeTests.cs)).

**Excluded:** CSS `visibility`/`opacity`, list/table row
suppression, general `display` layout, flex/grid, CSS media queries, scripts,
reflow or animations. See the
[40-entry visible report](../examples/report-display-visibility.html),
[separate visible-only oracle](../examples/report-display-visibility-oracle.html),
`ReportDisplayTests` and the Native AOT stylesheet gate for measured
multi-page PDF parity and output-preserving failure cases. This source change
does not create a retained preview binary, customer distribution right, or
human-approved PDF reference.

## Overflow, clipping and line breaking

Text and boxes overflow as they do in Chrome instead of failing, and lines break where Chrome breaks them. Every
expectation below was read back from headless Chrome with the same font files (`OverflowAndLineBreakingTests.cs`).

**Line breaking.** Soft wrap opportunities come from the Unicode line breaking algorithm (UAX #14, Unicode 16
`LineBreak.txt`; `src/FactsPDF/Text/LineBreaker.cs`, generated data by `scripts/generate_line_break_data.py`). The rules are
checked against the Unicode `LineBreakTest.txt` conformance file, and then follow Chrome where it differs: a break is
allowed after every run of spaces whatever follows, two printable ASCII characters break by Blink's own table (read
back for every pair, with `word-break:normal` and `break-all`), a minus sign before a digit splits from a letter or digit
that precedes it only, and a hyphen or en dash that starts a word stays with the letter after it. Spaces hang at the end
of a line. CJK text breaks between ideographs, kana and Hangul syllables; closing punctuation never starts a line and
opening punctuation never ends one; small kana and the prolonged sound mark start a line (`line-break:auto|normal`) unless
`line-break:strict`, and `loose` also lets the iteration marks start one. `line-break:anywhere` breaks between any two
grapheme clusters. No dictionary is loaded, so scripts written without spaces (Thai, Lao, Khmer, Burmese) have no break
opportunities inside a word. A soft hyphen (`&#173;`) breaks the line and shows a hyphen (U+2010, or a hyphen-minus where the font
has no U+2010) only where it fits; `hyphens:none` removes it as a break opportunity, `hyphens:auto` needs a dictionary and fails `FPDF1202`.

**white-space.** `white-space` and its longhands `white-space-collapse` (`collapse`, `preserve`, `preserve-breaks`,
`break-spaces`) and `text-wrap-mode` (`wrap`, `nowrap`; `text-wrap:wrap|nowrap`) apply to every element, `span` and other
inline elements included. Preserved spaces and tabs keep their glyphs; spaces at a soft wrap hang (`pre-wrap`) or wrap too
(`break-spaces`), and trailing spaces count for alignment only before a forced break or at the end of the text. A tab runs
to the next multiple of `tab-size` (a number of space advances, default 8, or a length) from the start of the line, first-line
indent included. `word-break:keep-all` keeps letters, ideographs, kana and Hangul syllables together; `break-all` also
splits Latin words and digits (still honouring opening and closing brackets and quotes); `overflow-wrap:break-word|anywhere`
and `word-break:break-word` break a word only when no earlier opportunity fits.

**Overflow.** A word, line, box or image wider than its box overflows it instead of failing `FPDF1302`; a line always holds at
least one unbreakable unit. Content that spills past the right edge of the page area prints scaled down, as in Chrome: the
document is laid out again on a page as much wider as the overflow (at most 1.5 times) and printed at the original size
(`PdfOptions.PrintShrink`). Overflow beyond that is cut off by the page. Page headers, footers and numbers cannot be combined
with a scaled page (`FPDF1302`). Zero-width spaces, joiners, the word joiner and variation selectors advance nothing and paint
nothing; a zero-sized image keeps an empty box and paints nothing; supplementary Han (planes 2 and 3), CJK compatibility forms and the enclosed and radical blocks are accepted.

**overflow, overflow-x, overflow-y, text-overflow, visibility.** `overflow:hidden|clip` (and `visible`) on a block clips to
its padding box with a PDF clip path, one region per page the box spans; `clip` beside a visible axis clips one axis. A fixed
height lets content overflow it (or cuts it off with `hidden`), and the next block follows the box. `scroll` fails
`FPDF1201`, as does an axis computing to `auto` (explicit `auto`, or `hidden` beside `visible`) whose content overflows,
because Chrome prints the scrollbars and gives them room; `auto` over content that fits prints nothing extra. Clipping a box
with rounded corners fails `FPDF1201`. On an inline box and on table parts other than cells these properties do nothing or
fail as before. `text-overflow:ellipsis` ends a line that overflows its clipped block with an ellipsis (three full stops
where the font has no U+2026). `visibility:hidden|collapse` keeps the layout and paints no text, background, border, image or
list marker; a visible child of a hidden box paints. `visibility:hidden|collapse` declared on a table part fails `FPDF1201`, as does a hidden table whose cells have backgrounds or borders
(a hidden table without paint keeps its room and prints no text).
