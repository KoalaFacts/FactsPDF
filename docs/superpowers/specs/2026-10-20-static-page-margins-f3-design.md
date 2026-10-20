# F3: controlled static page headers and footers

Baseline: verified F2 main `0b149f67761a26b567f09ab9dd8551410a16652a`. This is the next bounded increment in the accepted F route. The user explicitly authorizes minimal compatible API decisions after independent design review, followed by RED/GREEN, native acceptance, independent code review, normal CI/merge and main verification. Internal choices within that scope do not introduce an additional user approval gate.

## Scope and public contract

Add nullable `PdfOptions.HeaderText` and `FooterText`, default null, plus single-use CLI `--header-text value` and `--footer-text value`. The existing document options and F2 post-layout composition are sufficient; a template object, custom HTML convention or CSS Paged Media interpreter would add unnecessary semantics. No arbitrary HTML templates, JavaScript, remote resources, new document language, combined-report acceptance, database changes or publication.

Each CLI option consumes exactly the next argv token as literal text, including empty strings, `-` and values beginning `--`. Missing values are usage errors. Non-null/seen state rejects duplicates even after an empty value.

Text is literal: no HTML/entity/placeholder expansion. Null, empty and all-U+0020 values disable a field. Trim and collapse U+0020 before glyph resolution; other Unicode spaces remain literal. Reject controls and Unicode line/paragraph separators (`FPDF1305`), and invalid UTF-16 (`FPDF1304`). Scan the complete field lexically before resolving glyphs; the first invalid scalar encountered from left to right wins. Existing simple-script and explicit-font rules still apply.

Bound each raw field at 1024 UTF-16 units (`FPDF1001`). Count HTML plus both raw fields once against `MaxInputCharacters`, with long arithmetic before body parsing and after existing argument checks/resource snapshots. Discarded spaces still count, so disabled-field byte compatibility assumes these input limits pass. This deliberate bound limits repeated static text without another public resource setting.

## Placement and compatibility

Repeat each enabled field on every actual body page, including the existing one page for empty HTML. Trailing breaks create no extra pages. Use fixed normal black 10pt text with line-height 1.2 through the normal `Fonts` fallback chain, or Courier when empty. Body CSS, `FontSize` and bold/italic chains do not affect it. Missing explicit glyphs remain `FPDF1504`; no discovery, synthetic font or silent cross-role fallback.

Header and footer each occupy one centered line in the existing top/bottom margin. A nonempty footer combined with `PageNumbers` is `normalized text | N`, with an invariant decimal actual page number. The separator requires ordinary glyph support. Number-only output, errors and placement retain F2 compatibility. No total-page label, formatting/start/skip, custom font/style/position or per-page variants.

Advance width must fit `PageWidth - 2 * Margin`; any second wrapped line is rejected before appending or charging actual runs. Long text fails `FPDF1302`, never truncates/shrinks or creates pages. A footer that fits page 9 may fail at page 10. Preserve body page count, text geometry, F1 heading keeps and E2 table continuation/header repetition.

Occupied extents combine the first normal font's strut and the declared global boxes of fonts actually drawing each glyph, at its cumulative advance origin. Require 2pt physical-edge/body-area clearance and 2pt vertical separation from actual conservative body bounds. The footer baseline remains `(Margin - ascent - descent) / 2 + descent`; the header baseline is `PageHeight - (Margin - ascent - descent) / 2 - ascent`. Retain F2 epsilon and serializer rounding tolerance.

Body top/bottom bounds use text baseline plus font YMax/YMin at text size, disc Y±radius, and image/paint `PageHeight - Top` / minus height. Ignore zero-command paint boxes. Inspect only original body runs, before newly appended margin text. Fonts remain trusted; large declared boxes can reject otherwise visually plausible input. Explicit body leading/padding or document geometry must accommodate them. No automatic body-area reduction or repagination.

## Implementation and diagnostic order

Generalize the small F2 compositor to `PageMarginLayout`; keep native layout and serializers unchanged. Share its local glyph cache and exact per-font run charging. Copy a borrowed wrapped line before advancing to detect a second line. Reuse the existing font/subset/searchable-text pipeline.

Order: existing argument/cancellation/resource snapshots → raw input lengths → body parsing/layout/page/display checks → page-major header then footer → serialization/output-byte checks. Each enabled line first ensures one available command, then validates lexical content, resolves glyphs, rejects wrapping, measures geometry and appends with exact actual run charges. Thus exhausted commands precede that line's glyph errors, but a wrapped multi-font line fails geometry before its partial runs can exhaust the budget. Empty/all-ASCII-space fields consume no commands.

`MaxPages` still bounds body pages; `MaxDisplayCommands` includes every repeated fallback-font run; `MaxOutputBytes` includes the complete PDF. Cooperative cancellation and existing pre-copy/CLI atomic destination guarantees remain. Text errors have no HTML source offset. No new runtime dependency is required; external verification reuses the existing Python/Poppler/qpdf/fontTools tooling.

## Acceptance and independent review

- RED public API/CLI tests, then minimal implementation and full suite. Verify precise geometry, empty/explicit/trailing pages, literal text, normalization without a space glyph, control/UTF-16 ordering, ASCII/CJK overflow, number transitions, body ink overhang and exact resource boundaries.
- Verify normal-role fallback, full/subset embedding, unchanged body text/paint, preserved destinations and recovery; compare disabled and number-only bytes with the retained original F2 executable.
- Generate real API/CLI/Linux Native AOT PDFs with explicit fonts. Independently verify every page's content, baseline, size, centering, clearance, body painting and body raster; inspect all bounded-corpus pages. Altered text, small/large displacement, changed size and removed table paint must be rejected by the inspector.
- Independent code review, final build/full tests/Node checks, normal CI, zero unresolved comments, normal protected merge and actual main rerun remain delivery gates. No local Chrome printing/security workaround or Library upload retry.

Independent design reviewer `/root/f3_design_review` accepted this scope before RED after freezing normalization-before-resolution, wrapping-before-charge, deterministic error order, CLI literal/duplicate handling and raw input bounds. No unresolved design blocker or additional approval gate remained.
