# E2 whole-row pagination Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, then an independent whole-branch review.

**Goal:** Paginate only tables taller than an empty usable page, repeating the optional header and preserving whole rows.
**Architecture:** Keep E1 measurement and short-table placement. Add measured row ranges and a focused internal fragment planner; reuse text wrapping, display budgets and ancestor pagination.
**Tech Stack:** .NET 10, NUnit, native FactsPDF PDF writer, qpdf/Poppler.
**Spec:** ../specs/2026-10-11-first-usable-release-scope-design.md and ../specs/2026-10-18-fixed-column-table-e1-design.md, with the user's E2 instruction (short tables stay indivisible).

## Global constraints

No new public API, HTML/CSS syntax, dependencies, spanning/nesting, cell splitting/images, F work or database changes. No browser printing, sandbox changes or Library upload. Short-table output stays byte compatible. Errors leave destinations untouched. Required CI and review protection are not bypassed.

## Review focus

- Page tails cannot produce an orphan repeated header or lose a row.
- First/last table decoration and new/continued ancestor padding must remain bounded.
- Repeated header text/paint and table fragments must charge display commands.
- Zero-height/empty rows must progress without blank pages or infinite loops.
- Oversized row/header combinations retain FPDF1302 and source provenance.

## Task 1: native layout

Files: `Tables/TableLayout.cs`, `TextLayout.cs`, new internal `Tables/TablePagination.cs`, `TablePaginationTests.cs`; adjust the E1 whole-table-too-tall expectation now superseded by E2.
Interface: measured row metadata (height, source, ranges into retained text/paint), fragment row ranges and geometry; no public surface.
- [x] Add authored geometry tests for short-table movement, long tables with/without header, variable rows, page tails, decorations, empty rows and resource failures.
- [x] Run targeted tests RED against E1; record actual failures.
- [x] Implement minimal row measurement and placement, with cancellation and budget charging.
- [x] Run all .NET tests GREEN, then commit.

## Task 2: actual output evidence

Files: `scripts/verify_table_pagination.py`, E2 authored fixtures, `docs/tables.md`, support matrix and verification record; existing public API probe reused.
- [x] Generate 10/30/100-row and bilingual native PDFs through API, CLI and Native AOT.
- [x] Assert authored row order, repetitions, geometry, no missing/split rows, deterministic bytes and entry parity; reject mutated PDFs.
- [x] Inspect every distinct page raster and retain PDF/text/raster/hash reports.
- [x] Run E1 regression verification, Release build/tests and independent review. Fix findings with RED→GREEN.
- [ ] Commit, create PR, wait for normal CI and zero unresolved comments, merge under existing authorization and verify main. Stop at E2.

## Execution record

Baseline: remote main `194c6e2e56098022176403550bf66ebeda9e0142`; open PR list empty. Clean dedicated checkout on `feat/table-row-pagination` used in place (no second worktree necessary). AGENTS.md, checkout .agents skills, and prior 37-HTML/15-call E2 preparation are absent; no claim to have read them.

Ruling: use sliced table decorations (top only first fragment, bottom only last), matching existing block behavior. Reserve last-row bottom decoration; reject a header/row/required-decoration combination that cannot fit, without splitting cells. Cost if wrong: decoration behavior would require an explicitly different contract.

Task 1 evidence: baseline 917 pass; RED 9/12 fail on E1 height; GREEN full suite 930 pass. Source-derived test corrections: 13pt Courier ascent and quota diagnostics without source offsets. Native GCC publish succeeded.
Task 2 evidence: six cases / 16 pages, API/CLI/Native bytes identical; qpdf/Poppler geometry and mutation checks pass. E1 inspector passes 24 fixture entries and two examples; five short-table sources compare byte-identical with separately built original main.

Independent review: two P2 findings reproduced externally; 3 zero-row tests RED→GREEN and 2 split-decoration tests RED→GREEN. Final suite 935 pass. No deferred minors. Ruling: zero-height rows bind to adjacent occupied rows, so they cannot create header-only pages or detach bottom decoration; this enforces the no-orphan intent without giving empty rows visual space.

Delivery: engine commit `4a3869c`, PR #34 ready for review, normal exact-head CI pending. Clean-tree final native E2/E1 reruns pass. Three post-fix public-API reproductions produce nine inspected pages with exact page text and qpdf checks.
