# PNG and Transparency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render real static PNG brand assets with transparency through the current API and CLI.

**Architecture:** Add a bounded PNG reader beside JPEG. Extend immutable image
resources and the shared writer with compressed color planes and optional soft
masks. Keep layout and both serializer paths.

**Tech Stack:** C#/.NET 10, BCL zlib, NUnit, independent Python PDF/pixel inspection.

**Spec:** `docs/superpowers/specs/2026-10-24-png-transparency-design.md`.

## Global Constraints

- No third-party runtime package; retain the shared core and current image APIs.
- Preserve JPEG/default output and failure-before-output behavior.
- `PdfImage.ByteLength` continues to mean original encoded input bytes.
- Support static non-interlaced grayscale/indexed 1/2/4/8, RGB/gray-alpha/RGBA 8.
- Reject 16-bit, Adam7 and APNG explicitly; preserve alpha/tRNS with soft masks.
- Honor existing resource/output bounds and cancellation; no peak-memory claim.
- One implementation PR at a time; no publication or license change.

## Review Focus

1. Chunk lengths, CRCs, palette/tRNS and compressed stream boundaries.
2. Packed low-bit samples and all filters, including odd widths.
3. Unassociated RGB and alpha compositing over colored backgrounds.
4. Mixed/repeated PNG/JPEG images, Unicode output and xref numbering.
5. Malformed/oversized input, output budget preservation and recovery.

---

### Task 1: Bounded PNG loading and transparent PDF emission

**Files:**
- Create `src/FactsPDF/Images/PngReader.cs` and focused helpers if required.
- Modify `src/FactsPDF/Images/PdfImage.cs`, `PdfImageWriter.cs`,
  `src/FactsPDF/PdfSerializer.cs`, `UnicodePdfSerializer.cs`.
- Add `tests/FactsPDF.Tests/Png*Tests.cs` and fixture helper; narrowly adjust
  old signature-only PNG rejection in `JpegResourceTests.cs`.

**Interfaces:**
- Consumes existing `LoadImage` overloads, `PdfImageLimits`, `ImageResources`.
- Produces PNG resources through the same API, correct main/mask PDF objects in
  both writers. No public API addition is required. Internal reader result owns
  compressed color/alpha planes and dimensions/components. Keep original encoded
  byte length separately if storage changes.

- [ ] Write authored raw-pixel/chunk cases with literal expected color/alpha,
  all five filters, supported color/depth combinations and tRNS.
- [ ] Run focused tests against unmodified production code; record expected
  unsupported-PNG failures before implementation.
- [ ] Implement chunk parsing, bounded inflation/unfiltering, sample expansion,
  compressed planes, signature dispatch and PDF soft-mask numbering.
- [ ] Verify malformed CRC/order/palette/index/zlib/filter and truncated/excess
  decoded data; exact byte bounds, ownership and pre-output preservation.
- [ ] Verify CLI/private-adapter parity, mixed/reused images and Unicode output.
- [ ] Run focused tests then `dotnet test FactsPDF.slnx -c Release`; self-review
  and commit only task files with actual red/green evidence recorded.

### Task 2: Independent business example and delivery documentation

**Files:**
- Create a deterministic PNG fixture/example generator and independent PDF/pixel
  verification script in `scripts/`.
- Create `examples/png-brand-report.html` and permitted synthetic image assets
  or a documented deterministic generation command.
- Update `docs/images.md`, `docs/evaluation.md`, `README.md` and current-scope
  statements that still reject PNG. Extend an appropriate existing CI gate.

**Interfaces:**
- Consumes Task 1's same CLI/API image binding.
- Produces inspectable PDFs/page previews and transparent-logo report.

- [ ] Define independent expected pixels for RGB/gray/indexed/RGBA and partial
  alpha over a colored background; generate PDFs using the real CLI.
- [ ] Inspect decoded color/mask bytes and rasterized pixels, reuse and text.
- [ ] Provide a synthetic report using actual PNG resources and supported layout;
  retain truthful image-placement boundaries and no third-party font bundling.
- [ ] Update input/limit contracts and runnable commands; record actual results.
- [ ] Run affected checks, independent review, one PR and exact-head CI; merge
  and verify main before the next implementation increment.

## Follow-up

After PNG merges and main is verified, implement controlled image/text composition
using the existing HTML/CSS model, followed by the business-table/page-furniture
priorities already discussed. These are not delivered merely by supporting PNG.
