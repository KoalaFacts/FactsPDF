# FactsPDF development guide

**Experimental renderer, not a production release or a complete browser-compatible implementation.** The shared core accepts a documented HTML/CSS subset, performs font-aware text layout/pagination and writes PDF objects directly. Public APIs may change. No extra markup language is required.

The initial ASCII-only M0 support matrix has been superseded by [Unicode/fonts](unicode-fonts.md), [native font subsetting](font-subsetting.md), and [embedded CSS stylesheets/selectors](css-stylesheets.md). Their detailed constraints apply alongside this guide. Tables and images remain unsupported. The controlled CSS block box model now includes width, four-sided padding, solid borders, background fills and paginated box fragments; see [M8 Box Fragmentation](box-fragmentation.md) for exact limits.

## Run from source

Use the .NET 10 SDK selected by `global.json`, from the repository root:

```sh
dotnet test FactsPDF.slnx -c Release
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

For a bilingual stylesheet template, supply your own trusted, appropriately licensed static TrueType files:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Stylesheets need no additional flag. Font paths are placeholders, not included assets. `--font` may repeat up to eight times. `--subset-fonts` is opt-in and respects no-subsetting font permissions; omitting it retains full embedding.

The CLI accepts input/output paths or `-` for stdin/binary stdout. Input must be valid UTF-8, with or without a UTF-8 BOM. Diagnostics go to stderr, not into PDF bytes. Existing output files require `--overwrite`; a sibling temporary file is replaced into place only after successful conversion. Input HTML and explicit font paths cannot be the output path. Use a trusted working directory; filesystem aliases, symlinks and concurrent hostile modifications are not sandboxed.

Exit codes: 0 success; 2 usage/argument error; 3 input/rendering limitation; 4 I/O/access error; 130 cancellation. Cancellation is cooperative and cannot interrupt a blocked synchronous caller-stream read/write until it returns.

## Library API

Reference the source project while packages are unreleased:

```csharp
using FactsPDF;
using var output = File.Create("example.pdf");
var result = PdfConverter.Convert(
    "<style>p{color:#008000}</style><h1>Example</h1><p>Hello PDF</p>",
    output, new PdfOptions { Margin = 36 });
```

This example uses fontless ASCII mode. Explicit font API examples are in the Unicode guide. `Convert` leaves the caller's stream open and does not require a seekable output. It returns page/byte counts. Unsupported input causes no output writes because conversion buffers a bounded PDF before final copying; final-copy I/O failures or cancellation may leave arbitrary output streams partly written. `File.Create` above overwrites an existing file immediately; use your own temporary-file policy or the CLI to protect it.

## Current content and layout scope

| Area | Supported | Not yet supported |
| --- | --- | --- |
| Content | p, h1-h6, div, section, article, span, br | Lists, links, bold/italic elements, tables, images, SVG, forms |
| Document shell | html/body, explicit head/title, UTF-8 meta, HTML5 doctype, comments, embedded style | Full HTML5 tree construction, encoding sniffing and browser recovery |
| Text | ASCII/Courier fallback; explicit static TrueType fonts, simple horizontal Unicode/Chinese, searchable text and optional glyph subsetting | Complete shaping, bidi/RTL, all-script typography, automatic font discovery |
| Styles | Inline and embedded declarations; tag/class/id/universal/compound/group/descendant/child selectors, bounded author cascade | External stylesheets, at-rules, other selector classes, variables/functions and full CSS recovery |
| Text layout | Supported font size/color/line-height/alignment; paragraph margins; nested block widths, four-sided padding, solid border edges and background colors | Arbitrary CSS height, horizontal margins, border-radius, floats/flex/grid and image/table layout |
| Pagination | Wrapping; paragraph/heading break-before/break-after auto/page; CSS sliced block decorations across page fragments (background and side borders continue, top/bottom only first/last) | @page, repeated page furniture, widows/orphans, keep-together and full browser fragmentation rules |
| PDF | PDF 1.7 text/page tree/xref, embedded and compressed font resources, ToUnicode | Tagged PDF, PDF/A/UA conformance, links/bookmarks, signatures/encryption |

CSS property/value and applicability rules are in the stylesheet guide. Unsupported inputs fail explicitly rather than silently rendering wrong output. Headings use larger text by default, not synthetic bold. Normal whitespace is collapsed; font-aware CJK wrapping has a limited punctuation rule, not full UAX #14 support. A segment wider than the line is rejected rather than clipped or arbitrarily split.

Names are ASCII case-insensitive in the supported HTML grammar. Quoted/unquoted attributes and selected omitted p/body/html end tags are supported. XML self-closing non-void tags and unsupported nesting are errors. Class/id attributes now participate in selectors; lang/title/data-* do not execute code. Title remains non-rendering metadata. Character references require a supported name/number and semicolon: amp, lt, gt, quot, apos, nbsp and valid numeric scalars. Without explicit fonts, non-ASCII output is rejected rather than replaced with question marks.

The core does not execute scripts, dynamically compile input, fetch network resources or discover fonts/images implicitly. Font loading is structurally checked but not a complete sanitizer for hostile fonts. This parser is not a general HTML sanitization API.

## Resource limits and diagnostics

Base defaults: 1,000,000 UTF-16 input units; 50,000 elements; depth 128; 1,000 pages; PDF output 16,777,216 bytes; 200,000 display commands; A4 595.28 x 841.89 points and a 36pt margin. Font and CSS limits are documented in their respective guides. These constrain data/work/output, not a guaranteed process-memory ceiling. Fonts, paragraph data, pages, CSS token/declaration structures and output buffers allocate memory; minimum allocation/constant-memory behavior is not claimed.

| Codes | Meaning |
| --- | --- |
| FPDF1001-1003 | Input/element/depth limit |
| FPDF1101-1104 | HTML structure/element/attribute/reference error |
| FPDF1201-1205 | CSS property/value/syntax/source/budget error |
| FPDF1301-1305 | Text/font availability, layout/page limit, invalid UTF-16 or unsupported text processing |
| FPDF1401 | Output-byte or display-command limit |
| FPDF1501-1506 | Font format, resource, mapping, embedding-permission or subsetting issue |

Not every integer in a displayed range needs a distinct public API guarantee. Refer to exact guide tables and exception messages. Offsets are supplied where available, with inline CSS anchored to its HTML element; these are development contracts, not complete browser error classification.

## Native AOT and verification

Linux x64 (requires native compiler and platform dependencies):

```sh
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/native
artifacts/native/FactsPDF.Cli examples/first-document.html artifacts/first-document.pdf
```

That binary targets Linux x64 and still uses platform libraries. Windows/Linux/macOS run tests; this does not mean their entire AOT matrix or WASM is validated. Other language bindings remain planned.

CI independently inspects real output using qpdf/Poppler and, for subset inspection, fontTools. Those are verification-only tools, not runtime parser/font/PDF dependencies. The stylesheet workflow compares a bilingual stylesheet template against a hand-authored inline equivalent, including exact bytes/text/pixels. Fonts are not committed or uploaded as standalone files. NuGet packing is local inspection only: exact license/README metadata/content and no runtime package dependencies are checked. No registry publication occurs and all licensing release gates remain.

Historical execution evidence is retained in the first-renderer, Unicode, subsetting and CSS ledgers. A green run for one commit is not proof for later changes. Performance observations identify their exact source/corpus/environment; no universal throughput, cold-start or peak-memory guarantee is created by these documents.
