# Embedded CSS stylesheets and selectors

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

Font paths are examples, not shipped files. The fontless ASCII mode still works. Library callers use the same `PdfConverter.Convert(html, output, options)` entry point.

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
| Inline declarations | Existing `style="..."` attributes with the same supported values |

HTML tag selectors are ASCII case-insensitive; class/ID values are case-sensitive. Class attributes are split on HTML whitespace after attribute entity decoding. CSS identifiers in this increment are ASCII; escaped and non-ASCII selector syntax is rejected. A synthetic document root is not an element and cannot match `*`. The existing partial HTML tree-construction rules still apply; no browser DOM conformance is claimed.

All style elements are compiled before content styling, so later styles can affect earlier content. Style contents use HTML RAWTEXT: entities are not decoded and fake markup inside CSS comments is not parsed as elements. The actual HTML `</style>` delimiter still ends the element even inside a CSS quote/comment. CSS comments separate lexical tokens without becoming descendant-combinator whitespace or concatenating identifiers.

No `link` stylesheet fetching, external files, `@import`, `@media`, `@page` or other at-rules are implemented. `media="print"` on a style element is not the same as supporting a CSS `@media` rule. Attribute/sibling/pseudo selectors, nesting, escapes, custom properties and functions remain unsupported. Style blocks within a paragraph/span/title are rejected rather than silently reparented.

## Cascade and inheritance

Winners are selected independently for each property:

1. Important declarations beat normal declarations.
2. Within the same importance level, inline declarations beat stylesheet declarations.
3. Stylesheet declarations use lexicographic ID/class/type specificity, then source order.

Only actually matching arms of a grouped selector contribute specificity. Repeating a simple selector increases specificity. Duplicate declarations use the last winner at the appropriate importance. This author cascade sits above the renderer's existing defaults; it does not implement user-origin styles, cascade layers, animations or transitions.

Inherited values are the parent's computed values; the parent's importance and selector specificity do not compete with declarations on a child. `inherit`, `initial` and `unset` are supported for the text properties below. `unset` resolves to inheritance or the initial value, not a lower-priority heading/paragraph default.

Renderer initial values are `PdfOptions.FontSize` (normally 12pt), line-height 1.2, black, left alignment, zero margins and automatic breaks. These are explicitly FactsPDF defaults, not a claim of browser initial font/theme behavior. Heading font-size multipliers and the default 8pt paragraph-after gap remain when no author declaration replaces them.

## Property scope

| Property | Values / applicability |
| --- | --- |
| font-size | pt/px/mm/cm/in lengths, positive parent-relative percentages and `em`, or positive `rem` relative to the computed `html` root font (configured initial font when absent); computed 1–144pt |
| font-weight | normal/bold or numeric 400/700; explicit role resources required |
| font-style | normal/italic; explicit role resources required |
| line-height | Unitless 1–10, `normal` (= default 1.2), positive `%`/`em` (at most 10× element font), positive `rem` (computed root font), or `pt/px/mm/cm/in` lengths; computed 0.1–1440pt, inherited as fixed points except unitless/normal |
| color | #rgb/#rrggbb; black, white, red, green, blue, gray, grey; bounded opaque rgb()/rgba() |
| text-align | left/center/right; bounded `justify` distributes retained U+0020 spaces only on automatically wrapped report lines via measured PDF `TJ` gaps; final/explicit-break lines stay ragged; start/end, RTL and CJK inter-character justification unsupported |
| white-space | `normal` (default) or `nowrap` on explicit `p` / `h1`–`h6`; collapsed spaces and explicit `br` retained; overwide unbroken lines rejected |
| letter-spacing | Supported report text and inline spans: `normal`, nonnegative 0–24pt print lengths, or scoped `em` (own computed font) / `rem` (computed root); resolved to fixed points before real PDF glyph advances, wrapping, centring and decorations; negative/%/calc unsupported |
| word-spacing | Supported text and inline spans: `normal`, 0–24pt nonnegative print lengths or scoped `em` (own final font) / `rem` (root font); computed extra advance after each collapsed U+0020 affects real PDF placement, wrapping, alignment, decorations and pagination; negative/%/calc rejected. Not full CSS Text. |
| text-indent | Explicit `p` / `h1`–`h6` outside table cells: nonnegative `pt/px/mm/cm/in/%`, scoped own-font `em` / root `rem`, or zero; percentages use resolved content width, `em/rem` fixed at cascade; only first logical line indented, later wrap/`br`/page fragments retain normal x; unfit first words fail `FPDF1302` with output preservation |
| text-decoration, text-decoration-line | none/underline/line-through as a single value; real PDF text-run decoration, current text colour, no full browser decoration features |
| margin-top, margin-bottom | Paragraphs/headings only; nonnegative pt/px/mm/cm/in/rem/em or zero, computed at most 14400pt; table-cell rem/em rejected |
| margin-left, margin-right | Paragraphs/headings only outside table cells; nonnegative pt/px/mm/cm/in/%/rem/em or zero, plus `auto`; percentages use containing width, rem uses computed html root, em uses own computed font; auto side takes remaining free space |
| margin (bounded shorthand) | Paragraphs/headings only; 1–4 TRBL values, vertical pt/px/mm/cm/in/rem/em/zero and horizontal pt/px/mm/cm/in/%/rem/em/zero/`auto`, including `margin:0.25rem 0.5em`; `margin:0 auto` centres an explicit-width outer box; a single CSS-wide keyword accepted |
| break-before, break-after | Paragraphs/headings only; auto/page. break-after also accepts avoid on headings outside lists; see [heading pagination](heading-pagination.md) |

These properties also accept `inherit`, `initial`, `unset` and `!important`. Matching paragraph-only properties on containers or spans is an error, including declarations that would lose to a stronger rule. Styling `br` is unsupported; a matching declaration errors. Head/title metadata remains non-rendering and does not acquire presentation from stylesheet rules. Inline styling of metadata is still rejected.

This engine's bounded `text-indent` does **not** implement negative/hanging indentation, `each-line`, `hanging`, `calc()`, container-level declarations, inline spans or table-cell paragraph indents. It affects actual glyph positions and first-line wrapping, not painted box geometry; the usual CSS inheritance/wide-keyword resolution is retained only where an applicable paragraph/heading declaration can be expressed.

The original stylesheet increment and later box-layout increments are distinct; current block width/padding/paint scope is documented in [development](development.md), and C1 style selection in [font styles](font-styles.md). Selectors alone do not make new layout features available. Width/height, padding, borders, background fills, flex/grid, tables, images and font-family/@font-face are not added by this increment. Unsupported properties or values fail explicitly even in an unmatched selector. That strict behavior deliberately differs from browser CSS error recovery; use the documented subset, not arbitrary website stylesheets.

## Current-source font-relative report text spacing

Controlled report templates can use `text-indent:0.4em`,
`letter-spacing:0.1em` and `word-spacing:0.15rem` alongside existing
fixed print lengths. `em` resolves against the **declaring element's final
computed font-size** after stylesheet cascade, even if a later or
higher-priority font-size declaration wins. `rem` uses the computed
`html` font-size. On `html` itself, spacing `rem` uses its
newly computed font-size; a document without `html` uses
`PdfOptions.FontSize`. Inherited spacing stays fixed in points rather
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
**whole explicit paragraphs/headings only**: authoring `white-space` on a
`span`, container, table structural element or image is rejected with
`FPDF1201`. A paragraph inside a table cell still uses its actual cell width
and will fail when an unbreakable label does not fit. `pre`, `pre-wrap`,
`pre-line`, `break-spaces`, `word-break`, non-breaking span fragments and
full CSS Text conformance are not implemented. Unsupported values are
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
`inherit` / `initial` / `unset` keywords. Normal selector precedence,
`!important`, non-inheritance, element applicability and existing background
painting/fragmentation apply identically.

This is **not** general CSS background shorthand support: images, URL loading,
gradients, positioning, repeats, size, multiple layers and function colours
are still rejected with FPDF1202, apart from the narrowly supported opaque rgb()/rgba() colours described below. There is no change to old PDF bytes unless
the caller actually uses the newly accepted shorthand. A malformed or
unsupported declaration still leaves existing output files intact.

## Current-source opaque RGB colours for reports

Supported text, background and solid-border colour declarations now accept
`rgb(35, 68, 107)`, `rgb(35 68 107)` or `rgba(35,68,107,1)`. The
channel values are **integers 0–255**; `rgba()` additionally accepts a finite
numeric alpha of **exactly 1** in comma syntax. These opaque values are lowered
to the matching `#rrggbb` colour before normal CSS cascade, box painting and
PDF serialization. For the same computed colour and declarations, PDF bytes
and page-fragment painting match the existing hex path. This applies to
`color`, `background`/`background-color`, `border-color` and its four
longhands, and solid `border` and per-side shorthands.

This does **not** implement CSS alpha compositing: `rgba(...,0)`,
`rgba(...,0.5)`, modern slash-alpha syntax, percentage channels, fractional
RGB channels, `hsl()`, gradients, colour mixing and nested `calc()` are
rejected with `FPDF1202`. Rejected conversions preserve existing outputs;
no CSS color-scheme, new colour spaces or implicit image loading is added.
See `OpaqueRgbCssTests.cs` and the real Linux Native AOT run of
`examples/report-print-spacing.html` with an independent hex projection.
This is a source-preview feature, not a public release or general CSS colour
conformance statement.

## Current-source computed report line-height

Business reports can specify `line-height:150%`, `line-height:1.5em`,
`line-height:18pt`, `line-height:24px` or a physical print length
such as `line-height:0.25in`. The renderer also accepts the existing
unitless `1–10` multiplier and `normal` (this engine's 1.2 default,
not an implementation of browser-dependent font normal metrics).

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
`lh`, `rlh`, `calc()`, font shorthand, arbitrary CSS and browser text shaping
remain unsupported and fail explicitly (`FPDF1202`). The accepted subset
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
If `html` is omitted, the base is the configured `PdfOptions.FontSize`
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
scoped implementation supports paragraph/heading `rem` margins. General container
margins, borders, `letter-spacing`, `word-spacing`, table/image/column dimensions,
`ex/ch/lh/rlh` and `calc()` remain unsupported.
Real current-head CI, independent code review, installable packaging,
commercial rights and human page approval are separate gates.

## Current-source root-relative report box lengths

Controlled report blocks, paragraphs and headings can now use
`width:13rem`, `min-width:10rem`, `max-width:14rem`,
`padding:0.5rem` and corresponding padding longhands. Each length resolves
to **the computed `html` font size** (or `PdfOptions.FontSize` when
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
with `FPDF1201`, not silently ignored. Container and span margins remain
unsupported. Scoped paragraph/heading root `rem` is described below.
Negative margins, vertical `auto`/`%`, `calc()` and full browser margin
collapsing are not implemented. A bounded `em` source slice below supports only paragraphs/headings. Geometry exceeding
the containing content rectangle fails with `FPDF1302` and preserves output.
Four-sided shorthand now counts four emitted declarations toward the existing
CSS budget, rather than the earlier two when both horizontal sides were zero.
For a ready-to-run example needing no external images or fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- \
  examples/report-print-spacing.html report-print-spacing.pdf
```

This is a renderer/source capability and not a public package release,
general browser margin collapsing, automatic print/page sizing or support for
general container margins, `calc()`, negative or vertical `auto` margins.
The separate root-`rem` and element-`em` margin features cover only
paragraphs/headings outside tables.
Existing reports without these explicit new values retain their old rendering
path. Results must still respect the chosen page geometry and explicit
resource contracts.

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
`word-spacing`, or general container margins. `calc()`, negative,
unbounded and invalid lengths fail with `FPDF1201`/`FPDF1202`; content
too wide for its resolved geometry fails with `FPDF1302`, leaving an
existing PDF destination unchanged. This is not full CSS Values/Sizing
or browser margin collapsing.

The source-owned [32-entry service report](../examples/report-em-layout.html)
and its [independently authored point-value oracle](../examples/report-em-layout-oracle.html)
exercise actual paginated output. `ElementRelativeBoxLengthTests.cs` and
the Linux Native AOT stylesheet verifier check PDF-byte/geometry equivalence,
text and error recovery at the specific source commit; they are not a
customer-authorised binary release or human PDF approval.

## Current-source root-relative paragraph margins

Controlled report paragraphs and headings (`p`, `h1`–`h6`) outside tables now
accept `margin:0.25rem 0.5rem`, `margin-top:0.4rem` and one to four
top/right/bottom/left values, with existing horizontal `auto` and `%`
rules. Each `rem` is resolved against the actual computed `html` font
size (or the configured `PdfOptions.FontSize` if `html` is absent).
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
`%`/`auto` and `calc()` reject with `FPDF1202`; oversized geometry
rejects with `FPDF1302`. General container/inline margins and any `rem`/`em`
margin inside table-cell paragraphs reject with `FPDF1201` even when
another declaration could win. Existing vertical-spacing/collapse rules
are unchanged; this is not general browser margin collapsing.

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
PDF bytes. The normal cascade, `!important`, and explicit `inherit`,
`initial` and `unset` work independently of width and padding. For example:

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
`text-transform:uppercase` or `text-transform:lowercase`; `none` is the
unchanged inherited default. These declarations work on styled headings,
paragraphs, inline spans and text-bearing containers with normal selector
cascade, `!important`, `inherit`, `initial` and `unset` semantics.
A nested `span style="text-transform:none"` can preserve a customer's
proper name inside an uppercase label.

The conversion is deliberately **ASCII Latin letters A–Z/a–z only**.
Caseless Chinese characters, numerals and punctuation are preserved.
The operation occurs before actual font/glyph fallback and width measurement:
searchable PDF glyph text, line wrapping, page positions, decoration widths
and pagination are based on the *transformed* characters. It does not rely
on postprocessing a rendered PDF or synthetic font faces. `none` and
unstyled content retain byte-identical PDF output.

Locale-specific casing, Unicode multi-character expansions, non-ASCII
cased letters, `capitalize`, `full-width`, `full-size-kana`, language
sensitivity and general Unicode text transforms are **not** supported.
Non-ASCII cased letters and symbols with Unicode case mappings (including Roman numerals and circled Latin letters) under an active transformation explicitly raise
`FPDF1305` rather than silently producing incorrectly cased output;
unsupported CSS values raise `FPDF1202`, and applying this text property
to a standalone image raises `FPDF1201`. Normal missing-result-glyph
diagnostics (`FPDF1504`) still apply. All failures preserve an existing
destination. No CSS Text standard-compliance claim or publicly distributed
new engine binary is implied.

Run the fontless [case labels report](../examples/report-case-labels.html)
from source; the separately authored
[visible-text oracle](../examples/report-case-labels-oracle.html), NUnit
`TextTransformTests.cs` and the Linux Native AOT stylesheet verifier compare
actual PDF bytes, searchable text, mixed spans and negative recovery. The
engine change starts at source commit `8c946a29423ee3de6c171548fe6f633393ca76b6`;
introduction in a distributed version is unknown.

## Budgets and diagnostics

`PdfOptions` includes per-conversion limits:

| Option | Default |
| --- | ---: |
| MaxCssCharacters | 262144 across embedded and decoded inline CSS, including comments |
| MaxCssSelectors | 4096 selector arms, including comma groups and empty blocks |
| MaxCssDeclarations | 32768 across embedded and inline declarations |
| MaxCssMatchOperations | 5000000 charged candidates, matching steps and offered declarations |

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
