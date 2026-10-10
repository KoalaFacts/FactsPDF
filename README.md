# FactsPDF

A browser-free HTML and CSS to PDF engine, designed for cross-language integration.

> **Status: experimental renderer.** This development branch contains a limited
> document-text core and CLI, not a production release. Start with the
> [development guide](docs/development.md), [CSS support](docs/css-stylesheets.md),
> [Unicode/fonts](docs/unicode-fonts.md) and [font subsetting](docs/font-subsetting.md).

FactsPDF is a KoalaFacts project. Input is standard HTML and a documented CSS
subset; no separate markup language is required. The shared core connects HTML
parsing, stylesheet/inline cascade, font metrics, wrapping, pagination and PDF
output without a browser or third-party runtime parser/font/layout/PDF library.
High throughput, fast startup and low resource use remain engineering goals.
Published development measurements identify their scope and limitations; no
general performance guarantee or competitor comparison is claimed.

## Try the development build

From a source checkout with the .NET 10 SDK:

```sh
dotnet test FactsPDF.slnx -c Release
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

For the bilingual stylesheet example, supply your own appropriately licensed,
trusted static TrueType files and optionally enable native font subsetting:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

No additional switch is needed for embedded stylesheets. `--font` may repeat in
fallback order. Without fonts, the ASCII/Courier mode remains. Full embedding is
the default; `--subset-fonts` or `PdfOptions.SubsetFonts` keeps needed glyphs and
composite components where permitted. No-subsetting fonts remain fully embedded.
No font files are bundled.

## Stylesheets and selectors

Templates can now share styles instead of repeating every inline declaration:

```html
<style>
  body { font-size: 12pt; line-height: 1.5; }
  h1, h2 { color: #1d4568; }
  .report > p.lead { font-size: 16pt; }
  .report .accent { color: #008000; }
  p.next { break-before: page; }
</style>
<body>
  <section class="report">
    <h1>FactsPDF</h1>
    <p class="lead">Hello, world.</p>
    <p>A paragraph with <span class="accent">shared styling</span>.</p>
  </section>
</body>
```

Supported selector forms include type, universal, class, ID, compound, comma
groups, descendant and child combinators. The bounded author cascade handles
per-property specificity/source order, inline rules, `!important`, inheritance,
`inherit`, `initial` and `unset`. Later style elements can affect earlier content.

The syntax pipeline now parses standards-based CSS token, block, declaration
and at-rule structures with recoverable diagnostics. **StrictPdf still rejects
unsupported semantics and recovered syntax errors**, so this remains a strict
documented rendering subset, not full HTML5/CSS or browser layout compatibility.
See the [CSS Syntax Core guide](docs/css-syntax-core.md),
[measured compatibility scope](docs/css-syntax-compatibility.md) and
[independent-review / browser-differential gates](docs/css-syntax-quality-gates.md).
The existing eight style properties and text layout remain the scope; selectors
do not add width/padding/borders/backgrounds, tables/images or flex/grid. External
stylesheets, at-rules and other selector classes remain unsupported. Full Unicode
shaping/RTL, CSS font selection and automatic font discovery are not implemented.
Unsupported input fails explicitly rather than silently changing the output.
See [exact values, budgets and diagnostics](docs/css-stylesheets.md).

## Verification and integration goals

CI tests Windows, Linux and macOS, and builds/runs an actual Linux x64 Native AOT
CLI. Independent inspectors check ASCII/Unicode output, real-font subsetting,
and stylesheet-versus-handwritten-inline equivalence. The latter requires equal
PDF bytes, text and rendered page pixels for the bilingual fixture. NuGet packing
is local license/dependency inspection only, not a registry release.

See the [CSS execution record](docs/css-development-ledger.md) and previous
[first-renderer](docs/development-ledger.md), [Unicode](docs/unicode-development-ledger.md)
and [subsetting](docs/subsetting-ledger.md) ledgers. A configured workflow is not
proof for an untested commit, and fixture equivalence is not full conformance.

Public integration is not tied to the implementation language. Current entry
points are the library API and CLI. The roadmap includes .NET/NuGet distribution,
JavaScript/TypeScript/npm, Rust, Go, Python and WebAssembly sharing one rendering
core. WASM, non-.NET bindings and additional platform/AOT combinations are not
yet implemented or verified. No stable API or published package is claimed.

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