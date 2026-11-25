# Harness memory — FactsPDF

Why each change to `advance-factspdf`, `verify-factspdf`, the gate or a friction-removing script
was made, and whether it worked (agent-harness autonomous-dev §8.1). Read it whole every run.

## RC-1 Runs end early instead of using their budget
Status: open
Inherited: agent-harness RC-1 — 10 of 12 active tasks (factspdf at 23 minutes) ended under 40 minutes on 2026-11-25, giving "not enough budget for the next item" or "hosted CI still running"; firm progress rule 2 now names both as non-stops (agent-harness A3).
- A1 · PR #366 · 2026-11-25 · claude-opus-5-5
  Change: none to the rules; this run sized each R5 item as one element with an exact oracle (`div`, `ul`, literal text in a `span`) so the full gate (build, 5000+ tests, 426-run parity, site + Playwright) fits three times in one run, and recorded the pattern here. Why: the inherited observation; this run merged #363, #364 and #365 inside the budget. Predict: the next 3 factspdf runs each merge at least two user-visible PRs and none ends under 40 minutes without a quoted stop reason in `get_overview`.
  Observed: pending (check 2026-11-27)

## RC-2 Lessons stay in the repo that learned them, and predictions go unchecked
Status: open
Inherited: agent-harness RC-6 — the tracker now reads each repo's memory and flags passed check dates; a repo without one starts without other tasks' lessons.
- A1 · PR #366 · 2026-11-25 · claude-opus-5-5
  Change: this file created (factspdf had no harness memory). Why: §8.1 requires one, and `get_overview` cannot check factspdf's predictions without it. Predict: `list_root_causes` lists factspdf within a day of the tracker's next read, and the RC-1 A1 check on 2026-11-27 records an outcome rather than staying pending.
  Observed: pending (check 2026-11-27)

## Notes for the next attempt
- A Can I Render row that reuses only strings already earlier in the dataset (e.g. `html.aside`'s for a div-like element) leaves the ordered source text unchanged, so no new English layer is needed; new strings need a chained `audit-english-*.mjs` layer. Check with `collectAuditStrings` before and after (#363, #364).
- Pins to update when the rejected count moves: `site/scripts/validate.mjs` SHA, the `counts(...)` lines in `site/tests/model.test.mjs`, and the `FPDF1102` record/page counts in `site/tests/startup.test.mjs` and `site/tests/english.spec.js` (25 records per page).
