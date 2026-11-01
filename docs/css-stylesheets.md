## Current-source minimum-height report panels

**Supported:** Ordinary `div`, `section`, `article` and `blockquote` outside
lists and tables can set `min-height` to a bounded nonnegative `pt`, `px`,
`mm`, `cm` or `in` length, unitless exactly zero, a root-computed `rem`
or own-final-font `em`. All lengths resolve to PDF points before layout.
Only actual CSS winners resolve against computed font sizes; unmatched and
losing declarations still undergo syntax/value/applicability validation.
The default initial `min-height` imposes no minimum. It does not inherit by
default. Explicit `inherit` takes the direct parent's computed minimum;
`initial` and `unset` reset to no minimum. Ordinary selector specificity,
source order and `!important` apply without a separate cascade.

**Visible PDF behavior:** The chosen minimum reserves actual vertical page
space for the panel, including empty unpainted panels. For
`box-sizing:content-box` (the default), the specified minimum is inside
vertical padding and borders; with `box-sizing:border-box`, the minimum
includes those decorations (never less than their combined thickness).
Normal content that naturally occupies more height remains the winner:
text lines, fonts, backgrounds, borders and page continuations keep existing
behavior. A short minimum-height panel moves whole to a fresh page when it
cannot fit at the current cursor. A minimum too tall for a fresh page and
its required leading spacing fails `FPDF1302` rather than silently
overflowing. Naturally long boxes can still fragment across pages, with
no extra phantom minimum-height pages. Existing `MaxPages`,
`MaxDisplayCommands`, CSS work and destination protection remain active.

**Excluded:** `height` and `max-height` remain unsupported; this is not
a general CSS sizing engine. Percentages (indefinite paged content height),
`auto`, negative values, `calc()`, viewport/`lh` lengths, implicit
table/list/image/inline sizing, and `min-height` on `p`/headings/body,
tables or any list/table descendant explicitly fail
`FPDF1201`/`FPDF1202`. Overlarge or unplaceable minimum geometry rejects
`FPDF1302`; the previous caller PDF bytes/position are preserved.
A scoped [inspection example](../examples/report-min-height-cards.html),
`ReportPanelMinHeightTests.cs`, and
[a 40-card Native AOT comparison](../examples/report-min-height-cards-native.html)
against an independently specified `padding-top` PDF oracle verify true
text/paint geometry, font-unit parity, page movement, native output,
failures and recovery. This source addition does not rebuild the retained
preview, approve unseen reference pages or grant customer rights.


## Current-source rounded business-report cards (2026-11-01)

Ordinary `p`/`pre`/`h1`–`h6`/`div`/`section`/`article`/`blockquote`
outside lists/tables support the one-to-four-value physical `border-radius`
shorthand. The physical `border-top-left-radius`,
`border-top-right-radius`, `border-bottom-right-radius` and
`border-bottom-left-radius` longhands each set one independent corner.
Every value is zero or 0.25–72pt in `pt`, `px`, `mm`, `cm`, or `in`. Two values alternate opposite corners (TL/BR and TR/BL); three values
assign TL, TR/BL, BR; four values assign TL, TR, BR, BL clockwise. Actual
opaque PDF backgrounds and uniform solid border rings use separate cubic
curves at each corner. Overlapping arcs are proportionally fitted to the
available fragment. Neither text metrics nor outer dimensions change.
On continued report boxes only the first fragment's top and the final
fragment's bottom receive curved corners; continuation sides stay straight.
The shorthand is expanded into four separate cascade candidates **before**
matching. A later/more-specific longhand overrides its single corner;
`!important` competes per corner. An important shorthand beats a normal
longhand, and an important longhand overrides a normal shorthand only on
its own corner. `inherit` takes the corresponding corner from the direct
parent; `initial` and `unset` reset that corner without implicitly
inheriting other values. Unmatched declarations and losing values are
still validated. Output budget and destination protection remain active.

Percentages, elliptical `/` radii (including two axes in one corner
longhand), `em`/`rem`, box radii on table cells, images, lists or inline
elements, nonuniform/missing,
dashed/dotted borders, CSS background clipping, full CSS Backgrounds &
Borders and browser layout are **not** implemented; incompatible input
fails explicitly. `border-radius:0` preserves the old PDF bytes.
This is source capability, not part of the previously retained private
preview binary, human PDF approval or an evaluation grant. The
[54-card authored sample](../examples/report-rounded-panels.html) and
[separate 16px/12pt projection](../examples/report-rounded-panels-oracle.html),
plus the [50-card four-corner report](../examples/report-asymmetric-rounded-panels.html)
and [separately authored physical-length oracle](../examples/report-asymmetric-rounded-panels-oracle.html),
plus the [50-card corner-longhand report](../examples/report-corner-longhands.html),
exercise actual PDF bytes and pagination; NUnit and Linux/Windows
Native AOT/Poppler/qpdf tests verify painted curves, byte-for-byte
shorthand/longhand equivalence and protected-error recovery.

## Current-source bounded vertical alignment for fixed-column table cells

Fixed-column `td` and `th` accept the real PDF-rendered values
`vertical-align:top`, `middle` and `bottom` for an intact table row.
The shorter cell's text and direct image placements move by half/all
the available vertical gap, while backgrounds, borders and column
dimensions remain unchanged. Whole rows and repeated headers work in
multi-page reports, with the existing selector cascade and `!important`.

`inherit` reads the computed parent; `initial`/`unset` return to
non-inherited `top`. No alignment is silently ignored: split-body-row
middle/bottom fails `FPDF1302`, baseline/sub/super/length/percentage
values fail `FPDF1202`, and non-cell targets fail `FPDF1201`.
This does not implement general CSS inline vertical alignment or
CSS Tables. See [the table contract](tables.md#vertical-alignment-in-whole-table-rows),
[the source tests](../tests/FactsPDF.Tests/TableVerticalAlignmentTests.cs)
and [the 40-row report](../examples/report-table-vertical-align.html).
This is source-only, not a rebuilt private candidate or authorised distribution.

# Embedded CSS stylesheets and selectors


## Current-source colored and patterned report text decorations

Controlled invoice corrections, inspections and approval notes support one text
decoration line: `none`, `underline`, `line-through` or `overline`.
`text-decoration-line` chooses the line; `text-decoration-style` supports
`solid`, `dashed` or `dotted`, and `text-decoration-color` accepts `currentcolor`
or opaque `#rgb`, `#rrggbb`, named/RGB colours from the existing palette.
The `text-decoration` shorthand takes exactly one line keyword plus optionally
one supported stroke and one colour, in any order. `none` stands alone.
Shorthand omission resets stroke to `solid` and ink to `currentcolor`.
Normal declaration order, specificity, `!important` and CSS-wide
`inherit`/`initial`/`unset` work through the existing cascade.

```html
<style>
  .old { text-decoration: line-through dashed #c02020; }
  .new { text-decoration: underline dotted #008000; }
</style>
<p class='old'>Original estimate AUD 3,450.00</p>
<p class='new'>Reviewed estimate AUD 3,200.00</p>
<p style='color:blue;text-decoration:overline dashed currentcolor'>Signed off</p>
```

On supported paragraph/heading and inline text runs, the selected ink and
pattern paint an actual PDF path at a font-proportional height, clipped to
the **real measured glyph width**. Wrapped lines and subsequent pages paint
their own runs, including explicit TrueType faces. Existing solid
underline/strike-through output remains byte-identical by default.

This is a deliberately **bounded** CSS Text Decoration subset: no simultaneous
lines, `double`, `wavy`, alpha, arbitrary line thickness/offset,
HTML `<u>`/`<s>`, browser-style descendant decoration propagation, bidi,
or complex shaping. Invalid values and targets raise `FPDF1202`/`FPDF1201`,
unpaintable geometry `FPDF1302`, and insufficient paint/byte budget `FPDF1401`.
Failures preserve the original PDF destination. Tests cover API/CLI, actual
PDF paint streams, TrueType glyph widths, inline overrides and multipage flow.
See [printable 60-entry report](../examples/report-text-decoration-paint.html)
and [regressions](../tests/FactsPDF.Tests/TextDecorationPaintTests.cs).
Exact-head CI, independent review, retained binary, human PDF approval and
commercial rights remain separate from this source implementation.
## Current-source bounded patterned report borders

The existing CSS box targets (ordinary report boxes, images and fixed-layout
tables/cells where borders were already implemented) now accept
`solid`, `none`, `dashed` and `dotted` for `border-style`,
`border-top/right/bottom/left-style`, and the corresponding `border` and
individual side shorthands. The 1–4-value `border-style` expansion and CSS
importance, specificity, source order and per-side CSS-wide keywords continue
to use the existing cascade. A style is non-inherited unless `inherit` wins.
Border widths remain the existing nonnegative, bounded absolute values and
thin/medium/thick; colours use the existing opaque RGB and `currentcolor`
rules. There is no new element applicability, font synthesis or layout mode.

A nonzero dashed edge paints a PDF stroke with butt caps and an on/off
pattern of three times the resolved border width each. A dotted edge uses
round caps and a zero-approaching on segment with an off gap of twice the
width, creating visible circular marks. Each stroke is contained in the
existing border rectangle, charged as one display command, and enclosed
in PDF `q/Q` graphics state. Fill backgrounds and legacy solid rectangles
are unchanged. Patterned edges consume the *same* measured geometry as solid
ones: padding, text X/line wrapping, nested boxes and table/image positioning
are not estimates. At page boundaries vertical strokes restart their pattern;
top/bottom strokes appear only on first/last decorated fragments. There is
no global dash continuity across pages or CSS background clipping.

Only the supported patterns are painted. `double`, `hidden`, `groove`,
`ridge`, `inset`, `outset`, arbitrary `border-image`,
`border-radius` with dashed/dotted/asymmetric borders, invalid mixed shorthand terms and out-of-scope elements
fail with existing `FPDF1201/1202`; impossible widths with `FPDF1302`,
display-command limits with `FPDF1401`. Sub-millipoint patterned strokes
that would round to an invisible PDF linewidth fail `FPDF1302` instead
of silently turning into a hairline. Invalid input never overwrites a
previous PDF file; subsequent valid conversions recover normally.

The [76-record multipage report](../examples/report-patterned-borders.html)
is verified through the real Native AOT CLI, uncompressed PDF stroke
commands, Poppler item positions, page bounds and failure/recovery checks
in `scripts/verify_native_feature_paths.py`; source NUnit cases separately
check cascade, all sides, exact solid byte preservation, cross-page borders,
resource limits and invalid values. That is scoped engineering evidence,
**not** human page approval, release, or full CSS Borders conformance.

## Current-source multiline report notes

Explicit `p` / `h1`–`h6` report text, including an explicit paragraph in
a fixed-width table cell, accepts `white-space:pre-line`. This **changes actual
PDF glyph baselines and pagination**, not only the CSS parser. ASCII space and
tab runs collapse like `normal`; LF, CRLF (one break), CR, and form feed retain
logical line breaks. Spaces adjacent to those breaks disappear; a final authored
segment break is ignored, but consecutive interior breaks create empty lines.
HTML `<br>` remains an explicit break, including after a source break.
Soft wrapping, word measurement, font fallback, `overflow-wrap:break-word`,
`text-align`, first-line indent and all existing box resource limits still apply.

```html
<p style="white-space:pre-line">Asset 018
  Inspection complete
  Follow-up next week</p>
```

Stylesheet selectors, inline declarations, `!important`, `inherit`,
`initial`, `unset`, and element computed inheritance follow the same
whole-paragraph cascade as `normal`/`nowrap`. `pre-line` does **not** retain
runs of spaces or tabs, allow arbitrary preformatted text, or enable RTL,
vertical writing, bidi, complex shaping, negative length or new font sources.
Explicit declarations on containers, spans, images and table structural elements
still fail `FPDF1201`. `break-spaces`, functions and mixed
keyword values still fail `FPDF1202`, including unmatched selector declarations.
Overwide unbreakable tokens fail `FPDF1302`; missing fonts retain their
existing resource errors. Failed CLI/API output preserves a previous PDF.

The [72-record authored note report](../examples/report-pre-line-notes.html)
and [separate HTML-br oracle](../examples/report-pre-line-notes-oracle.html)
exercise actual output bytes and paginated glyph placement, including
Linux/Windows Native AOT and Poppler checks via
`scripts/verify_native_feature_paths.py`. Initial source implementation
commit: `6ab5d2c8ff3c10c76e928b6ad192d9ac46965c89`. This source scope
does not imply current package publication or human page approval.

## Current-source preformatted audit blocks

The explicitly closed `<pre>` element is a bounded **standalone report
paragraph**. It has the same default 8pt trailing paragraph margin as a
plain `p`, but its default whitespace flags are equivalent to
`white-space:pre`: authored U+0020 spaces (including leading, consecutive
and trailing), CRLF/CR/LF/form-feed line breaks and explicit `<br>` become
measured PDF advances/lines. A line never automatically wraps at words,
spaces or CJK opportunities. Each line must fit the computed content width
and first-line indent, otherwise `FPDF1302` leaves the previous PDF intact.
Only glyphs supplied by the caller's font roles or built-in Courier can paint;
there is no default browser font family or inferred mono-style substitution.

```html
<pre>REF001   OK
REF002   NEEDS REVIEW
TOTAL    002</pre>
```

`white-space:pre` also applies on `p`/headings including explicitly styled
`p` inside a fixed-column table cell; author CSS on `pre` may choose other
supported whole-paragraph modes. Selector/inline cascade, `!important`,
CSS-wide `inherit`/`initial`/`unset`, real font metrics, page continuation,
text colours and output recovery use the existing paragraph engine.
No `pre` within a table/list, no nested block elements within `pre`, no
unclosed `pre`, tab stops, complex shaping, RTL or `break-spaces`.
In preserving modes, unsupported control whitespace (including tab)
raises `FPDF1305`. Inline `white-space` declarations remain unsupported
(`FPDF1201`); invalid values raise `FPDF1202`.

The [72-row source example](../examples/report-preformatted-audit.html)
and [separately authored pre-wrap equivalent](../examples/report-preformatted-audit-oracle.html)
must produce byte-identical PDFs while every authored line fits. Native
AOT/Poppler geometry checks measure the extra two-space x advance and
confirm page text, overflow failure and output restoration. Source commit
`d8d1a9e083bfc83829275ec08f6038261b4e1663`; this is not full HTML
`pre` support, a released CLI or a human-approved reference PDF.

## Current-source preserved-space report notes

For authored notes with meaningful horizontal spacing, explicit paragraphs
and headings can opt into `white-space:pre-wrap`. Unlike `pre-line`, each
literal U+0020 space is measured and painted as a glyph, including spaces at
the start/end of a line and repeated spaces between labels. CRLF (including
across styled spans), CR, LF and form feed create real line boxes. `br`
continues to force a break. Ordinary soft word wrapping still happens at
spaces and existing CJK opportunities, with actual page continuation and
full destination-file protection on failed conversion. Leading spaces count
towards the available line width; when necessary they wrap onto another
line instead of disappearing. CSS `text-align`, inline font metrics,
font fallback, `overflow-wrap:break-word`, and existing resource budgets
operate on the measured output.

```html
<p style="white-space:pre-wrap">REF001  ASSET   CHECK
  OWNER:  AB</p>
```

Boundaries: only whole explicit `p`/`h1`–`h6` (including explicit
paragraphs in fixed-width table cells). Container/span/structural-table
declarations remain `FPDF1201`. Tab characters and other unimplemented
control whitespace need tab-stop/text processing and fail `FPDF1305` rather
than collapsing silently. `pre`, `break-spaces`, CSS Text shorthand
combinations, RTL and complex shaping are unsupported (`FPDF1202` for
unsupported values). Extremely long words, or a single space wider than
the measured line, fail `FPDF1302`. This is a documented bounded PDF
report behavior, not general browser CSS `pre-wrap` conformance or a
released preview binary.

The [72-entry preserved-space report](../examples/report-preserved-space-notes.html)
has a [separately authored collapsed-space control](../examples/report-preserved-space-notes-oracle.html);
`WhiteSpacePreWrapTests.cs` checks exact glyph runs, TrueType metrics,
CSS cascade, page continuation, table cells and negative recovery.
`verify_native_feature_paths.py` checks actual Native AOT Poppler word
offsets against that control, unique references, equal line baselines,
pagination and preserved output across failed-then-successful conversion.
Source SHA of the implementation remains distinct from published binaries.

## Current-source LTR text-align start/end

The engine renders `text-align:start` and `text-align:end` on the same
supported paragraphs, headings and inherited text containers as physical
`text-align:left/right`. In the **existing left-to-right horizontal-only**
report model, `start` resolves to `Left` and `end` to `Right` as computed
text styles. Embedded selectors, inline styles, specificity, `!important`,
inheritance and `inherit/initial/unset` continue to select the resulting
physical side. No declaration is accepted then ignored: the measured
per-line PDF glyph start X, font advances, padding/width/indent and
pagination use the same renderer as explicit left/right. Default/no-CSS
output bytes remain unchanged.

Scope intentionally excludes RTL, bidirectional or vertical writing:
`direction` and HTML `dir` are still rejected, and `text-align:start/end`
do not request either writing mode. Inline spans cannot specify start/end or justify;
unknown/multi-token values such as `match-parent`, `justify-all` and
`start end` fail `FPDF1202`, unsupported `direction`/inline alignment
fail `FPDF1201`, and geometric failures retain `FPDF1302`. The prior
destination PDF and stream position remain protected. Existing
`text-align:justify` behavior and its final-line/space limitations are
unchanged; this is **not** full CSS Text support.

The [46-entry paginated LTR example](../examples/report-logical-text-align.html)
and separately authored [left/right oracle](../examples/report-logical-text-align-oracle.html)
are compared as real equal PDF bytes by Native AOT with qpdf, searchable
first/last markers and protected-error recovery. Source
`LogicalTextAlignmentTests.cs` also covers glyph coordinates, cascade,
inheritance, explicit TrueType metrics, page continuation and negatives.
Exact-head CI, independent review, private candidate distribution and
human page approval remain separate gates.

## Current-source bounded quoted-report panels

`blockquote` is a **real block container** at report root or inside ordinary
body/div/section/article/blockquote contexts. It accepts direct text,
explicit paragraphs/headings and their existing permitted inline children,
with the same block CSS properties and positional painting as a `div`.
Its default non-collapsing outside margins are **8pt top/bottom and 24pt
left/right**. Those values are actual PDF layout insets, not ornamental
metadata; an unstyled quote is byte-equivalent to a `div` using
`margin:8pt 24pt`. The normal per-property cascade can override the
defaults with physical or LTR logical margin/padding declarations; explicit
`initial`/ `unset` reset to zero and `inherit` reads the computed parent
side. Inherited text colour/fonts, backgrounds, solid per-side borders,
width/min/max-width, `box-sizing:border-box`, alignment and multiline
pagination reuse their already bounded element contracts.

Unsupported: quote-in-list, quote-in-table/cell, quote-in-inline; `cite`
and browser citation metadata; CSS generated quote marks; `q`/`cite`
elements; RTL/vertical writing; margin collapse; negative margins; vertical
margin percentages/auto; height, arbitrary display, floating and elliptical/per-corner
rounded border painting. A single physical radius with a uniform solid ring
is separately supported. Existing unsupported CSS and unplaceable geometry reject
with `FPDF1101/1103/1201/1202/1302`, preserving the original PDF output
on conversion failure. Quoted content is **not** a tagged PDF quotation.
No separate font download or asset is involved.

[44-item paginated quoted service report](../examples/report-blockquotes.html)
and its [physical div oracle](../examples/report-blockquotes-oracle.html)
prove real PDF-byte equivalence, searchable text, page fragments and protected
failure recovery via `BlockquoteReportTests.cs` and
`scripts/verify_stylesheets.py` on supported actual CI platforms. New
source evidence is not a human PDF approval or private candidate rebuild.

## Current-source LTR logical report spacing

Four logical shorthands are supported for **normal left-to-right horizontal
writing only**: `margin-inline` maps inline-start/inline-end to
`margin-left`/`margin-right`; `margin-block` maps block-start/end to
`margin-top`/`margin-bottom`; `padding-inline` and `padding-block`
map to their analogous physical sides. One value supplies both sides; two
values supply start and end in that order. A CSS-wide `inherit`, `initial`
or `unset` must be alone; `!important` and the existing stylesheet/
inline per-longhand cascade apply normally. Expansion counts both emitted
declarations against `MaxCssDeclarations`, including unmatched rules.

This is a **rendered geometry feature**, not an accepted-but-ignored alias:
on `p`/`h1`–`h6`, `margin-block` uses nonnegative
`pt/px/mm/cm/in/rem/em` (or zero), and `margin-inline` uses the existing
bounded nonnegative physical lengths plus percentage/auto outside table cells.
On normal `div`/`section`/`article` panels and standalone `hr`, the
same physical outside-box margins are applied: horizontal `auto` can centre
an explicit width; vertical percentages/auto are rejected. Padding maps to
the existing box model on permitted painted boxes, using nonnegative
`pt/px/mm/cm/in/%/rem/em`, with vertical percentages resolved from the
containing **width**. Relative `em` uses the element's final computed font,
and `rem` the computed root font. Zero values retain original geometry.
All limits, table-cell restrictions and prohibited elements remain those of
the equivalent physical side. `inherit` reads the appropriate computed
parent side; neither margins nor padding become implicitly inherited.

The box widths, painted PDF backgrounds/borders, text starting X positions
and pagination all use the existing longhand layout. Unsupported values
(negative lengths, mixed wide keywords, three-plus terms, `calc()`,
vertical `auto`/`%` margins or impossible geometry) fail with
`FPDF1201/1202/1205/1302` as appropriate and preserve any existing
output. This feature does **not** implement `direction`, RTL, vertical
writing, logical properties beyond the eight scoped side longhands, general
margin collapsing, `gap`, Flex/Grid or Paged Media.

[Logical 36-entry report](../examples/report-logical-spacing.html) and
[separately specified physical-longhand oracle](../examples/report-logical-spacing-oracle.html)
exercise equivalent actual PDF bytes; `LogicalReportSpacingTests.cs`
covers full conversion, geometry, cascade, computed lengths, CLI parity,
fragmentation, invalid input and output recovery. Existing SDK/native CI
remains the authoritative platform gate for the exact source commit.

### Individual LTR logical margin/padding sides (eight names)

Current source additionally renders all eight **one-value** logical side
longhands: `margin-inline-start/end`, `margin-block-start/end`,
`padding-inline-start/end`, and `padding-block-start/end`. The parser lowers
**each** to exactly one existing physical side in LTR horizontal writing:
left/right for inline-start/end and top/bottom for block-start/end. They may
appear in embedded stylesheets or inline CSS alongside physical longhands and
one/two-value logical shorthands. Normal property-by-property specificity,
source order, `!important` and `inherit/initial/unset` apply; no shorthand
silently overwrites an unrelated side. A single emitted declaration consumes
one `MaxCssDeclarations` slot. All other supported element/value restrictions
are **exactly** those of the equivalent physical side, including the bounded
font-relative `rem/em` and percentage geometry.

Numeric exact-zero spellings (`-0`, `0.0`, `0e999`) normalize to zero.
Unitless nonzero or underflowing numeric forms, negative physical lengths,
multiple values, `calc()`, unsupported elements, vertical `auto/%` margins,
RTL and vertical writing reject with the existing `FPDF1201/1202/1205` or
unfit geometry `FPDF1302`, preserving any previous destination bytes. The
existing physical length parser also rejects nonzero unit values that would
round to zero in a floating-point conversion, rather than accepting them as
accidental zero box lengths. No new CSS layout model, margin collapse, browser
logical properties or private software candidate is introduced.

The [40-reference source report](../examples/report-logical-longhands.html)
and independently written [physical longhand oracle](../examples/report-logical-longhands-oracle.html)
are compared as **real multipage PDF bytes** by Linux Native AOT, with
qpdf, searchable text markers and protected invalid-target recovery;
`LogicalReportLonghandTests.cs` also asserts actual run X, painted boxes,
per-side cascade, root/element fonts, page continuation, CLI parity and
strict declaration budgets. Exact-head CI and truly independent code review
remain separate merge gates; this is source, not a public binary release.

## Current-source HTML horizontal report rules

A standalone void `<hr>` or `<hr />` in an ordinary report box now
produces a **visible PDF separator**: 1pt solid `currentcolor` top border
over the containing content width, with 8pt non-collapsing outside margins
above and below. Its box is really painted and affects following text
positioning. The rule creates no text runs, new font resource or remote load.

A controlled rule accepts the existing scoped box `width`, `min-width`,
`max-width`, `box-sizing`, `padding`, `background` and solid
`border` properties, `color` and four-sided `margin` with nonnegative
`pt/px/mm/cm/in/rem/em` top/bottom and nonnegative horizontal
`pt/px/mm/cm/in/%/rem/em` or `auto`. Only `font-size` is accepted
from typography, as a scale for rule-local `em` values. The existing
selector/inline cascade, importance and `inherit/initial/unset` apply.
The rule's actual height is vertical padding plus effective top/bottom
solid border widths. Explicit `border-top:none` removes the default
ink; no minimum synthetic content height is added.

A separator moves as one box to a fresh page if it cannot fit after prior
report content. A rule too tall for a fresh page fails `FPDF1302`
without altering any existing target PDF. Its painting is charged against
display command limits and preserves containing panel fragments.

`hr` is rejected inside paragraphs/headings, spans, lists or tables
(`FPDF1101`), as are explicit `</hr>` closing tags; unrecognised
attributes fail `FPDF1103`. Text-only declarations such as
`text-align`, `font-weight` or `line-height` reject with
`FPDF1201`. Negative/percentage vertical margins, unsupported functions
and unsupported dashed/rounded border combinations fail `FPDF1201/1202`. This does not
implement margin collapsing, `height`, pseudo-elements, `display`,
complex rules, table/inline separators or general CSS conformance.

The [44-entry report](../examples/report-horizontal-rules.html) and
[independent painted-div oracle](../examples/report-horizontal-rules-oracle.html)
are compared via native CLI PDF bytes, qpdf, extracted text, pagination and
protected-output failure in `scripts/verify_stylesheets.py`.
`HorizontalRuleTests.cs` covers managed PDF equality, exact geometry,
selector precedence, computed lengths, page edges and budgets. New source,
CI and review evidence do not relabel old private candidates or provide
human page approval.

FactsPDF now accepts embedded CSS stylesheets in addition to inline styles. This is a bounded, strict implementation for controlled document templates, not full browser CSS. It does not add a new markup language or a browser dependency. The previous font, Unicode and optional subsetting paths are reused.

## Example

```html
<html>
<head>
<style>
  body { font-size: 12pt; line-height: 1.5; color: #222; }
  h1, h2 { color: #1d4568; }
  .report > p.lead { font-size: 16pt; }
  .report .accent { color: #008000; }
  #summary { color: #1d4568 !important; }
  p.next { break-before: page; }
</style>
</head>
<body>
  <section class="report">
    <h1>Report</h1>
    <p class="lead">Hello, world.</p>
    <p id="summary">A paragraph with <span class="accent">shared styling</span>.</p>
    <p class="next">A new page.</p>
  </section>
</body>
</html>
```

No new command-line flag is required to enable stylesheets. With the .NET 10 SDK and your own trusted, appropriately licensed static TrueType fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Font paths are examples, not shipped files. The fontless ASCII mode still works. Library callers use the same `PdfConverter.Convert(html, output, options)` entry point.

## Sources and selectors

| Area | Supported |
| --- | --- |
| Stylesheet source | Embedded `style` at head, document or supported container level |
| Type and media | type absent/empty/text/css; media absent/empty/all/print |
| Simple selectors | `p`, `*`, `.note`, `#summary` |
| Compound selectors | `p.note.active#summary`, `*.note` |
| Selector lists | `h1, h2, p.note` |
| Descendants | `.report p`, `.report .accent` |
| Direct children | `.report > p`, mixed child/descendant chains |
| Inline declarations | Existing `style="..."` attributes with the same supported values |

HTML tag selectors are ASCII case-insensitive; class/ID values are case-sensitive. Class attributes are split on HTML whitespace after attribute entity decoding. CSS identifiers in this increment are ASCII; escaped and non-ASCII selector syntax is rejected. A synthetic document root is not an element and cannot match `*`. The existing partial HTML tree-construction rules still apply; no browser DOM conformance is claimed.

All style elements are compiled before content styling, so later styles can affect earlier content. Style contents use HTML RAWTEXT: entities are not decoded and fake markup inside CSS comments is not parsed as elements. The actual HTML `</style>` delimiter still ends the element even inside a CSS quote/comment. CSS comments separate lexical tokens without becoming descendant-combinator whitespace or concatenating identifiers.

No `link` stylesheet fetching, external files, `@import`, `@media`, `@page` or other at-rules are implemented. `media="print"` on a style element is not the same as supporting a CSS `@media` rule. Attribute/sibling/pseudo selectors, nesting, escapes, custom properties and functions remain unsupported. Style blocks within a paragraph/span/title are rejected rather than silently reparented.

## Cascade and inheritance

Winners are selected independently for each property:

1. Important declarations beat normal declarations.
2. Within the same importance level, inline declarations beat stylesheet declarations.
3. Stylesheet declarations use lexicographic ID/class/type specificity, then source order.

Only actually matching arms of a grouped selector contribute specificity. Repeating a simple selector increases specificity. Duplicate declarations use the last winner at the appropriate importance. This author cascade sits above the renderer's existing defaults; it does not implement user-origin styles, cascade layers, animations or transitions.

Inherited values are the parent's computed values; the parent's importance and selector specificity do not compete with declarations on a child. `inherit`, `initial` and `unset` are supported for the text properties below. `unset` resolves to inheritance or the initial value, not a lower-priority heading/paragraph default.

Renderer initial values are `PdfOptions.FontSize` (normally 12pt), line-height 1.2, black, left alignment, zero margins and automatic breaks. These are explicitly FactsPDF defaults, not a claim of browser initial font/theme behavior. Heading font-size multipliers and the default 8pt paragraph-after gap remain when no author declaration replaces them.

## Property scope

| Property | Values / applicability |
| --- | --- |
| font-size | pt/px/mm/cm/in lengths, positive parent-relative percentages and `em`, or positive `rem` relative to the computed `html` root font (configured initial font when absent); computed 1–144pt |
| font-weight | normal/bold or numeric hundred steps 100–900 (100–500 normal role; 600–900 bold role), including equivalent CSS number tokens; explicit normal/bold font-chain resources required |
| font-style | normal/italic; explicit role resources required |
| line-height | Unitless 1–10, `normal` (= default 1.2), positive `%`/`em` (at most 10× element font), positive `rem` (computed root font), or `pt/px/mm/cm/in` lengths; computed 0.1–1440pt, inherited as fixed points except unitless/normal |
| color | #rgb/#rrggbb; black, white, red, green, blue, gray, grey; bounded opaque rgb()/rgba() |
| text-align | left/center/right; bounded `justify` distributes retained U+0020 spaces only on automatically wrapped report lines via measured PDF `TJ` gaps; final/explicit-break lines stay ragged; start/end, RTL and CJK inter-character justification unsupported |
| white-space | `normal` (default), `nowrap`, scoped `pre-line`, bounded `pre-wrap` and unwrapped `pre` on explicit `p` / `h1`–`h6` or supported `<pre>`; `pre-line` collapses spaces, `pre-wrap` retains each space and soft-wraps, `pre` retains each space and source break without auto-wrap; tab stops fail `FPDF1305` in preserving modes |
| letter-spacing | Supported report text and inline spans: `normal`, nonnegative 0–24pt print lengths, or scoped `em` (own computed font) / `rem` (computed root); resolved to fixed points before real PDF glyph advances, wrapping, centring and decorations; negative/%/calc unsupported |
| word-spacing | Supported text and inline spans: `normal`, 0–24pt nonnegative print lengths or scoped `em` (own final font) / `rem` (root font); computed extra advance after each collapsed U+0020 affects real PDF placement, wrapping, alignment, decorations and pagination; negative/%/calc rejected. Not full CSS Text. |
| text-indent | Explicit `p` / `h1`–`h6` outside table cells: nonnegative `pt/px/mm/cm/in/%`, scoped own-font `em` / root `rem`, or zero; percentages use resolved content width, `em/rem` fixed at cascade; only first logical line indented, later wrap/`br`/page fragments retain normal x; unfit first words fail `FPDF1302` with output preservation |
| text-decoration, text-decoration-line | none/underline/line-through as a single value; real PDF text-run decoration, current text colour, no full browser decoration features |
| margin-top, margin-bottom | Paragraphs/headings plus ordinary div/section/article report panels; nonnegative pt/px/mm/cm/in/rem/em or zero (up to 14400pt); panels create real outside-border vertical spacing, without margin collapse; table-cell p rem/em still reject |
| margin-left, margin-right | Paragraphs/headings only outside table cells; nonnegative pt/px/mm/cm/in/%/rem/em or zero, plus `auto`; percentages use containing width, rem uses computed html root, em uses own computed font; auto side takes remaining free space |
| margin (bounded shorthand) | Paragraphs/headings and scoped div/section/article panels; 1–4 TRBL values, vertical pt/px/mm/cm/in/rem/em/zero and horizontal pt/px/mm/cm/in/%/rem/em/zero/`auto`, including `margin:0.25rem 0.5em`; `margin:0 auto` centres an explicit-width outer box; a single CSS-wide keyword accepted |
| break-before, break-after | Paragraphs/headings only; auto/page. break-after also accepts avoid on headings outside lists; see [heading pagination](heading-pagination.md) |

These properties also accept `inherit`, `initial`, `unset` and `!important`. Paragraph-only breaks and text styles remain inapplicable on containers/spans, even for losing declarations; report-panel margins are a separate bounded exception. Styling `br` is unsupported; a matching declaration errors. Head/title metadata remains non-rendering and does not acquire presentation from stylesheet rules. Inline styling of metadata is still rejected.

This engine's bounded `text-indent` does **not** implement negative/hanging indentation, `each-line`, `hanging`, `calc()`, container-level declarations, inline spans or table-cell paragraph indents. It affects actual glyph positions and first-line wrapping, not painted box geometry; the usual CSS inheritance/wide-keyword resolution is retained only where an applicable paragraph/heading declaration can be expressed.

The original stylesheet increment and later box-layout increments are distinct; current block width/padding/paint scope is documented in [development](development.md), and C1 style selection in [font styles](font-styles.md). Selectors alone do not make new layout features available. Width/height, padding, borders, background fills, flex/grid, tables, images and font-family/@font-face are not added by this increment. Unsupported properties or values fail explicitly even in an unmatched selector. That strict behavior deliberately differs from browser CSS error recovery; use the documented subset, not arbitrary website stylesheets.

## Current-source bounded list marker types

An `ol`, `ul`, or direct `li` may specify one CSS
`list-style-type` identifier from `disc`, `circle`, `square`,
`decimal`, `lower-alpha`, `upper-alpha`, `lower-roman`,
`upper-roman` and `none`. Defaults remain `ol` decimal and `ul` solid disc,
with no byte change for an unstyled existing list. CSS-wide
`inherit`/`unset` read the computed parent kind; `initial`
resolves to disc. An explicit nested list overrides the enclosing
list's kind by its own default unless it asks for inheritance. List
items inherit the parent kind, and individual item overrides do not
renumber later siblings. The existing specificity, source order,
inline origin and `!important` cascade apply. Even unmatched
or losing declarations must have an accepted value.

The marker is part of actual PDF layout, not just an accepted CSS
token: decimal/alphabetic/Roman glyphs use the item's caller-supplied
TrueType role or built-in Courier with fallback, measured width and
tracking. The marker gutter reserves each list's widest text marker;
the list's body X, line wrapping and page continuation are accordingly
computed. A marker occurs only once alongside its first body line,
including continued items; `disc` remains the original filled vector
circle, `circle` draws a hollow stroked outline without painting over
any underlying background, and `square` paints a solid vector box.
Each uses the same list gutter, item colour, pagination and display-command
budget. The default disc PDF bytes are unchanged. `none` removes the marker and paint command but leaves
the list-item's ordinary content/gutter geometry intact.
Alphabetic labels continue after `z` as `aa`; Roman notation is
subtractive and deliberately supported only up to 3999 items
(`FPDF1202` beyond). Missing requested-font marker glyphs fail
with the existing FPDF1504/1507 paths rather than being substituted.

This is not full CSS Lists: `list-style` shorthand,
`list-style-position`, images, counters,
`::marker`, arbitrary nesting, or declarations on non-list elements
are unsupported and rejected. Invalid values reject FPDF1202,
invalid target elements FPDF1201, and failed output leaves an
existing PDF unchanged. No HTML `ol start`/`li value` or new
fonts/platforms are implied. Source implementation
`e137e523264d1250702656ee1968c5ff2f61e8fe` (initial list types),
`7665f2fbafa34e14f9d792e7e7728d67f268e0e3` (circle/square shapes),
with an [80-item Native AOT report](../examples/report-list-vector-shapes.html);
actual first-use evidence requires final CI/independent code review
on the completed PR head. See [list contract](lists.md),
[74-entry example](../examples/report-list-marker-styles.html),
`tests/FactsPDF.Tests/ListMarkerStyleTests.cs` and
`scripts/verify_lists.py`. Historical private binaries are unchanged.

## Current-source font-relative report text spacing

Controlled report templates can use `text-indent:0.4em`,
`letter-spacing:0.1em` and `word-spacing:0.15rem` alongside existing
fixed print lengths. `em` resolves against the **declaring element's final
computed font-size** after stylesheet cascade, even if a later or
higher-priority font-size declaration wins. `rem` uses the computed
`html` font-size. On `html` itself, spacing `rem` uses its
newly computed font-size; a document without `html` uses
`PdfOptions.FontSize`. Inherited spacing stays fixed in points rather
than recomputing on each child. A child's explicit declaration uses its
own newly computed font.

Actual PDF glyph advances, U+0020 word-gap `TJ` commands,
first-line inset, wrapping, alignment, decoration width and pagination
reflect those lengths. `normal` and zero preserve legacy PDF bytes.
Letter/word spacing remains **0–24pt after computation**;
indentation retains its nonnegative box-length limit and first-word-fit
`FPDF1302`. Invalid values fail before replacing a pre-existing output.
Percentage text-indent continues to use actual paragraph content width;
negative/percent letter/word spacing, `calc()`, hanging indentation,
indent on inline elements/containers/table-cell paragraphs, and browser
text shaping are unsupported. This is a bounded CSS Text increment,
not full relative-unit support for borders, table/image sizing, or
arbitrary browser stylesheets.

The [72-entry native report](../examples/report-relative-text-spacing.html),
[independently authored point oracle](../examples/report-relative-text-spacing-oracle.html),
`FontRelativeTextSpacingTests.cs` and Native AOT stylesheet verifier
exercise actual PDF parity, searchable text, pagination, TrueType glyphs
and target-preserving rejection. Source code, passing CI and independent
review remain distinct from an installable candidate, customer grant
or human PDF approval.

## Current-source soft-wrapped report justification

`text-align:justify` is an opt-in left-to-right report-text layout mode.
On each **automatically word-wrapped** line except the final or an explicit
`<br>` break, the remaining measured content width is divided evenly
across retained, collapsed U+0020 word spaces. Existing `word-spacing`
is included first. Indented first lines use their reduced actual content
width. The PDF emits the additional spacing using the existing Courier/CID
TrueType `TJ` path; actual glyph placement and text decoration widths
follow the expanded advances. The behavior continues across pages.
`left`, `center` and `right` retain their existing behavior and
default output remains byte-identical.

An unwrapped or `white-space:nowrap` line, explicit break, final
paragraph line or a line without retained spaces does not expand.
Explicit `text-align:justify` on inline spans rejects with `FPDF1201`;
inherited container alignment applies to the actual paragraph.
Unsupported `start`, `end`, `justify-all`, RTL and CJK
inter-character justification are not provided. Invalid values fail
`FPDF1202`, and overwide text retains its `FPDF1302` protection
of the existing target. This is not full browser CSS Text conformance.

## Current-source bounded word-spacing

`word-spacing:0.01in` (equal to `0.72pt`) is supported on report
paragraphs, headings and inline spans, including inherited and CSS-wide
computed values. HTML whitespace still collapses to one U+0020 between words;
only that retained space gains a fixed nonnegative extra advance, from zero
through 24pt. Glyph measurement, word wrapping, first-line indentation,
alignment, text-decoration widths and pagination use the same value.

Both Courier and embedded CID TrueType PDFs place word gaps using a
`TJ` displacement after the encoded space. PDF `Tw` alone does not apply
reliably to multibyte CID space codes. `normal` and all accepted zero values
preserve byte-identical prior text drawing commands. The actual printable
service report exercises this CSS through Linux Native AOT.

This is not general browser CSS Text: negative word spacing,
percentages, `calc()`, Unicode word segmentation, text shaping
and arbitrary whitespace preservation remain unsupported. Invalid values
fail with `FPDF1202`, an unbreakable overwide segment with `FPDF1302`;
both preserve the existing output.

## Current-source bounded letter-spacing

`letter-spacing:0.75pt` and positive `px/mm/cm/in` print lengths (up to 24pt)
on supported report paragraphs/headings, container-inherited text and inline
spans increase the PDF character advance. Each text run emits the matching
PDF `Tc` text-state command for both built-in ASCII Courier and the explicitly
supplied CID TrueType fonts. Since `Tc` persists across PDF text objects, a
transition back to zero emits an explicit `0 Tc` while documents with no
tracking preserve the old byte path. The same spacing is counted during word measurement,
normal/nowrap overflow checks, alignment, paragraph pagination and decoration
widths. No external font or browser layout is inferred. CJK fallback glyph
selection and content bytes are unchanged. Ordered list markers that inherit
tracking reserve their actual wider glyph advances in the list gutter, so
numbered markers do not intrude into adjacent list-item text.

`normal`, `0` and `0pt` are zero spacing and preserve the prior byte path.
CSS cascade, `!important`, `inherit`, `initial` and `unset` select the
computed fixed point spacing; nonnegative units remain fixed through children.
Negative tracking, percentages, `calc()`, and computed values above 24pt return
`FPDF1202` without altering the destination. This is fixed-positive tracking
for controlled reports, **not** full CSS Text letter-spacing, complex shaping,
kerning or word-spacing. The real printable report tracks an invoice reference
and the native oracle checks unit equivalence, actual `Tc` paint and recovery.

## Current-source nonwrapping report labels

For controlled report headings, totals and invoice references, `white-space:nowrap`
on an explicit `p` or `h1`–`h6` prevents automatic breaks at spaces and at the
existing CJK opportunities. The property operates in actual glyph-based PDF
line construction, not just CSS parsing. Whitespace is still collapsed as in
the existing normal text mode. An explicit HTML `<br>` still ends a line;
that line can continue onto another page using the ordinary pagination rules.

```html
<p style="white-space:nowrap">Reference: INV-2026-1042</p>
```

A normal wrapped paragraph and a `nowrap` paragraph with identical content
produce identical PDF bytes when the content fits on one line. If any forced-break
segment is wider than its actual containing content width, conversion raises
`FPDF1302` and preserves any existing destination, rather than clipping,
shrinking or silently inserting an automatic break. The same strict behaviour
applies to explicitly sized and nested report blocks. The current scope is
**whole explicit paragraphs/headings only**: authoring `white-space` on a
`span`, container, table structural element or image is rejected with
`FPDF1201`. A paragraph inside a table cell still uses its actual cell width
and will fail when an unbreakable label does not fit. `break-spaces`,
`word-break`, non-breaking span fragments remain unsupported; `pre-line`
retains breaks but collapses spaces, `pre-wrap` preserves spaces with soft
wrapping, and `pre` preserves spaces/newlines without soft wrapping. Full CSS
Text conformance is not implemented. Unsupported values are
`FPDF1202` errors, including unmatched stylesheet declarations. Supported
CSS-wide `inherit`, `initial`, `unset`, precedence and `!important`
remain explicit; normal/default output does not change. See
`WhiteSpaceNowrapTests.cs` and the actual Native AOT verification in
`scripts/verify_stylesheets.py`.

This is a current-source feature, not an installable release or a promise to
accept arbitrary browser stylesheet content.

## Current-source background shorthand (colour only)

The current report renderer also accepts the familiar `background` shorthand **only
when it contains one supported colour**. For example,
`<div style="background:#cee8fb">Summary</div>` is byte-for-byte equivalent
to the same element with `background-color:#cee8fb`. It accepts the same
`#rgb` / `#rrggbb`, documented colour names, `transparent` and
`inherit` / `initial` / `unset` keywords. Normal selector precedence,
`!important`, non-inheritance, element applicability and existing background
painting/fragmentation apply identically.

This is **not** general CSS background shorthand support: images, URL loading,
gradients, positioning, repeats, size, multiple layers and function colours
are still rejected with FPDF1202, apart from the narrowly supported opaque rgb()/rgba() colours described below. There is no change to old PDF bytes unless
the caller actually uses the newly accepted shorthand. A malformed or
unsupported declaration still leaves existing output files intact.

## Current-source opaque RGB colours for reports

Supported text, background and solid-border colour declarations now accept
`rgb(35, 68, 107)`, `rgb(35 68 107)` or `rgba(35,68,107,1)`. The
channel values are **integers 0–255**; `rgba()` additionally accepts a finite
numeric alpha of **exactly 1** in comma syntax. These opaque values are lowered
to the matching `#rrggbb` colour before normal CSS cascade, box painting and
PDF serialization. For the same computed colour and declarations, PDF bytes
and page-fragment painting match the existing hex path. This applies to
`color`, `background`/`background-color`, `border-color` and its four
longhands, and solid `border` and per-side shorthands.

This does **not** implement CSS alpha compositing: `rgba(...,0)`,
`rgba(...,0.5)`, modern slash-alpha syntax, percentage channels, fractional
RGB channels, `hsl()`, gradients, colour mixing and nested `calc()` are
rejected with `FPDF1202`. Rejected conversions preserve existing outputs;
no CSS color-scheme, new colour spaces or implicit image loading is added.
See `OpaqueRgbCssTests.cs` and the real Linux Native AOT run of
`examples/report-print-spacing.html` with an independent hex projection.
This is a source-preview feature, not a public release or general CSS colour
conformance statement.

## Current-source computed report line-height

Business reports can specify `line-height:150%`, `line-height:1.5em`,
`line-height:18pt`, `line-height:24px` or a physical print length
such as `line-height:0.25in`. The renderer also accepts the existing
unitless `1–10` multiplier and `normal` (this engine's 1.2 default,
not an implementation of browser-dependent font normal metrics).

A percent or em line height is calculated **on the element where the winning
declaration is applied**, after that element's computed font-size. The result,
like a fixed absolute length, is inherited as a fixed point height by nested
spans and blocks. In contrast, a unitless multiplier is inherited as a number
and re-scales when a child changes font size. For example:

```css
body { font-size: 16pt; line-height: 150%; } /* 24pt inherited as fixed */
p { font-size: 12pt; }                       /* still 24pt */
span { font-size: 24pt; }                   /* still 24pt */
/* With line-height:1.5 instead, p gets 18pt and span gets 36pt. */
```

The resolved points feed the actual glyph line-box strut, inline metrics and
page continuation; they are not merely parsed. The runnable ASCII print report
and the real Linux Native AOT verifier compare three independent relative and
physical declarations with their handwritten absolute-point PDF projections,
check PDF structure/text and preserve existing destination bytes on invalid
zero height. Tests also cover computed inheritance, `inherit/initial/unset`,
precedence, fonts and legacy unitless/default byte stability.

Percentage/em multipliers must be positive and no greater than 10; absolute
computed line height is bounded to 0.1–1440pt. Negative/zero values,
`lh`, `rlh`, `calc()`, font shorthand, arbitrary CSS and browser text shaping
remain unsupported and fail explicitly (`FPDF1202`). The accepted subset
is not full CSS Line Height conformance, and the changed source has not been
published as an installable public release.

## Current-source parent-relative report typography

Controlled report headings, paragraphs and inline spans can use a positive
`font-size:125%` or `font-size:1.125em`. The scaling factor is applied to the
**computed font size of that element's actual parent** after the winning
declaration is chosen in the existing cascade. It is not multiplied by the
renderer heading's optional default size multiplier, nor by a losing
declaration. The calculated size must be between 1pt and 144pt inclusive.
Both inline and embedded stylesheet declarations work with the documented
selector/importance/inheritance semantics; `inherit`, `initial` and `unset`
retain their previous meanings.

For example, when `body { font-size:16pt }`, `h1 { font-size:125% }`
becomes 20pt, and `p { font-size:1.5em }` becomes 24pt. A nested inline
`span { font-size:125% }` inside that paragraph becomes 30pt. Actual glyph
width, wrapping, line boxes, page continuation and embedded font selection
use the resolved absolute point size. The runnable print report exercises
percent and em values against an explicitly authored point-value projection
in real Linux Native AOT PDF generation.

This does **not** implement `ex`, `ch`, `lh`, `rlh`, `calc()`, font-size
keywords or unrestricted relative layout units; separate bounded source slices below permit `rem` and element-relative `em` for ordinary report box sizing/padding and paragraph/heading margins, not borders or general browser layout.
Non-positive, malformed or out-of-range computed values are rejected with
`FPDF1202`, before replacing an existing output. Unmatched stylesheets are
still syntax-checked. This is source-preview capability, not public delivery.

## Current-source root-relative report typography

Controlled reports may now author `font-size:1.2rem` and
`line-height:1.5rem` on supported text elements, including paragraphs,
headings and inline spans. The base is the **computed font-size of the
actual `html` element**, not the enclosing `body` or the immediate parent.
If `html` is omitted, the base is the configured `PdfOptions.FontSize`
(default 12pt). An `html` element using `font-size:2rem` resolves that
self-reference against the configured initial size; `line-height:1.5rem`
on the same root uses its newly computed font size. For example:

```css
html { font-size: 15pt; }
body { font-size: 11pt; }
h1 { font-size: 1.6rem; line-height: 2rem; } /* 24pt font, 30pt leading */
p { font-size: 1rem; line-height: 1.5rem; }  /* 15pt font, 22.5pt leading */
p span { font-size: 1.2rem; }                /* 18pt font, 22.5pt inherited leading */
```

Winning declarations obey the existing cascade, `!important`,
`inherit`/`initial`/`unset` and explicit stylesheet/inline semantics.
The size is resolved to 1–144pt; line height is computed to 0.1–1440pt,
then inherited as **fixed points**. Unmatched rules are still
syntax-validated without assuming an arbitrary root size. Out-of-range,
zero or unsupported values fail with `FPDF1202` and preserve an existing
destination; a layout that cannot fit keeps the original `FPDF1302`
guards. Defaults and existing absolute/percent/em values retain their
byte-stable paths.

The [63-record paginated business report](../examples/report-root-relative-typography.html)
and independently authored [fixed-point PDF oracle](../examples/report-root-relative-typography-oracle.html)
exercise actual line wrapping, pagination, text extraction and native
PDF byte equivalence, with managed PDF and explicit TrueType regressions.
This typography scope itself does not add general CSS Values conformance. A
separate bounded source implementation now permits `rem` for ordinary report
box `width`, `min-width`, `max-width` and padding (see below), while a distinct
scoped implementation supports paragraph/heading `rem` margins. General container vertical
margins, borders, `letter-spacing`, `word-spacing`, table/image/column dimensions,
`ex/ch/lh/rlh` and `calc()` remain unsupported.
Real current-head CI, independent code review, installable packaging,
commercial rights and human page approval are separate gates.

## Current-source root-relative report box lengths

Controlled report blocks, paragraphs and headings can now use
`width:13rem`, `min-width:10rem`, `max-width:14rem`,
`padding:0.5rem` and corresponding padding longhands. Each length resolves
to **the computed `html` font size** (or `PdfOptions.FontSize` when
`html` is absent), including a root self-reference resolved from the initial
font size. It is a fixed point length after cascade, not a percentage of the
parent, and does not change with the immediate parent's font size.
An `html` element cannot itself be a styled box; the root font only
establishes the scale.

```css
html { font-size:15pt; }
.report { box-sizing:border-box; width:13rem; max-width:14rem;
          padding:0.5rem; border:1pt solid #23446b; }
.reference { min-width:10rem; max-width:11rem; margin:0 auto; }
```

This affects real PDF content widths, line wraps, auto centring, background
and border painting, and page-fragment continuation. Existing percentage
width/padding and box-sizing/min-max semantics retain the **containing block
content width** as their own basis. CSS cascade, `!important`,
`inherit/initial/unset` and non-inheritance continue to apply independently
per longhand. The supported nonnegative resolved box length range remains
0–14400pt; invalid/unmatched values raise `FPDF1202` and unfit boxes
raise `FPDF1302`, leaving an existing output unchanged. Unsupported rem on
`img`, `table`, `col`, table structure/cells or their child paragraphs
raises `FPDF1201`, even if a different cascade declaration would win.

The original root box-length slice does not itself change margins, but a
separately scoped implementation below permits `rem` in paragraph/heading
margins. General container/table margins, borders, text spacing, image/table/column
lengths, `calc()`, negative values, full CSS Values/Sizing and browser layout
remain unsupported. A later bounded `em` source slice below covers actual
ordinary report box widths/padding and paragraph/heading margins. Legacy templates that do not opt in retain
their existing PDF bytes. The real
[69-record printable report](../examples/report-rem-box-lengths.html) and
[independently authored points-only projection](../examples/report-rem-box-lengths-oracle.html)
exercise multi-page boxes, searchable body and exact PDF equivalence through
`RootRelativeBoxLengthTests.cs` and the Linux Native AOT stylesheet gate.
This is unshipped source functionality; exact-head CI, independent review,
human PDF approval and packaging are separate requirements.

## Current-source print-length and vertical-margin authoring

Controlled invoices, maintenance reports and service records can use physical
print lengths in existing length-bearing CSS properties: `mm`, `cm`, `in`,
`pt` and `px`. Conversion is fixed at 72pt/in, 25.4mm/in and 96px/in.
`font-size` (1–144pt after conversion), `margin-top`/`margin-bottom`
(0–14400pt), `width`, `padding` and solid `border-width` (including
their existing shorthands) accept these units. `width`/`padding` retain
their existing percentage semantics; the other lengths do not gain percentage
support; `font-size` separately supports parent-relative percentages as scoped
above. All existing finite/bounds/containing-block and PDF size guards apply.

The paragraph/heading `margin` shorthand now expands into **all four** independently
cascaded sides. `margin-left` and `margin-right` also work as longhands and accept
nonnegative `pt/px/mm/cm/in/%` and unitless zero. Percentage sides resolve
against the original containing content-box width, including when the
paragraph has padding or an explicit width. Nonzero horizontal margins shift
the paragraph border/background and reduce line-wrap and alignment width
on every continued page. Opt-in heading keep also measures the inset width.

For example, `margin:2mm 5% 3mm 4mm`, `margin:5mm`, `margin:0 4mm`, and
`margin-left:5%` and `margin:0 auto` with an explicit `width` affect real PDF geometry. CSS-wide `inherit`, `initial` and
`unset` remain per-side; horizontal margins are not inherited by default.
On flat table-cell paragraphs, only **explicit zero** horizontal margin values
are allowed (preserving old vertical shorthand); `auto` or nonzero horizontal values error
with `FPDF1201`, not silently ignored. General container **vertical** margins and span margins remain
unsupported; the bounded horizontal report-panel exception is below. Scoped paragraph/heading root `rem` is described below.
Negative margins, vertical `auto`/`%`, `calc()` and full browser margin
collapsing are not implemented. A bounded `em` source slice below supports only paragraphs/headings. Geometry exceeding
the containing content rectangle fails with `FPDF1302` and preserves output.
Four-sided shorthand now counts four emitted declarations toward the existing
CSS budget, rather than the earlier two when both horizontal sides were zero.
For a ready-to-run example needing no external images or fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- \
  examples/report-print-spacing.html report-print-spacing.pdf
```

This is a renderer/source capability and not a public package release,
general browser margin collapsing, automatic print/page sizing or support for
general container vertical margins, `calc()`, negative or vertical `auto` margins.
The separate root-`rem` and element-`em` margin features cover only
paragraphs/headings outside tables.
Existing reports without these explicit new values retain their old rendering
path. Results must still respect the chosen page geometry and explicit
resource contracts.

## Current-source centred report panels

Controlled business-report `div`, `section` and `article` boxes can use
**horizontal** `margin-left`, `margin-right` and `margin:0 auto` on
real PDF layout. The 1–4-value shorthand expands independently cascaded
sides. The top/bottom values on these container kinds also accept nonnegative
physical lengths and scoped root-`rem`/element-`em` as **real spacing
outside painted borders**. This deliberately does not implement negative
margins, vertical percent/auto, `calc()`, `margin-block`, CSS margin
collapse or arbitrary browser flow. Matching invalid values still reject,
even if a stronger declaration would win.

With an explicit `width` or constrained `max-width`,
`margin:0 auto` divides the free space **outside the painted border
box** equally. Horizontal lengths accept bounded nonnegative
`pt`/`px`/`mm`/`cm`/`in`/`%` and scoped root-`rem` or
element-final-font-`em`; only horizontal sides accept `auto`.
Percentages resolve against the containing content width, before
margin subtraction. The existing `box-sizing:border-box`,
`min-width`/`max-width`, padding, borders, nested wrapping,
background and multi-page fragment layout all use this resolved X/width.

```css
.report { box-sizing:border-box; max-width:440pt; margin:0 auto;
          padding:8pt; border:1pt solid #23446b; background:#f0f6fa; }
.report article { width:300pt; margin-left:auto; margin-right:auto; }
```

Horizontal and vertical `inherit`/`initial`/`unset`, inline declarations,
selectors, `!important` and per-side cascade remain supported.
This exception does **not** extend to `body`, `html`, list
containers/items, table structure, images, inline spans or flat
table-cell paragraphs, which retain strict applicability. Negative
or calculated lengths (including vertical percent/auto) raise `FPDF1202`;
unsupported elements raise `FPDF1201`; an outer box too wide for its parent
raises `FPDF1302`, preserving an existing output destination.
`margin:0` on a panel produces byte-identical legacy PDF output.

The [50-entry paginated panel report](../examples/report-panel-margins.html)
and [independently authored fixed-point oracle](../examples/report-panel-margins-oracle.html)
use identical content but `margin:0 auto` versus explicit point insets.
Managed regressions exercise byte-equivalent PDF/box X/width, root-`rem`
and final-font-`em`, percentage sides, border-box, CSS cascade,
continued fragments and failure recovery. The Linux Native AOT gate
checks qpdf, PDF-byte equality, searchable first/last entries, page
progression and original-output protection. This is development-source
capability, not an updated private preview, approved reference page,
general browser CSS support, licensing grant or customer release.

## Current-source element-relative report box and margin lengths

A controlled business report can now use nonnegative `em` lengths for
`width`, `min-width`, `max-width` and all four `padding` sides on
ordinary blocks, paragraphs and headings. Paragraphs/headings outside table
cells additionally accept `em` in `margin-top/right/bottom/left` and
the 1–4 value `margin` shorthand, including horizontal `auto`.

Unlike `rem` (the computed `html` root), each `em` is computed from
**that element's own final computed font-size**, after selector specificity,
inline declarations and `!important` resolution. It is converted to bounded
0–14400pt before PDF layout. The same resolved point lengths feed content
width, soft wraps, horizontal placement/auto-centring, painted backgrounds
and borders, vertical gaps, heading keeps and page fragments.

```css
html { font-size: 15pt; }
.report { font-size: 18pt; box-sizing: border-box;
          width: 12em; padding: .5em; } /* 216pt width; 9pt padding */
.report p { font-size: 10pt; margin: .2em .5em .3em;
            padding: .1em; }             /* 2pt/5pt/3pt margins; 1pt padding */
```

Existing `rem`, percentage, `inherit`/`initial`/`unset`, scoped
`auto`, default values and zero-value byte stability remain distinct.
No new units apply to image/table/column/inline box sizing, table-cell
paragraph margins, borders, `text-indent`, `letter-spacing`,
`word-spacing`, or general container vertical margins. `calc()`, negative,
unbounded and invalid lengths fail with `FPDF1201`/`FPDF1202`; content
too wide for its resolved geometry fails with `FPDF1302`, leaving an
existing PDF destination unchanged. This is not full CSS Values/Sizing
or browser margin collapsing.

The source-owned [32-entry service report](../examples/report-em-layout.html)
and its [independently authored point-value oracle](../examples/report-em-layout-oracle.html)
exercise actual paginated output. `ElementRelativeBoxLengthTests.cs` and
the Linux Native AOT stylesheet verifier check PDF-byte/geometry equivalence,
text and error recovery at the specific source commit; they are not a
customer-authorised binary release or human PDF approval.

## Current-source root-relative paragraph margins

Controlled report paragraphs and headings (`p`, `h1`–`h6`) outside tables now
accept `margin:0.25rem 0.5rem`, `margin-top:0.4rem` and one to four
top/right/bottom/left values, with existing horizontal `auto` and `%`
rules. Each `rem` is resolved against the actual computed `html` font
size (or the configured `PdfOptions.FontSize` if `html` is absent).
An `html` root using `font-size:2rem` refers to the configured initial
font size for that self-reference. The immediate paragraph/heading or body
font size does not change the margin's root-relative length.

```css
html { font-size: 15pt; }
.report-title { margin: 0.25rem 0.5rem; } /* 3.75pt 7.5pt */
.summary { width: 9rem; margin: 0.5rem auto 0.25rem; }
```

Top/bottom margins become bounded points before PDF paragraph layout, with
real vertical gaps and page progression. Left/right margins move painted
boxes and text, reduce wrap width, combine with `auto` centring and remain
stable on continued pages. The normal cascade, `!important`, CSS-wide
keywords, and non-inheritance apply per side. Computed margins are bounded
to nonnegative 0–14400pt. Invalid/negative values, vertical
`%`/`auto` and `calc()` reject with `FPDF1202`; oversized geometry
rejects with `FPDF1302`. General container/inline margins and any `rem`/`em`
margin inside table-cell paragraphs reject with `FPDF1201` even when
another declaration could win. Existing vertical-spacing/collapse rules
are unchanged; this is not general browser margin collapsing.

The [32-entry printable report](../examples/report-rem-margins.html) and
[independently authored point-value oracle](../examples/report-rem-margins-oracle.html)
exercise actual multi-page layout and searchable text; the
`RootRelativeMarginTests.cs` and Linux Native AOT stylesheet verification
compare rendered PDFs and protected-output failures. This source feature
is not a new installable candidate, licence grant or human PDF approval.

## Current-source underline and strike-through for reports

Report templates can use `text-decoration:underline`, `text-decoration:line-through`
and the `text-decoration-line` alias on supported text and inline spans.
The renderer paints actual solid PDF lines with each font-resolved glyph-run
width and current text colour, including wrapped lines and page fragments.
Unstyled and explicit `none` output still use the original PDF rendering.
These values participate in the normal cascade, `!important` and
`inherit`/`initial`/`unset` handling.

This is **bounded text decoration**, not complete browser CSS. Only one
decoration per computed run is available. Line offsets/thickness are estimated
from font size, not complex font metrics. No `overline`, wavy/dotted/dashed
line, compound decorations, decoration-colour/style/thickness options,
`<u>`/`<s>` HTML tags, browser subtree-decoration propagation or RTL shaping.
Unsupported values/attributes error even in unmatched rules. Paint commands
count against existing budgets; failing conversions preserve original files.
Actual feature evidence is in `TextDecorationTests.cs` and the print-spacing
example; there is no public package or automatic visual approval.


## Current-source border-box report panel widths

Ordinary blocks (`body`, `div`, `section`, `article`, `ol`, `ul`, `li`,
paragraphs and headings) accept `box-sizing:border-box`. With a specified
`width`, the declared width now includes the resolved padding and solid
borders; content width is reduced *before* text wrapping, auto-margin centring,
heading keeps, painting and continued-page fragmentation. Percent width and
percent padding still resolve against the original parent content width.
`width:auto` preserves the existing fill-available-width behaviour.

`box-sizing:content-box` is the non-inherited default and preserves existing
PDF bytes. The normal cascade, `!important`, and explicit `inherit`,
`initial` and `unset` work independently of width and padding. For example:

    <section style="box-sizing:border-box;width:140mm;padding:5mm;border:0.25mm solid #23446b">
      <p>Service summary</p>
    </section>

This implementation does not extend the separate image/table sizing paths:
`img`, `table`, structural table elements, table-cell paragraphs, inline
spans and `html` reject the property with `FPDF1201`. Invalid `padding-box`,
`calc()` and multi-token values are `FPDF1202`, including in unmatched rules.
A border-box that cannot contain its own padding/borders or fit its containing
width fails with `FPDF1302` and leaves existing output intact. No general CSS
height, flex/grid, column sizing or full box-model conformance.
`BorderBoxSizingTests.cs` and the real Native AOT stylesheet gate compare actual
PDF geometry with an independently authored content-box projection; the
resource-free `examples/report-print-spacing.html` exercises the feature.

## Current-source bounded report min/max widths

Ordinary block boxes, paragraphs and headings can now use `min-width` and
`max-width` for real PDF layout. `min-width:auto` (the unchanged default)
and `max-width:none` (the unchanged default) impose no extra constraint;
bounded nonnegative `pt`, `px`, `mm`, `cm`, `in`, `%`, scoped root-relative
`rem`, scoped own-font `em` and unitless zero are accepted. Percentages resolve against the **containing block content
width**, including inside nested sections, not a post-margin box. Bounds are
resolved after `width` (including `auto`) and before auto margins are
distributed; max-width can therefore centre a content panel without requiring
a fixed width. `min-width` takes precedence if it exceeds `max-width`.

For `box-sizing:content-box`, min/max constrain the *content* width; for
`box-sizing:border-box`, they constrain the outer width including padding
and solid borders. The adjusted content width drives real text wrapping,
backgrounds, border geometry, heading keeps and page fragments, and does not
change unstyled or unconstrained output bytes. For example:

```html
<section style="max-width:146mm;padding:4mm">
  <p style="box-sizing:border-box;min-width:82mm;max-width:90mm;width:70mm">
    Reference: SR-2026-0142
  </p>
  <p style="max-width:95mm;margin:0 auto">Approved service summary</p>
</section>
```

The scope does **not** add intrinsic `min-content`/`max-content`/`fit-content`,
height constraints, negative lengths, `calc()`, text-overflow,
CSS flex/grid, image sizing, table and cell width constraints, or inline span
boxes. Unsupported values reject with `FPDF1202`, unsupported elements with
`FPDF1201`, and boxes that cannot fit their content area with `FPDF1302`;
conversion preserves any prior destination. The existing independent
stylesheet cascade, `!important`, `inherit`/`initial`/`unset`, and box
fragments apply. Scope is **source-only**, not a published engine release.

The resource-free [actual report](../examples/report-width-constraints.html),
[hand-authored fixed-width oracle](../examples/report-width-constraints-oracle.html),
`MinMaxWidthTests.cs` and Native AOT stylesheet check compare PDF bytes,
searchable content, layout positions, real multi-page painting and invalid
output recovery. Engine implementation starts at
`bc704aed90b1e5dbdc1deec65a7fd314b626467e`; introduction in a
distributed version remains unknown.

## Current-source ASCII report text-transform

Business invoice labels, report headings and status fields can opt into
`text-transform:uppercase`, `text-transform:lowercase` or
`text-transform:capitalize`; `none` is the unchanged inherited default. These declarations work on styled headings,
paragraphs, inline spans and text-bearing containers with normal selector
cascade, `!important`, `inherit`, `initial` and `unset` semantics.
A nested `span style="text-transform:none"` can preserve a customer's
proper name inside an uppercase label.

The conversion is deliberately **ASCII Latin letters A–Z/a–z only**.
`capitalize` uppercases only the first lowercase ASCII letter of a word;
the rest of that token retains its original case. A word here is a maximal
run of ASCII letters/digits. Hyphens, punctuation, whitespace and non-ASCII
scalars separate words, while leading digits belong to the word. Inline
spans do not create word boundaries, including spans with `none`; `<br>`
starts a new word. Existing alphabetic/Roman list markers also measure and
paint their first ASCII letter consistently under inherited `capitalize`. This is a deterministic report-token subset, **not**
CSS Text's Unicode word segmentation. Caseless Chinese, numbers and
punctuation remain unchanged.
The operation occurs before actual font/glyph fallback and width measurement:
searchable PDF glyph text, line wrapping, page positions, decoration widths
and pagination are based on the *transformed* characters. It does not rely
on postprocessing a rendered PDF or synthetic font faces. `none` and
unstyled content retain byte-identical PDF output.

Locale-specific casing, Unicode multi-character expansions, non-ASCII
cased letters, `full-width`, `full-size-kana`, language-specific
casing and general Unicode text transforms are **not** supported.
Non-ASCII cased letters and symbols with Unicode case mappings (including Roman numerals and circled Latin letters) under an active transformation explicitly raise
`FPDF1305` rather than silently producing incorrectly cased output;
unsupported CSS values raise `FPDF1202`, and applying this text property
to a standalone image raises `FPDF1201`. Normal missing-result-glyph
diagnostics (`FPDF1504`) still apply. All failures preserve an existing
destination. No CSS Text standard-compliance claim or publicly distributed
new engine binary is implied.

Run the fontless [case labels report](../examples/report-case-labels.html)
and [54-entry capitalized service register](../examples/report-capitalized-register.html)
from source; the [independent literal-case register oracle](../examples/report-capitalized-register-oracle.html)
and separately authored
[visible-text oracle](../examples/report-case-labels-oracle.html), NUnit
`TextTransformTests.cs` and the Linux Native AOT stylesheet verifier compare
actual PDF bytes, searchable text, mixed spans and negative recovery. The
original uppercase/lowercase support began at `8c946a29423ee3de6c171548fe6f633393ca76b6`;
ASCII capitalize extends it in source commit `06d0be5aab6fcdf15e20f8f1e6b7432e27a4ed35`.
Neither establishes a published version or new installable candidate.

## Budgets and diagnostics

`PdfOptions` includes per-conversion limits:

| Option | Default |
| --- | ---: |
| MaxCssCharacters | 262144 across embedded and decoded inline CSS, including comments |
| MaxCssSelectors | 4096 selector arms, including comma groups and empty blocks |
| MaxCssDeclarations | 32768 across embedded and inline declarations |
| MaxCssMatchOperations | 5000000 charged candidates, matching steps and offered declarations |

Each selector is limited to 32 compounds with at most 32 simple terms per compound. Rightmost ID/class/type indices reduce unrelated candidates; mixed combinators use bounded dynamic programming over ancestry, not exponential backtracking. Parsing uses two HTML scans rather than a retained full DOM: lower retained tree scope is traded against a second scan. This is an architectural choice, not a benchmark result or a constant-memory guarantee.

Cancellation is cooperative and checked during source scanning, compilation and matching. CSS failures happen before PDF serialization, leaving the caller's output untouched. Existing final-copy I/O limitations still apply. Input limits and font trust requirements remain unchanged.

| Code | Meaning |
| --- | --- |
| FPDF1201 | Unsupported CSS property or unsupported element applicability |
| FPDF1202 | Invalid/unsupported value or declaration |
| FPDF1203 | Unsupported/malformed selector, token or stylesheet structure |
| FPDF1204 | Unsupported at-rule, style type or media |
| FPDF1205 | CSS resource/complexity budget exceeded |

Embedded stylesheet token offsets refer to the HTML source. Inline diagnostics are anchored to the element with decoded-value offsets; they are not an exact reverse mapping through HTML entities. Diagnostics remain development contracts, not complete browser parse-error classification.

## Verification

The NUnit suite checks selector matching, specificity, inline/important order, globals, inheritance, comments, late styles, HTML ancestry, RAWTEXT boundaries, budgets, cancellation and concurrent isolation. The two old tests rejecting all stylesheets and important declarations are replaced by positive PDF assertions, not silently removed without coverage.

The Native AOT fixture compares `examples/stylesheet-document.html` against an independently hand-authored `examples/stylesheet-inline-oracle.html`. Both use the same native FactsPDF executable and complete explicit fonts with native subsetting. Verification requires identical PDF bytes, identical extracted text matching visible HTML, page-contained text, and identical 120 DPI page pixels. qpdf and Poppler inspect outputs; no browser or third-party CSS renderer generates an expected PDF. This validates the authored cases, not every aspect of CSS conformance.

The artifact contains only the public two-page PDF, two PNG previews and a verification JSON record. No standalone font, executable or package is uploaded. Exact-head results belong in [the CSS ledger](css-development-ledger.md) and PR checks. Independent code review, broader standards-based test corpora, fuzzing and performance profiling remain necessary before production.

## Current-source bounded emergency wrapping for report identifiers

`overflow-wrap:break-word` is opt-in for explicit `p` and `h1`–`h6`
paragraphs/headings. It adds breaks at glyph boundaries **only** when a whole
otherwise-unbreakable token cannot fit the measured content width (or the
indented first line). A word that fits a fresh line instead moves whole after
its preceding collapsed space. Spaces still collapse, explicit `<br>` works
and `white-space:nowrap` continues to prohibit automatic breaking.
`overflow-wrap:normal` or omitted retains identical PDF drawing bytes.

Only `normal`, `break-word` and single CSS-wide
`inherit`/`initial`/`unset` values are accepted. Explicit declarations on
inline spans, general containers and table elements, the `word-wrap` alias,
`anywhere`, `break-all`, hyphen insertion, grapheme/complex-script shaping
and bidirectional text remain outside this scoped increment. A flat `p` inside
a fixed-column table cell may opt in for its own text, but this does not add
auto column widths or general table layout.
Unsupported declarations fail `FPDF1201/1202`; a glyph wider than its
usable line or an impossible first-line indent fails `FPDF1302` without
replacing any existing output. Breaks are scalar-glyph boundaries in the
already-supported simple horizontal text model, not full CSS Text support.

The [32-entry real report](../examples/report-overflow-wrap.html) has an
[independently authored manual-break oracle](../examples/report-overflow-wrap-oracle.html).
`OverflowWrapTests.cs` and the Native AOT stylesheet verifier test PDF bytes,
actual glyph positions, pagination, searchable content and error recovery.
Independent final-head review, audit-data update, candidate binaries and
human PDF approval remain separate requirements.


## Current-source real vertical report-panel margins

Scoped `div`, `section` and `article` report panels now accept nonnegative
`margin-top`/`margin-bottom` lengths in `pt`, `px`, `mm`, `cm`, `in`,
root-`rem` or final-element-font-`em`, as well as zero, shorthand and explicit
`inherit`/`initial`/`unset` with normal per-side cascade/`!important`.
A top margin precedes the first painted border and its text; a bottom margin
separates the following flow after the last painted border. Nested margins
occupy the painted ancestor's content area, without colouring the margin;
continuation pages do not repeat them. Adjacent panel margins **add** (no CSS
margin collapse). Unspecified/zero values retain existing PDF bytes.

Negative values, vertical percentages/`auto`, `calc()`, and unsupported
contexts (`body`, lists, tables, images, inline boxes) fail explicitly with
`FPDF1202`/`FPDF1201` as appropriate. Geometry overflow retains
`FPDF1302` and protects existing destinations. No full CSS box model,
`margin-block`, CSS Paged Media, or cross-element margin-collapsing claim.

The [50-entry real PDF report](../examples/report-panel-vertical-margins.html)
and [independently authored fixed-point spacer oracle](../examples/report-panel-vertical-margins-oracle.html)
check nested painted panels, inherited horizontal centring, ordered searchable
rows and cross-page fragments. `ReportPanelVerticalMarginTests.cs` verifies
PDF byte parity, exact positions, rem/em, cascade, margins on long rows and
protected output; `scripts/verify_stylesheets.py` runs an actual Linux Native
AOT CLI report and invalid-value recovery. Source change is not an already
released candidate or human-approved PDF evidence.


## Current-source bounded report display suppression

This source-only print-report subset accepts `display:none` and
`display:block` (single identifier each) on ordinary `p`/`h1`–`h6`,
`div`/`section`/`article` and `blockquote`, outside lists/tables.
A winning `none` suppresses the entire parsed block subtree. `block`
retains that element's existing block formatting. No other formatting
context is implemented.

**CSS semantics:** Embedded `<style>` rules and inline declarations share
the existing specificity, source-order and `!important` cascade. `display`
does not inherit; a descendant `display:block` cannot resurrect an
ancestor whose whole subtree has been suppressed. CSS-wide `inherit`,
`initial`, `unset` are deliberately rejected (`FPDF1202`): their full
standard computed behavior would include unsupported inline formatting.
Unknown `display` values (`inline`, `flex`, `grid`, `contents`, etc.),
multi-token values and functions also reject `FPDF1202`, including unmatched
selectors. All applicable declarations are validated even if they lose the
cascade. `html`/`body`, spans/emphasis, `img`, `hr`, all list/table
structures and ordinary paragraphs/blocks *inside* lists or tables reject
`FPDF1201` when they declare `display`.

**Actual rendering:** The reader parses and validates a hidden block and all
descendants first, then removes it from the parent document tree when closing
the element. It never reaches heading-keep lookahead, PDF font/glyph
measurement, width/height layout, outside margins, paint-command accounting,
decorations or page fragments. Visible siblings have the same positions, page
count and PDF bytes as a separately authored document that omits the hidden
block. Nested hidden sections, suppressed quotes and headings leave no blank
space. However, invalid markup/declarations, unavailable image resources,
CSS/input/depth budgets and other errors inside hidden markup are **not**
ignored: they fail using the pre-existing error codes and preserve any prior
destination bytes and position. The feature hides rendered output, not the
original source data; it is not an information-security boundary for HTML
input documents.

**Excluded:** HTML `hidden`, CSS `visibility`/`opacity`, list/table row
suppression, general `display` layout, flex/grid, CSS media queries, scripts,
reflow or animations. See the
[40-entry visible report](../examples/report-display-visibility.html),
[separate visible-only oracle](../examples/report-display-visibility-oracle.html),
`ReportDisplayTests` and the Native AOT stylesheet gate for measured
multi-page PDF parity and output-preserving failure cases. This source change
does not create a retained preview binary, customer distribution right, or
human-approved PDF reference.
