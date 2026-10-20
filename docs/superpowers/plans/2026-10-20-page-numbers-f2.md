# F2 execution and native verification record

Baseline: F1 main `8c22cde00253b8cc441d5749b1b7e411e11028f7`; no open PR or duplicate executor at start. No checkout AGENTS.md or .agents/skills directory was present. The accepted F route and explicit continuation authorize this page-number-only increment. Design: [F2 page numbers](../specs/2026-10-20-page-numbers-f2-design.md).

1. Read the existing public options, CLI, fonts, CSS/layout and serializer contracts. Baseline full Release suite: 971 passed.
2. Independent design review before RED. Incorporated body-ink overhang rejection, per-digit font bounds, fixed line-height/rounding rules and precise pre-composition vs serialization error ordering. Selected one default-off bool/CLI flag; no new HTML/CSS or general footer model.
3. RED: 17 tests fail for the missing opt-in contract/CLI flag. The initial API lookup used reflection to compile against unchanged baseline; final tests use the typed property. Minimal compositor then passed all 17. Additional boundary/font/atomicity coverage brings the focused suite to 33 and full suite to 1004 passed.
4. Independent code review found no required fixes and independently passed 33/33 tests. It checked default compatibility, glyph fallback/origins, bounds/clearance, diagnostic order, exact run charging, cancellation and destinations.
5. Actual Linux Native AOT and direct .NET API host built. `scripts/verify_page_numbers.py` rendered 8 cases / 112 pages: empty, single, explicit/trailing breaks, F1 heading movement, E2 repeated table headers, bilingual/role fonts, full font embedding and 100-page digit transitions. API/CLI/AOT outputs match with numbering on and off; body text/coordinates remain unchanged. qpdf and exhaustive number/centering/clearance checks pass. Wrong-number and displaced-number mutations are rejected.
6. Default outputs for all 8 cases are byte-identical to the retained original F1 Native AOT executable, whose SHA-256 matches its F1 delivery manifest. Visual review covers all 12 bounded-corpus pages, full counter pages 1/9/10/99/100 and every one of the 100 footer strips.
7. Final Release build: zero warnings/errors. Node subprocess tests: 12 passed. Normal CI includes the new native inspector; successful future CI, resolved comments, normal protected merge and actual-main verification remain delivery gates, not claims made by this pre-merge record.

Reproduce after building the CLI, `probes/page-numbers` and Native AOT, using an explicit font manifest in the same existing format as the font-style probe:

```sh
python scripts/verify_page_numbers.py --manifest FONT_MANIFEST.json --output artifacts/f2/output \
  --dotnet dotnet --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --api-dll probes/page-numbers/bin/Release/net10.0/FactsPDF.PageNumbers.dll \
  --native-cli artifacts/f2/native/FactsPDF.Cli
```

An optional `--baseline-cli ORIGINAL_F1_EXECUTABLE` compares every default output with the original renderer. Fonts stay private; evidence contains generated documents, previews, hashes and logs. The first local inspector invocation ran before its API host build finished and failed for missing DLL; it was rerun after the successful build, with no product workaround.

No local Chrome printing, sandbox changes, Library upload retry, database, release package or public publication. Existing target-report/page-reference approval status is unchanged; this is functional native page-number verification.
