---
name: verify-factspdf
description: How to exercise FactsPDF for real (CLI, Pdf.Write() SDK, Native AOT, byte parity against main, rasterised pages) and what evidence to capture before claiming a change works.
---

# Verify FactsPDF

Use with `.claude/skills/verification-first/SKILL.md`. The real boundary is the
CLI (`src/FactsPDF.Cli`) and the `Pdf.Write()` SDK (`src/FactsPDF/Pdf.cs`)
rendering `examples/*.html` into PDFs. The output must be byte-identical to
`main` unless the PR explains the difference. Rasterise the changed pages and
look at them.

## Setup

```bash
# .NET 10 SDK (global.json pins 10.0.100 with latestFeature roll-forward)
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
# Fonts and tools used by CI (ci.yml installs the same packages)
sudo apt-get install -y fonts-dejavu-core fonts-droid-fallback poppler-utils qpdf clang zlib1g-dev
dotnet build src/FactsPDF.Cli -c Release
CLI="dotnet src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll"
D=/usr/share/fonts/truetype/dejavu
Z=/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf   # covers the Chinese examples
FONTS="--font $D/DejaVuSans.ttf --font $Z --font-bold $D/DejaVuSans-Bold.ttf --font-bold $Z \
  --font-italic $D/DejaVuSans-Oblique.ttf --font-italic $Z \
  --font-bold-italic $D/DejaVuSans-BoldOblique.ttf --font-bold-italic $Z"
python3 scripts/png_fixtures.py --output /tmp/png   # deterministic PNG resources (brand-mark.png, rgba.png, ...)
```

Render: `$CLI examples/<name>.html out.pdf --overwrite $FONTS`.
Look at it: `pdftoppm -r 50 -png -f 1 -l 1 out.pdf page`, then open the PNG.
Check structure: `qpdf --check out.pdf`.
`$CLI --help` lists every option.

## Feature map

The areas follow the categories in `site/data/compatibility.json`.

| Feature | Code entry points | How to exercise it for real | Evidence to capture | Neighbours at risk |
| --- | --- | --- | --- | --- |
| HTML elements (headings, paragraphs, lists, blockquotes, tables, images) | `HtmlDocumentReader.cs`, `DocumentModel.cs`, `BlockLayout.cs`, `ListLayout.cs`, `Tables/` | `$CLI examples/report-blockquotes.html` / `lists-report.html` / `tables-report.html` with `$FONTS` | "Wrote N page(s)" line, rasterised page 1, parity vs main | pagination, text wrapping |
| HTML attributes and parsing (malformed input, `hidden`, entities) | `HtmlTokens.cs`, `HtmlDocumentReader.cs` | `echo '<p>bad <b' \| $CLI - out.pdf` → `FPDF1101: Unterminated start tag.` with `Location: line 1, column 8.`, exit 3; `examples/report-display-visibility.html` | exact FPDF code, location line, exit code | error catalog tests, fuzz tests |
| CSS properties (box model, borders, colours, text decoration) | `Css/`, `InlineCss.cs`, `PdfPaintSerializer.cs` | `examples/stylesheet-document.html`, `report-rounded-panels.html`, `report-hsl-colours.html`; compare with the `*-oracle.html` pair where one exists | rasterised page, parity vs main | `BoxStyle` sharing, allocation budgets |
| CSS values (lengths, `em`/`rem`, percentages, colour syntax) | `Css/`, `CssSyntax/` | `examples/report-rem-box-lengths.html`, `report-percentage-panel-margins.html` | rasterised page, parity | layout geometry |
| Selectors and syntax (`<style>` rules, attribute selectors, cascade) | `CssSyntax/CssSyntaxTokenizer.cs`, `CssSyntaxParser.cs`, `Css/CssSyntaxAdapter.cs` | `examples/report-attribute-operators.html` (2 pages), `stylesheet-inline-oracle.html` vs `stylesheet-document.html` | page count, parity; `CssSyntax` tests | CSS allocation budgets |
| Text, language and fonts (TrueType, fallback chains, subsetting, CJK wrapping) | `Fonts/`, `TextLayout.cs`, `TextLines.cs`, `UnicodePdfSerializer.cs` | `examples/unicode-document.html $FONTS --subset-fonts` (2 pages; Chinese and Latin on one line); without `$Z`: `FPDF1504: No supplied font contains U+4E2D.` | rasterised page showing CJK glyphs, error code without the font | subset parity, wrapping |
| Integration and platform (SDK, CLI, Native AOT, browser-wasm) | `Pdf.cs` (`Pdf.Write()`, six outputs), `src/FactsPDF.Cli`, `probes/browser-wasm` | SDK: `dotnet test tests/FactsPDF.Tests -c Release --filter "FullyQualifiedName~CliTests\|FullyQualifiedName~ConversionTests\|FullyQualifiedName~OutputCancellation"`; AOT: `dotnet publish src/FactsPDF.Cli -c Release -r linux-x64 -p:PublishAot=true -o /tmp/aot` then run `/tmp/aot/FactsPDF.Cli` on an example and compare bytes with the managed CLI; wasm: the `browser-wasm` CI job | test counts; AOT vs managed bytes equal | trimming/AOT warnings, cancellation |
| Pagination and PDF (page size, margins, page numbers, header/footer, page furniture, metadata, links, images) | `PageFurniture.cs`, `PageFurnitureLayout.cs`, `PageMarginLayout.cs`, `PdfSerializer.cs`, `PdfBuffer.cs`, `Images/` | `examples/png-brand-report.html $FONTS --image logo=/tmp/png/brand-mark.png --page-size letter --landscape --margin 20mm`; `examples/operations-report.html --page-furniture examples/business-page-furniture.json --page-width 148mm --page-height 210mm $FONTS`; `examples/report-section-page-breaks.html` (3 pages) | page count, rasterised page, `qpdf --check`, parity | serializer buffers, allocation budgets |

## Gate

1. Build every csproj except `examples/first-use` (it needs a local feed):
   `for p in $(git ls-files '*.csproj' | grep -v examples/first-use); do dotnet build "$p" -c Release -warnaserror || exit 1; done`
2. `dotnet test tests/FactsPDF.Tests -c Release` (the CLI tests are in this project).
3. Byte parity against `main`: build the CLI from a `git worktree` of `origin/main` and from the branch. Render every `examples/*.html` with three option sets and compare exit codes and bytes:
   - defaults;
   - DejaVu with `--subset-fonts --page-numbers --header-text H --title T`, PNG images from `scripts/png_fixtures.py`, and `--page-size letter --landscape --margin 20mm`;
   - `--page-furniture examples/business-page-furniture.json --page-width 148mm --page-height 210mm` with a fully embedded font.
   If `git diff origin/main -- src` is empty, parity holds by construction. Say so.
4. Docs: `node scripts/check-docs.ts` and `node --test scripts/check-docs.test.ts`.
5. When `site/` data changes, run the site's own tests (see `docs/compatibility-site.md`).

## Traps

- Many examples exit 3 without the right fonts or images: `FPDF1301`/`FPDF1504` (no font covers a CJK character), `FPDF1507` (no bold or italic chain), `FPDF1604` (image resource not supplied). Matching exit codes on both sides is parity, but it proves nothing about rendering. Use `$FONTS` and `--image` so that the examples actually render.
- `Wrote N page(s)` proves only that rendering finished. Rasterise the page and look at it.
- A mutation that drops a `CancellationToken` trips IDE0060 in a normal build. Mutation checks need `-p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false`.
- Timing-based cancellation tests report Inconclusive (counted as skipped) where a full render takes under 100 ms. Watch the skip count.
- Allocation budgets (`tests/FactsPDF.Tests/AllocationBudgetTests.cs`) only go down. A green run after raising one is not a pass.
- CI may stay queued for many minutes. A queued check is not a pass.

## Last verified

2026-11-06, `origin/main` 4a3b275. Every feature-map row above was run once on that commit. For Native AOT, the `unicode-document.html` output was byte-equal to the managed CLI's (29,268 bytes).
