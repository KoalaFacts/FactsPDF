# Acceptance support matrix — evidence, not marketing

Engine comparison baseline: M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`. Source SHA and every actual outcome come from each run's manifest/environment/report.

| Cases | Contract | Completion meaning |
|---|---|---|
| B00 | ASCII text control via CLI/API | Independent text/structure/bounds; human freeze still required |
| B01 | Bilingual brief, headings and information panel | Same plus every native/Chrome page for human review |
| B02-10/30/100 | Ordered synthetic records as paragraphs | Not table support; every ID and sentence checked |
| B03 | Three explicit report sections inside a continued panel | Real existing block fragmentation; observed pages reported |
| N-wide/N-zero | Unbreakable/zero-width text | FPDF1302 before replacing existing bytes, CLI/API |
| N-pages/input/elements/depth | Existing resource limits | Exact FPDF1303/1001/1002/1003, public API probe only |
| N-css-characters/declarations | Existing CSS work budgets | FPDF1205, public API probe only |
| N-display/output | Command/byte budgets | FPDF1401, public API probe only |
| N-utf16-high/low | Runtime malformed UTF-16 | FPDF1304 without replacement or caller-output writes |
| T-bold | C1 explicit normal/bold/italic/bold-italic; separate actual font and cross-entry evidence | Legacy corpus supplies only normal fonts and now checks missing-role FPDF1507; use C1 evidence for positive support |
| T-list | Positive decimal/disc two-level smoke, CLI/API bytes must match | Candidate only; full wrapping/pagination/geometry evidence is in C2 |
| T-table-10/30/100 | Isolated unsupported HTML probes | Expected rejection is NOT implementation/verification |
| T-jpeg/T-page-furniture/T-heading-keep/T-complete-report | Unimplemented product requirements | blocked-design until real APIs/specs and independent assertions exist |

A green candidate-evidence job means capture and its independent checks completed, **not** that frozen baselines, all nine target cases or the Developer Preview are approved. Reports keep target totals, missing approval, and unapproved performance budgets visible. Missing tools or wrong target diagnostics are errors, not skipped support.

T-bold's [explicit font style implementation and evidence](../font-styles.md) preserves ordered per-character fallback and adds four independent roles. [C2 lists](../lists.md) have their own real report and cross-entry verification; the legacy smoke does not import full C2 evidence or user reference approval automatically. The original nine target cases remain the denominator. Final exact-head CI/review gates must be recorded for each stage; neither expected rejection nor one successful smoke completes all product targets.
