# FactsPDF licensing guide

This is a plain-language summary, not an additional license. The controlling
terms are the two standard licenses in [LICENSE.md](LICENSE.md). Rights beyond
them need a separate agreement; see [COMMERCIAL.md](COMMERCIAL.md).

## The policy

Koala Facts Trading Pty Ltd (ABN 88 602 884 408) licenses FactsPDF under your
choice of:

- [PolyForm Small Business License 1.0.0](https://polyformproject.org/licenses/small-business/1.0.0), or
- [PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0).

You only need to qualify under one of them. Everyone else needs a commercial
license from Koala Facts Trading Pty Ltd.

FactsPDF is **source-available**, not MIT or OSI-approved open source. Public
source, a public GitHub repository or a package-manager listing is not an
unrestricted use grant.

## Who can use it free

| Actual use | License | Free? |
| --- | --- | --- |
| Your company has fewer than 100 employees and contractors **and** less than USD 1,000,000 (2019 dollars, CPI-adjusted) revenue in the prior tax year | Small Business | Yes, including closed-source products and SaaS |
| Personal study, hobby or research with no anticipated commercial application | Noncommercial | Yes |
| Charity, school or university, public research body, public safety or health body, environmental body or government institution | Noncommercial | Yes, regardless of funding source |
| Any other noncommercial purpose | Noncommercial | Yes |
| A company at or above either Small Business threshold, for commercial use | — | No; commercial license required |
| Receiving a PDF that FactsPDF generated | — | No license needed to use the PDF |

**"Your company" includes affiliates.** Both thresholds count every
organization that controls, is controlled by, or is under common control with
the company you work for. A large group cannot qualify by using a small
subsidiary.

**Contractors and agencies** building software for a client use it for the
client's benefit, so assess the client's size, not the agency's.

**Open-source projects** have no separate exemption. A project qualifies when
its own use is noncommercial or the people using FactsPDF work for a qualifying
small business.

**Evaluation** by a company above the thresholds is not covered by either
license. Ask for written evaluation permission before building or running the
engine.

## Changes, distribution and notices

- You may modify FactsPDF for a permitted purpose. Neither license requires you
  to publish your changes.
- You may distribute FactsPDF, changed or not, inside your product. Everyone who
  receives a copy must also receive the license terms (or their URL) and the
  `Required Notice:` line from LICENSE.md.
- Distribution does not pass on your license. Each person or organization that
  runs the engine must qualify under one of the licenses, or be covered by a
  commercial license that expressly includes redistribution to them.
- If you stop qualifying (for example, your company grows past a threshold),
  the free license no longer covers your use. Obtain a commercial license or stop.
  Generated PDFs you already have remain yours.

## Scope and license versions

Third-party code, fonts, images and other assets keep their own licenses.

Before the switch to PolyForm, FactsPDF was published under the custom FactsPDF
Community License 1.0 and 1.1. Copies obtained under those versions keep the
rights they were granted; this change does not revoke them. Source published
from this change onwards is licensed under the PolyForm terms. See
[license history](docs/license-history.md).

This guide has not been reviewed by retained legal counsel. Complete the
[release and commercial-activation checks](docs/licensing-review.md) before
publishing distributable software or accepting paid orders.
