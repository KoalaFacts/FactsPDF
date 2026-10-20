# Combined bilingual reports (F4)

[10](../examples/combined-report-10.html), [30](../examples/combined-report-30.html) and [100](../examples/combined-report-100.html) synthetic quality-record reports are executable examples of the existing API. Each combines real normal/bold/italic/bold-italic fonts, decimal and nested disc lists, an explicitly supplied JPEG, fixed-column tables, whole-row pagination with repeated headers, heading keeps, page numbers and static header/footer text. F4 adds acceptance evidence, not engine features or public APIs.

## Run a report

Build `src/FactsPDF.Cli` with .NET 10. Supply licensed static TrueType files explicitly for all four roles. The examples use Latin followed by CJK fallback in each chain; the reproducible inspector uses the pinned DejaVu/Sarasa chains from `scripts/font_style_fonts.py`. Fonts are not distributed with the examples. The repository-owned procedural JPEG is `tests/fixtures/jpeg/rgb-420.jpg`.

```sh
dotnet src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  examples/combined-report-30.html report.pdf --subset-fonts \
  --font "$LATIN_NORMAL" --font "$CJK_NORMAL" \
  --font-bold "$LATIN_BOLD" --font-bold "$CJK_BOLD" \
  --font-italic "$LATIN_ITALIC" --font-italic "$CJK_ITALIC" \
  --font-bold-italic "$LATIN_BOLD_ITALIC" --font-bold-italic "$CJK_BOLD_ITALIC" \
  --image quality-chart=tests/fixtures/jpeg/rgb-420.jpg \
  --page-numbers --header-text 'FactsPDF | Quality review' --footer-text 'Internal copy'
```

The [direct public API example](../probes/combined-report/Program.cs) loads the same explicit resources into `PdfOptions` and calls `PdfConverter.Convert`; it does not invoke the CLI. Its extra negative modes are verification-host inputs, not public CLI flags. A successful conversion writes a PDF only after conversion completes in memory. Negative probes additionally seed a caller stream and verify its original bytes and position.

## Independently authored expectations

The reports use A4, 36pt margins, 11pt body text with line-height 2, and 80/160/280pt columns. The 362pt introduction places the table at physical y=398pt. Each row has 4pt padding and 1pt borders: ordinary rows are 32pt; every tenth row has an explicit second line and is 54pt. Table headers are 32pt. These are input design constraints, not geometry harvested from generated PDFs.

| Records | Pages | Records on each page | Closing decision |
|---|---|---|---|
| 10 | 2 | 10, 0 | 24pt heading + 44pt paragraph move together to page 2 |
| 30 | 2 | 11, 19 | Follows the table on page 2 |
| 100 | 6 | 11, 21, 21, 21, 21, 5 | Follows the table on page 6 |

The short table remains whole on page 1. An explicit `break-after:auto` counterfactual places its closing heading on page 1 and its paragraph on page 2, demonstrating that the positive report actually exercises F1. Disabled-furniture variants independently satisfy the same authored body layout and preserve body pixels. The positive corpus totals ten pages; the three disabled-furniture variants and one counterfactual add twelve pages. All 22 generated variant pages have separate engineering inspection accounting; API/managed CLI/NativeAOT copies must be byte-identical.

## Reproduce engineering acceptance

Install qpdf, Poppler, Python fontTools, pypdf and Pillow. Prepare the explicit font manifest as in the existing font-style verification workflow. Build the direct API probe, managed CLI and real Linux NativeAOT CLI, then run:

```sh
dotnet build probes/combined-report/FactsPDF.CombinedReport.csproj -c Release
dotnet build src/FactsPDF.Cli -c Release
dotnet publish src/FactsPDF.Cli -c Release -r linux-x64 -p:PublishAot=true -o artifacts/f4-native
python3 -m unittest discover -s scripts -p 'test_combined_report.py' -v
python3 scripts/verify_combined_report.py \
  --manifest artifacts/font-manifest.json --output artifacts/combined-report \
  --dotnet "$(command -v dotnet)" \
  --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --api-dll probes/combined-report/bin/Release/net10.0/FactsPDF.CombinedReport.dll \
  --native-cli artifacts/f4-native/FactsPDF.Cli
```

Use a fresh output directory. `scripts/combined_report_corpus.py` reproduces the committed HTML only; it never generates expected PDF geometry or reference hashes. `verify_combined_report.py` separately specifies every visible line, cell and page. It checks exact character order and English word separation through two PDF parsers; every character's font, size, color and position; source glyph outlines, advances and subset mappings; consumed rectangle/disc paint operations and their ordering before text; effective text graphics transforms and saved/restored color/font state; image bytes, dimensions, matrix and rendered orientation/color samples; heading movement; page text placement; and cross-entry bytes. Every tenth row's two baselines must be 22pt apart. Actual NativeAOT pages are rasterized and recorded with PDF/page-image hashes for visual review.

Real-PDF mutations test missing, duplicate and reordered records, missing repeated headers, collapsed note baselines, wrong font roles, missing list markers/discs, missing/altered/moved images, unpainted/white/reordered table fills, shifted header/footer, wrong counter, detached heading, translated text graphics state and backgrounds moved over text and saved/restored state that makes text white. An unmodified pypdf rewrite must first pass the same complete inspector. Each mutation must fail its intended assertion, not an unrelated serializer error.

Negative report variants check missing image/font resources, overwide and overheight rows, excessive footer width and raw length. All applicable CLI/NativeAOT failures must leave stdout empty, existing files unchanged, new files absent and no temporary files. Direct API probes also check page/output/display quotas and preserve caller stream bytes and position. Recovery must reproduce the original successful bytes. These checks cover pre-output failures; the existing final caller-stream I/O contract is unchanged.

The normal protected browser-WASM workflow runs this native acceptance step before its existing browser-engine tests. F4 does not require browser printing, disable browser security, change original baselines, run G performance budgets or publish packages.

## Approval and release boundary

The original six native baseline cases (ten pages) already have human approval in [reference-reviews.json](acceptance/reference-reviews.json). F4 verifies all their retained PDF/text/page hashes without changing the registry. That approval remains bound to the original source and artifacts.

New combined reports are **pending user reference review**. Automated checks, agent visual inspection, CI and code review are engineering evidence; they do not freeze new output as human truth. The original 6 baseline / 12 negative / 9 target denominator and legacy malformed/unbound entries remain unchanged. Separate positive evidence is mapped in the [support matrix](acceptance/support-matrix.md). Current-source reference approval, remaining release gates, fixed-machine performance budgets and publication are not completed by F4.
