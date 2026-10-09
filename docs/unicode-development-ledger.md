# Unicode font development ledger

Plan: docs/superpowers/plans/2026-10-10-unicode-fonts.md
Base: ce6abd12664f494a9e862c8dc660f5d3cc0d1c42 (PR #1); work isolated in feat/unicode-fonts, stacked PR #2.

## Test baseline
- 4e7480f: font API stub and 43 new test cases. First CI found a test-fixture local-variable shadowing error; no behavioral red claim for that run.
- 3fe36b9: fixture compilation fixed. Run 37947518087, Ubuntu job 113877538889, compiled and ran 101 tests: 66 passed and 35 failed. All original 58 remained green. Font positive cases failed at the intentional stub, and CLI --font failed as unsupported. This is the behavioral red baseline.

## Implementation rulings
- Read and fully embed a supplied static glyf TrueType font; do not implement subsetting yet. This costs output size and compression work, measured separately later.
- Prefer one format-12 Unicode cmap over format 4; do not merge incompatible mappings. Inspect cmap 4 idRangeOffset/idDelta and format 12 supplementary values.
- Use distinct PDF character codes per Unicode scalar even when two scalars map to the same glyph. This preserves copying/searching aliases.
- Keep the original fontless ASCII serializer. Unicode rendering requires explicit fonts, preventing ambient font discovery or silent platform substitution.
- OpenType fsType preview-and-print alone requires read-only document handling not provided by this increment. Conservatively reject it with FPDF1505, as well as restricted/bitmap-only permissions, rather than claim compliance. The earlier test expecting acceptance of fsType=4 must be corrected to match this specification-based restriction. Callers remain responsible for their actual font licenses.
- Font structural validation is not a full TrueType instruction/outline sanitizer. Only trusted, appropriately licensed font resources should be supplied.

No local .NET compiler is available; compiler/test/AOT evidence must come from real GitHub Actions. No passing claim for the implementation until its CI is inspected. License files are unchanged; no merge or package publication is authorized.
