# F4 combined bilingual report acceptance

F4 follows merged F3 (`7f1e88f31499a492111c11527997db1c2bb2a867`). It exercises existing APIs and preserves the accepted staged release route. No engine architecture, public API, document syntax, database, publication or performance-budget work is included. A demonstrated composition bug would require a failing reproduction before a bounded fix; the implemented slice required no engine changes.

## Acceptance design

Commit reproducible 10/30/100-record bilingual quality reports and a direct public API host. Each report combines four explicit font roles, ordered/nested lists, a supplied repository-owned procedural JPEG, fixed-column tables, whole-row pagination and repeated headers, heading keeps, page counters and literal header/footer text. Use A4/36pt margins and pinned explicit source fonts for repeatable inspection. Font bytes remain private resources.

The authored introduction is 362pt; the table starts at physical y=398pt. Columns are 80/160/280pt. With 11pt text, line-height 2, 4pt cell padding and 1pt borders, ordinary rows are 32pt and every tenth row is 54pt. Headers are 32pt. Expected page counts are 2/2/6, with record groups [10,0], [11,19], [11,21,21,21,21,5]. The 10-record table stays whole; its 24pt closing heading and 44pt paragraph move together. An explicit auto-break counterfactual proves that keeping is exercised. Three furniture-disabled variants verify body preservation. All seven variants total 22 pages; the three positive reports total ten.

The HTML generator must not supply PDF expectations. Independently specify semantic lines, per-character font roles/coordinates, row/cell geometry and source-font advances. Inspect actual consumed paint operations and their order, text graphics state, source glyph outlines/subsets, JPEG bytes/placement and rendered orientation/color samples. Use a second text parser to check full extraction and word separation. Cross-entry and body-raster equality supplement these authored checks; matching same-engine images alone is insufficient.

Construct real-PDF mutations and require diagnostic-specific rejection. An unchanged pypdf rewrite must pass the same inspector first. Include missing/duplicate/reordered records, repeated header loss, collapsed second-line baselines, wrong font roles, missing markers/discs/images, changed JPEG bytes/geometry, missing or reordered paint, text transforms, occluding fills, header/footer shifts, incorrect counter and detached heading. Independent review additionally reproduced text-CTM, fill-after-text and restored-text-color false passes; RED reproductions preceded their checker fixes.

Negative report variants preserve exact existing diagnostics and atomic output through public API, managed CLI and actual NativeAOT. API probes seed caller bytes/position and cover quotas. CLI/AOT cover stdout, existing/new destinations and temporary-file absence. Recovery reproduces successful bytes. No local browser printing or security-policy changes are needed.

## Evidence and approvals

Record source identity, input/font/PDF/page-image hashes, entry parity, assertions, mutation failures and negative outcomes. Inspect every actual native page, including counterfactual/disabled variants; record agent visual inspection separately from user approval. Run complete local build/tests, independent design and checker review, normal CI and unresolved-comment gates before normal protected merge; rebuild and verify merged main.

The original registry already contains six human-approved baseline cases (ten pages). Verify their retained hashes without rewriting approval records or original inputs. Preserve the original 6 baseline / 12 negative / 9 target accounting and malformed diagnostic probes. Correct stale support descriptions and distinguish unbound legacy harness entries from implemented capabilities. F4 positive evidence does not silently promote legacy targets to verified or freeze new pages as human truth. New combination references, current-source release gates and numerical performance budgets remain separately incomplete.

Internal design review confirmed arithmetic and required consumed-paint checks, explicit line baselines, mutation rewrite controls and per-page review accounting. The executable details and commands are in [combined-report.md](../../combined-report.md).
