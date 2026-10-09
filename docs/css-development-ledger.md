# CSS stylesheet development ledger

Plan: docs/superpowers/plans/2026-10-10-css-stylesheets.md
Base: 16b55d46ba3022f0fb9de91cb6e47f28b4edba4f; stacked draft branch feat/css-stylesheets, PR #4.

## Observed execution

- 2f2c062701495632a74a303f3048e69d035660a5: CI 38004198616, Ubuntu job 114069103998 compiled and ran 210 tests, 139 passed / 71 failed. The original 137 stayed green; the failures demonstrate absent stylesheet/cascade/budget functionality before implementation.
- 9213b6017df838f856fd6eef8e089d4705e7c06f: CI 38005065260, Ubuntu job 114071839870 ran 210 tests, 208 passed / two failed. Every new stylesheet case passed. Only the two obsolete tests that demanded rejection of all style elements and !important failed because those features now worked. Existing Unicode/AOT output checks also passed.
- fd2f2d2b1f37bae1e9f0446acdaa261d6154aa8d: those exact obsolete cases were replaced by positive PDF assertions and thirteen review/boundary cases were added. CI 38005594197 passed the Windows/Linux/macOS suites and real Linux x64 Native AOT execution/package checks. Inspected Ubuntu job 114073527295 explicitly reports 223/223 passed.
- Stylesheet evidence run 38005594246 for fd2f2d2b passed native compilation and byte/text/pixel equivalence against the independent handwritten inline oracle. The downloaded artifact 11650604432 matched ZIP SHA-256 28206243b7f7d662a32e078c6dd006f9aa52db383bd74953de3a99351a40694b and contained only a public PDF, two page images and verification JSON. That initial two-page fixture was 35,798 bytes; PDF hash b3d6d3c0946b269e8edc321969fd035fac56e7d731e327904525023835d591f7. Both actual previews were viewed, with readable Chinese/Latin, correct style distinctions and no observed overlap/clipping. A short sentence in both input variants is subsequently shortened to avoid an isolated final word; this is fixture copy editing, not an untested engine change.

Final-head documentation/fixture checks must still be read separately before completion; earlier results must not be silently relabeled as results for a later commit. PR verification comments record the exact final head and artifact.

## Implementation and review decisions

Two bounded HTML scans compile all sources before computing styles, enabling late styles without retaining a complete DOM. Keep actual element ancestry and exclude the synthetic root. Rightmost ID/class/type indices reduce unrelated candidates; mixed descendant/child chains retain possible ancestors using bounded dynamic programming. The added index regression proves 2,000 unrelated class rules do not consume a small matching budget.

Resolve each property by importance, inline status, lexicographic specificity and source order. Inherited values have no inherited priority. Strict unsupported/malformed CSS errors are intentional even for unmatched rules; this is not browser recovery. Existing layout values/defaults are retained. No external resource fetching, new runtime dependency or dynamic code is introduced.

Self-review checked grouped matching specificity, non-greedy mixed combinators, sibling isolation, actual whitespace versus comments, RAWTEXT delimiter precedence, globals and budget charging. Added review tests all passed on fd2f2d2b; no independent code-review agent or human sign-off is claimed. Broader corpus/fuzzing, exact inline source mapping and resource profiling remain future work.

All six inherited licensing/contribution-policy files stay unchanged. README's licensing section is preserved. No merge, software registry publication, commercial activation or standalone font distribution occurs. The editing container has no .NET SDK/DNS access; compilation and AOT evidence comes from actual GitHub Actions, not a claimed local build.
