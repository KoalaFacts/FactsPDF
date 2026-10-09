# Unicode fonts implementation plan

> For agentic workers: execute the font increment task by task, with failing tests before implementation.

**Goal:** Explicit font loading and correctly searchable horizontal Chinese/Unicode PDF output.
**Architecture:** Immutable bounded sfnt reader; scalar-aware layout using real metrics and a deterministic fallback chain; embedded composite-font serializer; CLI-only filesystem access.
**Tech stack:** Existing .NET 10 core and NUnit tests; no new runtime dependency.
**Spec:** ../specs/2026-10-10-unicode-fonts.md

## Global constraints
Preserve existing licenses and ASCII compatibility. Maximum eight supplied fonts; default 32 MiB per load and 64 MiB aggregate. No automatic filesystem/network access in the core. No package publishing or merge.

## Review focus
Corrupted offsets and overflow; multiple scalars sharing one glyph; supplementary-plane characters; punctuation at wrap boundaries; output path aliasing an explicitly supplied font. Cover each with behavioral tests.

## Tasks
- [ ] Commit a compiling font API stub and independently authored synthetic-font test builder; run the expanded suite and confirm expected behavioral failures.
- [ ] Implement PdfFont and its bounded OpenType reader. Prove cmap 4/12, real widths, immutable ownership, permissions and malformed-input diagnostics.
- [ ] Extend scalar layout and embedded PDF output together. Prove no Unicode replacement, deterministic fallback, CJK wrapping, exact ToUnicode mappings and binary stream/xref integrity.
- [ ] Implement CLI --font loading and file-safety checks; prove existing CLI behavior remains green.
- [ ] Run the complete platform matrix and Native AOT; independently inspect a real Chinese fixture. Record exact commits/results and remaining limitations, then update the draft PR.

Each task runs tests, diagnoses any failures, and records a checkable result rather than assuming success from configuration.
