# Readable operations report sample

`examples/operations-report.html` is a separate synthetic customer-reading sample.
It does not replace B00–B03, change their gates, freeze references, or implement
any future report capability. Base: PR20 `df26256c22857523fb6a4c2978f9e586f8aabadf`;
main at start: `818da048604d6123ccb928c1a5ef3beba2f14cf6`.

PR20 was subsequently merged by its owner as `e35722bf299adae3a994593f1375ee43ec53ad12`.
This sample branch is based on that main commit. Its diff contains only the
sample, this documentation, and the separate verifier/regression tests; the
approved technical references are unchanged.

The report contains an overview, three actions with responsible roles and dates,
and 24 distinct records grouped by subject (18 complete, six open). Text includes
short and multiline Chinese/English paragraphs. A small summary panel and
section spacing replace the diagnostic fixture's almost empty spanning panels.
Explicit heading page breaks precede Actions, Activity register, and Register
continued; the four record subject groups are arranged within the register.
Page count is an
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

## Observed reading review and provenance

### CI coverage

The document-acceptance workflow explicitly runs `test_operations_report.py`
in addition to the original acceptance tests. After its existing clean-source
Native AOT build, `capture_operations_report.py` reuses the same CLI/API binaries,
explicit fonts, build-receipt validation and Chrome capture helper to render this
exact input. Both PDFs undergo full-text, ordered-ID, qpdf and strict content-bound
checks, and every page is rasterized. The native and browser page counts are
reported as observations, not forced to four or to equality.

The separate `operations-report-review` Actions artifact contains Native/Chrome
PDFs, all page PNGs, source and normalized Chrome input, extracted text/bounds,
inspection results, executable/font hashes and exact compiled source identity.
Only its explicit public directory is uploaded; browser profiles, binaries and
standalone fonts stay outside it. Failed captures keep an incomplete summary;
they are not approved candidates. The original corpus and its review artifact
remain separate. Green workflows on the earlier PR head did not cover this
sample; only a run containing these new steps establishes CI coverage.

### Local evidence

All four actual Native pages were visually reviewed independently. Review found
and corrected missing English action dates/completion conditions, an ambiguous
Chinese completion statement, and tight spacing below the summary panel. A
separate verifier review caught removal of English word separators; two failing
regression cases reproduced it before correction, and all three text tests now
pass. The corrected PDF also passes the stricter real-text comparison.

The unchanged reviewed HTML SHA-256 is
`f935ff2925167dd00125d0d3e45609f44d7da9fc7ed301ed05b0522d9b745ad4`;
the reviewed Native PDF SHA-256 is
`1cb802bc8a758327abd43b5851e29324de52d3122576190fc90a5adc0313521f`
(97,864 bytes, four A4 pages). The first exact-source verified build of this
final content/verifier was `a300667220f744847f3bac97bca028e00b721bec`, before
rebasing this independent branch on the merged main. These are observations,
not approved references or a promise of browser pixel equality.

The report text is newly authored AI-assisted synthetic content for this task,
not copied customer or third-party material. The verifier reuses existing
repository normalization and inspection helpers. Fonts are caller-supplied
verification inputs, not bundled assets. Repository inbound-rights and release
gates remain applicable; a draft PR does not satisfy them.

The local image has no `/usr/bin/time`. An authorized attempt to install the
official `time` package failed because the unprivileged account cannot acquire
the system dpkg lock. Five existing peak/cleanup acceptance tests remain blocked
by that missing prerequisite. No assertions, thresholds, replacement supervisor,
or test skips were introduced. Use the real system GNU time package in a properly
provisioned environment before treating the full acceptance suite as green.
Library upload failed before preparation because its tool-discovery request
could not reach the service; no Library file IDs were created. Neither that
upload nor policy-blocked Chrome printing is claimed as delivered evidence.
