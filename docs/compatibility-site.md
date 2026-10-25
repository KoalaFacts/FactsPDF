# Compatibility lookup site (Purity npm)

A static engineering-support lookup, not an engine release announcement. The only support-data source is `site/data/compatibility.json`. Its audit commit/date describe inspected engine behavior; the site build commit is separate. Do not update audit dates or mark features supported merely because the site is rebuilt.

## Local and CI use

Use Node >=22.12, then `cd site && npm ci && npx playwright install chromium && npm run verify`. The lockfile pins the published npm packages, including Purity core/plugin 0.3.4, Vite 8.3.4 and Playwright 1.64.0. No framework is vendored, no CDN is required, and no fonts or tracking scripts are downloaded by the page.

On Linux hosts missing browser system libraries, use `npx playwright install --with-deps chromium` with the host administrator's permission, as hosted CI does. This installs the matching pinned browser; do not bypass managed browser policies. Restricted environments can run `npm test && npm run build` and use authorized hosted CI for browser coverage.

The model tests verify IDs, status/category data, filters, URL routing and safe CSV export. Real Playwright tests use the production build under `/FactsPDF/`, cover filters, detail links and refresh, empty/error states, download, keyboard navigation and mobile width, and retain screenshots. Passing these site tests does not re-audit the renderer.

## Publish and update

Set repository **Settings > Pages > Build and deployment > Source: GitHub Actions** once. This is a repository administration setting; do not add a personal token to client code or artifacts. `.github/workflows/compatibility-pages.yml` tests pull requests without deploying them. After merge to main, the same verified artifact is deployed with narrowly scoped Pages/OIDC permissions. No npm or NuGet publication occurs.

To update a feature, edit its stable-ID row in the JSON, including scope, limitations, source/test evidence and actual audit commit/date. Update category totals when adding or removing rows. For an intentional snapshot change, record and review its SHA-256 in `site/scripts/validate.mjs` and this provenance document; validation rejects any other byte changes, including notes-only edits. Run tests/build, review the PR, then merge. The site reads that data; there is no separate support table to hand-edit in the UI. Keep plans, milestones and release versions distinct. Missing history remains unknown. Counts describe this inventory, not whole-standard conformance or product completion.

## Scope

This first page supports search, category/status/introduction filters, pagination, feature details/evidence, shareable hash links, Chinese/English interface labels, and JSON/CSV download. Audit notes remain in their original language. External links are HTTPS; examples are text, never executed. A subfeature marked full does not make its parent fully supported. No database, account system, server renderer, or complete historical-version matrix is introduced.

The original 418-row audit remains in Git history with SHA-256 `c740aa1e2614a9f5f1abee5e64d54f0f64b69bf2d60f8a4a41c7e5b6c7489ffe`. The intentional C1 update changes seven stable-ID rows (b/strong/i/em, font-weight/font-style, explicit font roles) to partial support based on core revision `00d55dc5f48779d49945fcc10ccabb6019e57d11`. Remaining rows retain their original per-row audit provenance. The C1 snapshot SHA-256 is `64e91b017dbfc595368387433e34dced00c01e73a80e992d44d4924d483aef20`; record counts and the nine product targets are unchanged. Actual current test and deployment results belong to the PR/workflow logs, not guessed success claims in this guide. The separate report sample PR is not changed or approved by this work.

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

C2 intentionally updates only `html.ol`, `html.ul`, and `html.li` against core `a172961c471fa12f45a1ac831daad1e2b32d7bcc` after list tests and the real API/CLI/Linux AOT report. The other 415 rows retain their previous provenance; total rows remain 418. Current C2 snapshot SHA-256: `420e04d19016776b895d860b420d1bc3bca65192debe8e8108a338bdde787a9d`. This data update does not change Pages configuration or claim deployment success.

D1 intentionally updates only three stable rows (`html.img`, `attr.13`, `pdf.images`) to partial support using actual engine/entry evidence at `5ca3ad5fce178b895b704c6afdec05d56b8b8bb1`. The count remains418; PNG/SVG, inline/list images and implicit URL/file resolution remain unsupported. Snapshot SHA-256: `2fd3a747bb8040321c6e0558adc8ba08295c4b862ff17a401bfc47f6aaa6c3be`. Local772 core tests,16 actual PDFs across4 entries, raw DCT/geometry/raster checks and four viewed unique pages support this snapshot; protected browser WASM run39090248247 also passes with identical4PDFs and actual PDF.js search/copy/paste. The Exif diagnostic correction and final same-head PR gates are tracked separately. Existing Pages deployment404 is unchanged and was not retried or reconfigured.

## 24 October 2026: implemented preview capabilities

Snapshot `audit-2026-10-24.preview-capabilities` audits main
`5722a6a3eeafb6833cd1ddfe733d7b7cae9df9cf`. Its SHA-256 is
`84be0d224340acda9330dd013f28f74267a8c2b0fcf24a060bf74547e97bc56a`. The 418 stable IDs, their order,
category totals and introduction-history fields are unchanged. Exactly 50 rows
have updated audit evidence; the other 368 rows retain their previous values,
including per-row source commits and dates.

| Updated rows | Count | Recorded scope |
| --- | ---: | --- |
| `html.col/colgroup/table/tbody/td/th/thead/tr` | 8 | E1 fixed columns and E2 whole-row pagination with one optional repeated header |
| `html.h1`–`html.h6`, `css.break-after`, `value.break.avoid` | 8 | F1 opt-in heading/first-paragraph keep outside lists; oversized combinations relax |
| `attr.15` | 1 | Spanning remains unsupported by the implemented table subset |
| `pdf.keep`, `pdf.headers`, `pdf.tables` | 3 | F1–F3 behavior and separate F4 combined-report engineering evidence |
| `runtime.node`, `runtime.win-aot`, `runtime.browser-wasm` | 3 | Existing subprocess probe, Windows x64 native execution and private browser WASM host |
| Existing width, padding, background and border properties | 27 | Current element applicability, including tables/cells; cell padding percentages use column-track width |

Fourteen rows move to `partial`: the eight table elements, bounded
`break-after:avoid`, three PDF capabilities, Windows x64 AOT and browser WASM.
The remaining updated rows retain their statuses. Counts are **44 full,
114 partial, 252 none, 6 unknown and 2 not applicable**. The accepted HTML tag
count becomes 34; the accepted CSS property count remains 37. These counts
describe this inventory, not whole HTML/CSS conformance.

The Node row remains `none` for its named JavaScript/TypeScript SDK feature:
B1 has an internal CLI subprocess probe, not a published SDK. Browser WASM is
`partial`, explicitly single-threaded and non-AOT. Windows AOT is `partial`
for its tested two-page ASCII, diagnostics, destination-preservation and recovery
slice; untested font/resource/platform combinations are not promoted.

Evidence was read from the code, documentation, existing tests and actual
Actions results. The [main CI run](https://github.com/KoalaFacts/FactsPDF/actions/runs/39876677455)
passed 1052 .NET tests on its Ubuntu job and the
[Windows native job](https://github.com/KoalaFacts/FactsPDF/actions/runs/39876677455/job/120163846267)
passed. The protected browser and native F4 steps passed in
[PR49's browser workflow](https://github.com/KoalaFacts/FactsPDF/actions/runs/39876108148)
at head `30a5d575ea205a882cd36800d3f08f2a44196985`.
That head and the audited main share Git tree
`4e5baf835b7db888932df2103c76a8d9ac85f268`; the workflow is still recorded
against its actual PR head, not relabelled as a main-SHA run.

[F4's combined 10/30/100-record reports](combined-report.md) supply separate
positive engineering evidence for the implemented features. Their new reference
pages remain pending user review. This data refresh neither reran the engine nor
changed the original acceptance denominator, performance budgets or release
approvals. Current site validation results belong to the actual update's
command/CI logs.

## 25 October 2026: PNG and rich table capabilities

Snapshot `audit-2026-10-25.png-rich-tables` audits merged main
`a085085ef161d51a241046839e59a1fd2dab7eea`. The audit date uses
Australia/Sydney (25 October; 24 October in UTC). Its SHA-256 is
`c4f428650a45752561c0fc3ed58b61660ccd7e10c2da0d40c6c73f0720967800`.
All 418 IDs and their order, category totals, parent relationships and
introduction-history fields are unchanged. Exactly 44 rows change; the other
374 rows retain their complete previous records, including source commits and
audit dates. The `engine_baseline` field remains the original inventory
baseline, not the current audited engine commit.

| Updated rows | Count | Recorded scope |
| --- | ---: | --- |
| `html.img`, `attr.13`, `pdf.images` | 3 | Bounded static PNG and transparency alongside JPEG; explicit resource bindings and direct cell images |
| `html.p/table/tbody/td/th/thead/tr`, `attr.15`, `pdf.tables` | 9 | Colspan, flat paragraph/image cell content and oversized body-row continuation; headers and images remain atomic |
| Existing width, padding, background and border properties | 27 | Cell paragraphs reject box declarations; cell percentage padding uses the sum of tracks covered by colspan |
| `css.break-before`, `css.break-after` | 2 | Cell paragraphs reject break declarations, including overridden declarations |
| `html.header`, `html.footer`, `pdf.headers` | 3 | Existing page-margin text options are separate from unsupported HTML tags and future structured furniture |

Only `attr.15` changes status, from `none` to `partial`: colspan is implemented
on td/th, while rowspan remains unsupported. Counts are **44 full, 115 partial,
251 none, 6 unknown and 2 not applicable**. The accepted tag/property counts
remain 34 and 37. The images' D1 introduction records still describe their
original JPEG implementation; this update does not invent a new historical
milestone or release for PNG or table features.

The current scope is defined by [explicit image resources](images.md) and
[fixed-column tables](tables.md). PNG support excludes 16-bit samples, Adam7,
APNG and ICC/HDR color transformations; SVG remains unsupported. Short tables
move intact, and body rows that fit on a fresh page stay whole. Only oversized
body rows continue through complete text lines and direct image items, with
independent cell progress so short values appear once. Repeated headers and
each image with its own decoration remain indivisible. The inability to place
required atomic content still fails explicitly.

`pdf.headers` remains partial for `PageNumbers`, `HeaderText` and `FooterText`:
literal single-line text, fixed normal black 10pt styling and existing centered
margin placement. Total pages, six regions, first-page replacements and custom
styles are outside this main snapshot. HTML `<header>` and `<footer>` remain
unsupported. Unmerged implementation work is not release evidence or current
support.

This refresh statically inspects the merged source, documentation and existing
renderer tests. It does not rerun renderer acceptance, approve new reference
pages, publish a package, or extend untested platform claims. The three narrow
data guards exercise lookup results for colspan versus rowspan, bounded PNG
support, and page-margin options versus HTML tags. Existing site tests and
the exact-byte SHA guard remain in place. The `viewer_validation` metadata is
explicitly historical; current command/CI logs establish site verification,
and a successful build does not establish a successful Pages deployment.

The configured project path is `/FactsPDF/`, corresponding to
<https://koalafacts.github.io/FactsPDF/> once Pages is enabled and deployed.
This data refresh does not change the publishing workflow or repository Pages
settings. Until a successful production deployment is verified, use the
repository [source dataset](../site/data/compatibility.json) and the feature
documentation as the available compatibility reference.

## 25 October 2026: structured page furniture

Snapshot `audit-2026-10-25.structured-furniture` audits merged main
`2f9cfd1fac13ab6034ac03a10d03b680f1de4018`. Its SHA-256 is
`7426bdd28a6ebbb5c5af04c790e655cb64408abfec1ef35a68d99979b7ad35ee`. Only `pdf.headers`,
`html.header` and `html.footer` change; the other **415 complete records** retain
their previous values, including their source commits and audit dates. All 418
IDs, order, categories, parent relationships, introduction history and statuses
remain unchanged: **44 full, 115 partial, 251 none, 6 unknown and 2 not
applicable**. The tag/property counts remain 34 and 37.

`pdf.headers` remains partial and now describes the implemented .NET API and
CLI structured mode: `PdfOptions.PageFurniture`, six left/center/right regions,
physical `{page}` / `{pages}`, whole-set `FirstPage` replacement, per-region
1–144pt text, opaque RGB and explicit bold/italic font roles. An empty first-page
set clears that page's furniture without removing its body or restarting page
numbers. The CLI uses `--page-furniture path.json`, a bounded strict UTF-8 JSON
configuration. Body pagination is preserved.

Regions remain single-line text, without HTML, images, multiline wrapping,
automatic shrinking or CSS `@page`. Missing fonts, bad templates, collisions
and overflow fail explicitly. Structured furniture is mutually exclusive with
legacy `PageNumbers`, `HeaderText` and `FooterText`, even empty legacy text;
those text fields retain their literal behavior. HTML `<header>` and `<footer>`
still fail independently of either PDF option mode. No full status or new
release/introduction version is inferred from this expansion.

The cited core, CLI, shared JSON decoder and dedicated furniture tests are
unchanged between historical main `ff03f91edde3bc686262477aac9232e0e88b5608`
and this audited main. The actual
[Ubuntu CI log at that historical commit](https://github.com/KoalaFacts/FactsPDF/actions/runs/40134259420/job/120992303896)
records **1,364/1,364 tests passing**, including actual totals, first-page
replacement, six-region styling and CLI/independently constructed API output
comparisons. That run remains attributed to `ff03f91e`; it is not recorded as a
new `2f9cfd1f` execution. This documentation/data refresh does not rerun the
renderer, complete the full 52-PDF legacy replay, or approve release/reference
pages. Later native acceptance evidence keeps its own commit and scope; this
three-record update does not revise the platform rows.

The model guard now requires structured API/CLI lookup results while retaining
the unsupported-HTML-tag and legacy-mode distinctions. The browser startup
expectation follows the new global audit SHA. Its `border-style` detail source
expectation deliberately remains at `a085085e`, because that row was not
reaudited. Current site verification belongs to this change's command/CI logs;
prior passing browser tests or a successful local build do not establish a new
browser run or successful public deployment.
