# Instructions for coding agents

These rules apply to every automated contributor (Codex, Claude and others)
working in this repository.

## Licensing — do not change or restate

- FactsPDF is licensed by **Koala Facts Trading Pty Ltd** under the standard
  PolyForm licenses listed in `LICENSE.md`, with commercial licenses for other
  use. `LICENSE.md` is the only source for which licenses apply; do not restate
  the list elsewhere. The former "FactsPDF Community License" is retired; never
  refer to it as current, and never describe engine changes as having to be
  published.
- Do not edit `LICENSE.md`, `LICENSING.md`, `COMMERCIAL.md` or
  `docs/license-history.md` unless the owner asks for it in the task.
- Do not add licence, distribution, approval or legal disclaimers to feature
  notes, PR descriptions or docs. One line linking to `LICENSING.md` is enough
  where licensing is relevant.

## Documentation — where a new feature goes

- `README.md` is the front door and the NuGet package page. **Never add a
  per-feature section to it.** Change it only when its summary tables, quick
  start, CLI option table or status become inaccurate. Keep the Can I Render
  link near the top.
- For a new capability:
  1. Update its record in `site/data/compatibility.json` (Can I Render).
  2. Add a note under the matching area in `docs/feature-notes.md`.
  3. Update the exact contract in the relevant `docs/` page, such as
     `docs/css-stylesheets.md`.

## Code conventions

- Builds treat warnings as errors with the .NET recommended analyzers and
  `.editorconfig` code-style rules. Fix warnings at the cause; suppress a rule
  only in source with `#pragma warning disable <id> // reason`.
- Extension methods for a type `Xxx` go in a static class named
  `ExtensionsToXxx`, declared in the namespace of `Xxx` (for example
  `ExtensionsToString` in `System`) and disabling IDE0130 in that file.

## Other rules

- Never put a person's name in any file, commit, PR or copy; refer to the
  company or "the owner".
- Commit messages, code comments and test names are written in English.
- Before claiming a change works, follow `.claude/skills/verification-first/SKILL.md`
  and `.claude/skills/verify-factspdf/SKILL.md`.
