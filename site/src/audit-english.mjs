// English display translations in collectAuditStrings() order.
// The source-text SHA-256 is verified before any translation is displayed.
// Raw support data, audit dates, statuses, URLs and code examples are untouched.
export const sourceDigest = "bf91f70292a94b15feffd522a5746264e91b02aec4861b6ce9ca395dbd33b8f3";
export const englishAudit = Object.freeze(`The current document reader does not accept this element.
There is no div/span fallback: conversion explicitly rejects it. Correct rejection does not count as support.
Semantic rejection
Shared core (CLI / .NET)
Not assigned
Source inspection; tests were not rerun in this audit.
Nested block containers with width, padding, backgrounds, borders and page continuation.
Does not implement the element's full HTML semantics or Tagged PDF; limited to the controlled document tree.
Semantics / rendering
Inline container with an absolute bold default. Weight and style inherit independently; nesting selects bold-italic, and author CSS can override the default.
Requires explicitly supplied matching font chains. No synthetic styles, system discovery or automatic downgrade; no block children or full browser font matching.
Explicit style selection / actual glyph rendering
C1 explicit font styles (implemented subset)
Local core/CLI regressions and actual four-style glyph/page checks. Final cross-platform PR CI is recorded separately; this is not approval of the entire preview.
Bounded block containers, inheritance, nesting and painting.
No complete browser body/canvas background propagation; only the listed properties are supported.
Explicit line breaks, including consecutive blank lines.
No styling of br or clear attribute; the element is implemented only within this limited scope.
E tables (bounded planned scope)
Void col elements explicitly define fixed tracks with positive pt/px/% widths; their sum must match the table content width within 0.001pt.
Only width CSS is accepted. Width cannot be omitted, auto or zero. No HTML width/span attributes or column decoration.
Bounded table structure / layout / pagination
Shared core (.NET / CLI; Native AOT and WASM probe coverage is recorded separately)
E1/E2 fixed-column tables and whole-row pagination (implemented subset)
Static inspection of current source, documentation and existing tests; main 5722a6a3 passed 1052 .NET CI tests. The engine was not rerun and new reference pages were not approved in this audit.
Exactly one explicit colgroup per table, before thead/tbody, containing 1–16 col elements.
No inferred columns, span attribute, column-group box decoration or multiple column groups. Only inherited text properties are accepted.
Inline container with an italic default. Weight and style inherit independently; nesting selects bold-italic, and author CSS can override the default.
The HTML footer element is still not accepted by the document reader.
HTML elements do not fall back to div/span. PageFurniture / --page-furniture and legacy HeaderText/FooterText are separate PDF margin options; they do not enable HTML header/footer elements. Explicit rejection is not support.
Static inspection of the document reader and separate page-header/footer API at main 2f9cfd1f. Structured options do not expand the HTML element allowlist; the renderer was not rerun in this audit.
Heading paragraphs with default font-size multipliers and text layout. break-after:avoid enables the bounded heading-with-first-paragraph keep.
Not bold by default; no bookmarks or Tagged PDF. Keep applies only to a heading and the first nonempty text paragraph in the same ordinary container outside lists; over-page combinations fall back to normal pagination.
F1 heading with first paragraph (opt-in subset)
Supports a bounded metadata section.
Nonempty body content and invalid placement are rejected; not full metadata/DOM support.
Accepted but not painted
The HTML header element is still not accepted by the document reader.
Document shell and text-style inheritance context.
Does not create a painted box. Width, padding, borders and backgrounds cannot be set on html; not a full HTML5 document model.
Explicitly named baseline JPEG or static non-interlaced PNG resources, including transparency. Images may be blocks under the root/body/ordinary containers or direct td/th items interleaved with direct paragraphs.
No images in paragraphs, inline contexts or list ancestry; no SVG, 16-bit PNG, Adam7 or APNG. No automatic path/URL/data-URI loading, independent height or automatic shrinking. Images and their decorations are indivisible; reject when they cannot fit on an empty page or a fresh row fragment with its required header.
Structure / layout / output
Bounded JPEG/PNG, direct cell images and oversized body-row continuation (implemented subset)
Static inspection of source, documentation and existing tests at main a085085e. The renderer was not rerun; no new reference pages or release were approved in this audit.
Decimal ol, solid-disc ul and at most two nesting levels, with real marker columns, continuation indentation and one marker across page breaks.
No start/reversed/type/value, arbitrary counters or list CSS; no whole-item keep/widows/orphans. The disc is vector artwork and is not a character in extracted text.
Bounded list layout
C2 bounded lists (partial implementation)
Actual structure/layout regressions and five-page API/CLI/Linux AOT specimens; final browser/PR gates are recorded per commit.
Explicit charset=utf-8 inside head.
No full name/content/http-equiv or other meta semantics; no encoding sniffing.
Paragraphs, inline text, vertical spacing and pagination. Direct td/th paragraphs may be interleaved with direct text/images; body-row continuation preserves whole text lines.
Not full HTML default styling; default trailing margin is 8pt. Cell paragraphs must close explicitly and accept only text styles and vertical margins. First/last margins are retained and adjacent gaps use max. Box styles and pagination declarations reject even if overridden. No arbitrary nested blocks or paragraph images.
Ordinary paragraphs and bounded direct cell paragraphs (implemented subset)
D images (exact formats to be specified)
Inline text styles, inheritance, font size and colour.
No painted span backgrounds/borders, block width/padding or inline-block.
Embedded stylesheets at the root, in head or in supported containers.
Only bounded rendering semantics. Not permitted in p/span/title; type/media support is limited.
Standalone fixed-column tables: explicit colgroup, optional single-row thead and one nonempty tbody. Supports colspan and flat paragraph/direct-image cells. Short tables move whole; longer tables keep ordinary rows whole and continue oversized body rows.
1–16 explicit columns. No automatic column sizing, rowspan, nested tables, caption/tfoot or tables inside lists. Headers and images remain whole; atomic content that cannot fit a fresh fragment with its required header/borders is rejected.
Bounded table structure / layout / body-row continuation
Fixed columns, colspan, flat paragraphs/images and oversized body-row continuation (implemented subset)
Exactly one nonempty tbody per table, with tr elements in source order. Body rows that fit a fresh page remain whole; oversized rows continue at whole text-line/image boundaries.
Must follow colgroup and optional thead. No multiple tbody elements, implicit completion or rowspan. Continuation does not make images splittable.
Body td cells support text, span/b/strong/i/em/br, flat direct p/image items, colspan, padding, solid borders and backgrounds. Cells advance independently across oversized-row fragments; short values appear only once.
No lists, arbitrary nested blocks, nested tables or rowspan. td rejects width/margin/break. Cell paragraphs accept only text styles and vertical margins; box/pagination declarations reject even when overridden. Rejects an indivisible image that cannot fit a fresh fragment.
Header th cells support colspan, text/inline content, flat direct p/image items, padding, solid borders and backgrounds. The complete single-row header repeats per fragment; its text uses a real bold font role by default.
Bold text requires an explicit bold font chain or an override to normal. Image-only headers do not require a bold font. No scope/rowspan, lists, nested blocks/tables, independent width/margin/break or Tagged PDF semantics. Cell paragraphs reject box/pagination declarations; headers do not split.
One optional thead with exactly one tr. th may use colspan and flat paragraph/direct-image items. Long tables repeat the complete header on every fragment.
Only th header cells. No multiple header rows, tfoot, rowspan, header splitting or table-structure recovery. The complete header and required decorations must fit a fresh fragment.
Accepts title within head and skips its text.
Accepted and ignored, not rendered on the page; this does not claim that PDF Title metadata is written.
The sole thead header row or a tbody body row. The sum of colspan-covered tracks must equal the explicit column count; row height follows the tallest cell. Body rows that fit a fresh page remain whole; oversized rows continue at whole text-line/image boundaries.
Headers and images do not split. Body cells advance independently without repeating short values; no rowspan. Rejects atomic content that cannot fit a fresh fragment with its header/required borders. Rows accept only inherited text properties, not explicit height/background/borders.
<svg> (foreign namespace)
SVG / MathML root elements are not currently accepted.
Tracked separately from WHATWG HTML elements; not part of HTML-element coverage.
<math> (foreign namespace)
Custom elements <x-…>
Custom tags are not accepted.
Does not execute components, JavaScript or DOM lifecycle callbacks.
IDs used for selector matching.
Does not imply link anchors, PDF destinations or full DOM id semantics.
Input semantics
Splits class tokens on HTML whitespace for supported elements and matches case-sensitively.
Full support refers only to this row's class-token matching contract.
Inline CSS on supported elements.
Disabled on metadata/br; see individual CSS property/value records for scope.
Accepts the lang attribute text.
Not used for font selection, hyphenation, shaping or PDF document language.
Accepted without language-specific behaviour
Accepts the title attribute text.
Does not create tooltips, page text, links or PDF metadata.
Accepts non-executable data-* attributes.
No DOM data access or scripting semantics; not available as selector filters.
UTF-8 only, inside head; other metadata attributes are rejected.
Absent, empty or text/css.
Other MIME types are rejected.
Absent, empty, all or print.
Not CSS @media queries; screen and compound media conditions are unsupported.
The direction attribute is not accepted.
Bidirectional layout is not implemented.
Attribute not accepted.
Not silently ignored or automatically treated as display:none.
Hyperlink/external stylesheet attributes are not implemented.
The a/link elements themselves are also not accepted.
img src names an explicit JPEG/PNG resource; alt is non-rendered metadata. Root/container images and direct cell images share binding rules.
No srcset, URL/data URI or automatic download. alt is not a missing-resource fallback or Tagged PDF feature.
Resource binding
Bounded explicit JPEG/PNG resource binding (implemented subset)
width / height (HTML attributes)
HTML presentational width/height attributes are not accepted.
This is separate from partial CSS width support.
td/th colspan accepts 1 through the explicit column count; omission means 1. Surrounding ASCII whitespace is trimmed; only ASCII digits are accepted, with leading zeros allowed. Covered tracks per row must exactly match the column count.
rowspan remains unsupported. colspan is not valid on other elements and creates no implicit columns. Spanning width and percentage padding use the sum of covered tracks; a valid attribute does not imply arbitrary table-layout support.
Attributes / structure / layout
Implemented colspan subset; rowspan not implemented
on* event attributes
Event attributes are not allowed.
JavaScript is not executed.
HTML5 doctype / comments
Controlled HTML5 doctype and comments are accepted without painting.
Full support means only the input forms listed in the development guide, not quirks/recovery compatibility.
Character references
amp/lt/gt/quot/apos/nbsp and valid numeric references.
Semicolons are required; not the complete named-entity table. Font capabilities are assessed separately.
Omitted end tags
Specific optional end-tag rules for p/body/html.
Other unclosed elements are rejected; not full HTML5 recovery.
XML-style self-closing non-void tags
Forms such as <p/> are rejected.
br/meta are tracked separately as void elements.
Attribute and tag case
Supported HTML names are ASCII case-insensitive.
Class/id values remain case-sensitive; not Unicode case folding.
Arbitrary HTML5 tree construction / error recovery
Bounded nesting and p auto-closing rules.
Invalid block/inline nesting produces FPDF1101; browser recovery algorithms are not reproduced.
pt/px/mm/cm/in; positive % and em use the parent's computed font size; positive rem uses the computed html root size, or PdfOptions.FontSize without html. Root self-reference uses the initial size. The result must be 1–144pt and affects actual glyphs, wrapping and pagination.
No ex/ch/lh/rlh, font-size keywords or calc. Root rem also applies to ordinary box width/padding and bounded p/h margins. This audited row still excludes rem on general container margins, borders, text spacing and image/table sizing; out-of-range sizes reject. Not full CSS font-size support.
Supported text elements; paragraph behaviour is applied to actual paragraphs.
Bounded root-rem spacing implementation, independent point-value PDF samples and NUnit/Native AOT verification paths are pushed. Final exact-head CI, independent review, private candidate and human PDF approval are verified separately.
#rgb/#rrggbb, specified named colours and opaque integer rgb(35,68,107), rgb(35 68 107), rgba(35,68,107,1).
No transparent/translucent alpha, percentage/fractional RGB channels, hsl(), currentColor or complete CSS colour syntax.
Source-scoped actual PDF/API/CLI and native AOT test-path audit; merge, CI and human page approval are verified independently.
normal, unitless 1–10, %/em relative to the element's computed font size, positive rem relative to the computed html root, and pt/px/mm/cm/in. Relative lengths compute to inherited fixed points and affect actual line boxes and pagination; final range 0.1–1440pt.
No zero/negative values, out-of-range multipliers, lh/rlh/calc(), full browser normal font metrics or arbitrary CSS font shorthand. This typography audit covers rem on this text-length path, not box geometry.
Actual C# style computation and multi-page PDF equivalence/failure-recovery tests are committed. Exact-head CI and genuinely independent review must be checked separately; not public release or human approval.
left/center/right; bounded text-align:justify distributes remaining width among U+0020 spaces on actual automatically wrapped paragraph/heading lines, including inherited container styles. First-line indentation, explicit fonts, PDF TJ and page continuation use real measurements.
Final lines, explicit br, nowrap and lines without spaces are not expanded. No start/end, justify-all, RTL/Bidi or CJK inter-character justification. Explicit inline-span justify raises FPDF1201; invalid values raise FPDF1202. Not full CSS Text.
Source, NUnit layout and actual PDF-path audit, separate from complete acceptance; CI and review require separate checks.
p/h1–h6: nonnegative pt/px/mm/cm/in or computed html-root rem (0–14400pt) affect actual vertical gaps and pagination. Bounded paragraph/heading em uses the element's final computed font size, not the html root, and affects PDF positions, wrapping and pagination.
No negative values, %, auto, calc or general containers. Cell-paragraph rem/em explicitly raises FPDF1201. Not general CSS margin collapse; real em margin geometry is limited to p/h1–h6 outside table cells.
Actual layout/glyph-measurement code, independently handwritten point-value reports and NUnit/Linux Native AOT verification code are committed. Exact-head CI, independent review, human approval and release are checked separately.
Only p/h1–h6. No avoid/left/right/recto/verso or column breaks. Pagination declarations in cell paragraphs reject even if overridden.
p/h1–h6, excluding paragraphs inside table cells.
auto/page on p/h1–h6. Headings outside lists additionally accept avoid to keep the heading with its first nonempty sibling paragraph.
avoid is not a general keep: it does not apply to paragraphs, containers or headings inside lists. Oversized groups relax the keep and explicit page breaks win. No left/right/recto/verso or column breaks. Cell-paragraph pagination declarations reject even if overridden.
auto/page: p/h1–h6; avoid: h1–h6 without ol/ul/li ancestry. Cell paragraphs are excluded.
F1 heading with first paragraph (implemented subset)
auto, pt/px/mm/cm/in/%, and numeric zero. Ordinary report blocks/paragraphs/headings also accept nonnegative rem based on the computed html root, or PdfOptions.FontSize without html, affecting actual width, wrapping and background position. Their em sizes use the element's final computed font size and affect real box geometry and pagination.
rem is limited to ordinary blocks/paragraphs/headings; images/tables/col retain separate length contracts and reject rem. No intrinsic/min-content/calc. Overwide geometry raises FPDF1302; out-of-range rem raises FPDF1202. Cell-paragraph width is inapplicable. img/table/col/inline em still rejects; not full CSS width.
body/div/section/article/ol/ul/li/p/h1–h6/img/table/col. Not html, inline elements or td/th; p means only paragraphs outside cells.
Nonnegative pt/px/mm/cm/in/%, or numeric zero. Ordinary report blocks/paragraphs/headings also accept computed-root rem, such as padding:0.5rem, affecting content width, borders/backgrounds and continuation. The 1–4-value shorthand expands and cascades per side. Their em uses the element's final computed font size for actual box geometry, wrapping and pagination.
Vertical percentages still use parent content width. td/th percentages use the sum of spanned tracks; image/table/cell padding rejects rem/em. No auto/calc or negative lengths. Inapplicable elements: FPDF1201; invalid lengths: FPDF1202; geometry overflow: FPDF1302. Cell-paragraph restrictions remain. New em box lengths are only for ordinary report blocks, not table/img/col.
body/div/section/article/ol/ul/li/p/h1–h6/img/table/td/th. Not html, inline elements, col, table groups or rows; p means only paragraphs outside cells.
Nonnegative pt/px/mm/cm/in/%, or numeric zero. Ordinary report blocks/paragraphs/headings also accept computed-root rem, such as padding-top:0.5rem, affecting actual content width, borders/backgrounds and page continuation. Their em uses the element's final computed font size for actual geometry, wrapping and pagination.
Nonnegative pt/px/mm/cm/in/%, or numeric zero. Ordinary report blocks/paragraphs/headings also accept computed-root rem, such as padding-right:0.5rem, affecting actual content width, borders/backgrounds and page continuation. Their em uses the element's final computed font size for actual geometry, wrapping and pagination.
Nonnegative pt/px/mm/cm/in/%, or numeric zero. Ordinary report blocks/paragraphs/headings also accept computed-root rem, such as padding-bottom:0.5rem, affecting actual content width, borders/backgrounds and page continuation. Their em uses the element's final computed font size for actual geometry, wrapping and pagination.
Nonnegative pt/px/mm/cm/in/%, or numeric zero. Ordinary report blocks/paragraphs/headings also accept computed-root rem, such as padding-left:0.5rem, affecting actual content width, borders/backgrounds and page continuation. Their em uses the element's final computed font size for actual geometry, wrapping and pagination.
Transparent or specified hex/named colours, plus opaque integer rgb()/rgba(...,1), for actual block and paginated background fills.
No currentColor, translucent alpha, hsl/gradient or other functional colours. Existing element applicability remains; cell-paragraph declarations still reject.
Up to three components in any order: width + solid/none + colour; lengths also accept mm/cm/in. Colour components may use opaque integer rgb()/rgba(...,1).
Components are bounded. No rounded/image borders. border expands to 12 longhands; related cell-paragraph declarations reject even when overridden. No translucent border colours, HSL or nested functions.
Up to three components in any order: width + solid/none + colour; lengths also accept mm/cm/in.
Components are bounded. No rounded/image borders. border expands to 12 longhands; related cell-paragraph declarations reject even when overridden.
New source and exact regression cases inspected; PR-head execution must be checked in CI separately. Not full-standard support or human approval.
pt / px / zero / thin / medium / thick; lengths also accept mm/cm/in.
Nonnegative and bounded. No %/em/rem. thin=.75pt, medium=2.25pt, thick=3.75pt. Related cell-paragraph declarations reject even if overridden; mm/cm/in affect actual border thickness and layout.
solid / none, with 1–4-value expansion.
No dashed/dotted/double/hidden/groove/ridge/inset/outset. Related cell-paragraph declarations reject even if overridden.
currentColor, #rgb/#rrggbb or seven named-colour spellings; also opaque integer rgb()/rgba(...,1).
No transparent, translucent alpha, HSL or other functional colours. border-color accepts 1–4 values. Related cell-paragraph declarations reject even if overridden.
The property is not connected to rendering semantics.
Block/inline behaviour follows element types. Even display:block/none is outside the property allowlist; this is not only a lack of Flex/Grid.
Representable syntax; rendering rejects
The current strict property allowlist excludes this name. Even syntactically valid input is rejected rather than silently ignored.
Ordinary blocks, paragraphs and headings: box-sizing:content-box (default) or border-box. Explicit width includes horizontal padding and solid borders, affecting actual wrapping, auto centring, page continuation and painting. Width/padding percentages use parent content width; CSS-wide values and !important follow the cascade. min/max-width work with the box model; undeclared defaults preserve existing output.
Not supported on img, table, table-structure elements, cell paragraphs, span or html. min/max-width has a separate bounded ordinary-block implementation. No padding-box, calc() or general height/Flex/Grid. Invalid input: FPDF1201/1202; undersized or over-parent border boxes: FPDF1302. Existing output is preserved.
Supported in scoped source; not publicly released
Ordinary blocks/paragraphs/headings, excluding images and tables.
Source and actual layout/failure-recovery tests; final CI and independent review for this change still require exact-head verification.
Computing actual content height does not mean CSS height declarations are supported.
min-width:auto removes the extra constraint; otherwise nonnegative pt/px/mm/cm/in/%, zero and ordinary-block root rem. min wins when greater than max. Computed-root rem constrains actual content-box/border-box, margin:auto, wrapping, backgrounds and pagination; percentages use parent content width. Ordinary-block em uses the element's final computed font size, not the root.
Only ordinary report blocks/paragraphs/headings. No min-content/max-content/fit-content, calc, negative lengths or table/image/inline rem/em. Invalid values: FPDF1202; inapplicable elements: FPDF1201; overflowing geometry: FPDF1302. Existing PDF is preserved. Not full CSS Sizing; img/table/col/inline em remains rejected.
Ordinary blocks/paragraphs/headings; not images, tables, cell paragraphs or inline elements.
max-width:none removes the extra constraint; otherwise nonnegative pt/px/mm/cm/in/%, zero and ordinary-block root rem. max can cap width:auto panels. Computed-root rem constrains actual content-box/border-box, margin:0 auto, wrapping, backgrounds and pagination; percentages use parent content width. Ordinary-block em uses the element's final computed font size, not the root.
1–4-value TRBL shorthand on p/h1–h6. Vertical values: nonnegative pt/px/mm/cm/in/rem; horizontal values also accept %/auto. rem uses the html root; margin:0 auto centres the actual outer box and affects wrapping, painting and pagination. Bounded paragraph/heading em uses the element's final computed font size, not the root.
Only ordinary p/h1–h6. Cell paragraphs allow only explicit zero horizontal margins and reject all rem/em margins. No negatives, vertical auto/%, calc or general container margins. Overflow: FPDF1302. Real em margin geometry is limited to p/h1–h6 outside cells.
Bounded four-sided shorthand / actual horizontal-auto layout
p/h1–h6; cell-paragraph horizontal margins must be zero.
Ordinary p/h1–h6: nonnegative pt/px/mm/cm/in/%/rem or auto. rem uses the actual computed html root; auto resolves from space outside the outer box, affecting actual wrapping, painting and horizontal position. Their em uses the element's final computed font size, not the root, for real geometry and pagination.
Nonzero horizontal margins are inapplicable to cell paragraphs and all rem/em margins reject there. No other blocks, negative values or calc. One-sided auto consumes remaining width; overflow raises FPDF1302. Real em margin geometry is limited to p/h1–h6 outside cells.
Bounded horizontal longhands / actual auto layout
p/h1–h6; nonzero values are inapplicable in table cells.
Single solid-colour background shorthand: hex, specified named colours, transparent, CSS-wide keywords or opaque integer rgb()/rgba(...,1), with actual fills equivalent to background-color.
Only one solid colour. No URL images, gradients, non-opaque alpha, percentage RGB, hsl(), repetition, positioning, sizing or layered backgrounds.
CSS value validation / semantics / rendering
Same applicability as background-color: bounded block elements and table cells. Not inline elements, columns/row groups or related cell-paragraph declarations.
normal/bold, numeric 400/700 and equivalent number tokens; inherit/initial/unset/important.
No other weights, bolder/lighter or variable axes. Role chains must be supplied explicitly; no font-family matching.
normal/italic; inherit/initial/unset/important; combines independently with weight.
No oblique angles, font synthesis or variable axes; missing chains are not downgraded.
Report paragraphs/headings/inline spans and inheritance: normal/zero, pt/px/mm/cm/in, em based on the element's final size or rem based on the computed html root. Results must be 0–24pt. Real PDF Tc advances with Courier and explicit TrueType affect wrapping, centring, page continuation and decoration widths.
No negative tracking, %, calc(), values over 24pt, complex spacing/shaping/kerning or full CSS Text. Invalid values: FPDF1202; overwide text: FPDF1302. Existing PDF is protected.
CSS computation / glyph measurement / actual PDF output
Shared PDF engine (.NET and CLI): body text, headings, inline spans and container text inheritance.
Report text and inline spans: normal/zero, pt/px/mm/cm/in and bounded em/rem using final element size/computed html-root size respectively, producing fixed 0–24pt word spacing. Retained U+0020 spaces add measured advance; TrueType CID and Courier use PDF TJ, affecting wrapping, alignment, continuation and decorations.
No negative word spacing, %, calc(), values over 24pt, Unicode word-boundary handling, complex shaping, unsupported white-space modes or full CSS Text. FPDF1202 for invalid values; FPDF1302 for overwide text. Existing PDF is protected.
CSS computation / text measurement / actual PDF TJ output
Shared PDF engine (.NET and CLI): report body text, headings, inline spans and container text inheritance.
text-decoration / text-decoration-line: a single none, underline or line-through value. Actual PDF solid lines use resolved-font line widths and continue with wrapping and pagination.
Only one opaque solid decoration, using text colour and font-size-based fixed thickness/position estimates. No overline, wavy/dotted/dashed, combinations, independent decoration colour/style/thickness, HTML u/s, full browser decoration propagation or complex shaping.
CSS cascade / actual PDF text painting
Supported paragraphs/headings and inline span/b/strong/i/em. Explicit font-resource rules are unchanged.
Static audit of source and new NUnit regressions; current PR CI/independent review still requires verification. Not release or human PDF approval.
ASCII A–Z/a–z report text accepts text-transform:none/uppercase/lowercase. Paragraphs, headings, inherited containers and inline spans transform actual glyphs before font lookup/measurement, affecting searchable text, wrapping, decorations and pagination. Uncased Chinese text, numbers and punctuation remain unchanged; explicit span none can protect company names.
No capitalize, full-width, full-size-kana, locale-sensitive or general Unicode casing. Affected non-ASCII cased letters and Unicode case-mapped symbols such as Roman numerals (LetterNumber) and circled Latin letters (OtherSymbol) raise FPDF1305 rather than silently change or disappear. Unsupported values: FPDF1202; standalone images: FPDF1201; missing transformed glyphs: FPDF1504. Not full CSS Text.
CSS cascade / computed styles / actual PDF text and glyph rendering
Source, NUnit actual-font glyph/geometry/failure-recovery cases and Linux Native AOT PDF checks; exact-head CI and independent review are verified separately.
Explicit non-table p/h1–h6: nonnegative pt/px/mm/cm/in/%, plus bounded em/rem. em uses the declaring element's final size and rem the computed html root, becoming fixed points; % uses actual content width. Only the first logical line changes glyph positions/wrapping; br and page continuations do not repeat the indent, and the background box stays put.
No negative/hanging indent, each-line/hanging, calc, containers/spans/cell paragraphs or full CSS Text. Excessive indent or a first word that cannot fit: FPDF1302; invalid values: FPDF1202. Existing output is protected.
CSS cascade / actual PDF first-line positioning / pagination
Explicit non-table paragraphs and headings; shared .NET / CLI core.
Explicit p/h1–h6 accept normal or nowrap with normal whitespace collapsing. nowrap disables automatic inter-word and CJK wrapping; explicit br still breaks lines. Actual PDF line layout and pagination are preserved.
Only whole-paragraph/heading nowrap. No direct span/container setting, pre/pre-wrap/pre-line/break-spaces, inline nowrap fragments or full CSS Text. Text exceeding actual content width raises FPDF1302, rather than clipping or silently wrapping.
Semantics / layout / output
Explicit paragraphs and headings, including cell paragraphs using actual content width; shared CLI/.NET core.
Source and added regression cases recorded; final platform CI, Native AOT and independent review require exact candidate-head verification. Not full-standard support.
C text expression (planned)
E tables (planned)
F pagination (exact syntax undecided)
No legacy page-break-* aliases; do not confuse this with supported break-before/after:auto/page.
Built-in block continuation uses slice-like painting, but this CSS property name itself is not accepted; clone is also not implemented.
D images (exact syntax undecided)
Supported blocks compute content width after subtracting horizontal padding/borders.
Full support applies only to controlled auto block layout; outer-width/page constraints still apply.
Rendering value
Elements allowed by the corresponding CSS property.
Explicit finite nonnegative content width; px=.75pt.
Not border-box; no overflow clipping.
Relative to parent content width, including nesting.
Bounded percentages; outer width must not exceed the parent's content area.
Numeric zero is parsed; empty zero-width boxes may exist.
Nonempty text that cannot fit zero width produces FPDF1302.
Intrinsic sizing is not implemented.
A valid identifier is not necessarily a usable layout value.
Representable syntax; value rejected
TRBL shorthand and per-side longhands cascade independently; mm/cm/in are accepted.
Lengths are limited to pt/px/mm/cm/in/%, or zero, on supported blocks.
All padding percentages use parent content width.
Not a percentage of page height or parent height.
auto is not available.
Do not confuse this with width:auto.
pt / px units
Relevant current length properties accept pt/px.
Sign and bounds differ by property; this does not implement every CSS property.
font-size retains parent-relative em/% and root rem; line-height supports %/em/rem. Ordinary report width/min/max-width/padding and p/h margins support final-element em and html-root rem. Bounded text-indent and letter-spacing/word-spacing em/rem affect actual indentation, advances, wrapping and pagination.
Relative dimensions remain unsupported for general container margins, borders and img/table/col. Other relative text-layout values beyond text-indent/letter-spacing/word-spacing, ex/ch/lh/rlh, calc and full CSS Values remain unsupported. Inapplicable cell-paragraph em/rem margins raise FPDF1201.
Bounded relative font lengths / box geometry / actual PDF text output
Supported text font-size/line-height; ordinary block/paragraph/heading width/min-max/padding, excluding tables/images.
Viewport units are not connected to layout.
A PDF page is not automatically a CSS viewport.
mm/cm/in apply to font-size, vertical p/h1–h6 margins, width, padding and solid border-width; conversions use 72pt/in, 25.4mm/in and 96px/in.
q/pc remain unsupported. This does not imply all CSS length properties, em/rem/calc/@page or other layouts; existing value bounds and element applicability remain.
Bounded physical length units / rendering
Colours #rgb / #rrggbb
Opaque hexadecimal RGB colours for text, block backgrounds and borders.
Exactly these two forms; not alpha or colour management.
Seven named-colour spellings
Only the seven listed spellings; gray/grey are aliases for the same colour.
All CSS named colours
Only seven spellings are recognised.
For example, orange/yellow/rebeccapurple are not implemented.
rgb(0..255, 0..255, 0..255), with comma or space syntax; rgba(r,g,b,1) only with a fully opaque numeric alpha.
No translucent/transparent alpha, percentage/noninteger channels, slash alpha, hsl()/hsla(), colour mixing or nested functions.
CSS AST / semantic normalisation / actual PDF painting
Alpha hexadecimal colours are not implemented.
The transparent background keyword is a separate exception.
Modern colour spaces are not evaluated.
CSS token acceptance is not evidence of colour rendering.
Does not paint a background fill.
Does not imply translucent colour or opacity support.
The current background validator does not handle currentColor.
Border currentColor support cannot be extrapolated to backgrounds.
Uses the final computed text colour.
Explicit supported blocks, not every place CSS permits currentColor.
The current border-colour validator treats only currentColor as a special exception.
Transparent backgrounds do not imply transparent border support.
The current text-colour validator does not accept currentColor.
inherit is a separate supported CSS-wide value.
Independent solid painting on all four sides.
Sizes remain subject to page/resource constraints; no rounded corners.
Effective border width is zero and nothing is painted.
Specified width may remain in style data but occupies no border geometry.
Other common line styles are not implemented.
They do not silently degrade to solid lines.
.75 / 2.25 / 3.75pt respectively.
Fixed FactsPDF mappings, for both longhands and shorthand.
Border percentages are rejected.
Padding percentage rules do not apply.
Alignment uses the current paragraph content width.
Precisely scoped to simple horizontal layout; this row excludes RTL/justification.
Only text-align:justify, on automatically wrapped nonfinal horizontal LTR lines with retained U+0020 spaces. PDF TJ distributes measured remaining space based on actual line width.
No start/end, justify-all, RTL/Bidi or CJK inter-character justification. Explicit br, final lines, nowrap and spaceless lines are not expanded. Explicit span justify is unsupported.
Actual PDF layout / rendering value
Paragraphs, headings, actual text paragraphs inheriting from containers and existing flat table text. Explicit inline-span setting is unsupported.
Bounded source/NUnit/actual PDF-path audit; final exact-head CI and independent review remain to be verified.
Unitless multiplier with controlled line-box metrics.
Glyph fallback does not change the first font's line-box basis.
normal (engine default 1.2), %/em based on current element size, rem based on html root size, and pt/px/mm/cm/in compute absolute heights inherited as fixed values, affecting actual PDF leading and page breaks.
Only bounded text line-height support. Zero/negative values, out-of-range multipliers, lh/rlh/calc() and full browser normal font metrics still reject explicitly.
margin-top/bottom: negative values / % / auto
This audited scope accepts only nonnegative pt/px or zero.
Does not implement general margin collapse.
Explicit and automatic pagination of paragraphs/headings.
Only p/h1–h6; containers cannot set the same property.
Only break-after:avoid on h1–h6 outside lists. Keeps the heading with the first nonempty sibling paragraph when the group fits a page.
No break-before:avoid or left/right. No general keep, break-inside or widows/orphans. The keep relaxes for an oversized group.
Bounded values / heading pagination
h1–h6 without ol/ul/li ancestry, followed by a paragraph in the same ordinary container.
Connected properties take parent values according to their own inheritance semantics.
Only the current property set; unknown properties still reject.
Connected properties reset to engine-defined initial values.
FactsPDF's default font/size is not a browser theme's initial value.
Inherited properties take parent values; other properties take initial values.
Only the current property set.
Cascade layer/origin rollback is not implemented.
Not equivalent to initial/unset.
Author stylesheet and inline precedence for current properties.
Not the complete user-origin, animation or cascade-layer system.
Type selector p
Matches supported tags with ASCII case-insensitivity.
Only non-namespaced type selectors are covered.
Selector matching
Universal selector *
Matches actually supported elements.
Does not match the internal synthetic root; not a namespace wildcard.
Class selector .note
HTML-whitespace-separated class tokens with case-sensitive values.
Subject to the matching-operation budget.
Normal identifier ID matching.
Escaped IDs beginning with digits or similar characters receive extra validation; not complete ID syntax.
Compound selector p.note.active#id
Multiple classes, IDs and a tag match the same element.
At most 32 simple items per compound.
Selector list h1, p
The matching branch determines specificity.
Empty/malformed branches reject; no forgiving-selector-list pseudo-classes.
Descendant combinator .a p
Matches the actual ancestor chain.
At most 32 compounds; does not imply a complete browser DOM.
Child combinator .a > p
Direct parent/child and mixed ancestor chains.
Depends on the supported HTML tree.
CSS escaped identifiers
The syntax layer decodes escapes; type/class selectors can use supported identifiers.
Some IDs remain subject to validation; not all escaped IDs count as supported.
Attribute selector [attr=value]
Attribute selectors are not implemented.
Acceptance of lang/data-* attributes does not enable attribute selectors.
Selector rejection
Adjacent sibling combinator +
Sibling combinators are not implemented.
They do not fall back to descendants.
General sibling combinator ~
Pseudo-classes :first-child/:nth-child/:lang
Pseudo-classes are not implemented.
Static pseudo-classes are also unsupported.
Logical/relational pseudo-classes are not implemented.
A function token does not imply selector execution.
Pseudo-elements/generated content are not implemented.
The content property is also not connected to rendering.
Namespace selectors svg|…
Namespace selectors are not implemented.
The SVG root is also not accepted.
& and CSS Nesting evaluation
Nested selectors are not evaluated.
Preserving a structure in the syntax AST does not imply rendering.
Preprocessing, tokens, declarations, functions, blocks, at-rule structure and recovery diagnostics.
Selected test coverage, not complete WPT/standards-conformance proof; produces no layout.
Syntax parsing only
CSS string/identifier tokens can decode escapes.
Specific token boundaries and semantic applicability need separate verification.
CSS error recovery in the rendering path
StrictPdf rejects recovery diagnostics and unknown semantics.
The standalone syntax layer can recover; this does not imply browser-like tolerant rendering.
Strict rejection
Inline style attribute
Inline declarations of supported properties/values.
Limited metadata/br and per-property applicability.
Style source
Embedded <style>
Cascade is evaluated after a complete scan; later styles can affect earlier content.
Only fixed type/media forms, with bounded placement/properties/selectors.
External stylesheets / @import
Does not load link elements or import CSS.
No network or automatic external-resource loading.
Rendering rejects
Accepts a fixed print media marker.
Not @media print or full media-query evaluation.
Conditional rules are not evaluated.
An AST may represent the rule; rendering still rejects at-rules.
Syntax representation only
CSS page rules are not implemented.
Page parameters in PdfOptions are not @page support.
Fonts are not loaded from CSS.
Only fonts explicitly supplied by the caller are currently accepted.
Cascade layers are not implemented.
!important support cannot be extrapolated to every cascade rule.
Custom counters are not implemented.
This historical audited row states that lists themselves are not yet connected.
Animation rules are not executed.
A static PDF is not a dynamic timeline.
No custom-property declaration or substitution evaluation.
The syntax core can preserve custom properties; StrictPdf still rejects them.
Value functions are not evaluated.
Dimension values must already be expressed as supported tokens.
Author-style cascade
Per property: important → inline → specificity → source order.
Not the complete origin/layer/animation cascade; FactsPDF initial values are independent.
Computed styles
Text/box properties inherit independently
Text inherits; box properties do not inherit by default. CSS-wide values are supported.
Only the current property set; this does not imply new-property support.
Printable English ASCII text
Fontless Courier path: U+0020–U+007E.
Full support means printable ASCII only, not all English punctuation or every Unicode character.
Text layout / glyphs
Depends on explicit font glyphs, script and encoding, not the SDK programming language.
Existing cases and code paths; not rerun here. This was a static audit.
Latin and some precomposed accented letters
Simple precomposed characters within the allowed range, using explicit fonts.
Missing glyphs raise errors. Decomposed combining marks and full language-specific typography are unsupported.
Development documentation and character-limit inspection; static audit.
Chinese (horizontal Simplified/Traditional Han text)
Explicit fonts, horizontal Han glyphs, searchable/copyable text and simple line breaking.
No guarantee of all character coverage; complex typography, vertical layout and complete punctuation rules are not implemented.
Existing Chinese/page-continuation acceptance cases; font-dependent. Static audit.
Extension Han characters (supplementary planes)
cmap12 with explicit glyphs; UTF-16BE ToUnicode.
Limited by allowed character ranges and actual glyph coverage.
Existing supplementary-character/ToUnicode regressions; static audit.
Simple Greek characters
Simple precomposed characters within the allowed range.
Not complete Greek-language conformance; combining characters are rejected.
Documented capability; a dedicated language corpus was not verified. Static audit.
Simple Cyrillic characters
Not a promise of all extended letters or language-specific typography.
Whole-document Japanese capability
Complete Japanese specimens have not been verified.
Han support does not establish full kana, line-breaking prohibition, annotation or vertical-layout support.
No dedicated language baseline confirmable in this audit; static inspection only.
Whole-document Korean capability
Complete Korean specimens have not been verified.
Character-range acceptance is not verification of an entire language.
Whole-document Vietnamese capability
Dedicated Vietnamese documents have not been verified.
Precomposed and decomposed characters may take different paths; a language name alone does not determine support.
No dedicated corpus verification; static audit.
Arabic layout
Complex shaping and RTL are not implemented.
Loading an Arabic font alone does not establish support.
Explicitly unsupported text-processing scope; static audit.
Hebrew / bidirectional mixed text
RTL/bidi is not implemented.
Logical/visual text order cannot be handled by simple reversal.
Explicitly unsupported bidi scope; static audit.
South Asian scripts requiring complex shaping
Complex-script shaping is not implemented.
A font containing the glyphs does not imply correct layout.
Explicit shaping exclusions and simple-range limits; static audit.
Combining marks / decomposed glyph sequences
Combining marks are explicitly rejected.
For example, A plus combining acute; not replaced with a question mark.
Negative regression cases; static audit.
Bidirectional algorithm and direction controls
bidi/RTL/direction controls are not implemented.
dir and direction are not connected to layout.
Negative cases and documentation; static audit.
CJK vertical text / ruby
Vertical text and pronunciation annotations are not implemented.
writing-mode and ruby elements are also not accepted.
Source allowlist/development guide; static audit.
Complete Emoji / ZWJ / colour fonts
Sequence, variation and colour-font pipelines are not implemented.
Some ordinary symbol glyphs do not establish complete Emoji support.
Exclusion-scope inspection; static audit.
Static TrueType glyf fonts
SFNT glyf, cmap4/12, horizontal advances and metrics.
Must be explicitly supplied. Structural checks are not a complete malicious-font sanitiser.
Existing font-boundary and real-font evidence; static audit.
Explicit per-character font fallback
Selects glyphs in the supplied font-chain order.
At most eight input entries, with no automatic system discovery; missing glyphs raise FPDF1504.
Existing fallback regressions; static audit.
TrueType glyph subsetting
Optional subsetting, composite closure/ID mapping and ToUnicode.
Only supported static glyf fonts; fonts prohibiting subsetting retain full embedding.
Existing subsetting regressions/font evidence; static audit.
Bold/italic font-role selection
Four independent ordered fallback chains: normal, bold, italic and bold-italic, using actual static-font outlines and widths.
No paired/same-family requirement, system discovery or synthetic styles. Eight entries across all four chains; default 64MiB. Variable fonts are unsupported.
Web-font containers are unsupported.
No implicit loading via @font-face.
Explicit exclusion; static audit.
TTC / OTC / CFF / CFF2 / variable fonts
These font representations are unsupported.
The .otf extension alone does not determine suitability; the core requires supported SFNT glyf data.
Advanced OpenType glyph substitution/positioning is not implemented.
Simple per-glyph advances are not complete text shaping.
NBSP nonbreaking space
Explicit-font text preserves NBSP and keeps adjacent fragments together.
The fontless ASCII path lacks this glyph; exceeding line width still raises an error.
Existing NBSP regressions; static audit.
Unspaced Chinese wrapping and simple punctuation constraints
Han characters may offer line-break opportunities; limited leading/trailing punctuation rules.
Not complete Unicode UAX #14 or language-specific line breaking.
Existing punctuation/page-continuation cases; static audit.
Unicode scalar / UTF-16 validation
Handles supplementary characters correctly and rejects isolated surrogates.
Full support means only this validation/fidelity contract, not every writing system.
Tests for isolated high and low surrogates; static audit.
C# / .NET library API
Calls PdfConverter.Convert through a source ProjectReference.
Current net10.0 entry contract, not a stable package/API or other language bindings.
Library entry point
Assessed separately by interface, RID and build mode.
Existing development guide/known process; no new platform execution in this audit.
CLI command-line entry point
Explicit UTF-8 input and fonts; PDF file or binary stdout.
File protection and exit codes follow the existing CLI contract; not an SDK.
Command-line entry point
No dedicated JavaScript/TypeScript SDK has been delivered. The B1 Node subprocess probe invokes the production CLI with UTF-8/explicit resources and receives PDF bytes.
Internal verification helper, not an npm package, stable Node API, TypeScript SDK or in-process binding. Timeout/cancellation terminates the child process; this is not .NET cooperative cancellation.
SDK not delivered; subprocess integration probe
Node 22+ with a supported production CLI; actual Native AOT integration evidence is limited to Linux x64.
B1 Node/CLI probe implemented; dedicated SDK not delivered
Inspected the B1 helper, documentation and real CLI integration tests. Main CI 39876677455 passed Node process tests and Linux Native AOT integration. No new platform execution in this audit.
No dedicated SDK currently exists.
Python verification scripts are test tools, not a FactsPDF SDK.
Programming-language adapter
Rust SDK / native binding
No Rust integration is currently implemented.
A shared core is a design goal, not an implemented interface.
Go SDK / native binding
No Go integration is currently implemented.
Does not implement a separate layout engine.
No Java integration is currently implemented.
A C# core does not automatically expose a JVM ABI.
Stable C ABI / FFI
No cross-language native ABI has currently been delivered.
The CLI is a process interface, not an FFI.
Cross-language ABI
B integration-risk validation (planned)
An existing path with actual compilation, execution and PDF verification.
Full support means this RID/build mode only; system-library dependencies remain.
Target-platform verification
Windows managed .NET
An existing managed NUnit test path.
Not Windows AOT evidence or verification of all CPU architectures.
macOS managed .NET
Not evidence for the complete macOS arm64/x64 AOT matrix.
Linux managed .NET
Existing managed testing/execution path.
Does not imply every distribution and CPU was verified.
Actual win-x64 Native AOT publish followed by copying the standalone exe into an empty directory and executing it. Checks a two-page ASCII PDF, exact errors, destination protection and recovery.
Not acceptance of every font/image/combined report or Windows architecture. System libraries are still used; no downloadable stable CLI product has been released.
Verified target-platform subset
Windows x64; actual native build and isolated exe execution.
G Windows x64 Native AOT has bounded execution evidence
Main 5722a6a3 windows-native-aot job 120163846267 succeeded. Workflow, verifier and boundary tests were inspected. Windows was not run locally in this audit.
No currently verified AOT artifact/execution evidence.
Evidence must be added separately by RID.
Platform-verification candidate
This target-platform matrix has not been verified.
Do not infer arm64 from x64.
Browser WebAssembly host
The B2 private browser-wasm host runs the same .NET core. It generates PDFs from explicit resources in protected Chromium, compared with CLI bytes/text/pages.
Single-threaded non-AOT experimental probe, not a public WASM/JS SDK. Does not establish other-browser/mobile support, responsiveness, cancellation, production performance or WASI.
Implemented experimental host
Non-AOT single-threaded browser-wasm; verified protected Chromium.
B2 WASM risk probe implemented; formal SDK/release is a separate deliverable
Static audit at main 5722a6a3. Protected-browser run 39876108148 succeeded on merged PR49 head 30a5d575, whose source tree matched main. That run is not relabelled as execution on main SHA or approval of new reference pages.
WASI runtime
Compilation/execution routes have not been verified.
Not equivalent to browser WASM.
Execution environment
<br> hard-line-break contract
Explicit line interruption; consecutive br elements can create blank lines.
Only hard-line-break behaviour, not br styling.
Layout / output
Current guide/source and existing test assets; static inventory audit.
Automatic text wrapping
Wraps using actual font advances and container width.
An overwide unsplittable segment raises an error; no arbitrary CSS word-break/hyphens configuration.
Nested block box tree
Preserves container/paragraph order and parent-child relationships.
Only the internal structure contract for currently supported elements.
Cross-page box slice behaviour
Backgrounds and side borders continue; top border on the first fragment, bottom border on the last.
Bounded block layout, not all CSS Fragmentation. box-decoration-break cannot be declared.
Cross-page clone behaviour
Does not repaint all four borders and top/bottom padding on every page.
Cannot be enabled through box-decoration-break:clone.
PdfOptions page size and margins
Existing API/CLI options define page geometry.
Not CSS @page, size or margin-box support.
Heading with paragraph / keep-together
Explicit break-after:avoid keeps a complete heading/consecutive marked headings with the first nonempty sibling paragraph, moving the group when the page tail is too small.
Only bounded sequences in the same ordinary container outside lists. Oversized groups fall back to normal line-by-line pagination; explicit page breaks win. Not arbitrary keep-together.
F1 implemented; F4 combined reports have engineering evidence, with reference pages awaiting user review.
Widow/orphan control is not implemented.
Normal line-by-line pagination does not include these constraints.
F pagination experience (planned)
Automatic headers / footers / page numbers
PdfOptions.PageFurniture provides six left/centre/right header/footer regions. {page}/{pages} use actual physical page numbers and total pages. FirstPage replaces all six regions; an empty object clears them without restarting numbering. Each region supports 1–144pt, opaque RGB and explicit bold/italic font roles. .NET API and CLI --page-furniture are connected without reflowing body content. Legacy PageNumbers/HeaderText/FooterText remain separate and literal.
One line per region. No HTML/images, multiline text, automatic wrapping/shrinking, arbitrary per-page layout or CSS @page. Mutually exclusive with legacy numbering/text options, even empty legacy text. At most 1024 UTF-16 units per template; strict CLI UTF-8 JSON is limited to 65536 bytes and depth eight. No automatic resource loading. Missing fonts, bad templates, collisions or bounds violations fail explicitly.
Shared core (.NET API / CLI). Platform acceptance follows its own records; this audit does not expand other platform entries.
Structured PageFurniture and strict CLI configuration are implemented; complete 52-PDF legacy replay and release acceptance are recorded separately.
Static inspection at main 2f9cfd1f; cited core, CLI, shared decoder and dedicated tests match ff03f91e. Historical Ubuntu CI 40134259420/120992303896 on main ff03f91e passed 1364/1364, covering six regions, physical total pages, first-page replacement and CLI/API comparison. That run is not relabelled to this SHA. This audit did not rerun the engine or complete the full 52-PDF legacy replay/release acceptance.
Table layout / repeating cross-page headers
Explicit fixed columns, colspan, flat paragraph/direct-image cells and an optional single-row repeating header. Oversized body rows continue by whole text lines/images; cells advance independently and short values appear once. F4 combined-report engineering evidence is recorded separately.
No automatic column widths, rowspan or nested tables. Short tables move whole; body rows that fit a fresh page remain whole. Headers/images do not split; atomic content that cannot fit a new fragment with its required header/borders rejects. This audit does not complete new-reference-page approval or release gates.
Fixed columns, colspan, flat paragraphs/images and oversized body-row continuation. Engineering evidence is not release approval.
Image rendering: JPEG / PNG / SVG
LoadImage identifies bounded baseline JPEG and static non-interlaced PNG by content. PNG grayscale/indexed colour accepts 1/2/4/8-bit; RGB, grayscale+alpha and RGBA accept 8-bit. tRNS/alpha produces a separate soft mask. Repeated placements reuse images/masks; JPEG retains original DCT data.
No 16-bit PNG, Adam7, APNG, ICC/HDR conversion or SVG. Progressive/multiscan/CMYK/rotated/ICC JPEG remains rejected. No automatic shrinking, independent height, inline images or colour management. Images and their decorations do not split; loading/output budgets remain.
Loading / layout / output
Bounded JPEG / PNG and transparency (implemented subset)
Searchable text / ToUnicode
ASCII and supported Unicode glyphs yield extractable text.
Script capabilities are bounded; not Tagged PDF or accessible reading order.
Structure tags and accessibility standards are not implemented.
HTML headings/sections do not imply a PDF semantic structure tree.
PDF/A conformance
No claim of PDF/A output.
A normal PDF opening successfully is not PDF/A certification.
PDF links / bookmarks
Hyperlinks and outlines are not implemented.
Input id/title values do not create PDF navigation.
JavaScript / DOM execution
Not an execution capability of this static document engine.
script remains explicitly unsupported; no dynamic content execution.
Automatic network-resource fetching
Remote CSS/images/fonts are not loaded automatically by default.
Explicit resource input is not automatic fetching; this row describes a design boundary.
Input / element / depth / page / command / output limits
Errors are raised at the corresponding boundaries; early conversion stages do not write the final PDF.
Full support refers only to the current resource-limit contract, not a process-memory/CPU sandbox.
Caller output remains untouched before conversion succeeds
Preserves the caller's original bytes on parsing/layout/serialisation failure.
I/O failure or cancellation during the final copy cannot roll back an arbitrary stream.
Current rendering semantics accept 51 CSS property names. font-size/line-height support bounded relative values. Ordinary block width/min/max-width/padding and four-sided p/h margins use html-root rem or final-element em for actual geometry, wrapping and pagination. text-indent and letter-spacing/word-spacing support bounded em/rem; computed tracking/word spacing stays within 0–24pt. General container margins, borders and img/table/col still reject these relative box values. text-align:justify and min/max-width are explicit subsets.
This document describes the original stylesheet increment and links to later box-layout documentation. Historical statements about eight properties, no escapes, or width/padding/painting not being part of that increment are not a current compatibility matrix by themselves.
The semantics section still describes eight properties and a future box tree. Parsing-layer descriptions must be distinguished from current rendering behaviour.
The legacy helper's important error string does not override production AST support for !important.`.split('\n'));
