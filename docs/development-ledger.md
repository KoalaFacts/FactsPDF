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
