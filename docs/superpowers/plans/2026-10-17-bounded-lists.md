# C2 Bounded Lists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Render decimal and disc lists with at most two nested levels and correct wrapping/pagination in the existing report engine.

**Architecture:** Retain strict list structure in the existing block tree. `ListLayout` measures uniform sibling marker gutters and binds each marker to an item paragraph; block traversal reserves the gutter and text layout places marker and first line together. Share disc painting between both existing PDF serializers.

**Tech Stack:** C#/.NET 10; NUnit; existing Python/qpdf/Poppler verification and protected browser WASM CI. No product dependency additions.

**Spec:** `docs/superpowers/specs/2026-10-17-bounded-lists-design.md`

## Global Constraints

- Only `ol/ul/li`, decimal/disc, at most two list levels; no new public options or CLI flags.
- Every ordered list starts at 1; no start/reversed/type/value, counters or list CSS.
- Gutter I=max(2S,W+0.5S); disc diameter=0.3×item font size, center=baseline+0.3×item font size.
- Marker travels with the first line, never repeats on continuation pages; no keep-together/widows/orphans support.
- Existing paragraph break/margin and container slice rules remain; all old no-list bytes and errors remain.
- Existing limits, cancellation, font licensing and pre-copy output protection apply to generated markers.
- No database/release/Pages changes, browser printing or policy changes. Same-head normal CI and independent branch review are merge gates.

## Review Focus

- An empty first paragraph or nested-list-only item still gets exactly one correctly positioned marker (Task 2).
- A large marker style and smaller first child paragraph cannot move the marker outside its first line or onto another page (Task 2).
- Padding and percent widths use the reduced item containing width, including nested lists inside decorated parents (Task 2).
- A later wide marker, digit absent from explicit fonts, or exhausted command budget cannot silently produce overlapping/partial output (Tasks 2/3).
- Optional li closure must not swallow a nested list or repair an unclosed inline/container (Task 1).

---

### Task 1: Strict list tree

**Files:** Modify `src/FactsPDF/HtmlDocumentReader.cs`; create `tests/FactsPDF.Tests/ListStructureTests.cs`.

**Interfaces:** Consumes existing `HtmlDocumentReader.ReadTree(string, PdfOptions, CancellationToken)`, `BlockNode`, `ParagraphNode`; produces unchanged types containing `ol/ul/li` nodes, only valid trees, and existing source offsets.

- [x] Write tests `RetainsMixedTwoLevelListAndParagraphOrder`, `OptionalItemEndTagsPreserveNesting`, `EmptyItemsAndListsRetainStructure`, `InvalidListStructureProtectsOutput` (orphan li, direct text/block/style, third level, unclosed inline), `UnsupportedListAttributesProtectOutput` (start/reversed/type/value), `ListsCountTowardsExistingElementAndDepthLimits`. Assert actual nodes/source order and exact `FPDF1101/1103/1002/1003`, unchanged stream bytes/position.
- [x] Run `/workspace/.dotnet/dotnet test tests/FactsPDF.Tests -c Release --filter FullyQualifiedName~ListStructureTests`; expected RED because ol/ul/li are rejected as FPDF1102.
- [x] Add list/container predicates, direct-child and depth validation, allowed li omission, and existing box-style eligibility in `ReadTree`; keep paragraph-only margin/break applicability. Reject unsupported list attributes through the existing allowlist.
- [x] Run the targeted command and the core suite; expected all pass.
- [x] Commit `feat: retain strict two-level list structure`.

### Task 2: Marker gutter and first-line pagination

**Files:** Create `src/FactsPDF/ListLayout.cs`, `tests/FactsPDF.Tests/ListLayoutTests.cs`; modify `DocumentModel.cs`, `BlockLayout.cs`, `TextLayout.cs`, `PdfPaintSerializer.cs`, `PdfSerializer.cs`, `UnicodePdfSerializer.cs`, `PdfConverter.cs`.

**Interfaces:** `ListLayout.Prepare(DocumentRoot, PdfOptions, CancellationToken)` returns a per-layout `ListLayout` holding marker/gutter metadata; do not persist layout-dependent geometry on a reusable parsed tree. `ListLayout.Gutter(BlockNode)` returns double; `ListLayout.Marker(Paragraph)` returns nullable `ListMarker`. `ListMarker` holds nullable decimal Text (null=disc), item TextStyle, measured Width, SourceOffset. `LayoutParagraph` gains optional marker and marker right-edge X. `LayoutPage` gains `List<PlacedDisc> Discs`; `PlacedDisc` holds X center, PDF baseline-space Y center, Radius, Color. `PdfPaintSerializer.AppendDiscs(...)` consumes discs with the same output/cancellation limits.

- [x] Write hand-computed Courier tests: 12 items at 12pt reserve max(24,21.6+6)=27.6pt; 100 items reserve34.8pt; right edges align; normal continuation X is constant; nested item adds its own gutter. Tests cover mixed font size/role, explicit font missing digits, decimal search text, fontless discs, first-line baseline equality, large marker strut, nested-only/empty items, empty lists, left/center/right and percent/padded boxes.
- [x] Write pagination/limits tests: pageheight100 margin20 with a lead paragraph moves marker+first line together; long item spans pages but emits one marker; explicit break inside item preserves subsequent numbers; painted list/item slices retain only first/last edges; command cap exact (number runs/discs included), page/output/width failure protects stream, cancellation, repeated layout of the same tree is deterministic.
- [x] Run targeted `ListLayoutTests`; expected RED (missing markers/gutter or absent disc model before implementation).
- [x] Implement iterative list metadata preparation and first paragraph binding. Insert marker-only paragraph leaves through a layout view rather than modifying the parsed tree; preserve empty first paragraph style. Measure ordered digits with actual role fallback and invariant decimal formatting. Use reference identity for metadata keys.
- [x] Reserve each list gutter only for its direct items in `BlockLayout.Steps`; retain all other widths/padding. Pass marker and fixed gutter right edge to the marked paragraph, including when that paragraph is under ordinary nested blocks.
- [x] In `TextLayout`, extend only marked first lines with marker strut, force one line for an empty marked paragraph, perform existing page-fit logic with that combined first-line height, then emit marker once before body runs. Charge each retained numeric run/disc. Keep continuation line heights unchanged and no marker in the compatibility paragraph-only overload.
- [x] Add shared four-cubic-curve filled disc painting and converter command recount; leave existing no-disc/no-list serialization byte-for-byte unchanged.
- [x] Run targeted tests and `/workspace/.dotnet/dotnet test FactsPDF.slnx -c Release`; expected all pass, including old layout/resource suites. Compare an unchanged fixed ASCII sample against its recorded main PDF bytes.
- [x] Commit `feat: lay out bounded list markers across pages`.

### Task 3: Real report, cross-entry evidence and support contract

**Files:** Use `examples/lists-report.html`; create `probes/lists/FactsPDF.Lists.csproj`, `probes/lists/Program.cs`, `scripts/verify_lists.py`, `docs/lists.md`; update acceptance target generator/expectations, compatibility cases/data and `.github/workflows/ci.yml` / existing WASM fixture plumbing only as needed for the new positive list cases.

**Interfaces:** Probe takes normal explicit font paths and output directory, emits direct API PDF and small geometry/error JSON for the same HTML; CLI uses existing font/subset flags. Verification accepts actual managed/native CLI, actual probe output and fixed authored expected text/record IDs; saves exact source/font hashes and measured outcome, never invents page approval.

- [x] Establish authored text/marker assertions independent of renderer output: IDs C2-001..012 in order once; nested N01..N04 once; restarted R01/R03, empty second item; end sentinel; at least one item crosses a natural page boundary. Verifier tests corrupt IDs/marker counts/geometry to prove meaningful rejection before making evidence green.
- [x] Render via .NET API and CLI with existing licensed normal Latin/CJK font bytes; run qpdf, pdftotext bbox, inspect embedded fonts and actual page images. Compare same-input PDFs across API/CLI and newly built Linux AOT; verify numeric marker positions and disc paths without relying only on hashes.
- [x] Open every produced report PNG for actual visual review. Record actual page count, no cropped/overlapping marker or continuation, list/item decoration fragments and ordinary text after lists. Do not claim user reference approval.
- [x] Add bounded-list cases to existing protected WASM CI using the same core and existing browser sandbox checks; no local browser launch/printing. Test malformed/depth/font/geometry/budget failures and recovery across entrypoints; preserve file/stdout outputs.
- [x] Update T-list to a truthful positive contract while preserving the nine-target denominator and unimplemented targets; update compatibility dataset with only observed list changes and documentation of explicit boundaries.
- [ ] Run changed verification suites, full core suite and normal PR CI at final head; independently review whole branch, resolve actual findings and all PR comments before existing-authorized normal merge/main checks.
- [ ] Commit evidence tools/docs and link actual results in PR. No release or Pages settings changes.

## Execution and record

User has explicitly authorized continuing after independent design review without new product decisions. Execute natively in the existing isolated worktree and use a fresh independent reviewer for the complete branch. Preserve task RED/GREEN commands, commits, observed results and any necessary rulings in `/workspace/scratch/factspdf-c2-lists/progress.md`; this durable evidence is retained for the parent task.
