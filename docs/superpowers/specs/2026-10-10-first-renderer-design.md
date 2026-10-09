# FactsPDF first renderer: development slice

Status: implementation proposal for the first development PR, not a complete HTML renderer or stable API.

## Intent and agreed constraints

FactsPDF accepts HTML/CSS directly. There is no new markup language and no FactsML dependency.
One shared core is intended for future language bindings. Product copy is language-neutral.
The implementation uses .NET 10, with no third-party runtime parser, layout, browser, graphics, or PDF library.
Existing Community 1.1, Commercial, contribution and release-review rules remain unchanged.

## Smallest useful increment

A reusable stream-output library and CLI produce a genuine fixed-page PDF 1.7 document.
Supported content: controlled HTML fragments/documents containing p, h1-h6, div, section,
article, span and br, with html/body/head/title/meta and comments for document structure.
A documented subset, not HTML5 conformance: quoted/unquoted attributes, case-insensitive
HTML names and optional paragraph end tags are supported; other malformed trees fail explicitly.
Only inline CSS is in this slice: font-size (pt/px), color (hex and a small named palette),
unitless line-height, text-align, nonnegative paragraph margin-top/margin-bottom, and
break-before/break-after (auto/page). Containers can inherit text styles, but cannot
specify paragraph-only margins or breaks. Stylesheets, @page, tables and images are subsequent increments.

Use the PDF standard Courier font for an intentionally narrow ASCII baseline. It provides
known monospaced advance widths without bundling any fonts. Unicode outside the supported
baseline fails explicitly; TrueType/OpenType, CJK, bidi and shaping are separate work.
This is not the previously discussed complete v0.1 two-page Chinese/English milestone.
The primary purpose is to verify the end-to-end pipeline and its safety/packaging boundaries.

## Contracts

`PdfConverter.Convert(string, Stream, PdfOptions?, CancellationToken)` returns page count and byte count.
Page geometry is in PDF points. Defaults: A4 595.28 x 841.89, 36-point margins, 12-point text.
Limits default to 1,000,000 input characters, 50,000 elements, nesting 128, 1,000 pages,
and 16,777,216 PDF bytes. Invalid/unsupported input has a stable error code; no silent CSS dropping.
No JavaScript, arbitrary filesystem access, or network fetches in the core. No reflection or dynamic code.
Parse, style, layout and PDF serialization remain separate implementation components.
Output is buffered under a hard size cap before copying to the caller's stream. This avoids
partial output on content errors; I/O failure during final copying may still leave partial data.
The caller retains ownership of the stream. The CLI writes a sibling temporary file and moves it
only on successful conversion; overwriting needs an explicit flag. stdout remains binary-only.

## Layout baseline

Whitespace collapses in normal flow; br forces a line break. Words wrap at ordinary spaces.
Unbreakable text wider than the page is an explicit unsupported-layout error, not truncated text.
Mixed inline text sizes/colors, basic alignment, positive adjacent paragraph margin collapse,
and automatic line fragmentation are supported. No widow/orphan promises or browser-pixel parity.
Breaks do not add spurious leading/trailing blank pages. No actual content may be drawn outside
usable page geometry. Empty input produces one blank page.

## Verification and exclusions

NUnit tests cover externally observable PDF structure, text, CSS, pagination, stream ownership,
resource limits, culture invariance and hostile/unsupported input. Test dependencies are not runtime dependencies.
CI should compile/test on Windows, Linux and macOS and publish/run an actual Native AOT Linux CLI.
Independently check xrefs, stream lengths and visible/text output with a PDF reader.
Locally this execution environment lacks the .NET SDK and cannot reach download hosts;
compiler and AOT verification must therefore come from actual CI evidence, not assertions.
No package publication, release, paid order, performance promise or completed multi-language support.

## Primary references

- HTML parsing: https://html.spec.whatwg.org/multipage/parsing.html
- PDF reference: https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/pdfreference1.7old.pdf
- Native AOT: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
