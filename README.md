# FactsPDF

A browser-free HTML and CSS to PDF engine, designed for cross-language integration.

> **Status: experimental renderer.** This development branch contains a limited
> text-rendering core and CLI, not a production release. Read the
> [HTML/CSS support guide](docs/development.md), the
> [font and Unicode guide](docs/unicode-fonts.md) and the
> [font-subsetting guide](docs/font-subsetting.md) before using it.

FactsPDF is a KoalaFacts project. The intended input is standard HTML and CSS;
no separate FactsML language is required. The core connects parsing, inline
styling, font metrics, line wrapping, pagination and PDF output without a browser
or third-party runtime parser/font/layout/PDF library. High throughput, fast
startup and low resource use remain engineering goals. Initial full-versus-subset
measurements are recorded with explicit scope and limitations; no general
performance guarantee or competitor comparison is claimed.

## Try the development slice

From a source checkout with the .NET 10 SDK:

```sh
dotnet test FactsPDF.slnx -c Release
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

For Chinese and simple Unicode text, supply your own appropriately licensed
static TrueType fonts. Repeat `--font` for an ordered fallback chain:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/unicode-document.html unicode-document.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf
```

Opt in to native font subsetting with `--subset-fonts` (or `PdfOptions.SubsetFonts`):

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/subset-document.html subset.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Full embedding remains the default. Subsetting retains used glyphs plus composite
components, preserves text mappings, and falls back to full embedding when the
font prohibits subsetting. See [implementation boundaries and resource-baseline
methodology](docs/font-subsetting.md). No fonts are bundled.

The current subset includes paragraphs/headings/inline spans, basic inline CSS,
wrapping and pagination. Explicit fonts enable real glyph advances, horizontal
Chinese text, basic punctuation-aware wrapping, font fallback and searchable
PDF text with embedded fonts. Without fonts, the original ASCII/Courier mode
remains available. Unsupported elements, styles, glyphs and shaping requirements
fail explicitly. This is not full HTML5, stylesheet CSS or Unicode typography.
Complex shaping/RTL and automatic font discovery are not yet implemented.
No stable API or published package is claimed.

CI runs tests on Windows, Linux and macOS and builds/runs a Linux x64 Native AOT
CLI. Independent tools inspect generated PDFs, including real Chinese-font and
full/subset pixel-equivalence fixtures. Local NuGet packing checks the exact
license, runtime dependencies and absence of bundled font files, without
publishing. See the [first-renderer evidence](docs/development-ledger.md),
[Unicode increment ledger](docs/unicode-development-ledger.md) and
[subsetting ledger](docs/subsetting-ledger.md) for tested commits and runs;
workflow configuration alone is not evidence for an untested commit.

## Integration goals

The public integration model is not tied to the engine's implementation
language. The current development entry points are the library API and CLI.
The roadmap targets .NET/NuGet distribution, JavaScript and TypeScript/npm,
Rust, Go, Python and WebAssembly. Language bindings should reuse one shared
rendering core rather than implement separate layout and PDF engines.

WASM and non-.NET language bindings are not implemented or verified. Additional
platform/AOT combinations need their own implementation and validation.

## Licensing: Community and Commercial

**Commercial licenses are available by arrangement.** FactsPDF provides a free
Community License for qualifying uses and a separately agreed Commercial License
for other uses or negotiated licensing requirements.

**Source-available; not MIT and not an OSI-approved open-source license.**

The [FactsPDF Community License 1.1](LICENSE.md) permits free use for:

- Individuals and entities with Group annual gross revenue **at most
  USD 1,000,000**, including proprietary commercial use.
- Qualifying charitable organizations, for their charitable operations.
- Qualifying open-source projects, within those projects' scope.

The complete license defines consolidation, measurement periods, the 90-day
transition after loss of eligibility, notices, and downstream execution rights.
Receiving an ordinary generated PDF does not itself require an engine license.
FactsML's separate MIT license does not apply to FactsPDF.

### Changes to FactsPDF must be shared

**When you distribute or put a modified FactsPDF engine into operational use,
you must publicly release the engine changes and corresponding build materials
under the same FactsPDF Community License 1.1.** This includes internal use and
SaaS, not only selling or distributing a modified library. Pure controlled
development and testing can remain private until a publication trigger occurs.

An independent proprietary application that merely calls FactsPDF does not
have to be published because of this rule. Customer data, credentials, HTML/CSS
templates, and generated PDFs are not covered merely through use of the engine.
The open-source-project exemption has its own application-source requirements.

A public fork or a complete patch set against an available exact upstream
version is sufficient; an upstream PR is optional. Source must be available
without payment, login, or an NDA, and retained for at least three years after
last use or distribution of the modified version. See sections 4.1-4.5 of
[LICENSE.md](LICENSE.md) for the controlling scope, triggers, and conditions.

### Commercial License

A Commercial License is the licensing path for uses that do not qualify for the
Community License, including proprietary use by larger organizations without
another exemption. Eligible Community users may also enquire about a separately
negotiated commercial arrangement.

Commercial agreements can address proprietary applications, SaaS deployments,
and OEM or embedded redistribution. Covered entities, deployment and downstream
rights, versions, duration, pricing, and any support or service levels must be
expressly agreed; none is automatically included or unlimited.

**Paying for a Commercial License does not automatically permit private engine
modifications.** The standard commercial agreement must expressly retain the
modification-publication requirement. It licenses the agreed commercial use,
not a right to hide changes to FactsPDF. These policy statements do not amend
an existing separately agreed contract.

**[Enquire about a Commercial License](COMMERCIAL.md#contact-and-safe-handling).**
Commercial-licensing enquiries are welcome now. Issuing paid licenses and
accepting orders remain subject to the authorization and review requirements in
[COMMERCIAL.md](COMMERCIAL.md) and the
[licensing release gates](docs/licensing-review.md).

Read the [licensing guide and examples](LICENSING.md),
[commercial licensing details](COMMERCIAL.md), and
[contribution policy](CONTRIBUTING.md). No commercial contract or price is
created by this README. Technical support, an SLA, updates, and private-fork
rights are not implied by the words "Commercial License".

The initial licensing text has not been reviewed by retained legal counsel.
Maintainers must complete the [licensing release gates](docs/licensing-review.md)
before software-package release or accepting a paid license order.
[License history](docs/license-history.md) records earlier terms; version 1.1
does not retroactively change version 1.0 grants.