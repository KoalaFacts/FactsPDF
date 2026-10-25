# FactsPDF

**Generate searchable business-report PDFs from HTML and CSS, without a browser.**

FactsPDF is a KoalaFacts rendering engine for applications and command-line jobs
that control their report templates. It supports English and Chinese text,
explicit font styles, transparent PNGs, tables and pagination through a
documented HTML/CSS subset. Fonts and images are supplied by the caller.

> **Current stage: experimental private preview source (`0.1.0-preview.1`).**
> This source version is not a verified installable candidate yet. Run from source or build a local package. There is no published NuGet package
> or standalone CLI release, and the API is not stable yet.
> Start with the quick start below or the [business-report evaluation guide](docs/evaluation.md).

[Quick start](#generate-your-first-pdf) · [C# API](#use-the-net-api) ·
[Compatibility lookup](#compatibility-lookup) · [Examples](#choose-a-report-example) ·
[Licensing](#licensing)

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
| Stylesheets | Inline CSS and embedded `<style>` rules; documented selectors, cascade and inheritance | [CSS support](docs/css-stylesheets.md) |
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

## Compatibility lookup

The **Can I render it?** site provides searchable support records for HTML,
CSS, text, PDF behavior and runtime integration. Filter by category or status,
open a feature's precise scope and limitations, and follow its source/test
evidence. It supports Chinese/English interface labels, shareable feature links
and JSON/CSV export.

This is a **code-audit snapshot**, not a released-version comparison matrix.
Each record carries its own audit commit; unknown introduction versions remain
unknown. A supported subfeature does not make its whole parent standard supported.

- [Read the current support data](site/data/compatibility.json).
- [Run, verify or deploy the site](docs/compatibility-site.md).
- [Public deployment status](https://github.com/KoalaFacts/FactsPDF/issues/51):
  GitHub Pages configuration is still pending; the hosted site is not live yet.

To browse locally, use Node 24 and npm, starting from the repository root:

```sh
cd site
npm ci
npm run dev
```

Open the local URL printed by Vite, normally `http://127.0.0.1:5173/FactsPDF/`.

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

Structured page furniture is implemented in the development source. Private
`0.1.0-preview.1` package and CLI components are being staged and tested in
draft PR #60, but the complete, independently installed evaluation bundle has
not passed final acceptance and nothing has been published or distributed.
Outstanding gates include full legacy-PDF replay, original styled-CJK font
coverage and reference-page review. Follow
[issue #51](https://github.com/KoalaFacts/FactsPDF/issues/51)
for current implementation and delivery evidence.

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
