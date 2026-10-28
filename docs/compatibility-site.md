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
2 not applicable**. The byte-exact UTF-8 JSON SHA-256 is `14914a5685868293b0fefb09adba381d8ef981e85bfe01fe76348e49d9565776`.

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
