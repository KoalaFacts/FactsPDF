# D1 bounded JPEG implementation plan (LoadImage scope confirmed)

> **For agentic workers:** execute in this session with `superpowers:executing-plans`; use focused red/green checks and a fresh independent whole-branch review before completion. Do not implement the public interface until the concrete decision in the design is resolved.

**Goal:** An explicitly supplied bounded JPEG appears at the correct size/orientation in a real single-page bilingual PDF through the shared .NET/CLI/Node/WASM core; malformed, unsupported and over-budget resources fail deterministically before output copying.

**Base:** main `d1e9aa31dd54a244145dc3771b7bfa3a4fad3fa3`, isolated branch `feat/bounded-jpeg`. C1/C2 are complete. No AGENTS.md was found in the selected workspace. [Design](../specs/2026-10-18-bounded-jpeg-design.md) is confirmed for implementation; actual support still requires the tests and PDF acceptance below. No Pages retry, settings/permission change, release or Library upload belongs to this plan.

**Architecture:** immutable explicit resource loader → bounded marker/Huffman validation → per-conversion resource snapshot → block image leaf → atomic image placement → shared binary DCT XObject writer. Existing no-image conversion remains byte-compatible. Pillow/Poppler/qpdf/pypdf are independent verification tools only; no added runtime dependency.

**Decision resolved 2026-10-18 01:50 UTC:** user confirmed the presented scope with “loadimage 比较好”; use content-identified `LoadImage` (baseline JPEG only; other formats explicitly rejected), retaining the proposed `PdfImage.LoadImage`, `PdfImageLimits`, `PdfOptions.Images` map and CLI `--image name=path`, together with baseline JFIF/upright/block-only/outside-lists first-stage semantics. This is the previously unapproved image public API, not a request to repeat ordinary execution/merge authorization.

## Task 1 — Design, authored resource corpus and independent review

Files: this plan; paired design; `scripts/jpeg_fixtures.py`; `tests/fixtures/jpeg/{README.md,manifest.json,acceptance-cases.json,report.html,*.jpg}`.

- [x] Inspect historical scope, current API/font resource model, CLI stream/output protection, HTML/tree/layout and both PDF serializers, and private WASM adapter. Check base main and avoid repeating C2 delivery.
- [x] Author synthetic JPEG pixels and positive/negative files, hashes, proposed loader expectations and independently derived geometric/limit expectations. Keep proposed outcomes clearly separate from engine results.
- [x] Independently decode positive source images; inspect asymmetric chart. Verify dimensions, all file hashes and mutated over-limit headers without decoding those large declarations.
- [x] Self-review spec/fixture consistency and have an independent design reviewer examine public contract, JPEG correctness, bounded work, compatibility, output and testability. Resolve actual findings; do not label product decision approved from a technical review alone. Two rounds completed: R1–R4 and implementation notes closed; no remaining design blocker.
- [x] Record exact pending product decision and review evidence in the handoff.
- [x] Product decision recorded from the explicit 2026-10-18 instruction: LoadImage, content recognition, the agreed JPEG/block/named-resource/CLI scope.
- [x] Commit the design/fixtures as their own reviewable change. Do not update supported examples/README to claim image implementation yet.

## Task 2 — Immutable byte resource and complete bounded baseline scan validation

Create `src/FactsPDF/Images/{PdfImage.cs,PdfImageLimits.cs,JpegReader.cs,JpegEntropyReader.cs,JpegExif.cs}` and `tests/FactsPDF.Tests/{JpegResourceTests.cs,JpegEntropyTests.cs,JpegResourceBoundaryTests.cs}`. Update test project fixture content copying only as needed. Public signatures/defaults exactly as approved in the design; internal `JpegReader.Read(ReadOnlySpan<byte>, PdfImageLimits, CancellationToken)` returns validated pixel/component metadata. Internal entropy reader consumes a single scan using validated fixed-size table/component metadata; it does not own/copy a pixel plane.

- [x] Write externally observable loader tests first: ten positive files and all 25 proposed rejection files, plus immutable span ownership, nonseek/short-read stream, stream left open, length limit+1, option misuse and pre-cancellation. Run the targeted tests and retain actual RED due to missing loader behavior, not unrelated setup failures.
- [x] Implement bounded stream/span loading and marker range checks. Read SOF dimensions and check all derived budgets before entering entropy or allocating size-dependent state. Validate supported JFIF, tables, component IDs/sampling, scan references and bounded Exif orientation exactly as the design; reject unsupported forms explicitly.
- [x] Add small hand-authored Huffman/entropy tests before implementing that stage: valid DC0/EOB, amplitude extremes, ZRL at legal boundary, invalid run overflow/category/code, truncated amplitude/MCU, FF00, bad padding, missing/extra/out-of-order restarts, first/last partial MCU and early/final EOI. Cover legal short/long FF marker fill before headers/EOI/RST, only-FF-to-EOF and FF…00 distinctions, with cancellation inside long fill traversal. Preserve complete symbol payload in the oversubscribed-prefix fixture. Assert codes and bounded completion without calling the product parser to synthesize expected answers.
- [x] Implement fixed table storage/canonical Huffman decoding, expected MCU/block traversal, restart interval/sequence and final padding/EOI checks. Do not substitute SOI/EOI detection or a permissive pixel decoder for the declared validation contract.
- [x] Verify exact byte/dimension/pixel/decoded-byte/MCU and fixed metadata/segment boundaries; 64-bit checked arithmetic; cancellation during stream/IFD/scan work. Confirm no IDCT/full pixel allocation and no new package reference.
- [x] Run `dotnet test FactsPDF.slnx -c Release --filter 'FullyQualifiedName~Jpeg'`; inspect failures before implementation expansion. Commit loader stage only after meaningful tests pass.

## Task 3 — Resource map and strict HTML image structure

Modify `src/FactsPDF/{Api.cs,PdfConverter.cs,DocumentModel.cs,HtmlDocumentReader.cs,BlockTreeTraversal.cs}`; create `Images/ImageResources.cs` and `tests/FactsPDF.Tests/ImageStructureTests.cs`. Internal resource snapshot owns an ordinal map of validated immutable images, with checked per-entry accounting. `ImageNode` carries image instance, computed box/text style and the img source offset.

- [x] RED tests: case-sensitive explicit binding, missing/invalid src, null/invalid names/map entries, max+1 enumeration, alias budget counting, unused resources counted but not drawn, mutation-after-snapshot where supported, and no file/network interpretation. Duplicate src preserves the already-existing tokenizer `FPDF1101` at the second attribute start (offset 17 in the authored case); do not demand RED for unchanged behavior or change tokenizer priority.
- [x] RED strict structure/style tests: void and self-closing img, forbidden closing tag/children, root and nested ordinary containers, explicit paragraph/inline/list ancestors rejected, direct text around img, supported existing box CSS and unsupported attributes/height, prior CSS preflight error priority.
- [x] Implement option validation/snapshot before HTML; bind images without introducing implicit I/O. Add image leaves and explicit CSS eligibility. Preserve no-image trees and source offsets. Teach the existing `BlockTreeTraversal.Paragraphs` text projection to skip image leaves, retaining preceding/following text; test a document with sibling image and list without allowing an image inside a list.
- [x] Update only intentional old unsupported-img tests: valid unbound names become `1604`; syntactically invalid src becomes `1103`; unimplemented PNG/data sources are not positive support. Keep unrelated old failures and nine-target accounting unchanged until real output acceptance.
- [x] Target `ImageStructureTests`, `ConversionTests`, `WasmProbeTests` and existing CSS/list structure suites; commit only after the targeted checks pass.

## Task 4 — Whole-image layout and binary PDF embedding

Modify `BlockLayout.cs`, `TextLayout.cs`, `DocumentModel.cs`, `PdfConverter.cs`, `PdfSerializer.cs`, `UnicodePdfSerializer.cs`; create `Images/PdfImageWriter.cs`, `tests/FactsPDF.Tests/{ImageLayoutTests.cs,ImageOutputBoundaryTests.cs}`. Proposed internal steps: `LayoutImage(ImageNode Node, double ContentX, double ContentWidth, ...)` with resolved full outer geometry; `PlacedImage(PdfImage Image, double X, double Y, double Width, double Height, int SourceOffset)`. Final shape may use existing layout conventions, but responsibilities and the atomic outer-box fit must remain explicit.

- [x] RED geometry assertions from authored dimensions, not renderer capture: natural 96dpi; explicit 200×100 from 320×160; percent width; gray 48×24; padding/border outer fit; left alignment; text before/after; image-only page; repeated shared resource.
- [x] RED pagination/budget cases: exactly fits vs one unit too tall, remaining-space move, already pending ancestor top decoration, nested container slices, own bottom decoration included in fit, image followed by explicit paragraph break, no phantom page, zero/overwidth, MaxPages/commands/output and no writes on failure.
- [x] Add image branch before TextLayout's LayoutParagraph cast. Resolve natural/explicit content width and aspect height with checked finite geometry; move whole outer box at once. Share existing page-state and ancestor painting rules; count images as occupied content even without text runs.
- [x] Add binary DCT stream writer and per-page XObject resources to both serializer paths. Keep no-image object ordering/bytes unchanged; use first-use order and reference identity reuse. Charge image byte writes against MaxOutputBytes with cancellation, and retain converter's final defensive display-command recount.
- [x] Assert original JPEG stream bytes, transform, colorspace and draw matrix independently via pypdf/Poppler for small ASCII output; qpdf accepts output. Tests must not pass from matching only /Width metadata.
- [x] Run targeted image/layout/output tests then the full core suite once. Re-run further broad checks only for subsequent changes or unresolved failures; commit after passing.

## Task 5 — CLI/Node/private WASM resource boundaries

Modify `src/FactsPDF.Cli/CliApplication.cs`, `tests/FactsPDF.Tests/CliTests.cs`, `scripts/test_node_cli_integration.mjs`, `probes/browser-wasm/{ProbeConversion.cs,Program.cs,wwwroot/main.js,wwwroot/resources.mjs}`, `tests/FactsPDF.Tests/WasmProbeTests.cs`; add focused JPEG fixture cases to existing `scripts/wasm_probe` plumbing and normal CI as needed. Reuse the unchanged generic argv facility in `scripts/node_cli_process.mjs`; it is not a new public Node adapter API.

Private WASM shape: `RunImages(string html, byte[] fontBytes, int[] fontLengths, int[] fontRoles, byte[] imageBytes, int[] imageLengths, string[] imageNames, bool subsetFonts)` returns the existing ProbeResult. Its JS packer takes already supplied `{ name, bytes: Uint8Array }` entries and checks count/name/duplicates/per-image/aggregate size before allocating the packed array. No image URL field or fetch helper is added. Caller-owned input arrays and JS/.NET interop allocations are outside the loader's retrospective limits; each boundary checks the arrays it receives before its own copies. Shape errors use `B2_INPUT_IMAGES`, JS size errors `B2_RESOURCE_LIMIT`, direct .NET protocol size errors `FPDF1603`.

- [x] RED CLI tests for name=path parsing (spaces and subsequent `=`), case-sensitive duplicate names, count/total limits, explicit stream reads, output alias guard including image path, missing file, unsupported/corrupt resource, stdout/file success and preserved sentinels/no target on failure/cancel.
- [x] Implement `--image` and help with fixed public defaults, no resolver. Pass explicit image arguments through the existing Node argv helper without shell interpolation and enforce existing timeout/isolation semantics.
- [x] RED private WASM protocol tests for name/length count mismatch, empty or duplicate names, overflow-safe length sum, aggregate cap checked before copying, invalid JPEG and success→failure→success recovery. Extend the existing private protocol only; keep old call forms functioning.
- [x] Normal protected browser CI runs actual shared .NET WASM with explicit bytes supplied from the harness's fixed local fixtures and saves result evidence. Preserve sandbox and existing browser policy; no local printing, alternate browser tool workaround or permission grants. Record actual runtime/version and new image output bytes.
- [x] Respect each acceptance row's applies_to/not_applicable_reason and global assertion_entry_scope. Four custom PdfOptions cases, custom loader limits and SourceOffset assertions are managed-API-only; CLI/ProbeResult expose no offset field. No imagined CLI flags, diagnostic API additions or automatic cross-entry credit. Execute the two default-limit recipes at their real entries and assert each boundary's actual code/exit behavior. Do not claim that .NET cancellation tokens are a newly supported interactive browser cancel UI.

## Task 6 — Real single-page acceptance, independent branch review and normal PR gates

Create `probes/images/{FactsPDF.Images.csproj,Program.cs}`, `scripts/verify_images.py` and `docs/images.md`; promote the approved/implemented report to an examples location only after successful real conversion. Update existing T-jpeg/compatibility/support docs truthfully; preserve all still-unimplemented targets.

- [x] Implement an acceptance probe that consumes explicitly supplied fonts/JPEGs, emits API PDF and actual diagnostics with build/input/resource hashes. Verification reads the authored `acceptance-cases.json`; use meaningful corrupted-output checks to show it detects missing/wrong/duplicated image streams, geometry, text order and output-boundary failures.
- [x] Build and run managed API/CLI, Node and **new** Linux AOT at the actual D1 head. Compare same input/options/font/resource bytes; don't reuse old C2/native output as D1 evidence. WASM evidence must come from the same reviewed source tree or clearly identify any mismatch.
- [x] Run qpdf, Poppler extraction/bounds, JPEG raw stream comparison and actual PDF rendering. View every produced page, check chart corners/up arrow/aspect, caption/body order, borders and no clipping/overlap. Record actual page count, independent review, tool versions and hashes. Keep user-approved reference status pending until actually reviewed.
- [ ] Review the entire branch with a fresh independent reviewer, including test adequacy, scan/parser resource limits, both serializer paths, CLI no-output boundary, AOT/WASM and no-image compatibility. Resolve findings and receive normal PR comments before ready/merge.
- [ ] Same-head required CI, no unresolved blocking review comments, authorized normal PR merge and actual main verification. Existing unrelated Pages 404 is reported once and not retried or reconfigured. No release or unapproved Library upload.

## Evidence and stopping conditions

Design-stage evidence is at `/workspace/scratch/factspdf-d1-jpeg`; it is not product acceptance. Keep a progress ledger recording exact commands/results/heads, review rounds and the user's actual API decision. A future parser defect, resource bound gap or independent render failure blocks that stage; report the concrete failing case and fix it before continuing. An unsupported browser capability does not authorize policy bypass.

Current state: Tasks1–5 implemented. Real output at5ca3ad5 passed API/CLI/Node/new Linux AOT and protected browser WASM, with identical bytes for4documents, independently inspected PDFs and actual search/copy/paste. Whole-branch review found an Exif diagnostic mismatch and missing boundary regressions; fixes and787 passing tests are included in the next commit. Final focused review, same-head CI/comments, ready/merge and actual main verification remain pending. No Library upload or release.
