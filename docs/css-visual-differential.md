# Chrome PDF vs FactsPDF PDF: visual differential testing

**Scope:** A repeatable diagnostic for the currently supported CSS subset; not a full HTML/CSS browser compatibility test. Runs in CI without adding a browser or imaging dependency to the FactsPDF production assembly. See [PR #8](https://github.com/KoalaFacts/FactsPDF/pull/8), stacked on CSS syntax [PR #7](https://github.com/KoalaFacts/FactsPDF/pull/7).

## What gets compared

We generate two real PDFs **from each original fixture**:

1. FactsPDF: compiled Linux x64 **Native AOT** CLI, explicitly loading two complete OS-installed TrueType fonts (DejaVu Sans and Droid Sans Fallback), with the existing in-engine `--subset-fonts`.
2. Google Chrome: headless **print-to-PDF**, reading that same HTML/CSS fixture locally, with a second, reference-only CSS block injected just before `</head>`. This block establishes A4 paper/margins, resets browser body margins and heading weight, selects the two installed fonts, and disables browser kerning. The injected declarations are **not** supplied to FactsPDF because some of them are not yet supported by its CSS renderer.

Both PDFs are checked with **qpdf**, have their Unicode extracted independently with **Poppler**, and have text-word bounds inspected. Each PDF is rasterized separately by the **same Poppler pdftoppm version** at **120 DPI**. No screenshot-versus-PDF comparison, font pretreatment or third-party PDF generator is part of the engine's runtime.

Three public fixtures currently cover:

| Fixture | Pages | Supported features |
| --- | --- | --- |
| `latin-baseline.html` | 1 | Latin heading/paragraphs, class selector, font size, color, line height, explicit paragraph margins |
| `bilingual-baseline.html` | 1 | Mixed Chinese/English text, fallback fonts, inline span colors, actual Unicode PDF text |
| `pagination-baseline.html` | 2 | Two explicit PDF pages, `break-before:page`, heading and paragraph layout |

The fixture has an explicit expected page count. Different extracted text, unexpected page count, invalid qpdf output, missing extractable text, text outside the physical page, or blank page rasters **fail CI**. No arbitrary changed-pixel threshold can certify full browser visual compatibility.

## Why raw dimensions are not identical

In an actual Chrome 154 run, A4 was written as **594.96 × 841.92 pt**, whereas FactsPDF's configured page was **595.28 × 841.89 pt**. At 120 DPI, this quantization results in Chrome raster width 992 px versus FactsPDF width 993 px. For an **at most one-pixel** raster canvas discrepancy, the comparison adds white at the right/bottom to align the shared original top-left origin.

No image is translated, rotated, warped, stretched, scaled or automatically registered **for scoring**. Larger raster dimension discrepancies fail. Every page records both original raster dimensions, common canvas dimensions and the padding operation. The preview-only image thumbnails and ink-detail crops are *not* used to calculate scores.

## Reported metrics

- **Whole-page changed pixels:** fraction of all pixels with **any RGB channel absolute delta greater than 16 / 255**. Includes large white margins.
- **Ink-region changed pixels:** the same threshold, restricted to the union of bounding boxes of visible non-white ink in the Chrome and FactsPDF page. A non-white ink mask uses grayscale delta from white >32. This is **not** OCR or a full character-shape match.
- **RGB MAE:** mean absolute error per RGB channel, on 0–255 scale; reported both for the whole page and ink union.
- **Ink bounds:** Chrome and FactsPDF occupied-pixel rectangles, in original (top-left anchored) raster coordinates.
- **Per-page images:** `chrome`, `factspdf`, 50/50 `overlay`, red `difference` map, full-page side-by-side `comparison`, and enlarged `detail` crop showing only occupied text regions.

An anti-alias or font rasterizer difference may cause many ink-region pixels to differ while text and geometry remain correct. Conversely, a full-page fraction may look extremely small despite incorrect text. **Neither fraction is a standards score, recognition accuracy score or a substitute for manual inspection.**

## Reproduction and evidence

On a Linux x64 checkout with .NET 10, Native AOT compiler, Google Chrome, Poppler, qpdf, system Python with Pillow, and the two appropriately licensed installed font packages:

```sh
sudo apt-get update -qq
sudo apt-get install -y clang zlib1g-dev poppler-utils qpdf fonts-dejavu-core fonts-droid-fallback python3-pil

/usr/bin/python3 -m unittest discover -s scripts -p 'test_css_visual_metrics.py' -v

dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj \
    -c Release -r linux-x64 -p:PublishAot=true -o artifacts/css-visual-native

/usr/bin/python3 scripts/css_visual_differential.py \
    artifacts/css-visual-native/FactsPDF.Cli artifacts/css-visual
```

The GitHub Actions workflow `.github/workflows/css-visual-differential.yml` also adds a Markdown results table to the run summary. Uploaded files contain **only public synthetic PNG page comparisons, JSON and a Markdown summary**; Chrome/native generated PDFs, native executables, temporary browser profiles and standalone font files are not uploaded.

The report records the real Chrome version, exact fixture SHA-256 hashes, installed font SHA-256 hashes, source head SHA, checkout SHA, reference-only CSS contents and its hash, real PDF geometry and all per-page metrics. GitHub PR checks show the real AOT build and full original 314+ NUnit/Unicode/subsetting regression matrix in neighboring workflows.

## Interpretation and future expansion

This first milestone deliberately measures the current text/CSS subset. It does **not** include padding, borders, backgrounds, tables, images, full HTML5 layout, CSS `@page` in FactsPDF, default browser font selection or complete CSS shape fidelity. Cross-engine rendering parity requires explicit normalization; without it a browser may choose an unrelated font or different default margins.

Next, grow this suite alongside the Block Box Tree / box-model design: colored backgrounds, four-side padding/borders, nested blocks, real multi-page fragmentation and more complex document layouts. Once a representative, pinned browser-version corpus and independent human review establish justified tolerances, introduce per-fixture regression thresholds or reviewed golden image baselines. Do **not** silently equate a white-page-heavy low changed-pixel fraction with Chrome compatibility.

This visual test is an **additional QA layer**. It does not replace the independent GitHub Codex review, CodeQL default-scanning review, CSS Syntax/WPT-based tests, cross-platform builds, or font trust/license policy. No package is published or PR merged by this workflow.
