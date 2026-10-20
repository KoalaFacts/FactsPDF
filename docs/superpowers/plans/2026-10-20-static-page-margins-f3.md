# F3 execution and native verification record

Baseline: verified F2 main `0b149f67761a26b567f09ab9dd8551410a16652a`, no open PR at start. No checkout AGENTS.md or local .agents/skills files were found. The user's continuation explicitly authorizes this bounded static header/footer increment, independent reviews, normal CI/merge and main verification. Design: [F3 static page margins](../specs/2026-10-20-static-page-margins-f3-design.md).

1. Baseline full Release suite: 1004 passed. Independent design review accepted nullable literal strings and fixed single-line placement in existing margins after clarifying ASCII-space normalization, borrowed-line handling, diagnostic order, CLI next-token/duplicate behavior and raw input bounds.
2. RED: 39 tests failed for absent API properties/CLI options. Reflection let the initial tests compile against F2; final tests use typed properties. Minimal compositor extension then passed all 39 plus 33 F2 tests. Added regression coverage brings the final full suite to 1050 tests, zero skipped.
3. Independent production review found no actionable defects and independently passed 79 margin/page-number tests. It checked typography, geometry, budgets, cancellation, atomic failure, literal CLI parsing and F2 compatibility.
4. Built a direct public-API probe and actual Linux Native AOT executable. Native corpus: 10 cases / 24 pages, including empty content, independent header/footer, literal markup, explicit/trailing breaks, F1 keeps, E2 tables, bilingual fonts, full embedding, ten-page digit transition and near-width text. All API/CLI/AOT PDF bytes match. Ten disabled cases match the retained verified original F2 executable, including number-only behavior.
5. The initial bilingual source triggered the intended conservative overlap error: Chinese header global bounds ended at 32.28pt from page top while the first body Chinese run's declared top was 30.458pt. Existing natural page-start layout discards the attempted top margin; explicit heading line-height 3 shifts its baseline by 6pt and produces the successful two-page example. The original source is retained as an atomic-failure case across all three entries. No production overlap check was relaxed.
6. Reviewed every one of the 24 pages in six contact sheets. No clipping, overlap or miscentering. Independent inspection checks page count/text/geometry, source-font-derived vertical baselines, font size/color, unchanged body painting and identical body-region raster bytes. qpdf passes. Eight negative cases preserve existing destinations through API/CLI/AOT, with no temporary-file leak.
7. Independent inspector review found two gaps: a 3pt margin-internal displacement and missing table rectangles were previously accepted. Fixed the inspector using source-font expected baselines and non-text paint/raster comparisons. Reviewer independently confirmed both original counterexamples now fail and all 24 pages pass. Eight mutations cover changed text, large/small header/footer displacement, font size and removed table painting. Production remained unchanged.
8. Final Release build: zero warnings/errors. Node subprocess boundary tests: 12 passed. One attempted npm invocation used a nonexistent node_probe directory; the actual repository test is `node --test scripts/test_node_cli_process.mjs`. Normal CI includes the new inspector. CI success, resolved comments, normal protected merge and clean-main rerun remain delivery gates, not claims made by this pre-merge record.

Reproduce after building the CLI, `probes/page-margins` and Native AOT, using an explicit font manifest in the existing font-style probe format:

```sh
python scripts/verify_page_margins.py --manifest FONT_MANIFEST.json --output artifacts/f3/output \
  --dotnet dotnet --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --api-dll probes/page-margins/bin/Release/net10.0/FactsPDF.PageMargins.dll \
  --native-cli artifacts/f3/native/FactsPDF.Cli
```

Optional `--baseline-cli ORIGINAL_F2_EXECUTABLE` compares disabled output against F2. The inspector uses the existing qpdf, Poppler, pypdf, Pillow and fontTools verification dependencies. Fonts stay private; evidence contains generated documents, previews, hashes and logs.

No local Chrome printing, security-setting changes, Library retry, database changes, combined-report acceptance, package publication or public release. These are functional implementation checks and do not replace pending user approval of general report reference pages.
