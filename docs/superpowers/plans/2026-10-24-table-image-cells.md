# Image-only table cells implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development to execute
> the two bounded tasks, with task reviews and a final integrated review.

**Goal:** Deliver logo/company-information composition and branded repeated
headers through fixed-column tables and current image resources.

**Spec:** `docs/superpowers/specs/2026-10-24-table-image-cells-design.md`.

**Architecture:** Retain an optional image on each table cell, share existing
image geometry, retain measured image ranges per row, and translate placements
through both existing table paths. The PNG/JPEG loaders, serializers, resource
map, public API, CLI protocol and pagination fit algorithm remain shared.

## Global Constraints

- One direct `img` per otherwise empty `td`/`th`; only formatting whitespace and
  comments may surround it. Existing text-cell behavior stays unchanged.
- No new runtime dependency or public API; retain original image instance reuse.
- Cell `text-align` positions the complete image box. No image margin, vertical
  alignment, shrink-to-fit, spanning, mixed-content cell or Flex/Grid support.
- Preserve standalone image and text-only table geometry and output order.
- Charge before retaining commands, including each repeated image/header box;
  retain cancellation, source diagnostics and failure-before-output guarantees.
- Only one implementation PR at a time; no release or license amendment.

## Review Focus

1. Empty inline wrappers, `br`, `&nbsp;` and text after an image cannot escape the
   cell or silently become a new paragraph.
2. A colored cell cannot cover image padding/background/borders due to retained
   paint-box ordering; percentages use the correct containing width.
3. Positive table-relative image tops translate correctly for whole-table
   movement and repeated headers, without double offsets or sign inversion.
4. Header repetition charges image draws and decorations while preserving one
   resource identity; exact budget failures leave output untouched.
5. Image-only cells create no text line or font-role requirement; unchanged
   standalone images and text-only tables keep established output.

### Task 1: Parse, measure and place image-only table cells

**Files:**
- Modify `src/FactsPDF/Tables/TableModel.cs`, `TableMarkup.cs`, `TableLayout.cs`.
- Modify `src/FactsPDF/HtmlDocumentReader.cs`, `BlockTreeTraversal.cs`,
  `BlockLayout.cs`, `TextLayout.cs`, and image-resource XML documentation in
  `src/FactsPDF/Api.cs`.
- Create `src/FactsPDF/Images/ImageLayout.cs` if a separate shared resolver is
  clearer than an internal resolver on `BlockLayout`.
- Create focused `tests/FactsPDF.Tests/TableImage*Tests.cs`; reuse authored image
  helpers. Narrowly adjust only tests whose previous image-cell rejection is
  intentionally replaced by this contract.

**Interfaces:**
- Consume current `ImageNode`, `PdfImage`, `BoxStyle`, `Paragraph.Style`,
  `LayoutTable`, and `PlacedImage`.
- `TableCell` retains its paragraph and an optional image. Its parser state must
  distinguish formatting whitespace, text/inline content, and an accepted image.
- A shared image resolver consumes `ImageNode` and containing width and returns
  content offsets, content width/height and outer width/height, preserving the
  existing standalone calculation and errors.
- `MeasuredTable` adds `List<PlacedImage>`; `MeasuredTableRow` adds image start
  and count. Image tops are positive table-relative distances.
- Short placement uses table X/top. Paginated placement uses
  `rowTop + image.Top - row.Top`. Decoration ranges include image boxes after
  cell boxes. The resource writer needs no format-specific change.

- [ ] Add failing tests before production edits. Include a 32x16 image in an
  80pt track with 4pt cell padding and 1pt borders: 70pt content width. At 50%
  image width with 2pt image padding and 1pt image border, content is 35x17.5pt,
  outer box 41x23.5pt and decorated row 33.5pt when its sibling is shorter.
  With the ordinary 36pt page margin, expected content X is 44pt left, 58.5pt
  center, 73pt right; expected content top is 44pt.
- [ ] Pin the five Review Focus cases with literal expected geometry, resource
  identity, diagnostics, command counts and output preservation. Include an
  image-only `th` with no supplied bold font and a visible adjacent text cell.
- [ ] Run focused tests against the merged PNG base and retain the expected
  unsupported-cell failures. Record one existing text-only and standalone-image
  output before changing the core for subsequent byte comparison.
- [ ] Implement parser shape guards, shared geometry, measurement and both
  placement paths. Charge before retention and check cancellation in new loops.
- [ ] Run the focused table/image tests, then the full Release solution once.
  Verify the retained pre-change output bytes, self-review, and commit only
  task-owned production/test files.

### Task 2: Independent branded-report evidence and documentation

**Files:**
- Create `scripts/verify_table_images.py`, a small
  `scripts/wasm_probe/table_image_cases.mjs` integration and
  `examples/branded-business-report.html`.
- Update `scripts/wasm_probe/run.mjs`, `.github/workflows/wasm-probe.yml`.
- Update current support statements in `README.md`, `docs/tables.md`,
  `docs/images.md`, `docs/evaluation.md`, `docs/development.md` and
  `docs/acceptance/support-matrix.md` where applicable.

**Interfaces:**
- Consume the unchanged CLI `--image name=path`, existing browser
  `convertImages` adapter, `scripts/png_fixtures.py` authored samples and Task 1's
  image-cell HTML contract. No new resource protocol.
- Produce actual PDF/page previews, a provenance JSON and repeatable commands;
  no bundled external fonts or claims of user reference approval.

- [ ] Author independent expected image matrices, transparent pixels over cell
  color, text positions and repeated-header page/image counts for small fixed
  geometry. Include short-table movement and distinct body-row tokens.
- [ ] Generate real CLI PDFs; inspect with pypdf and Poppler, optional qpdf.
  Compare Native AOT bytes when provided. Confirm invalid mixed content leaves
  the target intact and conversion recovers.
- [ ] Add the same bounded cases to the existing actual browser workflow,
  compare with CLI and independently inspect browser-produced PDFs.
- [ ] Provide a polished one-page synthetic business report using a logo in one
  cell and company details in its neighbor, then line items and page furniture.
- [ ] Update docs with exact content, geometry and pagination limits, run affected
  checks, obtain task/final reviews, then open the single implementation PR.
- [ ] Complete exact-head CI, merge, verify main and record actual outcomes.
