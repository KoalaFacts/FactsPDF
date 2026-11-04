## Source-only bounded print-media rules

Embedded report CSS now accepts a top-level single `@media print` or
`@media all` block containing ordinary supported selectors and
declarations. Actual PDF font, glyph positions, backgrounds/borders,
pagination, specificity and source order use the same renderer and
resource limits as unwrapped CSS. A 44-row two-page report and
independently authored inline equivalent are tested via NUnit and
Linux/Windows Native AOT with qpdf/Poppler, supplied TrueType,
CLI parity, explicit failure/recovery and original-output preservation.
Unsupported queries (`screen`, lists, conditions), nested media,
`@supports`, `@container`, `@page`, `@import` and loose declarations
remain strict `FPDF1204` failures; unsupported values fail closed,
even in unmatched selectors. This is **partial** coverage of the existing
`rule.media` compatibility record, **not** full CSS Media Queries.
418 fixed compatibility IDs and 107 existing accepted CSS property
spellings are retained. Engine source `70f37e7e881bade30851ecaa06528a27813f0403`.
Do not confuse source evidence with CI/review completion,
private/public software, human approval or distribution rights.

## Scoped LTR logical border width/style/colour components (source-only)

The existing six logical border shorthands now have 18 scoped component
property spellings: `border-inline-width/style/color`,
`border-block-width/style/color`, and the individual
`border-inline-start/end-{width,style,color}` and
`border-block-start/end-{width,style,color}`. Pairs accept one or two
values and individual sides accept one. All lower to existing per-side
physical border cascade, width-dependent text geometry and actual PDF
opaque stroke paint; `inherit/initial/unset` must stand alone and
`!important` applies to the authored declaration. Same supported
LTR elements, original limits and error safeguards, not RTL or complete
CSS Borders.

[44-row two-page source](../../examples/report-logical-border-components.html)
and [independently authored physical-side oracle](../../examples/report-logical-border-components-oracle.html)
check true PDF byte parity, glyph positions, page fragmentation, supplied
TrueType, CLI, Linux/Windows Native AOT qpdf/Poppler and invalid-input
destination recovery. Source [`99328f07`](https://github.com/KoalaFacts/FactsPDF/commit/99328f071395d5d7cc1eedad9138f05ee118fb0f);
CI, independent review, publication, human PDF approval and commercial
licences are separate.

## Bounded LTR logical border shortcuts (source-only)

Six familiar CSS border shorthands are implemented only for horizontal
LTR report boxes: `border-inline`, `border-block`,
`border-inline-start/end` and `border-block-start/end`.
They map to existing physical border-width/style/colour slots before
the normal cascade and painted PDF geometry. Supported terms are the
same existing nonnegative bounded physical widths, four stroke styles,
opaque RGB/HEX/named/HSL colours or one CSS-wide keyword. A multi-page
44-row sample and independent physical-side PDF oracle check actual
text order, positions, border paint, Native AOT qpdf/Poppler and
protected negative-to-success retry. Not RTL, logical longhand
components, arbitrary CSS borders or alpha. Source engine
`6916ef5da96068c167690ef3a0b5f1c94d41f255`; neither source
presence nor test definitions establish CI review, human PDF
approval or public distribution.

## Standard opaque CSS named colours for printable reports (source-only)

## Bounded computed currentColor source update (2026-11-04)

At source `3dbcded4f5d39b16b79bbff7aac4132305659d4a`, existing scoped CSS `color`,
`background-color` and colour-only `background` accept `currentColor`.
For `color` the token selects inherited computed parent text ink; for
backgrounds it selects the painted element's final computed text colour.
Existing border and decoration currentColor support remains unchanged.
Real PDF text, background, text positions and multi-page fragments are
checked against 48 authored entries and a separate literal-HEX oracle.
Explicit font, API, CLI and protected-failure regression paths are included;
native Linux/Windows AOT qpdf/Poppler verification is source-defined.
Unsupported alpha, `color:transparent`, system colours, gradients, URLs,
other contexts and general CSS colour behaviour remain rejected.
This is source-only: exact-head CI, external independent review,
private candidate, human approval, commercial grants and release each
require separate confirmation. The existing 418 capability records and
83 accepted CSS property names remain unchanged.


Controlled templates may now use all **148 standard opaque CSS named colours** in
the existing supported colour slots. Examples: `color:rebeccapurple`,
`background:aliceblue`, `border:2pt solid slategray` and
`text-decoration:underline dotted teal`. Aliases such as
`gray/grey`, `aqua/cyan` and `fuchsia/magenta` resolve to the same
fixed RGB values. They affect real PDF text, background fills, borders and
decorations, not only CSS parsing. Existing inheritance, specificity,
font metrics, page geometry, resource limits and fragmentation still apply.

The [64-record report](../../examples/report-named-colours.html) and
[independently authored HEX oracle](../../examples/report-named-colours-oracle.html)
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

## Bounded percentage RGB colours for business reports (source-only)

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

[48-entry real paginated report](../../examples/report-rgb-percent-colours.html) and
[independently authored HEX control](../../examples/report-rgb-percent-colours-oracle.html)
target exact PDF byte, searchable body, page/position, managed API/CLI,
Linux/Windows Native AOT qpdf/Poppler and failure/recovery equivalence.
Implementation source [`6a79f3f6`](https://github.com/KoalaFacts/FactsPDF/commit/6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d).
Actual CI, independent review, retained preview binary, human approval
and customer distribution are separate checks; no release is implied.

## Bounded opaque HSL report colours (source-only)

Business-report templates can use `hsl(210,60%,40%)`,
`hsl(210deg 60% 40%)` or `hsla(120,100%,25%,1)` as scoped
`color`, `background`/`background-color`, border colours and
text-decoration colour. Hue is a finite number or `deg` from
-3600 through 3600 (wrapped by 360); saturation/lightness accept
0–100% including fractions. HSL is converted to opaque 8-bit RGB
before the existing PDF ink/background/border paths, retaining CSS
cascade, font/layout geometry, pagination and resource protection.

The [48-entry colour report](../../examples/report-hsl-colours.html)
and [independent fixed-hex oracle](../../examples/report-hsl-colours-oracle.html)
must produce identical actual PDF bytes. Source NUnit, managed CLI
and Linux/Windows Native AOT qpdf/Poppler check searchability, multi-page
geometry and original-output recovery. No transparency, slash alpha,
turn/rad, nesting, gradients or general CSS Color: unsupported values
fail `FPDF1202`. Implemented in source `ee5af1a67d8aa88fa23da281ea6ab23414f9ef22`; exact-head CI,
independent review, private software, human approval and customer
distribution remain separate.

## Bounded relative font weights for source-built reports

Source [`e94987af`](https://github.com/KoalaFacts/FactsPDF/commit/e94987af9b07c2d7502c25b41625b85aa393f63f) adds `font-weight:bolder/lighter` in the existing text CSS cascade, selecting the caller's actual bold/normal font chains. On this explicit **two-role** ladder, either endpoint saturates; numeric hundreds still map 100–500 to normal and 600–900 to bold. The 48-entry report, separate 400/700 role PDF-byte oracle, NUnit, real Native AOT/qpdf/Poppler, missing-font diagnostics, pagination and protected-output tests are source prepared. This is not CSS's complete nine-weight matching, synthesized fonts, a new distribution candidate, an actual CI/review approval or customer licensing.

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

## Current-source percentage report-panel margins

Source `0518b51d7a34457798c748db6753c4270ce20996` adds nonnegative vertical percentages to bounded
div/section/article/blockquote and standalone hr. `margin-top/bottom`,
`margin`, LTR `margin-block` and logical-side longhands use actual
containing content **width**, existing CSS cascade and outside-box PDF
layout. The [50-entry report](../../examples/report-percentage-panel-margins.html)
and [independent fixed-point oracle](../../examples/report-percentage-panel-margins-oracle.html)
are exercised by `PercentagePanelMarginTests.cs` and Linux/Windows
Native AOT `verify_stylesheets.py` with qpdf/Poppler, full searchable
text, pagination, and previous-output preservation. Paragraph/heading
percentages, cross-type percentage inheritance, vertical auto, negative
values and unplaceable geometry fail explicitly. Source coverage is not
exact-head CI, an independent approval, a retained preview binary or
customer distribution permission.

## Source-only PDF Info metadata for first use

Optional `--title/--author/--subject/--keywords` (and the public
`PdfOptions` fields) write real UTF-16BE PDF Info strings, without
altering the searchable page body, adding timestamps, parsing HTML
titles or requiring fonts. `CliPdfMetadataTests.cs` tests actual
Info/trailer links in classic and Unicode/PNG PDFs, size limits,
default byte parity, malformed values and output recovery. The
Linux/Windows Native AOT verification uses Poppler `pdfinfo` and
glyph-coordinate comparisons plus qpdf. This does not add CSS audit
rows, XMP, PDF/UA, a released binary, or a customer licence.

## Current-source custom physical CLI paper sizes

A complete `--page-width length --page-height length` pair selects
bounded, invariant-culture point dimensions (72–14400pt per side, using
`pt/px/mm/cm/in`) for custom report stock; the actual PDF MediaBox, glyph
placement, wrapping, pagination, images and page furniture use that geometry.
The pair cannot mix with `--page-size` or `--landscape`. Malformed,
missing, repeated, mixed or unplaceable geometry exits 2 without replacing an
existing PDF, with successful retry. `CliCustomPaperTests.cs` includes direct
CLI/API PDF-byte, Unicode font/PNG, default parity and recovery cases;
`scripts/verify_native_feature_paths.py` extends Linux/Windows Native AOT
qpdf/Poppler checks with A5/Legal multi-page geometry and complete body text.
Introduced at source `271d05350f363857cf2a6d44f1ea40e876f8573c`.
This is not a CSS compatibility row, custom print bleed, a new private/public
binary, human-approved page or customer licensing grant.

## Current-source real CLI physical page margins

`--margin 0|length` accepts one nonnegative uniform physical margin in
`pt/px/mm/cm/in` for A4/Letter portrait or landscape. The selected inset
updates real PDF body glyph positions, wrapping and page-furniture layout.
Default and explicit 36pt retain identical PDF bytes. Bounds must fit the
shorter physical page; malformed, repeated, negative or unplaceable margins
fail before replacing an existing PDF. `CliPageMarginTests.cs` and Native
AOT qpdf/Poppler all-page PDF text/geometry and recovery checks cover the
bounded first-use command-line scope. This does not add any CSS support
matrix row, a new retained candidate or a customer evaluation grant.
## Current source: bounded image min/max-height PDF matrices

Direct bound PNG/JPEG images and fixed-column td/th image items consume
physical `max-height` and `min-height` constraints. Auto-height scales both
PDF image axes from intrinsic pixel ratio; explicitly set CSS height can
stretch independently, while min overrides max and border-box counts vertical
edges once. Existing image resource, pagination and protected-output
semantics remain. A 36-record Native AOT/qpdf/Poppler source-versus-physical
PDF oracle and `ImageHeightConstraintsTests.cs` validate this bounded scope,
including invalid input, conflicting axis geometry and retry preservation.
Not a general CSS Sizing/max-height implementation: no percentages,
em/rem/calc, SVG, object-fit, auto shrink or image page fragmentation.
No new software package, reference approval, licensed grant or public release.

## Current-source bound image height PDF geometry

Physical CSS `height` now changes the real PDF image matrix for explicitly
bound standalone/direct fixed-column JPEG/PNG images. Height alone derives
intrinsic-ratio width; two explicitly specified axes can stretch the image.
Border-box subtracts vertical padding/border once; unsupported percentage,
em/rem, calc, object-fit and zero/overfull geometry fail strictly.
`ImageHeightSizingTests.cs`, the [36-record report](../../examples/report-image-heights.html)
and [independent width oracle](../../examples/report-image-heights-oracle.html)
provide source NUnit and Linux/Windows Native AOT/qpdf/Poppler PDF-byte,
searchable-text, pagination and protected-error evidence. Source `7afae6b56ba8ddbaad2f434cfb4a9d35f8b7a2dd`
does not relabel the retained private candidate or constitute human approval,
rights grant, final-head CI success or independent review.

## Current-source bounded image min/max and border-box PDF geometry

Standalone and direct fixed-column cell images support `min-width`,
`max-width` and `box-sizing:border-box` using the shared actual PDF
image matrix resolver. Images keep original resource aspect ratio; explicit
bounds depend on parent/cell content width (including colspan). An
independent [36-record point-width control](../../examples/report-responsive-images-oracle.html)
is compared against the [responsive report](../../examples/report-responsive-images.html)
through NUnit and the Linux/Windows Native AOT CLI/qpdf/Poppler gate,
including searchable content, page movement, strict failures and retained
output/recovery. No implicit shrinking, CSS height, object-fit, em/rem image
sizing, auto columns or general CSS Sizing. Engine source
`b6bebe8e3b97f13190148c6f53bed49ec2678ede`;
actual head CI, independent review, held candidate, human approval and
customer rights are separate.

## Current-source scoped word-break PDF glyph wrapping

`word-break:normal | break-all` accepts explicit p/heading (including
fixed-column table-cell p) in the current source. The measured PDF line
breaker actually fills remaining line width inside long identifiers, unlike
`overflow-wrap:break-word`'s emergency-only behavior. Standard per-property
cascade/CSS-wide values, `white-space:nowrap`, font glyph availability,
pagination and protected destination errors remain enforced. Not Unicode
shaping, grapheme-safe breaks, keep-all, hyphenation, inline styles or full
CSS Text.

Native `verify_native_feature_paths.py` compares the
[48-record report](../../examples/report-word-break-identifiers.html)
with an [independently authored explicit-break oracle](../../examples/report-word-break-identifiers-oracle.html),
including exact PDF bytes, qpdf integrity, Poppler per-page searchable text,
strict invalid input and restored output. `WordBreakTests.cs` covers
managed PDF geometry/bytes and typed errors. Source ac6cec7000b308b403dd95abcd14441240b1f791; no old binary
relabel, customer grant or human approval.

## Current-source fixed-height business-report panel rendering

`height:auto` or bounded nonnegative `pt/px/mm/cm/in/em/rem` (and
exact unitless zero) on ordinary div/section/article/blockquote outside
lists/tables now determines actual PDF painted box height and following
text location, under the established CSS cascade and `box-sizing`.
The larger `min-height` remains the lower bound. A fixed-height panel
must fit its content on one page: overflow, cross-page continuation,
percentages, invalid values and unsupported elements fail explicitly
without replacing a prior destination.

`ReportPanelFixedHeightTests.cs`, the 40-card
[report](../../examples/report-fixed-height-cards.html),
[layout control](../../examples/report-fixed-height-cards-oracle.html)
and the shared Native AOT/qpdf/Poppler gate check PDF bytes, measured
glyph/box positioning, pagination and protected-output recovery.
This is source capability at `9f141fbc33bb2c4a3764b12c6c8c52b8c2bd00bc`,
not full CSS Sizing, an installable package, approved pages or a
customer distribution grant. Final-head CI and independent review
must be verified separately.

## Current-source bounded report-card keep-together

A top-level div/section/article/blockquote with one-to-eight direct
nonempty p/pre/h1–h6 children can use `break-inside:avoid` (also
`page-break-inside:avoid`); preflight measures actual PDF glyph wraps,
padding, borders and margins, moving the whole card intact to a new page
when needed. `ReportKeepTogetherTests.cs` asserts PDF-byte parity with
a manually forced page, no spurious page when it fits, cascade/aliases,
unfit-card failure and untouched existing output. The
[printable report](../../examples/report-keep-together.html) and
[authored forced-break control](../../examples/report-keep-together-oracle.html)
are separately converted with real Native AOT and qpdf/Poppler page/text
verification in `scripts/verify_native_feature_paths.py`.
Not general break-inside, nested panels, tables/lists/images, fixed/minimum
height, arbitrary descendants, or a card taller than one PDF page.
Source implementation: `ecdef6d8745caa3d2b527197c5767e25f89ba6b0`.
Exact final CI, independent review, human PDF approval, separately retained
software and publication rights are independent gates.

## Current-source bounded report-section page boundaries

The source at `67566764419232585632d8ea6f2106aeeeff2f70`
allows explicit `break-before:page` and `break-after:page`
on ordinary div/section/article/blockquote panels. Scoped legacy
`page-break-before/after:always` aliases and `auto` target the same
cascade slots, including `!important`, inheritance/reset and nested
panel fragments. `SectionPageBreakTests.cs` checks actual PDF paint
and text per page, two boundaries without an empty middle page, no phantom
first page, hidden panels, and protected input errors.
`verify_native_feature_paths.py` compares three actual CLI PDF pages
and searchable qpdf/Poppler output from the authored source
[report](../../examples/report-section-page-breaks.html) and a separate
[modern-break oracle](../../examples/report-section-page-breaks-oracle.html),
including protected PDF recovery after rejected input.
Not general keep, Paged Media, table/list/inline or unsupported left/right
page selection; a separate bounded top-level flat-card break-inside subset
does not alter forced section breaks. Exact-head CI, independent
review, private candidate, customer rights and human page approvals are
separate; no previous binary is relabelled.

## Current-source independent corner-radius PDF rendering

The four physical corner longhands compose with 1–4 value `border-radius`
under per-side cascade, `!important`, CSS-wide resets and explicit
inheritance. Each accepts one bounded physical value. Real PDF Bezier
background and uniform solid border-ring paint follows the original
pagination/layout path. `RoundedCornerLonghandTests.cs` asserts actual
PDF bytes, per-corner source order, independent inheritance and errors;
the native verifier compares the 50-card
[longhand report](../../examples/report-corner-longhands.html) byte-for-byte
to the [existing independent shorthand report](../../examples/report-asymmetric-rounded-panels.html),
with Poppler/qpdf, original-output protection and real page continuations.
No ellipse/percent/em/rem radii, rounded table/list/image/inline boxes,
new installable candidate, human approval or distribution authorization.

## Current-source bounded minimum-height card geometry

`min-height` on `div/section/article/blockquote` outside lists/tables
reserves real PDF-height space and painted panel area, including empty cards.
Positive physical/em/rem lengths and exact zero, non-inheritance, CSS-wide
reset/explicit inheritance and `!important` cascade follow the scoped box
model; `border-box` includes padding/borders, `content-box` adds them.
Actual glyph positions, panel rectangles, following rows, page relocation and
multi-page natural content are covered by `ReportPanelMinHeightTests.cs`,
the inspection example and 40-card Native AOT qpdf/Poppler physical-padding
oracle in `verify_native_feature_paths.py`. Indefinite percentages, auto,
unrestricted height/max-height, table/list/inline targets and unplaceable geometry
explicitly fail with protected original output. No general CSS Sizing,
human reference approval, public/private candidate rebadge or customer licence.

## Current-source rounded report-card PDF painting

`border-radius` now accepts one to four physical corner lengths (each zero
or bounded 0.25–72pt) clockwise TL/TR/BR/BL and shapes actual opaque
PDF backgrounds and uniform solid borders on ordinary blocks only; first/top
and last/bottom page fragment caps are rounded, middle continuation sides
are square. `RoundedReportPanelTests.cs`, the 54-card report, separate
16px/12pt oracle and Linux/Windows Native AOT/Poppler witness validate real
curves, text positions, pagination, legacy zero-radius PDF bytes and failure
recovery. Not asymmetric/patterned borders, percentages, two-axis elliptical radii
or table/image/list/inline boxes. Source-only; current-head CI, independent
review, retained binary, human reference approval and customer grant are
separate gates.

Current-source `td/th` `vertical-align:top/middle/bottom` is a
bounded whole-row PDF positioning feature, not general CSS table layout.
`TableVerticalAlignmentTests.cs`, the 40-row report, its separate
physical-padding control and the Native AOT/Poppler feature verifier
cover actual text/image geometry, repeated whole rows and protected
failure recovery. Split rows reject middle/bottom. Exact-head CI and
independent review, human approval, private binary and publication
remain separate gates.

# Acceptance support matrix — evidence, not marketing

Current-source `blockquote` is a bounded genuine PDF block container with
default 8pt vertical and 24pt horizontal margins, inherited text, real
borders/backgrounds/padding and continued page fragments. Its 44-entry
report and separately authored physical `div` oracle, source NUnit
`BlockquoteReportTests.cs` and Native AOT `verify_stylesheets.py` gate
actual PDF bytes, text, pagination and error recovery. It is not browser
citation metadata, a `cite` attribute, a Tagged PDF semantic, a human
approved output or a rebuilt private candidate. Unsupported input still
fails explicitly. Exact-head CI and independent review must precede
normal merge.

Current-source `text-indent` is limited to explicit paragraphs/headings outside tables. Actual first-line glyph x and wrapping differ, while later lines/page fragments and painted boxes keep their original x. `TextIndentTests.cs` and `verify_stylesheets.py` cover point/physical/percent parity, rejection and output preservation. Evidence is source-specific, not a new retained candidate or a human-approved PDF comparison.

Current-source parent-relative `font-size` percentages and `em` values
resolve against the actual computed parent (not a default heading multiplier).
`RelativeFontSizingTests.cs` and the native print report check explicit-point
PDF byte equivalence, multiline glyph layout and strict failing-output
recovery; not `rem`, general relative lengths or a released candidate.
These checks require exact-head CI and independent review before merger.

Current-source opaque RGB-function additions have a separate real PDF and Native AOT hex-byte-equivalence/rejection path in `OpaqueRgbCssTests.cs` and `scripts/verify_stylesheets.py`; they do not re-approve any historical human reference pages, re-label previously retained binaries, or establish full CSS colour/alpha support.

Engine comparison baseline: M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`. Source SHA and every actual outcome come from each run's manifest/environment/report.

| Cases | Contract | Completion meaning |
|---|---|---|
| B00 | ASCII text control via CLI/API | Independent text/structure/bounds; original native reference approved |
| B01 | Bilingual brief, headings and information panel | Original native reference approved; new captures require their own review |
| B02-10/30/100 | Ordered synthetic records as paragraphs | Not table support; every ID and sentence checked |
| B03 | Three explicit report sections inside a continued panel | Real existing block fragmentation; observed pages reported |
| N-wide/N-zero | Unbreakable/zero-width text | FPDF1302 before replacing existing bytes, CLI/API |
| N-pages/input/elements/depth | Existing resource limits | Exact FPDF1303/1001/1002/1003, public API probe only |
| N-css-characters/declarations | Existing CSS work budgets | FPDF1205, public API probe only |
| N-display/output | Command/byte budgets | FPDF1401, public API probe only |
| N-utf16-high/low | Runtime malformed UTF-16 | FPDF1304 without replacement or caller-output writes |
| T-bold | C1 explicit normal/bold/italic/bold-italic; separate actual font and cross-entry evidence | Legacy corpus supplies only normal fonts and now checks missing-role FPDF1507; use C1 evidence for positive support |
| T-list | Positive decimal/disc two-level smoke, CLI/API bytes must match | Candidate only; full wrapping/pagination/geometry evidence is in C2 |
| T-table-10/30/100 | Legacy malformed table probes (missing explicit columns/sections) | FPDF1101 remains expected; positive E1/E2/F4 evidence is separate |
| T-jpeg | Defined LoadImage resource API; legacy probe has no bound image and checks FPDF1604 | Full positive D1 evidence is separate; rejection is not positive credit |
| T-page-furniture/T-heading-keep/T-complete-report | Original harness placeholders have no bound executable inputs | Legacy `blocked-design` describes these unbound entries, not current engine support; use separate F1–F4 evidence below |

The [approval registry](reference-reviews.json) records **six human-approved original native baselines (ten pages)** with durable PDF/text/image hashes and the original reviewed source. A green new candidate-evidence job means capture and independent checks completed; it does not transfer those approvals to changed outputs, approve all nine targets, or complete the Developer Preview. Reports keep target totals, missing approval, and unapproved performance budgets visible. Missing tools or wrong target diagnostics are errors, not skipped support.

T-bold's [explicit font style implementation and evidence](../font-styles.md) preserves ordered per-character fallback and adds four independent roles. [C2 lists](../lists.md) have their own real report and cross-entry verification; the legacy smoke does not import full C2 evidence or user reference approval automatically. The original nine target cases remain the denominator. Final exact-head CI/review gates must be recorded for each stage; neither expected rejection nor one successful smoke completes all product targets.

[D1 explicit JPEG images](../images.md) have their own real image report and per-entry validation. The legacy harness does not import D1 evidence or user page approval automatically.

## Separate positive evidence

These implemented paths have independent executable evidence. This table neither changes the original nine-target denominator nor promotes malformed/unbound legacy entries to `verified`.

| Capability | Executable positive evidence | Approval boundary |
|---|---|---|
| Computed `em/rem` report text spacing | [Relative CSS contract](../css-stylesheets.md#current-source-font-relative-report-text-spacing), `tests/FactsPDF.Tests/FontRelativeTextSpacingTests.cs`, [72-entry sample](../../examples/report-relative-text-spacing.html) and independent [point oracle](../../examples/report-relative-text-spacing-oracle.html), `scripts/verify_stylesheets.py` | Actual first-line indentation, Courier/TrueType glyph advances, wrapping, multi-page searchable PDF, byte equality to fixed points and preserved output on failure; negative/percent letter/word spacing, hanging indent and general CSS Values unsupported. Exact-head CI/independent review required; not retained candidate or human approval. |
| Bounded ASCII report `text-transform` | [CSS case contract](../css-stylesheets.md#current-source-ascii-report-text-transform), `tests/FactsPDF.Tests/TextTransformTests.cs`, `examples/report-case-labels.html` | Actual pre-font-selection ASCII uppercase/lowercase and token-initial `capitalize` in PDF glyph runs and searchable text, spans across word boundaries, explicit inline reset, Unicode-cased rejection, pagination, 54-entry authored-case PDF/Native AOT parity and protected-output failures; no Unicode word segmentation or new released software |
| Bounded positive report `letter-spacing` | [Tracking contract](../css-stylesheets.md#current-source-bounded-letter-spacing), `tests/FactsPDF.Tests/LetterSpacingTests.cs`, `examples/report-print-spacing.html` | True glyph advance in ASCII/TrueType PDF, wraps/pagination/decoration geometry, native physical-unit PDF parity and strict preserved-output failures; no negative tracking or full CSS Text. Final source head requires genuine separate review and checks. |
| Soft-wrapped report `text-align:justify` | [Scoped contract](../css-stylesheets.md#current-source-soft-wrapped-report-justification), `tests/FactsPDF.Tests/JustifiedTextTests.cs`, `examples/report-print-spacing.html` | Real auto-wrapped first/middle lines fill their content width via Courier/TrueType PDF `TJ` word spacing; last and explicit-break lines remain ragged, insets/inheritance/pagination/negative recovery; Native AOT PDF checks; not RTL/CJK/full CSS Text |
| Bounded positive report `word-spacing` | [Scoped contract](../css-stylesheets.md#current-source-bounded-word-spacing), `tests/FactsPDF.Tests/WordSpacingTests.cs`, `examples/report-print-spacing.html` | Measured U+0020 glyph advances, actual PDF `TJ` on Courier/CID fonts, wrap, inheritance, legacy zero-byte parity, rejecting unsafe values and destination recovery; Linux Native AOT PDF check |

| Current-source bounded HTML `pre` and `white-space:pre` | [Scoped contract](../css-stylesheets.md#current-source-preformatted-audit-blocks), `tests/FactsPDF.Tests/PreformattedReportTests.cs`, [72-row report](../../examples/report-preformatted-audit.html), [independently authored pre-wrap oracle](../../examples/report-preformatted-audit-oracle.html), `scripts/verify_native_feature_paths.py` | Actual authored spaces, breaks, no soft wraps, measured Courier/TrueType glyphs, multipage report, CSS cascade, Native AOT/Poppler x positions and protected-output negative recovery. Source commit d8d1a9e0; exact head CI, independent review, human approval and public/private release remain distinct. |
| Bounded preserved-space `white-space:pre-wrap` | `tests/FactsPDF.Tests/WhiteSpacePreWrapTests.cs`, [72-note report](../../examples/report-preserved-space-notes.html), [independently authored collapsed-space control](../../examples/report-preserved-space-notes-oracle.html), `scripts/verify_native_feature_paths.py` | Real PDF spaces and Poppler X offsets, CRLF source breaks, TrueType glyph metrics, soft wraps/pagination, table cells, CSS cascade and strict protected-output failures. Tabs and broad CSS Text intentionally rejected; exact new-source CI/independent review and human page approval remain separate. |
| Whole-paragraph/heading `white-space:pre-line` | [Multiline note scope](../css-stylesheets.md#current-source-multiline-report-notes), `tests/FactsPDF.Tests/WhiteSpacePreLineTests.cs`, [72-entry report](../../examples/report-pre-line-notes.html) and [explicit br oracle](../../examples/report-pre-line-notes-oracle.html), `scripts/verify_native_feature_paths.py` | Actual CRLF/CR/LF/FF segment-break painting, collapsing spaces, trailing-break handling, TrueType PDF byte parity, real paginated Native AOT/Poppler text positions and strict error/output recovery. Source commit `6ab5d2c8ff3c10c76e928b6ad192d9ac46965c89`; full CSS Text, inline/container declarations and public binary release remain unsupported. |
| Whole-paragraph/heading `white-space:nowrap` | [CSS scope](../css-stylesheets.md#current-source-nonwrapping-report-labels), `tests/FactsPDF.Tests/WhiteSpaceNowrapTests.cs`, `examples/report-print-spacing.html` | Real PDF one-line/explicit-break geometry, overflow `FPDF1302` with protected destination, stylesheet cascade and unsupported-value/element recovery; corresponding real Native AOT checks and independent review required for this new head. |
| Bounded ordinary report min/max widths | [Scoped CSS contract](../css-stylesheets.md#current-source-bounded-report-minmax-widths), `tests/FactsPDF.Tests/MinMaxWidthTests.cs`, [printable report](../../examples/report-width-constraints.html) and independent authored oracle | Resolved content/border-box constraints on automatic and explicit widths, actual horizontal centring, percentage containing width, page-fragment painting, strict unsupported cases and preserved PDF target; Linux Native AOT actual-PDF equality/recovery; precise head review and CI required before merge. |
| Report `box-sizing:border-box` | [CSS scope](../css-stylesheets.md#current-source-border-box-report-panel-widths), `tests/FactsPDF.Tests/BorderBoxSizingTests.cs` | Real width, wrapping, auto centring, multi-page painted fragments and destination recovery with an independently authored content-box PDF; real Native AOT gate and independent review still required. |
| Root-relative `rem` report font/leading | [Scoped root-relative CSS](../css-stylesheets.md#current-source-root-relative-report-typography), `tests/FactsPDF.Tests/RootRelativeTypographyTests.cs`, `examples/report-root-relative-typography.html`, `scripts/verify_stylesheets.py` | Actual computed `html` root and fallback; separate `html` font self-reference and line-height behavior; multi-page Courier/TrueType and Native AOT point-projection PDF equivalence, searchable content and protected-output failures. Only `font-size`/`line-height`; exact-head CI, independent review, customer authorisation and human approval remain separate. |
| Root-relative `rem` report box lengths | [Bounded box contract](../css-stylesheets.md#current-source-root-relative-report-box-lengths), `tests/FactsPDF.Tests/RootRelativeBoxLengthTests.cs`, [69-record source report](../../examples/report-rem-box-lengths.html) and independent points-only oracle, `scripts/verify_stylesheets.py` | Real computed `html` scale drives `width`, `min-width`, `max-width` and padding on ordinary blocks; checks border-box, auto centring, text wrap, multi-page backgrounds, actual Native AOT PDF-byte equivalence, strict unsupported-value/element rejection and unchanged destination. This original width/padding slice predates the separate root-margin feature; general container margins, border/table/image or full CSS Values rem remain unsupported; not in retained private candidate |
| Computed percent/em/print-unit line-height | [CSS contract](../css-stylesheets.md#current-source-computed-report-line-height), `tests/FactsPDF.Tests/LineHeightUnitsTests.cs`, `examples/report-print-spacing.html` | Nested fixed-vs-unitless inheritance, actual PDF baselines and byte-equivalent independent point-oracles, invalid destination recovery; exact-head Native AOT and review required. |
| Bounded colored and patterned text decorations | [CSS scope](../css-stylesheets.md#current-source-colored-and-patterned-report-text-decorations), `tests/FactsPDF.Tests/TextDecorationTests.cs`, `tests/FactsPDF.Tests/TextDecorationPaintTests.cs`, [60-entry report](../../examples/report-text-decoration-paint.html) | Real measured PDF underline/overline/line-through glyph paths, optional currentcolor/opaque RGB and solid/dashed/dotted clipped strokes, inline colour and cascade, TrueType/Courier, multipage text, API/CLI byte parity, untouched legacy solid path and protected-output negative/budget recovery. One decoration line only; no wavy/double/thickness/alpha/full browser inheritance. Exact-head CI, separate independent review and human PDF approval remain distinct. |
| Root-relative paragraph/heading `rem` margins | [Scoped contract](../css-stylesheets.md#current-source-root-relative-paragraph-margins), `tests/FactsPDF.Tests/RootRelativeMarginTests.cs`, [32-entry source report](../../examples/report-rem-margins.html), independent [points oracle](../../examples/report-rem-margins-oracle.html), `scripts/verify_stylesheets.py` | Root-computed nonnegative `rem` sides, actual horizontal wrap/box paint, vertical spacing and pagination, mixed auto, cascade and FPDF1201/1202/1302 protected-output recovery; genuine Native AOT PDF parity, final-head CI and independent review separately required |
| Own-font-relative `em` report boxes and margins | [Scoped CSS contract](../css-stylesheets.md#current-source-element-relative-report-box-and-margin-lengths), `tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs`, [32-entry em report](../../examples/report-em-layout.html) with independent [point oracle](../../examples/report-em-layout-oracle.html), `scripts/verify_stylesheets.py` | Own final computed font, real width/min/max/padding/margins, painted PDF geometry and continuation, valid/invalid cascade, bounded FPDF1201/1202/1302 preservation; exact-head managed/Native AOT CI and independent review required. Source only; not a retained candidate or human-approved reference. |
| Bounded paragraph/heading `overflow-wrap:break-word` | [CSS scope](../css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers), `tests/FactsPDF.Tests/OverflowWrapTests.cs`, [32-entry report](../../examples/report-overflow-wrap.html), [manually broken oracle](../../examples/report-overflow-wrap-oracle.html) and `scripts/verify_stylesheets.py` | Measured emergency line splits, legacy byte equivalence for ordinary words, actual Native AOT PDF parity/page progression/searchable content, strict FPDF1201/1202/1302 recovery. No inline/container or direct table-element declarations, `anywhere` or general CSS Text; a flat `p` inside a fixed table cell can opt in. Requires exact-head CI, independent review and audit update; source-only until accepted. |
| Horizontal margins and centred report panels | [Bounded panel CSS contract](../css-stylesheets.md#current-source-centred-report-panels), `tests/FactsPDF.Tests/ReportPanelMarginTests.cs`, [50-entry source report](../../examples/report-panel-margins.html), [independent point oracle](../../examples/report-panel-margins-oracle.html), `scripts/verify_stylesheets.py` | Real `div`/`section`/`article` horizontal margins and `margin:0 auto`, border-box/max-width/pagination and printed PDF X/width, root-`rem`/own-font-`em`, separately bounded nonnegative vertical panel spacing, unsupported list/body/table/inline exclusions and protected-output errors; exact-head managed/Native AOT and independent review required. Source only, not retained preview or customer release. |
| Non-collapsing vertical report-panel margins | [Scoped PDF CSS](../css-stylesheets.md#current-source-real-vertical-report-panel-margins), `tests/FactsPDF.Tests/ReportPanelVerticalMarginTests.cs`, [50-entry report](../../examples/report-panel-vertical-margins.html), [independent spacer oracle](../../examples/report-panel-vertical-margins-oracle.html), `scripts/verify_stylesheets.py` | Real top/bottom spacing on div/section/article with nested/continued painted boxes, root-rem/own-em, cascade, PDF byte parity and preserved output on errors; vertical auto/negative, general margins and collapse unsupported; scoped panel percentages now resolve against containing width, but p/h percentages and direct inherit into text still reject. Exact-head CI, independent review and human page approval separate. |
| Four-sided report margins, horizontal auto centring and print lengths | [Scoped CSS](../css-stylesheets.md), `tests/FactsPDF.Tests/HorizontalParagraphMarginTests.cs`, `tests/FactsPDF.Tests/HorizontalAutoMarginTests.cs`, `examples/report-print-spacing.html` | Bounded explicit-width auto sides, wrapping, painted geometry, heading keeps, cell rejection and safe recovery; exact-head CI/native and independent review still required |
| Bounded CSS list marker kinds | [CSS/list contract](../css-stylesheets.md#current-source-bounded-list-marker-types), `tests/FactsPDF.Tests/ListMarkerStyleTests.cs`, [74-entry report](../../examples/report-list-marker-styles.html), [80-entry vector report](../../examples/report-list-vector-shapes.html), `scripts/verify_lists.py` | Actual source HTML → paginated PDF text marker glyphs and geometry, CSS cascade/important/inheritance, alphabetic/Roman sequence, vector disc/circle/square and marker-free items, managed and Linux Native AOT byte parity with independent expected labels, plus 80-item Linux/Windows native PDF ring/rectangle and Poppler position evidence, and strict FPDF1201/1202/1504/1401 recovery. No custom CSS counters, general list styling, new installable software, release, human PDF approval or commercial grant. Final-head CI and independent review gate merge. |
| Fixed columns, whole rows, repeated headers | [E1/E2 tables](../tables.md), `scripts/verify_table_pagination.py` | Separate from malformed T-table rejection |
| LTR logical side margin/padding longhands (eight spellings) | [Exact CSS contract](../css-stylesheets.md#individual-ltr-logical-marginpadding-sides-eight-names), `tests/FactsPDF.Tests/LogicalReportLonghandTests.cs`, [40-entry report](../../examples/report-logical-longhands.html) and [independent physical PDF oracle](../../examples/report-logical-longhands-oracle.html), `scripts/verify_stylesheets.py` | Individual per-side cascade and !important/CSS-wide behavior; true glyph X, panel paint, page continuation, CLI and Native AOT byte-equivalent PDF, qpdf/searchable text and original-file preservation. Strict physical element/value limits, LTR only; new-source CI/independent review separate from private candidate and human PDF approval. |
| Bounded report `display:none` / `display:block` | [Scoped source](../css-stylesheets.md#current-source-bounded-report-display-suppression), `tests/FactsPDF.Tests/ReportDisplayTests.cs`, [40-entry report](../../examples/report-display-visibility.html), [visible-only oracle](../../examples/report-display-visibility-oracle.html), `scripts/verify_stylesheets.py` | Source and Native AOT verification of actual subtree suppression before PDF layout, same visible text/geometry/pagination/bytes, cascade/important, CLI/API, and strict malformed value/resource/element recovery. Excludes CSS-wide display, inline/flex/grid, HTML `hidden`, lists/tables/images and public release. Exact-head CI and independent review are separate gates. |
| Standalone HTML horizontal report rules | [Scoped separator contract](../css-stylesheets.md#current-source-html-horizontal-report-rules), `tests/FactsPDF.Tests/HorizontalRuleTests.cs`, [44-entry report](../../examples/report-horizontal-rules.html), [independent painted-box oracle](../../examples/report-horizontal-rules-oracle.html), `scripts/verify_stylesheets.py` | Actual 1pt current-colour rule, bounded CSS box/colour/cascade, auto centring, page relocation, searchable text, native PDF-byte parity, limits and protected-output errors; no list/table/paragraph/inline nesting, arbitrary height, new software candidate or human page approval. |
| Scoped LTR `text-align:start/end` for business report text | [Exact CSS contract](../css-stylesheets.md#current-source-ltr-text-align-startend), `tests/FactsPDF.Tests/LogicalTextAlignmentTests.cs`, [46-entry report](../../examples/report-logical-text-align.html) and [physical LTR oracle](../../examples/report-logical-text-align-oracle.html), `scripts/verify_stylesheets.py` | Actual font-measured PDF X coordinates, selector/important/inheritance, TrueType, multipage byte parity, native searchable markers, invalid direction/value/inline recovery. LTR horizontal only; no RTL or vertical writing. Source commit 748867bb7ece222cc552a4d315143572fb1bd642; no new private/public binary, licensing grant or human PDF approval. |
| Static PNG color and transparency | [PNG image resources](../images.md), `scripts/verify_png.py` | Scoped non-interlaced formats; separate engineering evidence |
| Image-only table cells and repeated image headers | [Branded table composition](../tables.md#image-only-cells-and-branded-layouts), `scripts/verify_table_images.py` | Literal geometry/pixels, reuse and actual mutated-PDF rejection; new pages pending reference review |
| Column spans and ordered paragraph/image cells | [Rich and spanning cells](../tables.md#rich-and-spanning-cells), `scripts/verify_table_rich_cells.py` | Authored geometry, margin and border witnesses; new pages pending reference review |
| Oversized body rows with independent cell continuation | [Long descriptions](../tables.md#long-descriptions-across-pages), `scripts/verify_table_row_fragments.py` | Actual text/image/slice geometry, terminal fit and mutation evidence; short tables and fresh-page-fit rows remain whole; new pages pending reference review |
| Heading kept with following paragraph | [F1 heading keep](../heading-pagination.md), `scripts/verify_heading_keep.py` | Existing opt-in paragraph contract |
| Custom physical CLI paper dimensions | `tests/FactsPDF.Tests/CliCustomPaperTests.cs`, `scripts/verify_native_feature_paths.py`, source `271d05350f363857cf2a6d44f1ea40e876f8573c` | Both bounded `--page-width` and `--page-height` are required; no `--page-size` or `--landscape` combination. Real MediaBox, text/page layout, Unicode PNG, page furniture, Native AOT qpdf/Poppler and protected recovery; source-only, no CSS `@page` or customer binary. |
| A4/Letter CLI paper geometry and landscape | `tests/FactsPDF.Tests/CliPageGeometryTests.cs`, `scripts/verify_native_feature_paths.py`, source CLI `bb34436538b8a02f1ace0878be1f97eb5bbff77d` | A4 portrait preserves default byte output; A4 landscape/US Letter portrait or landscape alter true MediaBox and paginated physical coordinates, with direct .NET API parity, Unicode font use, independent native Poppler/qpdf, binary stdout and protected invalid/recovery. Source only until exact-head platform checks and independent review; no `@page`, nonstandard formats, preview binary refresh or human PDF approval. |
| Page counters | [F2 page numbers](../page-numbers.md), `scripts/verify_page_numbers.py` | Existing body geometry remains unchanged |
| Static header/footer and counter composition | [F3 page margins](../page-margins.md), `scripts/verify_page_margins.py` | Explicit literal text; existing margins |
| Styled six-region page furniture, total pages and first-page replacement | [Structured page furniture](../page-furniture-cli.md), `scripts/verify_native_feature_paths.py`, [exact-main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/40283254200) | Real .NET/managed CLI and Linux/Windows Native AOT evidence; HTML header/footer templates unsupported; new pages pending human reference review |
| Combined bilingual 10/30/100-record reports | [F4 executable reports and independent checks](../combined-report.md) | Engineering acceptance; new pages pending user reference review |
| Original styled-CJK input identity and real styles | [Original F4/C1 PR-head execution](https://github.com/KoalaFacts/FactsPDF/actions/runs/40497304548) and [evidence reconciliation](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6448294820) | F4 10/30/100 inputs match Unicode examples and use genuine Chinese regular/bold; **separate C1** exercises four distinct Chinese styles. API/managed/native exact-head byte parity; not a user's changed-page approval |
| Sealed legacy PDF replay | [Managed/native scoped executions](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6443259610) | 52/52 original PDFs (311 pages) byte-identical at previously tested managed and Linux native revisions; no inference of fresh exact-`47bc9791` replay |
| Private installable preview candidate | [Exact merged-main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/40561057964) and [private retention receipt](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6452146222) | Five exact-source components retained/read back privately; real Linux native first-use and safe `FPDF1505` recovery; no public/customer distribution, official signature attestation, page approval or usage-rights grant |

F4 configures four font-role chains for its combined report, but the **original Chinese spans exercise regular and bold only**; the separate C1 styles report supplies positive evidence for all four genuine Chinese faces. F4 also combines nested lists, explicit JPEGs, fixed-column tables, whole-row pagination/repeated headers, heading keeps, page numbers and static page text. Its positive evidence is additional to the original 6 baseline / 12 negative / 9 target cases. New combination PDFs are not automatically frozen as human truth. Current-head user reference approval, remaining release gates, fixed hardware and numerical performance budgets stay separate and incomplete.


### Numeric font-weight source validation (29 October 2026)

The C1 four-role font implementation now also resolves exact CSS numeric hundred
weights 100–900: 100–500 selects the explicit normal chain and 600–900 the
explicit bold chain; italic composition remains the existing independent role.
The `NumericFontWeightTests` real PDF/cascade/page/failure regression suite and
64-entry report against a separately authored 400/700 role oracle are recorded
at source `7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750`. The Native AOT verifier checks a real report,
two embedded faces, searchable text, pagination and protected output for
unsupported/missing-role input. This is supplemental source evidence, not a
new T-bold human approval, private candidate, customer delivery or NuGet release.


## Current-source dashed/dotted business-report borders (2026-11-01)

Report box borders now support dashed/dotted PDF strokes with real measured
thickness, per-side colour/cascade, page-sliced edges and bounded display
commands. This is implemented source-only at `4b5d6da3553579dad75dd00bfb041e1f3c200d90`;
the 76-record report and `ReportPatternedBorderTests` / Native AOT
`verify_native_feature_paths.py` exercise actual paths and protected
failure recovery. Solid rectangles retain original byte output. Unsupported
border styles, round corners, complex effects and full CSS Borders remain
outside scope. Actual exact-head CI, independent review, package retention,
rights and human reference-page approval are **separate gates**, not
asserted by this source audit.

## Current-source list numbering and one-token shorthand (2026-11-01)

Source `bea39e6d6a55f8a9e77ef8156bf6ca6529494738` implements ordered `start` and direct `li value`
within 1–999999, reversed counting with bounded positive range, plus
single-marker-kind `list-style` shorthand. `ReportListControlsTests.cs`
and the [77-entry report](../../examples/report-list-continuations.html)
are checked for PDF bytes, source errors, numbering, page continuity,
font/vector markers and cancellation/recovery. The existing Linux/Windows
native-feature workflow additionally uses independently enumerated
ordinal expectations with Poppler text/position extraction.
Unsupported HTML attributes/targets, numbering underflow, CSS image
and multi-token position shorthands fail rather than render incorrectly.
Exact-head CI and independent review are separate required gates; source
tests cannot approve new human reference pages or relabel the historic
private `0.1.0-preview.1` binary.
