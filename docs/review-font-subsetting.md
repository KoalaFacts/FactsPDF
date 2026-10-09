# Font-subsetting self-review

Scope: the opt-in subsetting increment on top of d191bcb7a6f8d79d88e1dac333c25136b0350d0f. This is implementer self-review, not independent human or code-review-agent approval. Independent PDF/font tools are used for output verification, not represented as code reviewers.

## Review finding and regression evidence

A first composite component was accepted with point-to-point attachment even though the parent has no previous component points. Microsoft OpenType's glyf specification requires ARGS_ARE_XY_VALUES on the first component. The fix rejects that unsupported/malformed record before including its child.

Regression commit 2ece243f5a1a7b6618ce52f38b87293c5355865b was compiled and tested by CI run 38000368912. Inspected Ubuntu job 114056707687 ran 137 cases: 136 passed, exactly one failed, `FirstCompositeComponentCannotAttachToNonexistentParentPoints`, because no exception was thrown. All prior subsetting tests remained green. This is the observed failing test before the one-condition fix. Fresh CI for the fix must independently pass before completion is claimed.

The same review increment also covers composite depth before stack exhaustion, cmap format-4 length boundaries at 8,188/8,189/9,000 aliases, concurrent subsets without cross-document glyph leakage, and output-limit errors preserving an existing caller stream.

## Checked boundaries

- Full embedding stays the default. `SubsetFonts` and `--subset-fonts` are opt-in. No-subsetting permissions result in full embedding, never edited permission bits.
- Used scalars and composite closure are mapped to dense glyph IDs. PDF character codes remain per scalar; shared glyphs and supplementary-plane mappings are preserved.
- Source fonts remain immutable. A subset does not reuse mutable per-document maps from another conversion.
- Copied glyph programs and hinting tables are not claimed to be sanitized. Known resource/graph boundaries do not make arbitrary uploaded fonts safe.
- These output subsets are PDF-only, not a claim of fully installable or shaping-capable fonts. Unhandled glyph-indexed tables and invalidated signatures are dropped.
- Generated sizes/checksums, metrics, outlines, legal metadata and output equality were independently checked on identical complete real-font inputs in run 37999991799. No third-party tool created those subsets.
- Measurements distinguish current-thread managed allocated bytes, whole-process peak RSS, new-process CLI elapsed time and reused-font render time. Small shared-runner observations are not production guarantees or competitor comparisons.
- No changes are intended to the six existing licensing/contribution-policy files. No merge, package release, commercial activation, or standalone-font distribution is part of this increment.

## Remaining work before production

Independent code review, larger font/document corpora, malformed-font fuzzing, more cancellation/resource stress cases, further platform-specific AOT builds and sustained memory profiling remain open. Full HTML/CSS, shaping/RTL and additional language bindings are not delivered by subsetting.

## Reference

https://learn.microsoft.com/en-us/typography/opentype/spec/glyf
