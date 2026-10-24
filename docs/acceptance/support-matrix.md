# Acceptance support matrix — evidence, not marketing

Engine comparison baseline: M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`. Source SHA and every actual outcome come from each run's manifest/environment/report.

| Cases | Contract | Completion meaning |
|---|---|---|
| B00 | ASCII text control via CLI/API | Independent text/structure/bounds; original native reference approved |
| B01 | Bilingual brief, headings and information panel | Original native reference approved; new captures require their own review |
| B02-10/30/100 | Ordered synthetic records as paragraphs | Not table support; every ID and sentence checked |
| B03 | Three explicit report sections inside a continued panel | Real existing block fragmentation; observed pages reported |
| N-wide/N-zero | Unbreakable/zero-width text | FPDF1302 before replacing existing bytes, CLI/API |
| N-pages/input/elements/depth | Existing resource limits | Exact FPDF1303/1001/1002/1003, public API probe only |
| N-css-characters/declarations | Existing CSS work budgets | FPDF1205, public API probe only |
| N-display/output | Command/byte budgets | FPDF1401, public API probe only |
| N-utf16-high/low | Runtime malformed UTF-16 | FPDF1304 without replacement or caller-output writes |
| T-bold | C1 explicit normal/bold/italic/bold-italic; separate actual font and cross-entry evidence | Legacy corpus supplies only normal fonts and now checks missing-role FPDF1507; use C1 evidence for positive support |
| T-list | Positive decimal/disc two-level smoke, CLI/API bytes must match | Candidate only; full wrapping/pagination/geometry evidence is in C2 |
| T-table-10/30/100 | Legacy malformed table probes (missing explicit columns/sections) | FPDF1101 remains expected; positive E1/E2/F4 evidence is separate |
| T-jpeg | Defined LoadImage resource API; legacy probe has no bound image and checks FPDF1604 | Full positive D1 evidence is separate; rejection is not positive credit |
| T-page-furniture/T-heading-keep/T-complete-report | Original harness placeholders have no bound executable inputs | Legacy `blocked-design` describes these unbound entries, not current engine support; use separate F1–F4 evidence below |

The [approval registry](reference-reviews.json) records **six human-approved original native baselines (ten pages)** with durable PDF/text/image hashes and the original reviewed source. A green new candidate-evidence job means capture and independent checks completed; it does not transfer those approvals to changed outputs, approve all nine targets, or complete the Developer Preview. Reports keep target totals, missing approval, and unapproved performance budgets visible. Missing tools or wrong target diagnostics are errors, not skipped support.

T-bold's [explicit font style implementation and evidence](../font-styles.md) preserves ordered per-character fallback and adds four independent roles. [C2 lists](../lists.md) have their own real report and cross-entry verification; the legacy smoke does not import full C2 evidence or user reference approval automatically. The original nine target cases remain the denominator. Final exact-head CI/review gates must be recorded for each stage; neither expected rejection nor one successful smoke completes all product targets.

[D1 explicit JPEG images](../images.md) have their own real image report and per-entry validation. The legacy harness does not import D1 evidence or user page approval automatically.

## Separate positive evidence

These implemented paths have independent executable evidence. This table neither changes the original nine-target denominator nor promotes malformed/unbound legacy entries to `verified`.

| Capability | Executable positive evidence | Approval boundary |
|---|---|---|
| Fixed columns, whole rows, repeated headers | [E1/E2 tables](../tables.md), `scripts/verify_table_pagination.py` | Separate from malformed T-table rejection |
| Static PNG color and transparency | [PNG image resources](../images.md), `scripts/verify_png.py` | Scoped non-interlaced formats; separate engineering evidence |
| Image-only table cells and repeated image headers | [Branded table composition](../tables.md#image-only-cells-and-branded-layouts), `scripts/verify_table_images.py` | Literal geometry/pixels, reuse and actual mutated-PDF rejection; new pages pending reference review |
| Column spans and ordered paragraph/image cells | [Rich and spanning cells](../tables.md#rich-and-spanning-cells), `scripts/verify_table_rich_cells.py` | Authored geometry, margin and border witnesses; rows still indivisible; new pages pending reference review |
| Heading kept with following paragraph | [F1 heading keep](../heading-pagination.md), `scripts/verify_heading_keep.py` | Existing opt-in paragraph contract |
| Page counters | [F2 page numbers](../page-numbers.md), `scripts/verify_page_numbers.py` | Existing body geometry remains unchanged |
| Static header/footer and counter composition | [F3 page margins](../page-margins.md), `scripts/verify_page_margins.py` | Explicit literal text; existing margins |
| Combined bilingual 10/30/100-record reports | [F4 executable reports and independent checks](../combined-report.md) | Engineering acceptance; new pages pending user reference review |

F4 combines all four font roles, nested lists, explicit JPEGs, fixed-column tables, whole-row pagination/repeated headers, heading keeps, page numbers and static page text. Its positive evidence is additional to the original 6 baseline / 12 negative / 9 target cases. New combination PDFs are not automatically frozen as human truth. Current-head user reference approval, remaining release gates, fixed hardware and numerical performance budgets stay separate and incomplete.
