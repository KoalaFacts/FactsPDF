# Licensing review and release gates

Status: initial maintainer-authored licensing documents; not reviewed by retained
legal counsel. This checklist is a maintainer release policy, not an amendment
to the grants in ../LICENSE.md. It does not represent a legal opinion or promise
that a license eliminates all legal risk.

## Before the first software-package release or paid order

- [ ] Identify the actual copyright holders and authorized commercial licensor;
  verify employment/contractor and contribution rights. Do not substitute the
  KoalaFacts GitHub organization label for an unknown incorporated entity.
- [ ] Obtain review by counsel familiar with software licensing, Australia, and
  intended sales jurisdictions. Review free-category definitions, Group revenue,
  financial-year boundaries, the 90-day transition, downstream execution, and
  open-source-license compatibility against the intended business model.
- [ ] Review acceptance mechanics, termination, warranty and liability provisions,
  mandatory consumer protections, and unfair-contract-term obligations. A broad
  disclaimer or a savings clause alone does not guarantee compliance.
- [ ] Approve the actual commercial agreement, its parties, price, duration,
  renewal, post-expiry rights, redistribution scope, and private sales channel
  before accepting an order. COMMERCIAL.md grants none of these by itself.
- [ ] Establish documented contributor permissions before merging external work;
  review third-party code, transitive components, fonts, and test assets and keep
  required notices. A no-third-party-engine goal is not a provenance audit.
- [ ] Include LICENSE.md and all applicable notices in every source/binary,
  NuGet, CLI, WASM, container, and language-binding distribution. Verify the
  actual archive, not only the repository. Do not label the engine MIT.
- [ ] When a NuGet package exists, use a packed license file via PackageLicenseFile
  (LICENSE.md), not a fabricated SPDX license expression. For a future npm
  package use `"license": "SEE LICENSE IN LICENSE.md"` and include that file.
- [ ] Preserve license/version evidence for releases; do not retroactively
  replace historical notices. Confirm README and sales summaries match the
  controlling grant. Implement enforcement or declarations only after their
  behavior, privacy impact, and accessibility are designed and approved.

No automated license CI check, CLA acceptance service, package validation,
legal review, sales mechanism, or license-key enforcement is asserted as
implemented by adding this checklist.

## References consulted on 9 October 2026

These primary sources inform the review topics. They do not approve, endorse,
or provide the text of the custom FactsPDF license.

- [OSI Open Source Definition](https://opensource.org/osd): source availability
  alone does not satisfy the open-source definition.
- [GitHub: licensing a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository):
  public-repository behavior and the importance of an explicit license.
- [GitHub Open Source Guides: legal considerations](https://opensource.guide/legal/):
  rights, licensing, and contributions.
- [ACCC: consumer rights and guarantees](https://www.accc.gov.au/consumers/buying-products-and-services/consumer-rights-and-guarantees):
  mandatory protections cannot simply be removed by contract wording.
- [ACCC: removing unfair contract terms](https://www.accc.gov.au/media-release/businesses-urged-to-remove-unfair-contract-terms-ahead-of-law-changes):
  standard-form contract review is a separate obligation.
- [NuGet .nuspec reference](https://learn.microsoft.com/en-us/nuget/reference/nuspec):
  custom license-file metadata.
- [npm package.json reference](https://docs.npmjs.com/cli/v11/configuring-npm/package-json/):
  custom license-file metadata.
