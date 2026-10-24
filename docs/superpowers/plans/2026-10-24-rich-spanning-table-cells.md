# Rich and spanning table cells implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the two tasks and their independent reviews.

Base: merged #54 main `c97b6bd3d797734a2a4e873ca301adf57caba092`.
Spec: `docs/superpowers/specs/2026-10-24-rich-spanning-table-cells-design.md`.

## Start/finish gates

Do not implement until #54 is independently reviewed, exact-head CI green, merged, and main verified. The three explicit spec decisions have been adopted and recorded. Start exactly one implementation PR. This PR retains oversized-row rejection; PR B handles splitting only after this PR's merge/main verification.

Two ownership tasks only: one core implementer owns src/tests; root owns independent PDF evidence, example, docs and CI delivery. No overlapping edits. Test-first, task review and independent final review are required. Do not opportunistically change generic blocks, font layout, serializers or pagination fit.

## Task 1 — core implementation and focused tests

Owner: one core implementer. Files: src/FactsPDF/Tables/TableModel.cs, TableMarkup.cs, TableLayout.cs; HtmlDocumentReader.cs, BlockTreeTraversal.cs, Css/CssStylesheets.cs; necessary source documentation only; focused tests under tests/FactsPDF.Tests. ImageLayout/BlockLayout/TextLayout/TablePagination should normally need no behavioral changes. Explain any change there before expanding scope.

### Interfaces

- TableCell: cell Style/BoxStyle/SourceOffset/ColumnSpan and ordered TableCellText(Paragraph) or TableCellImage(ImageNode) items.
- TableMarkup: occupied-track count and validated span on cell start; parent-sensitive p/inline/img rules.
- HtmlDocumentReader: cell-local paragraph capture/flush; p CSS ancestry without outside BlockNode attachment; colspan allowed/parsed only on td/th.
- CssStylesheets.Compute Offer: detect p with td/th ancestor; reject box/break properties before comparing winners.
- TableLayout: per-cell summed track width, shared image resolver/text wrapper, pending-margin algorithm from spec; unchanged measured row command ranges and fit/placement interfaces.
- Traversal: ordered paragraph projection, images skipped, empty text requiring no fonts.

### Ordered work

- [ ] Before production edits, retain actual baseline bytes/provenance for existing short text table, long repeated-text header, short image-only table, repeated-image header and standalone image. Root may supply independent captured artifacts; coordinate fixed inputs/options so comparison is exact.
- [ ] Add failing tests for colspan grammar/coverage and explicit parent rules, then rich content capture/projection, then CSS applicability before winners. Verify failures against the merged #54 base are the expected unsupported-feature failures.
- [ ] Add literal geometry assertions from spec cases 1–4, including 45pt paragraph row and 78.5pt paragraph/image/paragraph row. Cover source-order before/after images, right/center alignment, image resources, spanned percentages and th normal-weight fixtures.
- [ ] Add preserved-error tests: rowspan, nested p/table/list, img inside p/inline, cell p box/break properties, unbreakable text, insufficient width, oversized rich row/header. Expect FPDF1101/1103/1201/1302 as specified; assert useful source offsets and destination preservation/recovery.
- [ ] Implement normalized content and parser-local ownership first. Preserve legacy cell paragraph style instead of adopting the first span's style. No general block traversal inside cells.
- [ ] Implement span widths and ordered measure with pending margins. Check margins/heights before retaining subsequent output. Preserve existing command order and full-height cell-before-image-decoration paint order.
- [ ] Test exact display-command success/failure, repeated rich headers and cancellation. Include whitespace-only p in bold th with no bold font, image-only cells with no font demand, and br-only geometry with its existing font behavior.
- [ ] Run focused table/image/CSS tests, compare saved old bytes, then full Release solution once. Broaden tests only for a concrete risk/gate. Provide results and self-review to root; commit only task-owned files when authorized by root's execution workflow.

## Task 2 — independent acceptance, example, docs and CI

Owner: root, independent of core implementer. Files: new scripts/verify_table_rich_cells.py, examples/business-table-rich-cells.html; minimal integration into scripts/wasm_probe and current workflow; README.md, docs/tables.md, docs/images.md, docs/evaluation.md, docs/development.md, docs/acceptance/support-matrix.md as needed. No core src/tests edits without explicit handoff.

- [ ] Independently author geometry/token expectations before reading implementation results. Use spec literal fixtures and a realistic invoice: spanning description heading, paragraph/image/caption description, full-width note, ordinary totals. Supply synthetic authored image data; no external font bundling.
- [ ] Run actual CLI/public API and fresh Native AOT output. Inspect PDF text positions, page count/order, spanned borders, alpha background pixels and image resource identity using pypdf/Poppler; compare bytes where the established paths promise equivalence.
- [ ] Pin the legacy-byte fixtures independently against #54 and the candidate. Include whole-table next-page movement and a long table with repeated spanned/rich header; all body rows remain whole.
- [ ] Exercise invalid spans, overridden illegal cell-p CSS and oversize rich row through actual entry points. Check stable diagnostics, unchanged preexisting output, and successful recovery.
- [ ] Prove the inspector catches an omitted/displaced rich-cell text run or spanned-border mutation, not just that unmodified output passes.
- [ ] Integrate bounded actual-browser cases into the existing WASM harness: compare CLI/browser bytes and independent positions/raster, rejected input and recovery. Reuse existing image binding protocol.
- [ ] Document exact HTML, margin, CSS and span contracts; explicitly retain whole-row limit and PR B separation. Produce actual PDF/previews/provenance with reproducible commands; do not claim user visual approval.
- [ ] Obtain independent core/task and integrated review; fix findings in the owning task. Run exact-head relevant CI including native/browser gates, merge the single implementation PR, then verify main. Only then begin PR B preparation/execution.

## Review priorities

1. Cell p/current paragraph state cannot leak into surrounding document or swallow later cells; structure validation precedes ordinary p repair.
2. CSS applicability checks operate on all matched declarations before cascade winners, including inherited/global keyword syntax.
3. Sum spans in both measure and paint loops; no cell-index/track-index conflation, false internal border or percent reference regression.
4. Default/empty margins follow one explicit algorithm and do not create font requirements or alter legacy direct-text bytes.
5. Rich row image decoration ranges remain after all cell boxes and belong to repeated row ranges; image references are reused.
6. Charge before retention and before repeated headers, keep cancellation, and reject oversized rows without partial output. Do not add row fragmentation in this PR.

## Required compatibility migration from #54

#54 intentionally rejects text after a direct img in TableImageTests, scripts/verify_table_images.py and scripts/wasm_probe/table_image_cases.mjs. PR A makes that source order valid. Do not preserve an obsolete negative assertion or remove rejection/recovery coverage wholesale.

- Core implementer owns tests: replace only obsolete mixed direct-image/text rejection cases with still-forbidden image-in-span or image-in-p cases; add positive ordered-flow coverage for text after a direct image.
- Root owns existing independent scripts: narrowly update those negative input cases in verify_table_images.py and table_image_cases.mjs to still-forbidden image-in-span/paragraph inputs. Preserve actual entry-point diagnostics, preexisting destination/output preservation where applicable, and successful recovery checks.
- Keep all #54 positive byte, geometry, alpha, resource-identity and header-repetition checks unchanged. Perform this migration only for cases whose content contract deliberately changes; unrelated old rejection cases stay.
- Actual CI additionally identified the legacy public-API table probe's `colspan="1"` rejection. Root owns migrating it to invalid `colspan="0"`, preserving `FPDF1103` and destination checks. The unchanged E1 `colspan.html` input overfills three tracks and now correctly expects coverage error `FPDF1101`; update only that manifest diagnostic. Run the actual probe locally and retain all positive fixture bytes and checks.
