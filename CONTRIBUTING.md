# Contributions

FactsPDF is licensed under the [Apache License 2.0](LICENSE.md). Unless you
state otherwise, any contribution you intentionally submit for inclusion is
licensed under the same terms, as set out in section 5 of the license
(inbound = outbound). You keep your copyright; no separate agreement or
assignment is required.

Bug reports, feature discussions and descriptions of independently observed
behavior are welcome. Do not include confidential documents, personal data,
private source code or materials you are not entitled to share. Use
non-secret placeholders and synthetic fixtures.

Only submit work you have the right to contribute under Apache-2.0, including
any employer authorization where it applies.

## Third-party and generated material

Record the provenance, version, license, and required notices of any copied or
adapted material, including small snippets, test suites, fonts, and images.
Review compatibility before inclusion; public visibility is not permission to
copy. Standards-based independent implementation does not justify copying a
reference implementation without complying with its terms.

AI-assisted contributions require the same rights and provenance review. Do
not assert sole ownership of copied third-party material or discard notices
because a tool reproduced it.

## Documenting a new feature

Automated contributors must also follow [AGENTS.md](AGENTS.md).

Keep `README.md` a short front door; it is also the NuGet package page. Do not
add a per-feature section to it. For a new capability:

1. Update its record in `site/data/compatibility.json` (Can I Render).
2. Add a note under the matching area in `docs/feature-notes.md`, with the
   example report, oracle, test evidence and source commit.
3. Update the exact contract in the relevant `docs/` page, such as
   `docs/css-stylesheets.md`.

Change the README only when its summary tables, quick start or status become
inaccurate.
