# First renderer review notes

This is an implementation self-review, not an independent reviewer approval.
The work remains a draft PR for human review; no main-branch merge or package
publication is authorized by the notes below.

## Reviewed boundaries

- Main's licensing and contribution documents are outside this change.
- The engine has no third-party runtime PackageReference. NUnit/test adapter and
  PDF inspection tools are test-only, not runtime engine dependencies.
- No browser, runtime code generation, implicit network fetch or script execution.
- Reader, style interpretation, layout and PDF output have separate internal units.
- CLI does not open the destination for truncation before rendering; it uses a
  sibling temporary file and explicit overwrite consent.
- Rendering failures are checked before writing to the caller's output stream.
- Text outside the supported font/character baseline is rejected.
- PDF cross-reference offsets, content-stream lengths and deterministic numeric
  formatting are asserted in tests and the example is inspected independently.

## Defects discovered during review and verified with failing tests

- CLI and core initially had a case-insensitive assembly/project-name collision.
- Unicode whitespace outside an existing paragraph could disappear silently.
- Unhandled head text and invalid head placement could hide content.
- A break-before property on an empty paragraph was lost.

Those defects have targeted fixes and regression tests. Future reviews should
not weaken the tests to make an unsupported document appear successful.

## Explicitly deferred

The parser is a small supported-input reader, not a conforming HTML5 tokenizer/tree
builder. CSS is inline-only. Courier ASCII is not the Unicode milestone. AOT beyond
Linux x64, WASM, language bindings, resource profiling, general layout correctness,
font embedding, PDF accessibility and a broader adversarial corpus remain open.
Do not market or ship this development slice as the complete product.
