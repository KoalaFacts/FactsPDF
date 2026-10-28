# Publishing the FactsPDF NuGet preview

The workflow is `.github/workflows/publish-nuget.yml`, displayed as **FactsPDF
NuGet preview**. It follows HeroParser's **production environment + NuGet/login
OIDC + provenance** approach. It builds FactsPDF's own declared package directly;
it does not depend on a pre-existing GitHub Release or publish HeroParser's CLI
and symbol packages. Dotted prereleases such as `0.1.0-preview.1` are accepted.

Adding or merging this workflow does **not** publish a package. Pull requests
run verification only. Manual runs default to verification only and do not
upload engine binaries. There is no push/tag/release-triggered publication.

## One-time account settings

In the existing NuGet publisher account, create a trusted-publishing policy:

| Setting | Value |
| --- | --- |
| Repository owner | `KoalaFacts` |
| Repository | `FactsPDF` |
| Workflow file | `publish-nuget.yml` (filename only) |
| Environment | `production` |
| Package glob | `FactsPDF` |
| Scopes | Permit publishing a **new package** for the first preview, and new versions |

Choose the actual package-owning user or organization; do not infer ownership
from the GitHub organization name. An existing **HeroParser** trust policy does
not automatically authorize **FactsPDF**.

The owner explicitly selected **`KoalaFacts`** as `NUGET_USERNAME`. The publish
job sets this public profile name directly in its `env` and passes the same
value to `NuGet/login`; no `NUGET_USERNAME` secret or variable is required or
read. This configures the requested identity, not the account-side trust policy.
The temporary `NUGET_API_KEY` still comes only from the OIDC login output; no
long-lived API key is stored in source or required as a repository secret.

Configure the GitHub `production` environment for the existing owner/release
approval process and restrict it to `main`. Merely naming an environment in
YAML does not configure its protection rules. This change does not claim to
have created the account policy, secret or environment protections.

Official setup: [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
Reference: [HeroParser publisher](https://github.com/KoalaFacts/HeroParser/blob/main/.github/workflows/publish-nuget.yml),
audited blob `105cb01058cac00da7af67c8165f16652704b301`.

## Verify, then publish

1. Merge through the repository's existing independent review and CI rules.
   The Actions manual-run button is available after the workflow reaches the
   default branch. Run **FactsPDF NuGet preview** on `main` with version
   `0.1.0-preview.1` and `publish=false` to exercise the complete packaging path.
2. Before public distribution, complete and record the applicable existing
   [licensing/rights review](licensing-review.md) and human acceptance gates.
   Neither successful CI nor the workflow's confirmation checkbox is evidence
   that a legal review or human PDF acceptance actually occurred.
3. On `main`, run the workflow with the source-declared version, `publish=true`
   and `release_gates_confirmed=true` only when those facts are established.
   Approve the configured `production` environment when requested.

The current source declares **FactsPDF / 0.1.0-preview.1 / net10.0**, with SDK
**10.0.402** and strict consumer runtime **10.0.13**. A different input cannot
relabel the current build. Changing the source version/toolchain requires the
existing candidate-contract updates and reviews; this workflow is not a stable
release or arbitrary-version publisher.

## What the workflow proves

Before a real publication it requires successful **push-to-main FactsPDF CI for
that exact source SHA**, retaining the complete existing platform/Native AOT and
candidate gates. It then builds a fresh NuGet package, runs the managed suite,
uses the **compiled PackageInspector**, and executes the existing strict clean
consumer/API/managed-CLI/PDF/resource/failure-recovery verifier on that package.
This is not a replacement for the separate five-component private-candidate
archive, nor a claim that old binaries were rebuilt from a newer source SHA.

A source/version/SHA256/member manifest binds the package transferred between
jobs. Only the NuGet package and manifest are uploaded, only for an approved
publication. Fonts, CLI binaries, symbols and unexpected entries are rejected.
The production job rechecks the exact bytes before requesting an OIDC key,
attests provenance, and pushes **one** package without masking duplicate-version
errors. A failed-job-only rerun uses the artifact name emitted by the successful
verification job, not a nonexistent new-attempt artifact.

After NuGet accepts the push, the workflow downloads the public package with
bounded retries and compares every original package member. NuGet's added
`.signature.p7s` is permitted, but changed assembly, licence, README, metadata or
extra assets are not. This content comparison is not certificate validation.
Only successful public readback produces the final published/verified summary.
If push succeeds but readback times out, inspect NuGet before retrying; do not
assume failure means no package was published, or overwrite/relabel the version.

No GitHub Release/tag, commercial grant, price, legal identity, customer delivery
or scheduled-task change is created by this workflow. Community/Commercial
terms and the engine-modification publication policy remain unchanged.
