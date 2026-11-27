# Compatibility lookup site (Purity npm)

A static engineering-support lookup, not an engine release announcement. The only support-data source is `site/data/compatibility.json`. Its audit commit/date describe inspected engine behavior; the site build commit is separate. Do not update audit dates or mark features supported merely because the site is rebuilt.

## Local and CI use

Use Node 24 or later (`site/.nvmrc`), then `cd site && npm ci && npx playwright install chromium && npm run verify`. The lockfile pins the published npm packages, including Purity core/plugin 0.5.2, Vite 8.3.4 and Playwright 1.64.0. No framework is vendored, no CDN is required, and no fonts or tracking scripts are downloaded by the page.

On Linux hosts missing browser system libraries, use `npx playwright install --with-deps chromium` with the host administrator's permission, as hosted CI does. This installs the matching pinned browser; do not bypass managed browser policies. Restricted environments can run `npm test && npm run build` and use authorized hosted CI for browser coverage.

The model tests verify IDs, status/category data, filters, URL routing and safe CSV export. Real Playwright tests use the production build under `/FactsPDF/`, cover filters, detail links and refresh, empty/error states, download, keyboard navigation and mobile width, and retain screenshots. Passing these site tests does not re-audit the renderer.

## Publish and update

Set repository **Settings > Pages > Build and deployment > Source: GitHub Actions** once. This is a repository administration setting; do not add a personal token to client code or artifacts. `.github/workflows/compatibility-pages.yml` tests pull requests without deploying them. After merge to main, the same verified artifact is deployed with narrowly scoped Pages/OIDC permissions. No npm or NuGet publication occurs.

To update a feature, edit its stable-ID row in the JSON, including scope, limitations, source/test evidence and actual audit commit/date. Update category totals when adding or removing rows. For an intentional snapshot change, record and review its SHA-256 in `site/scripts/validate.mjs` and this provenance document; validation rejects any other byte changes, including notes-only edits. Run tests/build, review the PR, then merge. The site reads that data; there is no separate support table to hand-edit in the UI. Keep plans, milestones and release versions distinct. Missing history remains unknown. Counts describe this inventory, not whole-standard conformance or product completion.

## Languages and English translation

New visits open in English (interface, loading and error messages, feature
scopes, limitations, evidence and methodology); Chinese stays an explicit
option, remembered when browser storage is available, and blocked storage does
not stop the lookup or language switching. Search accepts both the English and
the original Chinese wording.

English is a display translation, not a new engine audit. The source-language
JSON, IDs, support states, dates, source links, code examples and JSON/CSV
exports are unchanged, examples are labelled as verbatim source, and a
translation never removes a limitation or turns unverified support into
support.

Translations are chained layers under `site/src/audit-english-*.mjs`. The
current layer is the one `site/src/localisation.mjs` imports. Above the base
`audit-english.mjs`, each layer builds on earlier ones (usually one: it checks
its length and replaces, remaps or inserts strings by source-text index) and
declares a new `sourceDigest`. The page refuses to load, with no Chinese
fallback, unless the SHA-256 of the JSON array of ordered strings returned
by `collectAuditStrings()` matches that digest and the counts agree. When audit
text changes, add a new layer and update its digest together with the
translations; never change the digest alone to silence a failure.
`site/tests/localisation.test.mjs` checks coverage, source preservation,
diagnostic anchors and stale-text rejection. The ordinary browser suites run
with Chinese stored as the locale (`site/playwright.config.js`), so Chinese
interaction stays covered; `site/tests/english.spec.js` proves the first-visit
default with empty storage and a Chinese browser locale, all list pages,
details, bilingual search, preference persistence, blocked storage, failure
recovery and unchanged raw downloads. No translation service, tracking, dependency or runtime network
call is used.

## Pages publisher

Public site: <https://koalafacts.github.io/FactsPDF/>.
`.github/workflows/compatibility-pages.yml` is the only workflow that may
publish Pages (`site/tests/pages-publishing.test.mjs` fails on a second
publisher), and changes to any workflow re-run these checks. It publishes
only the built `site/dist` after the Chromium checks pass, then checks the
public HTML and support-data SHA-256 against the tested build and that the
compiled JavaScript and CSS are reachable. That deployment identity is
separate from each feature's engine audit commit; rebuilding the site changes no audited record.

## Scope

This first page supports search, category/status/introduction filters, pagination, feature details/evidence, shareable hash links, Chinese/English interface labels, and JSON/CSV download. Audit notes remain in their original language. External links are HTTPS; examples are text, never executed. A subfeature marked full does not make its parent fully supported. No database, account system, server renderer, or complete historical-version matrix is introduced.

The original 418-row audit remains in Git history with SHA-256 `c740aa1e2614a9f5f1abee5e64d54f0f64b69bf2d60f8a4a41c7e5b6c7489ffe`. The intentional C1 update changes seven stable-ID rows (b/strong/i/em, font-weight/font-style, explicit font roles) to partial support based on core revision `00d55dc5f48779d49945fcc10ccabb6019e57d11`. Remaining rows retain their original per-row audit provenance. The C1 snapshot SHA-256 is `64e91b017dbfc595368387433e34dced00c01e73a80e992d44d4924d483aef20`; record counts and the nine product targets are unchanged. Actual current test and deployment results belong to the PR/workflow logs, not guessed success claims in this guide. The separate report sample PR is not changed or approved by this work.

### Production template regression

Purity 0.3.4 through 0.5.1 compiled a lone `tbody` interpolation to a U+200B
text placeholder. HTML table parsing moved that text before the table,
invalidating the compiled DOM paths and preventing startup, so `main.js` used
an adjacent empty `${null}` interpolation as a workaround. Purity 0.5.2 emits
comment placeholders inside table sections, and the workaround has been
removed. The startup tests below would catch a regression.

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
styles are outside this main snapshot. HTML `<header>` and `<footer>` are
ordinary flow blocks (like `div`), never repeated page furniture. Unmerged implementation work is not release evidence or current
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
CLI structured mode: `WithPageFurniture`, six left/center/right regions,
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
render as ordinary flow blocks like `div`, independently of either PDF option mode. No full status or new
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

## 27 October 2026: colour-only CSS `background` shorthand

Snapshot `audit-2026-10-27.color-only-background` audits implementation commit
`fe622926bbe5a9b83932e09a643c4c9cb7ceb60d` and its independently authored tests. SHA-256: `e0b89783176b492b75fd12731f566971ca6dff1671c27769c3824335059b0633`.
Of the 418 stable feature IDs, only `css.background` changes from `none`
to **partial**: one supported colour or `transparent`, with the same cascade,
background painting and element applicability as `background-color`. No
background images, gradients, functions, sizing, positioning or repeat.
The other 417 feature records, source histories and category totals are kept
unchanged. Counts are **44 full, 116 partial, 250 none, 6 unknown, 2 not
applicable**; accepted CSS property names become 38 (not whole-standard
coverage). This code-audit snapshot does not assert completion of PR-head CI
or any public product release; use exact PR/main workflow evidence for that.
GitHub Pages availability still depends on repository settings and successful
deployment; a validated static build alone is not a live website.

## 28 October 2026: report print spacing preview audit

Snapshot `audit-2026-10-28.report-print-spacing` audits source `1b6a2076460610e56fd2a6bf366fea391d03f23b`, not a publicly released product. SHA-256: `a9347e19ca77034ab1019cd34c74a3981352fb2eba894d9b8d71747be93b1a2a`. All **418 feature IDs**, ordering, categories, parent IDs and introduction histories remain unchanged. Exactly **22 rows** updated; the other **396 feature objects** retain their previous audit metadata. `css.margin` and `value.length.absolute` change `none` → `partial`. Totals are **44 full, 118 partial, 248 none, 6 unknown, 2 n/a**; **39 accepted CSS property names**. Print units `mm/cm/in` apply to bounded length properties, not `q/pc`, `em/rem/calc` or full CSS. The margin shorthand only renders vertical paragraph/heading margins if horizontal sides resolve to zero; nonzero horizontal offsets fail explicitly.

The implementation source includes actual API/managed CLI/geometry/PDF tests and a fontless business example. Exact PR-head CI results and independent review must be recorded separately. Updating this dataset is not a real test execution, human PDF approval, customer right or GitHub Pages deployment.

## 28 October 2026: scoped horizontal report margins

Snapshot `audit-2026-10-28.horizontal-report-margins` audits engine code `c8caaa273415a24ad062dd036a90b40ba11c53eb`.
SHA-256 `46e7a5684d4ee24b70a3f7ce4c1bf47f517c57748c63d4d90ba06e24c80b9aa4`. Only three stable IDs (`css.margin`, `css.margin-left`,
`css.margin-right`) change. Other 415 complete feature objects and their source
histories remain untouched. Counts: 44 full, 120 partial, 246 none, 6 unknown,
2 not applicable. 41 accepted property names are not CSS standard coverage.
Horizontal margins are bounded and only for paragraphs/headings; CSS-wide and
cascade stay per-side. Native CI/review and Pages are separately gated; this
audit does not claim any public release, real customer use, or Pages deploy.

## 28 October 2026: bounded horizontal auto margins

The `audit-2026-10-28.horizontal-auto-report-margins` snapshot reaudits exactly
three CSS records (`css.margin`, `css.margin-left`, `css.margin-right`) against
implemented engine source `125e7788ac804d9d6960b29717efaf0c151077c0`. The other 415 complete feature objects,
including source attribution and unknown introduction versions, are preserved.
All 418 IDs, category/status totals and 41 accepted CSS property names remain
unchanged. New audit SHA-256: `ba62f3b3eda709cab9d53c2bb922f69c7e9337ec87a469cb20b4db40e6c507a4`.

For explicitly sized paragraphs/headings outside table cells, horizontal
`auto` values consume remaining outer-box space (accounting for border and
padding); two `auto` sides divide it. This is not vertical auto, negative
margins, generic container margins, full CSS conformance or a released package.
Source tests and rendered PDF evidence belong to their exact PR/head workflows;
this data-only refresh is not standalone Native AOT, customer approval or Pages
hosting evidence.

## 28 October 2026: solid real-PDF text decorations

Snapshot `audit-2026-10-28.text-decoration-real-pdf` audits core source
`280212ceea1eb7d0105a5bbb2ee61825364836e8`. The `text-decoration` and `text-decoration-line` names
share one bounded text-run paint capability (none, underline, line-through).
One existing row (`css.text-decoration`) moves none -> partial; the other
417 rows preserve full provenance and status. Exactly 418 IDs remain, with
43 accepted CSS property names; totals are 44 full, 121 partial, 245 none,
6 unknown and 2 not applicable. Snapshot SHA-256: `b788350a7ca6407baf26e8a5601831ef102b8da0917c8574a0a7338ef05ac81f`.

These values generate PDF filled lines, measured from resolved glyph advances
and budgeted like other drawing commands. The dataset audit is not a substitute
for exact-head CI, truly independent review, distribution rights, a published
preview, Pages deployment or human PDF approval.

## 28 October 2026: bounded opaque RGB colour rendering

Snapshot `audit-2026-10-28.opaque-rgb-colours` audits the actual RGB semantic/rendering
implementation commit `b35413c2a5a4cdc584d03374c964fc3ed6ccee43`. Its SHA-256 is
`18c5f20793c637ca7c43fcfaada1d51838177ba3a39555fe6f0c1d48d4bfaed2`. **All 418 IDs, order, introduction/release fields and 43
accepted CSS property spellings remain unchanged.** Exactly 10 colour rows were
reaudited; the remaining 408 full records retain their prior provenance. The
function-family row `value.color.rgbfunc` moves from none to partial, so counts
are **44 full, 122 partial, 244 none, 6 unknown, 2 not applicable**.

Only opaque integer `rgb()` (comma/space) and comma `rgba(r,g,b,1)` are
supported for existing text/background/border colour inputs; the function AST
is lowered to the exact existing `#rrggbb` paint semantics. Fractional or
percentage channels, transparency, HSL, slash-alpha, gradients and nested
functions still fail explicitly. The implementation adds actual PDF/browser-free
CLI and Linux Native AOT hex-equivalence/recovery checks; audit data does not
claim that still-running CI or a future independent review has passed. This is
not a public package or Pages deployment.

## 28 October 2026: bounded parent-relative font sizing

Snapshot `audit-2026-10-28.relative-font-sizing` is tied to implemented engine
source `cd9355bbf8c85d31a8f5c08ae8e02d89a3ebe345`. Exactly two records (`css.font-size` and
`value.length.relative`) were reaudited. All **418** IDs and order,
introduction/release-null values, and the other **416** complete records are
unchanged. No new CSS property name was introduced: the current property-name
count stays **43**. The relative-length family moves none to partial (44 full,
123 partial, 243 none, 6 unknown, 2 not applicable).

Only `font-size` accepts positive percent or `em` lengths relative to the
computed parent size, with computed 1–144pt limits and real glyph/pagination
layout. `rem/ex/ch/lh/rlh`, `calc()`, font-size keywords and relative lengths
for box properties remain unsupported. The real Linux Native AOT print report
and NUnit tests check equivalent fixed-point PDF bytes and output recovery.
The SHA-256 of this exact JSON snapshot is `b8bbe04c89cff46d7e9aabf086eb610cff0757e16214ffc9f42c4da9b717ac2c`.
Source audit and site builds do not establish a publicly deployed Pages site,
independent merged-source approval, a published software package or a customer
grant.

## 28 October 2026: bounded report min/max widths

Snapshot `audit-2026-10-28.report-minmax-width` is tied to actual engine change
`bc704aed90b1e5dbdc1deec65a7fd314b626467e`. Of 418 stable IDs, only `css.min-width`,
`css.max-width` and `css.box-sizing` are reaudited; **415 other complete records
are unchanged**. Two rows move from none to partial, producing 44 full,
133 partial, 233 none, 6 unknown and 2 not applicable. The accepted property
name count increases from 49 to **51**. The exact UTF-8 JSON SHA-256 is
`2ddafd7804fb4f37c1b838a85f5fb8b2c72e9839f930712e6099faafac47de85`.

The implementation constrains real used report block, paragraph and heading
widths, including `width:auto`, `margin:auto`, `box-sizing:border-box`,
text wrapping and page fragments. Nonnegative print units and percentages are
bounded and measured against the parent content width. It is **not** generic
intrinsic sizing, table/image/inline layout, or complete CSS Sizing. Real NUnit
PDF geometry and an independently authored source-built Linux Native AOT PDF
oracle provide engineering acceptance; exact-head checks, independent code
review, private software packaging, release rights and Pages deployment are
separate gates. No customer release or public site is implied by this snapshot.

## 28 October 2026: root-relative report typography

Snapshot `audit-2026-10-28.root-relative-typography` audits the actual
C# engine implementation at `1aba07ce42ccd8b2ae9eade4a71204818984cb9c`.
Exactly four of the existing 418 stable records were revised:
`css.font-size`, `css.line-height`, `value.length.relative` and
`value.lineheight.other`. All four remain **partial**, the other 414
records retain their original provenance, 51 accepted CSS property names
remain, and statuses remain **44 full / 133 partial / 233 none / 6 unknown /
2 not applicable**. The byte-exact UTF-8 JSON SHA-256 is `37346194babc594d6597bdf07353b27f1bba9cc0e151017ce58bc4c7e5bf183c`.

Positive `rem` now resolves `font-size` and `line-height` against the
real computed `html` root font (initial configured size if `html` is
absent); root font self-reference uses initial size while root line-height
uses computed size. It actually changes PDF glyph width, line boxes and
pagination in a runnable 63-record report, with a separately authored
fixed-point PDF oracle and failure-output preservation. Width, padding,
margin, border, letter/word spacing and general CSS Values `rem` remain
unsupported. Matching PR-head CI, independent review, new private binary
candidate, Pages administration, licensing/customer distribution and human
PDF approval are independent, not inferred from this audit.

## 2026-10-29 source audit: scoped horizontal report-panel margins

Snapshot `audit-2026-10-29.report-panel-horizontal-margins` is bound to
actual renderer change `a1182e81c8bf7c5737c4d8bcf2877af3f8e5fbe2`. Exactly **three**
existing CSS IDs (`css.margin`, `css.margin-left`, `css.margin-right`)
were reaudited; **415** other entries preserve their prior provenance.
The **418** stable records, category and status totals, and **52**
accepted CSS property names are unchanged.

Only `div`, `section`, `article` gain bounded nonnegative
horizontal margins and real `margin:0 auto`. Vertical panel margins
remain zero/reset-only, including losing declarations. No margin
collapse, nonzero container vertical margins, generalized body/list,
table/image/inline box margins or new release is claimed. The rows
link the actual code, tests, source PDF example and native evidence.

Exact JSON SHA-256 `8cbf4b3d2c649e4ddae44f288ec3c389ac427b74f0cdbe5f7926e4222e42a8db`
is checked by the original fail-closed validator; English display
uses ordered source-text SHA-256
`f500d4849945d36e581bb6c1ec34bd75a623399e576d47b93f93a5604092fb23`.
Actual CI, independent review, human page approval and commercial
distribution remain separate and must not be inferred from the audit.


## 29 October 2026: scoped vertical report-panel margins

The newer `f5aba206ca30c86dca8bd4b465182d5cea436dcc` source extends the
previous horizontal-panel support to nonnegative `margin-top` and
`margin-bottom` on ordinary `div`/`section`/`article` panels. These
margins change actual painted PDF geometry outside the borders, including
nested box heights and cross-page continuation; they do not implement
margin collapsing or general browser flow. Vertical `auto`, percentages,
negative lengths, `calc()`, unsupported elements and overwide geometry
remain explicit errors. The [50-entry report](../examples/report-panel-vertical-margins.html)
uses an independently authored [fixed-spacer oracle](../examples/report-panel-vertical-margins-oracle.html).

The existing 418 stable records and 52 accepted CSS property names are
preserved. Five scoped margin rows are revised; the remaining 413 records
are not re-scored. English content is bound to the new source text through
`site/src/audit-english-panel-vertical.mjs`; raw JSON/CSV, status identities,
code examples and evidence links retain their provenance. Historical
horizontal-only snapshot hashes above are not the new snapshot digest.
Exact-head CI, Native AOT checks, independent review and human PDF approval
remain separate, not inferred from this documentation change.

## 29 October 2026: bounded numeric report font weights

The `audit-2026-10-29.numeric-report-font-weights` snapshot audits implemented
engine/report source `7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750`. It updates **only**
the existing `css.font-weight` stable-ID record to describe numeric CSS hundred
steps 100–900 selecting the explicit normal (100–500) or bold (600–900) font role.
Other **417 complete records** retain their historical per-feature provenance.
All 418 IDs, category/status counts, 52 accepted CSS property names and unknown
introduction/release versions are preserved. Dataset SHA-256:
`ce2002e73db8f829820605c5cf4b599703ea77e8d67144052a667571efe9ce62`; ordered bilingual source-text SHA-256:
`0cf781f2c0cffa4521ebddb7e01c8de962aafbc35a781dc2db0f300a9f84e9ab`. This is a code-audit update, not a new release, independent
final-head review, human page acceptance or a font distribution.

## 17 November 2026: border edge keywords, opaque alpha colours, background none and word-wrap

Three chained snapshots audit merged sources: `transparent-border-color`
(`value.border.transparent`, none → partial, source `291f1014`),
`stale-value-rows` (`value.border.dashed`, `value.text.currentcolor` and
`value.background.currentcolor` re-audited from none to partial; the source
already supported them) and `hidden-border-style` (the five `border-style`
rows and `value.border.dashed` gain `hidden`, equal to `none` on border
edges); `opaque-alpha-hex` then moves `value.color.alphahex` from none to
partial (alpha `f`/`ff` only); `opaque-slash-alpha` re-audits
`value.color.rgbfunc` (still partial) and the shared colour limitation text of
eight colour rows: `rgba()`/`hsla()` are aliases, the comma form takes a fourth
alpha and the space form a slash alpha, exactly `1` or `100%`; `background-none`
re-audits `css.background` (still partial) for the single `none` reset;
`word-wrap-alias` re-audits `css.overflow-wrap` (still partial) for the legacy
`word-wrap` alias, so accepted CSS property names go from 116 to 117;
`hsl-hue-units` updates the shared HSL limitation text of the same eight colour
rows for `grad`/`rad`/`turn` hues; `initial-value-no-ops` moves nine CSS rows
(`position`, `float`, `clear`, `overflow`, `overflow-x`, `overflow-y`,
`visibility`, `z-index`, `opacity`) from none to partial for their initial value
only, so accepted CSS property names go from 117 to 126; `none-initial-no-ops`
adds five more at `none` (`box-shadow`, `text-shadow`, `transform`, `filter`,
`background-image`; 126 to 131) and re-audits the nine rows' shared text;
`interaction-only-css` records four names accepted with any single keyword and
dropped (`cursor`, `pointer-events`, `user-select`, `resize`; 131 to 135) in
the CSS inventory note only, since none of them has a feature row;
`print-color-adjust` does the same for `print-color-adjust` and
`-webkit-print-color-adjust` at `exact`/`economy` (135 to 137);
`font-smoothing-hints` does the same for `text-rendering`,
`-webkit-font-smoothing` and `-moz-osx-font-smoothing` (137 to 140);
`text-size-adjust` does the same for `text-size-adjust` and
`-webkit-text-size-adjust` at `auto`/`none`/percentages (140 to 141);
`appearance` does the same for `appearance` and its `-webkit-`/`-moz-`
spellings at any keyword (141 to 142); `backface-touch-action` does the same
for `backface-visibility` (and its `-webkit-` spelling) and a single
`touch-action` keyword (142 to 144); `will-change` does the same for
`will-change` at `auto` or a comma-separated list of names (144 to 145);
`transition` does the same for `transition` and its five longhands at a
well-formed transition list (145 to 151); `animation` does the same for
`animation` and its eight longhands at a well-formed animation list and drops
well-formed `@keyframes` rules (151 to 160); `scroll-behavior`,
`overscroll-behavior` and its `-x`/`-y`/`-inline`/`-block` longhands,
`overflow-anchor`, `-webkit-overflow-scrolling` and `scrollbar-width` accept a
single keyword and are dropped, since a PDF page never scrolls (160 to 169);
`caret-color`, `accent-color`, `-webkit-tap-highlight-color`, `scrollbar-color`
and `scrollbar-gutter` accept `auto`, their keywords or a well-formed colour and
are dropped, since a PDF page has no caret, native control, tap flash or
scrollbar (169 to 174); the legacy `-webkit-box-sizing` and `-moz-box-sizing`
spellings are aliases of `box-sizing` with its values and applicability
(174 to 176). `outline-none-reset` re-audits `css.outline` (still partial,
no count change): outside report panels outline resets (`outline:none`/`0`,
`outline-style:none`, widths, colours, offsets) are dropped, since no outline is
painted there, while a style that could paint still fails. The `-ms-` and
`-moz-text-size-adjust` spellings (normalize.css v3/v4) are aliases of
`text-size-adjust` (176 to 178); `color-scheme` and `forced-color-adjust`
accept well-formed values and are dropped, since a PDF page has no preferred
or forced colour scheme (178 to 180); `clip-path` and `backdrop-filter` at
`none`, `mix-blend-mode:normal` and `isolation:auto` join the initial-value
no-ops (180 to 184); the legacy engine spellings `-webkit-`/`-moz-border-radius`,
`-webkit-`/`-moz-box-shadow`, `-webkit-`/`-moz-`/`-ms-`/`-o-transform`,
`-webkit-filter`, `-webkit-clip-path` and `-webkit-backdrop-filter` are aliases
of the standard names with their values and applicability (184 to 195);
`-webkit-`/`-moz-`/`-ms-user-select` are aliases of `user-select`, and
`-webkit-user-drag`, `-webkit-touch-callout` and `-ms-overflow-style` accept
their keywords and are dropped, since a PDF page has no drag, touch callout or
scrollbar (195 to 201); `font-feature-settings` (with its `-webkit-`/`-moz-`
aliases), `font-variation-settings`, `font-variant-ligatures`,
`font-variant-numeric` and `font-stretch` at `normal`, `font-optical-sizing:auto`
and `font-size-adjust:none` join the initial-value no-ops (201 to 210);
`color-adjust` is the deprecated alias of `print-color-adjust`, and `tab-size`
(with `-moz-tab-size`) accepts a non-negative number or length and is dropped,
since no tab stop reaches a page (210 to 213); `zoom:normal` (or `1`/`100%`),
`contain:none`, `content-visibility:visible` and `auto` for `line-break`,
`text-justify` and `image-rendering` join the initial-value no-ops (213 to 219); `speak` accepts its CSS Speech and
CSS 2 aural keywords and is dropped, since the renderer speaks nothing (219 to 220);
`text-decoration-skip-ink` accepts `auto` or `none` and `font-synthesis` accepts `none`,
both dropped, since the renderer synthesizes no face and skips no ink (220 to 222); the legacy
`text-decoration-skip` (with its `-webkit-` alias) accepts `none`, `auto` or `objects` and is
dropped, since no atomic inline sits in a line (222 to 224); `-webkit-text-decoration` and its
`-line`, `-color` and `-style` longhands are aliases of the standard names (224 to 228); `font-variant` and its `-caps`,
`-east-asian`, `-alternates` and `-position` longhands at `normal` join the
initial-value no-ops (228 to 233); `font-language-override`, `font-variant-emoji` and
`font-palette` at `normal` and `text-emphasis` and `text-emphasis-style` at `none` join them
too (233 to 238); `direction:ltr`, `unicode-bidi:normal`, `writing-mode:horizontal-tb`,
`text-orientation:mixed` and `text-combine-upright:none` join them as well (238 to 243);
`vertical-align:baseline` outside table cells is dropped too (the `css.vertical-align`
limitations change; the count stays 243); the multi-column resets `column-count`,
`column-width` and `columns` at `auto`, `column-gap:normal`, `column-span`, `column-rule`
and `column-rule-style` at `none` and `column-fill:balance` join the initial-value no-ops
(243 to 251); `text-overflow:clip`, `line-clamp` and `-webkit-line-clamp` at `none` and
`box-decoration-break` and `-webkit-box-decoration-break` at `slice` join them too (251 to 256);
`rotate`, `scale`, `translate` and `perspective` at `none`, `transform-style:flat` and `mask`,
`mask-image`, `-webkit-mask`, `-webkit-mask-image` and `shape-outside` at `none` join them as
well (256 to 266); `hanging-punctuation:none`, `hyphenate-character` and
`text-underline-position` at `auto` and `text-spacing-trim:normal` join them too (266 to 270);
`background-attachment:scroll`, `background-blend-mode:normal`, `background-origin:padding-box`,
`container-type:normal` and `container:none` join them as well (270 to 275); `aspect-ratio:auto`,
`mask-repeat:repeat`, `mask-size:auto`, `mask-origin` and `mask-clip` at `border-box`,
`mask-mode:match-source`, `mask-composite:add` and `mask-type:luminance` join them too (275 to 283);
`scroll-margin` and `scroll-padding` with their ten side and axis longhands each accept well-formed
offsets and are dropped, since a PDF page is never scrolled to an anchor (283 to 305);
`scroll-snap-type`, `scroll-snap-align` and `scroll-snap-stop` accept their keyword forms and are
dropped too (305 to 308); `animation-composition:replace`, `animation-timeline` and
`view-timeline-inset` at `auto`, `animation-range` and its `-start`/`-end` longhands at `normal`,
`scroll-timeline`, `view-timeline`, their `-name` longhands, `timeline-scope`,
`view-transition-name` and `view-transition-class` at `none` and `scroll-timeline-axis` and
`view-timeline-axis` at `block` join the initial-value no-ops (308 to 323); `anchor-name`,
`anchor-scope`, `position-area`, `position-try`, `position-try-fallbacks` and `overlay` at
`none` and `position-try-order:normal` join them too (323 to 330); `interpolate-size:numeric-only`,
`reading-flow:normal`, `field-sizing:fixed`, `dynamic-range-limit:no-limit` and `caret`,
`caret-animation` and `caret-shape` at `auto` join them as well (330 to 337); `text-wrap-style` and
`text-box-edge` at `auto`, `text-box-trim:none` and `text-box`, `paint-order`, `math-style` and
`math-shift` at `normal` join them too (337 to 344); `contain-intrinsic-size` and its four longhands
at `none` and `transform-box:view-box` join them as well (344 to 350); `font-kerning` at `auto` or
`none` joins them too, and its feature row moves from rejected to partial (350 to 351); `top`,
`right`, `bottom`, `left`, `inset` and the six logical insets at `auto` and `border-image` and
`border-image-source` at `none` join them as well, and the `top`, `right`, `bottom`, `left`, `inset`
and `border-image` rows move from rejected to partial (351 to 364). The `direction`,
`unicode-bidi`, `writing-mode` and `text-orientation` rows, whose initial values the engine already
accepted, are re-audited from rejected to partial as well (count unchanged at 364), and so are the
`font-variant`, `text-overflow`, `box-decoration-break`, `columns`, `column-count`,
`column-width`, `column-gap`, `animation` and `transition` rows (count unchanged).
Well-formed `transform-origin` and `-webkit-transform-origin` values join them too, and the
`transform-origin` row moves from rejected to partial (364 to 366). The `font-synthesis-weight`,
`font-synthesis-style`, `font-synthesis-small-caps` and `font-synthesis-position` longhands accept
`none` like the shorthand and are dropped, since no face is ever synthesized (366 to 370). `border-collapse:separate` and `border-spacing` at one
or two zero lengths are accepted as initial values, since cells already paint separate border boxes
that touch without spacing, and the `border-collapse` and `border-spacing` rows move from rejected
to partial (370 to 372). `empty-cells:show` and `caption-side:top` join them, since empty cells already
paint and no `caption` element is accepted, and both rows move from rejected to partial (372 to 374). `offset-path:none`, `offset-rotate` and
`offset-anchor` at `auto`, `initial-letter:normal` and `text-emphasis-color:currentcolor` are accepted
as initial values too, since no box follows a motion path, no drop cap is drawn and no emphasis mark
is painted; no feature row moves (374 to 379). `quotes` at `auto`, `none`, `match-parent` or pairs
of strings is dropped, since no `q` element or quote content is accepted, and the `quotes` row moves
from rejected to partial (379 to 380). The `word-break` row adds the legacy `break-word` value,
which wraps like `overflow-wrap:break-word`; no count moves. `gap` and `row-gap` at `normal` are
accepted as initial values, since no flex, grid or multi-column container exists, and both rows
move from rejected to partial (380 to 382). A well-formed `font-family` list is validated and
dropped, since no font is discovered and the supplied chain is the only installed font, and the
`font-family` row moves from rejected to partial (382 to 383). The `box-sizing` row adds `<html>`
and inline text as accepting hosts with no effect, then table parts, and the table box's width
supports `border-box`, so universal resets render; no count moves. The margin rows add a zero
margin on `<html>`/`<body>` and the padding rows a zero padding on `<html>`, accepted with no
effect, so `html,body{margin:0;padding:0}` renders; no count moves. The margin, padding and
background rows add inline text as a host that accepts a zero margin or padding and a transparent
background with no effect, then zero margins on `li` and table rows, groups, columns and cells,
then zero margins on `ul`, `ol`, `table` and `br` and zero padding on table rows, groups, columns
and `br`; no count moves. The `<code>`, `<kbd>`, `<samp>`, `<var>`, `<cite>` and `<dfn>` rows
move from rejected to partial inline text (partial 212 to 218, rejected 154 to 148; the
`FPDF1102` search lists 67 records instead of 73). The `<time>` and `<data>` rows follow
(partial 220, rejected 146; 65 `FPDF1102` records), then `<abbr>` (partial 221, rejected 145;
64 records). The `<header>`, `<footer>`, `<main>`, `<nav>` and `<aside>` rows move to partial
block containers like `div` (partial 226, rejected 140; 59 records). `<figure>` moves to
partial like `blockquote` and `<figcaption>` like `div` (partial 228, rejected 138; 57 records). `<address>` moves to partial as an
italic block like `div` (partial 229, rejected 137; 56 records). `<dl>`, `<dt>` and `<dd>` move
to partial block containers like `div` (partial 232, rejected 134; 53 records). `<tfoot>` moves to
partial as body rows after the `tbody`; the `<table>` and `<thead>` limitations drop `tfoot`
(partial 233, rejected 133; 52 records). `<hgroup>` and `<search>` move to partial block
containers like `div`, reusing the `<aside>` row's strings, so the ordered source text and the
English layer are unchanged (partial 235, rejected 131; 50 records). `<menu>` moves to partial as a list exactly like `<ul>`
with its own scope and limitation strings (partial 236, rejected 130; 49 records). `<q>` moves
to partial inline text in quotation marks (partial 237, rejected 129; 48 records). `<bdi>` moves
to partial inline text like `span` (partial 238, rejected 128; 47 records). `<template>` moves
to partial: dropped with its inert contents (partial 239, rejected 127; 46 records). `<script>`
moves to partial: dropped with its data, never run (partial 241, rejected 125; 45 records). The `dir`
attribute moves to partial: `ltr` is accepted on any element (partial 240, rejected 126).
The `<meta>` row stays partial with a wider scope (named and Open Graph metadata accepted); its
scope and limitation strings are replaced in place, so no count moves. `<link>` moves to
partial: metadata links accepted, stylesheet links still fail (partial 242, rejected 124; 44
records). The `hidden` attribute moves to partial as `[hidden]{display:none}` on ordinary
report blocks, the `display` row's limitation stops calling it unsupported and the attribute-selector row adds `hidden` (partial 243,
rejected 123; its failure code is `FPDF1103`, so the `FPDF1102` search is unchanged).
The stale `href` / `target` / `rel` row moves to partial: `a` `href` and metadata `link` `rel`
were already accepted, and `target` plus hyperlink `rel` types on `a` are now ignored
(partial 244, rejected 122; `FPDF1103`, so the `FPDF1102` search is unchanged).
The `display` and `<tfoot>` rows stay partial; their limitation strings are replaced in place
to say that table parts accept the display value they already render and that
`tfoot{display:table-footer-group}` fails `FPDF1202`, so no count moves.
The non-standard `background-color-adjust` joins `color-adjust` as an alias of
`print-color-adjust` with its values and errors, recorded in the CSS inventory note only,
since no feature row covers it (accepted CSS property names 383 to 384).
The `break-inside` and `page-break-inside` rows stay partial; their limitation strings are
replaced in place to say that `tr` and `thead` accept `auto`/`avoid` unchanged and that a kept
row FactsPDF would split fails `FPDF1302`, so no count moves.
Single-line flex rows move the eight flex rows (`flex`, `flex-direction`, `flex-wrap`,
`flex-grow`, `flex-shrink`, `flex-basis`, `align-items`, `justify-content`) from rejected to
partial (partial 252, rejected 114; their failure codes are `FPDF1201/FPDF1202/FPDF1302`, so
the `FPDF1102` search is unchanged); `gap`, `row-gap` and `column-gap` stay partial with
lengths accepted, the `display` limitation names `flex`, and the inventory note gains twelve
names (accepted CSS property names 384 to 396).
Current dataset SHA-256:
`1716424400f39f47f8745eaf461af0ea2308c6eab54fc881361b2e48d4300082`; English
layer `site/src/audit-english-flex-layout.mjs`, ordered source-text
SHA-256 `b39e89a4893e805865e50f0394d30b401619e0d4752a3515c1a077b11e9e63a5`.
