## Source-only minimum-height business report cards

On a controlled invoice, inspection or status report, ordinary panels outside
tables/lists can use `min-height:90pt` to reserve a consistent visible area
for backgrounds/borders and keep following rows aligned. The minimum changes
actual PDF vertical space and respects `box-sizing`, `em/rem` computed
fonts, CSS cascade, existing page movement and failure protection. See
[the inspection example](../examples/report-min-height-cards.html),
[40-card native witness](../examples/report-min-height-cards-native.html)
and [bounded CSS contract](css-stylesheets.md#current-source-minimum-height-report-panels).
Percentage/auto heights, height/max-height, table/list/image/inline targets,
unplaceable panels and full CSS Sizing are not accepted. This is only a
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

The [Community License](../LICENSE.md) allows qualifying entities with Group
annual gross revenue at most USD 1,000,000 to use FactsPDF in proprietary
commercial applications. The license also defines separate charitable and
open-source-project categories.

Uses outside those categories require a separately agreed authorization.
Commercial arrangements can cover agreed application, hosted-service or
redistribution needs. The standard policy retains the requirement to publish
covered engine modifications; ordinary application code and templates do not
become public merely by calling FactsPDF.

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
