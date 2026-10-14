# B2 browser WASM probe implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. The continuing user instruction explicitly authorizes implementation; retain an unmerged branch until actual browser execution is evidenced.

**Goal:** Compile the existing FactsPDF core for browser WASM and test real PDF generation with explicit resources, independently of whether the local browser can start.

**Architecture:** A private test host references the existing core. A small JS-exported adapter accepts HTML and ordered font bytes and returns PDF bytes or existing diagnostics. A local static server and browser runner compare actual WASM results with the CLI and record provenance and measurements.

**Tech stack:** .NET 10 WebAssembly SDK, single-threaded non-AOT runtime, Node 22+, pinned Playwright, qpdf and Poppler.

**Spec:** [Accepted scope, section 4](../specs/2026-10-11-first-usable-release-scope-design.md). The 2026-10-14 continuation explicitly requests compilation and a minimum host even if protected local Chromium fails, plus a protected remote CI path.

## Global constraints

- Reuse `PdfConverter.Convert` and `PdfFont.LoadTrueType`; no engine feature or public library API changes.
- Browser is only the WASM host; no DOM/print layout, server PDF generation or substitute engine.
- Explicit UTF-8 HTML and ordered font resources; missing resources and unsupported inputs fail without PDF bytes.
- Sandbox stays enabled; no permission changes, insecure launch fallback, or warning bypass.
- Record actual build/load/first/subsequent/memory observations with definitions; no invented budget or AOT claim.
- Do not merge or claim B2 acceptance before real browser execution evidence. No public package/site release or Library upload.

## Review focus

- Missing or corrupt fonts must not silently fall back or return a previous PDF.
- A failed conversion must not poison a subsequent conversion in the same runtime.
- Invalid UTF-8, empty resources and unsupported HTML must keep distinct error boundaries.
- CI or automation must not silently disable Chromium sandboxing.
- Reported startup, resource size and memory numbers must reflect their real measurement scope, not peak process memory.

## Task 1: Compilable core adapter

Files: `probes/browser-wasm/FactsPDF.WasmProbe.csproj`, `ProbeConversion.cs`, `Program.cs`; `tests/FactsPDF.Tests/WasmProbeTests.cs` and its project link.

Interface: `ProbeConversion.Run(string html, byte[] latinFont, byte[] cjkFont, bool subsetFonts)` returns a serializable result with PDF base64, page count, diagnostic and managed heap snapshots. `Program.Convert` exposes it through `[JSExport]`. Empty font arrays mean the existing ASCII/Courier mode.

- [x] Write tests for ASCII PDF bytes equal to direct core, explicit-font requirement, invalid font, unsupported HTML, and failure followed by success.
- [x] Run tests against an unimplemented adapter and observe expected failures.
- [x] Implement the wrapper using the existing core; run the full .NET suite.
- [x] Build/publish the real browser-wasm target with the official workload; retain effective build properties and logs.

## Task 2: Actual browser contract and independent inspection

Files: `probes/browser-wasm/wwwroot/{index.html,main.js}`, `scripts/wasm_probe/{package.json,package-lock.json,run.mjs,fixtures.mjs}`.

Interface: the browser exposes `window.factspdfProbe.convert(html, fontUrls, subsetFonts)` and `ready`; the runner serves only an explicit static asset set, executes cases, writes local PDFs and a JSON evidence report.

- [x] Write the browser tests/runner assertions before the host JS: ASCII, bilingual explicit fonts, multi-page layout, missing font, invalid UTF-8, unsupported HTML and sequential recovery.
- [x] Implement runtime initialization and explicit resource loading. Preserve ordered bytes and reject bad HTTP/UTF-8 before conversion.
- [ ] Compare each successful actual PDF with CLI bytes, qpdf, pdftotext, page counts, bounding boxes and raster output; record input/font/output hashes.
- [ ] Record asset bytes/requests, runtime-ready time, first and three subsequent conversion samples, managed heap snapshots and unavailable memory metrics explicitly.
- [x] Try existing official Playwright Chromium with `chromiumSandbox: true`; retain failure evidence if blocked and continue compilation/CI work.

## Task 3: Protected CI and reviewable handoff

Files: `.github/workflows/wasm-probe.yml`, `docs/browser-wasm-probe.md`.

- [x] Add a read-only, pinned-action CI job with sandbox explicitly enabled and no host-security adjustments. Install only official SDK/Playwright and distro verification tools.
- [x] Run applicable local checks and independent whole-branch review; fix substantial findings with regression tests.
- [ ] Commit and prepare a draft PR for normal CI if authorized tools permit; preserve logs and report actual local and remote outcomes separately.
- [ ] Keep the PR unmerged and B2 unaccepted until browser evidence exists; stop at B2.

Progress: local publish and managed/Node tests passed; both local protected browser launches failed. Node-hosted WASM diagnostics pass, but browser comparisons/measurements remain unchecked above until actual protected CI execution.
