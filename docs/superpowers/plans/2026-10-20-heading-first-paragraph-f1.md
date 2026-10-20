# F1 execution and verification record

Baseline: E2 merged main `51f3e215eccda1f0bce558fca4a20ebeb1d970af`; no open PR or duplicate executor at start. No checkout AGENTS.md or .agents/skills directory was present. Scope follows the accepted first-usable-release F stage and the independently reviewed [F1 design](../specs/2026-10-20-heading-first-paragraph-f1-design.md).

Completed sequence:

1. Read accepted scope, E1 design and existing CSS/tree/layout paths. Baseline Release tests: 935 passed.
2. Independent design review before test/production edits. Incorporated diagnostic precedence/local budget, failed-chain scan suppression, list-context exclusion and accurate Wrap work bounds. No additional product approval gate.
3. RED: 25 focused tests failed on unsupported `avoid`; GREEN after minimal implementation. Four initial test-setup issues were corrected (missing explicit font role and table-only source-offset assertion), without changing production behavior for those failures.
4. Final coverage: 36 F1 cases; full Release suite 971 passed. Covers compatible bytes, real wrapping/fonts, whole-group move, overflow, explicit breaks, chains/empty paragraphs, decorations/ancestor top, exact limits, unchanged destinations, cancellation and bounded lookahead emission.
5. Independent code review found no required fixes. Reviewer separately passed the then-current 34 tests and 6,429 decoration combinations. Two subsequent tests add real wrapping and explicit font-role byte equality; The shared box helper additionally retains baseline left-coordinate addition order to preserve floating-point compatibility.
6. Built actual Linux Native AOT with GCC and direct public API host. `scripts/verify_heading_keep.py` rendered 8 authored cases / 16 pages through API, CLI and Native AOT; bytes agree. qpdf validity, Poppler per-page text/order/bounds pass; inspector rejects missing-title and displaced-title mutations. All 16 distinct pages were visually inspected, including decorated and bilingual output. Fits-here PDF equals its auto equivalent.
7. Compared default/no-avoid cases and existing E2 tables against a separately built original baseline CLI. Results and hashes are in the local artifact report; no baseline substitution or font files included in deliverable archives.
8. Added native F1 corpus to the existing normal CI workflow. Draft → ready → normal protected merge and post-merge checks remain the delivery gate; do not interpret this record as a passing future CI run.

Reproduce native evidence after building the CLI, `probes/font-styles`, and Native AOT, with the existing pinned private font manifest:

```sh
python scripts/verify_heading_keep.py --manifest FONT_MANIFEST.json --output artifacts/f1/output \
  --dotnet dotnet --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --api-dll probes/font-styles/bin/Release/net10.0/FactsPDF.FontStyles.dll \
  --native-cli artifacts/f1/native/FactsPDF.Cli
```

No local Chrome printing, sandbox change or Library upload was attempted. Browser comparison is not required for this native-engine slice. No database/migration, public API, table expansion, page furniture, release or registry publication work.
