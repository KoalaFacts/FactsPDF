# M9 — Layout resource guards and box-model performance evidence

## Scope and provenance

M8 was merged as `9b9ad5b04c3cc89a18f620b346589381b693695b`. The user requested starting M9 and retains the established one-PR delivery sequence. No distinct M9 feature definition was present in the current roadmap. This is a deliberately bounded follow-up to the original box-model design's resource-limit and performance-evidence requirements, not a claim that the user previously selected a new CSS feature.

**Goal:** Stop retaining additional text/paint commands as soon as `MaxDisplayCommands` is exceeded, and record repeatable Native AOT conversion measurements against the exact M8 baseline. Do not expand HTML/CSS rendering support.

## Observed issue

`PdfConverter.Convert` currently checks display commands only after `TextLayout.Layout` returns every page and fragment. Although caller output remains protected, a low command limit does not bound construction of those lists. In particular, nested multi-page painted boxes can materialize many commands before the limit is consulted.

## Required behavior

- Charge each placed text run and every nonzero rectangle in a `PaintedBox` before retaining that run or fragment in the page lists. Use the existing `PaintedBox.CommandCount`, including first/last clipping and zero-area behavior.
- Enforce a single per-conversion aggregate across pages, nested boxes and both layout entry paths. The exact limit succeeds; the first command that would exceed it throws `FPDF1401` before it is retained.
- Preserve the existing final converter check as a defensive invariant. No public API/default changes and no global mutable counters.
- Invalid later layout input must not be visited after an already-reached command limit. HTML/CSS parsing still precedes layout; do not claim it becomes lazy or is bypassed.
- Preserve PDF bytes for inputs within the budget, including ASCII, Unicode, single-page boxes and first/middle/last fragments. Existing cancellation, page limits and fail-before-caller-write remain.
- This is NOT full streaming or a total-memory sandbox. HTML trees, glyph wrapping for the current paragraph, font storage, zero-area records and the final PDF buffer retain their existing separate limits and allocation behavior.

## Measurement contract

Reuse the existing small Native AOT benchmark harness, allowing its existing ASCII/Courier mode with no font argument. Preserve the current full/subset font workflows. Add untimed input/output hashes so a same-harness baseline comparison can require exact inputs, PDF bytes and page counts.

Run the identical current harness against both the fixed M8 commit and this branch on one Linux runner, using unchanged text, nested single-page panels and a multi-page decorated workload. Report raw timings, current-thread managed allocated bytes, process-lifetime peak working set, runtime/architecture and source revisions. No timing threshold on shared CI; no superiority, cold-start, native-allocation or total-memory claim. Hash equality is a correctness gate, not a performance result.

## Explicit exclusions

No tables/images, new CSS properties, pagination redesign, pooling, new rendering dependencies, public distribution, package publication or licensing changes. M10 must not start before this PR is reviewed, green, merged and verified on main.

## Delivery record

- Baseline PR #14 is merged; main matches the M8 SHA above; no open PR existed when M9 began.
- Execution uses the GitHub connector and its isolated feature branch. Local clone was attempted but DNS for github.com is unavailable, and no local .NET SDK is installed. Test/build evidence must therefore come from actual GitHub Actions runs, not claimed local execution.
- Implementation plan: `docs/superpowers/plans/2026-10-11-m9-layout-resources.md`.
