# Contributions and inbound rights

FactsPDF is licensed by Koala Facts Trading Pty Ltd under PolyForm Small
Business 1.0.0 or PolyForm Noncommercial 1.0.0, with separate commercial
licenses. Clear inbound rights are required before another person's
copyrightable work can be incorporated into that distribution model.

## Current acceptance policy

Bug reports, feature discussions, and descriptions of independently observed
behavior are welcome. Do not include confidential documents, personal data,
private source code, or materials You are not entitled to share.

**External copyrightable code, tests, substantial documentation, and assets must
not be merged until their ownership and the necessary licensing authority are
recorded.** The project has not yet established a counsel-reviewed contributor
agreement with a verified legal receiving party. Maintainers should discuss a
proposed contribution before asking its author to invest substantial work.

Before acceptance, record either a suitable signed contributor agreement or
another documented rights basis approved for this project. Where a separate
agreement is needed, it must identify the actual parties and expressly cover
the rights needed for modification, distribution, and commercial sublicensing,
including relevant patents and employer authorization where applicable. A
contributor need not surrender ownership merely to provide a suitable license.

A GitHub pull request, repository access, or Developer Certificate of Origin
sign-off alone must not be treated as copyright assignment or an unrestricted
right to issue commercial sublicenses. This file does not itself impose a new
contributor agreement or retrospectively change a submitted work's license.

## Forks and private changes

Neither PolyForm license requires anyone to publish changes to FactsPDF. A
public fork does not grant the project any rights in its changes; maintainers
must still complete the inbound-rights process above before incorporating
another person's work into licensed releases. Do not publish secrets, private
rights records or customer documents with a contribution; use non-secret
placeholders and synthetic fixtures.

## Third-party and generated material

Record the provenance, version, license, and required notices of any copied or
adapted material, including small snippets, test suites, fonts, and images.
Review compatibility before inclusion; public visibility is not permission to
copy. Standards-based independent implementation does not justify copying a
reference implementation without complying with its terms.

AI-assisted contributions require the same rights and provenance review. Do
not assert sole ownership of copied third-party material or discard notices
because a tool reproduced it. Maintainers' own work also requires an appropriate
chain of authority where employment or contracting terms may apply.

Keep rights records securely; do not publish private contracts or identity
records in this repository. A future automated CLA check is not implemented
and is not claimed by this policy.

## Documenting a new feature

Keep `README.md` a short front door; it is also the NuGet package page. Do not
add a per-feature section to it. For a new capability:

1. Update its record in `site/data/compatibility.json` (Can I Render).
2. Add a note under the matching area in `docs/feature-notes.md`, with the
   example report, oracle, test evidence and source commit.
3. Update the exact contract in the relevant `docs/` page, such as
   `docs/css-stylesheets.md`.

Change the README only when its summary tables, quick start or status become
inaccurate.
