# FactsPDF licensing guide

This is a practical summary, not an additional license. [LICENSE.md](LICENSE.md)
is the controlling Community text. Commercial rights require a separate agreed
contract; see [COMMERCIAL.md](COMMERCIAL.md).

## The policy

FactsPDF is **source-available**, not MIT or OSI-approved open source. Public
source, a public GitHub repository, an open-source wrapper, or a package-manager
label is not an unrestricted use grant.

| Actual use | Community eligibility |
| --- | --- |
| Individual's own non-business project | Free |
| Entity or sole trader whose consolidated Group annual gross revenue is at most USD 1,000,000 | Free, including proprietary paid products and SaaS |
| Group at exactly USD 1,000,000 | Free |
| Group above USD 1,000,000, no other exemption | Commercial agreement required |
| Legally qualifying charity using it for its charitable operations | Free regardless of revenue |
| Qualifying open-source project, within that project only | Free regardless of sponsor/operator revenue |
| Employee or agency building an internal application for a large client | Assess the client, not the employee or agency's small project budget |
| Recipient of a generated PDF, without receiving the engine | No FactsPDF license required merely to use the PDF |
| Recipient who executes a bundled engine or browser WASM module | Independently eligible or expressly covered by commercial redistribution rights |

The exemptions are alternatives, not cumulative requirements. An exemption
covers the actual use described by it, not every activity of its sponsor.

## Revenue and changes

The default period is the most recently completed full 12-month financial year.
New entities without one use actual history up to 12 months, reassessed monthly.
The calculation consolidates entities under control, counts all operations,
eliminates duplicate intercompany amounts, and is gross revenue rather than
profit, valuation, or funding. Section 3 of LICENSE.md controls conversions,
reorganizations, and exact assessment dates.

Previously eligible use has a 90-calendar-day transition after losing all free
categories. Existing use may continue during that period; expansion to new
products or customers is not covered. Afterwards, obtain commercial rights or
stop. There is no retroactive charge for periods of compliant use. A previously
ineligible user does not receive a free 90-day trial.

No automatic general evaluation license is provided. Non-qualifying companies
should obtain written evaluation permission before building or executing the
engine, including for pre-production integration.

## Examples that avoid surprises

- A USD 700,000 software company may sell its own closed-source hosted invoice
  service using FactsPDF under Community terms. Large customers who only use
  that general service and receive PDFs do not need a separate engine license.
- A USD 20 million parent does not gain free proprietary use by putting it in a
  USD 10,000 subsidiary. Group revenue is the measure.
- A large company may develop and operate a genuinely qualifying open-source
  application under the project exemption. Publishing only its FactsPDF helper
  while keeping the actual application proprietary does not qualify that app.
- A developer may retain and share generated PDFs after an engine license ends.
  Continuing to run the engine is a separate question.
- A small vendor distributing a desktop app that embeds FactsPDF must include
  the engine license. Large downstream organizations executing the engine need
  their own entitlement unless a negotiated redistribution license covers them.
- Renaming a FactsPDF fork or publishing a TypeScript wrapper does not convert
  the engine to MIT. A genuinely independent implementation is not claimed as
  FactsPDF merely because it implements HTML/CSS or PDF standards.

## Distribution and compatibility

Keep the complete license and required notices with source and compiled copies.
An application's own code is not forced open merely because its eligible author
uses FactsPDF. Nonetheless, this custom license does not promise compatibility
with GPL, AGPL, or every other project's distribution requirements; examine the
actual combination separately. A free open-source-project exemption and
license compatibility are different questions.

There is no document-volume royalty, watermark requirement, mandatory
registration, or required disclosure of revenue in a public issue. Eligibility
records should be retained privately. This is a policy description, not a claim
that a working engine or licensing-enforcement implementation exists.

## Scope of these documents

FactsML's separate MIT license is not copied into FactsPDF and is not revoked.
Third-party code, fonts, images, and other assets retain their own licenses.
The Community text is versioned: later edits do not silently change the terms
attached to an earlier copy. Actual paid terms are not inferred from a README.

This initial licensing text has not been reviewed by retained legal counsel.
The release and commercial-activation checks in
[docs/licensing-review.md](docs/licensing-review.md) must be completed before
publishing distributable software or accepting paid license orders. That
maintainer policy does not nullify permissions expressly granted in LICENSE.md.
