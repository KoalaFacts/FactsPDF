# Explicit image resources

`PdfImage.LoadImage` recognizes the supplied bytes. It supports bounded baseline
JPEG and static non-interlaced PNG, including PNG transparency. The same API,
CLI resource binding and shared layout apply to both formats. The engine never
interprets `src` as a file path, URL or data URI.

```csharp
using FactsPDF;
using var input = File.OpenRead("chart.jpg");
var chart = PdfImage.LoadImage(input);
using var output = new MemoryStream();
PdfConverter.Convert("<p>Before</p><img src='chart' style='width:200pt'><p>After</p>", output,
    new PdfOptions { Images = new Dictionary<string, PdfImage> { ["chart"] = chart } });
// Persist output only after successful conversion, under the caller's file policy.
```

The span overload owns an immutable copy; the stream overload reads at most `MaxBytes+1`, supports short/nonseekable reads and leaves the input open. Resources expose `PixelWidth`, `PixelHeight` and `ByteLength`. The conversion snapshots the resource map with ordinal, case-sensitive names: 1–128 ASCII characters, first alphanumeric, followed by alphanumeric, dot, underscore or hyphen. Every binding, including aliases and unused entries, counts toward limits. The same `PdfImage` instance is embedded once when used repeatedly; unused images are not written.

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --image chart=chart.jpg
```

Repeat `--image name=path` for up to 16 resources. The first `=` separates the name; quote arguments with spaces. Relative paths use the process working directory. Extensions are not format evidence. Each file is read through the bounded loader; no network download is provided. Output paths cannot equal the supplied HTML, font or image path. Existing CLI temporary-file, stdout and exit-code rules still apply, including the documented trusted-directory limitation for filesystem aliases.

## Supported bytes and layout

JPEG requires a first JFIF APP0, 8-bit SOF0, one complete interleaved sequential scan, explicit nonredefined quantization/Huffman tables and an exact EOI/EOF. Grayscale is 1×1; YCbCr supports 4:4:4, 4:2:2 and 4:2:0 with standard component IDs/order. Restart intervals and legal marker fill are checked. Entropy validation visits every expected MCU/block, decodes Huffman symbols and checks coefficient runs, amplitude availability, stuffing, all-one padding and restart sequence. It does not perform IDCT or allocate a decoded pixel buffer.

Progressive/multiscan, CMYK/RGB-coded JPEG, arithmetic/lossless, ICC/Adobe transforms, non-square density, thumbnails and unsupported markers fail explicitly. Exif may omit orientation or specify 1; rotations/flips fail. Only bounded TIFF IFD0 ranges and the orientation field are interpreted; child IFD semantics are not validated. COM and bounded supported metadata are retained in the original DCT stream. DeviceRGB/Gray output does not claim color management or full image sanitization.

`img` is a void block at the root/body or inside ordinary supported containers, or the sole direct content of a [table cell](tables.md#image-only-cells-and-branded-layouts). Paragraph/inline/list ancestors, `</img>`, HTML width/height, srcset and CSS height are rejected. `alt` is metadata only; it is neither a fallback nor Tagged PDF. Existing id/class/style selectors apply.

CSS width controls content width; percentages use the parent content width. Natural size uses 96 dpi (`pixels × .75pt`) regardless of density metadata. Height preserves the pixel aspect ratio. Padding, solid borders, background and currentColor work through the existing box model. Standalone images remain left-aligned. An image-only cell uses its cell's text alignment and available content width. No automatic shrinking, independent height, transforms, float, wrap, inline/list images or mixed image/text cell content are implemented.

The complete image outer box moves to the next page if necessary. An image plus required leading ancestor decoration that cannot fit an empty page fails `FPDF1302`. Its own padding/borders never split; ancestor decorations retain existing slice behavior. Each image draw costs one display command, plus normal decoration commands.

## Static PNG and transparency

PNG uses the same `LoadImage` overloads, `Images` map and `--image name=path`
binding. Supply original PNG bytes; no conversion to JPEG is needed.

| PNG color type | Accepted bit depth | Transparency |
| --- | --- | --- |
| Grayscale | 1, 2, 4, 8 | Optional `tRNS` gray key |
| RGB | 8 | Optional `tRNS` RGB key |
| Indexed palette | 1, 2, 4, 8 | Optional per-entry `tRNS`; omitted entries are opaque |
| Grayscale with alpha | 8 | Full alpha channel |
| RGBA | 8 | Full alpha channel |

All five scanline filters and consecutive split IDAT chunks are supported.
Color samples become eight-bit DeviceGray/DeviceRGB PDF image streams; alpha
becomes a separate eight-bit soft mask. Transparent pixels reveal existing page
content, including colored panels, and partial alpha is retained. Images remain
unassociated: color samples are not premultiplied or flattened onto white.
Repeated placements share the same image and mask objects.

The loader validates chunk framing/type/CRC, required ordering, palette indices,
transparency, a completed zlib stream/checksum and the exact decoded row length.
Unused bytes after the completed zlib stream in final IDAT are ignored as allowed
by the PNG specification; data after IEND is rejected. Supported sRGB metadata
is validated. ICC/HDR color-transform chunks are explicitly unsupported. Other
ancillary metadata, including gamma/chromaticity, density, background, Exif and
text, is ignored after framing/type/CRC validation; compressed metadata is not
inflated. This does not implement color management or automatic Exif rotation.

16-bit samples, Adam7 interlacing, APNG and SVG remain unsupported and fail
explicitly. PNG and JPEG share standalone and image-only-cell placement. The
engine does not fetch images from the network or bundle fonts.

### Generate the brand-report sample

The repository's Python generator creates synthetic test images without external
packages. It is a sample preparation tool, not a renderer dependency:

```sh
python3 scripts/png_fixtures.py --output artifacts/png-inputs
dotnet run --project src/FactsPDF.Cli -c Release -- examples/png-brand-report.html artifacts/png-brand-report.pdf --image logo=artifacts/png-inputs/brand-mark.png --page-numbers --header-text "FactsPDF | Business report" --footer-text "Synthetic example"
```

The transparent mark is drawn over the report's green brand panel. The example
uses fontless ASCII so it needs no external font download; existing explicit
font options can be supplied for other templates. Its illustrative line-item
values are not a product quote. New sample reference approval remains separate
from executable engineering verification.

For a logo beside company information, use the newer complete
[branded business report](../examples/branded-business-report.html):

```sh
python3 scripts/png_fixtures.py --output artifacts/png-inputs
dotnet run --project src/FactsPDF.Cli -c Release -- examples/branded-business-report.html artifacts/branded-business-report.pdf --image logo=artifacts/png-inputs/brand-mark.png --page-numbers --header-text "FACTSPDF | CLIENT REPORT" --footer-text "Synthetic capability sample"
```

## Bounds and diagnostics

| Loader limit | Default |
|---|---:|
| MaxBytes | 8,388,608 |
| MaxDimension | 8,192 |
| MaxPixels | 16,777,216 |
| MaxDecodedBytes (expanded color samples plus PNG alpha, when present) | 50,331,648 |
| MaxMcuBlocks (JPEG, including padded edge blocks) | 1,048,576 |

`PdfImageLimits` can override these positive bounds for an individual load. JPEG
also limits length-delimited segments to 256, total APP/COM payload to 65,536 bytes
and Exif IFD0 entries to 256. PNG checks expanded color/alpha size before allocation,
decodes with two bounded row buffers and compresses the separate PDF planes.
`ByteLength` always reports original encoded bytes. Conversion defaults remain
16 image bindings and 33,554,432 original encoded bytes
(`PdfOptions.MaxImageResources/MaxTotalImageBytes`), including unused aliases.
Existing input, page, display-command and output-byte bounds also apply, with
PNG soft-mask bytes included in output accounting. These are work/data bounds,
not a total process-memory guarantee; caller buffers, encoded images, expanded
PNG samples, compressed planes and final PDF buffers can coexist while loading.

| Code | Meaning |
|---|---|
| FPDF1601 | Malformed/truncated JPEG/PNG, invalid encoded structure or checksum |
| FPDF1602 | Unsupported format, encoding, color, metadata or orientation |
| FPDF1603 | Image byte, dimension, pixel, scan-work or resource budget |
| FPDF1604 | Valid resource name has no explicit binding; located at img |

Invalid API options, map names and null resources throw argument exceptions. Invalid `src` uses `FPDF1103`; forbidden nesting uses `FPDF1101`. Duplicate src keeps the tokenizer's existing `FPDF1101` at the duplicate attribute. CSS preflight and existing layout/output errors keep their established order. Pre-output failures leave caller output unchanged; cancellation or I/O during the final copy can still partially write a caller stream.

Cancellation is checked between bounded reads, marker/chunk walks, CRC work,
metadata entries, entropy blocks, PNG inflation/unfiltering/expansion/compression,
layout and binary output chunks. A synchronous caller stream cannot be interrupted
while its read/write is blocked. Private WASM verification accepts explicit byte
arrays, bounds JS packing before copying, then applies .NET limits again.
Caller-owned arrays and interop copies are outside loader accounting; no
image-fetch API or browser cancellation UI is added.

## Evidence scope

`scripts/verify_png.py` generates eight authored PNG cases, verifies their decoded
colors independently, checks actual PDF color and alpha streams with pypdf, and
checks all 561 pixel centers per case after Poppler rendering over a colored
background. It also checks image reuse, malformed-input target preservation,
recovery and the one-page brand report. The existing WASM workflow runs these
cases through managed/Native AOT CLI and the actual protected-browser host.
Actual completed-run results, not the workflow configuration alone, establish
which platforms were verified.

`tests/fixtures/jpeg` contains 35 authored JPEGs, a single-page report and explicit per-entry contracts. `probes/images` and `scripts/verify_images.py` exercise direct API/managed CLI/Node/Linux Native AOT; the protected WASM workflow runs the shared .NET engine with supplied bytes. Verification checks raw DCT hashes, authored matrices, image reuse, qpdf, Poppler text/bounds, rendered corner/arrow/bar pixels, and unchanged output on failure. Deliberately removed/duplicated draws, changed geometry and changed image streams must fail inspection. Reference page approval remains separate from successful conversion.

[Multi-page composition samples](image-composition.md) additionally check automatic image movement around ordered/nested lists, image-only middle pages, four explicit font roles and sliced ancestor decoration.

The legacy nine-target denominator is unchanged. T-jpeg now checks missing explicit resources; positive D1 evidence is separate, as for C1 font styles. Tables, heading keep, page numbers and static page text now have separate implemented evidence; [F4 combined reports](combined-report.md) exercise them together with images. New user reference approval and complete release gates remain outstanding. No runtime dependency or standalone font redistribution is introduced.
