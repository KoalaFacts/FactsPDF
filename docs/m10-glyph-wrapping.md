# M10 — remove paragraph-wide glyph scratch

PR: [#16](https://github.com/KoalaFacts/FactsPDF/pull/16).
Base: merged M9 `308e8b3eee6dbfb91105883f3d9cbc8a716661ac`.

M9 was merged and its actual main passed 488 NUnit tests, Native AOT and all six push workflows before this branch was created. The user requested sequential continuation to M10. No separate M10 feature definition was found; this bounded allocation improvement was selected and communicated rather than inventing more CSS support.

## Scope and design

Only the private `TextLayout.Wrap` implementation changes. Previously it accumulated a paragraph-wide `List<Glyph>` and then copied those glyphs into retained line lists. M10 instead uses a replayable glyph iterator and one reusable unbreakable-word buffer. Completed lines, page text and paint records still use the existing representation. `LayoutCore`, pagination, font fallback, PDF serializers, public APIs and M9 display-budget guards are unchanged.

A validation pass enumerates all collapsed glyphs without retaining them, preserving the established ordering in which a late missing glyph or invalid UTF-16 error precedes an earlier width error. The layout pass replays the same input with the existing per-conversion glyph-resolution cache and builds lines with a one-glyph lookahead. This deliberately exchanges additional traversal for less allocation; no general speedup is claimed.

HTML whitespace is collapsed across runs with its first whitespace run's style. A pending space is resolved at the original point, even if a later explicit break or paragraph end drops it, preserving font diagnostics. Leading/trailing spaces and repeated explicit breaks keep their original behavior. The iterator and word loop check cancellation. Word widths and break opportunities use the existing `CanBreak`, glyph advance and line-metric rules.

Scratch storage is proportional to the largest unbreakable segment encountered, not guaranteed constant: pathological zero-advance text can make that segment large. The existing input/font limits remain relevant. The glyph cache can also grow with distinct scalars. This is **not** full streaming, a total-memory sandbox, a bound on peak process RSS, or elimination of retained paragraph lines/pages/final PDF bytes.

## Test-first execution ledger

1. `01665f777ff28fe6b02714c3e44d7ea32be484b4` added tests before production implementation. [Initial RED CI 38066052178](https://github.com/KoalaFacts/FactsPDF/actions/runs/38066052178): 495 tests, 493 passed, two failures. One is the desired allocation regression; the other was test scaffolding: an unpaired surrogate in attribute string metadata was not the intended runtime input.
2. Test-only `765a14686d0e17c90e7011ea3d0a9d1220271044` constructs high/low unpaired surrogates from integer test parameters at runtime. [Clean RED CI 38066192785](https://github.com/KoalaFacts/FactsPDF/actions/runs/38066192785): **496 tests, 495 passed, exactly one allocation failure**. The valid 200,000-character paragraph retained all 100,000 letters but allocated **42,995,272 bytes**, above the 32 MiB test bound. Diagnostic/whitespace/style controls passed on the original engine.
3. Implementation `0c0023c5acc9c80e9971b92f97eb12fc72b86236`: [GREEN CI 38066402652](https://github.com/KoalaFacts/FactsPDF/actions/runs/38066402652), **496/496 NUnit**, Windows/macOS/Ubuntu and Linux x64 Native AOT passed. The same long-paragraph test allocated **26,212,648 bytes**, retaining all text: about 39% fewer current-thread managed allocation bytes for this particular layout workload. This is not a peak-live-memory or timing result. Exact final-head verification remains required after documentation changes.
4. An additional local algorithm probe compared old versus proposed whitespace/run-boundary glyph sequences and resolution order in 20,000 deterministic generated cases. All matched. That probe is not a C# build or PDF verification; actual .NET testing is performed by Actions because the local container lacks the SDK and repository cloning failed DNS.

## Acceptance and merge gate

Require all current-head cross-platform NUnit/Native-AOT, existing CSS/font/subsetting/stylesheet and real Chrome/PDF visual workflows. The inherited same-harness resource workflow compares three actual Native-AOT PDF workloads (plain, 12-level nested, six-page fragmented) against its unchanged pinned M8 baseline; it must preserve hashes, byte counts and pages. Do not relabel that benchmark as a new M9 baseline. Inspect measured allocation and timing data separately and report limitations honestly.

Independent review must check whitespace, validation order, supplementary/CJK/font behavior, iterator disposal, zero-width words, resource guards and output compatibility. Fix substantive findings with reproducible tests, then merge the exact reviewed head and verify actual main. No M11, package publication, dependency or license changes in this stage.
