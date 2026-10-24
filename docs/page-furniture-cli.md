# Structured page furniture: CLI configuration

This is the in-development PR #57 CLI contract, not a published package or a completed cross-platform acceptance claim. Core .NET callers use `PdfOptions.PageFurniture` directly. The legacy flags and their default output remain separate.

## Run the repository example

From the repository root, using the repository's required .NET SDK:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- \
  examples/quick-start.html report.pdf \
  --page-furniture examples/business-page-furniture.json
```

This ASCII example and configuration need no external resources. For templates requiring Unicode, styles or images, also supply the appropriate explicit `--font`, `--font-bold`, `--font-italic`, `--font-bold-italic` and `--image` options. Configuration never loads resource paths or URLs. No font files are bundled.

## Contract

`--page-furniture path.json` occurs once and cannot be combined with `--page-numbers`, `--header-text` or `--footer-text`, even when legacy text is empty. The file must be strict UTF-8 JSON, at most 65,536 bytes including an optional single leading UTF-8 BOM, with maximum depth eight. Input is bounded while reading; the CLI never trusts a stream length. Property names are case-sensitive; unknown and duplicate properties, comments, trailing commas, malformed Unicode and wrong types are rejected.

The root accepts only `default` and `firstPage`. Both contain any of `headerLeft`, `headerCenter`, `headerRight`, `footerLeft`, `footerCenter`, `footerRight`. Omitted `default` is empty; `default: null` is invalid. Omitted or null `firstPage` inherits default regions; `firstPage: {}` suppresses all first-page furniture. A nonempty first-page object replaces the whole set, not selected fields. Physical page numbering does not restart.

Each region is absent when omitted or null. An object accepts `template` (string, default empty), `fontSize` (finite number from 1 to 144, default 10), `color` (exactly `#RRGGBB`, default black), `bold` and `italic` (booleans, default false). Text supports `{page}`, `{pages}`, and doubled braces for literal braces. The core enforces the 1,024 UTF-16-unit template limit, total input/display/output budgets, explicit font roles, single-line geometry and collision rules. It does not silently wrap, truncate, move the body or invent missing font styles.

## Failures and recovery

Configuration/argument failures return 2; core template/font/layout failures return 3; file-access failures return 4; cancellation returns 130. PDF bytes are not mixed with diagnostics. The destination may not be the configuration file, even with `--overwrite`. Existing destinations are replaced only after successful conversion, and temporary output is removed on failure. Fix the reported input and rerun; do not remove guards or treat a refused configuration as an empty one.

## Verification checkpoint

The first 48 CLI contract cases were committed before implementation at `85268dd112a3e068d7f9488fa4edf2f309b7f257`. A subsequent non-conflicting documentation commit preserved them at `946d1eaae54a11ead63b6bbcebf5bd306d5fbd9e`. Its actual [macOS test job](https://github.com/KoalaFacts/FactsPDF/actions/runs/40118458882/job/120944641868) compiled and ran 1,324 tests: all 48 new cases failed for the missing `--page-furniture` option, while all 1,276 existing cases passed. This is observed RED evidence, not a claimed acceptance pass. The current implementation must pass those unweakened cases, the direct-decoder and bounded non-seekable stream tests, and the remaining PR gates.

Remaining whole-feature gates include protected browser integration, actual managed/API/Native AOT PDF evidence, the precise independent geometry/body-preservation oracle, legacy-byte baselines, independent review and all required checks for the final head. The existing Windows Native AOT smoke alone does not exercise new furniture or continued-row output. A neutral CodeQL result is not a successful analysis. PR #57 remains draft until the full accepted plan is complete.
