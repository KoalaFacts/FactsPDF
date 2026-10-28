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
| font-size | pt/px/mm/cm/in lengths or positive parent-relative percentages and `em`; computed result 1–144pt |
| font-weight | normal/bold or numeric 400/700; explicit role resources required |
| font-style | normal/italic; explicit role resources required |
| line-height | Unitless 1–10, `normal` (= this engine's default 1.2), positive `%` or `em` (at most 10× font-size), and `pt/px/mm/cm/in` lengths; computed absolute values 0.1–1440pt; inherited as fixed points except unitless/normal |
| color | #rgb/#rrggbb; black, white, red, green, blue, gray, grey; bounded opaque rgb()/rgba() |
| text-align | left/center/right; inherited through text containers, controls paragraph layout |
| text-decoration, text-decoration-line | none/underline/line-through as a single value; real PDF text-run decoration, current text colour, no full browser decoration features |
| margin-top, margin-bottom | Paragraphs/headings only; nonnegative pt/px/mm/cm/in or zero, at most 14400pt |
| margin-left, margin-right | Paragraphs/headings only outside table cells; nonnegative pt/px/mm/cm/in/% or zero, plus `auto`; percentages use containing width; one auto side takes available free space |
| margin (bounded shorthand) | Paragraphs/headings only; 1–4 TRBL values, vertical pt/px/mm/cm/in/zero and horizontal pt/px/mm/cm/in/%/zero/`auto`, including `margin: 2mm 5% 3mm 4mm`; `margin:0 auto` centres an explicit-width outer box; a single CSS-wide keyword accepted |
| break-before, break-after | Paragraphs/headings only; auto/page. break-after also accepts avoid on headings outside lists; see [heading pagination](heading-pagination.md) |

These properties also accept `inherit`, `initial`, `unset` and `!important`. Matching paragraph-only properties on containers or spans is an error, including declarations that would lose to a stronger rule. Styling `br` is unsupported; a matching declaration errors. Head/title metadata remains non-rendering and does not acquire presentation from stylesheet rules. Inline styling of metadata is still rejected.

The original stylesheet increment and later box-layout increments are distinct; current block width/padding/paint scope is documented in [development](development.md), and C1 style selection in [font styles](font-styles.md). Selectors alone do not make new layout features available. Width/height, padding, borders, background fills, flex/grid, tables, images and font-family/@font-face are not added by this increment. Unsupported properties or values fail explicitly even in an unmatched selector. That strict behavior deliberately differs from browser CSS error recovery; use the documented subset, not arbitrary website stylesheets.

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
`rem`, `lh`, `calc()`, font shorthand, arbitrary CSS and browser text shaping
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

This does **not** implement `rem`, `ex`, `ch`, `lh`, `calc()`, font-size
keywords or relative units in margins, width, padding and borders.
Non-positive, malformed or out-of-range computed values are rejected with
`FPDF1202`, before replacing an existing output. Unmatched stylesheets are
still syntax-checked. This is source-preview capability, not public delivery.

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
unsupported. Negative margins, vertical `auto`/`%`, `calc()`, `em/rem`
and full browser margin collapsing are not implemented. Geometry exceeding
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
`em`/`rem` in margins, `calc()`, negative or vertical `auto` margins.
Existing reports without these explicit new values retain their old rendering
path. Results must still respect the chosen page geometry and explicit
resource contracts.

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
