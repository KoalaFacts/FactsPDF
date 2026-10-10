# M9 Layout Resources Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans in the current session. Steps use checkboxes; GitHub Actions supplies actual .NET verification in this environment.

**Goal:** Enforce the existing display-command limit during layout and record same-harness Native AOT box-workload evidence against M8.

**Architecture:** A conversion-local counter in TextLayout charges the existing exact command cost before either page collection is extended. Keep the converter's final defensive check. Reuse the separate benchmark executable and compare identical inputs/harness against two pinned engine revisions.

**Tech Stack:** C#/.NET 10, NUnit, Native AOT, Python standard library, existing GitHub Actions.

**Spec:** `docs/m9-layout-resource-guards.md`.

## Global constraints

No new public rendering API, CSS semantics, runtime dependency, license or release. Exact-budget and zero-area behavior must remain. No M10 before merge and post-merge verification. Measurements on shared CI are preliminary, not certified performance.

## Review focus

Budget must aggregate across pages; zero-area/clipped borders must match serializer counts; unrelated downstream layout errors must not mask an already-exceeded budget; counters must not leak across conversions; baseline reports must identify the exact engine revisions and the same input/harness.

## Task 1 — Incremental layout accounting

Files: create `tests/FactsPDF.Tests/LayoutResourceGuardTests.cs`; modify `src/FactsPDF/TextLayout.cs` only for the counter and the two retention sites.

- [ ] Add direct-layout tests that fail on M8: text-run limit; rectangle limit; cross-page aggregate; and early rejection before unsupported downstream text. Add passing controls for exact limit, zero-area boxes, no partial caller output and independent conversions.
- [ ] Run `dotnet test FactsPDF.slnx -c Release` through the existing CI and record actual RED failures.
- [ ] Add a local `ChargeDisplayCommands(int count, int sourceOffset = -1)` helper with subtraction-based overflow-safe checking. Invoke immediately before retaining a PaintedBox and before retaining each PlacedText run.
- [ ] Re-run the full suite, existing Native AOT and visual oracles. Record exact commit/run IDs; do not relax old tests.

## Task 2 — Same-harness performance baseline

Files: modify `benchmarks/FactsPDF.Benchmarks/Program.cs`; create `.github/workflows/box-layout-resources.yml`, `scripts/box_layout_evidence.py` and `scripts/test_box_layout_evidence.py`.

- [ ] Write validator/workload tests first and observe failure before implementation.
- [ ] Extend existing benchmark CLI so two arguments (mode and input) enable ASCII, retaining font arguments when supplied. Add SHA-256 input and PDF hashes outside timed samples.
- [ ] Implement deterministic plain/nested/fragmented workloads and report validation. Reject missing/mismatched scenarios, source revisions, hashes, page counts, AOT flags, invalid or non-finite measurements and unexpected sample counts.
- [ ] Compile the identical current harness against the pinned M8 engine and the PR engine, using fresh processes per workload on one Linux runner. Require correctness parity and retain raw measurements without a timing pass/fail threshold.
- [ ] Run Python tests, full .NET/Native AOT/visual workflows and actual comparison on the exact final head.

## Task 3 — Review and integration

- [ ] Request independent Codex review of the exact final head against M8; fix substantive findings with RED/GREEN evidence.
- [ ] Verify the current head still matches all successful checks and review. Merge this single PR using expected-head protection.
- [ ] Verify main points to the actual merge commit and its triggered workflows pass; no subsequent milestone or publication.

## Rulings

M9 has no previously approved standalone feature specification in the repository; this scope closes existing resource/performance requirements instead of inventing new rendering capabilities. The cost is deferring feature expansion one stage. Remote branch/Actions execution replaces a local worktree because local DNS/SDK are unavailable; no local success will be claimed.
