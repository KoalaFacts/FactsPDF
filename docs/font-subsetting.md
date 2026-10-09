# Font subsetting and resource baselines

FactsPDF can now build its own glyph subsets for the supported static TrueType fonts. This is opt-in while the implementation is experimental: full embedding remains the default, preserving previous behavior.

## Use

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/subset-document.html subset.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Use your own appropriately licensed, trusted static TrueType files. The paths are examples, not bundled assets. The library equivalent is `new PdfOptions { Fonts = [latin, chinese], SubsetFonts = true }`, passed to the existing `PdfConverter.Convert` API. Loaded font objects can be reused across requests. Omitting the option retains full embedding; a font whose embedding flags prohibit subsetting also remains fully embedded even when the option is selected. The engine never clears those flags.

## What is retained

Each used font gets its own subset containing glyph zero, used glyphs and all transitive composite components. Dense remapping updates component references and PDF CIDToGIDMap together. Unicode scalars retain distinct PDF character codes and ToUnicode mappings even if their glyph is shared. Actual advances, side bearings, contours, component transforms and hinting programs are preserved. New sfnt table offsets, lengths and checksums are generated; PDF names use deterministic six-letter subset prefixes.

The `name` table preserves original copyright/license records while updating supported PostScript-name records. `cvt `, `fpgm`, `prep` and `gasp` are retained when present. Unsupported optional glyph-indexed/layout tables and stale digital signatures are omitted, not copied with invalid indices. These are PDF-only embedded subsets, not general-purpose installable replacement fonts. This does not add kerning, shaping or CSS font selection.

## Limits and trust

Input fonts are still loaded in full and snapshotted. This increment reduces the embedded output and avoids compressing unused glyphs; it does not make font loading zero-copy or promise constant peak memory.

Composite traversal rejects missing components, cycles, truncated arguments/instructions, invalid transform combinations and excessive nesting. Maximums are 64 component levels, 1,024 components per glyph, 1,000,000 visited component edges, 65,535 requested scalar entries and 64 MiB of generated sfnt data. `FPDF1506` identifies malformed/unsupported subsetting structures. Existing input/font/output limits and cooperative cancellation also apply. These limits are not complete outline-bytecode sanitization: continue to use trusted fonts.

Fonts without subsetting permission fall back to full embedding. Other embedding restrictions continue to be rejected by the existing loader. Flag checks are not legal clearance for a supplied font.

## Reproducible verification

`.github/workflows/subsetting.yml` builds the CLI and a separate measurement harness as real Linux x64 Native AOT executables. `scripts/verify_subsetting.py` passes the same COMPLETE DejaVu Sans and Droid Sans Fallback inputs into both modes; no third-party tool pre-subsets them.

Independent verification checks:
- qpdf syntax/stream structure and Poppler embedded/subset/Unicode font reporting.
- The entire visible HTML text, identical extracted text between modes, and pixel-for-pixel equality at 120 DPI for both pages in the same Poppler environment.
- Each subset's exact Unicode coverage, transitive component count, glyph contours and advance/side-bearing metrics against the complete input font.
- sfnt checksums, retained copyright/license records and shared hinting programs.

fontTools is used only to INSPECT output fonts in memory. It is not in the engine, is not called to create these subsets, and no standalone fonts are uploaded. The artifact contains only the public test PDF, its two previews and `measurements.json`.

## Measurement interpretation

The JSON records source/check-out hashes, font/corpus hashes, machine/SDK information, native CLI size and raw samples.

| Measurement | Scope |
| --- | --- |
| Output bytes | Same document and complete input fonts, full versus subset mode |
| New-process CLI wall time | Five samples per mode, alternating order; includes launch/time-wrapper, initial font I/O/loading, rendering and output write/fsync |
| CLI peak RSS | Per-process high-water resident memory from GNU time, including every CLI phase |
| Font-load time/allocation | Native harness loads fonts from already-read bytes; one warm-up, five measured loads; file I/O excluded |
| Reused-font conversion | Native harness reuses fonts; three warm-ups and twenty measured conversions, including output-buffer allocation |
| Managed allocated bytes | Cumulative current-thread managed allocations, NOT peak memory and NOT native allocations |

Processes start afresh for CLI samples, but OS/filesystem caches are not flushed. This is not an isolated cold-disk startup measurement or a third-party engine comparison. A small two-page corpus on a shared CI runner is a baseline, not a production SLA, percentile guarantee or proof of universal speed. Font-load costs still exist and can be amortized by reusing font objects.

See [the execution ledger](subsetting-ledger.md) and exact-head PR checks for observed results. The broader HTML/CSS, shaping, format, platform and language-binding limitations in [the Unicode guide](unicode-fonts.md) remain.
