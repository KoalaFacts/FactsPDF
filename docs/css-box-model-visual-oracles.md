# FactsPDF: Browser Visual Oracles for the Planned CSS Box Model

**State:** Chrome reference suite implemented and verified. FactsPDF box-model rendering remains **unimplemented** at this stage. Do not interpret a green reference job as successful cross-engine rendering.

**Upstream:** CSS syntax implementation [PR #7](https://github.com/KoalaFacts/FactsPDF/pull/7), existing Chrome/FactsPDF PDF visual harness [PR #8](https://github.com/KoalaFacts/FactsPDF/pull/8), Block Box Tree architectural design [PR #5](https://github.com/KoalaFacts/FactsPDF/pull/5).
**This suite:** [PR #10](https://github.com/KoalaFacts/FactsPDF/pull/10), `test/box-model-visual-oracles`. The test PR stays unmerged pending upstream integration and review.

## Two-stage execution (do not confuse them)

1. **Today — `--mode reference`:** Render five controlled HTML/CSS templates to real **Chrome print-to-PDF**. Validate qpdf integrity, page geometry, extractable Unicode text against HTML, text inside page, exact colored paint probes, relative nested backgrounds, and first/middle/last page-fragment border behavior. Then run the real FactsPDF Linux x64 **Native AOT** CLI on the original HTML, **require FPDF1201** and prove that no output PDF was written. This is the CI default now.
2. **After box-model implementation — `--mode compare`:** Reuse exactly the same five fixtures, run both PDFs with installed explicit fonts, and require qpdf validity, identical extracted text, expected page count and independent Poppler rasters. Generate Chrome/FactsPDF page images, overlay, red heat map and a zoomed ink-detail image; report unregistered whole-page and ink-region changed-pixel metrics. A hard renderer failure or missing/extra page fails. Do **not** set a global pixel-similarity threshold without reviewed per-feature baselines and typography allowances.

The CSS declarations in the **original** HTML fixtures are limited to the planned PR #5 scope: `width`, `padding`, `border`, `background-color`, existing `font-size`, `line-height`, text `color`, paragraph margins and explicit page breaks. No image, table, Flex/Grid, positioning, `@page`, border radius, text shaping or extra JavaScript is required.

### Five fixture families

| Fixture | HTML file | Chrome pages | Key validation |
| --- | --- | ---: | --- |
| Solid background | `examples/css-visual/box-background.html` | 1 | Solid `#cee8fb` paint, width and padding |
| Four colored borders | `examples/css-visual/box-border.html` | 1 | All four different colored solid edges; background distinguishable from paper |
| Asymmetric padding | `examples/css-visual/box-padding.html` | 1 | Four-value padding; inner `#abd9e7` background bbox nested strictly within outer `#e4e5e7` bbox |
| Nested content boxes | `examples/css-visual/box-nested.html` | 1 | Real `section > div > article`; child `width:70%`; three distinct nested fill bboxes |
| Paginated box fragments | `examples/css-visual/box-fragmentation.html` | 3 | Background and side borders on every page; horizontal top edge first page only, horizontal bottom edge last page only |

## Actual independently produced browser evidence

Reference CI on Chrome **154.0.8037.97**, 120 DPI, Linux x64:
- Five of five fixtures rendered as expected; **seven pages** in total.
- All original HTML text matched the real Chrome PDF text, stayed inside page bounds, and produced page rasters.
- Each template returned the expected StrictPdf `FPDF1201` from the native CLI, and produced **no FactsPDF output PDF**; these are explicit expected unimplemented cases, not skipped "passing" visual diffs.
- For the nested geometry case, measured RGB paint bounds in pixels were: outer `#dbeefa` **[62,62,930,281]**, inner `#e4c76a` **[85,124,717,232]**, inner leaf `#ffb27d` **[111,166,691,216]**.
- For four-value padding, measured paint bounds were outer **[60,60,900,199]** and inner **[120,80,854,159]**.
- For three-page `box-decoration-break: slice` behavior, wide navy border rows (>=300 near-exact pixels per row) appeared **6** times on page 1, **0** on the middle page and **6** on the last page. Every page also contained the tested background color and side-border navy pixels.

These numbers come from **independent Chrome PDFs**. They are reference observations and are not claims that FactsPDF can already render those geometric structures.

## Browser normalization, correctness, resource and security constraints

The browser is supplied with a reference-only stylesheet for A4 paper (595.28 × 841.89 pt), 36 pt margins, body margin reset, DejaVu Sans/Droid fallback fonts and neutral heading weight/kerning. Additionally Chrome receives `print-color-adjust: exact` and its WebKit alias to require painted backgrounds in PDF. **Neither reference-only stylesheet is supplied to FactsPDF.** The report includes these precise CSS overrides and browser/system font SHA-256 hashes. Chrome's PDF point rounding can still produce a 1 px raster dimension difference; future paired comparisons use the existing white top-left origin padding rule, never image resampling or translation.

The browser, fonts, Python/Pillow and Poppler are CI-only **test tools**, not FactsPDF runtime dependencies. The native engine still must not load files or fonts implicitly; test CLI font paths are supplied explicitly. No third-party font files, generated Chrome PDFs, native binaries or NuGet packages are retained in uploaded artifacts — only public PNG reference pages, `report.json`, `summary.md`.

This suite isolates parser/layout compatibility from content/font-family/default-browser differences and respects the CSS Fragmentation `slice` default. It does not assert browser screenshot accuracy, full standards conformance or automatic visual approval of any future engine implementation.

## Rerun

On Linux with .NET 10 SDK/Native AOT compiler, Chrome, qpdf, Poppler, Python 3 with PIL and installed DejaVu/Droid fonts:

```bash
/usr/bin/python3 -m unittest discover -s scripts -p 'test_css_box_visual_cases.py' -v
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/box-visual-native
/usr/bin/python3 scripts/css_box_visual_oracle.py --mode reference --native artifacts/box-visual-native/FactsPDF.Cli --output artifacts/box-visual
```

Once code genuinely supports this HTML/CSS, switch **only** the mode to `--mode compare` and include both PDF image outputs in the CI artifacts. **Remove the reference-mode StrictPdf expected-error gate in that implementation milestone**; do not keep pretending unsupported output is success after implementing the box model.

The full test command and browser report are reproducible in `.github/workflows/css-box-visual.yml`. The first test-only commit in PR #10 intentionally failed because its oracle helper had not been implemented, then passed when the helper was added. A subsequent red/green loop added stricter color-distance and three-page slice-geometry checks.

## Future box-model acceptance

- Parser semantics: property cascade and shorthands valid under the new CSS Syntax Core; `!important` and side priorities retained.
- Layout: width and percentage relative to containing **content box**, all four padding sides, borders and text offsets, nested container structure.
- Painting: backgrounds beneath text, solid side colors, first/middle/last split fragments and no fake top/bottom edges or blank artifact-only pages.
- Source evidence: compare both generated PDFs page by page, include actual rendered image artifacts and text-difference diagnostics. Keep raw pixel metrics contextual; measure visual geometry and human-readable text. Do not claim production browser parity from only five selected fixtures.

**Standards references:** [CSS box model](https://www.w3.org/TR/CSS22/box.html) and [CSS Fragmentation Level 3 §5.4](https://www.w3.org/TR/css-break-3/#break-decoration) — default `box-decoration-break: slice` carries side edges through fragments while deferring horizontal top/bottom edges to the first and last fragments.
