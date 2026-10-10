# CSS line boxes: fallback-font baseline and explicit line-height

**Scope:** Renderer layout correction stacked on PR #8 (visual comparison), which depends on PR #7 (CSS Syntax Core). The patch changes `TextLayout` and NUnit tests; no new CSS properties, font-file distribution or third-party runtime dependency.

## Root cause

The previous `TextLayout.Wrap.Flush` computed an inline line's ascent and descent by taking the maxima across **actual glyph fonts**, including Chinese fallback fonts. It then expanded height to `max(specified line-height, ascent + descent)` and positioned the baseline with those expanded metrics.

This has two visible consequences:

1. A fallback font with a larger/asymmetric ascent could move **all** glyph baselines in an English/Chinese line even though the CSS parent had fixed `line-height`.
2. A requested `line-height:1` could silently become a much taller line if the embedded font's hhea vertical metrics summed to more than one em.

CSS Inline Layout Level 3 states that with a *non-normal* computed line-height, layout bounds derive from the **first available font**, ignoring glyphs from other fallback fonts; half-leading adjusts the effective ascent and descent to the used line-height. See [CSS Inline Layout Level 3 §5.3](https://www.w3.org/TR/css-inline-3/#layout-bounds).

## Correction

- The paragraph contributes its invisible baseline **strut**, derived from the first supplied font and computed CSS `font-size × line-height`.
- Each inline run contributes leading-adjusted ascent and descent using the same primary font at its own font-size and computed line-height. Therefore **legitimately larger inline text can expand** the line box.
- The line box takes the maximum ascent and descent contributions. Its height is their sum; fallback-font glyph metrics are not used to force a larger line height.
- Baseline is directly `pageHeight − lineTop − effectiveAscent`, with page dimensions in PDF points.
- The embedded glyph font and actual horizontal advances remain unchanged. No font subset, cmap, ToUnicode, input, CSS parser or PDF serializer behavior is replaced.

In an original synthetic font regression, fallback metrics previously moved the baseline by **3.6pt** and the following paragraph by **1.8pt**. In another test, `12pt` text with `line-height:1` was incorrectly spaced **20.4pt** apart; it now follows the requested **12pt** line box.

## Real Chrome visual verification

The same [PR #8 visual harness](https://github.com/KoalaFacts/FactsPDF/pull/8) was rerun with the new Native AOT engine on three HTML/CSS documents (four pages), same OS fonts and reference-only normalization CSS, Poppler rasterization at 120 DPI. Baseline run: [38026110182](https://github.com/KoalaFacts/FactsPDF/actions/runs/38026110182) (`0d674ad`). Fixed engine: [38039207553](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039207553) (`31f5dee`).

| Sample | Page count | Whole-page changed pixels before → after | Ink-region changed pixels before → after |
| --- | ---: | ---: | ---: |
| Latin | 1 | 0.7474% → 0.7474% | 12.6672% → 12.6672% |
| Chinese + English | 1 | **1.5121% → 0.8954%** | **27.4257% → 16.4788%** |
| Explicit pagination | 2 | 0.5159% → 0.5159% (two-page aggregate) | 13.7488% → 13.7488% (page mean) |

Pixel differences count locations where the maximum RGB-channel absolute difference exceeds 16/255. These numbers are specific to four carefully controlled pages, not a browser-compliance percentage. White margins dilute whole-page metrics; ink-region metrics are more informative, but sensitive to glyph rasterization. The comparison **does not register, resample or warp** page images; its single-pixel A4 quantization difference is padded white at the outer page boundary.

Overlay inspection of identical page coordinates shows most bilingual lines improved from an approximately **2–3 px** vertical drift to **about 1 px**, while Latin and pagination fixtures show no changed rendered bytes. The remaining 1px residual is not grounds for introducing a hardcoded, DPI-specific offset; text outlines, Chrome font metrics and rasterization differ by engine.

The workflow checks page counts, searchable text equivalence, text-in-page bounds, and nonblank page rasters before reporting pixel diagnostics. Public CI artifacts contain only PNGs, JSON and Markdown, not PDF font files or executables.

## Tests and status

Test-first new suite `CssLineBoxMetricsTests` captured a red baseline:
[CI 38039102383](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039102383)
found **314 passed / 4 failed** (including one overstated assertion for larger inline text, corrected before the implementation). The layout fix at `31f5dee` passed
[CI 38039207591](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039207591)
with **318/318** NUnit tests on Linux; Windows, macOS and real Linux x64 Native AOT also succeeded. [Stylesheet 38039207565](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039207565), [font subsetting 38039207589](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039207589) and [CSS syntax 38039207609](https://github.com/KoalaFacts/FactsPDF/actions/runs/38039207609) workflows all passed for the same head.

**Boundaries:** This is not a complete CSS inline formatting engine. Full vertical-align, inline boxes, bidirectional shaping, fallback glyph ink overflow, text-decoration and precision browser font metrics remain later work. Do not claim pixel-perfect Chromium parity, a global throughput change, or production readiness from this fixture. No package has been published and nothing has been merged.
