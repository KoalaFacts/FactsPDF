---
name: verify-factspdf
description: How to exercise FactsPDF for real (CLI, Pdf.Write() SDK, byte parity, rasterised pages) and what evidence proves a change works.
---

# Verify FactsPDF

Use with `verification-first`. The real boundary is the CLI and the
`Pdf.Write()` SDK turning `examples/*.html` into PDFs: compare bytes against
`main`, check the PDF structure, and rasterise the pages and look at them.

## Setup

- .NET 10 SDK: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir <scratch>/dotnet`,
  then `export PATH=<scratch>/dotnet:$PATH DOTNET_ROOT=<scratch>/dotnet`
  (the CLI app host needs `DOTNET_ROOT` when .NET is not in `/usr/share/dotnet`).
- Node 22.18+ or 24 runs the TypeScript scripts directly (`node scripts/<name>.ts`).
- Python 3 for the existing fixture scripts (`python3 scripts/png_fixtures.py --output <dir>`).
- Inspectors: `qpdf --check`, `pdftotext`, `pdftoppm -r 50 -png`.
- Build the CLI: `dotnet build src/FactsPDF.Cli -c Release`; run it as
  `src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli <input.html|-> <output.pdf|-> [options]`
  (`--help` lists every option).
- Font for non-ASCII examples: `/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf`.

## Feature map

Areas follow `site/data/compatibility.json` categories. Each row was run once
on the commit under "Last verified".

| Feature | Code entry points | How to exercise it for real | Evidence to capture | Neighbours at risk |
| --- | --- | --- | --- | --- |
| HTML elements (HTML元素) | `src/FactsPDF/HtmlDocumentReader.cs`, `DocumentModel.cs`, `BlockLayout.cs`, `ListLayout.cs`, `Tables/` | `FactsPDF.Cli examples/quick-start.html out.pdf --overwrite` (expect `Wrote 1 page(s)`); `examples/lists-report.html`, `examples/business-table-rich-cells.html` | exit code, page count, `pdftotext` text, page PNG | tables, lists, pagination |
| HTML attributes and parsing (HTML属性与解析) | `HtmlTokens.cs`, `HtmlDocumentReader.cs` | `printf '<p>x</p><table><tr><td>a' \| FactsPDF.Cli - -` → `FPDF1101 ... Location: line 1, column 16.` | exact error code and location | every HTML example (parity) |
| CSS properties (CSS属性) | `Css/`, `InlineCss.cs`, `BlockLayout.cs`, `PdfPaintSerializer.cs` | render `examples/report-*.html` and the matching `*-oracle.html`; compare page PNGs | page PNGs side by side, parity | box painting, text layout |
| CSS values (CSS取值) | `Css/`, `CssSyntax/` | `examples/report-hsl-colours.html`, `report-em-layout.html`, `report-font-size-keywords.html` | PNG, parity | all styled examples |
| Selectors and syntax (选择器与语法) | `CssSyntax/`, `Css/` | `FactsPDF.Cli examples/report-attribute-operators.html out.pdf` → `Wrote 2 page(s)`; text starts `Invoice status register` | text, PNG vs `report-attribute-operators-oracle.html` | cascade order, every stylesheet example |
| Text, language and fonts (文字语言与字体) | `TextLayout.cs`, `TextLines.cs`, `Fonts/`, `UnicodePdfSerializer.cs` | `examples/font-styles.html` without `--font` → exit 3 `FPDF1504 No supplied font contains U+6837`; with `--font <DejaVu> --subset-fonts` → PDF | exit code and message; with fonts: `qpdf --check`, PNG | subsetting, wrapping, page furniture |
| Integration and platform (接入与平台) | `src/FactsPDF/Pdf.cs` (`Pdf.Write()`, `ToBytes`/`ToStream`/`ToStreamAsync`/`ToBufferWriter`/`ToFile`/`ToFileAsync`), `src/FactsPDF.Cli/CliApplication.cs`, `probes/browser-wasm` | `dotnet test tests/FactsPDF.Tests -c Release --filter "FullyQualifiedName~PdfWriterTests\|FullyQualifiedName~WasmProbeTests"`; CLI `--help`, `--version`, `--page-size b9` → exit 2 with usage | test counts, CLI exit codes and stderr | CLI tests, AOT and wasm CI jobs |
| Pagination and PDF (分页与PDF) | `PdfSerializer.cs`, `PdfBuffer.cs`, `PageFurniture*.cs`, `PageMarginLayout.cs`, `PdfDocumentMetadata.cs`, `PdfLinkAnnotations.cs` | `FactsPDF.Cli examples/<html> out.pdf --page-furniture examples/business-page-furniture.json --page-width 148mm --page-height 210mm --font <DejaVu>`; `--page-numbers --header-text H --title T` | `qpdf --check`, page count, PNG of first and last page | output buffer, metadata, allocation budgets |
| Byte parity (every area) | `scripts/parity.ts` | `node scripts/parity.ts --work <scratch>` (builds `origin/main` and the working tree, 3 option sets × every example) → `parity: N/N runs identical` | the summary line and `parity.json` | everything |
| Allocations (hot path) | `tests/FactsPDF.Tests/AllocationBudgetTests.cs` | `dotnet test tests/FactsPDF.Tests -c Release --filter FullyQualifiedName~AllocationBudgetTests` | measured bytes per phase vs budget | budgets only ever go down |
| Untrusted input | `tests/FactsPDF.Tests/RobustnessFuzzTests.cs`, `StylesheetBoundaryTests.cs`, `LayoutResourceGuardTests.cs` | `dotnet test ... --filter FullyQualifiedName~RobustnessFuzzTests` | every failure is `FactsPdfException` with an `FPDF` code | limits, cancellation |

## Gate

1. Build every project except `examples/first-use` with warnings as errors:
   `for p in $(git ls-files '*.csproj' | grep -v first-use); do dotnet build "$p" -c Release -warnaserror; done`
2. `dotnet test tests/FactsPDF.Tests -c Release` (includes the CLI tests).
3. `node scripts/parity.ts --work <scratch>` (byte parity against `origin/main`).
4. `node --test scripts/*.test.ts` and `node scripts/check-docs.ts`.
5. When `site/` data changes: the site's own tests and validation (`docs/compatibility-site.md`).

## Traps

- Several examples need CJK glyphs: without `--font` they exit 3 with `FPDF1301`/`FPDF1504`. That is the expected result, and parity compares it too; it is not a pass of the rendering.
- "Byte-identical by construction" (no `src/` change) is a claim, not a parity run; run the script.
- A PDF that `qpdf --check` accepts can still be visually wrong: look at the rasterised pages, and at the `*-oracle.html` counterpart where one exists.
- The CLI app host fails with "You must install .NET" unless `DOTNET_ROOT` points at the SDK used to build it.
- Allocation tests measure the current thread; run them in Release only.
- Hosted jobs `browser-wasm` and `cross-engine-visual` sometimes hit their time limits in install steps; read the job log before treating a cancellation as a failure.

## Last verified

2026-11-06, `main` at 4a3b275 plus `scripts/parity.ts` from #180: every feature-map row run; parity 402/402 identical.
