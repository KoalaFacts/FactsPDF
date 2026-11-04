# FactsPDF feature notes

Detailed notes for each bounded HTML/CSS capability added to the development
source, grouped by area. Each note states exactly what is supported, what is
deliberately rejected, and where the example report, oracle and test evidence live.

All notes describe **current source only**: none of them implies a public
package, a new private candidate, human PDF approval or distribution rights.
For the overview and quick start, see the [README](../README.md); for the
authoritative CSS contract, see [CSS stylesheets](css-stylesheets.md).

## Contents

- [Stylesheets](#stylesheets)
- [Colours](#colours)
- [Borders and rounded corners](#borders-and-rounded-corners)
- [Text, fonts and typography](#text-fonts-and-typography)
- [White space and line wrapping](#white-space-and-line-wrapping)
- [Lengths, margins and spacing](#lengths-margins-and-spacing)
- [Report panels and box sizing](#report-panels-and-box-sizing)
- [Images](#images)
- [Tables and lists](#tables-and-lists)
- [Report elements](#report-elements)
- [Pages, pagination and document properties](#pages-pagination-and-document-properties)

## Stylesheets

### Print-only stylesheet rules for business reports (source-only)

Reusable report stylesheets can now wrap existing supported CSS in **`@media print { ... }`** or **`@media all { ... }`**. For example:

```html
<style>
  @media print {
    .invoice { padding:8pt; background:#edf4fa; border:2pt solid #315f82 }
    .invoice p { margin:0; color:#27618b; font-size:11pt }
  }
</style>
<section class="invoice"><p>Inspection completed</p></section>
```

Unlike parse-only CSS, these scoped rules participate in ordinary source order, specificity, inheritance and `!important`, and change **real PDF fonts, text layout, box paint and pagination**. No external stylesheets or browser are needed. Only one bare media type (`print` or `all`) per block is supported; `screen`, conditional queries, comma lists, nested `@media`, `@supports`, `@container`, `@import`, `@page` and new elements/properties are still rejected with explicit errors, including inside rules that do not match any element.

The [44-record multi-page print report](../examples/report-print-media.html) is compared against a [separately written inline-style PDF oracle](../examples/report-print-media-oracle.html), with NUnit, supplied TrueType, managed CLI, Native AOT qpdf/Poppler and protected failure/retry checks. Bounded engine source [`70f37e7e`](https://github.com/KoalaFacts/FactsPDF/commit/70f37e7e881bade30851ecaa06528a27813f0403). This is not full CSS Media Queries, a public package, new private binary, human PDF approval or a customer licence.

## Colours

### Computed currentColor for brand-consistent reports (source-only)

Report templates can reuse one text colour for solid paint:
`color:currentColor` preserves the **computed inherited parent text ink**;
`background:currentColor` and `background-color:currentColor` paint the
**final computed colour on that element**. Existing border and decoration
`currentColor` values continue through the same opaque PDF ink path.
Stylesheet/inline cascade, `!important`, inheritance, real font metrics,
nested panels and page continuation are honoured.

The [48-entry inspection report](../examples/report-current-colour.html) and
[independently authored HEX control](../examples/report-current-colour-oracle.html)
must emit identical PDF bytes. Tests cover font roles, managed API/CLI,
pagination, text geometry and preserved output on invalid input. Native
Linux/Windows AOT probes additionally inspect qpdf/Poppler text/coordinates.
No alpha, system colours, `color:transparent`, gradients, URLs or general CSS
Color are implied. Implementation [`3dbcded4`](https://github.com/KoalaFacts/FactsPDF/commit/3dbcded4f5d39b16b79bbff7aac4132305659d4a).
This is source-only, not a revised private candidate, public package,
human PDF approval or permission to distribute.

### Standard opaque CSS named colours for printable reports (source-only)

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
`color:transparent`, system/theme colours, arbitrary `currentColor`,
alpha blending, gradients and unsupported selectors/elements still reject.
Implementation source [`c9138808`](https://github.com/KoalaFacts/FactsPDF/commit/c9138808d785986712f17e977fa2371854cb6aed).
Exact-head CI, independent review, private software, human reference approval
and customer permission remain separate; no public release is implied.

### Bounded percentage RGB colours for business reports (source-only)

Report templates may now use `rgb(20%,40%,60%)`, `rgb(10% 25% 50%)`
and `rgba(0%,50%,0%,1)` in the existing colour, background, border and
text-decoration paint slots. Exactly **three percentage channels** (0–100%,
fractional values allowed) are converted to 8-bit opaque PDF RGB; midpoint
conversion rounds to the nearest byte with ties up. The existing three
integer-channel forms still work. CSS specificity, `!important`, inheritance,
real PDF text/paint, pagination, resources and previous-output protection
use the same established engine paths.

Mixed percentage/integer channels, decimal *numeric* channels, transparency,
slash alpha, nesting, `calc()` and general CSS Color remain unsupported;
unrecognized syntax rejects with `FPDF1202`, including unmatched rules.
This is not a general CSS Colors or alpha-compositing implementation.

[48-entry real paginated report](../examples/report-rgb-percent-colours.html) and
[independently authored HEX control](../examples/report-rgb-percent-colours-oracle.html)
target exact PDF byte, searchable body, page/position, managed API/CLI,
Linux/Windows Native AOT qpdf/Poppler and failure/recovery equivalence.
Implementation source [`6a79f3f6`](https://github.com/KoalaFacts/FactsPDF/commit/6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d).
Actual CI, independent review, retained preview binary, human approval
and customer distribution are separate checks; no release is implied.

### Bounded opaque HSL report colours (source-only)

Business-report templates can use `hsl(210,60%,40%)`,
`hsl(210deg 60% 40%)` or `hsla(120,100%,25%,1)` as scoped
`color`, `background`/`background-color`, border colours and
text-decoration colour. Hue is a finite number or `deg` from
-3600 through 3600 (wrapped by 360); saturation/lightness accept
0–100% including fractions. HSL is converted to opaque 8-bit RGB
before the existing PDF ink/background/border paths, retaining CSS
cascade, font/layout geometry, pagination and resource protection.

The [48-entry colour report](../examples/report-hsl-colours.html)
and [independent fixed-hex oracle](../examples/report-hsl-colours-oracle.html)
must produce identical actual PDF bytes. Source NUnit, managed CLI
and Linux/Windows Native AOT qpdf/Poppler check searchability, multi-page
geometry and original-output recovery. No transparency, slash alpha,
turn/rad, nesting, gradients or general CSS Color: unsupported values
fail `FPDF1202`. Implemented in source `ee5af1a67d8aa88fa23da281ea6ab23414f9ef22`; exact-head CI,
independent review, private software, human approval and customer
distribution remain separate.

### Opaque `rgb()` and `rgba(…,1)` colours (source-only)

Controlled report templates can use opaque `rgb(35, 68, 107)`, `rgb(35 68 107)` and `rgba(35,68,107,1)` for text, backgrounds and solid borders. These produce the same painted PDF as matching hex colours; non-opaque alpha and gradients still fail explicitly. See [bounded CSS colours](css-stylesheets.md#current-source-opaque-rgb-colours-for-reports) and the [print report example](../examples/report-print-spacing.html).
For a fontless case-formatting example with a preserved proper-name span, see the
[report case labels](../examples/report-case-labels.html).

## Borders and rounded corners

### LTR logical border component styling for business reports (source-only)

Use `border-inline-width:2pt 4pt`, `border-block-style:solid dashed`,
`border-inline-color:rebeccapurple #315f82`, or individual
`border-inline-start-width`, `border-inline-end-style`,
`border-block-start-color` and corresponding component sides.
These **18 bounded CSS property spellings** map LTR inline-start/end to
physical left/right and block-start/end to top/bottom. Paired components
take one or two values; single sides take one value. Every width,
solid/none/dashed/dotted style, and opaque colour uses the existing
validated physical-border rendering path. `inherit`, `initial` and
`unset` stand alone and apply per side; `!important`, physical
longhands, existing shorthands and source order cascade independently.

The [44-entry PDF report](../examples/report-logical-border-components.html)
and [separately authored physical-border oracle](../examples/report-logical-border-components-oracle.html)
test actual painted strokes, geometry, page breaks, text order, supplied
TrueType, API/CLI, Linux/Windows native qpdf/Poppler and protected
invalid-to-success recovery. Unknown values also reject in unmatched
stylesheet rules. RTL, vertical writing, new applicable elements, alpha,
logical corner radii and the full CSS Borders specification remain
unsupported. First implemented in source
[`99328f07`](https://github.com/KoalaFacts/FactsPDF/commit/99328f071395d5d7cc1eedad9138f05ee118fb0f);
this is not a new package, approved reference PDF or distribution grant.

### LTR logical borders for printable report cards (source-only)

Controlled horizontal left-to-right report layouts may use `border-inline`
and `border-block` for paired side strokes, or
`border-inline-start`, `border-inline-end`, `border-block-start` and
`border-block-end` for a single side. In this explicitly LTR model,
inline-start/end map to **left/right** and block-start/end to **top/bottom**.
Each accepts the same existing width/style/opaque colour terms as a physical
`border-left` or `border-top`, in any order, or one CSS-wide
`inherit`/`initial`/`unset` keyword. Omitted components reset to the
same physical border defaults; each affected width, style and colour
participates independently in the actual CSS cascade.

```html
<section style="padding:8pt;border-inline:2pt solid #315f82;
                border-block-end:1pt dashed rebeccapurple">
  <p style="margin:0">Service inspection complete</p>
</section>
```

The [44-entry paginated report](../examples/report-logical-borders.html) and
[explicit physical-side control](../examples/report-logical-borders-oracle.html)
exercise real PDF borders, content positions, page fragmentation, TrueType,
API/CLI, Linux/Windows native qpdf/Poppler and protected errors. No RTL,
vertical writing modes, logical border-width/style/color subproperties,
CSS Borders in general, arbitrary alpha, or additional applicable elements
are claimed. Source implementation [`6916ef5d`](https://github.com/KoalaFacts/FactsPDF/commit/6916ef5da96068c167690ef3a0b5f1c94d41f255).
This is not a newly distributed/private binary or human/customer approval.

### Current-source dashed and dotted report borders

Business report panels and existing decorated text, image and table boxes may
use `border:2pt dashed #28557a`, `border:2pt dotted #a3452e` or individual
`border-*-style` values. Dashed edges use bounded PDF dash strokes; dotted
edges use round-cap PDF strokes. Width, colour, padding, containing width and
page continuation use the existing box model; a side's dash pattern restarts
per page fragment, while only the first and last fragments paint their top
and bottom edges. Unstyled and solid-border reports retain their legacy PDF
bytes. This is **not** arbitrary/multi-corner rounded or double borders or full CSS Borders support. One bounded border-radius is separately documented below.

See the [76-record real report](../examples/report-patterned-borders.html),
[scope and failure contract](css-stylesheets.md#current-source-bounded-patterned-report-borders),
and [Native AOT geometry/recovery evidence](../scripts/verify_native_feature_paths.py).
No public binary or customer distribution rights follow from this source change.

### Independent CSS corner-radius longhands (source-only)

For invoices, summary cards and branded report headings, four separate CSS
`border-top-left-radius`, `border-top-right-radius`,
`border-bottom-right-radius` and `border-bottom-left-radius`
longhands set individual **real PDF vector corners**, including across pages.
Each takes one physical `pt/px/mm/cm/in` length (zero or 0.25–72pt) or
a CSS-wide keyword, independently competing with `border-radius`
shorthand using specificity, `!important` and source order. Only ordinary
report blocks outside tables/lists, opaque fills and equal-width/colour solid
border rings are supported; no percentages, elliptical two-axis radii,
font-relative radii, rounded images/tables, or full CSS Borders.
The [50-card longhand report](../examples/report-corner-longhands.html)
is compared at native PDF-byte level against the existing independently
authored [four-corner control](../examples/report-asymmetric-rounded-panels.html).
Read [the exact CSS contract](css-stylesheets.md#current-source-rounded-business-report-cards-2026-11-01).
This is source implementation, not a new public/private distributable,
customer use right or human PDF approval.

### Rounded commercial report cards (source-only)

`border-radius:12pt 9pt 6pt 3pt` now paints four independent PDF corner curves
for ordinary report panels (one-to-four physical shorthand values). The existing
single-radius output remains byte-identical. Text, pagination, border protection
and unsupported-value errors remain scoped. The [54-card single-radius report](../examples/report-rounded-panels.html),
[50-card four-corner report](../examples/report-asymmetric-rounded-panels.html)
and [independent physical-unit oracle](../examples/report-asymmetric-rounded-panels-oracle.html) are scoped,
not browser CSS conformance. See [precise value and failure rules](css-stylesheets.md#current-source-rounded-business-report-cards-2026-11-01).
No new installable preview, human reference approval or customer grant is implied.

## Text, fonts and typography

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
widths, combined decoration lines or general browser propagation.
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


### Semantic inline change marks and fine print (source only)

In an invoice, updated quote or service register, `<small>` renders a
secondary note at 5/6 of its computed parent font size; `<u>` and
`<ins>` underline, while `<s>` and `<del>` strike through changed text.
These reuse actual measured PDF glyphs, TrueType font chains and decoration
strokes, including wrapping and pagination. Supported CSS font sizing,
colours, decoration styles, specificity and `!important` can override
the presentation of each tag. A removed line remains searchable PDF text;
the strike is *not* redaction.

These five tags are inline-only, with explicit closing tags and existing
depth/element budgets. Unsupported `datetime` or `cite` on `del`/`ins`,
block descendants, CSS lengths or malformed markup fail using the existing
output-preserving diagnostics. There are no native change annotations, PDF
history/timestamps, browser user-agent styles, or automatic fonts.

A [44-change work order](../examples/report-inline-changes.html)
and [explicit CSS/span oracle](../examples/report-inline-changes-oracle.html)
exercise actual PDF byte parity, positioned glyphs, paint and pages.
`SemanticInlineReportTests.cs` also tests supplied TrueType,
CSS override and failure recovery; the Native AOT stylesheet verifier
checks qpdf/Poppler and protected output. Implementation source
`a1300f3943efcf92426836b963e72d28b078e192`; this does not establish
independent code review, public/private new binary delivery, human page
approval or commercial distribution rights.


### Relative bold/light styling for report text (source-only)

Use `font-weight:bolder` or `font-weight:lighter` in existing paragraphs, headings and spans to select the explicitly configured **bold** or **normal** TrueType role. For example, a bold inspection card can contain a lighter detail line and a bold status. The PDF really changes font metrics, embedded faces, text wrapping and pagination. This deliberately bounded two-role model does not synthesize CSS 100–900 weights or discover fonts: missing bold faces fail with `FPDF1507`, preserving the previous PDF.

[48-entry report](../examples/report-relative-font-weights.html) · [Independent explicit-role control](../examples/report-relative-font-weights-oracle.html) · [Exact scope](css-stylesheets.md#bounded-bolderlighter-font-roles-for-report-typography-source-only). Source `e94987af`; no new installable candidate or customer distribution rights.

### Familiar font-size keywords for report text (source-only)

For controlled report text, `font-size:xx-small/x-small/small/medium/large/x-large/xx-large`
uses the documented fixed 7.2/9/10/12/14.4/18/24pt scale.
`smaller` and `larger` compute from the parent font at 5/6 and 6/5.
This works through existing cascade, `!important`, inherited text and
actual PDF glyphs, wrapping and pagination, not merely CSS parsing.
Unsupported values and computed sizes outside 1–144pt reject with
`FPDF1202` and protect the previous PDF. No general font shorthand or
browser system-font selection is implied.

The [40-entry paginated report](../examples/report-font-size-keywords.html),
[independent point-size oracle](../examples/report-font-size-keywords-oracle.html),
NUnit and Native AOT/qpdf/Poppler checks cover this source-only feature at
[`b95f1bd3`](https://github.com/KoalaFacts/FactsPDF/commit/b95f1bd3c5d8275bff21f399c9cf59a2d413c024).
No updated private binary, public release, human reference approval or
customer licence is implied. [Exact limits](css-stylesheets.md#bounded-font-size-keywords-for-printable-business-reports-source-only).

### Numeric `font-weight` 100–900 (source-only)

**Current source typography:** report templates may use numeric `font-weight`
in exact hundred steps from `100` to `900`. Values `100`–`500` select the
caller-supplied normal font chain and `600`–`900` the caller-supplied bold
chain, including inline labels, CSS selectors and cascade. Real font advances
affect PDF line wrapping and pagination. Missing bold resources fail explicitly;
FactsPDF does not synthesize variable weights. See the
[64-entry example](../examples/report-numeric-font-weights.html) and
[font role contract](font-styles.md). This source addition is not a new
public NuGet binary or stable CSS Fonts implementation.

### Computed `line-height` (source-only)

Controlled report typography can use `line-height:150%`, `line-height:1.5em`, `line-height:18pt` or print units such as `0.25in`. Percent/em compute an absolute line height against the element's own computed font size; that fixed height is inherited unchanged even when nested text changes font size. Unitless `line-height:1.5` remains a multiplier inherited by children. These values affect real line boxes and pagination and reject invalid input before replacing output. See [line-height contract](css-stylesheets.md#current-source-computed-report-line-height).

### Root-relative `rem` typography (source-only)

On current source, business report templates can opt into `font-size:1.2rem` and `line-height:1.5rem`. `rem` uses the `<html>` element's computed font size, or `PdfOptions.FontSize` when no `html` element exists. On `html` itself, `font-size:rem` resolves against the initial configured font size to avoid a self-reference; `line-height:rem` uses its newly computed root size. Font sizes resolve to 1–144pt; fixed line heights resolve to 0.1–1440pt and inherit as points. This feeds actual glyph layout, wrapping and pagination (not merely syntax acceptance). This typography feature is separate from the newer scoped `rem` lengths for ordinary report box width/min-max/padding; `rem` margins are now limited to paragraphs/headings outside table cells; borders and image/table/column sizing still reject `rem`; only bounded text-indent/letter-spacing/word-spacing now accept it. See the [root-relative contract](css-stylesheets.md#current-source-root-relative-report-typography) and [runnable multi-page example](../examples/report-root-relative-typography.html); these source changes are not the previously retained installable candidate.

### Font-relative text spacing (source-only)

Source-built business report templates can scale **text spacing**
with typography: `text-indent:0.4em`, `letter-spacing:0.1em`
and `word-spacing:0.15rem`. Each length uses its element's final font
or the computed `html` root, then changes actual glyph positioning,
first-line wrapping and pagination. Letter/word advances remain bounded
to 0–24pt after computation; percentage letter/word spacing, negative
values, inline text indentation and general browser CSS remain unsupported.
See [the contract](css-stylesheets.md#current-source-font-relative-report-text-spacing)
and [72-entry example](../examples/report-relative-text-spacing.html).
This source feature does not update retained private installable software.

### First-line `text-indent` (source-only)

For report explanatory paragraphs and headings, opt-in `text-indent:18pt`, `text-indent:0.25in` or `text-indent:15%` insets **only the first logical line** within the actual content width; later wrapped lines, explicit `<br>` lines and subsequent pages are not re-indented. The paragraph box/background keeps its original position. Percentages resolve against its actual content width. Nonnegative `pt/px/mm/cm/in/%` or zero are supported; overlarge insets or first words that cannot fit reject with `FPDF1302` without overwriting the destination. Inline spans, containers and table-cell paragraphs cannot set it; negative/hanging indents and `calc()` are unsupported. This is a scoped PDF text layout capability, not full CSS Text conformance.

### Bounded `letter-spacing` (source-only)

Report headings, labels and inline text can use a bounded, nonnegative
`letter-spacing:0.75pt` or equivalent `px/mm/cm/in` print length. This changes
real PDF glyph advances, line breaks, horizontal alignment and text-decoration
widths, including with explicitly bound Chinese TrueType glyphs. `normal` and
zero retain legacy PDF bytes. Negative spacing, percentages, `calc()`
and spacing over 24pt are rejected, rather than silently dropped. See the
[scoped tracking contract](css-stylesheets.md#current-source-bounded-letter-spacing).

### Soft-wrapped `text-align:justify` (source-only)

Controlled report prose can opt into `text-align:justify` to distribute the measured remaining width among collapsed U+0020 spaces on **automatically wrapped** lines. The final line, explicit `<br>` lines, single-line/nowrap text and lines without retained word spaces remain ragged; no RTL, CJK inter-character or full CSS Text claim is made. Both Courier and explicit TrueType paths use real PDF `TJ` displacement. See [bounded justification](css-stylesheets.md#current-source-soft-wrapped-report-justification).

### Bounded `word-spacing` (source-only)

Controlled report prose can opt into `word-spacing:0.01in` (equivalent to `0.72pt`) at collapsed word spaces. This changes actual PDF word positions, line wrapping, alignment and pagination; the `normal`/zero path preserves legacy PDF bytes. The renderer uses PDF `TJ` displacement for Courier and embedded CID TrueType, not byte-specific `Tw`. Only nonnegative print lengths up to 24pt are supported. See [word-spacing boundaries](css-stylesheets.md#current-source-bounded-word-spacing).

### Current-source LTR text alignment for report labels

Common report templates can now write `text-align:start` or
`text-align:end` on paragraphs, headings and inherited ordinary
containers. In the engine's **left-to-right horizontal** text model,
these correspond respectively to `left` and `right`: the actual PDF
glyph X coordinates, font measurement, first-line indentation, wrapping and
cross-page continuation follow the existing physical-alignment renderer.

This applies through embedded stylesheets, inline declarations, normal
cascade and inheritance; it does **not** add RTL, `direction`, HTML `dir`,
vertical writing, inline-span start/end alignment or full CSS Text behavior.
Unsupported values and directions fail without replacing an existing PDF.
Compare the [46-entry printable report](../examples/report-logical-text-align.html)
with its [physical alignment oracle](../examples/report-logical-text-align-oracle.html)
and the [exact CSS contract](css-stylesheets.md#current-source-ltr-text-align-startend).
This is current source only, not an updated package or distribution grant.

### Current-source ASCII capitalized report labels

`text-transform:capitalize` now formats English service-register labels as
actual PDF glyphs before font selection, wrapping and pagination. Only the
first lowercase ASCII letter in each ASCII letters/digits token is capitalized;
punctuation starts another token while styled inline spans do not. Mixed-case
remainder stays unchanged. Non-ASCII cased Unicode text fails explicitly
rather than being silently transformed. See the
[54-entry report](../examples/report-capitalized-register.html),
[independently authored literal-case PDF oracle](../examples/report-capitalized-register-oracle.html)
and [bounded CSS contract](css-stylesheets.md#current-source-ascii-report-text-transform).
This source capability is not a rebuilt binary or a public NuGet release.

### Coloured and patterned text decorations (source-only)

For inspection findings, revised quotes and status notes, `text-decoration:overline dashed blue` or `text-decoration:underline dotted #ff0000` paints a real clipped vector line at the font-measured text position. You can also set `text-decoration-color` and `text-decoration-style` individually; their CSS-wide cascade and `currentcolor` follow the existing styling rules. The unstyled and legacy solid paths preserve existing bytes. One line only; no double, wavy, multi-line, alpha/transparency, variable thickness or browser-wide propagation. See the [source example](../examples/report-text-decoration-paint.html), [PDF implementation tests](../tests/FactsPDF.Tests/TextDecorationPaintTests.cs), and [bounded CSS contract](css-stylesheets.md#current-source-colored-and-patterned-report-text-decorations). This is current source, not a newly distributed binary or customer licence.

## White space and line wrapping

### Current-source multiline report notes

For controlled invoices, inspections and customer notes, explicit paragraphs and
headings can use `white-space:pre-line` to **retain authored line breaks** from
LF, CRLF, CR and form-feed while continuing to collapse ordinary spaces/tabs.
Lines still soft-wrap to the measured PDF content width and continue across pages;
the final source segment break is discarded. An explicit `<br>` remains a break.
Scoped stylesheet/inline CSS, cascade and explicit p-in-table-cell width work,
but container/span declarations, `break-spaces`, RTL/shaping and
unrestricted CSS Text do not. For unwrapped source lines, use the separate
bounded `<pre>` or `white-space:pre` report mode. Use the separate bounded `pre-wrap` mode when
an authored report must retain visible U+0020 space advances.

The [72-record report](../examples/report-pre-line-notes.html) has an
[independently authored explicit-break oracle](../examples/report-pre-line-notes-oracle.html).
Source and Native AOT PDF text, position, pagination and output restoration
are checked by `WhiteSpacePreLineTests` and `verify_native_feature_paths.py`.
This source feature is not a released package or evaluation grant.

### Current-source preserved-space multiline report notes

Explicit business-report paragraphs and headings can now use
`white-space:pre-wrap` to keep **every authored ASCII space** (including
leading, repeated and trailing spaces) and CRLF/CR/LF/form-feed line breaks.
The spaces are measured PDF glyphs: they affect word positions, automatic
wrapping, paragraph alignment, table-cell line occupancy and pagination.

```html
<p style="white-space:pre-wrap">REF001  ASSET   CHECK
  OWNER:  AB</p>
```

The source-only boundary is deliberate: no tab stops or other control
whitespace (explicit `FPDF1305`), no `break-spaces` or span/container
white-space declarations, RTL or full CSS Text. The separate `pre` mode
preserves these spaces and authored breaks but forbids all automatic wrapping. An unbreakable word or
single unfit space errors with `FPDF1302`, preserving an existing PDF.
CSS cascade, `!important` and CSS-wide reset values use the same
paragraph-style path as `pre-line`; ordinary `normal` and `nowrap` PDFs
remain unchanged.

See the [72-record authored report](../examples/report-preserved-space-notes.html),
[independently authored collapsed-space control](../examples/report-preserved-space-notes-oracle.html)
and [scoped contract](css-stylesheets.md#current-source-preserved-space-report-notes).
Source NUnit and Linux/Windows Native AOT/Poppler evidence check the actual
glyph positions, line breaks, pagination and failed-output restoration.
No released binary, NuGet package or customer grant follows from this change.

### Current-source preformatted report audit blocks

For inspection logs, reconciliations and aligned audit notes, an explicitly
closed `<pre>…</pre>` block or `white-space:pre` on an existing `p`/heading
preserves **every authored U+0020 space** and CRLF/CR/LF/form-feed newline,
without soft-wrapping a line. This is real PDF glyph positioning and pagination,
not an HTML tag accepted without rendering. An explicitly styled inline span
may colour part of a line; `<br>` remains a manual break. The engine uses the
caller's chosen font chain (or built-in Courier when no font is supplied) and
does not silently substitute a browser-style monospace face.

```html
<pre>REF001   OK
REF002   NEEDS REVIEW
TOTAL    002</pre>
```

Long lines that do not fit the resolved content width fail `FPDF1302` rather
than wrapping, shrinking or clipping. Tabs and other control whitespace fail
`FPDF1305`; no tab stops, general CSS Text, nested blocks, or `pre` within
lists/tables are implemented. `<pre>` is an ordinary standalone block, not
a full HTML5 preformatted/semantic renderer. Stylesheets, `!important`,
`inherit`/`initial`/`unset`, page breaks, true font metrics and protected
output still use the established paragraph pipeline. This is source code only,
not a new installable binary or distribution grant.

See the [72-row audit report](../examples/report-preformatted-audit.html),
[independent pre-wrap PDF oracle](../examples/report-preformatted-audit-oracle.html),
[CSS/HTML contract](css-stylesheets.md#current-source-preformatted-audit-blocks),
and source/Native AOT tests.

### `white-space:nowrap` report labels (source-only)

Controlled identifiers or approval labels can use `white-space:nowrap` on `<p>` and headings; multiline inspection notes can use `white-space:pre-line` instead. Spaces still collapse, explicit `<br>` works, and content too wide for the measured line fails with `FPDF1302` without replacing the output. `normal` is byte-identical to the default; `pre` is separately supported as a bounded unwrapped preservation mode, while `break-spaces` and arbitrary inline white-space spans remain unsupported. See [bounded white-space](css-stylesheets.md#current-source-nonwrapping-report-labels) and [multiline notes](css-stylesheets.md#current-source-multiline-report-notes).

### `overflow-wrap:break-word` for long identifiers (source-only)

Long unbroken invoice and approval identifiers can use `overflow-wrap:break-word` on explicit report paragraphs/headings, including flat paragraphs inside fixed-column table cells. The measured PDF layout keeps ordinary words whole when they fit a fresh line, but splits an oversized token at supported glyph boundaries and continues across pages. `white-space:nowrap` takes precedence; unsupported `anywhere`/inline declarations and an unfit individual glyph reject without modifying a previous PDF. This bounded source-only feature is not full browser CSS Text or automatic hyphenation. See the [contract](css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers), [32-entry report](../examples/report-overflow-wrap.html), and [independent manual-break oracle](../examples/report-overflow-wrap-oracle.html).

### Full-width invoice identifier wrapping (source-only)

For tightly spaced reference numbers, use `word-break:break-all` on a whole
`p` or `h1`–`h6` (including a flat `p` inside a fixed-column table cell).
Unlike `overflow-wrap:break-word`, which moves a fitting word whole to the
next line, `break-all` fills the remaining available PDF line using real font
glyph advances; the rest of the identifier continues in source order. CSS
cascade, `!important`, `normal`, explicit `inherit/initial/unset` and
existing `white-space:nowrap` rules apply. It is a bounded simple-glyph
renderer, **not** Unicode grapheme segmentation or a browser CSS Text engine.

The [48-record report](../examples/report-word-break-identifiers.html) and
[separately authored explicit-break control](../examples/report-word-break-identifiers-oracle.html)
exercise source NUnit and real Linux/Windows Native AOT PDF-byte, page-text and
failure-recovery gates. Unsupported `keep-all`, `break-word` as a value of
`word-break`, inline spans, arbitrary container declarations, complex shaping
and width too small for one glyph fail without replacing the previous PDF.
[Exact CSS contract](css-stylesheets.md#current-source-word-break-for-invoice-identifiers).
Source implementation is ac6cec7000b308b403dd95abcd14441240b1f791; this does not update the retained private
`0.1.0-preview.1` candidate, grant customer rights or constitute human PDF approval.

## Lengths, margins and spacing

### Pica and quarter-millimetre CSS for printed reports (source-only)

Use `width:14pc`, `padding:4q`, `border:4q solid #23456a`,
`margin:1pc 0` and `font-size:1pc` in controlled HTML report templates
(one `pc` = 12pt, four `q` = 1mm). Existing bounded CSS properties
render these print lengths in actual PDF text, geometry and pagination.
Invalid values and unsupported elements fail under the original rules
without replacing a previous PDF. See the
[40-entry report](../examples/report-print-pica-q.html), [independent
physical PDF oracle](../examples/report-print-pica-q-oracle.html), and
[exact scope](css-stylesheets.md#physical-pica-and-quarter-millimetre-css-print-units-source-only).

No new CSS property names, full CSS Values/Paged Media, `calc`, CLI
paper-size/`--margin` pc/q flags or distributable preview are implied.
Source commit `ca3aef91e20792782edb49004369dfbf1e9712b3`; the retained candidate has an older SHA.

### Root-relative `rem` and element-relative `em` box lengths (source-only)

For source-built business report boxes, `width:13rem`, `max-width:14rem`,
`min-width:10rem`, and `padding:0.5rem` now resolve against the
computed `html` root font size, or `PdfOptions.FontSize` if there is no
`html` element. The resulting point lengths drive actual box/content widths,
text wrap, centring, borders/backgrounds and page continuation. `rem`
is limited to ordinary blocks, paragraphs and headings for these properties;
image/table/column lengths and border `rem` still reject; bounded text-indent/letter-spacing/word-spacing and paragraph/heading margins accept `rem`.
Unsupported values and unsafe geometry preserve previous output bytes.
The separate **element-relative `em` layout** is also available in source-built controlled reports:
`width:12em`, `min-width:8em`, `max-width:14em`, `padding:0.25em` and
paragraph/heading `margin:0.2em 0.5em` resolve against the **element's own
computed font-size after cascade**, unlike `rem` (the `html` root).
They change real PDF line wrapping, painted boxes and page breaks; negative
or overwide geometry rejects before replacing output. Inline/table/image/col
box lengths, table-cell `em` margins, border/text-spacing `em`, `calc()` and
general container margins remain unsupported. The
[32-entry `em` report](../examples/report-em-layout.html) has a separately authored
[point-value oracle](../examples/report-em-layout-oracle.html) and targeted tests;
this is not part of the retained installable candidate.
See the [root-relative box contract](css-stylesheets.md#current-source-root-relative-report-box-lengths)
and [69-record report](../examples/report-rem-box-lengths.html) with its
[independent absolute-point oracle](../examples/report-rem-box-lengths-oracle.html).
This feature is source-only and not part of the retained installable preview.

### Root-relative paragraph and heading margins (source-only)

Scoped report paragraphs/headings also accept `margin:0.25rem 0.5rem`,
`margin-top:0.5rem` and 1–4 side shorthand values. The computed `html`
root (or `PdfOptions.FontSize` fallback) drives real PDF vertical gaps,
horizontal text/paint position, wrapping and pagination. Horizontal `auto`
still combines with supported lengths. Negative margins, vertical `%`/`auto`,
`calc()`, body/list/table/image/inline margins and table-cell `rem`/`em` margins
remain rejected; `div`/`section`/`article` panels have a separate bounded exception. See [the scoped margin contract](css-stylesheets.md#current-source-root-relative-paragraph-margins),
the [32-entry report](../examples/report-rem-margins.html) and its separately
authored [fixed-point oracle](../examples/report-rem-margins-oracle.html).
Unfit output preserves the prior destination. This is source-only.

### Current-source logical report spacing (LTR only)

Controlled left-to-right business reports can now use
`margin-inline`, `margin-block`, `padding-inline` and
`padding-block` with one or two values, as alternatives to the corresponding
physical longhands. For example, `margin-inline:auto` centres an explicitly
sized panel, while `padding-inline:8pt 12pt` adds measurable left/right
insets and `margin-block:6pt 9pt` creates real vertical gaps. The same ordinary report boxes also support **eight individual LTR logical
side longhands**: `margin-inline-start`/`margin-inline-end`,
`margin-block-start`/`margin-block-end`,
`padding-inline-start`/`padding-inline-end`, and
`padding-block-start`/`padding-block-end`. For example,
`margin-inline-start:auto;margin-inline-end:auto` centres a fixed-width
report card; `padding-inline-start:6pt` changes the actual first glyph X;
and `margin-block-end:8pt` adds real space below a paragraph or card.
Each longhand takes **exactly one** value and participates independently in
normal physical-side cascade, `!important`, `inherit/initial/unset`,
print and font-relative lengths, box painting, page continuation and strict
failure restoration. A 40-reference paginated report has a separately
written physical-side [PDF oracle](../examples/report-logical-longhands-oracle.html)
for the [logical-side example](../examples/report-logical-longhands.html).

This is **LTR normal-flow mapping only**: inline-start = left, inline-end =
right, block-start = top, block-end = bottom. `direction`, RTL/vertical writing
modes, negative values, CSS margin collapse, other logical properties,
`calc()` and unsupported elements remain outside scope. Body/list/table/
image/inline margins remain rejected, as with their physical longhands.
See the [CSS contract](css-stylesheets.md#current-source-ltr-logical-report-spacing)
and [real report + physical-longhand oracle](../examples/report-logical-spacing.html).
This source change does not update the historical private binary candidate.

## Report panels and box sizing

### Centred panels and percentage panel margins (source-only)

Controlled `div`, `section` and `article` report panels can now be
centred using bounded horizontal `margin:0 auto`, including constrained
`max-width`, nested decorated boxes and page fragments. Report panels also accept bounded nonnegative vertical percentage margins
relative to their containing content width; ordinary paragraphs/headings
retain their separate nonpercentage vertical-margin contract. The [50-entry percentage-margin report](../examples/report-percentage-panel-margins.html)
and [independent point-length oracle](../examples/report-percentage-panel-margins-oracle.html)
verify actual nested PDF panel spacing, text, paint and continuation pages.
Paragraph/heading percentages and direct inheritance of a percentage panel
margin into these text elements fail explicitly. This source change
(`0518b51d7a34457798c748db6753c4270ce20996`) does not relabel previously retained software or authorise
customer release. See the
[panel-margin CSS scope](css-stylesheets.md#current-source-centred-report-panels),
[50-entry example](../examples/report-panel-margins.html) and
[independent point oracle](../examples/report-panel-margins-oracle.html).
This is source-only functionality, not a new installable candidate or
public release.

### Vertical margins on report panels (source-only)

For printed business reports, `div`, `section` and `article` now
support nonnegative top/bottom margins alongside horizontal `margin:0 auto`.
For example, `margin:12pt auto 7pt` centres a painted card and leaves
12pt above and 7pt below its **outside** border, including nested/multipage
PDF content without CSS margin collapsing.
See the [50-entry sample](../examples/report-panel-vertical-margins.html),
[independent fixed-spacer oracle](../examples/report-panel-vertical-margins-oracle.html)
and [bounded CSS contract](css-stylesheets.md#current-source-real-vertical-report-panel-margins).
Body, lists, tables, images and inline margins remain unsupported.
No new installable candidate or human PDF approval is implied.

### `min-width` / `max-width` constraints (source-only)

For source-built business reports, `min-width` and `max-width` now constrain
real used block widths, including `width:auto` panels and horizontal auto
margins. For example, `max-width:146mm` caps a report panel, while
`min-width:82mm;max-width:90mm` bounds an invoice-reference box.
`box-sizing:border-box` applies constraints to the padded/bordered outer box;
the default `content-box` constrains the content width instead. Supported
lengths are nonnegative `pt/px/mm/cm/in/%` or zero; `min-width:auto`
and `max-width:none` remove the constraint. Actual over-constrained geometry
is rejected without replacing prior output. This is only ordinary report
blocks/paragraphs/headings; table and inline sizing stay separate; image constraints use their own bounded, aspect-ratio-preserving layout.
See [CSS scope](css-stylesheets.md#current-source-bounded-report-minmax-widths)
and the [runnable report](../examples/report-width-constraints.html).

### Fixed-height report cards (source-only)

Invoices, inspection reports and status summaries can now use `height:85pt`
on ordinary `div/section/article/blockquote` panels outside tables and lists.
Unlike a syntax-only declaration, it reserves **actual PDF panel height**,
paints the background and borders through that height and moves following
text accordingly. `box-sizing:content-box` adds vertical padding/borders;
`border-box` includes them. CSS cascade, computed `em/rem` and the
independent `min-height` lower bound apply.

The [40-card report](../examples/report-fixed-height-cards.html) and its
[minimum-height physical PDF oracle](../examples/report-fixed-height-cards-oracle.html)
exercise actual Native AOT PDF text, page geometry and protected failures.
Fixed cards must fit on one page **with all their content**: overflow
explicitly fails `FPDF1302` instead of silently clipping or overlapping
the next section. No percentages, `calc()`, `max-height`, list/table/inline
targets or general CSS Sizing. This is current source only at
`9f141fbc33bb2c4a3764b12c6c8c52b8c2bd00bc`, not the older private
preview, an approved reference page or a customer grant.
[Exact CSS contract](css-stylesheets.md#current-source-fixed-height-report-panels).

### Minimum-height inspection cards (source-only)

A controlled business report panel can reserve a visible minimum area using
`min-height:90pt` (also `px/mm/cm/in`, scoped `em/rem` and exact zero).
`div`, `section`, `article` and `blockquote` outside lists and tables
paint their backgrounds/borders over the **actual** minimum height; later text
starts after that box. `box-sizing:border-box` counts vertical padding and
borders inside the specified minimum, while the default `content-box` adds
them. CSS cascade and existing pagination/resource checks apply, and invalid
or unplaceable input preserves the original PDF.

See [the bounded CSS and failure contract](css-stylesheets.md#current-source-minimum-height-report-panels),
[a ready-to-run inspection example](../examples/report-min-height-cards.html)
and [the native PDF / physical spacer oracle](../examples/report-min-height-cards-native.html).
This is not general CSS `height`/`max-height`, percentages, tables, lists,
flex/grid, arbitrary sizing, a new released binary or customer authorization.
A separate, strictly one-page fixed-`height` panel subset is documented above.

## Images

### Bounded image height limits for report logos and photos (source-only)

Explicitly bound JPEG/PNG images now accept nonnegative physical
`max-height` and `min-height` in `pt/px/mm/cm/in`, or `max-height:none`
and `min-height:initial` to remove a constraint. With `height:auto`,
the **actual PDF image matrix** preserves native pixel aspect ratio when a
vertical limit changes height; with an explicit physical `height`, the
width axis remains independently authored. `min-height` wins contradictory
`max-height`, and `box-sizing:border-box` includes vertical
padding/borders exactly once. Applicable to standalone images and direct
fixed-column table-cell items; no network resources, percentage/em/rem
height limits, `calc`, `object-fit`, arbitrary aspect-ratio fitting
or silently violated horizontal constraints. Impossible dimensions
fail `FPDF1201/1202/1302` while preserving existing PDFs.

The [36-record height-constrained report](../examples/report-image-height-limits.html)
and [separately authored physical matrix control](../examples/report-image-height-limits-oracle.html)
exercise multiple PDF pages, searchable text, PNG resources, Linux/Windows
Native AOT, qpdf/Poppler geometry, and failure recovery. This is a current
**source** capability under review; it is not a rebuilt private candidate,
human-approved page or customer licence.
[Exact boundary](images.md#bounded-image-height-limits).

### Explicit physical image heights for logos and report photos (source-only)

Explicitly bound JPEG/PNG images now support `height:36pt` for standalone
images and direct fixed-column `td/th` image items. With automatic width,
the used PDF image width follows the source image's intrinsic pixel ratio.
Specifying both width and height independently controls the PDF image matrix.
`box-sizing:border-box` deducts vertical padding and borders once, and
existing min/max-width only constrains the width axis when height is fixed.

The [36-record report](../examples/report-image-heights.html) and
[independently authored physical-width oracle](../examples/report-image-heights-oracle.html)
are exercised through NUnit and Linux/Windows Native AOT/qpdf/Poppler PDF
geometry, searchable text and recovery gates. No percentages, image em/rem,
object-fit, implicit auto-shrink or network loading. Unsupported or unplaceable
dimensions fail without replacing a previous PDF. This is current **source**
at `7afae6b56ba8ddbaad2f434cfb4a9d35f8b7a2dd`, not a rebuilt private preview, human approval or customer grant.
[Precise image contract](images.md#explicit-physical-image-heights).

### Bounded image sizing for report logos and evidence photos (source-only)

Explicitly bound JPEG/PNG images can now use `max-width:100%` and
`min-width` (bounded nonnegative print lengths/percentages), with
`box-sizing:border-box` for image padding and borders. Actual PDF image
matrices scale to the used **content** width while preserving the source
aspect ratio; direct images in fixed-column cells use the true cell width,
including `colspan`. The existing CSS cascade and explicit
`inherit/initial/unset` apply. No unrequested automatic shrinking,
`height`, `object-fit`, network fetch, `calc()`, image `em/rem`,
transforms, or fractional page splits. Unplaceable images fail before
replacing an existing output.

The [36-record inspection report](../examples/report-responsive-images.html),
[independently authored fixed-width control](../examples/report-responsive-images-oracle.html),
NUnit image/row tests and Linux/Windows Native AOT/qpdf/Poppler gate verify
real PDF bytes, searchability, pagination and protected error recovery.
[Precise image contract](images.md#responsive-standalone-and-cell-image-widths).
Engine source `b6bebe8e3b97f13190148c6f53bed49ec2678ede`;
new private software, human reference approval, CI and customer rights are separate.

## Tables and lists

### Table cell `vertical-align` (source-only)

For business tables with multiline descriptions, `td`/`th` now accept `vertical-align:top`, `middle` or `bottom` to position a shorter cell's text or image within the complete painted row. Whole rows and intact repeated headers work across pages. Rows needing independent cell-by-cell continuation reject `middle`/`bottom` instead of pretending to align; inline baseline alignment, `rowspan` and general CSS table layout remain unsupported. See the [40-row report](../examples/report-table-vertical-align.html), [physical-padding oracle](../examples/report-table-vertical-align-oracle.html) and [table contract](tables.md#vertical-alignment-in-whole-table-rows).

### Current-source selectable report-list markers

Business reports may set `list-style-type` on an existing `ol`, `ul`
or individual `li` to render actual numbered or unnumbered PDF lists.
For example:

```html
<style>
  ol.inspections { list-style-type: lower-alpha; }
  ol.actions { list-style-type: upper-roman; }
</style>
<ol class="inspections"><li>Inspect equipment</li><li>Record outcome</li></ol>
<ol class="actions"><li>Assign owner</li><li>Schedule follow-up</li></ol>
```

Supported kinds: `disc`, `circle`, `square`, `decimal`, `lower-alpha`,
`upper-alpha`, `lower-roman`, `upper-roman`, `none`. Ordered lists normally remain
decimal and unordered lists remain vector discs, preserving old PDF bytes.
`circle` paints a transparent-centre stroked ring; `square` paints a filled
vector square in the item colour. Both honour the measured list gutter,
first-line marker placement, page continuation and display-command limits.
Alphabetic labels progress `a`…`z`…`aa` and Roman labels use standard
subtractive notation through 3999. Text markers use caller-supplied fonts,
measured per-list gutters, and one marker on the first line even across
pages. `none` removes the marker, not the item's text; it retains the
existing list layout indentation. CSS cascade, `!important`, and
`inherit/initial/unset` apply; nested lists retain their own defaults
unless explicitly overridden. Unsupported marker kinds, lists over two
levels, placement on non-list elements and unavailable marker glyphs fail
explicitly, preserving the existing PDF destination.

[74-entry paginated facilities report](../examples/report-list-marker-styles.html),
[80-entry circle/square multi-page report](../examples/report-list-vector-shapes.html),
[bounded CSS contract](css-stylesheets.md#current-source-bounded-list-marker-types),
[full list mechanics](lists.md). This is a source-only capability:
**no preview binary, NuGet publication, commercial grant or human PDF
approval** follows from this change.

### Current-source report list continuation and numbering

Ordered business report actions can continue from `<ol start="21">`,
count down via `<ol reversed>`, or reset an item with `<li value="43">`
inside `ol`. A one-token shorthand such as `list-style:upper-roman`
or `list-style:square` uses the same real PDF text/vector marker,
CSS cascade, font measurement and pagination as `list-style-type`.
Unstyled lists retain byte-identical PDF output.

This is a **bounded subset**: only ASCII positive ordinals 1–999999,
only direct `ol > li` value overrides, and one existing marker kind or
CSS-wide keyword per `list-style`. No arbitrary CSS Lists semantics,
inside/position/images/counters, HTML `type`, negative ordinals or
list-item `value` on `ul`; invalid inputs fail without overwriting
a destination. See the
[77-entry pagination example](../examples/report-list-continuations.html)
and [exact list contract](lists.md#current-source-ordered-list-sequencing-and-one-token-shorthand).
This is current source, **not** a new retained binary, public NuGet or
customer distribution grant.

## Report elements

### Clickable HTTPS and mailto report references (source-only)

Controlled reports may place `<a href="https://example.com/order/42">Track the
order</a>` or `<a href="mailto:sales@example.com">Email sales</a>` inside
existing inline text contexts, including flat table cells. FactsPDF writes
actual PDF `/Link` URI annotations on the already laid-out glyph runs, with
page-local rectangles following wrapping and pagination; the text remains
selectable/searchable. By default anchor text is blue and underlined; scoped
CSS colour, font and `text-decoration` rules (including `!important`) work
through the existing cascade. Each linked run counts toward the display
command limit. No network requests are made while generating the PDF.

A valid `href` is required and limited to an absolute ASCII HTTPS URI with a
host and no embedded credentials, or a single plain unquoted ASCII `mailto:` mailbox with dot-separated
DNS-style labels (not a quoted mailbox, address literal, path or port), without
query parameters. URL length is limited to 2048 characters, percent escapes
must be well formed. An invalid/relative/HTTP/file/JavaScript URL or missing
`href` fails `FPDF1103` without replacing an existing PDF. Nested anchors,
block descendants and general DOM navigation fail; clickable linked images,
`target`, `download`, fragments, local-page destinations and full HTML anchor
semantics are not implemented. Links are external PDF actions, not embedded
content or a claim that target addresses are trusted.

[Business order sample](../examples/report-clickable-orders.html),
[real PDF/CLI/TrueType/NUnit cases](../tests/FactsPDF.Tests/ReportHyperlinksTests.cs)
and [Linux/Windows Native AOT verifier](../scripts/verify_stylesheets.py).
Implementation source `9a19096a69f323bda3c7678c590a96b19ad98864`;
exact-head checks, independent review, human approval, private software and
commercial distribution rights remain separate.


### Current-source business-report quotations

The standard `<blockquote>…</blockquote>` element now creates a **real**
inset PDF report panel, with default outside margins of **8pt vertically
and 24pt horizontally**. Quoted paragraphs and inline emphasis inherit the
existing font styles, while measured text, optional solid borders, backgrounds,
padding and page continuation use the same renderer as `div` report panels.
For example:

```html
<blockquote style="border-left:2pt solid #28557a;padding:7pt">
  <p>Inspection evidence: all nominated checks were completed.</p>
</blockquote>
```

This is a bounded quote **layout** feature, not citation metadata, Tagged PDF
or browser-complete `blockquote` semantics. `cite` attributes, nesting inside
lists/tables/inline elements, negative vertical margins (and percentage vertical margins on paragraphs),
margin collapse, CSS `height`, arbitrary/per-corner rounded geometry and arbitrary HTML are
unsupported and explicitly rejected. The quote is source-only: it does not
update the retained preview binaries or grant customer distribution rights.
[Real 44-item quote report](../examples/report-blockquotes.html),
[independently authored div oracle](../examples/report-blockquotes-oracle.html) and
[exact CSS/HTML contract](css-stylesheets.md#current-source-bounded-quoted-report-panels).

### Current-source horizontal report dividers

A standalone HTML `<hr>` now paints a real **1pt current-text-colour rule**
with 8pt non-collapsing outside spacing above and below. `<hr />` is
equivalent. Templates may customise the separator through the existing
bounded `width`, `min-width`, `max-width`, `margin` (nonnegative
vertical lengths and horizontal `auto`), `padding`, `box-sizing`,
`color`, `background` and solid `border` declarations. The rule
follows its containing width and moves intact to the next page when needed.
`font-size` affects only supported rule-local `em` box lengths.

This is a **void block separator**, not unrestricted HTML or CSS: no
content, table/paragraph/list/inline nesting, arbitrary `height`,
pseudo-elements, arbitrary/patterned rounded borders, margin collapse or resource URLs.
Unsupported declarations fail explicitly and preserve an existing output.
See the [scoped CSS contract](css-stylesheets.md#current-source-html-horizontal-report-rules),
[44-entry report](../examples/report-horizontal-rules.html), and the
[independently authored painted-box oracle](../examples/report-horizontal-rules-oracle.html).
This source feature is not a public NuGet/CLI release or a new private binary.

### Current-source print-only report sections (bounded display)

Use `display:none` to omit a draft, internal note, or unused ordinary
report block from the generated PDF **without leaving blank space**;
`display:block` preserves the ordinary block layout. Embedded stylesheets,
inline rules, specificity, source order and `!important` participate in the
same CSS cascade.

```html
<style>.internal-only { display: none; }</style>
<section class="internal-only"><p>Draft for internal approval</p></section>
<section><p>Customer-facing report</p></section>
```

Supported targets are `p`/`h1`–`h6`, `div`/`section`/`article`
and `blockquote` **outside lists/tables**. A hidden block and its complete
validated subtree never reach PDF glyph layout, margin/padding geometry,
pagination or painted backgrounds/borders. Invalid HTML/CSS, absent resources
and input budgets are **still checked inside hidden markup**; this is not
a validator bypass. The HTML `hidden` attribute, CSS-wide display keywords,
inline/flex/grid layout, image/`hr`/table/list suppression, JavaScript and
media-query display switches are not supported.

See [exact source contract](css-stylesheets.md#current-source-bounded-report-display-suppression),
[40-entry example](../examples/report-display-visibility.html), and
[visible-only PDF oracle](../examples/report-display-visibility-oracle.html).
Source implementation is not a published NuGet binary or customer licence.

## Pages, pagination and document properties

### A4/Letter and landscape pages from the CLI (source-only)

The source CLI can also render genuinely different physical paper sizes and
orientations. A4 portrait (595.28 × 841.89 PDF points) remains the default,
so existing commands keep exactly their previous page geometry:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html wide-report.pdf --page-size a4 --landscape
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html us-letter.pdf --page-size letter
```

`--page-size` accepts `a4` or `letter` (case-insensitive); `--landscape`
swaps the physical width and height for either size. It updates the actual
PDF MediaBox, text wrapping, page breaks and page furniture coordinates; it
does **not** scale an already-rendered page or implement CSS `@page`.
The `.NET` API continues to use `PdfOptions.PageWidth/PageHeight` for
explicit geometry. Source implementation began at `bb34436538b8a02f1ace0878be1f97eb5bbff77d`;
this is not part of the older retained private preview or a public binary.

### Print on custom physical paper (source-only)

Reports that need A5, US Legal, stationery or other defined stock can supply
**both** `--page-width` and `--page-height` in `pt/px/mm/cm/in`.
The CLI passes the actual point dimensions to the existing PDF renderer before
text wrapping, image positioning, pagination and page furniture are computed:

```sh
dotnet run --project src/FactsPDF.Cli -- examples/first-document.html a5-report.pdf --page-width 148mm --page-height 210mm --margin 12mm
```

Each physical dimension must be finite, 72–14400pt (1–200 inches) and fit the
uniform selected margin. Supply the pair together, without `--page-size` or
`--landscape`; to rotate custom paper, swap the two dimensions explicitly.
Missing, conflicting, repeated or out-of-range arguments exit 2, without
overwriting an existing customer PDF. Existing A4 portrait defaults, Letter,
landscape and `--margin` behaviour are unchanged. This is not CSS `@page`,
printer bleed or paper auto-detection. See `CliCustomPaperTests.cs` and the
Linux/Windows Native AOT Poppler/qpdf checks in
`scripts/verify_native_feature_paths.py`. Introduced in source
`271d05350f363857cf2a6d44f1ea40e876f8573c`; not in the previously
retained private preview or any public binary.

### Choose a physical report margin in the CLI (source-only)

The CLI accepts `--margin 15mm` (or `0`, `pt`, `px`, `cm`, `in`) alongside
`--page-size a4|letter` and `--landscape`. It sets **all four physical page
margins** before actual PDF text wrapping, pagination, images and page
furniture are laid out. The default remains 36pt on every side, preserving
the existing PDF bytes when no new flag is provided. For example:

```sh
dotnet run --project src/FactsPDF.Cli -- examples/first-document.html report.pdf --page-size a4 --margin 15mm
```

Lengths are invariant-culture, nonnegative and strictly less than half the
shorter selected page dimension. Unsupported, repeated or impossible
arguments fail with exit code 2; file output uses the existing atomic
replacement guard. This is not CSS `@page`, different margins per side,
printer bleed, an automatic paper fit or a released CLI. See
`CliPageMarginTests.cs` and the real Linux/Windows Native AOT
`verify_native_feature_paths.py` Poppler/qpdf geometry and text gates.

### Optional searchable PDF document properties (source-only)

For invoices and inspection reports, set real PDF document information using
`--title`, `--author`, `--subject` and `--keywords`, or the matching
public `PdfOptions` properties. This is **PDF Info metadata**, not text
printed on the page, extracted from HTML or a CSS feature:

```sh
dotnet run --project src/FactsPDF.Cli -- examples/first-document.html report.pdf --title "Facilities inspection — 澳新" --author "Koala Facts" --subject "Quarterly status" --keywords "inspection, report"
```

Each optional field is 1–1024 UTF-16 units, nonblank, single-line and
well-formed Unicode. The PDF stores each field with a UTF-16BE BOM,
including Chinese and supplementary characters. Without any fields, the
legacy PDF is byte-identical; no timestamps or machine identity are added.
Missing, repeated or malformed CLI fields fail with exit code 2 and
preserve an existing file. **PDF recipients can view these properties:
never put confidential data or credentials in them.**

Source NUnit verifies exact API/CLI PDF bytes, the two serialization
paths, PNG, text pages, bounded errors and recovery; Linux/Windows Native
AOT checks additionally use Poppler `pdfinfo`, qpdf and original body
geometry. Not XMP, HTML `<title>` inference, PDF/UA tagging, an updated
private candidate, a public release or customer rights.

### Print-ready report section page boundaries (source-only)

Use `break-before:page` and `break-after:page` on normal
`div/section/article/blockquote` report panels outside tables/lists.
Legacy print templates may use `page-break-before:always` and
`page-break-after:always` (also `auto`) as equivalent aliases on the same
scoped panels and explicit paragraphs/headings. Real PDF pages and painted
panel fragments move together, including nesting, first-page handling,
`!important` and explicit CSS-wide reset/inheritance.

The [three-page facilities report](../examples/report-section-page-breaks.html)
and [independently authored modern-break control](../examples/report-section-page-breaks-oracle.html)
are checked as actual Native AOT PDFs, not accepted syntax alone.
`break-after:avoid` remains **heading-only**, not a section keep-together;
left/right, RTL, tables/lists and general CSS Paged Media are unsupported.
A separately documented top-level flat-card `break-inside:avoid` subset
does not change these forced-boundary semantics. This is
source-only at `67566764419232585632d8ea6f2106aeeeff2f70`, pending
exact-head CI, independent review and the separate private/customer gates.
[Precise CSS scope](css-stylesheets.md#current-source-print-section-page-boundaries).

### Keep multi-paragraph sign-off cards together (source-only)

Use `break-inside:avoid` (or legacy `page-break-inside:avoid`)
on a **top-level** `div/section/article/blockquote` containing one to eight
direct, nonempty `p/pre/h1–h6` children. FactsPDF measures actual glyph wraps,
padding, borders and text margins **before** PDF placement; if a card does not
fit at the current page end, its whole painted box and text move to the next
page. A card taller than one usable page rejects with `FPDF1302`, retaining
the previous PDF. `auto`, `inherit/initial/unset`, `!important`
and the existing cascade apply. No new page for a card that fits.

This is **not** general CSS Fragmentation: nested cards, images/tables/lists,
explicit inner page breaks, fixed/minimum-height cards, more than eight direct
children, arbitrary children, left/right pages, or widows/orphans are not
implemented. Inapplicable structures reject `FPDF1201`; unsupported values
reject `FPDF1202`. [Exact CSS contract](css-stylesheets.md#current-source-keep-together-business-report-cards),
[printable card](../examples/report-keep-together.html),
[independently authored forced-page control](../examples/report-keep-together-oracle.html),
`ReportKeepTogetherTests.cs`, and Linux/Windows Native AOT/qpdf/Poppler
witness are provided for review. Source first implemented at
`ecdef6d8745caa3d2b527197c5767e25f89ba6b0`; new CI/review,
private software, human PDF approval and customer rights are separate.
