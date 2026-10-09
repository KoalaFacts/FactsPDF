# First renderer implementation plan

> For agentic workers: use superpowers:executing-plans to implement task-by-task.

**Goal:** establish a tested, deliberately limited HTML-to-PDF vertical slice.
**Architecture:** HTML reader -> styled paragraphs -> positioned lines -> bounded PDF serializer.
**Tech Stack:** .NET 10; NUnit for tests; no third-party runtime packages.
**Spec:** ../specs/2026-10-10-first-renderer-design.md

## Global constraints

Retain the existing license files unchanged. No new language, browser, network loading or dynamic code.
No unverified language/platform/performance claims. Do not publish packages or merge this draft automatically.

## Review focus

- Malformed quotes and misnested elements must fail with no PDF output.
- Unsupported scripts, styles and Unicode must not be silently dropped.
- Limits must constrain work before output; cancellation must be observed.
- Xref byte offsets/stream lengths must remain valid across locale/platform changes.
- CLI failures must preserve an existing destination and never mix status text into binary stdout.

## Tasks

- [ ] Add API and NUnit behavioral tests; run the intentionally non-rendering baseline and record failures.
- [ ] Implement HTML reading and inline styling with strict supported-feature diagnostics.
- [ ] Implement bounded word/line layout, page fragmentation and Courier PDF serialization.
- [ ] Run all tests; correct implementation failures and add regression tests for discovered defects.
- [ ] Add CLI and cross-platform/Native AOT checks, including an independently checked two-page sample.
- [ ] Verify the package contains the exact license file without publishing it.
- [ ] Document actual support, commands and evidence; open/update a reviewable PR.

Keep proof of test commands and CI results in the PR. A configured workflow is not a passing workflow.
