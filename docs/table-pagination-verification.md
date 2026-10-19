# E2 whole-row pagination verification

Scope: native fixed-column table pagination only, following merged E1 #33 (`194c6e2`). No new API or syntax, no cell splitting or F work. The previous E2 preparation described as 37 HTML inputs / 15 call variants was not present in this checkout, nor were AGENTS.md / checkout `.agents/skills`; this change uses the accepted release and E1 specifications and an independently authored minimal corpus.

## Reproduction

Use the SDK in `global.json`, qpdf, Poppler, Python with pypdf/Pillow/fontTools and the existing pinned `scripts/font_style_fonts.py` manifest. Fonts remain private verification inputs.

```sh
dotnet test FactsPDF.slnx -c Release
dotnet build probes/tables -c Release
dotnet build src/FactsPDF.Cli -c Release
dotnet publish src/FactsPDF.Cli -c Release -r linux-x64 -p:PublishAot=true -o artifacts/e2-native
python3 scripts/verify_table_pagination.py \
  --manifest artifacts/font-manifest.json --output artifacts/e2-output \
  --dotnet "$(command -v dotnet)" \
  --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --api-dll probes/tables/bin/Release/net10.0/FactsPDF.Tables.dll \
  --native-cli artifacts/e2-native/FactsPDF.Cli
```

The existing protected WASM workflow also runs this native inspector and a 100-row WASM case. Browser printing is unrelated and was not run locally. No browser sandbox settings or Library uploads were attempted. CI browser/WASM results are recorded separately from local native results.

## Local execution, 2026-10-19

- Clean baseline: 917 existing tests passed. Initial E2 RED: 9 failures / 3 passes, with long tables rejected by E1's whole-table height check. The initial GREEN suite passed 930 tests; after independent-review fixes, the final full suite passed 935 tests.
- Initial assertion corrections were based on source contracts, not frozen output: Courier at 10pt / 20pt line height has 13pt ascent (`8 + (20-8-2)/2`), not 15pt; page/output quota diagnostics do not promise source offsets. Layout-specific failures still assert offsets.
- Release API probe built with zero warnings/errors; actual Linux x64 Native AOT published using the installed GCC toolchain (`-p:CppCompilerAndLinker=g++`). No engine dependencies were added.
- API, managed CLI and fresh Native AOT produced identical bytes for all six cases below. API host checks exact output quota, error destination preservation, cancellation and recovery determinism.
- qpdf structure checks and Poppler text extraction passed. Independently authored 28pt cell backgrounds, text origins, page bounds and page-local row IDs were checked. Actual missing-header and displaced-text PDF mutations were rejected.
- All 16 distinct E2 page rasters were inspected: complete rows, repeated headers, readable Chinese, aligned columns and no clipping, overlap or ghost pages. This is agent inspection, not user reference approval.
- Existing E1 inspector passed 24 fixture/entry results, its boundary cases and two real-font examples (1 and 2 pages). Six distinct E1 page appearances were inspected. Five short-table inputs were also rendered by a separately built original `194c6e2` CLI and the changed CLI and compared byte for byte.

| Authored case | Pages | Body rows on each page |
| --- | --- | --- |
| 10 rows | 1 | 10 |
| 30 rows | 2 | 26, 4 |
| 100 rows | 4 | 26, 26, 26, 22 |
| 30 rows after 35 preceding lines | 3 | 1, 26, 3 |
| 100 rows without header | 4 | 27, 27, 27, 19 |
| 30 bilingual rows | 2 | 26, 4 |

These counts derive from the authored A4 usable height 769.89pt and 28pt rows, not a snapshot copied from the renderer. Local evidence is in `artifacts/e2/{output,e1-regression,compatibility}/report.json`, corresponding PDFs, extracted text, bounding-box XML and PNGs. CI retains its generated public artifacts under `browser-wasm-evidence`.

Implementation choices: short/long classification uses the table's complete height against an empty usable page, keeping required ancestor-leading-space failure behavior for short tables. Long-table decoration follows existing sliced blocks (top first / bottom last), and the last row reserves bottom decoration. These choices preserve the approved E1 defaults while implementing E2 without a new API.

No public release, numeric performance budget or user-approved visual reference is claimed. Independent review and exact commit CI/merge status belong to the delivery PR.

## Independent code review

A fresh reviewer inspected the full E2 change and independently reproduced two P2 findings using a separate temporary layout probe. Both were fixed with failing tests first: (1) zero-height leading/trailing rows could produce header-only fragments, covered by three RED→GREEN cases; (2) top+bottom padding was incorrectly checked as a single-page sum, covered by two RED→GREEN cases with/without a header. Zero-height rows now travel with adjacent occupied rows and last-row bottom decoration; top and bottom edges are validated individually, with actual fragment fit deciding their placement. The reviewer reported no other material findings. Final suite: 935 passed, zero failures. No deferred review findings. A separate post-fix public-API probe generated the three review reproductions as real PDFs (4/2/3 pages); all nine pages passed qpdf and exact page-text assertions and were visually inspected.

Committed engine evidence: `4a3869c4fba6a0d2cf02dc466d385d5f2cb065fe`; clean-tree reruns are in `artifacts/e2/final-output/report.json` and `artifacts/e2/final-e1/report.json`. Final PDFs match the inspected candidates byte for byte. Delivery: [PR #34](https://github.com/KoalaFacts/FactsPDF/pull/34), ready for review; normal CI and unresolved-review checks remain the merge gates.
