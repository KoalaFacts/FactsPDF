---
name: advance-factspdf
description: How the scheduled task advances FactsPDF — goal, headline metric, gate, work order, scope and project rules. Read with agent-harness/skills/autonomous-dev at the start of every run.
---

# Advance FactsPDF

This skill is the repo-specific half of the scheduled development task; the run
loop is `agent-harness/skills/autonomous-dev`, and the owner's seed is
`agent-harness/projects/factspdf.md`. Where this file and the seed differ, the
stricter rule wins. The task keeps this file current through its own PRs. It may
make rules stricter, never looser, and never relaxes the hard limits in the
scheduled task's prompt.

## Goal and metric
- FactsPDF is a .NET 10 C# HTML-to-PDF engine: the fluent `Pdf.Write()` SDK, a
  CLI, Native AOT and browser-wasm probes, and the "Can I Render" compatibility
  site under `site/`.
- Goal: a **reliable MVP** — correct, byte-for-byte deterministic output; every
  untrusted input either renders or fails with `FactsPdfException`; bounded
  memory and time; an easy SDK; near-zero allocation on the hot path; docs that
  say exactly what is true.
- Headline metric: the ledger items done, plus per-phase allocations of the
  200-row table (`AllocationBudgetTests`, `GC.GetAllocatedBytesForCurrentThread`
  per phase), each with the commit it was measured on.
- Real progress: a merged PR that changes `src/` code with tests (or tests and
  scripts for a gate item), passing the gate below.

## Setup
- Read first: `AGENTS.md` (binding), `CLAUDE.md`, `docs/feature-notes.md`,
  `docs/development.md`, `docs/compatibility-site.md`, `docs/mvp-roadmap.md`.
- Schedule: raised tier (every 4 hours, per the agent-harness README), 50 minutes per run;
  size each item to merge within the run. Housekeeping is therefore due once per Sydney
  calendar day.
- .NET 10 SDK: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet`,
  then `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`.
- Node 24 for `site/` and the TypeScript scripts; the existing Python `scripts/`
  tooling (for example `png_fixtures.py`) stays usable.
- Fonts, images and environment traps: see `.claude/skills/verify-factspdf/SKILL.md`.

## Gate
1. Build every csproj (`git ls-files '*.csproj'`) except `examples/first-use` in
   Release with warnings as errors (CI also builds probes and acceptance
   projects outside the solution).
2. `dotnet test tests/FactsPDF.Tests -c Release` (includes the CLI tests).
3. Byte parity against `main`: `node scripts/parity.ts` (CLI from main in a git
   worktree vs the branch, every `examples/*.html`, three option sets:
   defaults; DejaVu TrueType with `--subset-fonts --page-numbers --header-text H
   --title T`, images from `python3 scripts/png_fixtures.py`, `--page-size letter
   --landscape --margin 20mm`; `--page-furniture examples/business-page-furniture.json
   --page-width 148mm --page-height 210mm` with a fully embedded font). Compare
   exit codes and bytes; any difference must be intended and explained in the
   PR (`--expect <kind>` locally, `Parity-Expect: <kind>` trailer for CI).
4. Docs: `node --test scripts/check-docs.test.ts && node scripts/check-docs.ts`.
5. Verification boundary: the CLI and `Pdf.Write()` rendering `examples/*.html`;
   rendered pages are rasterised and looked at for visible changes, and the
   feature map in `verify-factspdf` follows the areas of
   `site/data/compatibility.json`.
6. When `site/` data changes: `cd site && npm test && npm run validate`; Can I
   Render data changes need a new chained English translation layer
   (`docs/compatibility-site.md`) plus updated snapshot pins.

## Ledger
`docs/mvp-roadmap.md` (work-item table). Update it in every PR.

## Work order
After Z1 (#177): P0 TypeScript parity script (plus a CI job against the merge
base only if it fits an existing workflow without new permissions); Z2 content streams as ASCII bytes into pooled
buffers; Z3 `TextStyle` single private copy like `BoxStyle` (#176); Z4 CSS tokenizer/parser/adapter
allocations; Z5 layout-phase allocations; Z6 read-phase allocations; R1 soak and
limits (every `Max*` limit, cancellation on ToBytes, ToStream, ToStreamAsync,
ToBufferWriter, ToFile, ToFileAsync); R2 SDK usability (complete XML docs,
README quick start and examples compile, clear `FactsPdfException` messages with
source location for every FPDF code); R3 fixed-seed fuzzing of stylesheets and
page furniture (`RobustnessFuzzTests`), fixing every non-`FactsPdfException` crash; R4 packaging (`dotnet pack` metadata, README, no stray files,
documentation XML; `examples/first-use` against the packed package from a local
feed); R5 the most valuable gap found. The ledger's table is the current state.

Each Z item measures allocations per phase before and after (per-phase
`GC.GetAllocatedBytesForCurrentThread` and an `EventListener` on
`GCAllocationTick` by type), lowers `AllocationBudgetTests` budgets to about 15%
above the new measurement (never raises them), passes parity, and claims timing
only from the existing same-harness benchmark workflow (`box-layout-resources.yml`).

## Authority
- Branch prefixes `perf/`, `feat/`, `fix/`, `test/`, `docs/`, `ci/`; squash
  merge pinned to the head SHA.
- First line of every PR body: "Opened by the FactsPDF scheduled task."
- One PR at a time; exception: while a code PR waits only on CI, the next one
  may be built locally (not opened).
- Never dispatch `publish-nuget.yml` or any publish workflow. Merging deploys
  nothing beyond what the repository's own workflows already do on `main`.
- Never touch other people's PRs or branches.

## Project rules
- Never edit `LICENSE.md`, `LICENSING.md`, `COMMERCIAL.md` or
  `docs/license-history.md`; add no legal disclaimers. The copyright holder is
  Koala Facts Trading Pty Ltd.
- Extension methods follow `shared/ExtensionsToString.cs`.
- A new capability also updates `site/data/compatibility.json`,
  `docs/feature-notes.md` and the area's docs page (`AGENTS.md`).
- Extra merge conditions: parity passes (or every difference is an explained,
  declared kind) and no allocation budget was raised.

## Docs scope
- User-visible changes go only in the draft release notes
  `docs/releases/0.1.0-preview.1.md`, kept unreleased; no new release-notes file.
- The finished milestone and execution ledgers (`docs/*-ledger.md`, the m9–m12
  notes) are evidence records: they stay where they are, unchanged, and pages
  that cite them keep linking to them. Any other superseded note is folded into
  its owning page and deleted.
- `site/data/compatibility.json` and its translation layers are product data,
  not docs.
- Housekeeping checks `docs/`, `README.md` and public API doc comments against
  the code (removed APIs such as `PdfConverter`/`PdfOptions`, old option names,
  wrong test counts, stale "next step" sections). README gets no per-feature
  sections and the Can I Render link stays near the top.

## Report extras
- Allocation before/after and the parity result for each merged PR.
