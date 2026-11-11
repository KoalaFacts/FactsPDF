# FactsPDF

**Generate searchable business-report PDFs from HTML and CSS, without a browser.**

FactsPDF is a .NET library (for .NET 10 and .NET 11) and command-line tool that turns a *controlled*
HTML/CSS template (an invoice, inspection report, quote or status summary)
into a real PDF: selectable text, embedded TrueType fonts, vector borders and
backgrounds, images, tables and page numbers. It does not start Chromium or any
other browser, and it never fetches anything from the network.

It is **not** a general webpage-to-PDF converter. It implements a deliberately
bounded subset of HTML and CSS, and anything outside that subset fails with an
explicit error code instead of rendering something approximate.

### 👉 Can I Render? — check what is supported

**[koalafacts.github.io/FactsPDF](https://koalafacts.github.io/FactsPDF/)** is
the searchable compatibility site for every HTML element, CSS property and
value, text, PDF and runtime feature. Each record shows its exact scope,
limitations and the tests that prove it. Check it before you write a template.

> **Status: experimental, unreleased (`0.1.0-preview.1`).** There is no public
> NuGet package or CLI download yet and no stable API. You build it from
> source; the [licensing rules](#licensing) apply **before** you build or run
> it, and any company may evaluate it free for up to 90 days. Progress is tracked in
> [issue #51](https://github.com/KoalaFacts/FactsPDF/issues/51).

**Contents:** [Quick start](#quick-start) · [C# API](#use-from-c) ·
[Fonts and images](#fonts-and-images) · [What is supported](#what-is-supported) ·
[What is not](#what-is-not-supported) · [Errors](#when-something-fails) ·
[Examples](#examples) · [Documentation](#documentation) · [Licensing](#licensing)

---

## Quick start

You need Git and any **.NET 10 SDK** (10.0.100 or later). The package targets `net10.0` and also runs
in .NET 11 apps: CI checks a `net11.0` consumer on the .NET 11 GA SDK and runtime, and that every
example renders the same bytes on both runtimes.

```sh
git clone https://github.com/KoalaFacts/FactsPDF.git
cd FactsPDF
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html quick-start.pdf
```

Open `quick-start.pdf`: a one-page service report with a styled table. It uses
ASCII text and the built-in Courier font, so it needs no font or image files.

The command is always `<input.html> <output.pdf> [options]` (use `-` for
stdin/stdout). Common options:

| Option | Effect |
| --- | --- |
| `--header-text "…"`, `--footer-text "…"` | Centred text on every page |
| `--page-numbers` | Append ` \| N` to the footer (or show the number alone) |
| `--page-furniture file.json` | Styled left/centre/right header and footer regions, `{page}`/`{pages}` labels ([guide](docs/page-furniture-cli.md)) |
| `--page-size a4\|letter`, `--landscape` | Paper size and orientation (default A4 portrait) |
| `--page-width 148mm --page-height 210mm` | Custom paper size (pt/px/mm/cm/in) |
| `--margin 15mm` | Uniform page margin (default 36pt) |
| `--title`, `--author`, `--subject`, `--keywords` | PDF document properties |
| `--font`, `--font-bold`, `--font-italic`, `--font-bold-italic` | TrueType font files, see [below](#fonts-and-images) |
| `--subset-fonts` | Embed only the glyphs used |
| `--image name=path.png` | Bind an image resource, see [below](#fonts-and-images) |
| `--overwrite` | Allow replacing an existing output file |
| `--help`, `--version` | Show usage, or the exact build version and commit |

Example with a header, footer and page numbers:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/quick-start.html report.pdf \
  --header-text "Service report" --footer-text "Internal" --page-numbers
```

## Use from C#

Until a package is published, reference the library project from a console app
next to your checkout (run from the `FactsPDF` directory):

```sh
dotnet new console --framework net10.0 --output ../FactsPdfDemo
dotnet add ../FactsPdfDemo/FactsPdfDemo.csproj reference src/FactsPDF/FactsPDF.csproj
```

```csharp
using FactsPDF;

// Configure once and reuse: the writer is immutable and thread-safe.
var pdf = Pdf.Write()
    .WithPage(p => p.A4().Margin(15, PdfUnit.Millimetre))
    .WithHeaderText("Service report")
    .WithFooterText("Internal")
    .WithPageNumbers();

PdfWriteResult result = pdf.ToFile(
    "<h1>Service report</h1><p>Prepared for Example Client.</p>", "report.pdf");
Console.WriteLine($"{result.PageCount} page(s), {result.BytesWritten} bytes");
```

Choose the output that fits: `ToBytes(html)`, `ToStream(html, stream)`,
`ToStreamAsync(html, stream)` (for example an ASP.NET Core response body),
`ToBufferWriter(html, writer)`, or `ToFile`/`ToFileAsync`, which replace the
file only after the PDF is complete. Every method renders fully before writing,
so a failed conversion writes nothing, and each accepts a `CancellationToken`.
`WithFonts`, `WithImage`, `WithMetadata`, `WithPageFurniture` and `WithLimits`
set fonts, images, document properties, styled headers and footers, and resource
limits. See the [API guide](docs/development.md#library-api) and the
[local-package recipe](docs/development.md#first-use-from-local-artifacts).

## Fonts and images

FactsPDF never guesses resources. **You pass every font and image explicitly**;
nothing is discovered from the system or loaded from HTML URLs.

**Fonts.** Without fonts, only ASCII text in built-in Courier is available.
For Chinese or other non-ASCII text, supply licensed static TrueType files.
Repeating `--font` defines a fallback order; bold, italic and bold-italic text
each need their own chain. Fonts are fully embedded unless you add
`--subset-fonts`, and embedding restrictions in the font are respected.

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html bilingual.pdf \
  --font fonts/Latin.ttf --font fonts/Chinese.ttf --font-bold fonts/Latin-Bold.ttf --subset-fonts
```

In C#, load fonts with `PdfFont.FromFile(path)` (or `FromBytes`/`FromStream`)
and set each chain with `WithFonts(f => f.Regular(latin, chinese).Bold(latinBold).Subset())`.

**Images.** `<img src="logo">` names a resource; it is not a path or URL.
Bind it with `--image logo=path/to/logo.png`, or in C# with
`WithImage("logo", PdfImage.FromFile("logo.png"))`. Baseline JPEG and static,
non-interlaced PNG (including transparency) are supported.

To try the branded example with a generated synthetic logo (needs Python 3):

```sh
python3 scripts/png_fixtures.py --output artifacts/readme-images
dotnet run --project src/FactsPDF.Cli -c Release -- examples/branded-business-report.html branded-report.pdf \
  --image logo=artifacts/readme-images/brand-mark.png
```

## What is supported

A summary only. **For any specific property or value, look it up on
[Can I Render](https://koalafacts.github.io/FactsPDF/)**; the
[feature notes](docs/feature-notes.md) explain each capability in detail.

| Area | Summary | Details |
| --- | --- | --- |
| HTML | Headings, paragraphs, `div`/`section`/`article` panels, `span`, scoped clickable `a` links, `small`, `u`/`s`/`del`/`ins` inline edit marks, `br`, `pre`, `blockquote`, `hr`, lists up to two levels, tables, images | [Layout scope](docs/development.md#current-content-and-layout-scope) |
| CSS input | Inline `style` and embedded `<style>` rules (including `@media print`/`all` blocks) with documented selectors, specificity, `!important`, inheritance and `inherit`/`initial`/`unset` | [CSS contract](docs/css-stylesheets.md) |
| Lengths | `pt px mm cm in pc q`, `%`, `em`, `rem` where documented | [Lengths and spacing](docs/feature-notes.md#lengths-margins-and-spacing) |
| Colours | Hex, opaque `rgb()`/`rgba(…,1)`, `hsl()`, 148 named colours, `currentColor`; no transparency | [Colours](docs/feature-notes.md#colours) |
| Text and fonts | Chinese and English text, normal/bold/italic/bold-italic roles, font size and weight, line height, letter/word spacing, `text-indent`, alignment and justification, ASCII `text-transform`, underline/overline/strike-through | [Fonts](docs/unicode-fonts.md), [typography](docs/feature-notes.md#text-fonts-and-typography) |
| Wrapping | `white-space` `nowrap`/`pre`/`pre-line`/`pre-wrap`, `overflow-wrap:break-word`, `word-break:break-all` | [White space](docs/feature-notes.md#white-space-and-line-wrapping) |
| Boxes | Width, min/max width, height, min-height, padding, margins, `margin:0 auto` centring, `box-sizing`, backgrounds, solid/dashed/dotted borders, rounded corners, LTR logical properties | [Panels](docs/feature-notes.md#report-panels-and-box-sizing), [borders](docs/feature-notes.md#borders-and-rounded-corners) |
| Tables | Fixed column widths, `colspan`, paragraph/image cells, repeated headers, long rows continuing across pages, cell `vertical-align` | [Tables](docs/tables.md) |
| Pages | Automatic pagination, `break-before`/`break-after`/`break-inside`, heading keeps, headers/footers, page numbers, paper size, margins, PDF metadata | [Pages](docs/feature-notes.md#pages-pagination-and-document-properties) |
| Runtime | .NET library and CLI; Linux and Windows x64 Native AOT are tested | [Build and verification](docs/development.md#native-aot-and-verification) |

## What is not supported

- Arbitrary webpages, JavaScript, external stylesheets or any network loading.
- Flexbox, grid, floats, positioning and CSS Paged Media (`@page`).
- Automatic table column widths, `rowspan` and nested tables.
- System font discovery, CSS `font-family` matching, synthetic bold/italic,
  complex-script shaping, right-to-left and vertical text.
- Transparency, gradients, SVG, interlaced or 16-bit PNG and APNG.

Unsupported input is rejected with a diagnostic, never silently ignored.

## When something fails

Every failure has a stable code such as `FPDF1202` (unsupported CSS value) or
`FPDF1302` (content does not fit). The CLI prints it to stderr with the line
and column in your HTML when known (plus a hint for common font and image
errors) and exits nonzero; the API throws `FactsPdfException` with `Code` and
`SourceOffset`. **A failed conversion never replaces an existing output file**
and never produces a partial PDF. Fixes for common first-run problems (missing
fonts, glyphs, bold faces or images) are in
[first-use troubleshooting](docs/first-use-troubleshooting.md).

## Examples

Every file in [examples/](examples) is a runnable report. Good starting points:

| Example | Shows | Extra inputs |
| --- | --- | --- |
| [quick-start.html](examples/quick-start.html) | One-page service report with a styled table | None |
| [report-print-spacing.html](examples/report-print-spacing.html) | Print units, line heights and paragraph margins | None |
| [branded-business-report.html](examples/branded-business-report.html) | Transparent logo beside company details and line items | `logo` image |
| [business-table-rich-cells.html](examples/business-table-rich-cells.html) | Column spans with paragraphs and images in cells | `logo` image |
| [business-table-long-description.html](examples/business-table-long-description.html) | A long table row continuing across pages | `logo` image |
| [Combined report](docs/combined-report.md#run-a-report) | English/Chinese text, four font roles, lists, image, table and page text | Fonts, JPEG, page options |

Files named `report-*-oracle.html` are independently written control versions
used by the tests to check that a feature renders the same PDF as its
hand-written equivalent; you do not need them to use FactsPDF.

## Documentation

| I want to… | Read |
| --- | --- |
| Check whether my template will work | [Can I Render](https://koalafacts.github.io/FactsPDF/), [evaluation guide](docs/evaluation.md) |
| Understand one feature in depth | [Feature notes](docs/feature-notes.md), [CSS contract](docs/css-stylesheets.md) |
| Build, test or package locally | [Development guide](docs/development.md) |
| See test and acceptance evidence | [Support matrix](docs/acceptance/support-matrix.md) |
| See what the first preview contains and what blocks its release | [0.1.0-preview.1 release notes (draft)](docs/releases/0.1.0-preview.1.md) |
| Run or deploy the Can I Render site | [Compatibility site](docs/compatibility-site.md) |
| Contribute | [Contribution policy](CONTRIBUTING.md) |

Have a template you want assessed? Read the
[template-fit checklist](docs/evaluation.md#check-template-fit-before-integration),
then [open a template-fit request](https://github.com/KoalaFacts/FactsPDF/issues/new?template=report-template-fit.md).
Use synthetic examples only: **do not attach customer documents, private
templates or business data**. A request starts a discussion; it is not a trial,
licence or support contract.

## Project status

- CI runs the managed tests on Windows, Linux and macOS and executes real
  Linux x64 and Windows x64 Native AOT builds, with independent PDF checks of
  text, geometry, fonts, images, pagination and error recovery.
- A `0.1.0-preview.1` candidate has been built and retained privately by the
  maintainer. That is not a release: there is no public NuGet/CLI package,
  customer delivery or stable API yet.
- The [Node subprocess](docs/node-cli-probe.md) and
  [browser WASM](docs/browser-wasm-probe.md) integrations are experimental
  probes, not SDKs. Benchmarks apply only to their documented workloads.

Detailed delivery evidence is in the [development ledger](docs/development-ledger.md)
and [issue #51](https://github.com/KoalaFacts/FactsPDF/issues/51).

## Licensing

Source-available under your choice of three standard [PolyForm](https://polyformproject.org) licenses ([full text](LICENSE.md)):

| License | Free for |
| --- | --- |
| [PolyForm Small Business 1.0.0](https://polyformproject.org/licenses/small-business/1.0.0) | Companies with **< 100 people** and **< USD 1M revenue**, including commercial use |
| [PolyForm Noncommercial 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0) | Personal, noncommercial, charity, education and government use |
| [PolyForm Free Trial 1.0.0](https://polyformproject.org/licenses/free-trial/1.0.0) | Anyone evaluating it for **up to 90 days** |

Everyone else: [commercial license](COMMERCIAL.md) from Koala Facts Trading Pty Ltd. Details: [LICENSING.md](LICENSING.md).
