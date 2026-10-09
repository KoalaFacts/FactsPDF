# Embedded CSS stylesheets and selectors

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

Inherited values are the parent's computed values; the parent's importance and selector specificity do not compete with declarations on a child. `inherit`, `initial` and `unset` are supported for the eight properties below. `unset` resolves to inheritance or the initial value, not a lower-priority heading/paragraph default.

Renderer initial values are `PdfOptions.FontSize` (normally 12pt), line-height 1.2, black, left alignment, zero margins and automatic breaks. These are explicitly FactsPDF defaults, not a claim of browser initial font/theme behavior. Heading font-size multipliers and the default 8pt paragraph-after gap remain when no author declaration replaces them.

## Property scope

| Property | Values / applicability |
| --- | --- |
| font-size | pt/px lengths, computed range 1-144pt |
| line-height | Unitless number 1-10 |
| color | #rgb/#rrggbb; black, white, red, green, blue, gray, grey |
| text-align | left/center/right; inherited through text containers, controls paragraph layout |
| margin-top, margin-bottom | Paragraphs/headings only; nonnegative pt/px or zero, at most 14400pt |
| break-before, break-after | Paragraphs/headings only; auto/page |

All eight also accept `inherit`, `initial`, `unset` and `!important`. Matching paragraph-only properties on containers or spans is an error, including declarations that would lose to a stronger rule. Styling `br` is unsupported; a matching declaration errors. Head/title metadata remains non-rendering and does not acquire presentation from stylesheet rules. Inline styling of metadata is still rejected.

Selectors do not make new layout features available. Width/height, padding, borders, background fills, flex/grid, tables, images and font-family/@font-face are not added by this increment. Unsupported properties or values fail explicitly even in an unmatched selector. That strict behavior deliberately differs from browser CSS error recovery; use the documented subset, not arbitrary website stylesheets.

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
