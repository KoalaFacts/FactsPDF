# F1: opt-in heading with its first paragraph

Status: independently reviewed minimal implementation contract, within the user's explicit F1 authorization. Baseline: merged E2 `51f3e215eccda1f0bce558fca4a20ebeb1d970af`. This adds only heading-with-first-paragraph from the accepted [release scope](2026-10-11-first-usable-release-scope-design.md).

## Contract

Add standard CSS `break-after: avoid` **only on h1–h6**. Defaults remain `auto`; existing `auto` and `page` behavior and public APIs remain unchanged. This value is currently unsupported and is implemented by this change, not claimed as existing functionality. `break-before: avoid`, `break-inside`, general paragraph/container keeps, page furniture and page numbers remain out of scope. Unsupported applicability remains an explicit CSS error, including a matched declaration later overridden in the cascade. CSS-wide keywords and `!important` follow existing cascade rules; the new keep state is not implicitly inherited.

`h2 { break-after: avoid }` keeps the complete heading and the first following non-empty-text `p` on one page **when that combination can fit an empty usable page**. It moves the combination once when it cannot fit remaining space. If it already fits remaining space, placement and PDF bytes match the same document without `avoid` (including inline styled runs). If the entire combination cannot fit an empty usable page, relax the keep and use existing progressive line pagination, without a retry loop or a new keep-specific error. Existing individual line/geometry/resource errors remain applicable.

Scope is direct siblings in one ordinary block container (including body/root). A maximal consecutive chain of headings participates only while each heading has `break-after: avoid`; the chain ends at its first non-empty-text paragraph. Empty/whitespace/br-only paragraphs between them are included with their existing margins, padding, borders and line boxes. An intervening unmarked heading, image, table, list, anonymous text, different nested container or end-of-container ends the candidate without searching across it. A heading with no eligible following paragraph follows existing layout. Headings anywhere within ol/ul/li are excluded, including nested containers, because list markers change measurement; an explicit avoid declaration there is rejected. This is a bounded subset, not general CSS fragmentation.

Any explicit `page` at an internal boundary cancels the candidate; `page` on the first heading or pending from preceding content uses existing explicit-break placement. A final paragraph's break-after:page still applies after it. Avoid never overrides an explicit break. Empty paragraphs carrying explicit breaks also interrupt the keep.

The candidate includes its own complete paragraph decorations and internal collapsed paragraph margins, but not the final paragraph's external trailing margin. Existing ancestor movement/decoration rules continue to apply. On an automatic move, discard preceding closed spacing and the first heading's leading margin exactly like existing automatic paragraph movement; retain required unconsumed ancestor top decoration. If that required leading decoration makes the group too tall, relax the keep rather than repeatedly moving it. Ancestor closing-decoration limitations remain existing behavior.

## Implementation boundary

- Add an internal non-inherited keep flag to computed text style for CSS/cascade handling; strip it from glyph-run styles so a layout hint cannot change text grouping/PDF bytes. No public options.
- In lazy `BlockLayout` traversal, identify sibling candidates before their first `BeginBlock`, emit one internal lookahead step, and suppress rescanning the rest of the same candidate, including failed chains without a final paragraph, explicit-break cancellations and oversized chains. Do not retry shorter suffixes of an oversized maximal chain. Retain index ranges into the existing tree, not a second document.
- Share existing block box resolution and text wrapping. Preflight stops accumulating geometry and subsequent paragraphs as soon as the combination exceeds one usable page; it does not retain text/paint display commands. Wrap may validate the entire current paragraph before yielding its first line, so this is not a one-page CPU bound. Keep existing input limits and cancellation checks. Track prospective display commands locally without charging the real budget; check remaining budget before wrapping subsequent text paragraphs. On budget exhaustion or FactsPdfException during measurement, abandon keep and let ordinary layout preserve diagnostic precedence. Cancellation must propagate. No whole-document layout replay.
- `TextLayout` decides whether to move before opening the heading's box, using current space and the same unconsumed-ancestor leading-space calculation as existing automatic moves. Actual placement still uses the existing paragraph/box path. No table or PDF writer change.

## Acceptance

1. Insufficient page tail: complete heading + multi-line first paragraph move together; explicit authored geometry.
2. Fits here: PDF bytes equal to no-avoid equivalent; default documents remain byte-identical to baseline, including mixed inline roles/styles.
3. Oversized heading/paragraph/heading-chain: finite progressive pagination, no repeated empty pages; exact page quota and existing line-too-high errors.
4. Explicit before/after page breaks take priority, including empty paragraph boundaries.
5. Multiple marked headings, unmarked interruption, empty/whitespace/br-only paragraphs, no following paragraph, nested-container boundary.
6. Width/wrapping, paragraph/ancestor padding/borders, font roles, CSS cascade/wide keywords, exact display/page/output limits, cancellation and unchanged destinations.
7. Real native-engine API/CLI/Linux Native AOT PDFs with fixed inputs and explicit fonts: qpdf, extracted text/order and page geometry, entry byte equality, every distinct page raster inspected. No local Chrome printing or Library upload.
8. Independent design and code review, RED→GREEN tests, full Release build/tests, normal CI and zero unresolved comments before normal merge; verify actual merged main.

## Standards and decisions

`avoid` is the standard break discouragement value; forced breaks take precedence. Reference: https://www.w3.org/TR/css-break-3/#break-between and https://www.w3.org/TR/css-break-3/#unforced-breaks. F1 deliberately documents its heading/paragraph subset and best-effort overflow relaxation.

Alternatives rejected: automatic keeps on every heading would change defaults; a new public option/custom attribute would expand the interface; general before/after/inside avoidance would exceed F1. The selected existing-property value has the lowest public-surface impact. No database/migration, table expansion, new document language or release work.

Independent design review completed 2026-10-20; its four resource, scan-complexity and list-scope findings are incorporated above.
