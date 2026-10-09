# Font subsetting execution ledger

Plan: docs/superpowers/plans/2026-10-10-font-subsetting.md
Base: d191bcb7a6f8d79d88e1dac333c25136b0350d0f; isolated branch feat/font-subsetting; stacked draft PR #3.

## Evidence
- fe3eb3a baseline: run 37999173183, Ubuntu job 114052755061 actually compiled and ran 130 tests: 113 passed, 17 expected missing-subsetter/CLI failures. All original 111 tests passed.
- aefc3b3 implementation: run 37999664337 passed Windows/Linux/macOS test jobs and actual Linux x64 Native AOT/backward-compatibility/package inspection.
- 788b7f2 added independent equivalence checks and native measurement harness. Run 37999991799 passed; artifact 11649240045 was downloaded and its SHA-256 matched 781065d836363507fe5a6318766f039790d5ab86666e784dbe18b1c94ab779ce.
- That artifact's two-page PDF is 31,384 bytes (full mode with IDENTICAL complete fonts: 2,564,856 bytes). Independent text, glyph-outline/metric and 120 DPI pixel equality checks passed. PDF SHA-256 c6498cf1f9ec1c4b262c1f732676d85dc49e202c115ac5d4f69406fd421ec4b6. Both downloaded page previews were inspected visually; no clipping, replacement boxes or overlap observed in this fixture.
- Initial shared-runner baseline: fresh-process CLI median wall time 146.206346 ms full / 20.484576 ms subset; median peak RSS 38,772 / 28,604 KiB. Reused-font native harness median conversion 91.97935 / 1.9935 ms and 27,268,216 / 1,700,328 current-thread managed allocated bytes. These are observations of that exact run and small corpus, not guaranteed performance. Final-head checks/results must be read separately.

## Review
Self-review (not independent code review) identified one missing composite precondition: a first component cannot use parent-point attachment before parent points exist. A new regression test is added before the fix; additional tests exercise deep graphs, the format-4 size boundary, concurrent subset reuse and unchanged output after a resource-limit failure.

Full embedding remains default, opt-in SubsetFonts/--subset-fonts preserves backward compatibility. No-subsetting fonts automatically remain fully embedded; permission bits are never cleared. Fonts remain trusted inputs: outline bytecode, complete checksum validation of inputs and arbitrary hostile-font safety are not solved by this increment.

No runtime dependencies were added. No standalone fonts, binaries or packages were uploaded. Artifacts contain only public sample PDF, page images and measurements. Both pre-existing PRs and this PR remain draft/unmerged; license files are unchanged. The editing container lacks a .NET SDK and outbound DNS; all compiler/AOT evidence is actual GitHub Actions, not a claimed local build.
