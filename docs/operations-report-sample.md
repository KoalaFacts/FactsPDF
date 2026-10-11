# Readable operations report sample

`examples/operations-report.html` is a separate synthetic customer-reading sample.
It does not replace B00–B03, change their gates, freeze references, or implement
any future report capability. Base: PR20 `df26256c22857523fb6a4c2978f9e586f8aabadf`;
main at start: `818da048604d6123ccb928c1a5ef3beba2f14cf6`.

The report contains an overview, three actions with responsible roles and dates,
and 24 distinct records grouped by subject (18 complete, six open). Text includes
short and multiline Chinese/English paragraphs. A small summary panel and
section spacing replace the diagnostic fixture's almost empty spanning panels.
The four subject sections use explicit heading page breaks. Page count is an
observed output, not a requirement or a reason to remove content. Report IDs,
record ranges and the continued heading are authored content, not automatic
page furniture. The sample uses no tables, lists, true bold or unsupported CSS.

## Reproduce

Use the existing documented .NET 10 SDK, Native AOT toolchain, verification
utilities, and licensed DejaVu Sans / Droid Sans Fallback font inputs. No new
runtime dependency or package is needed. Do not commit standalone fonts.

From a clean committed checkout, build both existing entrypoints:

```sh
python3 scripts/acceptance_build.py --cli-output artifacts/operations-cli --host-output artifacts/operations-api
artifacts/operations-cli/FactsPDF.Cli examples/operations-report.html artifacts/operations-native.pdf --subset-fonts --font /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf --font /usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf
artifacts/operations-api/FactsPDF.Acceptance render examples/operations-report.html artifacts/operations-api.pdf --subset-fonts --font /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf --font /usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf
python3 scripts/verify_operations_report.py --native artifacts/operations-native.pdf --api artifacts/operations-api.pdf --output artifacts/operations-inspection
```

The verifier checks all visible source text (ignoring layout whitespace), every
record-ID occurrence in order, existing strict 36pt content bounds, qpdf and
CLI/API byte parity, and renders every page at 120 DPI. It emits a browser-only
normalized input with the existing font/page normalization. In an environment
that permits Chrome PDF printing, print that input with backgrounds enabled and
browser headers/footers disabled, then rerun with `--chrome` and a fresh output
directory. Keep the Chrome version, fonts, wrapper hash and PDF with the evidence.
The wrapper is never supplied to FactsPDF. Do not substitute screenshots or
old PDFs for a real Chrome print reference.

Review every raster for readable hierarchy, adequate panel/title spacing,
paragraph continuity, group context, missing glyphs and overlap. Green machine
checks do not approve the reading experience. Any later text change requires
rerendering and rereview: the template does not promise automatic heading keep,
widow/orphan handling, repeated headers, footers or page numbers. This fixed
sample does not establish arbitrary-length report pagination support.

## Environment limitation

The current managed Chromium has `PrintingEnabled:false`. No policy is changed
or bypassed. A missing Chrome PDF remains an explicit incomplete comparison;
the Native PDF and its verified evidence can still be reviewed independently.
