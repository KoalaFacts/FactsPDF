# Unicode font development ledger

Plan: docs/superpowers/plans/2026-10-10-unicode-fonts.md
Base: ce6abd12664f494a9e862c8dc660f5d3cc0d1c42 (PR #1); isolated branch feat/unicode-fonts, stacked draft PR #2.

## Execution evidence
- 4e7480f: font API stub and 43 new test cases. First CI found a test-fixture local-variable shadowing error; no behavioral red claim for that run.
- 3fe36b9: fixture compilation fixed. Run 37947518087, Ubuntu job 113877538889, compiled and ran 101 tests: 66 passed and 35 failed. All original 58 remained green. Positive font cases failed at the deliberate stub; CLI --font failed as unsupported. This is the behavioral red baseline.
- 66d052b: first implementation. Run 37948670275, Ubuntu job 113881473781: 98/101 passed, including font loading, fallback, Chinese/supplementary text round-trip, metrics and CLI. Three remaining failures were an inappropriate preview-only embedding expectation and two attribute-serialized lone-surrogate test inputs.
- 237129d: corrected the preview-only expectation to fail closed and constructed lone UTF-16 surrogates at runtime (attribute UTF-8 serialization had replaced them before the test). All three OS test jobs passed in run 37949154223. AOT compiled and the original ASCII PDF passed; the new external-font fixture initially required fsType=0 but Droid reports fsType=8 (editable embedding). Its independent preparation check was corrected to accept 0 or 8 without changing font permission bits.
- 7d7cef: added ten boundary/review cases. Run 37949489803, Ubuntu job 113884244918: 111 tests ran, 107 passed, 4 failed as expected for missing head-version validation, two OS/2 version-specific length checks and the binary PDF header marker. This establishes red evidence for those targeted corrections. The same run's Native AOT job 113884244321 successfully generated and independently verified real two-page Chinese PDFs with full original and test-prepared fonts.
- The subsequent implementation enforces those version/length checks and emits the binary marker. Its fresh CI/checks on PR #2 must be inspected separately before claiming completion; no result is invented here.

## Design rulings and self-review
- Fully embed each used explicit static glyf TrueType font object once per PDF; subsetting is deliberately not implemented. This costs output size, compression work and memory, not yet benchmarked. A test independently decompresses FontFile2 and verifies the exact supplied bytes.
- Prefer one Unicode cmap 12 over format 4; do not merge incompatible maps. Test cmap 4 idRangeOffset/idDelta, including raw glyph zero staying zero, and cmap 12 supplementary values. A synthetic ranged fixture deliberately maps A to a raw zero; other variants cover A normally.
- Distinct scalars receive distinct PDF CIDs even when their glyphs are identical. This preserves Unicode aliases during copying/searching.
- Keep the fontless ASCII serializer unchanged. Supplying fonts enables the new path; there is no ambient system font discovery or silent missing-glyph substitution.
- Preview-and-print alone requires read-only document handling not provided here. Conservatively reject it, restricted and bitmap-only permissions. The flag check is not a legal clearance; actual font licenses still apply.
- Self-review found the missing version-specific validation and binary marker; tests reproduced them before correction. Font validation remains structural, not a complete glyph/hinting sanitizer or checksum audit. Use trusted fonts.
- This is self-review, not independent human review. Further review, curated malformed-font fuzzing, shaping, subsetting and resource profiling remain open before production.

## Verification boundaries
The editing environment has no .NET SDK and cannot download one. Actual compiler, NUnit and AOT evidence comes from GitHub Actions, not a claimed local build.

Real-font CI uses OS-installed DejaVu Sans and Droid Sans Fallback. fontTools only prepares small verification inputs; both the original complete inputs and prepared inputs are passed to our native executable. Poppler and qpdf inspect output independently. None of those tools is a runtime engine dependency. No font files are committed, packaged or uploaded, and no package is published.

All six inherited license/contribution-policy files are unchanged. README licensing text is preserved. Both PRs remain draft and unmerged. Final evidence belongs in the PR checks and verification comment tied to the exact head, not a claim that a previous green commit proves later changes.
