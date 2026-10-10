# M12 — direct placed-text string construction

PR #18. Base: M11 merged and main-verified `0ed8ede740bdffaccc23fc52038f8b40930a8ca0`. M11's actual merged CI passed 505 tests, Windows/macOS/Linux Native AOT and CodeQL before this branch was created. The user's sequential authorization covers review, merge, main verification and then the next bounded milestone; never concurrent feature PRs.

## Bounded implementation

Only the grouped text-run construction inside `TextLayout.LayoutCore` changes. M11 still yields a borrowed line buffer, and the consumer still must produce an independent immutable string before calling `MoveNext` again. Instead of allocating a StringBuilder and temporary character arrays for every placed run, M12 counts UTF-16 units while summing advances in exactly the original order, then uses `string.Create` to fill the final string synchronously. A static callback writes each validated scalar using `Rune.EncodeToUtf16`; supplementary characters no longer allocate individual surrogate-pair strings through `char.ConvertFromUtf32`.

The callback does not escape or retain borrowed glyph storage. Style/font grouping, text order, widths, baselines, command accounting and all subsequent PDF serialization remain unchanged. Length accumulation is checked; cancellation is checked during both counting and filling at bounded glyph intervals. Malformed UTF-16 still fails in the existing earlier validation stage before placement.

This is an allocation optimization, not a whole-document streaming claim. Final strings, pages, paint records, input, font/glyph caches and the final PDF bytes still occupy memory. CPU traversal and allocation are different measures; no general timing or peak-RSS improvement is claimed from a managed-allocation regression alone.

## Evidence

- Test-first `e8c28996d4ddbac8089435b0806b3eba732495b6`: [actual RED CI 38068987590](https://github.com/KoalaFacts/FactsPDF/actions/runs/38068987590), Ubuntu job 114262353396: **515 tests, 514 passed, exactly one allocation failure**. Long-paragraph layout allocated **2,885,768 bytes**, above the new 2 MiB target, while retaining all 100,000 letters. BMP/supplementary/NBSP tests, font/style separation, independent earlier-line strings, actual supplementary ToUnicode mapping and fail-before-write controls all passed on the old implementation.
- Implementation `36f7be3a3512a58758c93429e34aceca97894a37` changes only the grouped text construction. The exact diff is one hunk in `TextLayout.cs`; existing wrapper, serializer and CSS behavior were not changed. Final-head CI and independent-review results must be recorded in the PR before merge; no unobserved GREEN result is claimed here.
- Required independent evidence is unchanged: full NUnit Windows/macOS/Linux, actual Linux x64 Native AOT, all five Chrome/PDF box fixtures including the three-page slice, stylesheet and Unicode/font/subset suites, and the same-harness resource workflow. The latter remains pinned to M8, not a silently relabelled M11 baseline.

## Merge gate

Independent Codex review must target the final exact head, with no unresolved substantive findings; all relevant jobs must succeed. Merge only that reviewed head, verify actual main and only then start any subsequent PR. Do not publish packages, change licenses or introduce new runtime dependencies.
