# FactsPDF M5 — Block Box Tree and Basic CSS Box Model

**Status:** Design for review (not an implementation or a release)
**Design approved in conversation:** Approach A, retain actual block nesting; 2026-10-10.
**Repository base:** `KoalaFacts/FactsPDF` `main`, commit `8c303bda7a0fe1875f5376839a4bc7f4ebf8dc6e`.
**Scope:** `width`, `padding`, `border`, `background-color`; existing text, stylesheet, Unicode and font-subsetting behavior must remain intact.

## 1. User outcome and success criteria

FactsPDF must render useful document panels, cards and sections—not just styled text—directly from standard HTML/CSS, without a browser or a separate markup language. Preserve a single layout/PDF engine for future bindings. The minimum demonstrable output is an A4 report with nested boxes, a specified-width callout, padding, four solid borders, backgrounds, Chinese/English text and a box continuing onto a second page.

**Invariants:**
- No new runtime HTML/CSS/PDF/font engine dependency, automatic filesystem/network access, JavaScript execution or dynamic-code requirement. Keep Native AOT compatibility.
- Existing `PdfConverter.Convert`, `PdfOptions`, CLI arguments, licenses and commercial/community policy unchanged except additive resource-limit options if justified by tests.
- Previously supported unboxed documents remain semantically and visually compatible, including CSS selector specificity, text extraction, pagination and explicit font behavior.
- Unknown or malformed CSS, unsupported nesting and impossible geometry fail *before* any output bytes are copied. Do not pretend full browser CSS/HTML compatibility.

## 2. Existing architecture and reason for Approach A

Currently `HtmlDocumentReader.Read` flattens `div`/`section`/`article` into `List<Paragraph>`; `DocumentModel` contains `TextStyle`, `Paragraph` and `PlacedText`; `TextLayout.Layout` flows a flat paragraph list; `PdfSerializer` and `UnicodePdfSerializer` emit text only. `CssStylesheets` computes per-element styles, and `CssDeclarations.Parse` currently expects a single value token.

An alternative paragraph-only decoration patch is rejected. It cannot preserve container backgrounds, nested padding, true content widths or cross-page borders and would be replaced again for tables/images.

Use **Approach A**: retain block structure, perform recursive width/vertical layout into page fragments, and paint from a shared page display-list abstraction. Only **internal** models change. The document markup remains HTML/CSS.

```text
HtmlTokens + per-document CssStylesheets
                 |
           HtmlDocumentReader
                 v
       DocumentRoot / BlockNode
       - supported block children
       - text paragraphs + text runs
       - immutable computed CSS box styles
                 |
          BlockLayout / pagination
                 v
      LayoutPage [BoxFragments + TextRuns]
                 |
          PageContentPainter
           /            \
      ASCII PDF      Unicode PDF
                      (existing fonts, CID maps, subsetting)
```

Separate units have single responsibilities: CSS parsing/cascade, structural HTML reader, dimensions and vertical flow, page-fragment generation, PDF painting, and PDF object/font serialization. Neither serializer determines box dimensions.

## 3. Tree construction and supported elements

- Internal `DocumentRoot` contains document-order `BlockNode` children; a `BlockNode` stores its tag, computed style and children (nested boxes or text-containing paragraphs). Document root itself is not selectable or painted.
- Eligible painted/layout block nodes: `body`, `div`, `section`, `article`, `p`, `h1`–`h6`. `html` remains a structural/inheritance wrapper, without box decoration in this milestone; box-model declarations targeting `html` fail explicitly. `head`, `title`, `meta` and `style` remain non-rendering metadata.
- `span` is an inline text-style boundary; `br` a forced line break. This milestone does **not** paint inline boxes. A matched `width`, padding, border or background declaration on `span`/`br` is rejected rather than ignored.
- Consecutive direct text/inline children of a block container form one anonymous paragraph; a nested block ends that run. Anonymous paragraphs have no default margins and inherit computed text properties. Text before or after an explicit paragraph must not vanish.
- Keep current supported optional `p` closures, strictly validated nesting, DOM/CSS selector ancestor paths, heading user-agent font defaults and the two-pass discovery of later `<style>` blocks. No HTML5 tree-construction claim.
- Move existing paragraph `margin-top`/`margin-bottom` and `break-before`/`break-after` to paragraph/heading box styles. Do not inherit these box properties into children. Preserve `h1`–`h6` defaults.

## 4. CSS property grammar and cascade

Extend the existing **strict** lexer/declaration processor to accept multiple tokens, primarily for four-value padding and border shorthands, while retaining CSS source offsets, comments, declaration/source order and `!important` handling. Do **not** start accepting arbitrary CSS functions or unknown properties.

| Property | Supported first increment | Initial/computed behavior |
| --- | --- | --- |
| `width` | `auto`, nonnegative `pt`, `px`, `%`, unitless `0` | `auto` |
| `padding` | one to four `pt`/`px`/`%`/`0` values | all sides 0 |
| `padding-top/right/bottom/left` | same single length | 0 |
| `border` | up to three order-insensitive terms: optional width, `solid`/`none`, optional color; duplicate or unknown terms fail | style `none` by default |
| `border-top/right/bottom/left` | supported side-specific shorthand | style `none` |
| `border-width`, `border-style`, `border-color` | one to four side values | medium / none / currentColor |
| `border-{side}-width/style/color` | a single supported value | medium / none / currentColor |
| `background-color` | existing named/`#rgb`/`#rrggbb` colors and `transparent` | transparent |

Four-value expansion follows CSS top/right/bottom/left mapping (1 -> all; 2 -> vertical/horizontal; 3 -> top/horizontal/bottom; 4 -> TRBL). `border-style` is only `solid` or `none`. Border lengths are `pt`, `px` or unitless zero; no percentage border widths. Supported colors reuse the existing `InlineCss.Color` grammar, plus `currentColor` for border and `transparent` for background. Medium border width defaults to 3 CSS pixels, but a side with style `none` consumes **zero effective border width** regardless of specified width.

`inherit`, `initial` and `unset` apply to every new longhand. All new properties are **non-inherited**. Shorthand declarations expand to longhands *before* winner selection; cascade is separately resolved for each edge/property, respecting `!important`, author inline priority, specificity and order. Border colors using `currentColor` resolve against the *final computed* text color, not whichever declaration happened to be processed first. A high-priority longhand must override only its side, not overwrite unrelated edges; longhands and shorthands mix in either source order.

Reject unsupported `box-sizing`, `min/max-width`, `margin-left/right`, `background` image/gradient shorthands, `border-radius`, dashed/dotted styles, `overflow`, positioning, Flex/Grid, tables and images. Old supported property meanings remain unchanged.

## 5. Width and horizontal placement

DocumentRoot content rectangle = A4 (or configured page width) minus the existing symmetric `PdfOptions.Margin`. All x/y/width values used by layout are PDF points; 1 CSS px = 0.75 pt. A `body` background paints only the body's laid-out block fragment area inside page margins; full-page canvas background propagation/bleed is intentionally not implemented.

For each block, resolve its containing block's **content width** `C`. Percent `width` and percent **padding on every side (including top/bottom)** refer to `C` per CSS 2.1, not to the page or parent's height. The declared `width` is **content-box width**:
- `auto` = `C - left/right padding - effective left/right borders` (clamped only for exact round-off; a negative value is an error).
- absolute / percent = the declared content width, independent of padding and borders.
- Outer width = content width + horizontal padding + effective horizontal borders.
- Left edge = containing block's content left edge; children begin at parent content-left (after its border and padding). No `margin-left/right`, centering, floats or arbitrary alignment in this milestone.
- If outer width exceeds `C`, or computed child content width is negative/invalid, fail `FPDF1302` with useful source position rather than paint outside the containing box. `text-align:center/right` aligns text **inside that block's content width**, not the box itself.
- Nonnegative finite widths and padding only; each resolved length bounded by 14,400 pt and available page/work limits. Unbreakable words wider than a box continue to fail explicitly rather than clip.

## 6. Vertical block flow, margins and pagination

Children flow vertically in source order inside their parent's *content rectangle*. A box with no children can still render its padding, border and background. Content height is intrinsic; explicit CSS `height`, min-height and overflow modes are deferred.

Preserve existing vertical paragraph margins using the maximum of nonnegative adjoining **paragraph/heading sibling** margins within the same parent. Do not attempt CSS parent-child/sibling-across-container margin collapsing in this increment. Container boxes have no default margins and do not inherit paragraph margins. No negative margins.

One document may span many pages. Record a **box fragment** for each page that intersects painted box geometry, with absolute page-space bounds, nesting depth, first/last flags and ordered display operations:
- First fragment paints top border and top padding; last fragment paints bottom border and bottom padding; intermediate fragments do not repeat those vertical edges/paddings.
- Background fills the visible rectangle on **each** fragment, including split content; side borders are painted down **each** fragment. Between its first and last page, a spanning container's fragment extends through the page's usable vertical content area, not through the physical page margins. Border corners are square, no clipping-radius support.
- Before laying the first content line of a box, if its first-fragment top border/padding plus that line cannot fit in the page's remaining usable vertical space, advance to the next page. An indivisible top/line/bottom requirement larger than an entire page is a clear layout error. Avoid phantom decoration-only pages and trailing blank pages.
- Explicit `break-before/after:page` remains effective for paragraphs/headings. Nested boxes must not obscure or duplicate these breaks.
- Empty boxes whose decoration alone exceeds the usable page height fail rather than loop or truncate. Boxes with long content fragment and respect `MaxPages`.
- A box that contains several page-spanning descendants produces appropriately aligned outer fragments on those same pages. Nested backgrounds/borders cannot cover or clip descendants or overflow margins.

For this milestone, no footnotes, repeated headers, `@page`, widows/orphans, keep-together, arbitrary overflow clipping or full CSS fragmentation conformance.

## 7. Display list and PDF painting

Use page-local internal drawing commands: `FillRect`, `StrokeEdge` (one solid edge, width and color) and existing `DrawText` (with font reference and unaltered Unicode scalar mapping). Geometry carries **point units** with top-to-bottom layout; painter converts to PDF bottom-to-top coordinates.

Paint in stable document/nesting order: containing-box background, its borders, descendant backgrounds/borders, then content text. Page decoration is behind text, including across fragments; save/restore graphics state around fill and stroke so state cannot bleed into text. Both ASCII and Unicode serializers must consume the same generated geometry commands, not duplicate border calculations. No graphics/PDF runtime library.

Keep PDF object numbering/xref/stream lengths correct and both text encodings searchable/copyable. Existing font-subsetting logic sees only used `DrawText` glyphs and must continue to embed fonts once. Graphics must not create unnecessary font resources.

## 8. Safety, memory and error handling

No global mutable CSS/layout state. Preserve cancellation checks during parsing, recursive layout, pagination, display-list creation and serialization. Bound node count and recursion with existing element/depth limits. Add `PdfOptions.MaxDisplayCommands` (default **200,000**), counting text and drawing commands across all pages; reject on overflow with a documented diagnostic before writing.

Preserve fail-before-write semantics for malformed CSS, unsupported values, excessive dimensions, content that cannot fit and page/output limits. Output I/O failure on arbitrary caller streams remains non-rollbackable. Existing CLI temporary-file safety applies. Parsing CSS must avoid quadratic explosion from expanding or matching shorthands; charge generated longhands against declaration/work budgets.

No claim of full hostile-input sandboxing, pixel-perfect browser fidelity or proven peak-memory improvement. Fonts remain trusted input as before.

## 9. Implementation component boundaries

1. `CssDeclarations`/`InlineCss`/`CssStylesheets`: token/value grammar, longhand expansion, computed `BlockStyle` and text style; existing specificity and work budget reused.
2. `HtmlDocumentReader`/`DocumentModel`: replace flattened paragraphs with explicit block tree and anonymous runs while preserving selector ancestor paths and omission behavior.
3. `BlockLayout`: geometry, percentage resolution, flow and pagination into bounded `BoxFragment` records.
4. `PageContentPainter` and serializers: rectangle fills, solid edges and text drawing for both ASCII/Unicode.
5. CI/tests/docs: regression fixtures and reproducible Native AOT/independent PDF inspection.

These components remain internal. Public interfaces do **not** add a new document-markup language or mandate a single implementation-language identity.

## 10. Acceptance and evidence

- Unit tests: TRBL shorthand expansion, all four border edges, shorthand-vs-longhand priorities, `!important`, inherited text vs non-inherited box props, `initial/inherit/unset`, transparent/currentColor, invalid values, CSS budgets and deterministic output under another locale.
- Geometry tests: explicit/auto/percentage widths; percentage vertical padding relative to containing **width**; nested widths, x positions, right/center text alignment, empty boxes, zero widths, overconstrained width errors.
- Fragmentation tests: first/middle/last decoration, one-page box, long two-page Chinese paragraph, nested multi-page boxes, explicit page breaks and exact page limits; assert commands stay inside page and no runaway empty-page fragmentation.
- Rendering tests: PDF includes expected fill/stroke operators and visually distinct nested panels; independent qpdf/Poppler verify xref, two-page structure, text extraction, absence of missing glyphs/overlap, page bounds and actual rendered decoration. Pixel probes at known background/border/text positions can complement human visual review.
- Compatibility: all existing 223 NUnit cases pass on Windows/Linux/macOS. Existing ASCII, Unicode/CJK, font fallback, full vs subset glyph-equivalence and stylesheet-oracle workflows pass. Linux x64 Native AOT CLI actually publishes and runs; NuGet package license and no-font-bundle checks remain.
- Performance: record document size, time/allocations and representative peak RSS against `main` baseline for unchanged and decorated fixtures; mark shared CI observations preliminary. Do not imply AOT on other platforms or browser conformance.
- Licensing files (including Community License 1.1 and Commercial License explanation) unchanged. Keep work in a **draft feature PR** from latest `main`; no merge/package publication without a separate request.

## 11. Deliberate deferrals and compatibility limits

Inline `span` decoration, full vertical margin collapsing, border-radius/styles beyond solid, horizontal margins, CSS background shorthand/images, HTML image/table support, Flex/Grid/positioning, complex text shaping/RTL, and `@page` all remain future milestones. Unsupported syntax fails explicitly rather than rendering an incorrect approximation.

The version published by this milestone remains an **experimental controlled-HTML/CSS-to-PDF engine**, not a standards-complete browser replacement.

### Implementation references

- CSS 2.1 box dimensions and padding: https://www.w3.org/TR/CSS22/box.html
- CSS Backgrounds and Borders Level 3: https://www.w3.org/TR/css-backgrounds-3/
- CSS Fragmentation Level 3: https://www.w3.org/TR/css-break-3/

References guide syntax/behavior; no browser code or third-party engine is incorporated.
