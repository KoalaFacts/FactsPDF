### Fixed-column cell text-flow declarations (source candidate)

Ordinary `td/th` in explicit-column reports now consume `white-space`, `overflow-wrap` and `word-break` directly, inherited by anonymous text and flat `p` children. Supported values control measured PDF text wrapping, overflow errors, repeat headers and page splits. [Multi-page report](../examples/report-cell-text-flow.html), independent [explicit-p oracle](../examples/report-cell-text-flow-oracle.html), [NUnit tests](../tests/FactsPDF.Tests/TableCellTextFlowTests.cs), source [19c6e35eb2987aa84fc7b5ff16b2ccad57630c60](https://github.com/KoalaFacts/FactsPDF/commit/19c6e35eb2987aa84fc7b5ff16b2ccad57630c60). No general table layout, list/inline declarations or unsupported modes; all three capabilities stay partial pending gates.

### Fixed-track cell first-line indents (source candidate)

The business-register row notes can now use `td.memo { text-indent:0.25em }` (with a computed cell font size) and a nested `p { text-indent:inherit }` or an overridden `0.5rem`. Instead of merely parsing the declarations, the computed first-line offset enters actual PDF text measurement and pagination. A 24-row multi-page synthetic source and independently written point-value oracle, real byte and coordinate comparisons and prior-output-protecting errors document the bounded feature: [example](../examples/report-cell-text-indent.html), [oracle](../examples/report-cell-text-indent-oracle.html), [tests](../tests/FactsPDF.Tests/TableCellTextIndentTests.cs). Code source [7282c0839235443ab49935cb0f2e8092117786a1](https://github.com/KoalaFacts/FactsPDF/commit/7282c0839235443ab49935cb0f2e8092117786a1). Existing `text-indent` compatibility entry remains **partial**; structural table elements, inline spans, negative/calc and unlimited indent are not supported.

## Attribute selectors on actual invoice/report fields

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
malformed/whitespace-split operators, empty non-equality operands, pseudos
other than `:root`, sibling combinators and nested selectors fail explicitly with FPDF1203;
no network access or full CSS Selectors claim. This remains a **partial**
selector capability. [The 44-entry operator invoice example](../examples/report-attribute-operators.html)
and [independent inline control](../examples/report-attribute-operators-oracle.html)
are compared in [real PDF, cascade, image, TrueType and fail-safe tests](../tests/FactsPDF.Tests/AttributeOperatorReportTests.cs).
Initial presence/equality implementation: `d7fee5d311f9abebccf37a58c3c9198de78750e8`;
operator-family engine source: `845ab1799149e8b833306f2e49b3f8b240d699c8`. CI, independent review
and the actual merged main are separate evidence.


### Computed table-track widths for invoice templates (source implementation)

Fixed-track `table` and `col` `width` values accept bounded `em`/`rem` without changing the explicit-column contract. Table `em` resolves against the table's final computed `font-size`; column `em` resolves against the inherited `colgroup` font size; `rem` uses `html`. Table padding accepts the same units. Real column widths, cell wrapping, backgrounds, borders, repeated headers and page breaks follow the measured values. [The multi-page invoice](../examples/report-table-relative-tracks.html) and its [point-based oracle](../examples/report-table-relative-tracks-oracle.html) must generate identical PDF bytes.

Only explicitly enumerated fixed columns (1–16) are supported. Column widths must still sum to the measured table content width within 0.001pt. No auto-width, rowspan, relative row/group box lengths, independent cell widths, `calc()`, negative lengths or unsupported column decorations are added. Invalid geometry fails before altering the destination stream. This expands existing partial `width` and table box behaviour, not full CSS Tables.

## Last-line alignment for quotation text (source implementation)

`text-align-last:center` can centre the final line of a wrapped quote,
without centring its earlier lines; `text-align-last:justify` extends
retained spaces on final/explicit-break lines. This also paints real flat
table-cell text at computed PDF X positions. Default `auto` keeps prior
bytes. The [44-row report](../examples/report-last-line-alignment.html)
matches an [independent explicit-break control](../examples/report-last-line-alignment-oracle.html)
in [source PDF/geometry/error tests](../tests/FactsPDF.Tests/LastLineAlignmentReportTests.cs).
See the [bounded CSS contract](css-stylesheets.md#bounded-pdf-last-line-alignment-source-implementation).
Source `09a186b4e6f33355db3014ef0b0fcd61df224bc3`; independent review and current-head checks are separate.

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
- [.NET SDK](#net-sdk)
- [Diagnostics](#diagnostics)

## Stylesheets

### Initial-value reset declarations

Stylesheets copied from report templates often reset layout properties the engine does not implement. `position:static`, `float:none`, `clear:none`, `overflow`/`overflow-x`/`overflow-y:visible`, `visibility:visible`, `z-index:auto`, `opacity:1` (or `100%`) and `box-shadow`/`text-shadow`/`transform`/`filter`/`background-image`/`clip-path`/`backdrop-filter:none`, `mix-blend-mode:normal`, `isolation:auto`, `font-feature-settings` (also `-webkit-`/`-moz-`)/`font-variation-settings`/`font-variant-ligatures`/`font-variant-numeric`/`font-stretch:normal`, `font-optical-sizing:auto`, `font-size-adjust:none` (Tailwind preflight puts the first two on `html`), `font-variant` and its `-caps`/`-east-asian`/`-alternates`/`-position` longhands at `normal` (email and print template resets), `font-language-override`/`font-variant-emoji`/`font-palette:normal` and `text-emphasis`/`text-emphasis-style:none` (no language glyph forms, emoji switches, palettes or emphasis marks are applied), `direction:ltr`, `unicode-bidi:normal`, `writing-mode:horizontal-tb`, `text-orientation:mixed` and `text-combine-upright:none` (only left-to-right horizontal text is laid out; `direction:rtl`, `writing-mode:vertical-rl` and other values fail `FPDF1202`), `column-count`/`column-width`/`columns:auto`, `column-gap`/`row-gap`/`gap:normal`, `column-span`/`column-rule`/`column-rule-style:none` and `column-fill:balance` (every box is one column and no flex or grid container exists; `column-count:2` and `gap:1rem` fail `FPDF1202`), `text-overflow:clip`, `line-clamp`/`-webkit-line-clamp:none` and `box-decoration-break`/`-webkit-box-decoration-break:slice` (nothing clips overflow and split boxes already paint as slices; `ellipsis` and `clone` fail `FPDF1202`), `rotate`/`scale`/`translate`/`perspective:none`, `transform-style:flat` and `mask`/`mask-image`/`-webkit-mask`/`-webkit-mask-image`/`shape-outside:none` (nothing is transformed, masked or shaped; `rotate:45deg`, `preserve-3d` and `url()` fail `FPDF1202`), `hanging-punctuation:none`, `hyphenate-character`/`text-underline-position:auto` and `text-spacing-trim:normal` (nothing hangs, hyphenates automatically or trims CJK spacing, and underlines sit where the renderer puts them; `first`, `'-'`, `trim-start` and `under` fail `FPDF1202`), `background-attachment:scroll`, `background-blend-mode:normal`, `background-origin:padding-box`, `container-type:normal` and `container:none` (no background image is painted and no container query is evaluated; `fixed`, `multiply`, `content-box` and `inline-size` fail `FPDF1202`), `aspect-ratio:auto` and the mask longhands at their initial values (`mask-repeat:repeat`, `mask-size:auto`, `mask-origin`/`mask-clip:border-box`, `mask-mode:match-source`, `mask-composite:add`, `mask-type:luminance`; images keep their pixel ratio and no mask is applied, so `16 / 9`, `no-repeat`, `cover` and `alpha` fail `FPDF1202`), the animation timeline and view-transition resets (`animation-composition:replace`, `animation-timeline`/`view-timeline-inset:auto`, `animation-range`/`animation-range-start`/`animation-range-end:normal`, `scroll-timeline`/`scroll-timeline-name`/`view-timeline`/`view-timeline-name`/`timeline-scope`/`view-transition-name`/`view-transition-class:none`, `scroll-timeline-axis`/`view-timeline-axis:block`; no animation runs and the page is never scrolled or navigated, so `animation-timeline:scroll()`, named timelines and `view-transition-name:header` fail `FPDF1202`), the anchor positioning and top-layer resets (`anchor-name`/`anchor-scope`/`position-area`/`position-try`/`position-try-fallbacks`/`overlay:none`, `position-try-order:normal`; every box stays in normal flow and nothing reaches a top layer, so `anchor-name:--tip`, `position-area:top center`, `position-try:flip-block` and `overlay:auto` fail `FPDF1202`), the screen and UI resets (`interpolate-size:numeric-only`, `reading-flow:normal`, `field-sizing:fixed`, `dynamic-range-limit:no-limit`, `caret`/`caret-animation`/`caret-shape:auto`; nothing animates, no caret is drawn, form fields keep fixed sizing and colours stay standard range, so `interpolate-size:allow-keywords`, `field-sizing:content` and `caret-shape:block` fail `FPDF1202`), the text box and paint resets (`text-wrap-style`/`text-box-edge:auto`, `text-box-trim:none`, `text-box`/`paint-order`/`math-style`/`math-shift:normal`; lines break greedily, line boxes are never trimmed, glyphs carry no stroke and no MathML layout runs, so `text-wrap-style:balance`, `text-box-trim:trim-both` and `paint-order:stroke` fail `FPDF1202`), the intrinsic-size and transform-box resets (`contain-intrinsic-size` and its `-width`/`-height`/`-block-size`/`-inline-size` longhands at `none`, `transform-box:view-box`; size containment and transforms never apply, so `contain-intrinsic-size:auto 300px` and `transform-box:fill-box` fail `FPDF1202`), box offsets (`top`, `right`, `bottom`, `left`, `inset` and the logical insets) at `auto` and `border-image`/`border-image-source:none` (offsets never move a `position:static` box and no border image is painted; `top:0` or `border-image:url(a.png) 30` fail `FPDF1202`), `border-collapse:separate` and `border-spacing` at one or two zero lengths (table cells already paint separate border boxes that touch without spacing; `border-collapse:collapse` and `border-spacing:2pt` fail `FPDF1202`), `empty-cells:show` and `caption-side:top` (empty cells already paint and no `caption` element is accepted; `hide` and `bottom` fail `FPDF1202`), `offset-path:none`, `offset-rotate`/`offset-anchor:auto`, `initial-letter:normal` and `text-emphasis-color:currentcolor` (no box follows a motion path, no drop cap is drawn and no emphasis mark is painted; `offset-rotate:90deg`, `initial-letter:3` and `text-emphasis-color:red` fail `FPDF1202`), `zoom:normal`/`1`/`100%` (old clearfix rules), `contain:none`, `content-visibility:visible` and `line-break`/`text-justify`/`image-rendering:auto`, plus CSS-wide keywords, are now accepted on any element and change nothing: the PDF is byte-identical to the page without them. Any other value of these properties still fails with `FPDF1202`, also in unmatched rules; positioning, floats, clipping, hiding, stacking, transparency, shadows, transforms, filters, background images, OpenType features, variation axes, optical sizing, aspect adjustment, font width selection, zoom, containment, content skipping, alternative line breaking or justification and image interpolation control are not implemented ([property scope](css-stylesheets.md#property-scope), `InitialValueNoOpTests.cs`).

### Interaction-only declarations

Web stylesheets reused for reports often set `cursor:pointer`, `user-select:none`, `pointer-events:none` or `resize:none`. These only affect pointer interaction on a screen, so any single keyword value of the four properties (and CSS-wide keywords) is now accepted on any element and dropped: the PDF is byte-identical to the page without them. `-webkit-`/`-moz-`/`-ms-user-select` are aliases of `user-select`, and `-webkit-user-drag`, `-webkit-touch-callout` and `-ms-overflow-style` keywords are dropped the same way, as is `speak` (`speak:none`/`never` in icon-font stylesheets), which only steers aural rendering. `font-synthesis:none` (and its `-weight`, `-style`, `-small-caps` and `-position` longhands at `none`) and `text-decoration-skip-ink` at `auto` or `none` (Vite's starter `:root` and Bootstrap's `abbr` reset write them) are dropped too, since the renderer synthesizes no face and skips no ink; `font-synthesis:weight` and `text-decoration-skip-ink:all` fail. `font-kerning` at `auto` or `none` is dropped as well, since no glyph pair is ever kerned, and `quotes` at `auto`, `none`, `match-parent` or pairs of strings, since no `q` element or quote content is accepted, so no quotation mark is inserted; `font-kerning:normal` asks for kerning and fails. The legacy `text-decoration-skip` and `-webkit-text-decoration-skip` (`objects` on links in normalize.css 7 and Bootstrap 4) accept `none`, `auto` or `objects` the same way, since no atomic inline sits in a line; `ink` or `spaces` fail. Cursor images, unknown keywords and other prefixes still fail ([property scope](css-stylesheets.md#property-scope), `InteractionOnlyCssTests.cs`, `ScreenGestureHintTests.cs`).

Almost every copied stylesheet sets `font-family` (`body{font-family:"Helvetica Neue",Arial,sans-serif}`, normalize.css `pre{font-family:monospace,monospace}`). FactsPDF never discovers or matches fonts: the fonts you supply (or the standard Courier fallback) are the only installed fonts, so a well-formed family list is validated and dropped, and the text falls back to that chain exactly as a browser falls back when none of the named families is installed. The PDF is byte-identical to the page without the declaration; `monospace` uses the same chain. Malformed lists fail `FPDF1202` ([property scope](css-stylesheets.md#property-scope), `InteractionOnlyCssTests.cs`).

Print stylesheets almost always add `-webkit-print-color-adjust:exact` (or `print-color-adjust:exact`) so browsers keep backgrounds when printing. FactsPDF always paints colours and backgrounds exactly as declared, so both names (and the deprecated `color-adjust`) accept `exact` or `economy` and drop the declaration; the PDF is byte-identical to the page without it (`PrintColorAdjustTests.cs`). `economy` only permits a renderer to drop backgrounds or lighten colours to save ink; FactsPDF never does, so `economy` keeps every background, exactly as `exact` does. Print resets often add the non-standard `background-color-adjust:exact` next to them (`*{print-color-adjust:exact !important;color-adjust:exact !important;background-color-adjust:exact !important}`); it is a further alias with the same values and errors, while `-webkit-background-color-adjust` still fails `FPDF1201`. Tailwind preflight and modern-normalize put `-moz-tab-size:4;tab-size:4` on `html`; no tab stop reaches a page (collapsed tabs become spaces, preserved tabs fail `FPDF1305`), so `tab-size` and `-moz-tab-size` accept a non-negative number or length and are dropped, while other values fail `FPDF1202` (`TabSizeTests.cs`).

CSS resets and site stylesheets commonly set `-webkit-font-smoothing:antialiased`, `-moz-osx-font-smoothing:grayscale` and `text-rendering:optimizeLegibility`. These only steer how a screen rasterises glyphs, which a PDF leaves to its viewer, so any keyword value of the three is accepted on any element and dropped; the PDF is byte-identical to the page without them. Other values fail `FPDF1202` (`FontSmoothingHintTests.cs`).

normalize.css and most resets start with `html{-webkit-text-size-adjust:100%}`. It only stops mobile browsers from inflating text, which never happens to a PDF page, so `text-size-adjust` and its `-webkit-` spelling (and the `-ms-`/`-moz-` spellings normalize.css v3/v4 adds) accept `auto`, `none` or a non-negative percentage and drop the declaration; the PDF is byte-identical to the page without it (`TextSizeAdjustTests.cs`).

Resets also clear native control styling with `-webkit-appearance:none` / `appearance:none` on buttons and inputs. The renderer draws no native widgets, so `appearance` and its `-webkit-`/`-moz-` spellings accept any CSS UI 4 keyword and drop the declaration; the PDF is byte-identical to the page without it (`AppearanceTests.cs`).

Animation-ready and mobile stylesheets add `backface-visibility:hidden` (often `-webkit-` prefixed) and `touch-action:manipulation`. A back face only shows under a 3D transform, which the renderer never applies, and touch gestures never reach a PDF, so both accept a single keyword and drop the declaration; combined `touch-action` values still fail `FPDF1202` (`BackfaceTouchActionTests.cs`).

The same stylesheets add `will-change:transform` (or a list such as `transform, opacity`) as a compositor hint. A written PDF page never animates, so `will-change` accepts `auto` or a comma-separated list of property names and drops the declaration (`WillChangeTests.cs`). Component and animation CSS also sets `transform-origin` (`top left`, `50% 50%`, `0 0`) beside the transform it pivots; the only accepted transform is `none`, so a well-formed origin (one or two positions and an optional length, also `-webkit-transform-origin`) moves nothing and is validated and dropped (`TransformOriginTests.cs`).

They also add `transition:all .2s ease` or `transition:none` to buttons, links and panels. A written PDF page is never restyled, so `transition` and its five longhands accept a well-formed transition list and drop the declaration; malformed lists still fail `FPDF1202` (`TransitionTests.cs`).

Likewise `animation:spin 1s linear infinite` and its eight longhands (and `-webkit-` spellings) accept a well-formed animation list, and a well-formed `@keyframes` / `@-webkit-keyframes` rule (one name; `from`, `to` or 0–100% keyframes holding declarations, at the top level or inside `@media print`) is dropped: a written PDF page shows no keyframe. Malformed animation values fail `FPDF1202` and malformed keyframes rules `FPDF1204` (`AnimationTests.cs`).

Resets and app stylesheets set `html{scroll-behavior:smooth}`, `overscroll-behavior:none`, `-webkit-overflow-scrolling:touch` or `scrollbar-width:thin`. A PDF page never scrolls, so `scroll-behavior`, `overscroll-behavior` (and its `-x`/`-y`/`-inline`/`-block` longhands), `overflow-anchor`, `-webkit-overflow-scrolling` and `scrollbar-width` accept one keyword and are dropped; other values fail `FPDF1202` (`ScrollOnlyPropertyTests.cs`). Docs and utility stylesheets offset anchored headings with `scroll-margin-top:4rem` or `scroll-padding-top:80px`; a PDF page is never scrolled to an anchor, so `scroll-margin` and `scroll-padding` with their side and axis longhands accept well-formed lengths (and `auto` or percentages for padding) and are dropped, while malformed values fail `FPDF1202`; carousel and slider CSS adds `scroll-snap-type:x mandatory`, `scroll-snap-align:start` and `scroll-snap-stop:always`, whose keywords are dropped the same way. Mobile resets add `-webkit-tap-highlight-color:transparent` (or `rgba(0,0,0,0)`), forms `accent-color`/`caret-color`, and app shells `scrollbar-color`/`scrollbar-gutter:stable`; these accept `auto`, their keywords or a well-formed colour, alpha included, and are dropped (`UiColourPropertyTests.cs`). Modern stylesheets also declare `color-scheme:light dark` and `forced-color-adjust:none`; a PDF page has no preferred or forced colour scheme, so well-formed values are dropped and malformed ones fail `FPDF1202` (`ColorSchemeHintTests.cs`).

### The `:root` pseudo-class

Design-token stylesheets put their page-wide settings on `:root`. It matches the root `html` element and renders exactly like the same rule on `html`, with pseudo-class specificity (0,1,0) instead of a type selector's (0,0,1), so `:root{color:navy}` beats a later `html{color:maroon}`. It also works in compound and descendant selectors (`html:root`, `:root p`, `:root > body`). Other pseudo-classes and pseudo-elements still fail with `FPDF1203` ([sources and selectors](css-stylesheets.md#sources-and-selectors), `RootPseudoClassTests.cs`).

### Custom properties and var()

Design-token stylesheets declare colours, font sizes and spacing once as custom properties (`:root{--brand-700:#b3261e;--brand:var(--brand-700);--space-6:12pt}`) and use them everywhere with `var()`: `font-size:var(--font-3) !important`, `padding:var(--space-3) var(--space-6)`, `border-top:2px solid var(--brand)`, `style="padding: 0 var(--space-6)"`. Custom properties cascade, inherit and accept `inherit`/`initial`/`unset`; `var()` works in every supported property, shorthands included, with fallbacks and nested references, and the page renders byte-identical to the same page with the values written out ([example](../examples/report-custom-properties.html) with 71 tokens and its [literal oracle](../examples/report-custom-properties-oracle.html)). Where a browser would quietly unset a declaration (a reference with no value and no fallback, a used reference cycle, a value the property does not accept after substitution) FactsPDF fails with `FPDF1202` at that declaration; doubling or deep reference chains fail with `FPDF1205` ([custom properties](css-stylesheets.md#custom-properties-and-var), `CustomPropertyTests.cs`).

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

Mixed percentage/integer channels, decimal *numeric* channels, transparency
(any alpha other than exactly `1` or `100%`), nesting, `calc()` and general
CSS Color remain unsupported;
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
text-decoration colour. Hue is a finite number or an angle in `deg`,
`grad`, `rad` or `turn` from -3600 through 3600 degrees (wrapped by 360); saturation/lightness accept
0–100% including fractions. HSL is converted to opaque 8-bit RGB
before the existing PDF ink/background/border paths, retaining CSS
cascade, font/layout geometry, pagination and resource protection.

The [48-entry colour report](../examples/report-hsl-colours.html)
and [independent fixed-hex oracle](../examples/report-hsl-colours-oracle.html)
must produce identical actual PDF bytes. Source NUnit, managed CLI
and Linux/Windows Native AOT qpdf/Poppler check searchability, multi-page
geometry and original-output recovery. No transparency (alpha other than
exactly `1` or `100%`), nesting, gradients or general CSS Color: unsupported values
fail `FPDF1202`. Implemented in source `ee5af1a67d8aa88fa23da281ea6ab23414f9ef22`; exact-head CI,
independent review, private software, human approval and customer
distribution remain separate.

### Opaque `rgb()` and `rgba(…,1)` colours (source-only)

Controlled report templates can use opaque `rgb(35, 68, 107)`, `rgb(35 68 107)` and `rgba(35,68,107,1)` for text, backgrounds and solid borders, and hex with a fully opaque alpha (`#23446bff`, `#abcf`) wherever hex is accepted (other alpha values fail `FPDF1202`; `OpaqueAlphaHexColorTests.cs`). These produce the same painted PDF as matching hex colours; non-opaque alpha and gradients still fail explicitly. See [bounded CSS colours](css-stylesheets.md#current-source-opaque-rgb-colours-for-reports) and the [print report example](../examples/report-print-spacing.html).
For a fontless case-formatting example with a preserved proper-name span, see the
[report case labels](../examples/report-case-labels.html).

## Borders and rounded corners

### Computed em/rem borders on printable report boxes (candidate source)

Border widths now accept bounded nonnegative `em` and `rem` through the
existing physical and LTR logical width longhands, 1–4 value `border-width`,
and per-side / all-side border shorthands. `em` resolves from the final
computed font size of the painted element; `rem` uses the computed HTML root
(or configured initial size without `html`). The final points drive PDF
border strokes, content-box deductions, text placement, repeated fixed-track
table headers and page fragments. No border shorthand parsing without paint.

This extends the same supported border targets (ordinary report boxes,
bound images, fixed-column `table` and `td/th`), not structural `col`,
`tr` or inline elements. Percentages, negative lengths, `calc()` and
relative `outline-width`/corner radii remain rejected. Winning values that
compute beyond 14,400pt fail `FPDF1202`; impossible content geometry fails
`FPDF1302`, with prior output retained. See the actual HTML-to-PDF point
oracles and negative tests in `FontRelativeBorderWidthTests.cs`.

### Transparent border edges

`border-color: transparent` (longhands, the 1–4 value `border-color`
shorthand and the `border`/`border-*` shorthands) keeps the edge's width in
layout and paints nothing, so `border:4pt solid transparent` places content
exactly like `padding:4pt` and the background shows through the edge. A later
colour, or a `border` shorthand that omits the colour, makes the edge visible
again. Outlines still reject `transparent` (`FPDF1202`), and a rounded box
needs all four sides transparent or none (`FPDF1201`). See
`TransparentBorderColorTests.cs`. `border-style: hidden` on a border edge equals
`none` (no width, no paint; FactsPDF has no collapsing table borders), while
`outline-style: hidden` still fails `FPDF1202` (`HiddenBorderStyleTests.cs`).

### `background: none`

The `background` shorthand accepts a single `none` (any case), the common reset:
it clears an earlier fill exactly like `background: transparent`.
`background-color: none` and `none` combined with other background components
still fail `FPDF1202` (`BackgroundShorthandTests.cs`).


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
pt/px/mm/cm/in/pc/q only. Solid/dashed/dotted/none and opaque ink are
accepted; negative, percentage, alpha, gradient and rounded-corner outlines
reject, preserving output. Outside scoped panels no outline is ever painted,
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

### Combined underline, overline and strike-through in actual PDFs (source-only)

Revision registers and quotes can paint **one, two or three distinct** text
decoration lines in the same measured, searchable PDF text run:

```css
.revised { text-decoration: underline overline solid #285d91 1pt; }
.superseded { text-decoration-line: underline line-through overline; }
```

The shorthand accepts the existing opaque colour, solid/dashed/dotted
stroke and 0.2–12pt resolved thickness; `text-underline-offset` moves only
the underline. Ordinary cascade, `!important`, CSS-wide keywords, TrueType,
flat table text and page-local fragments apply. Standalone `none` is valid;
duplicate lines, `none` mixed with visible lines, `blink`, `wavy`, alpha
and browser descendant-decoration propagation reject explicitly. Each real
PDF path counts toward `MaxDisplayCommands`; failed conversion preserves
the prior PDF. The [44-item quote](../examples/report-combined-text-decorations.html) and
[independently authored longhand control](../examples/report-combined-text-decorations-oracle.html) are byte-compared by
[PDF/TrueType/CLI/negative NUnit](../tests/FactsPDF.Tests/CombinedTextDecorationReportTests.cs).
Source [`2d18399e`](https://github.com/KoalaFacts/FactsPDF/commit/2d18399e25c65267431f0decc7fe716dde82688a).
This is a bounded drawing subset, not complete CSS Text Decoration.


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
fragments continue through existing layout and paint. It does not add
multiple decoration lines, CSS underline-position or browser propagation.
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

`<code>`, `<kbd>` and `<samp>` render as inline text in the supplied regular
font (no monospace face), and `<var>`, `<cite>` and `<dfn>` as italic inline
text that needs an italic font chain (`PhrasingElementTests.cs`); `<time datetime>` and
`<data value>` render as plain inline text, their attribute kept as metadata only
(`TimeDataElementTests.cs`); `<abbr title>` draws a dotted underline and `<abbr>`
without a title is plain text (`AbbrElementTests.cs`). `<q>` wraps its inline text
in curly double quotes (curly single quotes when nested), in the `q`'s own text
style; normal text without a supplied font gets the ASCII `"` and `'`
(`QuoteElementTests.cs`). `<bdi>` renders like `<span>`; FactsPDF lays out every
run left to right, so it isolates nothing (`BdiElementTests.cs`).

The HTML `dir="ltr"` attribute (any ASCII case) is accepted on every element and changes nothing:
it names the only direction FactsPDF lays out, so the PDF is byte-identical to the page without it.
Email and document builders put it on `html`, `body`, tables and paragraphs. `dir="rtl"`,
`dir="auto"` and an empty `dir` still fail `FPDF1103`, and `[dir]` attribute selectors still fail
`FPDF1203` (`DirLtrAttributeTests.cs`).

`<template>` and everything inside it are dropped, as HTML treats template contents as an inert
fragment that never renders: the PDF is byte-identical to the page without the element, in the
head, in containers, lists and tables, and nested. Style elements and style attributes inside it
are not compiled, and elements FactsPDF does not otherwise support may appear there. The contents
must still tokenize: an invalid character reference in an attribute fails as it would elsewhere. The end tag is required (`FPDF1101`), a template inside `title`
fails `FPDF1101`, the contents still count toward `MaxElements` (`FPDF1002`), and a
declarative shadow root (`shadowrootmode`), which browsers do render, fails `FPDF1103`
(`TemplateElementTests.cs`).

`<script>` is dropped with its data and attributes, since FactsPDF runs no scripts and HTML never
renders the element: the PDF is byte-identical to the page without it, in the head, containers,
paragraphs, lists and tables. Script data ends at the first `</script` followed by a delimiter, as
in browsers (which also ignore a self-closing slash on `script`), so markup and styles inside it
never apply and `src` is never loaded; its attributes must still tokenize. The end tag is required
(`FPDF1101`), a script inside `title` or `colgroup` (where HTML would close the column group and
shift the columns) fails `FPDF1101`, scripts count toward `MaxElements` (`FPDF1002`), and data where `<!--` is
followed by `<script` (the double-escaped form, where a browser would not end the element at
`</script>`) fails `FPDF1101` instead of guessing (`ScriptElementTests.cs`). `noscript` is still
unsupported.

`<meta>` in the head accepts `charset=utf-8` and named metadata: `name` or `property` with
`content` (viewport, description, author, generator, Open Graph `og:*`). A static render never
reads it, so the PDF is byte-identical to the page without it, and it does not change the PDF
title or document information. `name=color-scheme` and `supported-color-schemes` fail
`FPDF1103`, since they change the canvas and text colours a browser paints; `http-equiv`,
`itemprop` and `style` fail `FPDF1103`, and a meta outside the head fails `FPDF1101`
(`MetaNameContentTests.cs`).

`<link>` in the head is accepted when every `rel` type only names a related resource or a
connection hint: `icon` (and `shortcut`, `apple-touch-icon`, `mask-icon`), `manifest`,
`canonical`, `alternate`, `author`, `license`, `help`, `search`, `next`, `prev`, `me`, `pingback`,
`amphtml`, `preconnect`, `dns-prefetch`, `prefetch`, `preload` and `modulepreload`. Nothing is
fetched or painted, so the PDF is byte-identical to the page without it. `rel=stylesheet`
(including `alternate stylesheet`), anywhere in the page, fails `FPDF1204` like `@import` (inside a table, the table's structure
check fails first with `FPDF1101`), since
external resources are never loaded and ignoring a stylesheet would change the render; unknown
types and a missing or empty `rel`,
`media` and `style` fail `FPDF1103`; a link outside the head fails `FPDF1101`; links count toward
`MaxElements` (`LinkMetadataTests.cs`).

One `<base>` in the head, such as the `<base href="/">` that server-side frameworks emit, is
accepted and changes nothing: the PDF is byte-identical to the page without it. `base` only sets
the URL that relative URLs resolve against and the default link target, and FactsPDF resolves no
relative URL (an `a` `href` must already be absolute HTTPS or mailto, and images are named
resources), so its `href` is never read and takes any value, including an empty one. `target`
follows the `a` rule (`_blank`, `_self`, `_parent`, `_top` or a name). A relative `a` `href`
still fails `FPDF1103` exactly as without a `base`. A `base` with neither `href` nor `target`,
other attributes such as `media`, `style` and `hidden`, and an empty or other underscore `target`
fail `FPDF1103`; a second `base`, a `base` outside the head and a `</base>` end tag fail
`FPDF1101`; a `base` counts toward `MaxElements` (`BaseElementTests.cs`).

`<header>`, `<footer>`, `<main>`, `<nav>`, `<aside>`, `<hgroup>` and `<search>` render exactly like
`<div>`: block containers with the same CSS, nesting and pagination. An HTML
`<header>` or `<footer>` is an ordinary block in the flow, not repeated on every
page; that is what `PageFurniture` / `--page-furniture` does
(`SectioningElementTests.cs`).

`<figure>` renders exactly like `<blockquote>` (the HTML default sheet gives
both the same margins; like `blockquote` it may not sit in a list or table), and
`<figcaption>` exactly like `<div>`, so a figure with an image and a caption
renders as an indented block (`FigureElementTests.cs`).

`<address>` renders like `<div>` with italic text, as the HTML default sheet
styles it; like `<em>` it needs an italic font chain, and CSS
`font-style:normal` resets it (`AddressElementTests.cs`).

`<dl>`, `<dt>` and `<dd>` render as block containers like `<div>`: `dl` with
the default 8pt top and bottom margins and `dd` with a 24pt start indent
(blockquote's scale), both overridable by CSS. The `</dt>` and `</dd>` end tags
must be written, and like `blockquote`, `dl` and `dd` may not sit in a list or
table (`DescriptionListElementTests.cs`).

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

On current source, business report templates can opt into `font-size:1.2rem` and `line-height:1.5rem`. `rem` uses the `<html>` element's computed font size, or the writer's base font size (`WithFonts(f => f.Size(…))`, default 12pt) when no `html` element exists. On `html` itself, `font-size:rem` resolves against the initial configured font size to avoid a self-reference; `line-height:rem` uses its newly computed root size. Font sizes resolve to 1–144pt; fixed line heights resolve to 0.1–1440pt and inherit as points. This feeds actual glyph layout, wrapping and pagination (not merely syntax acceptance). This typography feature is separate from the newer scoped `rem` lengths for ordinary report box width/min-max/padding; `rem` margins are now limited to paragraphs/headings outside table cells; borders and image/table/column sizing still reject `rem`; only bounded text-indent/letter-spacing/word-spacing now accept it. See the [root-relative contract](css-stylesheets.md#current-source-root-relative-report-typography) and [runnable multi-page example](../examples/report-root-relative-typography.html); these source changes are not the previously retained installable candidate.

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

For report explanatory paragraphs and headings, opt-in `text-indent:18pt`, `text-indent:0.25in` or `text-indent:15%` insets **only the first logical line** within the actual content width; later wrapped lines, explicit `<br>` lines and subsequent pages are not re-indented. The paragraph box/background keeps its original position. Percentages resolve against its actual content width. Nonnegative `pt/px/mm/cm/in/%` or zero are supported; overlarge insets or first words that cannot fit reject with `FPDF1302` without overwriting the destination. Inline spans and table-cell paragraphs cannot declare it directly. Document roots and ordinary report containers may now declare it for inheritance, including into flat fixed-column cell paragraphs; negative/hanging indents and `calc()` are unsupported. This is a scoped PDF text layout capability, not full CSS Text conformance.

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
cascade and inheritance; it does **not** add RTL, `direction` other than `ltr`, HTML `dir` other than `ltr`,
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

For inspection findings, revised quotes and status notes, `text-decoration:overline dashed blue` or `text-decoration:underline dotted #ff0000` paints a real clipped vector line at the font-measured text position. You can also set `text-decoration-color` and `text-decoration-style` individually. The `-webkit-text-decoration` shorthand and its `-line`, `-color` and `-style` longhands (Bootstrap 4 writes `-webkit-text-decoration:underline dotted` on `abbr`) are aliases sharing the standard cascade slots, values and errors (`LegacyPrefixAliasTests.cs`); their CSS-wide cascade and `currentcolor` follow the existing styling rules. The unstyled and legacy solid paths preserve existing bytes. One line only; no double, wavy, multi-line, alpha/transparency, variable thickness or browser-wide propagation. See the [source example](../examples/report-text-decoration-paint.html), [PDF implementation tests](../tests/FactsPDF.Tests/TextDecorationPaintTests.cs), and [bounded CSS contract](css-stylesheets.md#current-source-colored-and-patterned-report-text-decorations). This is current source, not a newly distributed binary or customer licence.

## White space and line wrapping

### Inherited report text flow (source-only)

The existing bounded `overflow-wrap:normal|break-word|anywhere`, `word-break:normal|break-all|break-word`
and nonnegative `text-indent` values can now be declared on `html`, `body`
or ordinary report `div`/`section`/`article`/`blockquote` containers.
Their computed values inherit into descendant paragraphs and headings and
control actual PDF word wrapping, first-line X coordinates and pagination.
An inherited `text-indent` also positions a flat paragraph inside a fixed-column
table cell, including left/centre/right alignment; it does not permit a
direct `text-indent` declaration on the table or its cell paragraph.
Inline elements, list/table structural declarations, unsupported values and
complex text shaping remain outside scope. See the
[bounded inherited CSS contract](css-stylesheets.md#current-source-inherited-report-text-flow).

### Inherited report white-space from document and panels (source-only)

A supported `white-space:normal|nowrap|pre-line|pre-wrap|pre` declaration on
`html`, `body`, `div`, `section`, `article` or `blockquote` now passes its
computed text-whitespace mode to nested report paragraphs and headings.
For example, `section.invoice-notes { white-space:pre-line }` preserves authored
newlines in descendant `p` text without restyling each paragraph. The
renderer uses the same measured line breaking, glyph placement, page
fragmentation and failure modes as an explicit declaration on each child.
A child's winning declaration or `initial` overrides the inherited mode;
`inherit` and `unset` on the container follow CSS text inheritance.
The [PDF equivalence and multi-page NUnit checks](../tests/FactsPDF.Tests/InheritedWhiteSpaceReportTests.cs)
compare inherited and explicitly styled reports and protect prior output
on invalid CSS. Inline `span`, table structural elements, list containers
and unsupported text modes still cannot declare this property. This is a
bounded inheritance extension, not arbitrary CSS Text support.

### Current-source multiline report notes

For controlled invoices, inspections and customer notes, explicit paragraphs and
headings can use `white-space:pre-line` to **retain authored line breaks** from
LF, CRLF, CR and form-feed while continuing to collapse ordinary spaces/tabs.
Lines still soft-wrap to the measured PDF content width and continue across pages;
the final source segment break is discarded. An explicit `<br>` remains a break.
Scoped stylesheet/inline CSS, cascade and explicit p-in-table-cell width work,
but inline/span and table-structural declarations, `break-spaces`, RTL/shaping and
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

Long unbroken invoice and approval identifiers can use `overflow-wrap:break-word` on explicit report paragraphs/headings, including flat paragraphs inside fixed-column table cells. The measured PDF layout keeps ordinary words whole when they fit a fresh line, but splits an oversized token at supported glyph boundaries and continues across pages. `white-space:nowrap` takes precedence; inline declarations and an unfit individual glyph reject without modifying a previous PDF. The legacy `word-wrap` name is an alias with the same values, cascade slot and applicability (`WordWrapAliasTests.cs`). `overflow-wrap:anywhere` (and `word-wrap:anywhere`, which Chromium, Firefox and WebKit all accept) is accepted wherever `break-word` is, so a report stylesheet's `.ledger td { overflow-wrap: anywhere; }` renders: CSS Text 3 makes the two differ only in min-content sizing, which FactsPDF never computes (fixed table columns, explicit or containing-block widths), so the PDF is byte-identical to `break-word` in paragraphs, headings, inherited containers, list items, table cells and multi-page ledgers (`OverflowWrapAnywhereTests.cs`). This bounded source-only feature is not full browser CSS Text or automatic hyphenation. See the [contract](css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers), [32-entry report](../examples/report-overflow-wrap.html), and [independent manual-break oracle](../examples/report-overflow-wrap-oracle.html).

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
failure-recovery gates. `word-break:break-word` wraps exactly like
`overflow-wrap:break-word` whatever `overflow-wrap` says (`WordBreakTests.cs`).
Unsupported `keep-all`, inline spans, arbitrary container declarations, complex shaping
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
computed `html` root font size, or the writer's base font size if there is no
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
root (or the writer's base font size as fallback) drives real PDF vertical gaps,
horizontal text/paint position, wrapping and pagination. Horizontal `auto`
still combines with supported lengths. Negative margins, vertical `auto`,
`calc()`, nonzero body/html margins, list/table/image/inline margins and table-cell `rem`/`em` margins
remain rejected (a zero margin on `<html>`/`<body>` and a zero padding on `<html>` change nothing and are dropped, so `html,body{margin:0;padding:0}` renders, `RootZeroInsetTests.cs`; zero margins and padding and a transparent background on inline text are dropped likewise, `InlineZeroBoxResetTests.cs`, and so are zero margins on `li` and table rows, groups, columns and cells, `TablePartMarginTests.cs`, and zero margins on `ul`/`ol`/`table`/`br` and zero padding on table rows, groups, columns and `br`, `UnusedZeroInsetTests.cs`); bounded paragraph/heading vertical `%` is described below; `div`/`section`/`article` panels have a separate bounded exception. See [the scoped margin contract](css-stylesheets.md#current-source-root-relative-paragraph-margins),
the [32-entry report](../examples/report-rem-margins.html) and its separately
authored [fixed-point oracle](../examples/report-rem-margins-oracle.html).
Unfit output preserves the prior destination. This is source-only.

### Paragraph and heading percentage outside margins (source candidate)

Ordinary `p`, `pre` and `h1`–`h6` outside lists and tables accept
nonnegative vertical `margin-top`/`margin-bottom` percentages via physical,
four-side `margin` and LTR `margin-block` syntax. Values resolve from the
**containing block's content width**, not page height, and alter real PDF
vertical placement, painted backgrounds and borders, heading keep and
multi-page text. `!important`, per-side cascade, CSS-wide reset and explicit
`inherit` from a percentage-margined report panel share the same computation.
Negative/vertical `auto`, `calc()`, list/table percentages and unplaceable
margin geometry still reject, preserving the previous output bytes; there is
no margin collapsing or RTL/Paged Media support. Source implementation
`0f6c58655d651ee934cfb2c95272dde54f4c477e`; [exact contract](css-stylesheets.md#current-source-paragraph-percentage-vertical-margins)
and `ParagraphPercentageVerticalMarginTests.cs` provide HTML-to-PDF evidence.
Exact-head CI, genuinely independent review, main merge and any public package
remain separate.

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
right, block-start = top, block-end = bottom. `direction` other than `ltr`, RTL/vertical writing
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
also accept bounded vertical percentages against their containing content width outside lists/tables. The [50-entry percentage-margin report](../examples/report-percentage-panel-margins.html)
and [independent point-length oracle](../examples/report-percentage-panel-margins-oracle.html)
verify actual nested PDF panel spacing, text, paint and continuation pages.
Paragraph/heading percentages and explicit inheritance of a percentage panel margin are now supported in a separate bounded paragraph feature. This earlier panel source change
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
`box-sizing:border-box` (also spelled `-webkit-box-sizing` / `-moz-box-sizing`,
legacy aliases sharing its cascade slot, `BoxSizingPrefixAliasTests.cs`; likewise
`-webkit-`/`-moz-border-radius`, `-box-shadow`, `-webkit-`/`-moz-`/`-ms-`/`-o-transform`,
`-webkit-filter`, `-clip-path` and `-backdrop-filter`, `LegacyPrefixAliasTests.cs`)
applies constraints to the padded/bordered outer box; the default `content-box` constrains the content width instead.
`<html>`, inline text and `<br>` also accept `box-sizing` (no width or height applies there), so
`*{box-sizing:border-box}` and `html{box-sizing:border-box}*{box-sizing:inherit}` reach every
block. On a `<table>`, `border-box` makes an explicit width include its padding and borders;
columns, groups, rows, cells and cell paragraphs accept it with no effect (`BorderBoxSizingTests.cs`). Supported
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

### Computed `em/rem` image geometry in business reports (candidate)

A bound JPEG/PNG logo or evidence photo may size its PDF image matrix with
`width:8em`, `max-width:6rem`, `height:2em`,
`min-height:3rem` and `padding:0.25em`. `em` uses the image's final
computed font size; `rem` uses the HTML root. The same geometry works for
standalone and direct fixed-column cell images with the existing cascade,
`!important`, `box-sizing`, text-following placement and page movement.
The [new source test](../tests/FactsPDF.Tests/FontRelativeImageSizingTests.cs)
compares actual PDF bytes with independent physical-length oracles and
checks protected output on invalid combinations. Earlier physical-only image
notes below document their original milestone scope. Still excluded:
image height percentages, negative lengths, `calc`, object-fit, automatic
external loading, table structural relative sizing and image splitting.

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

### Menu lists

`<menu>` renders exactly like `<ul>`: disc markers, the marker gutter, the
two-level nesting limit (mixable with `ul`/`ol`), `list-style` and the CSS
cascade, and pagination; every invalid shape fails with the same code as the
`ul` spelling (`MenuElementTests.cs`). It adds no toolbar-menu semantics.

### Automatic table column widths

A table without a `colgroup`, or whose `col` elements all lack a width, now
sizes its columns from its cells' min-content and max-content widths the way
browsers do, instead of failing: a table without a width shrinks to its
content, a wider or explicit width is shared out by each column's content
range, and long text wraps inside its column. The first row sets the column
count; `colspan` cells widen the columns they cover as in browsers. Measured
cell edges and widths match Chrome 158 within 0.15pt on 26
synthetic fixtures with the same font (`TableAutoLayoutTests.cs`,
`scripts/table-auto-layout-oracle.ts`). `col` widths in a partly sized
`colgroup` and `td`/`th` `width` in print lengths or percentages join in as in
Chrome: fixed widths constrain their column, percentages take their share.
Percentage cell padding or image sizes in such tables fail `FPDF1302`;
`table-layout` still fails `FPDF1201`; fixed-column tables are unchanged. See
[automatic column widths](tables.md#automatic-column-widths).

### Table footer rows

A `<tfoot>` directly after the `tbody` holds one or more ordinary `td` rows,
such as an invoice's total. They render exactly like the same rows appended
to the `tbody`: once, after the body rows, with the same cascade, inheritance
(`tfoot{color:…}`, `tfoot td{…}`) and pagination. They are not repeated on
each page fragment, so `tfoot{display:table-row-group}` is accepted and changes
nothing while `tfoot{display:table-footer-group}` fails `FPDF1202`. `th` cells and a `tfoot` before the `tbody` fail with
`FPDF1101` (`TableFooterElementTests.cs`).

### Font-relative padding for fixed-column invoice cells (candidate)

Fixed-track `td`/`th` can now resolve bounded `em` padding from
their own final font size and `rem` from the HTML root font size. These
values reach measured cell content widths, real PDF text placement, painted
cell boxes, repeated headers and multi-page reports. Physical/logical
padding shorthand and side longhands share the existing cascade and
`!important` rules. See the [24-line invoice](../examples/report-cell-relative-padding.html),
[physical-point control](../examples/report-cell-relative-padding-oracle.html)
and [PDF equality, font, pagination, and output-protection tests](../tests/FactsPDF.Tests/TableCellRelativePaddingTests.cs).
Table/col/row box lengths, independent paragraph insets, negative lengths,
`calc()` and impossible track geometry remain unsupported. Not a shipped
package or full CSS table layout.


### Table cell `vertical-align` (source-only)

For business tables with multiline descriptions, `td`/`th` now accept `vertical-align:top`, `middle` or `bottom` to position a shorter cell's text or image within the complete painted row. Whole rows and intact repeated headers work across pages. Rows needing independent cell-by-cell continuation reject `middle`/`bottom` instead of pretending to align; inline baseline alignment, `rowspan` and general CSS table layout remain unsupported. Outside cells the initial value `vertical-align:baseline` (as written by CSS resets) is validated and dropped, since inline text already sits on its baseline; on a cell it still fails `FPDF1202`. See the [40-row report](../examples/report-table-vertical-align.html), [physical-padding oracle](../examples/report-table-vertical-align-oracle.html) and [table contract](tables.md#vertical-alignment-in-whole-table-rows).

### Current-source list marker placement in business registers

The existing ordered/unordered report lists can now place measured numeric, alphabetic, Roman or vector bullet markers `inside` the first line rather than in the default outside gutter. Both `list-style-position` longhand and a restricted one- or two-term `list-style` shorthand participate in real CSS cascade and generate actual searchable PDF text/vector marks. An inside marker reserves its measured first-line advance, while subsequent wrapped lines regain the content edge; pagination retains a single marker per item. The old default outside-list PDF path remains byte-identical.

Examples: `examples/report-list-marker-inside.html`, `examples/report-list-marker-inside-oracle.html` (separately authored longhands) and `examples/report-list-marker-outside-control.html`. Focused managed/layout/failure tests are in `ListMarkerPositionTests.cs`, and Native AOT/qpdf/Poppler evidence is in `verify_native_feature_paths.py`. Only supported `ol/ul/li` contexts, known marker kinds, and `inside/outside` are accepted; no URL images, CSS counters, RTL or generic list formatting.

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

### HTML `type` on ordered clauses

Legal and contract templates write `<ol type="1"><li>Clause<ol type="a"><li>Sub-clause</li></ol></li></ol>`.
`type` on `ol` (`1`, `a`, `A`, `i`, `I`, case-sensitive), `ul` (`disc`, `circle`,
`square`, `none`) and `li` (either set) is a presentational hint for
`list-style-type`: byte-identical to the matching `list-style-type` on that
element, and any author `list-style-type` or `list-style` on it wins. Other
values and other elements fail `FPDF1103` ([contract](lists.md#html-type-attribute),
`ListTypeAttributeTests.cs`).

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
inside/position/images/counters, negative ordinals or
list-item `value` on `ul`; invalid inputs fail without overwriting
a destination. See the
[77-entry pagination example](../examples/report-list-continuations.html)
and [exact list contract](lists.md#current-source-ordered-list-sequencing-and-bounded-shorthand).
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
block descendants and general DOM navigation fail. `target` (`_blank`, `_self`,
`_parent`, `_top` or a name) and hyperlink `rel` types (`noopener`, `noreferrer`,
`nofollow`, `external`, `sponsored`, `ugc` …) are validated and ignored, since a
PDF link always opens outside the document; an empty or other underscore `target`
and an empty or unknown `rel` fail `FPDF1103` (`AnchorTargetRelTests.cs`). A `<base>` in the
head never makes a relative `href` acceptable (`BaseElementTests.cs`).
Clickable linked images, `download`, fragments, local-page destinations and full HTML anchor
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
a validator bypass. The HTML `hidden` attribute (empty or `hidden`) on the same
blocks is the user-agent rule `[hidden]{display:none}`, so it removes the block
unless author CSS sets `display:block`; `until-found` and `hidden` anywhere else
fail `FPDF1103` (`HiddenAttributeTests.cs`). Table parts accept the display
value that names what they already render and change nothing:
`thead{display:table-header-group}`, `tbody`/`tfoot{display:table-row-group}`,
`tr{display:table-row}` and `td,th{display:table-cell}`;
`tfoot{display:table-footer-group}` (a footer repeated on every fragment) and
any other pairing still fail `FPDF1202` (`TablePartDisplayTests.cs`). CSS-wide display keywords,
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
The .NET API sets the same geometry with
`WithPage(p => p.Letter().Landscape())` or `p.Size(width, height, unit)`. Source implementation began at `bb34436538b8a02f1ace0878be1f97eb5bbff77d`;
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
`WithMetadata(m => …)` writer settings. This is **PDF Info metadata**, not text
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
does not change these forced-boundary semantics. Source:
`67566764419232585632d8ea6f2106aeeeff2f70`.
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

`tr` and `thead` accept `break-inside:auto|avoid` (and `page-break-inside`)
too, as in `.report-table tr{break-inside:avoid}`: rows that fit a fresh page
fragment with the repeated header already move whole and the header never
splits, so the page is byte-identical to the page without it. A kept row that
must split (taller than a fresh fragment, or a first row split only for the
table's top decoration) starts on a fresh page, or moves the table there, and
then continues at line boundaries, on the same pages as Chrome prints it; a
kept last row split only for the table's bottom padding/border fails
`FPDF1302` (Chrome paints that decoration past the page bottom). `tbody`, `tfoot`, `td` and
`th` still fail `FPDF1201` (`TableRowBreakInsideTests.cs`,
[exact contract](css-stylesheets.md#table-rows)).

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

## .NET SDK

### Fluent, immutable writer (source-only)

Configure a writer once and reuse it for every document:

```csharp
var pdf = Pdf.Write()
    .WithFonts(f => f.Regular(PdfFont.FromFile("Latin.ttf"), PdfFont.FromFile("Chinese.ttf")).Subset())
    .WithImage("logo", PdfImage.FromFile("logo.png"))
    .WithPage(p => p.A4().Margin(15, PdfUnit.Millimetre))
    .WithPageFurniture(f => f.FooterRight("Page {page} of {pages}"));

byte[] bytes = pdf.ToBytes(html);
await pdf.ToStreamAsync(html, response.Body);
pdf.ToFile(html, "report.pdf");
```

Each `With` method returns a new writer, so one configured writer is safe to
share across threads and requests. Invalid arguments fail immediately with an
`ArgumentException` naming the parameter. Every output renders the complete PDF
in memory before writing, so unsupported input writes nothing; `ToFile` and
`ToFileAsync` also replace the destination only once the new file is complete.
`ToStreamAsync` works with streams that reject synchronous writes, such as an
ASP.NET Core response body, and `ToBufferWriter` writes into an
`IBufferWriter<byte>` such as a `PipeWriter`. Fonts and images load with
`FromFile`, `FromBytes` or `FromStream`; only the files you name are read.
See the [API guide](development.md#library-api). Source implemented at
`b81fdb12c6207a0f815227491876ac8ce2a40407`; it replaces the earlier
`PdfConverter.Convert`/`PdfOptions` entry point and is not part of the older
retained private preview.

### .NET 8 and .NET 9 apps (source-only)

The library targets `net8.0`, `net9.0` and `net10.0`, so .NET 8 and .NET 9 apps
reference it directly; the CLI stays on `net10.0`. The same source compiles on
every target: on `net8.0` two span helpers stand in for members that arrived in
.NET 9, with identical results (`SpanCompatibilityTests`). Output is
byte-identical for the same input, options and .NET runtime version; different
.NET versions may produce different bytes (for example in compressed streams:
fonts, images, page content, because each ships its own zlib) with the same
rendered content, so byte parity and hash pins are authoritative on `net10.0`
only. See
[Run from source](development.md#run-from-source).

## Diagnostics

### Line and column for CLI errors (source-only)

When a rejected template's diagnostic carries a source offset, the CLI prints
the position after the `FPDF` line, so authors can go straight to the cause:

```text
FPDF1202: Report display supports only block and none.
Location: line 3, column 19.
```

The offset usually points at the offending CSS value or element. Lines end at
LF (Unix), CRLF (Windows) or a lone CR (classic Mac), each counted once, and
columns count characters, so CJK text and emoji count as one column each. No
input text is echoed. Diagnostics without an offset (font or image loading,
resource limits other than the page limit) are unchanged; `FPDF1303` points at
the paragraph, list item, heading, panel, image or table that needed the page
past `MaxPages`, and the existing resource `Hint:` line still
follows. The library exposes the same value as `FactsPdfException.SourceOffset`.
Source implemented at `306012538553815967d0e364ef95842648c446bb`; it is not part
of the older retained private preview or a public binary.
