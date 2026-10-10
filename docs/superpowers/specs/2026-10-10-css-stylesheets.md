# M3: embedded CSS stylesheets, selectors and cascade

Continue the user's selected next increment on the existing FactsPDF renderer: templates may use standard HTML style elements and class/id selectors rather than repeating inline declarations. Preserve one shared core, explicit fonts, Unicode, optional subsetting, AOT compatibility and existing licensing. Work on a stacked draft branch from PR #3; no merge or package release.

## Bounded support

Support embedded `<style>` in head or at the existing root/container level, with no attributes or type empty/text/css and media empty/all/print. Other media values, external link stylesheets, @import and other at-rules are explicit unsupported errors. Style content is HTML RAWTEXT: no HTML entity decoding or tag recognition except an appropriate ASCII-case-insensitive style end tag. CSS comments do not become descendant-combinator whitespace or concatenate identifiers.

Compile rules once per conversion. Support ASCII CSS identifiers, HTML type names (case-insensitive), universal *, case-sensitive class and ID, compound selectors, comma lists, descendant and direct-child combinators. No attribute/sibling/pseudo selectors, escapes, nesting, CSS variables, functions or external resource fetching in this increment. Grouped selectors contribute only their matching specificity, never that of an unmatched arm.

Cascade each property independently: author important beats author normal; inline wins within the same importance; then compare ID/class/type specificity lexicographically and source order. Later style elements apply to earlier content. Preserve existing renderer heading/default behavior beneath author declarations. Inheritance uses the parent's computed values, without carrying its specificity or importance into the child's cascade. Support inherit/initial/unset for the existing eight properties. Renderer initial font-size is PdfOptions.FontSize, line-height 1.2, black, left, zero margins and auto breaks; these are explicit renderer defaults, not browser font/theme guarantees. Unset removes the winning declaration to inheritance/initial, not to heading defaults.

Reuse existing supported values: font-size pt/px, unitless line-height, color #rgb/#rrggbb/documented names, text-align left/center/right, paragraph margin-top/bottom and paragraph break-before/after. Paragraph-only layout properties on a matched unsupported container/inline element are errors, not silently ignored. Text-align may be inherited through spans but only controls paragraph layout. Metadata rules do not make metadata render. Styling br remains unsupported. This is a strict documented subset: malformed and unsupported rules/declarations error, even in unmatched selectors. It does not claim browser CSS error recovery.

## Architecture and limits

Two bounded HTML scans: collect/compile style blocks and inline declarations first, then reuse the existing streaming paragraph reader with an element ancestry path. Descendant/child matching works from right to left with bounded dynamic programming, not exponential recursive backtracking. Index rules by the rightmost ID/class/type where possible. No new public markup or DOM API and no runtime dependency.

Default budgets across embedded and inline CSS: MaxCssCharacters=262144, MaxCssSelectors=4096 (comma arms counted), MaxCssDeclarations=32768, MaxCssMatchOperations=5000000. Every candidate, compound comparison and applied declaration consumes work budget. Each selector allows at most 32 compounds and 32 simple terms per compound. Cancellation is checked during scanning, compilation and matching. CSS errors before serialization leave output untouched. Boundaries are not a claim of universally low peak memory or full HTML safety.

## Acceptance

Observe new tests fail before implementation. Then retain previous supported behavior and replace only the two obsolete negative tests for style elements/important with positive checks. Exercise specificity, multiple classes, case, groups, comments, late styles, child versus descendant matching, mixed-combinator non-greedy matching, metadata, RAWTEXT closing boundaries, per-property cascade, globals, inheritance, source offsets, budgets, cancellation and concurrent document isolation.

Run Windows/Linux/macOS suites plus real Linux x64 Native AOT. An original bilingual stylesheet sample must produce identical PDF bytes (or if serialization differences are justified, identical extracted text and page pixels) to an independently hand-authored inline-equivalent sample with the same complete fonts and subsetting option. Inspect rendered previews. Preserve all six licensing/contribution-policy files and README licensing text. No new claims about Flex/Grid, images/tables, WASM or other bindings.

## Primary references
- https://www.w3.org/TR/css-cascade-5/
- https://www.w3.org/TR/selectors-4/
- https://www.w3.org/TR/css-syntax-3/
- https://html.spec.whatwg.org/multipage/parsing.html#rawtext-state

References inform independent implementation; no parser code is copied.
