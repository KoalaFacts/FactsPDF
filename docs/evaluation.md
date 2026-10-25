# Evaluate FactsPDF for business reports

FactsPDF converts a documented HTML/CSS subset into searchable PDF documents
without a browser rendering engine. Its first delivery scope is bilingual business
reports for .NET applications and command-line integrations.

The current build is experimental. Evaluate a specific template and deployment
environment before making a production commitment. The package version remains
`0.1.0-dev`; the commands below use a source checkout.

**Working with an existing reporting template?** [Open a template-fit request](https://github.com/KoalaFacts/FactsPDF/issues/new?template=report-template-fit.md). Share a redacted layout description, not customer documents or credentials. A public request starts a scope discussion only; see licensing below before building or running FactsPDF.

## Start with a report that fits

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
| Page furniture | Single-line literal header/footer, decimal page counter | No HTML header templates, total-page counters or general CSS Paged Media |
| Layout | Documented block/text subset | General flex/grid and arbitrary webpage conversion are outside the current scope |
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

The accepted product scope is documented in the
[first usable release design](superpowers/specs/2026-10-11-first-usable-release-scope-design.md).
The [commercial-preview delivery issue](https://github.com/KoalaFacts/FactsPDF/issues/51)
tracks candidate artifacts, current page review, release preparation and the
evaluation/quote process. Performance observations remain specific to their
measured workload and environment.

