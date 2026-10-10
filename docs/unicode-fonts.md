# Explicit fonts and Unicode / Chinese output

This is an experimental increment of the shared FactsPDF core, not full Unicode typography, full HTML/CSS or a production release. No new runtime dependency is used. The existing ASCII/Courier mode is preserved when no fonts are supplied.

## Try it

From this development branch with the .NET 10 SDK and your own appropriately licensed static TrueType fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf
```

`--font` may be repeated up to eight times, in fallback priority order. The CLI reads only those explicit font paths. It rejects an output path equal to the input HTML or a supplied font, including with `--overwrite`. Existing-file protection, temporary-file replacement, stderr diagnostics and UTF-8 input behavior remain in place. This is not a hostile-filesystem sandbox; filesystem aliases and concurrent replacement require a trusted working directory.

Library example (load trusted font files once, reuse the immutable objects):

```csharp
using FactsPDF;

using var latinFile = File.OpenRead("fonts/Latin.ttf");
using var chineseFile = File.OpenRead("fonts/Chinese.ttf");
var latin = PdfFont.LoadTrueType(latinFile);
var chinese = PdfFont.LoadTrueType(chineseFile);
var options = new PdfOptions { Fonts = [latin, chinese] };

using var output = File.Create("output.pdf");
var result = PdfConverter.Convert("<p>Hello，中文。</p>", output, options);
```

This illustrates the current .NET binding, not a language-specific product identity. Other bindings still need implementation. `File.Create` in this library example overwrites an existing file immediately; use the CLI or your own temporary-file policy when existing destinations must be protected. `LoadTrueType` also accepts a `ReadOnlySpan<byte>`. The byte overload snapshots its input. The stream overload reads from the current position, supports non-seekable short reads and leaves the caller's stream open. Do not mutate a font-list collection concurrently with conversion; font objects themselves can be reused concurrently.

## Implemented subset

- Static TrueType sfnt fonts with `glyf` outlines, Unicode cmap 4 or 12, real horizontal advances, and vertical font metrics. A supported full-repertoire cmap 12 is preferred over cmap 4.
- Unicode scalar handling including supplementary-plane Han characters when the supplied font has the glyph. Unpaired UTF-16 surrogates are errors, not replacements.
- Horizontal Han and a documented simple character-range subset, including basic Latin, Greek and Cyrillic. Simple precomposed characters are supported; combining marks, variation sequences, bidi controls and scripts requiring shaping are rejected explicitly. This is not a complete Unicode script-support matrix.
- Ordered explicit fallback per scalar. No automatic system font lookup, network access or silent missing-glyph replacement.
- Chinese wrapping without spaces and a limited opening/closing punctuation rule. NBSP is preserved and keeps its adjacent segment unbroken. This is not full UAX #14 or advanced East Asian line-breaking conformance.
- Type 0/CIDFontType2 output with actual widths, CIDToGIDMap and ToUnicode. Distinct scalars keep distinct PDF character codes even when they share a glyph. Supplementary scalars have UTF-16BE surrogate-pair mappings for extraction.
- Each used supplied font object is fully embedded once per PDF, compressed. Unused fonts are not embedded. The exact supplied font bytes are retained: **the engine does not implement subsetting yet**.

## Limits, permissions and trust

Default maximums are 32 MiB per font load, eight input-chain entries and 64 MiB of unique supplied font objects per conversion. Input/page/output limits and cooperative cancellation still apply. These are byte/operation limits, not measured peak-process-memory guarantees. Full-font copies, layout data and compression buffers add memory overhead. Reuse loaded fonts rather than loading them on every request.

The loader structurally checks sfnt table ranges, overlap, versions/lengths, metrics, Unicode mappings and loca offsets. **It does not fully validate TrueType outline programs, hinting instructions, every table checksum, every optional table or every font license.** Do not treat it as a sanitizer for arbitrary untrusted uploaded fonts. Use trusted, curated fonts and independent validation before production.

Embedding flags are checked conservatively. Installable/editable embedding is supported; no-subsetting flags are compatible with full embedding. Restricted, bitmap-only and preview-only fonts are rejected. Preview-and-print requires read-only document handling that this increment does not implement. Actual copyright/license permission is the caller's responsibility; a flag is not a legal clearance.

Collections (TTC/OTC), CFF/CFF2, WOFF/WOFF2, variable/color fonts, CSS `font-family` / `@font-face`, system font discovery, synthetic bold/italic, kerning, GSUB/GPOS, RTL and tagged PDF are not implemented here. Existing HTML and CSS limitations remain; see [the first-renderer guide](development.md).

## Diagnostics

| Code | Meaning |
| --- | --- |
| FPDF1301 | Non-ASCII character without supplied fonts |
| FPDF1304 | Invalid UTF-16 text |
| FPDF1305 | Character/sequence requiring unsupported shaping, bidi or script processing |
| FPDF1501 | Structurally malformed/truncated font or inconsistent font metrics/maps |
| FPDF1502 | Unsupported font representation or missing supported Unicode cmap/name |
| FPDF1503 | Font byte/count/character-code capacity limit |
| FPDF1504 | Missing glyph in the explicit fallback chain |
| FPDF1505 | Embedding permissions unsupported or restricted |

An invalid font-list argument (null, null entry or more than eight entries) is an argument error rather than FPDF1503. Exceptions before final output copying do not write a partial PDF; final-copy I/O failures on arbitrary streams cannot be rolled back.

## Independent verification

NUnit uses an original synthetic sfnt builder (simple rectangular test glyphs, no bundled font files) to test cmap 4/12, supplementary text, glyph aliases, actual widths, fallback, CJK punctuation, permissions, corruption, resource bounds, concurrency, CLI safety and full embedded-byte recovery.

The Linux x64 Native AOT job additionally uses explicitly selected OS-installed DejaVu Sans and Droid Sans Fallback fonts. Poppler extracts the actual multi-font PDF text and renders both pages; qpdf checks structure. It tests both full original font inputs and small test-prepared font inputs. Test-only fontTools creates the latter solely to keep the public PDF fixture manageable. **That preparation is not a FactsPDF feature or runtime dependency.** Fonts are not committed, included in NuGet, or uploaded as artifacts. Only the generated public test PDF may be emitted in diagnostic logs. No user documents are used in these tests.

See [the development ledger](unicode-development-ledger.md) for exact tested commits and remaining work. No performance comparison, platform-wide AOT support or full Unicode typography is inferred from a green test run.
