# Explicit TrueType fonts and Unicode text: M1 development slice

## User-approved direction
Extend the existing HTML/CSS-to-PDF core with explicit font loading and Unicode/Chinese output. Keep one shared core, browser-free runtime, AOT compatibility, and the current licensing boundaries. This is not a new markup language or a promise of full multilingual typography.

## Scope of this increment
- Load a caller-supplied static TrueType sfnt with glyf outlines from bytes or a readable stream. Snapshot bytes, leave streams open, bound reads, and permit immutable fonts to be reused concurrently.
- Read Unicode cmap format 4 (including glyphIdArray/idDelta) or 12 (including supplementary scalars), actual hmtx advances, and hhea/OS/2 vertical metrics. Prefer a supported full-repertoire cmap consistently rather than mixing incompatible maps.
- Validate table offsets, lengths, metrics, cmap ranges and loca bounds. Reject unsupported collections, CFF, variable/color fonts, restricted embedding and bitmap-only embedding. This is structural validation, not a complete TrueType program sanitizer; use trusted font files.
- Options take an ordered list of at most eight fonts with a default aggregate limit of 64 MiB. Per-load default limit is 32 MiB. A supplied font chain must contain each used glyph; do not silently replace missing text with a box or question mark. Without fonts retain the original ASCII/Courier behavior.
- Decode Unicode scalars, not individual UTF-16 code units. Support horizontal Chinese and simple Latin/Greek/Cyrillic text and standalone supported symbols. Explicitly reject invalid UTF-16 and scripts/sequences requiring shaping or bidi that this slice does not implement.
- Wrap Chinese at supported ideographic boundaries while keeping common opening punctuation off line ends, closing punctuation off line starts, and NBSP-connected words together. Do not claim full UAX #14, Japanese typography, grapheme shaping or line-breaking conformance.
- Embed each used supplied font once per PDF as a compressed full font. Use Type 0/CIDFontType2, explicit CIDToGIDMap, actual widths and a ToUnicode map. Allocate character codes per scalar, not per glyph: two characters mapped to one glyph must remain distinct when copied. Encode supplementary scalars as UTF-16BE surrogate pairs in ToUnicode.
- Add repeatable CLI --font arguments. Only explicit font paths may be read. Protect the input HTML and font files from becoming the output destination, including when --overwrite is selected.

## Compatibility and deferrals
Existing unconfigured ASCII output and its tests remain compatible. CSS font-family, @font-face, automatic system discovery, remote fonts, font subsetting, kerning/GSUB/GPOS, RTL, combining/variation sequences, colored emoji, and tagged PDF remain out of scope. Full embedding is deliberately measurable first; do not claim minimum PDF size or tuned peak memory.

## Acceptance
Tests must cover valid and malformed fonts, cmap 4/12, hmtx repeated advances, missing glyphs, shared-glyph Unicode aliases, non-BMP characters, CJK wrapping/punctuation, fallback, output limits, culture independence, cancellation, concurrent font reuse and CLI file safety. Keep the existing suite passing. Run Windows/Linux/macOS tests and a real Linux x64 Native AOT CLI conversion. Independently inspect font embedding, text extraction and rendered pages using a real Chinese-capable font used only in verification. Do not commit or distribute font files in the repository/package. Preserve all license files exactly and do not merge or publish packages automatically.

## References for independent implementation
- https://learn.microsoft.com/en-us/typography/opentype/spec/cmap
- https://learn.microsoft.com/en-us/typography/opentype/spec/hmtx
- https://learn.microsoft.com/en-us/typography/opentype/spec/head
- https://learn.microsoft.com/en-us/typography/opentype/spec/hhea
- https://learn.microsoft.com/en-us/typography/opentype/spec/os2
- https://www.unicode.org/reports/tr14/
- https://pdf-issues.pdfa.org/32000-2-2020/clause09.html

These are specification references, not dependencies or copied parser implementations.
