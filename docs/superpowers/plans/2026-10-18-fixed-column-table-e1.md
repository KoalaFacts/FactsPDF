# Fixed-column table E1 implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, task by task, with one fresh independent whole-branch review. Preserve the existing workspace and evidence.

**Goal:** Render the approved explicit-column, single-page table subset through the existing shared converter and its adapters.

**Architecture:** Retain tables as leaf nodes in the block tree, with strict table structure and existing computed text/box styles. Extract the existing borrowed-line wrapper and text placement helper without changing its behavior. Measure cells with that shared wrapper, charge retained commands during measurement, and place each entire table through existing occupied-page/ancestor-fragment handling.

**Tech stack:** .NET 10, existing C# core and NUnit tests, existing CLI/Native AOT/WASM hosts, Python/pypdf/Poppler/qpdf verification. No new runtime dependencies or public API.

**Spec:** [Approved E1 contract](../specs/2026-10-18-fixed-column-table-e1-design.md).

**Execution record:** The implementation checklist below records the feature work. Final review corrections, exact-head CI and merge state are recorded in [PR #33](https://github.com/KoalaFacts/FactsPDF/pull/33). One fresh whole-branch review found invisible-table pagination and inclusive-width boundary defects; both have reproducing RED→GREEN tests. The obsolete legacy table-target diagnostic was also corrected without promoting E2 targets.

**Approval:** Parent thread `01a1188a-9700-74ff-b75f-6fb3037b100f` supplied the original exchange: assistant message `Sentinel_3e9cdec088888191be74122848231791` at 2026-10-18 04:54:35 UTC offered explicit colgroup/col widths, error on missing/mismatched widths, headers/wrapping/style roles and indivisible single-page placement, with the complete proposal linked; user reply `Sentinel_ee33008619008191b911a380e3431361` at 05:02:34 UTC: “先接受”. The subsequent delegation explicitly authorizes implementation, independent review, normal commits/CI/comment resolution and merge. No repeated design/plan approval is needed.

## Global constraints

- Exactly one colgroup with 1–16 col elements; optional thead with one tr; mandatory tbody with at least one tr; equal cell counts; explicit table closing structure.
- Positive pt/px/% column widths; sum equals resolved table content width within 0.001pt; no inference, distribution or shrink. Column tracks include cell padding and borders; content width must remain positive.
- Text and existing span/b/strong/i/em/br only inside cells; default th bold role; explicit fonts and all existing shaping/fallback restrictions remain.
- Existing table/container and cell box/text property subsets; col accepts width only; row/section/group accept inherited text properties only. No cell width/margin/break, spanning, nested tables, cell images/lists/paragraphs, border collapse or spacing controls.
- Whole table stays on one page; move once if necessary; fail FPDF1302 if it cannot fit an empty page with required leading decoration. No E2 row paging or repeated headers.
- Respect input/element/depth/CSS/font/page/display/output limits and cancellation. Charge commands before retaining measured text/paint. Failure leaves caller destination unchanged.
- Preserve existing non-table bytes, interfaces and resource behavior. No database, migration, public release, Library upload or local browser printing/policy change.

## Review focus

- Structural whitespace versus decoded nonbreaking space, orphan structural tags and optional-end-tag repair: reject malformed shape explicitly.
- CSS applied by selectors (including overridden declarations), percentages in nested containers and header role defaults: enforce applicability and existing cascade semantics.
- Empty/whitespace/br-only cells and a mixed-font tall cell: row height derives from actual wrapped content and decorations, with top alignment.
- An entire table moving after lists/images, inside painted/padded ancestors, or following explicit breaks: no phantom page, overlap or lost leading decoration.
- Tiny display budgets and huge cell content, exact page/output boundaries and cancellation: stop before retaining excess commands and preserve destination bytes/position.

## Task 1: strict table structure and styles

**Files:** create `src/FactsPDF/Tables/TableModel.cs`, `src/FactsPDF/Tables/TableMarkup.cs`, `tests/FactsPDF.Tests/TableStructureTests.cs`; modify `HtmlDocumentReader.cs`, `BlockTreeTraversal.cs`, `Css/CssStylesheets.cs` and the obsolete unsupported-table assertion in `ConversionTests.cs`.

**Interfaces:** `TableNode(TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChild` owns `List<TableColumn> Columns` and `List<TableRow> Rows`; `TableColumn(CssLength? Width, int SourceOffset)`; `TableRow(bool IsHeader, int SourceOffset)` owns cells; `TableCell(Paragraph Paragraph, BoxStyle BoxStyle, int SourceOffset)`. `TableMarkup` owns the current TableNode/row/cell, validates child transitions and completion, and exposes cell content to the existing inline reader. Column widths remain unresolved until layout.

- [x] Add parsing tests using existing ReadTree/paragraph projection APIs. Valid explicit point/percent tables retain header/body text in order, inline styles and empty cells; assert th bold default and normal override.
- [x] Add exact diagnostic/source-offset and unchanged-output tests for missing/reordered/duplicate sections, orphan tags, 17 columns, row-cell mismatch, optional table end tags, invalid placement, colspan/rowspan, forbidden cell content and tag-specific CSS, including matched overridden declarations and CSS preflight priority.
- [x] Run `dotnet test FactsPDF.slnx -c Release --filter FullyQualifiedName~TableStructureTests`; expect new valid inputs to fail FPDF1102 and new shape diagnostics to mismatch, proving missing support.
- [x] Implement the model and strict markup state machine; reuse the existing style path, element/depth counting and inline text reader. Extend text-only compatibility traversal to table cell paragraphs. Do not expose table structural nodes as ordinary block descendants.
- [x] Run targeted tests and the full suite; all must pass. Commit the structure/style slice.

## Task 2: shared text lines and indivisible table layout

**Files:** create `src/FactsPDF/TextLines.cs`, `src/FactsPDF/Tables/TableLayout.cs`, `tests/FactsPDF.Tests/TableLayoutTests.cs`; modify `TextLayout.cs` and `BlockLayout.cs`.

**Interfaces:** `TextLines.Wrap(Paragraph, double available, PdfOptions, Dictionary<(FontRole Role,int Scalar),(PdfFont? Font,double Width)>, CancellationToken, ListMarker? firstMarker=null)` retains the existing borrowed-line iterator contract. `TextLines.AppendLine(Line, double x, double baseline, List<PlacedText>, Action<int,int> charge, CancellationToken, int sourceOffset=-1)` charges before retaining each copied run. `LayoutTable(TableNode Node,double X,double AvailableWidth) : BlockLayoutStep`. `TableLayout.Measure(LayoutTable, PdfOptions, shared glyph cache, CancellationToken, Action<int,int>)` returns a measured width/height plus bounded local text runs and paint boxes; it never emits pages or writes output.

- [x] Add failing layout tests for authored Courier 12pt/18pt lines, tracks80/140/260, padding4, border1, row heights28/28/46 and total102. Test point/px/percent equivalence, default/explicit table widths, cell alignment and separate touching borders.
- [x] Test missing/auto/zero column widths, sums outside0.001pt, zero content widths and overwide/tall tables; errors must be FPDF1302 with available source offsets and unchanged destinations.
- [x] Test table/header defaults with explicit four-role fixture fonts, fallback and CJK wrapping; missing roles/glyphs retain existing diagnostics. Empty cells retain zero text-line height plus decoration; explicit br uses existing line metrics.
- [x] Run the new tests; expect unsupported table layout (feature missing), not compilation errors.
- [x] Extract wrapping and line placement verbatim behind the shared helper, then measure tables with bounded retained commands. Compute cell content boxes from track widths, row heights from their tallest wrapped cell and all cell backgrounds/borders to the row height.
- [x] Add atomic table placement beside the existing image path, preserving leading/trailing padding, explicit breaks, occupied-page detection, ancestor fragment origins and paint order. Translate already charged runs/boxes once after placement.
- [x] Run targeted and full tests, including existing image/list/font/fragmentation cases. Commit the layout slice.

## Task 3: limits, combinations and real entry-point evidence

**Files:** create `tests/FactsPDF.Tests/TableBoundaryTests.cs`, `examples/tables-report.html`, `examples/tables-page-movement.html`, `probes/tables/`, `scripts/verify_tables.py`, `scripts/wasm_probe/table_cases.mjs`, `docs/tables.md`; update fixture expectations, the readiness script/CI integration, WASM runner, support guides and this plan/spec status.

**Interfaces:** Verification-only API host accepts `input.html output.pdf font-manifest.json` using existing PdfOptions. Python verifier consumes the existing four-style font manifest and actual API/CLI/Native executables, records exact source/input/font hashes and actual PDF geometry/text/roles/paint. WASM cases use the existing protected browser host conversion API and compare actual PDF bytes with CLI output.

- [x] Add/run boundary verification tests (initially green after Task2; see ledger ruling) for exact display/page/output budgets, large decorated/empty cells, cancellation, repeated/concurrent conversion and table movement adjacent to lists/images inside sliced ancestors. Assert bytes and stream position on failure; mutate an inspected PDF to prove missing text/displaced cell detection.
- [x] Complete only behavior needed by those tests, then run all core tests. Preserve the prior readiness receipts; promote the11 authored fixtures to actual success/error expectations without changing their HTML hashes or substituting engine-derived geometry.
- [x] Add one bilingual report with all four roles, explicit borders/backgrounds and wrapping, and one surrounding-content example that forces the entire table to another page. Generate through actual API/CLI/Native; inspect qpdf, extracted text/order, independent geometry, font resources and raster paint. Open every full page and record observations; user page-reference approval stays separate.
- [x] Add actual table cases to the existing protected WASM CI host, including invalid input and recovery. Do not launch a locally prohibited browser or substitute browser printing.
- [x] Update support documentation and examples with exact limits and the distinction between E1 single-page tables and E2 paging. Run local Release build and full suite; publish a fresh Native AOT executable and verify actual PDFs. Final review fixes receive the same checks before merge, with receipts linked from PR #33.
- [ ] Commit and push a draft PR, obtain one fresh independent full-branch review, reproduce/fix findings and handle remote review comments. Convert to ready when reviewable; wait for exact-head CI/review, merge normally after gates, verify final main and preserve all evidence.

## Verification commands and receipts

Use the existing installed SDK `/workspace/.dotnet/dotnet` with `DOTNET_ROOT=/workspace/.dotnet`, `DOTNET_CLI_HOME=/workspace/.dotnet-home`, `NUGET_PACKAGES=/workspace/.nuget/packages`. Save full commands/results under `/workspace/scratch/factspdf-e1/`, with `progress.md` as the persistent task ledger. Existing baseline62cc5fb has already passed796 tests and7 main workflow runs; do not repeat that unchanged baseline as new feature evidence. Every newly authored test first demonstrates the missing behavior, then passes implementation.

No skill helper scripts are installed in this cloud skill package; equivalent explicit commands and ledger entries provide the same plan/task/review records. The user's preservation instruction overrides generic scratch-workspace cleanup guidance.
