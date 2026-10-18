# Explicit image resources

`PdfImage.LoadImage` recognizes the supplied bytes. This first implementation supports a bounded subset of baseline JPEG; PNG and other formats fail explicitly. The engine never interprets `src` as a file path, URL or data URI.

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

`img` is a void, standalone block at the root/body or inside ordinary supported containers. Paragraph/inline/list ancestors, `</img>`, HTML width/height, srcset and CSS height are rejected. `alt` is metadata only; it is neither a fallback nor Tagged PDF. Existing id/class/style selectors apply.

CSS width controls content width; percentages use the parent content width. Natural size uses 96 dpi (`pixels × .75pt`) regardless of JPEG density metadata. Height preserves the pixel aspect ratio. Padding, solid borders, background and currentColor work through the existing box model. Images remain left-aligned. No automatic shrinking, independent height, transforms, float, wrap, PNG, inline or list images are implemented.

The complete image outer box moves to the next page if necessary. An image plus required leading ancestor decoration that cannot fit an empty page fails `FPDF1302`. Its own padding/borders never split; ancestor decorations retain existing slice behavior. Each image draw costs one display command, plus normal decoration commands.

## Bounds and diagnostics

| Loader limit | Default |
|---|---:|
| MaxBytes | 8,388,608 |
| MaxDimension | 8,192 |
| MaxPixels | 16,777,216 |
| MaxDecodedBytes (width × height × components) | 50,331,648 |
| MaxMcuBlocks (including padded edge blocks) | 1,048,576 |

`PdfImageLimits` can override these positive bounds for an individual load. Additional fixed checks limit length-delimited segments to 256, total APP/COM payload to 65,536 bytes and Exif IFD0 entries to 256. Conversion defaults are 16 image bindings and 33,554,432 total compressed bytes (`PdfOptions.MaxImageResources/MaxTotalImageBytes`). Existing input, page, display-command and output-byte bounds also apply. These are work/data bounds, not a total process-memory guarantee; caller buffers, owned copies and the final PDF buffer can coexist.

| Code | Meaning |
|---|---|
| FPDF1601 | Malformed/truncated JPEG or invalid encoded structure |
| FPDF1602 | Unsupported format, JPEG encoding, color, metadata or orientation |
| FPDF1603 | Image byte, dimension, pixel, scan-work or resource budget |
| FPDF1604 | Valid resource name has no explicit binding; located at img |

Invalid API options, map names and null resources throw argument exceptions. Invalid `src` uses `FPDF1103`; forbidden nesting uses `FPDF1101`. Duplicate src keeps the tokenizer's existing `FPDF1101` at the duplicate attribute. CSS preflight and existing layout/output errors keep their established order. Pre-output failures leave caller output unchanged; cancellation or I/O during the final copy can still partially write a caller stream.

Cancellation is checked between bounded reads, marker walks, metadata entries, entropy blocks, layout and binary output chunks. A synchronous caller stream cannot be interrupted while its read/write is blocked. Private WASM verification accepts explicit byte arrays, bounds JS packing before copying, then applies .NET limits again. Caller-owned arrays and interop copies are outside loader accounting; no image-fetch API or browser cancellation UI is added.

## Evidence scope

`tests/fixtures/jpeg` contains 35 authored JPEGs, a single-page report and explicit per-entry contracts. `probes/images` and `scripts/verify_images.py` exercise direct API/managed CLI/Node/Linux Native AOT; the protected WASM workflow runs the shared .NET engine with supplied bytes. Verification checks raw DCT hashes, authored matrices, image reuse, qpdf, Poppler text/bounds, rendered corner/arrow/bar pixels, and unchanged output on failure. Deliberately removed/duplicated draws, changed geometry and changed image streams must fail inspection. Reference page approval remains separate from successful conversion.

[Multi-page composition samples](image-composition.md) additionally check automatic image movement around ordered/nested lists, image-only middle pages, four explicit font roles and sliced ancestor decoration.

The legacy nine-target denominator is unchanged. T-jpeg now checks missing explicit resources; positive D1 evidence is separate, as for C1 font styles. Tables, page furniture, heading keep and complete-report/release gates remain outstanding. No runtime dependency or standalone font redistribution is introduced.
