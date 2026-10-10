# M9 execution ledger — layout guards and box resource evidence

Plan: `docs/superpowers/plans/2026-10-11-m9-layout-resources.md`.
Spec: `docs/m9-layout-resource-guards.md`.
Base: merged M8 `9b9ad5b04c3cc89a18f620b346589381b693695b`.
Sole feature PR: [#15](https://github.com/KoalaFacts/FactsPDF/pull/15).

## Rulings

- No distinct next-stage M9 feature definition was found in the current repository. This bounded stage closes the original box-model resource/performance evidence requirements rather than inventing more CSS support. Cost: feature expansion is deferred one stage.
- Execution uses an isolated remote GitHub branch and actual Actions builds because local cloning failed DNS and the local container has no .NET SDK. No local .NET success is claimed.
- Display commands are bounded at retention sites and exhausted budgets are checked before wrapping subsequent command-bearing paragraphs. Eager parsing, wrapping while an allowance remains, zero-area records and the final PDF buffer are not newly streaming or a total-memory sandbox.

## Task 1 — incremental display accounting

1. `8265464ffa254288d57d08ca0bc42203f6a0e94e` adds twelve NUnit guard tests before implementation. [RED CI 38062385626](https://github.com/KoalaFacts/FactsPDF/actions/runs/38062385626), Ubuntu job `114243104352`: **472 tests, 465 passed, 7 failed**. Failures exposed missing direct-layout limits and downstream glyph errors masking an earlier display-budget excess; existing M8 tests passed.
2. `69ead108c10fed6e1630c5f276fe8495c33faed6` adds a conversion-local subtraction-based counter and charges text/paint before retaining them. The converter's defensive final check remains. Initial production diff was twelve added lines in `TextLayout.cs`, with no geometry or serializer changes; the independent-review fix below adds a pre-wrap check.
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
4. [Implementation verification 38064311397](https://github.com/KoalaFacts/FactsPDF/actions/runs/38064311397) is the real CI for that fix. Its actual result and the later final documentation head must be checked before claiming the change is green. Re-request independent review on the final exact head; do not treat an older review as approval of this fix.

## Final gates

- Request fresh independent Codex review of the final head, including resource/error ordering, zero-area costs, exact thresholds, same-harness provenance and honest measurement interpretation.
- Verify all current-head NUnit/Native-AOT, existing visual/font/CSS workflows and new resource evidence plus CodeQL. Reproduce and fix substantive review findings before merge.
- User already authorizes merge after those gates, followed by actual merged-main verification. Update PR conversation with final exact SHA/results; do not start M10, publish packages or change licensing.
