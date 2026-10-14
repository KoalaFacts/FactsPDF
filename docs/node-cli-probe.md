# Work package B1: Node calls the existing CLI

This probe checks the first integration step from the accepted preview roadmap:
Node sends explicit UTF-8 input/resources to the existing FactsPDF CLI and receives
PDF bytes. The same engine and production CLI are unchanged. This is a subprocess
integration experiment, not an in-process binding, npm package, stable Node API,
TypeScript SDK, or WASM implementation. It has no npm dependencies.

## Reproduce

Use Node 22 or later and the repository's .NET 10 SDK. Independent integration
inspection needs qpdf and Poppler's pdftotext, plus two explicitly supplied trusted
static TrueType files covering the test's Latin and Chinese text. No font is
downloaded or discovered by the probe. No font, executable or PDF is uploaded.

From the repository root on Linux, with the same Debian fixture packages as CI:

```sh
dotnet build src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release
node --test scripts/test_node_cli_process.mjs

FACTSPDF_CLI="$(command -v dotnet)" \
FACTSPDF_CLI_DLL="$PWD/src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll" \
FACTSPDF_PROBE_FONTS='["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf","/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"]' \
node --test scripts/test_node_cli_integration.mjs
```

For a previously built native executable, set `FACTSPDF_CLI` to its absolute path
and leave `FACTSPDF_CLI_DLL` unset. CI runs this mode against the CLI it actually
publishes with `PublishAot=true`; it also runs the subprocess tests on Linux,
Windows and macOS. Managed execution locally does not establish Native AOT support
on that platform. Direct executable invocation alone is not proof of AOT provenance.

`FACTSPDF_QPDF` and `FACTSPDF_PDFTOTEXT` can name explicit inspector executable paths
when they are not on PATH. Missing input files, fonts, inspectors, or configuration
fail the probe; there are no skipped-success substitutes. Font paths are a JSON
array in the requested fallback order, so spaces and non-ASCII filenames remain
literal arguments. Keep the files appropriately licensed; they are not distributed.

## What is checked

The 12 process tests use real small Node children. They check binary Buffer I/O,
UTF-8 bytes, stderr separation, literal argv without a shell, nonzero and spawn
errors, input-pipe failure, timeout, running/pre-launch cancellation, bounded
stdout/stderr, and independent concurrent calls. Timeout, cancellation and noisy
output tests assert the child no longer exists before the promise rejects.

The seven integration tests use the actual FactsPDF CLI:

- ASCII stdin/stdout PDF bytes equal a separate direct CLI file invocation.
- Ordered explicit fonts preserve `报告 探针 café`; the binary PDF matches the
  direct file invocation, qpdf checks structure, and pdftotext checks full text.
- Malformed UTF-8 returns exit 3 without PDF bytes.
- Unsupported HTML returns FPDF1102/exit 3 and preserves an existing output under
  the CLI's normal `--overwrite` failure behavior.
- A missing explicit font returns exit 4 without a partial PDF.
- A bad CLI option retains exit 2 and its usage diagnostic.
- Two concurrent conversions have different PIDs and isolated outputs, each
  byte-identical to its own serial result; the second document is independently
  inspected as well.

Test logs record Node/platform/architecture, invocation mode, executable/DLL and
ordered font hashes, and each inspected PDF's byte count and SHA-256. Those hashes
identify supplied artifacts; they are not a signed build receipt or a claim that
an arbitrary supplied executable was built from the current checkout. CI supplies
build provenance through its same-job checkout/publish steps. The generated PDFs
are tiny probe documents in a unique temporary directory, removed after the tests;
they do not replace or regenerate the accepted report/reference PDFs.

## Boundary and result

`scripts/node_cli_process.mjs` is an internal verification helper. It invokes argv
directly with `shell:false`, writes input as bytes, and waits for Node's `close`
event before resolving/rejecting. It preserves exit code and stderr. Successful
process exit by itself is not PDF validation; the integration tests supply that.

Timeout/AbortSignal **terminates the child process** and discards captured output.
It does not demonstrate .NET cooperative cancellation or a CLI exit 130. It assumes
the actual FactsPDF CLI, which creates no descendants; it is not a generic process
tree sandbox. Cancellation of a CLI writing a file could leave its temporary file,
so the probe's cancellation model is intended for stdin/stdout calls, with callers
committing bytes only after successful conversion. It does not promise rollback
of arbitrary stream I/O failures or cleanup of a hostile program.

Output collection defaults to 64 MiB stdout and 1 MiB stderr with a 10-second
deadline. The limits bound retained stream data, not total Node/renderer memory;
input and Buffer concatenation also allocate. The CLI's existing resource limits
remain in force. This is not a throughput benchmark or constant-memory claim.

Initial local evidence: Node 24.19.0/Linux x64, .NET SDK 10.0.100, successful
Release build (zero warnings/errors), 12/12 process tests and 7/7 real managed-CLI
integration tests. Process tests were observed 0/12 before implementation and
12/12 afterward. The integration tests document existing renderer behavior, so
their passing first run is not claimed as a renderer regression fix. Final CI
results belong to the PR's actual tested commit, not an assumed green status here.

B1 gives a reproducible positive result for Node subprocess use. WASM is the next
separate B probe, after this change's review and integration. The remaining preview
targets, human reference approval, numerical performance budgets and publishing
remain separate; this probe grants none of those approvals.
