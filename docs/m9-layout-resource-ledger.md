# M9 execution ledger — layout guards and box resource evidence

Plan: `docs/superpowers/plans/2026-10-11-m9-layout-resources.md`.
Spec: `docs/m9-layout-resource-guards.md`.
Base: merged M8 `9b9ad5b04c3cc89a18f620b346589381b693695b`.
Sole feature PR: [#15](https://github.com/KoalaFacts/FactsPDF/pull/15).

## Rulings

- No distinct next-stage M9 feature definition was found in the current repository. This bounded stage closes the original box-model resource/performance evidence requirements rather than inventing more CSS support. Cost: feature expansion is deferred one stage.
- Execution uses an isolated remote GitHub branch and actual Actions builds because local cloning failed DNS and the local container has no .NET SDK. No local .NET success is claimed.
- Display commands are bounded at retention sites. Exact exhaustion is also checked before wrapping subsequent command-bearing paragraphs and before validating a closed block's definitely nonzero paint geometry. Eager parsing, wrapping while an allowance remains, zero-area records and the final PDF buffer are not newly streaming or a total-memory sandbox.

## Task 1 — incremental display accounting

1. `8265464ffa254288d57d08ca0bc42203f6a0e94e` adds twelve NUnit guard tests before implementation. [RED CI 38062385626](https://github.com/KoalaFacts/FactsPDF/actions/runs/38062385626), Ubuntu job `114243104352`: **472 tests, 465 passed, 7 failed**. Failures exposed missing direct-layout limits and downstream glyph errors masking an earlier display-budget excess; existing M8 tests passed.
2. `69ead108c10fed6e1630c5f276fe8495c33faed6` adds a conversion-local subtraction-based counter and charges text/paint before retaining them. The converter's defensive final check remains. Initial production diff was twelve added lines in `TextLayout.cs`, with no geometry or serializer changes; independent-review fixes below add pre-wrap and paint prevalidation checks.
3. [GREEN CI 38062546111](https://github.com/KoalaFacts/FactsPDF/actions/runs/38062546111) and existing font, stylesheet, CSS and real Chrome/PDF visual workflows passed at that exact implementation SHA. Final-head verification remains a separate gate.

## Task 2 — Native AOT evidence

1. Python report tests were written before the module. The initial local run failed because the module did not yet exist (scaffolding/import RED, not nine independent behavioral failures). After implementation all **9 Python tests passed**, locally and in Actions. They reject mismatched workload sets, input/PDF hashes, page counts, missing/invalid sample fields, JIT reports, NaN/Inf and incorrect medians; slower timings alone are not a correctness failure.
2. `b0a39ad0082eade3372b1f54b97ea632c092d56b` adds the same-harness workflow. [RED integration 38062822258](https://github.com/KoalaFacts/FactsPDF/actions/runs/38062822258), job `114244377889`, compiled two real Native AOT executables but exited **2** at measurement: the old benchmark required a font argument even for ASCII workloads. The nine Python checks passed before that failure.
3. `08b3e7ed5af1ba9db97d8da78dc50a2c24009d85` makes benchmark fonts optional and records input/PDF SHA-256 from an additional untimed proof conversion. Existing full/subset font invocation remains compatible. Hashing is outside the timed conversion samples.
4. [GREEN real comparison 38063096368](https://github.com/KoalaFacts/FactsPDF/actions/runs/38063096368) produced native results for plain (1 page), nested (1 page) and fragmented (6 pages). Downloaded artifact `11673473644` had archive SHA-256 `0f3bc2509d47bac9db67918e50d1f6b52f137b1da02fd45fa96441c38394f8cf`. Its source SHA matches `08b3e7ed...`; all input/PDF hashes, output byte counts and pages match pinned M8 and `errors` is empty. Raw data were revalidated after download.
5. In that initial run, per-conversion managed allocations matched M8: plain 209,328 bytes; nested 362,312 bytes; fragmented 1,067,712 bytes. Timing medians varied slightly in both directions. This does not establish a speedup or memory reduction for valid documents; M9's demonstrated behavioral improvement is earlier rejection of excessive retained commands.
6. `10cc53606e3d4131002fbe2968bc35e589b5ccec` only clarifies the report's peak-working-set scope: all process work up to the reading, including setup/warmup/samples/proof/hash work, not isolated conversion memory or a measurement of later reporting.

## Independent review — exhaustion before the next paragraph

1. Codex reviewed `be2208f5a1ca449a00c846078428a6ed6abf8aab` and found a P2: when the prior output uses the exact command allowance, the next paragraph was still resolved and wrapped before the retention-site charge. [Review thread](https://github.com/KoalaFacts/FactsPDF/pull/15#discussion_r4238139511).
2. Test-only `3088f95d9a57138bed1424328916602c2aad2012` added eight cases before the fix. [RED CI 38063712398](https://github.com/KoalaFacts/FactsPDF/actions/runs/38063712398), Ubuntu job `114246981888`: **480 total, 474 passed, 6 failed**. Invalid/missing glyphs and over-wide words raised later errors instead of the exhausted display limit. The already-exhausted 200,000-character paragraph allocated **40,110,776 bytes** during layout, exceeding the test's generous 256 KiB ceiling. Command-free trailing content and valid error behavior with remaining allowance already passed.
3. Fix `c2fcc2b057ea83e35692a38b132400c9dd05ade6` checks exact exhaustion before `Wrap`, using an allocation-free, cancellation-aware scan matching the existing HTML-whitespace and explicit-break rules. Non-whitespace text immediately charges the unavailable command and raises `FPDF1401`; empty, whitespace-only and break-only paragraphs retain their existing layout semantics. No scan is added while command allowance remains, and normal text still charges once at retention.
4. Documentation head `e60c96cb8ef218fcd7b2e7070193cad942363df3`: [CI 38064380431](https://github.com/KoalaFacts/FactsPDF/actions/runs/38064380431), Ubuntu job `114248943790`, confirmed **480/480 passed**, including the allocation ceiling. Windows/macOS/Ubuntu and real Linux Native AOT succeeded. [Same-harness evidence 38064380354](https://github.com/KoalaFacts/FactsPDF/actions/runs/38064380354), artifact `11675195296`, archive SHA-256 `534db02eb5648dc4c71d021f8ec6ad1c36ff6436dca5fac8a1f420252f094634`, was downloaded and independently revalidated: source matches `e60c96cb`, all three PDF hashes/bytes/pages match M8, and raw sample medians agree. Managed allocation medians remained unchanged; timings moved in both directions, not evidence of a general speedup.

## Independent review — paint geometry after exact exhaustion

1. Fresh Codex review on `e60c96cb` found a second P2: a later nonzero background/border block could raise a geometry error before the already-exhausted display limit. [Review thread](https://github.com/KoalaFacts/FactsPDF/pull/15#discussion_r4238186375).
2. Test-only `7e1c18c6546ba368d3d2f58900aec8e7ffa40f9e` adds eight cases covering background, left/top/bottom borders, paint-only predecessors, remaining allowance, zero-width background and zero-height paint. [RED CI 38064779544](https://github.com/KoalaFacts/FactsPDF/actions/runs/38064779544), Ubuntu job `114250103722`: **488 total, 483 passed, 5 failed**. All five failures were the reproduced `FPDF1302` instead of `FPDF1401`; remaining allowance and command-free shapes already passed.
3. Fix `5188354f5028e201a4b22728e6adbb29a382e495` performs a bounded prevalidation only when the allowance is exactly exhausted. It reuses `PaintedBox.CommandCount` for at most the first, last and one representative middle fragment, so clipped edges and zero-area paint keep the actual serializer's accounting. A positive cost throws immediately; zero-cost probes cannot double-charge. Normal in-budget geometry/retention logic remains unchanged. Exact-head CI and a fresh independent review are still required before merging.

## Final gates

- Request fresh independent Codex review of the final head, including resource/error ordering, zero-area costs, exact thresholds, same-harness provenance and honest measurement interpretation.
- Verify all current-head NUnit/Native-AOT, existing visual/font/CSS workflows and new resource evidence plus CodeQL. Reproduce and fix substantive review findings before merge.
- User already authorizes merge after those gates, followed by actual merged-main verification. Update PR conversation with final exact SHA/results; do not start M10, publish packages or change licensing.
