# PNG and transparency for branded business reports

Date: 2026-10-24. Baseline: `4d810430ee550bdab212fc322be5a8d7779a511e`.

## Intent and authorization

The user asked to commercialize FactsPDF, reviewed the functional gaps, and
instructed us to proceed ("干就是了"). This starts the accepted priority of brand
images and composition. Decisions below are implementation choices, not a claim
that the user reviewed new PDF reference pages or approved publication. Retain
one implementation PR at a time, independent review, merge and main verification.

The first deliverable is real static PNG input and PDF transparency through
`PdfImage.LoadImage`, `PdfOptions.Images` and CLI `--image`. The next composition
increment places images with business text. This increment does not claim image
alignment, table-cell images or rich headers.

## Approaches and decision

1. Convert PNG to JPEG externally: cheap but loses alpha and adds customer work.
2. Add a third-party image library: broad support but adds a runtime dependency.
3. Extend the current bounded reader and PDF writer: selected by the implementer
   to preserve entry points and the current dependency constraint. Use BCL
   `System.IO.Compression` for zlib, already used for font streams.

## Input and output contract

- Recognize PNG by signature, not path/MIME. Preserve existing JPEG output bytes.
- Static non-interlaced PNG, compression/filter method zero; all five filters.
- Grayscale (type 0) at 1/2/4/8 bits; RGB (type 2) at 8 bits; indexed (type 3)
  at 1/2/4/8 bits with a valid palette; gray-alpha (type 4) and RGBA (type 6)
  at 8 bits. Expand packed samples to 8-bit Gray/RGB, including odd-width rows.
- Preserve explicit alpha and tRNS (gray/RGB keys and palette alpha) as an 8-bit
  PDF `/SMask`, with unassociated color. Never flatten onto an invented white.
- Reject 16-bit samples, Adam7 and APNG with `FPDF1602`; no silent downsampling
  or selecting an animation frame.
- DeviceGray/DeviceRGB matches the current image color model, not color-managed
  reproduction. Accept valid sRGB metadata; reject ICC/HDR transforms that cannot
  be honored. Document gamma/chromaticity/density treatment. Non-display ancillary
  text can be skipped without decompression after structure and CRC validation.
- Validate chunk ranges, type bytes/reserved bit, CRC, required order, singleton
  IHDR/PLTE/tRNS/IEND where applicable, consecutive IDAT, and exact EOF after IEND.
  Reject unknown critical chunks. Validate palettes, indices, tRNS lengths/ranges.
- Validate zlib data/checksum and exact expected decompressed scanline length;
  reject truncated, excessive, corrupt and invalid-filter input. Ignore unused
  bytes after the completed zlib stream in final IDAT, as PNG section 11.2.3
  permits; they are not an extra decompressed scanline or the zlib checksum.
- `FPDF1601`: malformed; `FPDF1602`: unsupported; `FPDF1603`: resource bound.

## Resource and compatibility contract

Reuse existing `PdfImageLimits`. Check dimensions/pixels/decoded bytes before
large allocation/inflation, with checked arithmetic. `MaxDecodedBytes` accounts
for expanded color plus alpha; filtered scanlines/row buffers must also be bounded.
Derive filtered bytes separately as `(ceil(width * channels * depth / 8) + 1) *
height`; a narrow gray image can have more filtered bytes than decoded samples.
`MaxBytes` and public `ByteLength` retain original encoded-byte meaning.
`MaxMcuBlocks` remains JPEG-specific; PNG work is bounded by bytes/pixels/rows.
These are work/data bounds, not a peak-RSS promise.

Keep immutable ownership, short/nonseekable streams, caller stream ownership,
resource-name binding, alias counting and aggregate encoded-byte limits. Repeated
placement of one resource emits one main image and at most one mask. Both ASCII
and Unicode writers must have valid references/xref offsets. Color/mask bytes count
toward `MaxOutputBytes`; preserve existing pre-output failure guarantees and
cooperative cancellation through parsing, inflation, rows and output.

## Verification

Use meaningful red/green NUnit cases for filters/formats, alpha, structures,
checksums, exact length, bounds, ownership and output preservation; verify CLI/API
and the existing private WASM byte adapter. A signature-only PNG becomes malformed,
so update that narrow legacy assertion, retaining other unsupported formats.

Generate independent pixel fixtures and a synthetic transparent-logo business
report. Inspect actual PDF image/mask bytes and rasterized output for orientation,
opaque/transparent/half-transparent samples, reuse and validity. Run the normal
suite and existing CI/AOT gates. Local managed tests are not Windows/browser runs.

Specification: https://www.w3.org/TR/png-3/ . This is a scoped decoder, not full
PNG/APNG conformance. New sample reference approval remains separate.
