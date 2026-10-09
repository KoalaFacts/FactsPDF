# CSS stylesheet increment implementation plan

> For agentic workers: execute task by task using test-driven development and observed CI results.

**Goal:** Embedded stylesheets and a bounded selector/cascade implementation on the existing renderer.
**Architecture:** Compile embedded/inline CSS, select candidates by rightmost simple selector, match against actual element ancestry, resolve one winning value per property, then reuse text layout and PDF output.
**Tech stack:** Existing .NET 10 and NUnit. No new runtime dependency.
**Spec:** ../specs/2026-10-10-css-stylesheets.md

## Global constraints
Preserve current font/Unicode/subsetting behavior and licenses. No merge or package publication. CSS budgets: 262144 characters, 4096 selector arms, 32768 declarations, 5000000 work operations; 32 compounds and 32 simple terms each.

## Review focus
Comments must not create combinators; grouped specificity only uses matched arms; mixed ancestor/child matching must not be greedy; later style elements must apply earlier; inherited priority must not compete on descendants.

## Tasks
1. Add budget properties and behavioral regression tests through HtmlDocumentReader/PdfConverter. Commit and observe compiled red tests before implementation.
2. Add CSS lexical tokens, selector compilation/matching, declarations and per-property cascade. Extend HTML tokenizer RAWTEXT and reader ancestry integration. Run the entire suite and correct failures at their source.
3. Replace exactly the two obsolete rejection test cases with positive assertions. Add any review regression tests before fixes. Keep all other old tests unchanged.
4. Add bilingual stylesheet and independently authored inline oracle, native conversion and external PDF checks. Run all previous verification jobs as well. Inspect previews, document support/strict limitations, update the draft PR and verify the exact final head.

Compiler and AOT proof must come from GitHub Actions when the local SDK is absent; do not label unexecuted local edits as compiled.
