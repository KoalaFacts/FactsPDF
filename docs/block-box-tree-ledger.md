# Block Box Tree — Implementation/Verification Ledger

**Repository:** KoalaFacts/FactsPDF
**Only active feature PR:** [#11 — Block Box Tree](https://github.com/KoalaFacts/FactsPDF/pull/11)
**Branch:** `feat/block-box-tree`
**Base main SHA:** `ee2152bdce48134e62340ee74b9188ba57aabbec`
**License changes:** none
**State before independent review:** implementation and exact-head CI green; PR draft/unmerged. Do not treat this as an approval or publication.

## Scope rulings

- **Ruling:** Only retain Block Box Tree structure, not all of the existing broader box-model specification. The user's current scope explicitly excludes new CSS widths, borders, padding, painted backgrounds and fragments. **Cost if wrong:** geometry/page-decoration implementation still requires its own PR later.
- **Ruling:** Keep the old `Read()` and `Layout(List<Paragraph>)` as **internal test/compatibility routes**, but use tree parsing and an iterative lazy leaf traversal for real PDFs. **Cost if wrong:** legacy tests retain a flattened view; production layout still needs future block-aware coordinates.
- **Ruling:** Explicit `p/h` nodes contain a `ParagraphNode(IsAnonymous=false)`; direct container text uses `IsAnonymous=true`. The actual containing-block computed style is retained on `BlockNode.Style`. For this **structure-only** milestone, `ParagraphNode.Paragraph.Style` deliberately retains the first-inline style when a `span` starts the paragraph, preserving the prior `text-align` and font behavior. **Cost if wrong:** this reflects the old renderer rather than ideal browser inline alignment; correcting that belongs in a separately reviewed layout-semantic milestone.
- **Ruling:** Leave existing layout, fonts, serializer and CSS Cascade unchanged. Actual box-model styling remains an explicit `FPDF1201` error. **Cost if wrong:** the next milestone must redesign block-aware measurements and drawing commands rather than reuse this paragraph-only layout core unchanged.

## Test-first work and evidence

1. `c9b042b5`: 12 new `BlockBoxTreeTests` for hierarchy, paragraph boundaries, nested ordering, whitespace, styles, optional `p` closures, errors and cancellation. **RED:** GitHub Actions [run 38044921731](https://github.com/KoalaFacts/FactsPDF/actions/runs/38044921731) failed with missing `DocumentRoot`, `BlockChild`, `BlockNode`, `ParagraphNode` types, as expected.
2. `40a8f17d`: implemented document model and `ReadTree`, added lazy/iterative `BlockTreeTraversal`. Initial CI [38045020880](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045020880) uncovered two actual C# nullable warnings treated as build errors.
3. `66ecf7e8`: resolved nullable access with checked local variables for nearest block. **GREEN:** [CI 38045073218](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045073218), Ubuntu NUnit **338/338** passed (326 old + 12 new), with actual Linux x64 Native AOT and other platforms.
4. `21627e01`: added four explicit tree-layout/real PDF parity tests. **RED:** [CI 38045171356](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045171356) failed with C# compile diagnostics showing `DocumentRoot` could not be passed to `TextLayout.Layout(List<Paragraph>)`.
5. `540be46d`: added the `Layout(DocumentRoot)` overload, lazy tree traversal input and changed `PdfConverter` to use `ReadTree`. **GREEN:** [CI 38045236652](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045236652) passed Windows, macOS and Ubuntu (**342/342 NUnit**) and Linux x64 Native AOT. The other five exact-head workflows all passed: CSS syntax `38045236636`, stylesheet `38045236641`, Chrome-vs-native visual comparison `38045236657`, font subsetting `38045236637`, and Chrome box-oracle reference `38045236654`.

## Independent review: span-led alignment regression

GitHub Codex reviewed `71b52013` and identified a P2 backward-compatibility regression: direct text beginning in `<span style='text-align:right'>` changed from the old PDF's right alignment to left alignment. This milestone is **Block Box Tree only**, so it should not silently alter PDF positioning.

- Added a failing compatibility test in `9f487847`; [RED CI 38045697599](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045697599) reported **341 passed / 2 failed**.
- `441016a9` restored the legacy first-inline paragraph style while retaining the correct computed containing block style on `BlockNode`. [GREEN Ubuntu CI 38045751310](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045751310) reported **343/343 passed**.
- The eventual CSS inline-layout alignment correction remains separate. This tree refactor does not claim complete browser alignment semantics.

## Independent byte-level proof of no PDF regression

Both successful stylesheet-artifact runs used **exactly the same OS fixture fonts** with SHA-256:
- `DejaVuSans.ttf` — `ae7b7855e115a5966d8b1b3f80f254ccc117ec86f9965e202ee2940453837280`
- `DroidSansFallbackFull.ttf` — `acb6440a713d880a13a21b468ba7cd43f5a2b2934972e51be791c880730777b8`

The actual bilingual stylesheet-generated **PDF SHA-256 was identical** before and after this refactor:
`c7a487cd3e2a5c50dbd80fc8a8a0fa3edc9ab401a8dc945e837f2c6e8213fd77`

- Base `main` [stylesheet evidence 38043518235](https://github.com/KoalaFacts/FactsPDF/actions/runs/38043518235) artifact `11666329744` returned that exact PDF SHA.
- Feature [stylesheet evidence 38045236641](https://github.com/KoalaFacts/FactsPDF/actions/runs/38045236641) artifact `11666967370` returned the same PDF SHA.
- Both independent artifacts record `pdf_bytes_equal`, `extracted_text_equal`, `page_pixels_equal_120dpi`, `text_inside_page`, and `two_embedded_subset_fonts` as true.
- This is **one representative fixed bilingual PDF**, not a claim of universal byte identity across arbitrary input.

## Open finishing gates

- Independent whole-branch code review on the exact PR head.
- Verify final documentation-only commit runs all 6 checks and inspect GitHub review threads for actionable findings.
- After the user-authorized single PR merge, verify merged `main` and then stop; **do not create another feature PR in this milestone**.
