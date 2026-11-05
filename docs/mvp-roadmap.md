# MVP roadmap

Goal: a reliable MVP. Output is correct and byte-for-byte deterministic; every
untrusted input either renders or fails with `FactsPdfException`; memory and
time stay bounded; the `Pdf.Write()` SDK is easy to use; the hot path allocates
close to nothing; and the docs say exactly what is true.

Every change keeps the output bytes of every example identical to `main`
(the parity check) unless the PR explains an intended difference. Allocation
budgets in `tests/FactsPDF.Tests/AllocationBudgetTests.cs` only go down.

## Work items

| Id | What | Why it matters | Status | Evidence |
| --- | --- | --- | --- | --- |
| Z0a | Shared `BoxStyle.Initial`; non-throwing CSS length parsing | Fewer allocations; no exceptions on the border hot path | done (#174) | quick-start 698 → 484 KiB |
| Z0b | Compress a fully embedded font program once per `PdfFont` | Full embedding recompressed the whole font every conversion | done (#175) | serialize 2216 → 1196 KiB (DejaVu Sans) |
| Z0c | One private `BoxStyle` copy per element | One full record copy per declaration | done (#176) | table 9,129 → 6,836 KiB; `BoxStyle` 2,479 → 285 KiB |
| Z1 | Pooled, cleared output buffer for both serializers | The growing `MemoryStream` allocated about twice the PDF size | done (#177) | full-embedding serialize 1196 → 100 KiB |
| P0 | Parity check as a TypeScript script under `scripts/`, run by CI if it fits an existing workflow | Makes the byte-parity gate repeatable by every contributor | blocked (#180 green and reviewed, stacked on #178; the scheduled run's merge is refused as "merge without review" in two runs; owner decision: approve or merge #178 then #180) | 390/390 runs identical in CI |
| Z2 | Page content streams written as ASCII bytes into pooled buffers | `StringBuilder` → `string` → `byte[]` copies per page | blocked (Z2a #181, Z2b #182, stacked on #180; same merge refusal; owner decision: merge after #180) | table serialize 1,009 → 77 KiB (Courier) |
| Z3 | `TextStyle`: at most one private copy per element, shared with the parent when unchanged | 202 KiB of `TextStyle` in the 200-row table | in progress (perf/text-style-builder) | 200-row table 6,562 → 6,380 KiB; quick-start 345 → 334 KiB; `TextStyle` no longer sampled; 402/402 runs identical |
| Z4 | CSS syntax allocations (`CssSyntaxToken[]`, strings, LINQ iterators) | Largest remaining parse-phase types | todo | |
| Z5 | Layout-phase allocations | About 2.4 MB in the 200-row table | todo | |
| R1 | Soak and limits: memory and time bounds for every `Max*` limit; cancellation on every output path | Bounded resources are an MVP promise | todo | |
| R2 | SDK usability: complete XML docs, README/examples compile, every FPDF code has a clear message with location | Ease of use | todo | |
| R3 | Fixed-seed fuzzing extended to stylesheets and page furniture | Untrusted input must never crash | todo | |
| R4 | Packaging: `dotnet pack` output checked; first-use example built from the packed package | What users install must work | todo | |

## Run log

- 2026-11-05: #174, #175 and #176 merged; each was byte-identical on 384
  example runs. Z1 opened.
