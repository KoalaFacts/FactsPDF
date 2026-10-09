# M2: TrueType subsetting and resource baseline

Continue the user's selected next step: reduce embedded font size without changing text, pagination, glyph appearance, or licensing. Extend the existing renderer, not a new subsystem or markup language. Work on a stacked draft branch; do not merge or publish packages.

## Contract

Add opt-in `PdfOptions.SubsetFonts` (default false) and CLI `--subset-fonts`. Preserve existing full-font output by default. Where OS/2 fsType forbids subsetting, retain full embedding even with the option selected; never clear permission bits. The current font support and trust boundaries remain.

For an eligible static glyf font, build a PDF-only subset from used Unicode scalars plus glyph zero and the transitive closure of composite components. Assign dense new glyph IDs in original glyph-ID order; rewrite component references and the PDF CIDToGIDMap. Preserve glyph programs, transforms, instructions, advances, side bearings and Unicode aliases. Keep ToUnicode independent from glyph identity. Rebuild glyf/loca/hmtx/cmap, head/hhea/maxp and checksums; retain font copyright/license names and hinting programs. Drop unsupported layout, optional glyph-indexed tables and invalidated signatures rather than copying stale indices. Prefix PDF subset names with six deterministic uppercase letters and '+'. A subset is for embedding, not a general installable replacement font.

Reject invalid composite references, cycles, malformed component records and component depth beyond 64 with FPDF1506. Traversal, sizes and cancellation remain bounded. This is not complete font-bytecode validation; fonts must remain trusted. Limit distinct requested scalars to 65,535 and check generated sfnt sizes before allocation. Preserve all original license files.

## Verification

Tests first: selected glyphs, empty/space glyphs, cmap 4 and 12 inputs, supplementary scalars, shared-glyph aliases, nested/duplicate composites, malformed composite flags/offsets/cycles, exact metrics/outlines, checksums, deterministic output, permission fallback, cancellation, PDF CID mappings and CLI option handling. Keep the 111 existing tests green.

Independent CI must use the SAME complete installed DejaVu/Droid inputs for full and subset modes. Do not preprocess them with fontTools. Verify source text and page geometry, compare rasterized output pixel-for-pixel in the same Poppler environment, inspect embedded fonts with fontTools/qpdf/pdffonts. Publish only the generated public PDF, two page previews and measurements, never standalone fonts or package artifacts.

Measure output bytes; repeated new-process CLI wall time and per-process peak RSS (includes startup, font loads, conversion and I/O); and separate managed font-load/reused-font conversion allocation/time baselines. Record hardware, OS, SDK/runtime, corpus and font hashes, modes, sample count and limitations. Shared CI measurements are preliminary, not guaranteed performance. No WASM/all-platform AOT or hostile-font safety claims.

## References
Independent implementation from Microsoft OpenType glyf, loca, hmtx, cmap, head and sfnt specifications. No third-party runtime parser/font/PDF library is added.
