# FactsPDF · Can I Render

Compatibility lookup built with the published Purity npm packages.
Source: `site/`. Data: `site/data/compatibility.json`.
Read `docs/compatibility-site.md` for test, deployment and update instructions.

This page describes scoped engineering support, not engine release readiness.

## Languages

New visits open in English, including loading/error messages, all feature
scopes, limitations, evidence descriptions and methodology. Chinese remains an
explicit option; the browser remembers a visitor's choice when storage is
available. Blocked storage does not prevent the lookup or language switching.
Search accepts both English display wording and the original Chinese wording.

English is a display translation, not a new engine audit. The source-language
418-record JSON, IDs, support states, dates, source links, code examples and
JSON/CSV exports remain unchanged. Examples are labelled as verbatim source.
Translations do not remove limitations or turn unverified support into support.

The current catalogue in `site/src/audit-english-logical-side-longhands.mjs`
reuses the previous bounded-blockquote English audit by exact source-text
index. It updates nine source strings (the eight logical-side scope/limit
descriptions and the audited CSS inventory note), without retranslating
unaffected records. All 694 ordered source strings are bound to an exact
SHA-256 before display. It retains one translation per unique Chinese display
string returned by `collectAuditStrings()` in
`site/src/localisation.mjs`. Its source SHA-256 binds the ordered original text
inventory before any translations are used. On intentional audit-text changes,
update the corresponding translations and digest together; never just change
the digest to silence a failure. `site/tests/localisation.test.mjs` checks
coverage, source preservation, diagnostic anchors and stale-text rejection.
The ordinary browser suites explicitly retain Chinese interaction coverage;
`english.spec.js` uses empty storage and a Chinese browser locale to prove the
real first-visit default, all list pages, details, bilingual search, preference
persistence, blocked storage, failure recovery and unchanged raw downloads.
Run `cd site && npm run verify` before merging. No external translation service,
tracking, new dependency or runtime network call is introduced.

## Publishing

Public site: <https://koalafacts.github.io/FactsPDF/>.
Use `.github/workflows/compatibility-pages.yml` as the **only Pages publisher**.
It installs the locked dependencies, tests the model and production UI, builds
Purity/Vite, then publishes only `site/dist` after the Chromium checks pass.
The generic `static.yml` template must not publish `.` or the unbuilt `site/`
source directory over this output. Workflow changes run regression checks.

After deployment, the publisher checks the public HTML and support-data SHA-256
against the tested build and confirms the compiled JavaScript/CSS are reachable.
That deployment identity is separate from each feature's engine audit commit;
rebuilding the site does not change its 418 audited compatibility records.
