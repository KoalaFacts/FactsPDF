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

## Start with a report that fits

The source report-print example now demonstrates border-box panel sizing and percent/em/physical line-height with true computed inheritance, parent-relative percentage/em font sizes, opaque CSS RGB-function colours across text, panel backgrounds and solid borders, alongside spacing and decorations. The current engine accepts only integer `rgb()` channels and fully opaque comma-form `rgba()`; it rejects semi-transparent colour rather than silently flattening it. See [the scoped CSS contract](css-stylesheets.md#current-source-opaque-rgb-colours-for-reports). Relative `font-size:125%`/`1.125em` resolves against the computed parent with a 1–144pt result; this does not extend relative units to box geometry. These new source abilities are **not** part of the earlier privately retained binary candidate.

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
| [Print spacing report](../examples/report-print-spacing.html) | Business panel with computed line-height, physical print units, four-sided margins, auto-centred summary, and real strike-through/underline labels | ASCII/Courier; no external resources |
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
| Layout | Documented block/text subset with print-unit lengths (`mm/cm/in`) and real horizontal/vertical paragraph/heading margins and explicit-width `margin:0 auto` centring | No negative/vertical-auto or general container/cell paragraph horizontal margins, flex/grid, CSS Paged Media or arbitrary webpage conversion |
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

