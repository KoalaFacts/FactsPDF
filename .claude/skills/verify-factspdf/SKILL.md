---
name: verify-factspdf
description: How to exercise FactsPDF for real (CLI and Pdf.Write() SDK rendering examples/*.html into PDFs), which evidence to capture per feature area, and what looks green here but proves nothing.
---

# Verify FactsPDF

The real boundary is the CLI (`src/FactsPDF.Cli`) and the `Pdf.Write()` SDK
(`src/FactsPDF/Pdf.cs`) turning `examples/*.html` into PDF bytes. Evidence is
exit codes, the CLI's `Wrote N page(s), B bytes.` line or its `FPDF####` error,
byte parity against `main`, and rendered pages rasterised and looked at.

## Setup
- .NET 10 SDK: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet`,
  then `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`. Without
  `DOTNET_ROOT` the built apphost `FactsPDF.Cli` exits 131 ("You must install .NET").
- Build: `dotnet build src/FactsPDF.Cli -c Release`; run
  `src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli <in.html> <out.pdf> [options]` (`--help` lists them).
- Fonts: Latin `/usr/share/fonts/truetype/dejavu/DejaVuSans*.ttf`. Chinese examples need a
  static TrueType font; the system only has collections, so extract one:
  `python3 -c "from fontTools.ttLib import TTCollection; TTCollection('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc').fonts[0].save('zh.ttf')"`.
  Bold/italic text needs its own chain (`--font-bold`, `--font-italic`, `--font-bold-italic`), else FPDF1507.
- Images: `python3 scripts/png_fixtures.py --output <dir>`, then
  `--image logo=<dir>/brand-mark.png --image chart=<dir>/rgb.png --image quality-chart=<dir>/rgba.png`.
- Look at pages: `pdftoppm -r 40 -png -f 1 -l 1 out.pdf page` and open the PNG.

## Feature map
Areas follow `site/data/compatibility.json` `categories`. "Run" is the command used when this map was written.

| Feature (area) | Code entry points | How to exercise it for real | Evidence to capture | Neighbours at risk |
| --- | --- | --- | --- | --- |
| HTML elements | `HtmlDocumentReader.cs`, `HtmlTokens.cs`, `DocumentModel.cs`, `BlockLayout.cs`, `ListLayout.cs`, `Tables/` | Run `examples/first-document.html` (default options) and `examples/lists-report.html` with DejaVu + zh.ttf | `Wrote 2 page(s)` / `Wrote 5 page(s)`; rasterised page 1 | tables, pagination, list markers |
| HTML attributes and parsing | `HtmlDocumentReader.cs`, `HtmlTokens.cs`, `MaxInputCharacters`/`MaxElements`/`MaxDepth` | `examples/report-display-visibility.html`; limits via `dotnet test tests/FactsPDF.Tests -c Release --filter "FullyQualifiedName~ConversionTests|FullyQualifiedName~EarlyDepthLimitTests"` (input, element and depth limits; the depth limit is also checked while the collecting pass reads tokens) | `Wrote 3 page(s)`; FPDF1001–1003 at limits; a `--filter` that selects 0 tests proves nothing | CSS attribute selectors |
| CSS properties | `InlineCss.cs`, `Css/`, `BlockLayout.cs` | `examples/stylesheet-document.html` with DejaVu + zh.ttf `--subset-fonts` | `Wrote 2 page(s)`; page image | every report-* example (parity) |
| CSS values | `Css/` (colours, lengths, keywords) | `examples/report-hsl-colours.html` | `Wrote 2 page(s)`; colours visible in raster | named/rgb colours, em/rem lengths |
| Selectors and syntax | `CssSyntax/`, `Css/` stylesheets, CSS budgets (FPDF1205) | `examples/report-attribute-operators.html` | `Wrote 2 page(s)`; FPDF12xx on bad CSS; a `f(` nesting flood fails FPDF1205 at the first opener past `MaxCssSyntaxDepth` (`EarlyCssDepthLimitTests`); a 100,000-rule flood over `MaxCssSyntaxNodes` stops after the limit with a few MB allocated (`EarlyCssNodeLimitTests`); the same flood over `MaxCssSelectors` fails at the first rule past the budget (`EarlyCssSelectorLimitTests`); a declaration flood over `MaxCssDeclarations` fails at the first declaration past the budget (`EarlyCssDeclarationLimitTests`) | cascade order, `!important` |
| Text, language and fonts | `TextLayout.cs`, `TextLines.cs`, `Fonts/`, `FontRoles.cs`, `UnicodePdfSerializer.cs` | `examples/unicode-document.html` and `examples/tables-report.html` with the four font chains (`tables-report` needs `--subset-fonts`; without it the full-embedding run fails with FPDF1401) | `Wrote 2 page(s)` / `Wrote 1 page(s)`; CJK and bold/italic visible in raster; FPDF1301/1504/1507 without fonts, and the CLI's `Location: line L, column C.` names the element whose text failed (`TextErrorLocationTests`, `LayoutErrorLocationTests`, `CliErrorLocationTests`); a dashed border of 0.0005pt fails `FPDF1302` with the location of its box (`PaintErrorLocationTests`) | subsetting, wrapping, decorations |
| Integration and platform | `Pdf.cs` (`Pdf.Write()`), `Api.cs`, CLI `CliApplication.cs`, `probes/browser-wasm`, Native AOT in CI | `dotnet test tests/FactsPDF.Tests -c Release --filter "FullyQualifiedName~OutputCancellationTests\|FullyQualifiedName~ConversionTests"` (all six SDK output paths); `examples/png-brand-report.html` with `--image` | `Passed: 56`; `Wrote 1 page(s)`; FPDF1604 when an image is not supplied | AOT/wasm trimming (CI jobs `native-aot`, `browser-wasm`) |
| Pagination and PDF | `PdfSerializer.cs`, `PdfBuffer.cs`, `PageFurniture*.cs`, `PageMarginLayout.cs`, `HeadingKeep.cs` | `examples/report-section-page-breaks.html`; `examples/quick-start.html --page-furniture examples/business-page-furniture.json --font DejaVuSans.ttf` | `Wrote 3 page(s)`; `Wrote 1 page(s)`; furniture text in raster | page numbers, header/footer text, margins |

## Gate
1. Build every csproj except `examples/first-use` in Release with warnings as errors:
   `for p in $(git ls-files '*.csproj' | grep -v examples/first-use); do dotnet build "$p" -c Release -warnaserror; done`.
2. `dotnet test tests/FactsPDF.Tests -c Release` (includes the CLI tests).
3. Byte parity against `main`: `node scripts/parity.ts` (Node 22.18+ strips types; defaults `--base origin/main`).
   Expect `N/N runs identical` (every `examples/*.html` × 3 option sets; 142 × 3 = 426 on 3e273b9) unless the PR explains an intended difference; a known kind
   (`location-added`) is declared with `--expect <kind>` locally and a `Parity-Expect: <kind>`
   trailer on the PR head commit for CI.
4. Docs: `node --test scripts/check-docs.test.ts && node scripts/check-docs.ts`.
5. `site/` data changes: `cd site && npm test && npm run validate`.

## Traps
- Most examples exit 3 under default options (missing CJK font or image). In parity that is
  expected and compared; a change that turns exit 3 into 0 or the reverse is a behaviour change.
- Parity compares bytes; a comments-only `src/` change is identical by construction, but run it anyway.
- Allocation tests (`AllocationBudgetTests`) only go down; a green run after raising a budget proves nothing.
- `dotnet test --no-build` after editing code runs the old build.
- The `.ttc` fonts on the system are rejected; only a static `.ttf` counts as a supplied font.
- Hosted CI may stay queued for long periods; the local gate is the evidence until it runs.

## Last verified
2026-11-06, main 14b717b: every feature-map row run as listed (all exit 0, page counts as shown;
`tables-report` page 1 rasterised and checked), gate on #191's head 3153 tests passed, parity 402/402.
