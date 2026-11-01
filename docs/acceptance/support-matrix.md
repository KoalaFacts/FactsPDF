## Current-source scoped word-break PDF glyph wrapping

`word-break:normal | break-all` accepts explicit p/heading (including
fixed-column table-cell p) in the current source. The measured PDF line
breaker actually fills remaining line width inside long identifiers, unlike
`overflow-wrap:break-word`'s emergency-only behavior. Standard per-property
cascade/CSS-wide values, `white-space:nowrap`, font glyph availability,
pagination and protected destination errors remain enforced. Not Unicode
shaping, grapheme-safe breaks, keep-all, hyphenation, inline styles or full
CSS Text.

Native `verify_native_feature_paths.py` compares the
[48-record report](../../examples/report-word-break-identifiers.html)
with an [independently authored explicit-break oracle](../../examples/report-word-break-identifiers-oracle.html),
including exact PDF bytes, qpdf integrity, Poppler per-page searchable text,
strict invalid input and restored output. `WordBreakTests.cs` covers
managed PDF geometry/bytes and typed errors. Source ac6cec7000b308b403dd95abcd14441240b1f791; no old binary
relabel, customer grant or human approval.

## Current-source fixed-height business-report panel rendering

`height:auto` or bounded nonnegative `pt/px/mm/cm/in/em/rem` (and
exact unitless zero) on ordinary div/section/article/blockquote outside
lists/tables now determines actual PDF painted box height and following
text location, under the established CSS cascade and `box-sizing`.
The larger `min-height` remains the lower bound. A fixed-height panel
must fit its content on one page: overflow, cross-page continuation,
percentages, invalid values and unsupported elements fail explicitly
without replacing a prior destination.

`ReportPanelFixedHeightTests.cs`, the 40-card
[report](../../examples/report-fixed-height-cards.html),
[layout control](../../examples/report-fixed-height-cards-oracle.html)
and the shared Native AOT/qpdf/Poppler gate check PDF bytes, measured
glyph/box positioning, pagination and protected-output recovery.
This is source capability at `9f141fbc33bb2c4a3764b12c6c8c52b8c2bd00bc`,
not full CSS Sizing, an installable package, approved pages or a
customer distribution grant. Final-head CI and independent review
must be verified separately.

## Current-source bounded report-section page boundaries

The source at `67566764419232585632d8ea6f2106aeeeff2f70`
allows explicit `break-before:page` and `break-after:page`
on ordinary div/section/article/blockquote panels. Scoped legacy
`page-break-before/after:always` aliases and `auto` target the same
cascade slots, including `!important`, inheritance/reset and nested
panel fragments. `SectionPageBreakTests.cs` checks actual PDF paint
and text per page, two boundaries without an empty middle page, no phantom
first page, hidden panels, and protected input errors.
`verify_native_feature_paths.py` compares three actual CLI PDF pages
and searchable qpdf/Poppler output from the authored source
[report](../../examples/report-section-page-breaks.html) and a separate
[modern-break oracle](../../examples/report-section-page-breaks-oracle.html),
including protected PDF recovery after rejected input.
Not break-inside, avoid on panels, general keep, Paged Media, table/list/
inline or unsupported left/right page selection. Exact-head CI, independent
review, private candidate, customer rights and human page approvals are
separate; no previous binary is relabelled.

## Current-source independent corner-radius PDF rendering

The four physical corner longhands compose with 1–4 value `border-radius`
under per-side cascade, `!important`, CSS-wide resets and explicit
inheritance. Each accepts one bounded physical value. Real PDF Bezier
background and uniform solid border-ring paint follows the original
pagination/layout path. `RoundedCornerLonghandTests.cs` asserts actual
PDF bytes, per-corner source order, independent inheritance and errors;
the native verifier compares the 50-card
[longhand report](../../examples/report-corner-longhands.html) byte-for-byte
to the [existing independent shorthand report](../../examples/report-asymmetric-rounded-panels.html),
with Poppler/qpdf, original-output protection and real page continuations.
No ellipse/percent/em/rem radii, rounded table/list/image/inline boxes,
new installable candidate, human approval or distribution authorization.

## Current-source bounded minimum-height card geometry

`min-height` on `div/section/article/blockquote` outside lists/tables
reserves real PDF-height space and painted panel area, including empty cards.
Positive physical/em/rem lengths and exact zero, non-inheritance, CSS-wide
reset/explicit inheritance and `!important` cascade follow the scoped box
model; `border-box` includes padding/borders, `content-box` adds them.
Actual glyph positions, panel rectangles, following rows, page relocation and
multi-page natural content are covered by `ReportPanelMinHeightTests.cs`,
the inspection example and 40-card Native AOT qpdf/Poppler physical-padding
oracle in `verify_native_feature_paths.py`. Indefinite percentages, auto,
unrestricted height/max-height, table/list/inline targets and unplaceable geometry
explicitly fail with protected original output. No general CSS Sizing,
human reference approval, public/private candidate rebadge or customer licence.

## Current-source rounded report-card PDF painting

`border-radius` now accepts one to four physical corner lengths (each zero
or bounded 0.25–72pt) clockwise TL/TR/BR/BL and shapes actual opaque
PDF backgrounds and uniform solid borders on ordinary blocks only; first/top
and last/bottom page fragment caps are rounded, middle continuation sides
are square. `RoundedReportPanelTests.cs`, the 54-card report, separate
16px/12pt oracle and Linux/Windows Native AOT/Poppler witness validate real
curves, text positions, pagination, legacy zero-radius PDF bytes and failure
recovery. Not asymmetric/patterned borders, percentages, two-axis elliptical radii
or table/image/list/inline boxes. Source-only; current-head CI, independent
review, retained binary, human reference approval and customer grant are
separate gates.

Current-source `td/th` `vertical-align:top/middle/bottom` is a
bounded whole-row PDF positioning feature, not general CSS table layout.
`TableVerticalAlignmentTests.cs`, the 40-row report, its separate
physical-padding control and the Native AOT/Poppler feature verifier
cover actual text/image geometry, repeated whole rows and protected
failure recovery. Split rows reject middle/bottom. Exact-head CI and
independent review, human approval, private binary and publication
remain separate gates.

# Acceptance support matrix — evidence, not marketing

Current-source `blockquote` is a bounded genuine PDF block container with
default 8pt vertical and 24pt horizontal margins, inherited text, real
borders/backgrounds/padding and continued page fragments. Its 44-entry
report and separately authored physical `div` oracle, source NUnit
`BlockquoteReportTests.cs` and Native AOT `verify_stylesheets.py` gate
actual PDF bytes, text, pagination and error recovery. It is not browser
citation metadata, a `cite` attribute, a Tagged PDF semantic, a human
approved output or a rebuilt private candidate. Unsupported input still
fails explicitly. Exact-head CI and independent review must precede
normal merge.

Current-source `text-indent` is limited to explicit paragraphs/headings outside tables. Actual first-line glyph x and wrapping differ, while later lines/page fragments and painted boxes keep their original x. `TextIndentTests.cs` and `verify_stylesheets.py` cover point/physical/percent parity, rejection and output preservation. Evidence is source-specific, not a new retained candidate or a human-approved PDF comparison.

Current-source parent-relative `font-size` percentages and `em` values
resolve against the actual computed parent (not a default heading multiplier).
`RelativeFontSizingTests.cs` and the native print report check explicit-point
PDF byte equivalence, multiline glyph layout and strict failing-output
recovery; not `rem`, general relative lengths or a released candidate.
These checks require exact-head CI and independent review before merger.

Current-source opaque RGB-function additions have a separate real PDF and Native AOT hex-byte-equivalence/rejection path in `OpaqueRgbCssTests.cs` and `scripts/verify_stylesheets.py`; they do not re-approve any historical human reference pages, re-label previously retained binaries, or establish full CSS colour/alpha support.

Engine comparison baseline: M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`. Source SHA and every actual outcome come from each run's manifest/environment/report.

| Cases | Contract | Completion meaning |
|---|---|---|
| B00 | ASCII text control via CLI/API | Independent text/structure/bounds; original native reference approved |
| B01 | Bilingual brief, headings and information panel | Original native reference approved; new captures require their own review |
| B02-10/30/100 | Ordered synthetic records as paragraphs | Not table support; every ID and sentence checked |
| B03 | Three explicit report sections inside a continued panel | Real existing block fragmentation; observed pages reported |
| N-wide/N-zero | Unbreakable/zero-width text | FPDF1302 before replacing existing bytes, CLI/API |
| N-pages/input/elements/depth | Existing resource limits | Exact FPDF1303/1001/1002/1003, public API probe only |
| N-css-characters/declarations | Existing CSS work budgets | FPDF1205, public API probe only |
| N-display/output | Command/byte budgets | FPDF1401, public API probe only |
| N-utf16-high/low | Runtime malformed UTF-16 | FPDF1304 without replacement or caller-output writes |
| T-bold | C1 explicit normal/bold/italic/bold-italic; separate actual font and cross-entry evidence | Legacy corpus supplies only normal fonts and now checks missing-role FPDF1507; use C1 evidence for positive support |
| T-list | Positive decimal/disc two-level smoke, CLI/API bytes must match | Candidate only; full wrapping/pagination/geometry evidence is in C2 |
| T-table-10/30/100 | Legacy malformed table probes (missing explicit columns/sections) | FPDF1101 remains expected; positive E1/E2/F4 evidence is separate |
| T-jpeg | Defined LoadImage resource API; legacy probe has no bound image and checks FPDF1604 | Full positive D1 evidence is separate; rejection is not positive credit |
| T-page-furniture/T-heading-keep/T-complete-report | Original harness placeholders have no bound executable inputs | Legacy `blocked-design` describes these unbound entries, not current engine support; use separate F1–F4 evidence below |

The [approval registry](reference-reviews.json) records **six human-approved original native baselines (ten pages)** with durable PDF/text/image hashes and the original reviewed source. A green new candidate-evidence job means capture and independent checks completed; it does not transfer those approvals to changed outputs, approve all nine targets, or complete the Developer Preview. Reports keep target totals, missing approval, and unapproved performance budgets visible. Missing tools or wrong target diagnostics are errors, not skipped support.

T-bold's [explicit font style implementation and evidence](../font-styles.md) preserves ordered per-character fallback and adds four independent roles. [C2 lists](../lists.md) have their own real report and cross-entry verification; the legacy smoke does not import full C2 evidence or user reference approval automatically. The original nine target cases remain the denominator. Final exact-head CI/review gates must be recorded for each stage; neither expected rejection nor one successful smoke completes all product targets.

[D1 explicit JPEG images](../images.md) have their own real image report and per-entry validation. The legacy harness does not import D1 evidence or user page approval automatically.

## Separate positive evidence

These implemented paths have independent executable evidence. This table neither changes the original nine-target denominator nor promotes malformed/unbound legacy entries to `verified`.

| Capability | Executable positive evidence | Approval boundary |
|---|---|---|
| Computed `em/rem` report text spacing | [Relative CSS contract](../css-stylesheets.md#current-source-font-relative-report-text-spacing), `tests/FactsPDF.Tests/FontRelativeTextSpacingTests.cs`, [72-entry sample](../../examples/report-relative-text-spacing.html) and independent [point oracle](../../examples/report-relative-text-spacing-oracle.html), `scripts/verify_stylesheets.py` | Actual first-line indentation, Courier/TrueType glyph advances, wrapping, multi-page searchable PDF, byte equality to fixed points and preserved output on failure; negative/percent letter/word spacing, hanging indent and general CSS Values unsupported. Exact-head CI/independent review required; not retained candidate or human approval. |
| Bounded ASCII report `text-transform` | [CSS case contract](../css-stylesheets.md#current-source-ascii-report-text-transform), `tests/FactsPDF.Tests/TextTransformTests.cs`, `examples/report-case-labels.html` | Actual pre-font-selection ASCII uppercase/lowercase and token-initial `capitalize` in PDF glyph runs and searchable text, spans across word boundaries, explicit inline reset, Unicode-cased rejection, pagination, 54-entry authored-case PDF/Native AOT parity and protected-output failures; no Unicode word segmentation or new released software |
| Bounded positive report `letter-spacing` | [Tracking contract](../css-stylesheets.md#current-source-bounded-letter-spacing), `tests/FactsPDF.Tests/LetterSpacingTests.cs`, `examples/report-print-spacing.html` | True glyph advance in ASCII/TrueType PDF, wraps/pagination/decoration geometry, native physical-unit PDF parity and strict preserved-output failures; no negative tracking or full CSS Text. Final source head requires genuine separate review and checks. |
| Soft-wrapped report `text-align:justify` | [Scoped contract](../css-stylesheets.md#current-source-soft-wrapped-report-justification), `tests/FactsPDF.Tests/JustifiedTextTests.cs`, `examples/report-print-spacing.html` | Real auto-wrapped first/middle lines fill their content width via Courier/TrueType PDF `TJ` word spacing; last and explicit-break lines remain ragged, insets/inheritance/pagination/negative recovery; Native AOT PDF checks; not RTL/CJK/full CSS Text |
| Bounded positive report `word-spacing` | [Scoped contract](../css-stylesheets.md#current-source-bounded-word-spacing), `tests/FactsPDF.Tests/WordSpacingTests.cs`, `examples/report-print-spacing.html` | Measured U+0020 glyph advances, actual PDF `TJ` on Courier/CID fonts, wrap, inheritance, legacy zero-byte parity, rejecting unsafe values and destination recovery; Linux Native AOT PDF check |

| Current-source bounded HTML `pre` and `white-space:pre` | [Scoped contract](../css-stylesheets.md#current-source-preformatted-audit-blocks), `tests/FactsPDF.Tests/PreformattedReportTests.cs`, [72-row report](../../examples/report-preformatted-audit.html), [independently authored pre-wrap oracle](../../examples/report-preformatted-audit-oracle.html), `scripts/verify_native_feature_paths.py` | Actual authored spaces, breaks, no soft wraps, measured Courier/TrueType glyphs, multipage report, CSS cascade, Native AOT/Poppler x positions and protected-output negative recovery. Source commit d8d1a9e0; exact head CI, independent review, human approval and public/private release remain distinct. |
| Bounded preserved-space `white-space:pre-wrap` | `tests/FactsPDF.Tests/WhiteSpacePreWrapTests.cs`, [72-note report](../../examples/report-preserved-space-notes.html), [independently authored collapsed-space control](../../examples/report-preserved-space-notes-oracle.html), `scripts/verify_native_feature_paths.py` | Real PDF spaces and Poppler X offsets, CRLF source breaks, TrueType glyph metrics, soft wraps/pagination, table cells, CSS cascade and strict protected-output failures. Tabs and broad CSS Text intentionally rejected; exact new-source CI/independent review and human page approval remain separate. |
| Whole-paragraph/heading `white-space:pre-line` | [Multiline note scope](../css-stylesheets.md#current-source-multiline-report-notes), `tests/FactsPDF.Tests/WhiteSpacePreLineTests.cs`, [72-entry report](../../examples/report-pre-line-notes.html) and [explicit br oracle](../../examples/report-pre-line-notes-oracle.html), `scripts/verify_native_feature_paths.py` | Actual CRLF/CR/LF/FF segment-break painting, collapsing spaces, trailing-break handling, TrueType PDF byte parity, real paginated Native AOT/Poppler text positions and strict error/output recovery. Source commit `6ab5d2c8ff3c10c76e928b6ad192d9ac46965c89`; full CSS Text, inline/container declarations and public binary release remain unsupported. |
| Whole-paragraph/heading `white-space:nowrap` | [CSS scope](../css-stylesheets.md#current-source-nonwrapping-report-labels), `tests/FactsPDF.Tests/WhiteSpaceNowrapTests.cs`, `examples/report-print-spacing.html` | Real PDF one-line/explicit-break geometry, overflow `FPDF1302` with protected destination, stylesheet cascade and unsupported-value/element recovery; corresponding real Native AOT checks and independent review required for this new head. |
| Bounded ordinary report min/max widths | [Scoped CSS contract](../css-stylesheets.md#current-source-bounded-report-minmax-widths), `tests/FactsPDF.Tests/MinMaxWidthTests.cs`, [printable report](../../examples/report-width-constraints.html) and independent authored oracle | Resolved content/border-box constraints on automatic and explicit widths, actual horizontal centring, percentage containing width, page-fragment painting, strict unsupported cases and preserved PDF target; Linux Native AOT actual-PDF equality/recovery; precise head review and CI required before merge. |
| Report `box-sizing:border-box` | [CSS scope](../css-stylesheets.md#current-source-border-box-report-panel-widths), `tests/FactsPDF.Tests/BorderBoxSizingTests.cs` | Real width, wrapping, auto centring, multi-page painted fragments and destination recovery with an independently authored content-box PDF; real Native AOT gate and independent review still required. |
| Root-relative `rem` report font/leading | [Scoped root-relative CSS](../css-stylesheets.md#current-source-root-relative-report-typography), `tests/FactsPDF.Tests/RootRelativeTypographyTests.cs`, `examples/report-root-relative-typography.html`, `scripts/verify_stylesheets.py` | Actual computed `html` root and fallback; separate `html` font self-reference and line-height behavior; multi-page Courier/TrueType and Native AOT point-projection PDF equivalence, searchable content and protected-output failures. Only `font-size`/`line-height`; exact-head CI, independent review, customer authorisation and human approval remain separate. |
| Root-relative `rem` report box lengths | [Bounded box contract](../css-stylesheets.md#current-source-root-relative-report-box-lengths), `tests/FactsPDF.Tests/RootRelativeBoxLengthTests.cs`, [69-record source report](../../examples/report-rem-box-lengths.html) and independent points-only oracle, `scripts/verify_stylesheets.py` | Real computed `html` scale drives `width`, `min-width`, `max-width` and padding on ordinary blocks; checks border-box, auto centring, text wrap, multi-page backgrounds, actual Native AOT PDF-byte equivalence, strict unsupported-value/element rejection and unchanged destination. This original width/padding slice predates the separate root-margin feature; general container margins, border/table/image or full CSS Values rem remain unsupported; not in retained private candidate |
| Computed percent/em/print-unit line-height | [CSS contract](../css-stylesheets.md#current-source-computed-report-line-height), `tests/FactsPDF.Tests/LineHeightUnitsTests.cs`, `examples/report-print-spacing.html` | Nested fixed-vs-unitless inheritance, actual PDF baselines and byte-equivalent independent point-oracles, invalid destination recovery; exact-head Native AOT and review required. |
| Bounded colored and patterned text decorations | [CSS scope](../css-stylesheets.md#current-source-colored-and-patterned-report-text-decorations), `tests/FactsPDF.Tests/TextDecorationTests.cs`, `tests/FactsPDF.Tests/TextDecorationPaintTests.cs`, [60-entry report](../../examples/report-text-decoration-paint.html) | Real measured PDF underline/overline/line-through glyph paths, optional currentcolor/opaque RGB and solid/dashed/dotted clipped strokes, inline colour and cascade, TrueType/Courier, multipage text, API/CLI byte parity, untouched legacy solid path and protected-output negative/budget recovery. One decoration line only; no wavy/double/thickness/alpha/full browser inheritance. Exact-head CI, separate independent review and human PDF approval remain distinct. |
| Root-relative paragraph/heading `rem` margins | [Scoped contract](../css-stylesheets.md#current-source-root-relative-paragraph-margins), `tests/FactsPDF.Tests/RootRelativeMarginTests.cs`, [32-entry source report](../../examples/report-rem-margins.html), independent [points oracle](../../examples/report-rem-margins-oracle.html), `scripts/verify_stylesheets.py` | Root-computed nonnegative `rem` sides, actual horizontal wrap/box paint, vertical spacing and pagination, mixed auto, cascade and FPDF1201/1202/1302 protected-output recovery; genuine Native AOT PDF parity, final-head CI and independent review separately required |
| Own-font-relative `em` report boxes and margins | [Scoped CSS contract](../css-stylesheets.md#current-source-element-relative-report-box-and-margin-lengths), `tests/FactsPDF.Tests/ElementRelativeBoxLengthTests.cs`, [32-entry em report](../../examples/report-em-layout.html) with independent [point oracle](../../examples/report-em-layout-oracle.html), `scripts/verify_stylesheets.py` | Own final computed font, real width/min/max/padding/margins, painted PDF geometry and continuation, valid/invalid cascade, bounded FPDF1201/1202/1302 preservation; exact-head managed/Native AOT CI and independent review required. Source only; not a retained candidate or human-approved reference. |
| Bounded paragraph/heading `overflow-wrap:break-word` | [CSS scope](../css-stylesheets.md#current-source-bounded-emergency-wrapping-for-report-identifiers), `tests/FactsPDF.Tests/OverflowWrapTests.cs`, [32-entry report](../../examples/report-overflow-wrap.html), [manually broken oracle](../../examples/report-overflow-wrap-oracle.html) and `scripts/verify_stylesheets.py` | Measured emergency line splits, legacy byte equivalence for ordinary words, actual Native AOT PDF parity/page progression/searchable content, strict FPDF1201/1202/1302 recovery. No inline/container or direct table-element declarations, `anywhere` or general CSS Text; a flat `p` inside a fixed table cell can opt in. Requires exact-head CI, independent review and audit update; source-only until accepted. |
| Horizontal margins and centred report panels | [Bounded panel CSS contract](../css-stylesheets.md#current-source-centred-report-panels), `tests/FactsPDF.Tests/ReportPanelMarginTests.cs`, [50-entry source report](../../examples/report-panel-margins.html), [independent point oracle](../../examples/report-panel-margins-oracle.html), `scripts/verify_stylesheets.py` | Real `div`/`section`/`article` horizontal margins and `margin:0 auto`, border-box/max-width/pagination and printed PDF X/width, root-`rem`/own-font-`em`, separately bounded nonnegative vertical panel spacing, unsupported list/body/table/inline exclusions and protected-output errors; exact-head managed/Native AOT and independent review required. Source only, not retained preview or customer release. |
| Non-collapsing vertical report-panel margins | [Scoped PDF CSS](../css-stylesheets.md#current-source-real-vertical-report-panel-margins), `tests/FactsPDF.Tests/ReportPanelVerticalMarginTests.cs`, [50-entry report](../../examples/report-panel-vertical-margins.html), [independent spacer oracle](../../examples/report-panel-vertical-margins-oracle.html), `scripts/verify_stylesheets.py` | Real top/bottom spacing on div/section/article with nested/continued painted boxes, root-rem/own-em, cascade, PDF byte parity and preserved output on errors; vertical auto/percent/negative, general margins and collapse unsupported. Exact-head CI, independent review and human page approval separate. |
| Four-sided report margins, horizontal auto centring and print lengths | [Scoped CSS](../css-stylesheets.md), `tests/FactsPDF.Tests/HorizontalParagraphMarginTests.cs`, `tests/FactsPDF.Tests/HorizontalAutoMarginTests.cs`, `examples/report-print-spacing.html` | Bounded explicit-width auto sides, wrapping, painted geometry, heading keeps, cell rejection and safe recovery; exact-head CI/native and independent review still required |
| Bounded CSS list marker kinds | [CSS/list contract](../css-stylesheets.md#current-source-bounded-list-marker-types), `tests/FactsPDF.Tests/ListMarkerStyleTests.cs`, [74-entry report](../../examples/report-list-marker-styles.html), [80-entry vector report](../../examples/report-list-vector-shapes.html), `scripts/verify_lists.py` | Actual source HTML → paginated PDF text marker glyphs and geometry, CSS cascade/important/inheritance, alphabetic/Roman sequence, vector disc/circle/square and marker-free items, managed and Linux Native AOT byte parity with independent expected labels, plus 80-item Linux/Windows native PDF ring/rectangle and Poppler position evidence, and strict FPDF1201/1202/1504/1401 recovery. No custom CSS counters, general list styling, new installable software, release, human PDF approval or commercial grant. Final-head CI and independent review gate merge. |
| Fixed columns, whole rows, repeated headers | [E1/E2 tables](../tables.md), `scripts/verify_table_pagination.py` | Separate from malformed T-table rejection |
| LTR logical side margin/padding longhands (eight spellings) | [Exact CSS contract](../css-stylesheets.md#individual-ltr-logical-marginpadding-sides-eight-names), `tests/FactsPDF.Tests/LogicalReportLonghandTests.cs`, [40-entry report](../../examples/report-logical-longhands.html) and [independent physical PDF oracle](../../examples/report-logical-longhands-oracle.html), `scripts/verify_stylesheets.py` | Individual per-side cascade and !important/CSS-wide behavior; true glyph X, panel paint, page continuation, CLI and Native AOT byte-equivalent PDF, qpdf/searchable text and original-file preservation. Strict physical element/value limits, LTR only; new-source CI/independent review separate from private candidate and human PDF approval. |
| Bounded report `display:none` / `display:block` | [Scoped source](../css-stylesheets.md#current-source-bounded-report-display-suppression), `tests/FactsPDF.Tests/ReportDisplayTests.cs`, [40-entry report](../../examples/report-display-visibility.html), [visible-only oracle](../../examples/report-display-visibility-oracle.html), `scripts/verify_stylesheets.py` | Source and Native AOT verification of actual subtree suppression before PDF layout, same visible text/geometry/pagination/bytes, cascade/important, CLI/API, and strict malformed value/resource/element recovery. Excludes CSS-wide display, inline/flex/grid, HTML `hidden`, lists/tables/images and public release. Exact-head CI and independent review are separate gates. |
| Standalone HTML horizontal report rules | [Scoped separator contract](../css-stylesheets.md#current-source-html-horizontal-report-rules), `tests/FactsPDF.Tests/HorizontalRuleTests.cs`, [44-entry report](../../examples/report-horizontal-rules.html), [independent painted-box oracle](../../examples/report-horizontal-rules-oracle.html), `scripts/verify_stylesheets.py` | Actual 1pt current-colour rule, bounded CSS box/colour/cascade, auto centring, page relocation, searchable text, native PDF-byte parity, limits and protected-output errors; no list/table/paragraph/inline nesting, arbitrary height, new software candidate or human page approval. |
| Scoped LTR `text-align:start/end` for business report text | [Exact CSS contract](../css-stylesheets.md#current-source-ltr-text-align-startend), `tests/FactsPDF.Tests/LogicalTextAlignmentTests.cs`, [46-entry report](../../examples/report-logical-text-align.html) and [physical LTR oracle](../../examples/report-logical-text-align-oracle.html), `scripts/verify_stylesheets.py` | Actual font-measured PDF X coordinates, selector/important/inheritance, TrueType, multipage byte parity, native searchable markers, invalid direction/value/inline recovery. LTR horizontal only; no RTL or vertical writing. Source commit 748867bb7ece222cc552a4d315143572fb1bd642; no new private/public binary, licensing grant or human PDF approval. |
| Static PNG color and transparency | [PNG image resources](../images.md), `scripts/verify_png.py` | Scoped non-interlaced formats; separate engineering evidence |
| Image-only table cells and repeated image headers | [Branded table composition](../tables.md#image-only-cells-and-branded-layouts), `scripts/verify_table_images.py` | Literal geometry/pixels, reuse and actual mutated-PDF rejection; new pages pending reference review |
| Column spans and ordered paragraph/image cells | [Rich and spanning cells](../tables.md#rich-and-spanning-cells), `scripts/verify_table_rich_cells.py` | Authored geometry, margin and border witnesses; new pages pending reference review |
| Oversized body rows with independent cell continuation | [Long descriptions](../tables.md#long-descriptions-across-pages), `scripts/verify_table_row_fragments.py` | Actual text/image/slice geometry, terminal fit and mutation evidence; short tables and fresh-page-fit rows remain whole; new pages pending reference review |
| Heading kept with following paragraph | [F1 heading keep](../heading-pagination.md), `scripts/verify_heading_keep.py` | Existing opt-in paragraph contract |
| Page counters | [F2 page numbers](../page-numbers.md), `scripts/verify_page_numbers.py` | Existing body geometry remains unchanged |
| Static header/footer and counter composition | [F3 page margins](../page-margins.md), `scripts/verify_page_margins.py` | Explicit literal text; existing margins |
| Styled six-region page furniture, total pages and first-page replacement | [Structured page furniture](../page-furniture-cli.md), `scripts/verify_native_feature_paths.py`, [exact-main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/40283254200) | Real .NET/managed CLI and Linux/Windows Native AOT evidence; HTML header/footer templates unsupported; new pages pending human reference review |
| Combined bilingual 10/30/100-record reports | [F4 executable reports and independent checks](../combined-report.md) | Engineering acceptance; new pages pending user reference review |
| Original styled-CJK input identity and real styles | [Original F4/C1 PR-head execution](https://github.com/KoalaFacts/FactsPDF/actions/runs/40497304548) and [evidence reconciliation](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6448294820) | F4 10/30/100 inputs match Unicode examples and use genuine Chinese regular/bold; **separate C1** exercises four distinct Chinese styles. API/managed/native exact-head byte parity; not a user's changed-page approval |
| Sealed legacy PDF replay | [Managed/native scoped executions](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6443259610) | 52/52 original PDFs (311 pages) byte-identical at previously tested managed and Linux native revisions; no inference of fresh exact-`47bc9791` replay |
| Private installable preview candidate | [Exact merged-main CI](https://github.com/KoalaFacts/FactsPDF/actions/runs/40561057964) and [private retention receipt](https://github.com/KoalaFacts/FactsPDF/issues/51#issuecomment-6452146222) | Five exact-source components retained/read back privately; real Linux native first-use and safe `FPDF1505` recovery; no public/customer distribution, official signature attestation, page approval or usage-rights grant |

F4 configures four font-role chains for its combined report, but the **original Chinese spans exercise regular and bold only**; the separate C1 styles report supplies positive evidence for all four genuine Chinese faces. F4 also combines nested lists, explicit JPEGs, fixed-column tables, whole-row pagination/repeated headers, heading keeps, page numbers and static page text. Its positive evidence is additional to the original 6 baseline / 12 negative / 9 target cases. New combination PDFs are not automatically frozen as human truth. Current-head user reference approval, remaining release gates, fixed hardware and numerical performance budgets stay separate and incomplete.


### Numeric font-weight source validation (29 October 2026)

The C1 four-role font implementation now also resolves exact CSS numeric hundred
weights 100–900: 100–500 selects the explicit normal chain and 600–900 the
explicit bold chain; italic composition remains the existing independent role.
The `NumericFontWeightTests` real PDF/cascade/page/failure regression suite and
64-entry report against a separately authored 400/700 role oracle are recorded
at source `7fc2f9c2b3c6c434bd8832afa7b383e02f3ee750`. The Native AOT verifier checks a real report,
two embedded faces, searchable text, pagination and protected output for
unsupported/missing-role input. This is supplemental source evidence, not a
new T-bold human approval, private candidate, customer delivery or NuGet release.


## Current-source dashed/dotted business-report borders (2026-11-01)

Report box borders now support dashed/dotted PDF strokes with real measured
thickness, per-side colour/cascade, page-sliced edges and bounded display
commands. This is implemented source-only at `4b5d6da3553579dad75dd00bfb041e1f3c200d90`;
the 76-record report and `ReportPatternedBorderTests` / Native AOT
`verify_native_feature_paths.py` exercise actual paths and protected
failure recovery. Solid rectangles retain original byte output. Unsupported
border styles, round corners, complex effects and full CSS Borders remain
outside scope. Actual exact-head CI, independent review, package retention,
rights and human reference-page approval are **separate gates**, not
asserted by this source audit.

## Current-source list numbering and one-token shorthand (2026-11-01)

Source `bea39e6d6a55f8a9e77ef8156bf6ca6529494738` implements ordered `start` and direct `li value`
within 1–999999, reversed counting with bounded positive range, plus
single-marker-kind `list-style` shorthand. `ReportListControlsTests.cs`
and the [77-entry report](../../examples/report-list-continuations.html)
are checked for PDF bytes, source errors, numbering, page continuity,
font/vector markers and cancellation/recovery. The existing Linux/Windows
native-feature workflow additionally uses independently enumerated
ordinal expectations with Poppler text/position extraction.
Unsupported HTML attributes/targets, numbering underflow, CSS image
and multi-token position shorthands fail rather than render incorrectly.
Exact-head CI and independent review are separate required gates; source
tests cannot approve new human reference pages or relabel the historic
private `0.1.0-preview.1` binary.
