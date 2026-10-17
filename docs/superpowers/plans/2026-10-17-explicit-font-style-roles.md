# Explicit font style roles implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation and independent design/whole-branch review. User explicitly authorized the reviewed direction, sequential implementation and normal merge; do not add another approval round.

**Goal:** Add explicit normal/bold/italic/bold-italic selection on the existing ordered glyph fallback across .NET, CLI and the private browser WASM probe.

**Architecture:** Keep `Fonts`/`--font`, the immutable loader, real glyph metrics and existing serializers. Add three explicit chains and two computed style attributes; select a role before scalar fallback. Preserve the fontless Courier path, including mixed documents with explicitly embedded styled runs.

**Tech Stack:** .NET 10/C#, existing CSS/HTML parser, Node/Playwright private probe, Python/fontTools/qpdf/Poppler evidence tools. No new engine runtime dependency.

**Spec:** `docs/superpowers/specs/2026-10-17-explicit-bold-font-roles-design.md`.

## Global constraints

- Four roles are independently optional, no family/pairing requirement, no cross-role fallback.
- `Fonts` empty preserves ASCII/Courier and ordinary Unicode FPDF1301; static fonts may themselves be italic/bold.
- No automatic system search, synthetic weight/slant, warning collection, variable-font support, package publication or database change.
- Eight declared chain entries combined; 32 MiB per load and 64 MiB default total unchanged.
- Final-copy I/O/cancellation cannot promise rollback of arbitrary output streams.
- Browser verification uses actual protected Chromium on the exact head; no print or security-policy changes.

## Review focus

- A character repeated in four styles must not reuse the first role's cache entry (Task 1).
- A sole italic chain plus fontless normal ASCII must serialize valid Courier and embedded resources (Task 1).
- Static italic supplied through old Fonts remains byte-compatible and is not inferred into a different role (Tasks 1/3).
- Nested strong/em and CSS resets must select both style dimensions independently, including paragraph struts and pending whitespace (Task 1).
- Every role resource participates in count/byte/output-path protection, and failures preserve output/recover without stale PDF (Tasks 2/3).

### Task 1: Core role selection, CSS/HTML and serialization

**Files:** `src/FactsPDF/Api.cs`, `DocumentModel.cs`, new focused internal `FontRoles.cs`, `PdfConverter.cs`, `HtmlDocumentReader.cs`, `InlineCss.cs`, `Css/CssDeclarations.cs`, `Css/CssStylesheets.cs`, `TextLayout.cs`, `UnicodePdfSerializer.cs`; new `tests/FactsPDF.Tests/FontStyleRoleTests.cs` and existing parser/font tests when old rejection expectations are superseded.

**Interfaces:** Public default-empty `BoldFonts`, `ItalicFonts`, `BoldItalicFonts` on PdfOptions; internal role selection from computed weight/style; existing conversion/font loader signatures unchanged. Missing requested styled chain FPDF1507; missing glyph FPDF1504 identifies role.

- [x] Add API-availability/default tests and observe missing-property assertions fail against current API, then add the three empty properties.
- [x] Add runtime tests using synthetic fonts with distinct names/advances/glyph coverage: style tags/CSS combinations, all roles/cache, no-pairing and no-cross-role fallback, Courier+embedded, struts/source errors, eight/nine entries, total bytes, same-object reuse, nulls, concurrent isolation, old paths. Observe unsupported HTML/CSS/wrong metrics/errors before production changes.
- [x] Implement role snapshot/validation, independent weight/style cascade and inline defaults, role-aware fallback/metrics/cache, and minimal mixed Courier serialization. No unrelated font parser or layout rewrite.
- [x] Run `dotnet test FactsPDF.slnx -c Release` with local SDK10 env; all tests must pass, and old rejection cases become new positive/missing-role tests rather than being dropped.
- [x] Commit the core change with red/green logs and task completion in the ledger.

### Task 2: CLI and private WASM adapters

**Files:** `src/FactsPDF.Cli/CliApplication.cs`; `probes/browser-wasm/Program.cs`, `ProbeConversion.cs`, `wwwroot/main.js`, `wwwroot/resources.mjs`; CLI/Wasm tests and probe Node tests.

**Interfaces:** CLI repeated `--font-bold`, `--font-italic`, `--font-bold-italic`. Preserve `--font`/existing exports. New private role export accepts explicit ordered resource data and returns existing PDF/core-error result shape; invalid role config has private B2_INPUT_FONTS, no public SDK contract.

- [x] Add failing CLI tests for all flags, ordered mixed-family fallback, resource/output-path protection, unknown/missing arguments and sentinels; add private adapter tests preserving old export semantics and no stale output across success/failure/recovery.
- [x] Extend explicit-resource loading and map to Task 1 properties without discovery/inference. Keep missing/empty HTTP resource errors separate from missing core role errors.
- [x] Run full .NET and probe Node suites; build/publish actual WASM Release host. Persist logs; commit.

### Task 3: Real evidence and current target status

**Files:** Extend existing `scripts/wasm_probe` fixtures/runner and workflow, add focused `scripts/font_style_*` evidence helper only where needed; `examples/` sample HTML; font/user docs and `docs/acceptance/support-matrix.md`; acceptance generator/tests adjusted only when actual T-bold proof exists.

**Interfaces:** Explicit role-font manifest records source version/hash/order, same inputs in CLI/.NET/WASM, real PDF/PNG/text/bbox/font validation evidence. Existing B2 behavior remains a regression path, not a repeated feasibility investigation.

- [x] Obtain pinned upstream static four-style glyf fonts and license/provenance; verify formats/embedding flags/total bytes. Do not commit standalone fonts or disguise generated/slanted normal glyphs as style evidence.
- [x] Add actual bilingual four-style fixture, wrapping/natural and explicit pagination, missing roles/glyphs/failed output/recovery. Pin expected text independently, record observed geometry and inspect all pages; no automatic user approval of reference pages.
- [x] Compare old regular-only output against baseline and full/subset normal/style outputs across CLI/.NET. Verify qpdf, ToUnicode, true font data/advances, no synthesis, exact expected text and raw rendered pages; save viewable artifacts and visually inspect.
- [x] Extend same-head protected browser CI and retain actual PDFs/PNGs on success/failure. Update documentation with measured capabilities and limitations, never claim untested platform AOT/variable fonts or nonexistent warnings.
- [x] Run full local .NET/Node/Python/site suites/build, actual WASM publish, source checks. Independent whole-branch review, fix important findings with regression tests, then create PR for normal review/CI.
- [ ] Ensure exact-head CI passes, reply/resolve all comments, ordinary expected-SHA merge, verify remote main tree and post-merge CI. Do not start the next feature stage.

## Execution record

Ledger and raw evidence: `/workspace/scratch/factspdf-c1-font-styles/`. Preserve them for handoff; user asks for reviewable evidence. Existing isolated worktree reused on `feat/explicit-font-style-roles`; prior PR27 artifacts remain intact. The user explicitly requested inline continued execution after independent design review, so no separate plan-approval wait is added.
