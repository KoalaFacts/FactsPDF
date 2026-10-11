# Work package A — document acceptance candidates

These tools exercise the **unchanged** FactsPDF engine. A successful capture means that the selected existing capabilities generated independently inspected PDFs; it is **not human approval**, a frozen reference, a complete Developer Preview or a performance guarantee.

## Reproduce

Use the repository's .NET 10 SDK. On Linux install the same independent inspectors as the existing workflows: qpdf, Poppler, Chrome/Chromium and Python Pillow, plus native compiler dependencies. The acceptance scripts use Python standard library except optional image-difference generation (Pillow). None is a new engine runtime dependency.

```sh
python3 -m unittest discover -s scripts -p 'test_acceptance_*.py' -v
python3 scripts/acceptance_corpus.py generate artifacts/acceptance-inputs
python3 scripts/acceptance_corpus.py validate artifacts/acceptance-inputs/manifest.json
python3 scripts/acceptance_build.py --cli-output artifacts/acceptance-cli --host-output artifacts/acceptance-host
export FACTSPDF_SOURCE_SHA=$(git rev-parse HEAD)
python3 scripts/acceptance_run.py capture --manifest artifacts/acceptance-inputs/manifest.json \
  --native-cli artifacts/acceptance-cli/FactsPDF.Cli \
  --api-host artifacts/acceptance-host/FactsPDF.Acceptance \
  --font /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf \
  --font /usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf \
  --reviews docs/acceptance/reference-reviews.json \
  --measure --output artifacts/document-acceptance
```

Supply your own trusted, appropriately licensed static TrueType files when not using the CI fixtures. CI explicitly installs `fonts-dejavu-core` and `fonts-droid-fallback`; consult their installed copyright/license notices and the upstream DejaVu/Droid notices before using them. Font files are never committed or distributed in the review package. Their order, names and SHA-256 fingerprints are recorded. No font discovery or network loading occurs inside FactsPDF. Chrome alone uses file URLs for these explicit resources as independent print normalization.

Build from a clean committed checkout into fresh directories. The builder runs
both real `dotnet publish` commands, verifies the host's compiled source SHA and
Native AOT status, and writes identical build receipts binding both executable
hashes to that source. Capture rejects absent/mixed receipts, another source or
replaced executable bytes. The receipt is included in `environment.json`.
It is a trusted-workspace build record, not cryptographic attestation against a
malicious builder. The host's source identity comes from compiled assembly
metadata; changing `FACTSPDF_SOURCE_SHA` cannot relabel its build.

`artifacts/document-acceptance/public/report.md` is the entry point. Each B-case directory includes the real native PDF, independent Chrome PDF, every page's rendered PNG, text, geometry and comparison record. Chrome uses the same physical page size, margins and explicit fonts, normal heading weight and disabled browser furniture. It is not used to create the FactsPDF output. Unregistered full-page differences are evidence, not a global pixel tolerance. Page-count differences are reported and require review; there is no claim of exact browser typography parity.

### Candidate reading space (still pending human review)

B01 explicitly gives the Final note heading `padding-top:8pt` after the panel.
B03 gives each of its two explicitly page-broken headings `padding-top:10pt`.
This is content-owned reading space: the outer section retains its 10pt padding,
1pt border and default slice behavior. Its first/middle/last horizontal edges
remain top/none/bottom. The shared `.next` rule and the engine are unchanged.

After capture, run:

```sh
python3 scripts/acceptance_reading_space.py artifacts/document-acceptance/public artifacts/document-acceptance/public/reading-space.json
```

This independently re-rasterizes native and Chrome B01/B03 PDFs and measures
Poppler word boxes. It requires at least 6pt from B01's rasterized panel bottom
to the following heading's word box, and at least 8pt from the 36pt content top
to both B03 continuation headings. These are new minimum reading-space guards,
not relaxed page-boundary tolerances or human-approved exact references. CSS
spacing and glyph/bbox positions differ; the report retains actual coordinates.
B01 must stay one page and B03 three, with the original sliced edge assertions.
`--renderer native` is an explicit partial local diagnostic, never a claim that
Chrome was inspected. CI requires both. All other text, boundary, record-order,
CLI/API and negative-case checks remain in the complete capture.

The generator is the version-controlled source of synthetic templates and data. It emits exact byte/hash manifests into a fresh artifact directory; these candidates are not yet frozen reference files. After review the accepted inputs/output references must be retained durably in the repository, not solely in a 14-day Actions artifact. Generation never edits `reference-reviews.json`. This bootstrap arrangement avoids committing fabricated reference hashes before inspection.

## State model and approval

Six baseline cases test current capabilities. Twelve negative cases test documented diagnostics and preservation of existing output. Nine targets represent missing preview features. Do not add these denominators together as a support percentage. Host-only quotas and malformed UTF-16 are explicitly API-only, not skipped CLI passes. A separate binary-stdout probe, pre-cancelled API probe and two parallel conversions check boundary behavior.

`capture` leaves `review_status=pending-review` and never changes human approvals. `verify` requires valid exact input/environment/font references, traceable human review and durable artifact hashes. Empty/missing/duplicate reviews or missing entrypoint results fail. `--strict-targets` separately requires all future targets to be verified; currently they are not.

No `--approve-current-output` command exists. Before freezing, a human must review every native page against the source/Chrome evidence. Preserve the accepted PDF, extracted text and page PNGs under a repository reference directory, then record the actual reviewer and discussion, input/source/environment/font/PDF/text/image fingerprints and checked requirements. Do not enter the assistant as a human reviewer or assume planning approval approves unseen pages. Re-run `verify` on the same environment; drift needs a separate reviewed reference. The candidate capture workflow must be switched to default verify only after actual reference approval, in a reviewed change.

## Error and resource boundaries

Production CLI options are unchanged. Acceptance-host `--limit`, `--max-pages`, `--probe-existing-output`, `--invalid-scalar` and `--pre-cancelled` are **test-host only**. Invalid surrogates are constructed from integers after reading valid UTF-8, not placed in metadata. Error cases seed `keep` and check byte preservation for both applicable entrypoints. Final arbitrary stream I/O failure is not a rollback guarantee.

Process execution uses explicit argument lists and isolated directories. POSIX timeouts kill/reap the process group; unsupported cleanup is an infrastructure error. This is a trusted-workspace test harness, not a general hostile-process sandbox. No user/client data or credentials enter the corpus.

Only this work package is implemented. No new lists/images/tables/font-role APIs, other-language SDKs, Windows AOT release, WASM, licenses or packages are delivered.
