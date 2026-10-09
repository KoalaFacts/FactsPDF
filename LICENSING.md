# FactsPDF licensing guide

This is a practical summary, not an additional license. [LICENSE.md](LICENSE.md)
is the controlling Community text. Commercial rights require a separate agreed
contract; see [COMMERCIAL.md](COMMERCIAL.md).

## The policy

FactsPDF provides **Community and Commercial licensing paths**. Commercial
licenses are available by arrangement; enquiries are welcome through
[the commercial licensing contact process](COMMERCIAL.md#contact-and-safe-handling).
Issuing paid licenses and accepting orders still require the documented legal
and authorization checks. Technical support, an SLA, updates, or unlimited
rights are not automatically included.

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
Eligible Community users may also seek a separately agreed Commercial License.

## Publish changes to the engine, not your independent application

Version 1.1 requires public release of Covered Modifications to FactsPDF itself
under the same FactsPDF Community License 1.1. This is source-sharing under a
custom license, not an assertion that FactsPDF is MIT, GPL, AGPL, or OSI-approved.
Sections 4.1-4.5 of LICENSE.md control the following summary.

| Situation | Source-publication requirement |
| --- | --- |
| Call an unmodified engine from an independent closed-source app | No application-source disclosure merely because of that call |
| Fix or extend engine code and distribute that version | Publish the engine changes and corresponding build materials no later than distribution |
| Use a modified engine internally for real document work | Publish no later than first operational use |
| Operate a SaaS/API with a modified engine, even if users only receive PDFs | Publish no later than making that service available |
| Privately develop/test/evaluate a modification solely in a controlled development environment | Can remain private until distribution, operational use, or service provision; not a free evaluation license for an ineligible user |
| Pay for a Commercial License | No automatic private-fork exemption; the standard agreement must incorporate the same publication duties |
| Publish an independent API wrapper without copying or adapting engine code | Wrapper is not a Covered Modification merely because it calls the engine |
| Rename, split, translate, or move modified engine code into another package | The engine changes remain covered |

Publish the preferred editable source, required engine build/generation scripts,
changed tests, dependency/toolchain identifiers, and usable build instructions.
A public source tree or complete patch set against an available exact base is
acceptable. A changelog, screenshots, an unusable fragment, or binaries alone
are not sufficient. A patch needs its exact base; if that base stops being
publicly available, supply the base material you may lawfully redistribute.

Use a stable public repository or download with no payment, login, NDA, or
permission request. Identify the source URL in the modified product's documents
or legal notices and in hosted-service documentation. No notice inside generated
PDFs is required. Keep each triggered source version available throughout use
or distribution and for at least three years after the last such activity.

Do not publish customer data, credentials, private templates, deployment keys,
or unrelated business code. Keep those separate from engine modifications;
provide synthetic test fixtures and non-secret build placeholders as needed.
Private secrets do not justify hiding actual engine changes or necessary build
materials. Independent application code is outside this publication rule, but
claiming the open-source-project exemption still has section 2.3's requirements.

You can use a public fork or patch archive; sending an upstream PR is optional.
Publishing your modifications is not copyright assignment and does not by itself
let maintainers commercially relicense your work. The separate inbound-rights
process in CONTRIBUTING.md still applies before upstream incorporation.

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
ineligible user does not receive a free 90-day trial. That eligibility transition
does not postpone an already-triggered engine-source publication obligation.

No automatic general evaluation license is provided. Non-qualifying companies
should obtain written evaluation permission before building or executing the
engine, including for pre-production integration.

## Examples that avoid surprises

- A USD 700,000 software company may sell its own closed-source hosted invoice
  service using FactsPDF under Community terms. Large customers who only use
  that general service and receive PDFs do not need a separate engine license.
  If the provider changes the engine, it must publish those engine changes,
  not its independent invoice service or customer data.
- A USD 20 million parent does not gain free proprietary use by putting it in a
  USD 10,000 subsidiary. Group revenue is the measure.
- A large company may develop and operate a genuinely qualifying open-source
  application under the project exemption. Publishing only its FactsPDF helper
  or engine patch while keeping the actual application proprietary does not
  qualify that app for the exemption.
- A developer may retain and share generated PDFs after an engine license ends.
  Continuing to run the engine is a separate question. A triggered source-hosting
  obligation continues for its specified retention period.
- A small vendor distributing a desktop app that embeds FactsPDF must include
  the engine license. Large downstream organizations executing the engine need
  their own entitlement unless a negotiated redistribution license covers them.
- Renaming a FactsPDF fork or publishing a TypeScript wrapper does not convert
  the engine to MIT. A genuinely independent implementation is not claimed as
  FactsPDF merely because it implements HTML/CSS or PDF standards.

## Distribution and compatibility

Keep the complete license and required notices with source and compiled copies.
An application's own independent code is not forced open merely because its
eligible author uses FactsPDF. Nonetheless, this custom license does not promise
compatibility with GPL, AGPL, or every other project's distribution requirements;
examine the actual combination separately. A free open-source-project exemption
and license compatibility are different questions.

There is no document-volume royalty, watermark requirement, mandatory
registration, or required disclosure of revenue in a public issue. Eligibility
records should be retained privately. This is a policy description, not a claim
that a working engine or licensing-enforcement implementation exists.

## Scope and license versions

FactsML's separate MIT license is not copied into FactsPDF and is not revoked.
Third-party code, fonts, images, and other assets retain their own licenses.
Version 1.1 introduces mandatory sharing of engine changes. It applies to material
offered under that version; it does not retroactively change version 1.0 grants
or an already agreed commercial contract. [License history](docs/license-history.md)
identifies the earlier text. Historical terms are not a choice of license for
new material published only under version 1.1.

This initial licensing text has not been reviewed by retained legal counsel.
The release and commercial-activation checks in
[docs/licensing-review.md](docs/licensing-review.md) must be completed before
publishing distributable software or accepting paid license orders. That
maintainer policy does not nullify permissions expressly granted in LICENSE.md.
