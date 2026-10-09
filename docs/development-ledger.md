# First renderer execution ledger

- Base main: 78d75bfb7fd9ef0c72552e234dc7af1236330ec4.
- Draft PR #1 and branch feat/first-renderer isolate the work; no merge or release authorized.
- Initial test commit: 97f1bf56eef133962a0253abc89d08d599cc09f2.
- Actual red run: 37937799186, job 113844157609. .NET SDK 10.0.401 compiled the projects.
- 40 NUnit cases discovered: 39 failed, 1 passed. Failures were expected missing-rendering/validation assertions, not compiler errors.
- The culture comparison initially passed vacuously for empty output; strengthen it to assert a real PDF before declaring final verification.
- Ruling: use a narrow ASCII/Courier slice first, not an unverified Unicode renderer. This leaves the full Chinese/English milestone open.
- Ruling: compiler tests must run in GitHub Actions because the local container has no .NET SDK or download connectivity.
- No external implementation source or font file was copied. Framework/test dependencies remain distinct from runtime engine dependencies.

- Core green run: 37939077534 (commit 17ceb1d), all 40 original cases passing.
- CLI setup initially failed restore with ambiguous `factspdf` versus `FactsPDF` identity; changed CLI assembly to `FactsPDF.Cli` in 9f0761c.
- Second red run: 37939484243 / job 113849892014, 58 discovered, 41 passed and 17 expected failures (12 CLI behaviors, 5 renderer boundaries).
- Root causes: Unicode whitespace was discarded by IsNullOrWhiteSpace; arbitrary head text was silently ignored; head placement unchecked; empty-paragraph break-before lost. Targeted fixes follow those observed regressions.
