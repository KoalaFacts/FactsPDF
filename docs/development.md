# First renderer development guide

**Experimental M0 slice, not a production release or complete HTML/CSS renderer.**
The purpose is to prove HTML input -> styled text -> pagination -> PDF serialization,
with an executable CLI, regression tests and a real Native AOT build. Unicode/fonts,
stylesheets, tables and images remain separate milestones. Public APIs may change.

## Run from a source checkout

Use the .NET 10 SDK selected by `global.json`, from the repository root:

```sh
dotnet test FactsPDF.slnx -c Release
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

The CLI is `FactsPDF.Cli <input.html|-> <output.pdf|-> [--overwrite]`.
Use `--help` for usage. UTF-8 input, with or without a UTF-8 BOM, is required;
this does not imply Unicode font support. `-` selects standard input or binary
standard output. Diagnostics go to standard error, never into a PDF stream.
Existing destinations are protected unless `--overwrite` is explicit. A conversion
writes a sibling temporary file and replaces the destination only after success.
I/O failures or cancellation while writing stdout can leave a partial stdout stream.
Use an output directory controlled by the caller, not a directory writable by
untrusted parties; filesystem alias and symlink security is not promised.

Exit codes: 0 success; 2 usage/argument error; 3 input or rendering limitation;
4 I/O/access error; 130 cancellation. Cancellation is cooperative: a synchronous
input read or output write must return before cancellation can be observed.

## Library API

Reference the source project while packages are unreleased:

```csharp
using FactsPDF;

using var output = File.Create("example.pdf");
var result = PdfConverter.Convert(
    "<h1>Example</h1><p style='color:#008000'>Hello PDF</p>",
    output,
    new PdfOptions { Margin = 36 });
```

`Convert` leaves the caller's stream open, does not need a seekable output, and
returns page and byte counts. The library buffers the bounded PDF before final
copying, so unsupported content causes no output writes. It cannot roll back
I/O errors or cancellation during the final copy. File replacement guarantees
belong to the CLI, not the sample's direct `File.Create` call.

## Actual supported subset

| Area | Included now | Not included |
| --- | --- | --- |
| Content | `p`, `h1`-`h6`, `div`, `section`, `article`, `span`, `br` | Lists, links, bold/italic, tables, images, SVG, forms |
| Document shell | `html`, `body`, explicit `head`, `title`, UTF-8 `meta`, HTML5 doctype, comments | Full HTML5 tree construction, encoding sniffing and browser error recovery |
| Text | Printable ASCII and normal HTML whitespace, Courier standard PDF font | Font loading/embedding, Unicode/CJK, bidi, shaping, hyphenation |
| Styles | Inline `style` only; text inheritance and per-run size/color | `style`/`link` stylesheets, selectors/cascade, `!important`, custom properties |
| CSS values | `font-size` in pt/px; `color` as #rgb/#rrggbb or black/white/red/green/blue/gray/grey; unitless `line-height` 1-10 | Other units, font families/weights, CSS functions |
| Block formatting | left/center/right `text-align`; paragraph/heading `margin-top` and `margin-bottom` in pt/px or zero | General box model, borders/backgrounds, floats, flex/grid, nested container margins |
| Pagination | Automatic wrapping at spaces, line fragmentation, paragraph/heading `break-before`/`break-after: page/auto` | `@page`, repeating furniture, widows/orphans, keep-with-next, general fragmentation rules |
| Output | PDF 1.7 page tree, text operators and cross-reference table | Compression, PDF/A, PDF/UA, tagged PDF, links/bookmarks, signatures/encryption |

Paragraph-only style properties are rejected on other elements rather than
silently ignored. Font size is limited to 1-144pt, margins to 0-14400pt, and
usable page geometry is validated. Headings use larger text but not a bold font.

Names are case-insensitive, attributes may be quoted or unquoted, and a limited
set of optional paragraph/body/html end tags is understood. XML self-closing
non-void elements and other unsupported nesting produce explicit errors. The
reader is not a standards-conformance or sanitization API. `id`, `class`, `lang`,
`title` and `data-*` attributes can be retained as accepted metadata but do not
activate selectors or scripts. Document title is ignored in this first output.

Character references must end with a semicolon. The initial decoder recognizes
`amp`, `lt`, `gt`, `quot`, `apos`, `nbsp`, and decimal/hexadecimal scalar values.
The ASCII renderer rejects decoded characters it cannot display, including
non-breaking space; it never substitutes question marks for unsupported text.
A word wider than the usable line is rejected, not clipped or broken arbitrarily.

No script execution, dynamic code compilation, network fetching, or implicit
font/image file discovery exists in the core. A normal web page containing
unsupported elements is expected to fail; it is not silently rendered as plain text.

## Resource limits and diagnostics

Defaults in `PdfOptions`: 1,000,000 UTF-16 input code units; 50,000 elements;
nesting depth 128; 1,000 pages; PDF output 16,777,216 bytes. Page geometry uses
points, defaults to A4 595.28 x 841.89 and a 36-point margin. The limits constrain
input/work/output, not an exact process-RSS guarantee; the current model buffers
text and pages and has not been tuned for minimum allocation.

| Code | Meaning |
| --- | --- |
| FPDF1001 / 1002 / 1003 | Input, element or depth limit |
| FPDF1101 / 1102 / 1103 / 1104 | Unsupported/malformed structure, element, attribute or character reference |
| FPDF1201 / 1202 | Unsupported CSS property/context or invalid/unsupported CSS value |
| FPDF1301 / 1302 / 1303 | Unsupported character/font, unfittable layout or page limit |
| FPDF1401 | Output byte limit |

Errors may include a source offset where available. Unsupported CSS rules fail
instead of attempting the browser's recovery behavior. These diagnostic codes
are development contracts, not an assurance of complete HTML error classification.

## Native AOT and packaging verification

Linux x64 command (requires the native compiler and platform dependencies):

```sh
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/native
artifacts/native/FactsPDF.Cli examples/first-document.html artifacts/first-document.pdf
```

AOT does not mean an OS-independent executable: this build targets Linux x64 and
still uses platform libraries. The CI test matrix covers Windows, Linux and macOS;
only Linux x64 has an actual Native AOT publication/run check in this increment.
WASM and non-.NET bindings have not been implemented or verified.

CI uses Poppler tools only to independently inspect and render a synthetic PDF;
they are not engine dependencies. `scripts/verify_sample.py` checks the actual
two-page output and extracts expected text. Its optional base64 log is solely for
reviewing that small public fixture, never private user documents.
`verify_package.py` checks the local NuGet archive includes the exact current
LICENSE.md/README.md, uses license-file metadata and has no runtime package dependencies.
It does not publish the package. Existing licensing release gates still apply.

See [execution evidence](development-ledger.md) for tested commits and runs. A
configured workflow is not evidence of success for a later commit. No throughput,
startup-time, peak-memory or comparative performance claims are made yet.
