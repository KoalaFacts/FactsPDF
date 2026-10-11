# Compatibility lookup site (Purity npm)

A static engineering-support lookup, not an engine release announcement. The only support-data source is `site/data/compatibility.json`. Its audit commit/date describe inspected engine behavior; the site build commit is separate. Do not update audit dates or mark features supported merely because the site is rebuilt.

## Local and CI use

Use Node >=22.12, then `cd site && npm ci && npm run verify`. The lockfile pins the published npm packages, including Purity core/plugin 0.3.4, Vite 8.3.4 and Playwright 1.64.0. No framework is vendored, no CDN is required, and no fonts or tracking scripts are downloaded by the page.

The model tests verify IDs, status/category data, filters, URL routing and safe CSV export. Real Playwright tests use the production build under `/FactsPDF/`, cover filters, detail links and refresh, empty/error states, download, keyboard navigation and mobile width, and retain screenshots. Passing these site tests does not re-audit the renderer.

## Publish and update

Set repository **Settings > Pages > Build and deployment > Source: GitHub Actions** once. This is a repository administration setting; do not add a personal token to client code or artifacts. `.github/workflows/compatibility-pages.yml` tests pull requests without deploying them. After merge to main, the same verified artifact is deployed with narrowly scoped Pages/OIDC permissions. No npm or NuGet publication occurs.

To update a feature, edit its stable-ID row in the JSON, including scope, limitations, source/test evidence and actual audit commit/date. Update category totals when adding or removing rows. Run tests/build, review the PR, then merge. The site reads that data; there is no separate support table to hand-edit in the UI. Keep plans, milestones and release versions distinct. Missing history remains unknown. Counts describe this inventory, not whole-standard conformance or product completion.

## Scope

This first page supports search, category/status/introduction filters, pagination, feature details/evidence, shareable hash links, Chinese/English interface labels, and JSON/CSV download. Audit notes remain in their original language. External links are HTTPS; examples are text, never executed. A subfeature marked full does not make its parent fully supported. No database, account system, server renderer, or complete historical-version matrix is introduced.

The imported snapshot remains byte-identical to the supplied 418-row audit (SHA-256 `c740aa1e2614a9f5f1abee5e64d54f0f64b69bf2d60f8a4a41c7e5b6c7489ffe`). Actual current test and deployment results belong to the PR/workflow logs, not guessed success claims in this guide. The separate report sample PR is not changed or approved by this work.

### Production template regression

The Purity 0.3.4 AOT compiler emits a U+200B text placeholder for a lone
`tbody` interpolation. HTML table parsing moves this text before the table,
invalidating the compiled DOM paths and preventing startup. The adjacent empty
interpolation in `main.js` deliberately selects the compiler's comment-slot
path; keep it until an upstream compiler fix is verified. It adds no table row.

`npm test` now also builds and mounts the real production bundle in jsdom,
checking startup, actual filtered row content, detail history creation and the
source JSON entry outside the mobile-hidden sidebar. These DOM checks do not
claim layout or native browser coverage. The existing hosted Chromium suite
also checks Back/Forward and mobile JSON download bytes. Local environments
with browser policy restrictions should use the DOM/build checks and authorized
CI rather than work around those restrictions.

The regression tests and application workaround were independently authored
with AI assistance for this repository; no framework source was copied or
modified. jsdom is a pinned MIT-licensed development-only dependency and is
not included in the deployed bundle.
