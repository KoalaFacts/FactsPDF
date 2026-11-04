## Clickable PDF references in controlled report templates (source-only)

The new bounded `a[href]` text path emits real searchable glyphs and page-local
PDF `/Link` URI annotations after actual layout, including repeated references
across page breaks and permitted fixed-column table-cell text. Initial blue
underlined link text can be overridden by supported CSS cascade. Only validated
absolute ASCII HTTPS without credentials and plain single-address mailto
are accepted (2048-character maximum). Missing/unsafe href, unsupported block
nesting/attributes and output-budget violations fail with their existing
explicit error codes and preserve the prior output. No linked image, browser
navigation, file/javascript/HTTP URLs or fetching of targets is provided.

See [sample](../examples/report-clickable-orders.html),
[PDF/CLI/NUnit evidence](../tests/FactsPDF.Tests/ReportHyperlinksTests.cs) and
[real Linux/Windows Native AOT verification](../scripts/verify_stylesheets.py).
Engine implementation source `9a19096a69f323bda3c7678c590a96b19ad98864`;
actual CI, independent review, distribution rights, retained private candidate
and human PDF acceptance are separately gated.

## Print-style template fit — bounded media blocks (source-only)

Controlled business templates may place existing supported styles in
`@media print { ... }` or `@media all { ... }` inside an embedded `<style>`.
These apply to actual PDF text/paint/layout and share the existing CSS
cascade and budgets. Before integration, reject templates relying on
`screen`, condition expressions, media lists, nesting, `@supports`,
`@container`, `@import` or `@page`; they are not silently ignored.
[Example 44-record printable report](../examples/report-print-media.html) and
[independently authored inline control](../examples/report-print-media-oracle.html)
exercise scope. Source `70f37e7e881bade30851ecaa06528a27813f0403`;
no public package, human page approval or licence is implied.

## LTR logical border components for printable reports (source-only)

Existing `border-inline` / `border-block` shorthands now also have scoped
`border-inline-width:2pt 4pt`, `border-block-style:dotted solid`,
`border-inline-color:red blue` and individual logical-side
`-{width,style,color}` variants: **18 names** total. LTR logical
start/end maps to physical left/right/top/bottom, with true PDF stroke
and text geometry, independent cascade, bounded opaque colours and
cross-page paint. Pair values may have one or two terms; a single-side
longhand takes one; CSS-wide keywords only stand alone. Invalid/unmatched
declarations fail closed, not silently omitted. No RTL, vertical text,
other border component families, alpha or expanded element applicability.

See the [44-entry print report](../examples/report-logical-border-components.html)
and [physical longhand control](../examples/report-logical-border-components-oracle.html);
the new NUnit, CLI/TrueType and Linux/Windows Native AOT qpdf/Poppler
checks exercise real PDF bytes, text positions and protected recovery.
Source `99328f071395d5d7cc1eedad9138f05ee118fb0f`; CI, review, prior private package, human approval
and distribution rights are distinct.

## LTR logical border report styling (source-only)

Existing report cards can use `border-inline:2pt solid #315f82`,
`border-block:1pt dashed teal`, and independent
`border-inline-start/end` / `border-block-start/end` shorthands.
In the documented LTR horizontal subset these map to the real painted
left/right/top/bottom PDF edges, including width contributions to body
geometry, cascading `!important`, inheritance and cross-page fragments.
All six names accept only existing physical-border width/style/opaque-colour
terms or a standalone CSS-wide keyword. No RTL, vertical writing,
logical border component properties, alpha or general border CSS.
The [44-row native report](../examples/report-logical-borders.html) and
[physical-side oracle](../examples/report-logical-borders-oracle.html)
exercise PDF-byte, text position and protected recovery via NUnit, CLI and
qpdf/Poppler. Source `6916ef5da96068c167690ef3a0b5f1c94d41f255`;
CI, independent review, prior private preview, human approval and
distribution rights remain independent.

## Standard opaque CSS named colours for printable reports (source-only)

## Computed currentColor report-source evidence (2026-11-04)

Source `3dbcded4f5d39b16b79bbff7aac4132305659d4a` adds a bounded CSS colour value, not a new property:
`color:currentColor` reuses inherited computed parent text ink;
`background[-color]:currentColor` paints the final element text ink. Existing
border/decorations retain their currentColor support. The
[48-entry multi-page report](../examples/report-current-colour.html) has a
[separate fixed-HEX control](../examples/report-current-colour-oracle.html) for
PDF-byte and text-geometry comparison. API/managed CLI/TrueType, CSS
cascade, inheritance and target-output-preserving failures are covered by
source regressions; native Linux/Windows AOT verification uses qpdf/Poppler.
No transparency, other CSS Color, images, gradients or changed element
applicability. Current-head CI and genuine independent review, as well as
private candidate, human PDF approval, licensing and release, require
independent actual evidence; source submission alone proves none of them.


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

## Bounded opaque HSL report colours (source-only)

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

## Source-only report PDF document properties

Use `--title`, `--author`, `--subject`, `--keywords`
(or `PdfOptions.Title`, `Author`, `Subject`, `Keywords`)
for optional Unicode PDF Info metadata. These are UTF-16BE with BOM,
independent of HTML and fonts, and visible to all PDF recipients.
Each field is limited to 1024 UTF-16 units, must be nonblank, single-line
and well-formed. No timestamps, implied metadata, XMP, or document
tags are added. Without metadata flags the PDF stays byte-identical;
invalid/repeated CLI values return code 2 without overwriting an
existing PDF. The metadata contributes to the usual input/output budgets.

`CliPdfMetadataTests.cs` validates classic and embedded Unicode font/PNG
API/CLI PDF bytes, real multipage output, Info trailer linkage, error
recovery and size limits. Linux/Windows Native AOT
`verify_native_feature_paths.py` checks `pdfinfo` fields,
qpdf integrity, original Poppler page coordinates and protected retry.
This is source capability only: not a new private/public candidate,
human-approved output or customer grant.

## Current-source custom physical report paper

For A5 forms or company stationery without a browser/printer dependency, use:

```sh
dotnet run --project src/FactsPDF.Cli -- examples/first-document.html a5-report.pdf --page-width 148mm --page-height 210mm --margin 12mm
```

Supply both dimensions in invariant-culture `pt/px/mm/cm/in`, each within
72–14400pt. The dimensions are actual PDF MediaBox geometry used by the
renderer for wrapping, page breaks, images, headers/footers and PDF coordinates.
A4/Letter defaults are unchanged when the new pair is absent. Custom dimensions
cannot be mixed with `--page-size` or `--landscape`, and values that cannot
fit the physical page or selected uniform margin fail. Missing/repeated/invalid
options exit 2 without replacing an existing PDF; a later valid retry succeeds.
Source NUnit independently compares CLI and public .NET API PDFs, including
multipage geometry, Unicode font/PNG resource and default byte parity.
The existing Linux/Windows Native AOT gate now also verifies qpdf, Poppler
all-page paper/text bounds, complete text, page furniture, stdout byte parity
and negative recovery. Scope is CLI-only (not CSS `@page`, asymmetric margins,
bleed or general document printing), introduced in source commit
`271d05350f363857cf2a6d44f1ea40e876f8573c`. No new sealed
software candidate, customer evaluation rights or public release is implied.

## Current-source uniform physical CLI report margins

First-use command-line reports now take `--margin 15mm` with the existing
`--page-size a4|letter` and `--landscape` switches. The CLI computes one
uniform PDF margin in points (also accepts `0`, `pt`, `px`, `cm`, `in`) before
body and page-furniture layout, so PDF glyph positions and page counts
change rather than just accepting the argument. Default 36pt and an
explicit `--margin 36pt` produce identical bytes. Invalid/repeated/unfit
margins exit 2 without overwriting an existing destination, including
after a later successful retry. NUnit compares real CLI pages with an
independent public-API invocation; the existing Linux/Windows Native AOT
gate uses qpdf and Poppler for physical margins, content searchability,
all-page print-area positions and failure recovery. Not CSS `@page`,
individual-edge margins, bleed handling or a released/customer licence.
See `CliPageMarginTests.cs` and `scripts/verify_native_feature_paths.py`.
## Source-only bounded image min/max-height report layout

Physical `min-height`/`max-height` now constrain directly bound standalone
JPEG/PNG and direct fixed-column cell images in the actual PDF matrix.
A vertical cap/minimum resizes auto-height images in native aspect ratio;
explicit height permits independent width. Border-box and contradictory
limits retain existing CSS cascade and failure-protection rules.
[36 independent report records](../examples/report-image-height-limits.html)
are checked against a [physical PDF control](../examples/report-image-height-limits-oracle.html)
through the shared native CLI/qpdf/Poppler evidence gate, source NUnit and
protected output recovery. Percent/em/rem/calc, unknown content resources,
impossible width/height intersections, and arbitrary object-fit are unsupported.
The retained preview, human PDF reference, actual CI, independent review and
commercial grants remain separate.

## Source-only image width constraints for report logos

Authors who bind a PNG/JPEG explicitly can write `max-width:100%` on an
`img` to cap its actual PDF matrix to a parent card or a fixed-column table
cell; `min-width` and opt-in `box-sizing:border-box` also use the real
image/padding/border geometry while preserving pixel aspect ratio.
A [36-record multipage specimen](../examples/report-responsive-images.html)
is compared byte-for-byte against an
[independently authored point-width projection](../examples/report-responsive-images-oracle.html)
in the shared Native AOT, qpdf and Poppler gate. Invalid/unfittable input
does not replace previous PDFs. No automatic shrink, independent height,
object-fit, external URL fetch, rem/em image widths, flex/grid, or new
human-approved/customer binary. Source `b6bebe8e3b97f13190148c6f53bed49ec2678ede`;
CI and independent review are separate.

## Source-only scoped break-all report identifiers

Author whole paragraphs/headings with `word-break:break-all` when narrow
invoice columns should fill a line with as many ordinary glyphs as actually
fit; unlike emergency `overflow-wrap:break-word`, a word that would fit on
a new line may split at the remaining PDF width. The identical source text,
exact PDF glyph positions, multi-page order, bounded table-cell paragraphs
and protected failure recovery are exercised by
[48-record report](../examples/report-word-break-identifiers.html),
[manually broken PDF control](../examples/report-word-break-identifiers-oracle.html),
`WordBreakTests.cs` and the Native AOT/Poppler/qpdf gate.

Only `normal/break-all`, explicit paragraph/heading scope and already
supported simple horizontal glyphs. No whole-page CSS Text engine,
`keep-all`, hyphenation, grapheme shaping, inline span declarations or
silent clipping. Source ac6cec7000b308b403dd95abcd14441240b1f791 is separate from final-head CI,
independent review, the retained private preview and human/customer rights.

## Source-only fixed-height inspection cards

Business reports may size ordinary panels using `height:85pt`,
`box-sizing:border-box` and the existing `min-height` lower bound.
This is a rendered page-box height, not only accepted CSS: body text,
background and border geometry, subsequent row placement and fresh-page
preflight all follow the chosen bound. If the panel would need more
space or a continuation page, conversion fails safely rather than
clipping text or overwriting the original output.

The [40-card native report](../examples/report-fixed-height-cards.html),
[independent short-card oracle](../examples/report-fixed-height-cards-oracle.html),
source NUnit and Native AOT/qpdf/Poppler verification cover actual PDF
geometry, resource isolation, output restoration and API/CLI behavior.
No percentages, general `max-height`, automatic overflow,
list/table/image/inline use or broad CSS Sizing. Source work
`9f141fbc33bb2c4a3764b12c6c8c52b8c2bd00bc` is distinct from
the retained software candidate, exact-head CI, independent review,
human page approval and customer rights.
[CSS contract](css-stylesheets.md#current-source-fixed-height-report-panels).

## Source-only intact approval cards

`break-inside:avoid` and the legacy `page-break-inside:avoid` now
preflight one-to-eight direct nonempty text paragraphs/headings inside a
top-level ordinary report panel. When the true wrapped PDF box cannot fit,
the complete card begins on the next page, including painted borders.
Oversize and unsupported structures fail without overwriting an earlier PDF.
This is a source-only scoped card helper, **not** generic break-inside or
CSS Fragmentation. Actual PDF byte parity with an independently authored
forced-page control, searchable/native page text, invalid-input recovery,
and Linux/Windows Native AOT are exercised by the
[card template](../examples/report-keep-together.html),
[control](../examples/report-keep-together-oracle.html) and
[limited CSS rules](css-stylesheets.md#current-source-keep-together-business-report-cards).
Source first implemented `ecdef6d8745caa3d2b527197c5767e25f89ba6b0`;
the retained preview is older, and exact-head CI, separate review, human
approval and customer usage remain independent.

## Source-only report-section page boundaries

Controlled facilities, billing and inspection reports can place whole ordinary
panels on fresh PDF pages using `break-before:page` /
`break-after:page`, or `page-break-before/after:always` in legacy print
stylesheets. Real PDF bytes, page count, searchable text and outer panel
paint are exercised by `SectionPageBreakTests.cs` and a
Linux/Windows Native AOT CLI witness comparing the
[facilities sample](../examples/report-section-page-breaks.html) with an
[independently authored modern-break oracle](../examples/report-section-page-breaks-oracle.html).
Nested continuation, CSS cascade, invalid/unsupported values, protected
output and recovery are included in this bounded scope. This is a **print
page boundary**, separate from the newly scoped, top-level flat-card
`break-inside:avoid`; it does not add table/list/inline pagination or
full Paged Media. Source `67566764419232585632d8ea6f2106aeeeff2f70`
is not the previously retained private candidate or a human-approved/customer
deliverable; exact-head CI and separate independent review remain required.

## Source-only independent report-card corners

Four physical `border-*-radius` longhands now control their own corners,
with real PDF Bezier backgrounds and uniform solid border rings rather
than a parse-only alias. The existing [50-card shorthand example](../examples/report-asymmetric-rounded-panels.html)
is the native PDF-byte comparison for the new
[50-card longhand report](../examples/report-corner-longhands.html);
NUnit asserts per-corner cascade, native PDF positioning, pagination and
protected failure recovery. Single-radius legacy output remains unchanged.
One value per longhand, zero or 0.25–72pt physical length, on ordinary
report blocks only: no elliptical/percentage/em/rem/inline/table/list/image
rounding or asymmetric/patterned rounded border rings.
[CSS scope](css-stylesheets.md#current-source-rounded-business-report-cards-2026-11-01).
Current source is not the previously retained private binary, customer
license, release, or human-approved PDF reference.

## Source-only minimum-height business report cards

On a controlled invoice, inspection or status report, ordinary panels outside
tables/lists can use `min-height:90pt` to reserve a consistent visible area
for backgrounds/borders and keep following rows aligned. The minimum changes
actual PDF vertical space and respects `box-sizing`, `em/rem` computed
fonts, CSS cascade, existing page movement and failure protection. See
[the inspection example](../examples/report-min-height-cards.html),
[40-card native witness](../examples/report-min-height-cards-native.html)
and [bounded CSS contract](css-stylesheets.md#current-source-minimum-height-report-panels).
Percentages and arbitrary height/max-height, table/list/image/inline targets,
unplaceable panels and full CSS Sizing are not accepted; a separately bounded
one-page fixed-height panel mode is documented above. This is only a
source-level capability until exact-head checks, independent code review,
human PDF approval and distribution rights are separately satisfied.

## Source-only rounded invoice/status cards

Supported ordinary business report panels can use `border-radius:12pt`
or distinct `border-radius:12pt 9pt 6pt 3pt` corners
for real opaque vector PDF background and a single-colour uniform solid border,
including continued page fragments. See the [54-card report](../examples/report-rounded-panels.html),
[physical-unit oracle](../examples/report-rounded-panels-oracle.html),
[50-card four-corner sample](../examples/report-asymmetric-rounded-panels.html)
and [independent unit projection](../examples/report-asymmetric-rounded-panels-oracle.html) and
[limited CSS contract](css-stylesheets.md#current-source-rounded-business-report-cards-2026-11-01).
No elliptical, percent, table-cell, list, image, patterned-border or complete
CSS Borders rendering is implied; invalid input fails without damaging the
existing PDF. This source functionality is not a new installable candidate,
release, customer licence or human-approved reference.

## Source-only fixed-column table-cell vertical alignment

For commercial report tables with multiline notes or brand marks,
`td/th` now support bounded `vertical-align:top/middle/bottom`
within intact rows; cell contents move without shifting painted
borders/backgrounds. See the [table contract](tables.md#vertical-alignment-in-whole-table-rows)
and [40-row native sample](../examples/report-table-vertical-align.html).
Body rows requiring cell-by-cell continuation reject middle/bottom.
This source change is not a new retained candidate, human-approved
reference page, public release or customer use grant.

# Evaluate FactsPDF for business reports

Current source adds bounded physical `height:36pt` for explicitly bound
standalone or direct fixed-column image resources, with measured PDF image
matrices and strict protected failures. [Image height contract](images.md#explicit-physical-image-heights).
Source `7afae6b56ba8ddbaad2f434cfb4a9d35f8b7a2dd` is not the held installable preview or an automatic
customer production licence.


FactsPDF converts a documented HTML/CSS subset into searchable PDF documents
without a browser rendering engine. Its first delivery scope is bilingual business
reports for .NET applications and command-line integrations.

The current build is experimental. Evaluate a specific template and deployment
environment before making a production commitment. The source declares
`0.1.0-preview.1`. Five real source-bound software components are now
privately retained by the owner after independent verification, but **none
has been authorised for customer installation or distribution**. No public
CLI/NuGet release or general evaluation grant exists. The commands below use
a source checkout and do not themselves grant evaluation or commercial rights.

**Working with an existing reporting template?** [Open a template-fit request](https://github.com/KoalaFacts/FactsPDF/issues/new?template=report-template-fit.md). Share a redacted layout description, not customer documents or credentials. A public request starts a scope discussion only; see licensing below before building or running FactsPDF.

Source-authored first-line insets can now use `text-indent:0.25in` or `text-indent:15%` on explicit paragraphs/headings outside table cells, with real PDF wrapping and retained background geometry. Only the first logical line moves; `FPDF1302` protects the destination if a first word cannot fit after indentation. This source behavior needs current-head native/managed verification and does not change the historical retained private candidate.

Current source accepts bounded font-scaled `text-indent:0.4em`,
`letter-spacing:0.1em` and `word-spacing:0.15rem` for controlled
report text. These are measured and painted glyph advances, not just
accepted CSS tokens. A [72-entry sample](../examples/report-relative-text-spacing.html)
and separately authored [point oracle](../examples/report-relative-text-spacing-oracle.html)
exercise positioning/pagination; Native AOT verification covers
byte parity, text and failure recovery. This source ability is absent
from the older retained installable candidate and does not imply full
CSS Text or customer distribution authority.

Controlled `div`, `section` and `article` report panels can now use
horizontal `margin:0 auto` for centred, nested painted PDF boxes
across pages, including `max-width`. The
[50-entry sample](../examples/report-panel-margins.html) and
[independently authored point-value oracle](../examples/report-panel-margins-oracle.html)
exercise real geometry and recovery; panel top/bottom margins are
zero-only. This is **source-only** and does not change the retained
private candidate, new-page approval, customer distribution rights or
supported CSS surface beyond [the bounded scope](css-stylesheets.md#current-source-centred-report-panels).

Nonnegative `margin-top`/`margin-bottom` on `div`, `section` and
`article` now create real outside-the-border report-panel spacing, alongside
existing horizontal `margin:0 auto`. A [50-row report](../examples/report-panel-vertical-margins.html)
and independent [fixed-spacer oracle](../examples/report-panel-vertical-margins-oracle.html)
check PDF geometry, text and page continuation; source-only, not a retained
candidate, general CSS margin collapse or customer release.
[Scoped details](css-stylesheets.md#current-source-real-vertical-report-panel-margins).

Source `0518b51d7a34457798c748db6753c4270ce20996` also supports bounded nonnegative vertical
percentage margins on ordinary report panels and standalone hr,
measured against the **containing content width**. A 50-entry HTML
report with an independently authored physical-point PDF oracle,
targeted NUnit layout/recovery tests and Linux/Windows Native AOT
qpdf/Poppler validation paths exercise actual font/text placement,
painted geometry and multi-page output. Paragraph/heading vertical
percentages and direct percentage-panel inheritance still reject rather
than reinterpret units. No general margin collapse, customer package,
human approval or commercial rights are implied.

### Current-source report display suppression

Templates can use `display:none` to remove a whole validated ordinary report
block before PDF layout, including invisible words, paint commands, margins and
pagination space. `display:block` preserves existing block behavior.
Scope: `p`/`h1`–`h6`, `div`/`section`/`article` and `blockquote`,
outside lists/tables. The [40-entry source report](../examples/report-display-visibility.html)
has a [separately authored visible-only oracle](../examples/report-display-visibility-oracle.html)
for real PDF byte/text/page parity, with Native AOT and output-preserving
negative acceptance. Selectors and `!important` use the ordinary cascade;
hidden invalid CSS, markup and resources still reject. Not general CSS
display, a hidden attribute implementation, a release or an evaluation grant.
[Complete source boundary](css-stylesheets.md#current-source-bounded-report-display-suppression).

Controlled LTR reports may use `margin-inline`, `margin-block`,
`padding-inline` and `padding-block` instead of physical side
declarations. These one/two-value shorthands affect real painted box
position, measured text and page continuation; they preserve existing
invalid-input protection and are **not** RTL, vertical-writing or general
CSS logical-property support. Compare the [36-entry report](../examples/report-logical-spacing.html)
with its [physical-longhand oracle](../examples/report-logical-spacing-oracle.html).
This is current source, not a newly authorised installable candidate.
[Exact scope](css-stylesheets.md#current-source-ltr-logical-report-spacing).

The same LTR report subset now also accepts **eight individual logical side
longhands** such as `margin-inline-start:auto`, `margin-block-end:8pt` and
`padding-inline-start:6pt`, each mapped to the corresponding existing
physical side before actual CSS cascade. The [40-entry source HTML](../examples/report-logical-longhands.html)
and [separate physical-side oracle](../examples/report-logical-longhands-oracle.html)
are compared using real paginated PDF/Native AOT output and invalid-target
recovery. This does not enable RTL, vertical writing, unsupported table/list
contexts, automatic margin collapsing or a new installable software candidate.

Controlled report quotations now accept `<blockquote>` as a true inset,
paginated block: default 8pt vertical and 24pt horizontal margins, plus
ordinary report-panel padding, measured text, solid borders and backgrounds.
The [44-item HTML example](../examples/report-blockquotes.html) has a
[separate physical-container PDF oracle](../examples/report-blockquotes-oracle.html);
strict invalid-input recovery remains in place. This is not full HTML citation
semantics or customer-ready package distribution, and `cite`, lists/tables
containing quotes, unsupported CSS and arbitrary nested content remain
outside the tested contract. See the [scoped support](css-stylesheets.md#current-source-bounded-quoted-report-panels).

Current-source maintenance checklists and corrective action lists can
also use bounded `list-style-type` on `ol`, `ul` or `li`:
alphabetic or Roman PDF number glyphs, decimal, filled disc,
hollow circle, solid square or no marker. Real glyph advances, per-list gutter width, cross-page marker
attachment, CSS cascade and unsupported-input recovery are exercised
by the [74-entry report](../examples/report-list-marker-styles.html)
and `ListMarkerStyleTests`, with a Native AOT PDF text/geometry
witness in `scripts/verify_lists.py`. The separate
[80-item vector marker report](../examples/report-list-vector-shapes.html)
checks PDF ring/rectangle commands and body geometry in real Linux/Windows
native executions via `scripts/verify_native_feature_paths.py`. The feature is not custom
CSS counters, arbitrary marker imagery, a release, a private-candidate
rebuild, a new licence or a reviewed human PDF reference. See
[bounded markers](css-stylesheets.md#current-source-bounded-list-marker-types).

### Bounded source-only action-list numbering

Existing `ol` report lists now support `start="21"`, `reversed`
and an individual direct `li value="43"` to continue or restart
numbered work-order steps. A single marker-kind `list-style` shorthand
uses the implemented list-style-type layout; it is not support for list
images, CSS counters or inside/outside. The new
[77-entry pagination report](../examples/report-list-continuations.html)
and [contract](lists.md#current-source-ordered-list-sequencing-and-one-token-shorthand)
cover actual PDF numbering, strict failure and old-output preservation.
There is no new binary, customer evaluation grant, human PDF approval or
package publication from this source addition.

## Preformatted operations and reconciliation notes (current source)

Templates requiring aligned register lines can use an explicitly closed
`<pre>…</pre>` block or `white-space:pre` on an explicit paragraph/heading.
Unlike `pre-wrap`, these preserve U+0020 spaces and authored line breaks
**without soft wrapping**. Line content is measured against the report's
actual PDF column width, can paginate at authored breaks and may use explicit
fonts; a line that does not fit fails safely. Tabs, pre within tables/lists,
nested block content and browser-complete pre semantics remain unsupported.
See the [72-line audit template](../examples/report-preformatted-audit.html),
[independent PDF oracle](../examples/report-preformatted-audit-oracle.html)
and [precise contract](css-stylesheets.md#current-source-preformatted-audit-blocks).
This source feature is not included in the retained private preview binary
and supplies no customer evaluation or production grant.

## Start with a report that fits

Long invoice/reference identifiers can opt into scoped `overflow-wrap:break-word` on report paragraphs/headings, including flat p inside fixed-column cells. Oversized tokens split at measured glyph boundaries and continue across pages, while ordinary words still move whole and `white-space:nowrap` takes precedence. `anywhere`, complex shaping, automatic hyphenation and full CSS Text are outside scope. The [32-entry report](../examples/report-overflow-wrap.html) has a separate [manual-break oracle](../examples/report-overflow-wrap-oracle.html) and protected-output errors. Source-only, not a customer grant or human PDF approval. See the [scope](css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers).

The source report-print example now exercises measured soft-wrapped word-space `text-align:justify` (not final/explicit-break lines or RTL/CJK full justification). It also demonstrates whole-paragraph `white-space:nowrap` for short references (overflow fails rather than breaking/clipping), plus border-box panel sizing and percent/em/physical line-height with true computed inheritance, parent-relative percentage/em font sizes, opaque CSS RGB-function colours across text, panel backgrounds and solid borders, alongside spacing and decorations. The current engine accepts only integer `rgb()` channels and fully opaque comma-form `rgba()`; it rejects semi-transparent colour rather than silently flattening it. See [the scoped CSS contract](css-stylesheets.md#current-source-opaque-rgb-colours-for-reports). Relative `font-size:125%`/`1.125em` resolves against the computed parent with a 1–144pt result; this is distinct from the later scoped `em` report box/margin source slice; neither is a general CSS layout promise. These new source abilities are **not** part of the earlier privately retained binary candidate.

For print templates with a root type scale, the source report can use
`html{font-size:15pt}` together with `font-size:1.2rem` and
`line-height:1.5rem`. The computed `html` root is the base for
`rem`; in documents with no explicit `html`, `PdfOptions.FontSize`
is the fallback. Both properties alter actual PDF glyph and page geometry
with strict invalid-input recovery. A separate scoped source feature permits
`rem` for ordinary report `width`/`min-width`/`max-width` and padding,
including real centring, wrapping and page fragments; a separate bounded
source implementation supports `rem` on paragraph/heading margins outside
tables. Other container margins, borders, general text-spacing values, images/tables and
complete CSS Values remain unsupported; only scoped text-indent/letter-spacing/word-spacing em/rem are implemented. See the
[runnable multi-page typography example](../examples/report-root-relative-typography.html)
and [scoped CSS contract](css-stylesheets.md#current-source-root-relative-report-typography);
the historical private candidate does not contain this new source change.

The existing combined report examples exercise these capabilities together:

- English and Chinese text, with explicit normal, bold, italic and bold-italic
  font chains and ordered character fallback.
- Headings, paragraphs, nested blocks, backgrounds, solid borders and lists
  nested up to two levels.
- Explicitly supplied baseline JPEG images.
- Fixed-column tables with whole-row pagination and repeated headers.
- Heading/first-paragraph keeps, page numbers and static single-line page text.

The newer [branded business report](../examples/branded-business-report.html)
adds a transparent PNG logo beside company details using image-only table cells.
The [detailed service report](../examples/business-table-rich-cells.html) adds
column spans and ordered paragraphs, images and captions inside a cell.
The [long service report](../examples/business-table-long-description.html)
continues one oversized description across pages, with repeated table headings,
single-occurrence item values and an intact image.

[Read the combined-report guide](combined-report.md) for the exact input and
acceptance contract. A template that fits these constraints is a useful first
evaluation. Existing templates may need changes to fit the supported subset.

| Example | Purpose | Resources |
| --- | --- | --- |
| [First document](../examples/first-document.html) | Run the shortest end-to-end conversion | ASCII/Courier; no external font files |
| [Print spacing report](../examples/report-print-spacing.html) | Business panel with computed line-height, a bounded nonwrapping invoice reference, physical print units, four-sided margins, auto-centred summary, and real strike-through/underline labels | ASCII/Courier; no external resources |
| [Root-relative typography report](../examples/report-root-relative-typography.html) | 63-record paginated service report using `font-size:rem` and `line-height:rem`, with an independently authored [fixed-point layout oracle](../examples/report-root-relative-typography-oracle.html) | ASCII/Courier; no external fonts or assets |
| [Root-relative report boxes](../examples/report-rem-box-lengths.html) | 69-record paginated business report using root `rem` width, min/max and padding with border-box painting, compared against a separately authored [fixed-point oracle](../examples/report-rem-box-lengths-oracle.html) | ASCII/Courier; no external resources; not part of retained candidate |
| [Root-relative report margins](../examples/report-rem-margins.html) | 32-entry paginated service report with real paragraph/heading `rem` margins and a separate [fixed-point oracle](../examples/report-rem-margins-oracle.html) | ASCII/Courier; no external resources; source-only |
| [Element-relative report box layout](../examples/report-em-layout.html) | 32-entry paginated service report with element-font-relative `em` widths/min-max/padding and paragraph/heading margins; [independent point projection](../examples/report-em-layout-oracle.html) | ASCII/Courier; source-built only, not an installable customer candidate |
| [Case labels report](../examples/report-case-labels.html) | Actual upper/lowercase ASCII invoice and status labels; a proper-name span overrides inherited casing | Courier; no external resources |
| [Branded business report](../examples/branded-business-report.html) | Transparent logo beside company details, line items and page furniture | Generated synthetic PNG and explicit image binding; no external fonts |
| [Detailed service report](../examples/business-table-rich-cells.html) | Spanning headings/totals and paragraph/image/caption detail cells | The same generated synthetic logo; no external fonts |
| [Long service report](../examples/business-table-long-description.html) | One long description continues across pages; a following item and totals remain in order | The same generated synthetic logo; no external fonts |
| [30-record combined report](../examples/combined-report-30.html) | Review typography, lists, image, table and page furniture together | Four explicit font roles, JPEG binding and page options |
| [100-record combined report](../examples/combined-report-100.html) | Review repeated table headers and longer pagination | The same resources as the 30-record report |

The combined-report corpus defines two pages for the 30-record example and six
for the 100-record example under its documented configuration. The guide links
the executable checks. New output references still require the existing page
review; the example names are not evidence that a particular build was approved.

## Generate the first PDF

First check the [licensing guide](../LICENSING.md) for your organization's
evaluation rights. Organizations outside Community eligibility need an agreed
written evaluation or commercial authorization before building or running the
engine.

From a source checkout with the repository's .NET 10 SDK:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

To print a wide A4 operations report or a US Letter report, use the
source CLI's bounded physical paper controls:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html a4-landscape.pdf --page-size a4 --landscape
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html us-letter.pdf --page-size letter
```

The default remains A4 portrait at 595.28 × 841.89 points. Letter is
612 × 792 points; landscape swaps those axes. These affect real PDF
MediaBox dimensions, layout, text flow, page furniture and page count,
not browser viewport CSS or `@page`. Invalid/repeated options return a
CLI argument error before replacing an existing PDF, including with
`--overwrite`. Source CLI implementation: `bb34436538b8a02f1ace0878be1f97eb5bbff77d`;
not in the earlier retained candidate or a public release.

Open `first-document.pdf`. The input and output paths are explicit. CLI
diagnostics go to stderr, and a conversion limitation returns a nonzero exit
code. Existing output files need `--overwrite`; failed conversions preserve the
existing target under the documented CLI contract.

For a direct .NET integration, use the
[external consumer example](../examples/first-use/Program.cs) and the
[local-package first-use recipe](development.md#first-use-from-local-artifacts).
The example consumes the packed library without inheriting the repository's
build configuration.

## Add multilingual text and styles

Supply appropriately licensed static TrueType fonts. FactsPDF does not bundle
fonts or search the operating system for them. A simple bilingual document uses
a Latin font followed by a Chinese fallback:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

These paths are placeholders for files you provide. For bold and italic content,
configure the corresponding independent font roles; a normal font chain does
not provide them automatically. Use the
[explicit font-style guide](font-styles.md) and the complete
[combined-report command](combined-report.md#run-a-report) for those examples.
Missing resources produce explicit diagnostics.

## Check template fit before integration

| Area | Supported evaluation scope | Current boundary |
| --- | --- | --- |
| Tables | Explicit fixed columns, column spans, ordered paragraph/image cells, repeated single-row header, oversized body rows split at text-line/image boundaries | Short tables and rows that fit a fresh page stay whole; no automatic widths, row spans, nested tables or split images/headers |
| Images | Explicitly bound baseline JPEG and supported static PNG, including alpha/tRNS; standalone or direct image items in cells | No SVG, APNG, interlaced/16-bit PNG, inline/list images, text wrapping around images or automatic URL loading |
| Fonts | Explicit static TrueType roles and character fallback | No automatic family discovery, synthetic styles, full shaping or RTL |
| Page furniture | Legacy literal header/footer and current page number, plus structured left/centre/right styled regions with actual total page count and first-page replacement via .NET/CLI | No HTML header templates or general CSS Paged Media |
| Layout | Documented block/text subset with print-unit lengths (`mm/cm/in`) and real horizontal/vertical paragraph/heading margins and explicit-width `margin:0 auto` centring; bounded `min-width`/`max-width` on ordinary report blocks drive actual width/centred panels | No negative/vertical-auto or general container/cell paragraph horizontal margins, flex/grid, CSS Paged Media or arbitrary webpage conversion |
| Integration | .NET library, CLI; actual Linux/Windows x64 Native AOT evidence | Node subprocess and browser WASM are experimental probes; other language SDKs remain planned |

For precise values and diagnostics, see [CSS](css-stylesheets.md),
[tables](tables.md), [images](images.md), [page text](page-margins.md) and the
[engineering support matrix](acceptance/support-matrix.md).

## Discuss an evaluation or commercial arrangement

FactsPDF is licensed under [PolyForm Small Business 1.0.0 or PolyForm
Noncommercial 1.0.0](../LICENSE.md). Companies with fewer than 100 people and
under USD 1,000,000 (2019, CPI-adjusted) revenue, counting affiliates, may use
it free in proprietary commercial applications; noncommercial, charitable,
educational and government use is also free.

Other uses, including evaluation by larger companies, require a separately
agreed authorization from Koala Facts Trading Pty Ltd. Commercial arrangements
can cover agreed application, hosted-service or redistribution needs.
Application code, templates and generated PDFs do not become public by
using FactsPDF.

[Start a licensing enquiry](../COMMERCIAL.md#contact-and-safe-handling). Use the
initial public issue only to request a private discussion; keep customer
documents and business details in an agreed private channel.

A useful pilot proposal identifies:

1. One target deployment environment and the integration owner.
2. Up to three agreed, appropriately shared report templates.
3. Required output, fonts, record counts, page behavior and failure handling.
4. Specific deliverables and written acceptance criteria.
5. Evaluation/use rights, duration, support scope, price and production-license
   treatment in an accepted agreement.

This is a proposed pilot structure, not a standard purchased entitlement.
[Commercial details](../COMMERCIAL.md) and the existing
[release requirements](licensing-review.md) continue to govern orders and
software distribution.

## Delivery status

**Engineering checkpoint (28 October 2026):** [PR #72](https://github.com/KoalaFacts/FactsPDF/pull/72)
merged the first-use `FPDF1505` safe recovery guidance. The resulting
`main` source is `47bc97910843ad6f72f11a285ec5b74e13862623`.
Its actual [CI 40561057964](https://github.com/KoalaFacts/FactsPDF/actions/runs/40561057964)
passed installed .NET API/managed CLI and Linux/Windows x64 Native AOT
execution and recovery checks after a targeted retry of a pre-compilation
MSYS2 download failure. Six other applicable main workflow groups passed.

The owner separately authenticated the exact-main CI encrypted transport and
privately retained **five real software components**: the NuGet package,
managed CLI ZIP, Linux x64 Native AOT CLI ZIP, examples ZIP and corresponding
source archive. Each was independently checked against its source-bound
checksum and read back from private storage. An offline run of the retained
Linux native CLI generated a two-page searchable PDF and verified destination
preservation; an actual restricted-embedding font produced `FPDF1505` and the
new licensing-safe diagnostic. Windows native and installed managed/API
execution are evidenced by exact-main CI, not by a claimed local rerun.
Fonts are not bundled.

Historically, all **52 sealed original PDFs (311 pages)** were genuinely
replayed byte for byte through a managed CLI at source
`1a81f69a4beaf92a8710e5b79ce355440efb00b0` and separately through
a Linux Native AOT CLI at `e1dfe3b8ea0b5a0cc1cbd612abf22dd8a59748ea`.
These results are not a new `47bc9791` replay. The original
10/30/100-record styled-CJK HTML was matched to the committed Unicode
examples and tested through actual typed API, managed CLI and Linux native
execution in [the PR #71 acceptance workflow](https://github.com/KoalaFacts/FactsPDF/actions/runs/40497304548).
Those reports use real Chinese regular/bold; a separate C1 style report
positively exercises **four distinct Chinese faces**. This does not
constitute user approval of the changed reference pages.

**No public NuGet/CLI/Release, hosted Pages site, customer software delivery,
general evaluation grant or production-support commitment exists.** Private
owner retention is not a legal grant. Human page review, legally valid
distribution/evaluation rights, documented customer/seller terms, fixed-hardware
performance budget and unclaimed source/toolchain signature checks remain
separate gates. Track current decisions in
[issue #51](https://github.com/KoalaFacts/FactsPDF/issues/51).

The accepted product scope is documented in the
[first usable release design](superpowers/specs/2026-10-11-first-usable-release-scope-design.md).
The [commercial-preview delivery issue](https://github.com/KoalaFacts/FactsPDF/issues/51)
tracks candidate artifacts, current page review, release preparation and the
evaluation/quote process. Performance observations remain specific to their
measured workload and environment.


## Source-only numeric report weights (29 October 2026)

The engine's current source accepts `font-weight` hundreds 100–900 for supported
report text (100–500 normal, 600–900 bold), selecting the caller's explicit
normal/bold chains with actual font advances and pagination. A 64-entry report,
separate 400/700 oracle, NUnit tests and Linux Native AOT validation are bound to
the source implementation at `7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750`. Non-hundred values, `bolder`,
`lighter`, variable-weight interpolation and missing bold roles do not silently
fall back. This is **not** evidence that the older retained
`0.1.0-preview.1` software candidate was rebuilt, that a NuGet package was
published, or that page/reference, licence and distribution gates were approved.

## Source-only patterned report borders

The bounded dashed/dotted border implementation at source `4b5d6da3553579dad75dd00bfb041e1f3c200d90` adds actual PDF strokes to existing business-report boxes. The [76-record report](../examples/report-patterned-borders.html) and the native PDF/Poppler evidence check are technical regression material only. This has not been incorporated into the retained 0.1.0-preview.1 binary candidate or human-approved reference pages; existing evaluation/licensing limits still apply.
