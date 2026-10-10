# M11 — line-at-a-time wrapping

Base: merged and verified M10 `b8a667c7220222facad506feeb807f95b0da8143`. PR #17 is the only active feature PR. The user authorizes sequential milestones: tests, implementation, independent review, merge, verify main, then start the next milestone. No package or license changes.

## Scope

The private wrapper no longer retains all Line/Glyph arrays for an entire paragraph. `Wrap` yields a private value-type Line whose glyph list is borrowed until the next `MoveNext`. `LayoutCore` consumes it immediately and finishes constructing independent PlacedText strings before advancing. A single line buffer is cleared and reused rather than reallocated for each completed line. The word scratch buffer and conversion-wide glyph-resolution cache retain M10 behavior.

A non-retaining preflight pass resolves the whole paragraph and checks unbreakable segment widths. A recorded width violation is reported only after glyph validation completes. This preserves the existing priority: malformed/missing glyph diagnostics before width errors, and paragraph width errors before page/layout/command-limit errors. M9's already-exhausted display-budget short circuit remains before wrapping. Whitespace ownership, explicit blank lines, line metrics, font fallback, paragraph margins and box fragmentation are unchanged. Enumerator disposal is deterministic even on failure/continue.

This is NOT whole-document streaming or a constant-memory promise. Input, placed strings, pages, fragments, glyph cache and final PDF buffer are still retained. A pathological zero-advance segment/line can still be large. The preflight also means an oversized paragraph must still be scanned before its first line is placed; no CPU sandbox or general speedup claim is implied.

## Evidence sequence

- Test-only commits `dca6f4cd` and `d657b74e` add nine tests. The latter corrects the narrow test fixture width to 22pt (two 7.2pt Courier letters plus a 7.2pt space fit in 22pt, not 20pt).
- Actual RED CI [38068132585](https://github.com/KoalaFacts/FactsPDF/actions/runs/38068132585), Ubuntu job 114259862911: **505 tests, 504 passed, 1 allocation failure**. The original engine allocates **26,212,648 bytes** for the 200,000-character test versus its 8 MiB target. Diagnostic-order, blank-line and immutable-output controls all pass before implementation.
- Implementation `ab57b94197fba82cbcc21e8515d395677bc2b0a4` introduces reusable line storage and immediate consumption. Full final-head CI and independent-review evidence must be recorded in the PR before merge; this document does not claim unobserved GREEN results.
- A local Python algorithm-only probe compared 20,000 deterministic glyph-stream cases (mixed zero/nonzero advances, punctuation, whitespace, CJK and explicit breaks) and matched old/proposed line content and widths. It is not a .NET or PDF test. Real build/runtime verification uses GitHub Actions because this container has no .NET SDK and cannot resolve public GitHub DNS.

## Gates and sequential follow-on

Require full Windows/macOS/Linux NUnit, Linux x64 Native AOT, original CSS/font/subsetting/stylesheet suites, independent Chrome/PDF five-fixture comparison, and existing same-harness resource evidence. Inspect exact reviewed head, unresolved review threads and actual merged-main CI separately. A green code branch is not proof of a successful merge.

After this PR is merged and main verified, the next bounded allocation target is PlacedText string construction: eliminate intermediate StringBuilder/chars and per-supplementary-scalar string allocations while retaining independent strings and exact Unicode values. That work belongs to a separate M12 PR, not this one.
