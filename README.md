# FactsPDF

**Generate searchable business-report PDFs from HTML and CSS, without a browser.**

FactsPDF is a KoalaFacts rendering engine for applications and command-line jobs
that control their report templates. It supports English and Chinese text,
explicit font styles, transparent PNGs, tables and pagination through a
documented HTML/CSS subset. Fonts and images are supplied by the caller.

> **Current stage: unreleased `0.1.0-preview.1`; source-bound private candidate retained for maintainer evaluation.**
> [Merged-main CI 40561057964](https://github.com/KoalaFacts/FactsPDF/actions/runs/40561057964) at `47bc97910843ad6f72f11a285ec5b74e13862623` passed managed/API and actual Linux/Windows x64 Native AOT checks; [six other applicable main workflows](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6452146222) also passed. The owner separately verified and privately retained the matching NuGet, managed CLI, Linux native CLI, examples and source components, and executed the Linux native first-use and restricted-font recovery paths.
> **No public NuGet/CLI release, customer software delivery, stable API or general evaluation grant exists.** Human approval of new PDF pages and legal distribution rights remain separate. See the [evaluation guide](docs/evaluation.md) and [licensing](#licensing); private engineering checks do not grant usage rights.

[Quick start](#generate-your-first-pdf) · [C# API](#use-the-net-api) ·
[Compatibility lookup](#compatibility-lookup) · [Examples](#choose-a-report-example) ·
[Licensing](#licensing)

**Have a business report template to assess?** [Request a template-fit check](https://github.com/KoalaFacts/FactsPDF/issues/new?template=report-template-fit.md) with a public, non-confidential description of the layout and runtime. Maintainers can compare it with the documented support boundaries and discuss evaluation rights. This is not a trial grant, a package release or a promise of production suitability.

## Generate your first PDF

You need Git and a **.NET 10 SDK** compatible with [global.json](global.json).
Check the [Community or Commercial licensing requirements](#licensing) before
building or running the engine; there is no general free trial for ineligible
organizations.

```sh
git clone https://github.com/KoalaFacts/FactsPDF.git
cd FactsPDF
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html quick-start.pdf
```

Open `quick-start.pdf`: a one-page service report with a styled table, using
ASCII text and built-in Courier. This example needs no external fonts or images.

To add a header, footer and current page number:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html numbered-report.pdf --header-text "Service report" --footer-text "Internal" --page-numbers
```

The footer reads `Internal | 1`. Existing output files require `--overwrite`;
conversion failures preserve the existing file. Diagnostics go to stderr, and
failure returns a nonzero exit code. Use `--help` for the CLI options.

To identify the **actual built CLI** during deployment or a support request,
run `dotnet run --project src/FactsPDF.Cli -c Release -- --version` from a
source checkout, or `FactsPDF.Cli --version` for an installed executable.
This reads no report input or resources and prints the assembly's informational
version, including the source commit suffix when stamped by the build. It does
not attest toolchain signatures or imply that a public package is released.

Missing fonts, glyphs, style roles, embedding permission or images? The current source CLI keeps the
original error code and adds an actionable hint. Follow the
[first-use recovery recipes](docs/first-use-troubleshooting.md); resources remain
explicit and a failed conversion does not become a partially successful PDF.

## Use the .NET API

For a source-based evaluation, create a console app beside the checkout and
reference the library project. Run these commands from the `FactsPDF` directory:

```sh
dotnet new console --framework net10.0 --output ../FactsPdfDemo
dotnet add ../FactsPdfDemo/FactsPdfDemo.csproj reference src/FactsPDF/FactsPDF.csproj
```

Replace `../FactsPdfDemo/Program.cs` with:

```csharp
using FactsPDF;

using var pdf = new MemoryStream();
var result = PdfConverter.Convert(
    "<h1>Service report</h1><p>Prepared for Example Client.</p>",
    pdf,
    new PdfOptions
    {
        HeaderText = "Service report",
        FooterText = "Internal",
        PageNumbers = true
    });

File.WriteAllBytes("report.pdf", pdf.ToArray());
Console.WriteLine($"Created {result.PageCount} page(s), {result.BytesWritten} bytes.");
```

```sh
dotnet run --project ../FactsPdfDemo -c Release
```

This writes `report.pdf` in the current directory after conversion succeeds.
The API also accepts page geometry, font/image resources, resource limits and a
cancellation token. See the [API guide](docs/development.md#library-api) and the
[local-package consumer recipe](docs/development.md#first-use-from-local-artifacts)
for integration without a source-project reference.

## What works today

| Area | Implemented capability | Details |
| --- | --- | --- |
| Text and fonts | English/Chinese text; explicit normal, bold, italic and bold-italic font chains; ordered character fallback; optional font subsetting | [Fonts](docs/unicode-fonts.md), [styles](docs/font-styles.md), [subsetting](docs/font-subsetting.md) |
| Document layout | Headings, paragraphs, nested blocks, lists up to two levels, page breaks and heading keeps; fixed widths, padding, backgrounds and solid borders that continue across pages | [Layout scope](docs/development.md#current-content-and-layout-scope), [box fragmentation](docs/box-fragmentation.md) |
| Stylesheets | Inline CSS and embedded `<style>` rules; documented selectors and cascade; print-length units `mm/cm/in` for supported lengths, parent-relative `font-size:125%`/`1.125em`, root-relative `font-size:1.2rem`, computed `line-height:150%`/`1.5em`/`1.5rem`/physical leading, four-sided paragraph/heading `margin` shorthand with scoped root-relative `rem` and element-relative `em` lengths, horizontal `auto` centring for explicit-width boxes, opt-in `box-sizing:border-box` report panel sizing, bounded real `min-width`/`max-width` constraints and actual horizontal `margin:0 auto` centring of `div`/`section`/`article` panels (bounded nonnegative vertical margins outside painted borders) plus computed-root `rem` and own-font `em` widths/padding for ordinary report blocks, bounded paragraph/heading `white-space:nowrap` with explicit `<br>` breaks and opt-in `overflow-wrap:break-word` for long report identifiers, first-line paragraph/heading `text-indent` with real wrapping, bounded physical `letter-spacing` for report labels and inline text, measured soft-wrapped `text-align:justify` using PDF word gaps, ASCII-only `text-transform:uppercase/lowercase` on report text, colour-only `background` and solid underline/strike-through decorations | [CSS and spacing boundaries](docs/css-stylesheets.md) |
| Images | Explicit baseline JPEG and supported static, non-interlaced PNG, including transparency; standalone images and direct image items in table cells | [Images and resource binding](docs/images.md) |
| Tables | Explicit fixed columns, `colspan`, flat paragraph/image cells, repeated single-row headers and oversized body rows continuing across pages | [Table rules and limits](docs/tables.md) |
| Headers, footers and page numbers | Legacy centered literal text and current page number; development-source `PageFurniture` adds styled left/center/right regions, `{page}`/`{pages}` labels and whole first-page replacement | [Legacy page text](docs/page-margins.md), [structured page furniture](docs/page-furniture-cli.md) |
| Integration | .NET library and CLI; Linux/Windows x64 Native AOT execution checks | [Build and platform verification](docs/development.md#native-aot-and-verification) |

### Headers and footers: current limits

`HeaderText` and `FooterText` repeat on every page as centered, normal black
10pt text. `PageNumbers = true` appends ` | N` to a nonempty footer, or shows
the number alone. Text is literal: `{page}`, `{pages}` and HTML are not expanded.
It must fit on one line in the existing margins; overflow is an error.

The current source API also supports total-page labels, left/center/right regions,
explicit styles and whole first-page replacement through `PdfOptions.PageFurniture`.
These additions are implemented in the development source and have not been
released as a package. See the [page-furniture CLI guide](docs/page-furniture-cli.md)
for configuration and limits.

Controlled `div`, `section` and `article` report panels can now be
centred using bounded horizontal `margin:0 auto`, including constrained
`max-width`, nested decorated boxes and page fragments. Vertical panel
margins remain zero-only; ordinary paragraphs/headings retain their separate
vertical-margin contract. See the
[panel-margin CSS scope](docs/css-stylesheets.md#current-source-centred-report-panels),
[50-entry example](examples/report-panel-margins.html) and
[independent point oracle](examples/report-panel-margins-oracle.html).
This is source-only functionality, not a new installable candidate or
public release.

**Current source typography:** report templates may use numeric `font-weight`
in exact hundred steps from `100` to `900`. Values `100`–`500` select the
caller-supplied normal font chain and `600`–`900` the caller-supplied bold
chain, including inline labels, CSS selectors and cascade. Real font advances
affect PDF line wrapping and pagination. Missing bold resources fail explicitly;
FactsPDF does not synthesize variable weights. See the
[64-entry example](examples/report-numeric-font-weights.html) and
[font role contract](docs/font-styles.md). This source addition is not a new
public NuGet binary or stable CSS Fonts implementation.

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
lists/tables/inline elements, negative or percentage vertical margins,
margin collapse, CSS `height`, rounded corners and arbitrary HTML are
unsupported and explicitly rejected. The quote is source-only: it does not
update the retained preview binaries or grant customer distribution rights.
[Real 44-item quote report](examples/report-blockquotes.html),
[independently authored div oracle](examples/report-blockquotes-oracle.html) and
[exact CSS/HTML contract](docs/css-stylesheets.md#current-source-bounded-quoted-report-panels).

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

Supported kinds: `disc`, `decimal`, `lower-alpha`, `upper-alpha`,
`lower-roman`, `upper-roman`, `none`. Ordered lists normally remain
decimal and unordered lists remain vector discs, preserving old PDF bytes.
Alphabetic labels progress `a`…`z`…`aa` and Roman labels use standard
subtractive notation through 3999. Text markers use caller-supplied fonts,
measured per-list gutters, and one marker on the first line even across
pages. `none` removes the marker, not the item's text; it retains the
existing list layout indentation. CSS cascade, `!important`, and
`inherit/initial/unset` apply; nested lists retain their own defaults
unless explicitly overridden. Unsupported marker kinds, lists over two
levels, placement on non-list elements and unavailable marker glyphs fail
explicitly, preserving the existing PDF destination.

[74-entry paginated facilities report](examples/report-list-marker-styles.html),
[bounded CSS contract](docs/css-stylesheets.md#current-source-bounded-list-marker-types),
[full list mechanics](docs/lists.md). This is a source-only capability:
**no preview binary, NuGet publication, commercial grant or human PDF
approval** follows from this change.

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

See [exact source contract](docs/css-stylesheets.md#current-source-bounded-report-display-suppression),
[40-entry example](examples/report-display-visibility.html), and
[visible-only PDF oracle](examples/report-display-visibility-oracle.html).
Source implementation is not a published NuGet binary or customer licence.

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
written physical-side [PDF oracle](examples/report-logical-longhands-oracle.html)
for the [logical-side example](examples/report-logical-longhands.html).

This is **LTR normal-flow mapping only**: inline-start = left, inline-end =
right, block-start = top, block-end = bottom. `direction`, RTL/vertical writing
modes, negative values, CSS margin collapse, other logical properties,
`calc()` and unsupported elements remain outside scope. Body/list/table/
image/inline margins remain rejected, as with their physical longhands.
See the [CSS contract](docs/css-stylesheets.md#current-source-ltr-logical-report-spacing)
and [real report + physical-longhand oracle](examples/report-logical-spacing.html).
This source change does not update the historical private binary candidate.

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
pseudo-elements, dashed/rounded borders, margin collapse or resource URLs.
Unsupported declarations fail explicitly and preserve an existing output.
See the [scoped CSS contract](docs/css-stylesheets.md#current-source-html-horizontal-report-rules),
[44-entry report](examples/report-horizontal-rules.html), and the
[independently authored painted-box oracle](examples/report-horizontal-rules-oracle.html).
This source feature is not a public NuGet/CLI release or a new private binary.

## Compatibility lookup

The **Can I Render** site provides searchable support records for HTML,
CSS, text, PDF behavior and runtime integration. Filter by category or status,
open a feature's precise scope and limitations, and follow its source/test
evidence. It supports Chinese/English interface labels, shareable feature links
and JSON/CSV export.

This is a **code-audit snapshot**, not a released-version comparison matrix.
Each record carries its own audit commit; unknown introduction versions remain
unknown. A supported subfeature does not make its whole parent standard supported.

- [Read the current support data](site/data/compatibility.json).
- [Run, verify or deploy the site](docs/compatibility-site.md).
- [Browse Can I Render](https://koalafacts.github.io/FactsPDF/).
- [Site build and deployment status](https://github.com/KoalaFacts/FactsPDF/actions/workflows/compatibility-pages.yml).
  Pages publishing is configured; the public site is separate from engine package releases.

To browse locally, use Node 24 and npm, starting from the repository root:

```sh
cd site
npm ci
npm run dev
```

Open the local URL printed by Vite, normally `http://127.0.0.1:5173/FactsPDF/`.

Source-built business report templates can scale **text spacing**
with typography: `text-indent:0.4em`, `letter-spacing:0.1em`
and `word-spacing:0.15rem`. Each length uses its element's final font
or the computed `html` root, then changes actual glyph positioning,
first-line wrapping and pagination. Letter/word advances remain bounded
to 0–24pt after computation; percentage letter/word spacing, negative
values, inline text indentation and general browser CSS remain unsupported.
See [the contract](docs/css-stylesheets.md#current-source-font-relative-report-text-spacing)
and [72-entry example](examples/report-relative-text-spacing.html).
This source feature does not update retained private installable software.

For report explanatory paragraphs and headings, opt-in `text-indent:18pt`, `text-indent:0.25in` or `text-indent:15%` insets **only the first logical line** within the actual content width; later wrapped lines, explicit `<br>` lines and subsequent pages are not re-indented. The paragraph box/background keeps its original position. Percentages resolve against its actual content width. Nonnegative `pt/px/mm/cm/in/%` or zero are supported; overlarge insets or first words that cannot fit reject with `FPDF1302` without overwriting the destination. Inline spans, containers and table-cell paragraphs cannot set it; negative/hanging indents and `calc()` are unsupported. This is a scoped PDF text layout capability, not full CSS Text conformance.

Report headings, labels and inline text can use a bounded, nonnegative
`letter-spacing:0.75pt` or equivalent `px/mm/cm/in` print length. This changes
real PDF glyph advances, line breaks, horizontal alignment and text-decoration
widths, including with explicitly bound Chinese TrueType glyphs. `normal` and
zero retain legacy PDF bytes. Negative spacing, percentages, `calc()`
and spacing over 24pt are rejected, rather than silently dropped. See the
[scoped tracking contract](docs/css-stylesheets.md#current-source-bounded-letter-spacing).

Controlled report prose can opt into `text-align:justify` to distribute the measured remaining width among collapsed U+0020 spaces on **automatically wrapped** lines. The final line, explicit `<br>` lines, single-line/nowrap text and lines without retained word spaces remain ragged; no RTL, CJK inter-character or full CSS Text claim is made. Both Courier and explicit TrueType paths use real PDF `TJ` displacement. See [bounded justification](docs/css-stylesheets.md#current-source-soft-wrapped-report-justification).

Controlled report prose can opt into `word-spacing:0.01in` (equivalent to `0.72pt`) at collapsed word spaces. This changes actual PDF word positions, line wrapping, alignment and pagination; the `normal`/zero path preserves legacy PDF bytes. The renderer uses PDF `TJ` displacement for Courier and embedded CID TrueType, not byte-specific `Tw`. Only nonnegative print lengths up to 24pt are supported. See [word-spacing boundaries](docs/css-stylesheets.md#current-source-bounded-word-spacing).

Long unbroken invoice and approval identifiers can use `overflow-wrap:break-word` on explicit report paragraphs/headings, including flat paragraphs inside fixed-column table cells. The measured PDF layout keeps ordinary words whole when they fit a fresh line, but splits an oversized token at supported glyph boundaries and continues across pages. `white-space:nowrap` takes precedence; unsupported `anywhere`/inline declarations and an unfit individual glyph reject without modifying a previous PDF. This bounded source-only feature is not full browser CSS Text or automatic hyphenation. See the [contract](docs/css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers), [32-entry report](examples/report-overflow-wrap.html), and [independent manual-break oracle](examples/report-overflow-wrap-oracle.html).

Controlled identifiers or approval labels can use `white-space:nowrap` on `<p>` and headings. Spaces still collapse, explicit `<br>` still works, and text that cannot fit fails with `FPDF1302` without replacing the output. `normal` is byte-identical to the default; `pre`, `pre-wrap` and arbitrary inline no-wrap spans remain unsupported. See [bounded white-space](docs/css-stylesheets.md#current-source-nonwrapping-report-labels).

Controlled report typography can use `line-height:150%`, `line-height:1.5em`, `line-height:18pt` or print units such as `0.25in`. Percent/em compute an absolute line height against the element's own computed font size; that fixed height is inherited unchanged even when nested text changes font size. Unitless `line-height:1.5` remains a multiplier inherited by children. These values affect real line boxes and pagination and reject invalid input before replacing output. See [line-height contract](docs/css-stylesheets.md#current-source-computed-report-line-height).

On current source, business report templates can opt into `font-size:1.2rem` and `line-height:1.5rem`. `rem` uses the `<html>` element's computed font size, or `PdfOptions.FontSize` when no `html` element exists. On `html` itself, `font-size:rem` resolves against the initial configured font size to avoid a self-reference; `line-height:rem` uses its newly computed root size. Font sizes resolve to 1–144pt; fixed line heights resolve to 0.1–1440pt and inherit as points. This feeds actual glyph layout, wrapping and pagination (not merely syntax acceptance). This typography feature is separate from the newer scoped `rem` lengths for ordinary report box width/min-max/padding; `rem` margins are now limited to paragraphs/headings outside table cells; borders and image/table/column sizing still reject `rem`; only bounded text-indent/letter-spacing/word-spacing now accept it. See the [root-relative contract](docs/css-stylesheets.md#current-source-root-relative-report-typography) and [runnable multi-page example](examples/report-root-relative-typography.html); these source changes are not the previously retained installable candidate.

Controlled report templates can use opaque `rgb(35, 68, 107)`, `rgb(35 68 107)` and `rgba(35,68,107,1)` for text, backgrounds and solid borders. These produce the same painted PDF as matching hex colours; non-opaque alpha and gradients still fail explicitly. See [bounded CSS colours](docs/css-stylesheets.md#current-source-opaque-rgb-colours-for-reports) and the [print report example](examples/report-print-spacing.html).
For a fontless case-formatting example with a preserved proper-name span, see the
[report case labels](examples/report-case-labels.html).

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
[32-entry `em` report](examples/report-em-layout.html) has a separately authored
[point-value oracle](examples/report-em-layout-oracle.html) and targeted tests;
this is not part of the retained installable candidate.
See the [root-relative box contract](docs/css-stylesheets.md#current-source-root-relative-report-box-lengths)
and [69-record report](examples/report-rem-box-lengths.html) with its
[independent absolute-point oracle](examples/report-rem-box-lengths-oracle.html).
This feature is source-only and not part of the retained installable preview.

For printed business reports, `div`, `section` and `article` now
support nonnegative top/bottom margins alongside horizontal `margin:0 auto`.
For example, `margin:12pt auto 7pt` centres a painted card and leaves
12pt above and 7pt below its **outside** border, including nested/multipage
PDF content without CSS margin collapsing.
See the [50-entry sample](examples/report-panel-vertical-margins.html),
[independent fixed-spacer oracle](examples/report-panel-vertical-margins-oracle.html)
and [bounded CSS contract](docs/css-stylesheets.md#current-source-real-vertical-report-panel-margins).
Body, lists, tables, images and inline margins remain unsupported.
No new installable candidate or human PDF approval is implied.

Scoped report paragraphs/headings also accept `margin:0.25rem 0.5rem`,
`margin-top:0.5rem` and 1–4 side shorthand values. The computed `html`
root (or `PdfOptions.FontSize` fallback) drives real PDF vertical gaps,
horizontal text/paint position, wrapping and pagination. Horizontal `auto`
still combines with supported lengths. Negative margins, vertical `%`/`auto`,
`calc()`, body/list/table/image/inline margins and table-cell `rem`/`em` margins
remain rejected; `div`/`section`/`article` panels have a separate bounded exception. See [the scoped margin contract](docs/css-stylesheets.md#current-source-root-relative-paragraph-margins),
the [32-entry report](examples/report-rem-margins.html) and its separately
authored [fixed-point oracle](examples/report-rem-margins-oracle.html).
Unfit output preserves the prior destination. This is source-only.

For source-built business reports, `min-width` and `max-width` now constrain
real used block widths, including `width:auto` panels and horizontal auto
margins. For example, `max-width:146mm` caps a report panel, while
`min-width:82mm;max-width:90mm` bounds an invoice-reference box.
`box-sizing:border-box` applies constraints to the padded/bordered outer box;
the default `content-box` constrains the content width instead. Supported
lengths are nonnegative `pt/px/mm/cm/in/%` or zero; `min-width:auto`
and `max-width:none` remove the constraint. Actual over-constrained geometry
is rejected without replacing prior output. This is only ordinary report
blocks/paragraphs/headings; image, table and inline sizing stay separate.
See [CSS scope](docs/css-stylesheets.md#current-source-bounded-report-minmax-widths)
and the [runnable report](examples/report-width-constraints.html).

### Check your template against these boundaries

- Tables need explicit column widths. Short tables and rows that fit a fresh
  page stay whole; only eligible oversized body rows continue at whole text-line
  or image boundaries. Headers and images remain indivisible. No automatic
  column widths, `rowspan` or nested tables.
- PNG support is bounded: no interlaced/16-bit PNG, APNG or SVG. Images are
  explicit resources, not URLs or files automatically loaded from HTML.
- Fonts are explicit static TrueType files. There is no system-font discovery,
  CSS font-family matching, synthetic bold/italic, full complex-script shaping
  or RTL layout.
- General flex/grid, external stylesheets, CSS Paged Media and arbitrary webpage
  conversion are outside the supported subset. Unsupported input produces
  explicit diagnostics.

## Fonts and images are explicit inputs

For Chinese or other supported non-ASCII text, provide appropriately licensed
font files. The paths below are placeholders for files you supply:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html bilingual.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Repeated `--font` options define fallback order. Bold, italic and bold-italic
content need their own `--font-bold`, `--font-italic` and `--font-bold-italic`
chains. Fonts are not bundled; full embedding is the default. Subsetting is
opt-in and respects the supplied font's embedding restrictions.

An HTML image such as `<img src="logo">` names a resource. Bind it using
`--image logo=path/to/logo.png`, or `PdfOptions.Images` in the API. To try the
branded example with a generated synthetic logo, use Python 3 for the fixture
generation step, then the same CLI:

```sh
python3 scripts/png_fixtures.py --output artifacts/readme-images
dotnet run --project src/FactsPDF.Cli -c Release -- examples/branded-business-report.html branded-report.pdf --image logo=artifacts/readme-images/brand-mark.png --header-text "Service report" --footer-text "Internal" --page-numbers
```

## Choose a report example

| Example | What it demonstrates | Inputs beyond HTML |
| --- | --- | --- |
| [Quick start](examples/quick-start.html) | A one-page service report with a styled table | None; ASCII/Courier |
| [Print spacing report](examples/report-print-spacing.html) | Print units (`mm/cm/in`), real computed line heights and four-sided paragraph margins affecting text, page layout and painted panels | None; ASCII/Courier |
| [Long invoice identifiers](examples/report-overflow-wrap.html) | 32 unbroken references, actual measured emergency wrapping, and [independent manual-break oracle](examples/report-overflow-wrap-oracle.html) | None; ASCII/Courier |
| [Root-relative margins](examples/report-rem-margins.html) | 32-entry multi-page service report with computed-root `rem` margins, compared against a separate [point-value oracle](examples/report-rem-margins-oracle.html) | None; ASCII/Courier |
| [Element-relative report layout](examples/report-em-layout.html) | 32-entry service report with `em` box widths, padding and paragraph/heading margins based on each element's computed font; compared to a separate [fixed-point oracle](examples/report-em-layout-oracle.html) | None; ASCII/Courier |
| [Branded report](examples/branded-business-report.html) | Transparent logo beside company details and line items | The explicit `logo` binding above |
| [Detailed service report](examples/business-table-rich-cells.html) | Column spans and ordered paragraphs/images inside cells | The same `logo` binding |
| [Long service report](examples/business-table-long-description.html) | A long description continues across pages; short values occur once and totals follow | The same `logo` binding |
| [Combined reports](docs/combined-report.md#run-a-report) | English/Chinese text, four font roles, lists, image, table and page text together | Four explicit font-role chains, JPEG binding and page options; complete command in the guide |

The [evaluation guide](docs/evaluation.md) helps match a real customer template
and deployment environment to the current scope.

## Development and delivery status

CI runs managed tests on Windows, Linux and macOS, and executes actual Linux x64
and Windows x64 Native AOT CLIs. Independent PDF checks cover content, geometry,
fonts, images, pagination and error recovery. The [merged native feature-path gate
(#58)](https://github.com/KoalaFacts/FactsPDF/pull/58) executes structured page
furniture and oversized-row continuation on both Linux and Windows Native AOT;
the [private candidate PR (#60)](https://github.com/KoalaFacts/FactsPDF/pull/60)
additionally checks native software-ZIP extraction and actual CLI execution.
These checks apply to their exact tested revisions, not to a released product.

The [Node subprocess](docs/node-cli-probe.md) and
[browser WASM](docs/browser-wasm-probe.md) integrations are experimental probes,
not published SDKs. Performance results apply to their documented workloads and
environments; there is no general speed or resource guarantee.

Structured page furniture and the locked `0.1.0-preview.1` candidate toolchain
are implemented in the development source ([#57](https://github.com/KoalaFacts/FactsPDF/pull/57),
[#60](https://github.com/KoalaFacts/FactsPDF/pull/60)).
The owner privately retained all five components built for **merged main
`47bc97910843ad6f72f11a285ec5b74e13862623`**, with source-bound
checksums and real offline Linux Native AOT first-use and
`FPDF1505` font-permission recovery checks. Actual main
[CI 40561057964](https://github.com/KoalaFacts/FactsPDF/actions/runs/40561057964)
passed after a targeted retry of a Windows-native dependency-mirror failure
that occurred before compilation; six other applicable workflow groups
also passed. CI validation and owner-only retention do **not** mean public
distribution, independent signature attestation or customer acceptance.

At earlier engine source `1a81f69a4beaf92a8710e5b79ce355440efb00b0`,
real managed-CLI replay matched all **52 original PDFs / 311 pages** byte for
byte. A separate real Linux Native AOT replay at
`e1dfe3b8ea0b5a0cc1cbd612abf22dd8a59748ea` also matched **52/52**.
Those are scoped historical executions, not a fresh `47bc9791` replay.
The original Unicode 10/30/100-record report inputs and positive C1
four-distinct-Chinese-font-style evidence were inspected in
[PR #71's actual API/managed/native acceptance artifact](https://github.com/KoalaFacts/FactsPDF/actions/runs/40497304548);
the original reports exercise Chinese regular/bold, while C1 separately
exercises Chinese regular/bold/italic/bold-italic.
Engineering evidence does not approve new PDF pages on the user's behalf.
New-page human sign-off, the fixed-environment performance budget,
distribution authority and customer evaluation terms remain open.
Strict local qpdf checking has not been asserted complete.
There is no public NuGet/CLI, Release or customer delivery; follow
[issue #51](https://github.com/KoalaFacts/FactsPDF/issues/51).

| Task | Documentation |
| --- | --- |
| Build, test, package locally or inspect Native AOT output | [Development guide](docs/development.md) |
| Check exact support and acceptance evidence | [Engineering support matrix](docs/acceptance/support-matrix.md) |
| Inspect CSS parsing and differential verification | [CSS syntax](docs/css-syntax-core.md), [quality gates](docs/css-syntax-quality-gates.md) |
| Contribute code or review licensing obligations | [Contribution policy](CONTRIBUTING.md), [licensing guide](LICENSING.md) |

## Licensing

FactsPDF is **source-available, not MIT or OSI-approved open source**. It offers
Community and separately agreed Commercial licensing paths.

- The [Community License 1.1](LICENSE.md) covers qualifying personal use,
  entities with Group annual gross revenue at most **USD 1,000,000** (including
  proprietary commercial use), and qualifying charitable/open-source-project
  uses within their defined scope. The full license controls eligibility.
- Uses outside Community eligibility need written evaluation or commercial
  authorization **before building or running** the engine. Commercial licences
  are [available by arrangement](COMMERCIAL.md#contact-and-safe-handling).
- Covered engine changes and corresponding build materials must be published
  under Community License 1.1 before distribution or operational use, including
  internal use and SaaS, subject to the license's controlled-development/testing
  exception. An independent application's code, customer data, templates and
  generated PDFs do not become public merely by calling FactsPDF.
- Paying for a Commercial License does not automatically permit private engine
  modifications or include support, an SLA, updates or unlimited deployments.

See [licensing examples](LICENSING.md), [commercial terms and enquiry process](COMMERCIAL.md)
and [release requirements](docs/licensing-review.md). Enquiries are welcome.
Maintainers must complete the repository's release and authorization checks
before publishing software packages or accepting paid license orders.
Request a private channel before sharing customer templates or confidential
business details. Receiving an ordinary generated PDF does not itself require
an engine license.
