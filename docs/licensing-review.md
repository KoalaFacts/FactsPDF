# Licensing review and release gates

Status: maintainer-authored checklist; not reviewed by retained legal counsel.
It is a release policy, not an amendment to the grants in ../LICENSE.md, and not
a legal opinion.

FactsPDF is licensed by **Koala Facts Trading Pty Ltd** (ABN 88 602 884 408)
under the Apache License 2.0. Paid commercial support is offered separately
(see ../COMMERCIAL.md).

## Before the first software-package release

- [x] Name the licensor: Koala Facts Trading Pty Ltd (owner decision).
  LICENSE.md and COMMERCIAL.md identify it.
- [x] Record the company's ABN: 88 602 884 408.
- [x] Use the standard Apache License 2.0, verbatim, in LICENSE.md.
- [x] Confirm no employer of the owner has a claim on FactsPDF (owner
  confirmation). Repeat this check for any future contributor or contractor.
- [ ] Assign or license existing maintainer-authored FactsPDF code to
  Koala Facts Trading Pty Ltd in writing, so the company holds the rights it
  licenses.
- [ ] Ask counsel how AI-assisted authorship affects copyright in the code.
- [ ] Review third-party code, transitive components, fonts and test assets and
  keep required notices. A no-third-party-engine goal is not a provenance audit.
- [ ] Include LICENSE.md in every source, binary, NuGet, CLI, WASM, container
  and language-binding distribution. Verify the actual archive, not only the
  repository.
- [ ] Agree the terms of any paid support engagement (scope, price, duration,
  liability, mandatory consumer protections) before accepting an order.
  COMMERCIAL.md grants none of these by itself.

Contributions are accepted under Apache-2.0 (see ../CONTRIBUTING.md).

The NuGet package declares the SPDX expression `Apache-2.0` and also carries
LICENSE.md, which the package contract scripts compare byte for byte with the
repository copy.

## References

- [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
- [NuGet .nuspec reference](https://learn.microsoft.com/en-us/nuget/reference/nuspec):
  license metadata.
