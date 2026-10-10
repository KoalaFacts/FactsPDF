# Font subsetting implementation plan

> For agentic workers: use executing-plans and test-driven-development; keep implementation on its feature branch.

**Goal:** Opt-in native TrueType subsetting with output-equivalence proof and an honest resource baseline.
**Architecture:** Immutable loaded font data -> bounded composite closure -> dense remap/sfnt assembly -> existing PDF serializer. Explicit full-font fallback when subsetting is disallowed.
**Tech stack:** Existing .NET 10 and NUnit; independent CI-only Poppler, qpdf and fontTools inspection.
**Spec:** ../specs/2026-10-10-font-subsetting.md

## Global constraints
No new runtime dependencies. Keep existing full embedding as the default. No license changes, merge or package release. Glyph traversal depth <=64. No standalone font artifacts.

## Review focus
Composite cycles and missing components; glyph-zero and empty outlines; shared-glyph/supplementary text mappings; no-subsetting permissions; resource numbers incorrectly labeled as engine-only or guaranteed speed.

## Tasks
1. Add compiling subset API stub and focused behavioral tests, run CI and record actual failures before implementation.
2. Implement `TrueTypeSubsetter.Create(PdfFont, IReadOnlyList<int>, CancellationToken)` returning bytes, remapping and PostScript name. Add focused sfnt assembly and composite traversal helpers. Verify all tests.
3. Connect `SubsetFonts` and `--subset-fonts` to the serializer, with explicit full embedding fallback. Verify actual PDF mappings and prior behavior.
4. Compare full/subset native output with identical real-font inputs. Record new-process and reused-font measurement contexts and package/license inspection. Review, document limits, update draft PR and verify its exact head.

Each task has an observed failing test or check before its implementation and a subsequent verified passing result. CI provides compiler/AOT evidence because the editing container has no .NET SDK or outbound DNS.
