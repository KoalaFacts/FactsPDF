# CSS stylesheet development ledger

Plan: docs/superpowers/plans/2026-10-10-css-stylesheets.md
Base: 16b55d46ba3022f0fb9de91cb6e47f28b4edba4f; stacked draft branch feat/css-stylesheets, PR #4.

## Observed test-first baseline

Commit 2f2c062701495632a74a303f3048e69d035660a5 compiled in CI run 38004198616. Ubuntu job 114069103998 ran 210 tests: 139 passed and 71 failed. The original 137 remained green; the new failures demonstrate absent style-element/cascade/budget support before implementation. The two already-passing new cases cover pre-cancellation and external-link rejection.

## Implementation decisions

Compile embedded rules and inline declarations before the existing streaming paragraph pass; this lets later style blocks apply to earlier content without exposing a new markup format or public DOM. Keep actual ancestry only, excluding the synthetic root. Candidate indices use rightmost ID/class/type; mixed descendant/child matching retains all eligible ancestors using bounded dynamic programming.

Resolve declarations per property using importance, inline status, lexicographic specificity and source order. Inherited values are parent computed values without inherited priority. Reuse existing property-value support and renderer defaults; this does not expand the box/layout model.

Strict behavior is intentional: unsupported/malformed CSS fails even in unmatched rules. Explicitly document the difference from browser error recovery. No network resources, imports or dynamic code are introduced.

Two earlier tests require update after this increment: rejection of all style elements and rejection of !important are obsolete now that those features are implemented. Replace those exact negative cases with positive assertions; do not weaken unrelated tests.

The editing container has no .NET SDK or DNS access. Compilation and Native AOT evidence must come from actual GitHub Actions. No passing implementation claim is made before inspecting its CI. No licenses changed, no merge, no package release.
