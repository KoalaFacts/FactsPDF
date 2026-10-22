# FactsPDF development guide

**Experimental renderer, not a production release or a complete browser-compatible implementation.** The shared core accepts a documented HTML/CSS subset, performs font-aware text layout/pagination and writes PDF objects directly. Public APIs may change. No extra markup language is required.

The initial ASCII-only M0 support matrix has been superseded by [Unicode/fonts](unicode-fonts.md), [native font subsetting](font-subsetting.md), and [embedded CSS stylesheets/selectors](css-stylesheets.md). Their detailed constraints apply alongside this guide. Explicit fixed-column tables with whole-row pagination use the [table subset](tables.md). Explicit standalone baseline JPEG images use the [image resource API](images.md). The controlled CSS block box model now includes width, four-sided padding, solid borders, background fills and paginated box fragments; see [M8 Box Fragmentation](box-fragmentation.md) for exact limits.

## Run from source

Use the .NET 10 SDK selected by `global.json`, from the repository root:

```sh
dotnet test FactsPDF.slnx -c Release
dotnet run --project src/FactsPDF.Cli -c Release -- examples/first-document.html first-document.pdf
```

For a bilingual stylesheet template, supply your own trusted, appropriately licensed static TrueType files:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- examples/stylesheet-document.html stylesheet.pdf --font fonts/Latin.ttf --font fonts/Chinese.ttf --subset-fonts
```

Stylesheets need no additional flag. Font paths are placeholders, not included assets. `--font` may repeat up to eight times. `--subset-fonts` is opt-in and respects no-subsetting font permissions; omitting it retains full embedding.

The CLI accepts input/output paths or `-` for stdin/binary stdout. Input must be valid UTF-8, with or without a UTF-8 BOM. Diagnostics go to stderr, not into PDF bytes. Existing output files require `--overwrite`; a sibling temporary file is replaced into place only after successful conversion. Input HTML and explicit font/image paths cannot be the output path. Use a trusted working directory; filesystem aliases, symlinks and concurrent hostile modifications are not sandboxed.

Exit codes: 0 success; 2 usage/argument error; 3 input/rendering limitation; 4 I/O/access error; 130 cancellation. Cancellation is cooperative and cannot interrupt a blocked synchronous caller-stream read/write until it returns.

## First use from local artifacts

The following Bash recipe uses the installed .NET 10 SDK and the current unreleased `0.1.0-dev` package. It builds local artifacts, then uses them from a fresh directory outside the checkout. No NuGet registry publication or credentials are needed. Package restore has one explicit local source and a new package cache; the installed SDK supplies the .NET targeting pack. This is the Linux first-use acceptance path, not a claim about Windows AOT or a published SDK.

From the repository root:

```sh
rm -rf src/FactsPDF/bin src/FactsPDF/obj src/FactsPDF.Cli/bin src/FactsPDF.Cli/obj
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release --no-self-contained -o artifacts/first-use-cli
dotnet pack src/FactsPDF/FactsPDF.csproj -c Release -o artifacts/packages
first_use_dir=$(mktemp -d /tmp/factspdf-first-use.XXXXXX)
mkdir -p "$first_use_dir/consumer" "$first_use_dir/feed" "$first_use_dir/packages"
cp examples/first-use/{FirstUse.csproj,Program.cs,NuGet.Config,input.html,unsupported.html,expected-pages.json} "$first_use_dir/consumer/"
cp global.json "$first_use_dir/"
cp artifacts/packages/FactsPDF.0.1.0-dev.nupkg "$first_use_dir/feed/"
cp -R artifacts/first-use-cli "$first_use_dir/cli"
export DOTNET_CLI_HOME="$first_use_dir/dotnet-home"
export NUGET_PACKAGES="$first_use_dir/packages"
export NUGET_HTTP_CACHE_PATH="$first_use_dir/http-cache"
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd "$first_use_dir/consumer"
dotnet restore FirstUse.csproj --configfile NuGet.Config --packages "$NUGET_PACKAGES" --no-http-cache
dotnet build FirstUse.csproj -c Release --no-restore
dotnet bin/Release/net10.0/FirstUse.dll input.html consumer.pdf
dotnet "$first_use_dir/cli/FactsPDF.Cli.dll" input.html cli.pdf
cmp consumer.pdf cli.pdf
```

Open either two-page PDF. The [complete consumer project](../examples/first-use/FirstUse.csproj) explicitly sets its framework and uses an exact `PackageReference`, with no source `ProjectReference`. Its [NuGet.Config](../examples/first-use/NuGet.Config) clears other feeds/fallback folders and maps FactsPDF to `../feed`. `NuGetAudit=false` avoids remote audit access during this local-only restore; it is not a security audit result. The SDK must already be installed; repository pack/publish may restore build tools from its normal trusted sources before the isolated consumer step.

The [consumer program](../examples/first-use/Program.cs) buffers conversion before opening the target. To see the unsupported-input path in the same directory:

```sh
printf 'keep original bytes' > preserved.pdf
dotnet bin/Release/net10.0/FirstUse.dll unsupported.html preserved.pdf
# Expected exit code 3 and FPDF1102 on stderr; preserved.pdf still contains the sentinel.
cat preserved.pdf
dotnet "$first_use_dir/cli/FactsPDF.Cli.dll" unsupported.html preserved.pdf --overwrite
# The CLI also exits 3 and preserves preserved.pdf.
```

The consumer's JSON records the loaded FactsPDF assembly identity and the actual
runtime version, framework description, OS, RID, process architecture, target
framework and dynamic-code support/compilation flags. The verifier checks these
against this sample's `net10.0` target and retains them separately from the SDK
version. A newer SDK compiling this sample does not establish execution on a
newer runtime. The flags describe managed dynamic-code capability; they are not
a Native AOT, WASM or JIT trace result.

The consumer demonstrates preservation on conversion errors. Its final `File.WriteAllBytes` is not an atomic-replacement policy for arbitrary file I/O failures. The production CLI retains its existing temporary-file/replace behavior. The sample output JSON identifies the loaded FactsPDF assembly as well as the resulting pages and bytes. Fontless ASCII input requires no extra resources. Keep the temporary directory while inspecting outputs; it is a local example, not an installation into your global package cache.

For automated, independent verification, return to the repository root and use a fresh output directory. Install qpdf and Poppler through your normal trusted system tooling; they are verification tools, not renderer dependencies:

```sh
python3 -m unittest discover -s scripts -p 'test_first_use.py' -v
python3 scripts/verify_first_use.py \
  --package artifacts/packages/FactsPDF.0.1.0-dev.nupkg \
  --cli-dll artifacts/first-use-cli/FactsPDF.Cli.dll \
  --dotnet "$(command -v dotnet)" --output artifacts/first-use
```

The verifier creates and removes its own separate temporary workspace and copies only the six source/input files. It allowlists the child process environment, uses an empty MSBuild user extension directory, and rejects repository files in the actual preprocessed import graph. It checks evaluated project properties, actual NuGet assets/source/config/cache paths, cached package SHA-512 and bytes, built and loaded assembly bytes/location, authored full text/page order and bounds, qpdf validity, rasterized pages and consumer/CLI byte equality. Unsupported input must preserve existing/absent targets and produce no PDF stdout. Recovery must reproduce the successful bytes. Reordered real PDFs and altered cached assemblies must fail the relevant checks. `report.json`, the import graph, command logs and PDF/page artifacts record actual results; no visual reference is automatically approved. The normal CI runs this check on the just-packed package and freshly published CLI.

Package acceptance requires a clean checkout, including untracked source even
when Git's user configuration normally hides it. Both package checks bind the
`FactsPDF` ID, version from `Directory.Build.props`, repository URL and exact Git
commit to the current source, require the existing sole `lib/net10.0/FactsPDF.dll`
asset and empty `net10.0` group directly under `dependencies`, reject additional
framework dependencies, and compare the packed license/README
with source. Additional NuGet build, analyzer, content and tool assets, including
the root `runtime.json` runtime graph, are rejected.
The consumer also reports the loaded library's informational version and inspects
the staged CLI assembly without invoking its entry point. Both must equal the
package version plus `+` and the same commit. A stale package with a matching stale
CLI fails, as does relabeling its nuspec or mixing a stale CLI with a new library.
The report retains package source and loaded binary identity separately from the
verifier's checkout and SDK/runtime. These checks detect accidental stale or mixed
trusted build artifacts; they are not cryptographic source-to-binary attestation.

The library and CLI use the SDK's default source/resource globs, which do not
honor Git ignore rules. Acceptance rejects ignored untracked files in those
project directories (except their top-level `bin`/`obj` build outputs), and ignored
parent `Directory.Build.props`, `Directory.Build.targets`, `Directory.Build.rsp`
and `Directory.Packages.props` inputs. It rejects `assume-unchanged` and
`skip-worktree` index flags that could hide tracked working-tree changes. This covers
global ignores and `.git/info/exclude`; it is tied to the current project layout.
The same build configuration filenames are conservatively rejected in every
ancestor of the checkout, even if a nearer configuration would shadow one.

The recipe and CI remove only the library and CLI's four generated `bin`/`obj`
directories before restore/build, independently of MSBuild's mutable clean
manifest. Stale generated compiler inputs such as `AssemblyInfo.cs` are then
regenerated; PDFs, reports and packages under `artifacts` are retained. This is a
requirement of the trusted build procedure, not a claim that the verifier proves
arbitrary supplied binaries were built cleanly or attests all build inputs.

The automated verifier reads the expected version and single target framework
from the clean checkout's explicit `Directory.Build.props` declaration, never
from the package under test. It specializes only the temporary consumer copy
with that framework and an exact package version. Without `--package`, it
requires the sole `artifacts/packages/FactsPDF.<declared-version>.nupkg`; stale
extra packages fail selection. Conditional, computed or multiple declarations
require a reviewed verifier update. This is a literal declaration reader, not a
general MSBuild evaluator. Future `net11.0` fixture tests exercise these paths
without executing .NET 11 or establishing RC/GA compatibility.

The current package remains the local, unreleased `0.1.0-dev` artifact. This
verification does not assign a formal release version, create a NuGet publishing
channel, or establish .NET 11 RC/GA compatibility. Candidate builds must be made
from their committed version/source; release-specific SDK/runtime validation
and actual registry publication remain separate operations.

### Exact toolchain verification for release preparation

`scripts/verify_release_candidate.py` runs the same fresh, isolated package
consumption with two additional required arguments: `--expected-sdk` and
`--expected-runtime`. Both must be explicit three-part stable versions, without
prerelease/build suffixes, wildcards or defaults. The selected SDK and actual
managed consumer runtime must match them exactly, including the patch number.
Both numeric runtime version and full `FrameworkDescription` must match, so an
RC runtime cannot pass by reporting the same numeric `Environment.Version`.
Missing or invalid expectations fail before consumption; a mismatch produces no
successful report. The original `verify_first_use.py` dev behavior is unchanged.

For example, **only to exercise this entrypoint with the existing .NET 10
toolchain and local dev package**, after the clean pack/publish recipe above:

```bash
python3 scripts/verify_release_candidate.py \
  --cli-dll artifacts/first-use-cli/FactsPDF.Cli.dll \
  --dotnet "$(command -v dotnet)" --output artifacts/exact-toolchain \
  --expected-sdk 10.0.100 --expected-runtime 10.0.0
```

For an actual release candidate, obtain the expected versions from the confirmed
official GA toolchain requirements; do not infer them from the artifact, current
date or the observed `dotnet --version` output. No .NET 11 versions are defaulted
or guessed here. Existing clean-source, declared package identity, loaded
assembly SHA/version, byte checks and real PDF/error cases still apply. The
report records `toolchainConstraint` alongside the observed SDK/runtime and
package/source identity and retains `published=false`. This additional constraint
does not turn a dev package or synthetic fixture into a formal candidate, prove
official GA availability, cover Native AOT/WASM, or create a publication receipt.

### Explicit consumer target for compatibility preparation

Both first-use entrypoints accept `--consumer-tfm net11.0` to target only the
temporary external consumer at .NET 11 while verifying the existing `net10.0`
package. Omitting the option keeps the consumer on the library's declared TFM.
The package version, assets, source SHA and cached/loaded DLL bytes still have
to match the trusted repository contract; the option does not retarget the
library or alter the checked-in example.

The verifier checks the evaluated consumer target, preserves its generated
`consumer.runtimeconfig.json`, and checks that its TFM and Microsoft.NETCore.App
request agree with the actual consumer's target, numeric runtime version and
`FrameworkDescription`. The actual runtime must be in the selected target's
family and at least the requested patch; a newer SDK alone is insufficient.
Strict release verification additionally retains its exact stable SDK/runtime
requirements. Reports distinguish `package.targetFramework` from
`consumerTargetFramework` and retain the runtimeconfig bytes/hash and raw process
outputs. Synthetic .NET 11 fixtures are verifier tests, not RC execution evidence.

For a real .NET 11 run, a suitable official toolchain must already be installed
and selectable under the repository's `global.json`, which is copied into the
isolated workspace. This option neither downloads an SDK nor overrides SDK
selection or roll-forward policy. The staged CLI remains on its original target
and also needs its runtime installed. The current SDK 10 configuration can
exercise the new option with `--consumer-tfm net10.0`; it cannot establish .NET 11
compatibility. Native AOT and WASM remain separate acceptance paths.

For an installed compatibility toolchain that must be selected independently of
the repository SDK, the dev `verify_first_use.py` entrypoint additionally accepts
the paired `--probe-sdk` and `--probe-runtime` arguments. They require exact
versions (stable or prerelease), never floating values. Only the temporary
consumer receives a `global.json` with SDK roll-forward disabled and a pinned
`RuntimeFrameworkVersion` with runtime roll-forward disabled. Repository files
and the default selection policy are unchanged. The actual selected SDK, generated
runtimeconfig, numeric runtime and full `FrameworkDescription` must all match;
an RC suffix cannot be replaced by a stable label or another RC build.

The following exercises this path with **the existing stable .NET 10 toolchain**
and the already packed dev artifact; it is not a .NET 11 RC result:

```bash
python3 scripts/verify_first_use.py \
  --cli-dll artifacts/first-use-cli/FactsPDF.Cli.dll \
  --dotnet "$(command -v dotnet)" --output artifacts/exact-probe \
  --consumer-tfm net10.0 --probe-sdk 10.0.100 --probe-runtime 10.0.0
```

This records `toolchainConstraint.mode=exact-probe`, both generated configuration
files, actual managed execution and the existing package/source identity. It
does not prove that a supplied version is official or GA, perform a download, or
establish Native AOT/WASM compatibility. Dynamic-code capability flags are not a
JIT trace. The strict release entrypoint rejects these probe arguments and keeps
its stable-only expectations. RC-shaped unit fixtures are only verifier tests;
real RC evidence requires the actual installed official toolchain and successful
consumer execution. A missing SDK/runtime fails without a successful report.

## Library API

After restoring the local package as above, the existing API is:

```csharp
using FactsPDF;
using var output = new MemoryStream();
var result = PdfConverter.Convert(
    "<style>p{color:#008000}</style><h1>Example</h1><p>Hello PDF</p>",
    output, new PdfOptions { Margin = 36 });
File.WriteAllBytes("example.pdf", output.ToArray());
```

This example uses fontless ASCII mode. Explicit font API examples are in the Unicode guide. `Convert` leaves the caller's stream open and does not require a seekable output. It returns page/byte counts. Unsupported input causes no output writes because conversion buffers a bounded PDF before final copying; final-copy I/O failures or cancellation may leave arbitrary output streams partly written. The example opens the destination only after conversion succeeds; use your own temporary-file policy or the CLI if final file replacement must also be protected.

## Current content and layout scope

| Area | Supported | Not yet supported |
| --- | --- | --- |
| Content | p, h1-h6, div, section, article, span, b/strong, i/em, br; bounded ol/ul/li; explicit standalone JPEG img; explicit-column tables with whole-row pagination/repeated headers | Links, spanning/nested tables, inline/list/cell images, PNG, SVG, forms; general list styles/counters |
| Document shell | html/body, explicit head/title, UTF-8 meta, HTML5 doctype, comments, embedded style | Full HTML5 tree construction, encoding sniffing and browser recovery |
| Text | ASCII/Courier fallback; explicit static TrueType fonts, simple horizontal Unicode/Chinese, searchable text and optional glyph subsetting | Complete shaping, bidi/RTL, all-script typography, automatic font discovery |
| Styles | Inline and embedded declarations; tag/class/id/universal/compound/group/descendant/child selectors, bounded author cascade | External stylesheets, at-rules, other selector classes, variables/functions and full CSS recovery |
| Text layout | Supported font size/weight/style/color/line-height/alignment; paragraph margins; nested block widths, four-sided padding, solid border edges and background colors | Arbitrary CSS height, horizontal margins, border-radius, floats/flex/grid and general image/table layout |
| Pagination | Wrapping; paragraph/heading break-before/break-after auto/page; opt-in [heading + first paragraph](heading-pagination.md) via break-after:avoid; opt-in [decimal page numbers](page-numbers.md) and [static page headers/footers](page-margins.md); CSS sliced block decorations across page fragments (background and side borders continue, top/bottom only first/last) | @page, arbitrary header/footer templates, widows/orphans, general keep-together and full browser fragmentation rules |
| PDF | PDF 1.7 text/page tree/xref, embedded and compressed font resources, ToUnicode, explicit JPEG XObjects | Tagged PDF, PDF/A/UA conformance, links/bookmarks, signatures/encryption |

CSS property/value and applicability rules are in the stylesheet guide. Unsupported inputs fail explicitly rather than silently rendering wrong output. Headings use larger text by default, not synthetic bold. Normal whitespace is collapsed; font-aware CJK wrapping has a limited punctuation rule, not full UAX #14 support. A segment wider than the line is rejected rather than clipped or arbitrarily split.

Names are ASCII case-insensitive in the supported HTML grammar. Quoted/unquoted attributes and selected omitted p/body/html end tags are supported. XML self-closing non-void tags and unsupported nesting are errors. Class/id attributes now participate in selectors; lang/title/data-* do not execute code. Title remains non-rendering metadata. Character references require a supported name/number and semicolon: amp, lt, gt, quot, apos, nbsp and valid numeric scalars. Without explicit fonts, non-ASCII output is rejected rather than replaced with question marks.

The core does not execute scripts, dynamically compile input, fetch network resources or discover fonts/images implicitly. Font loading is structurally checked but not a complete sanitizer for hostile fonts. This parser is not a general HTML sanitization API.

## Resource limits and diagnostics

Base defaults: 1,000,000 UTF-16 input units; 50,000 elements; depth 128; 1,000 pages; PDF output 16,777,216 bytes; 200,000 display commands; A4 595.28 x 841.89 points and a 36pt margin. Font and CSS limits are documented in their respective guides. These constrain data/work/output, not a guaranteed process-memory ceiling. Fonts, paragraph data, pages, CSS token/declaration structures and output buffers allocate memory; minimum allocation/constant-memory behavior is not claimed.

| Codes | Meaning |
| --- | --- |
| FPDF1001-1003 | Input/element/depth limit |
| FPDF1101-1104 | HTML structure/element/attribute/reference error |
| FPDF1201-1205 | CSS property/value/syntax/source/budget error |
| FPDF1301-1305 | Text/font availability, layout/page limit, invalid UTF-16 or unsupported text processing |
| FPDF1401 | Output-byte or display-command limit |
| FPDF1501-1507 | Font format, resource, mapping, embedding-permission, subsetting or missing explicit style chain |
| FPDF1601-1604 | Image format/support, resource limits or missing explicit binding |

Not every integer in a displayed range needs a distinct public API guarantee. Refer to exact guide tables and exception messages. Offsets are supplied where available, with inline CSS anchored to its HTML element; these are development contracts, not complete browser error classification.

## Native AOT and verification

Linux x64 (requires native compiler and platform dependencies):

```sh
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/native
artifacts/native/FactsPDF.Cli examples/first-document.html artifacts/first-document.pdf
```

Windows x64 requires the .NET SDK and [Visual Studio 2022 or later with the Desktop development with C++ workload](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#prerequisites). In PowerShell, from the repository root:

```powershell
dotnet publish src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release -r win-x64 -p:PublishAot=true -o artifacts/windows-native
& ./artifacts/windows-native/FactsPDF.Cli.exe examples/first-document.html artifacts/windows-sample.pdf
```

The separate `windows-native-aot` CI job builds that executable on `windows-latest`. [Its verifier](../scripts/verify_windows_aot.py) checks AMD64 PE32+ metadata, copies only `FactsPDF.Cli.exe` into an otherwise empty temporary directory and executes it directly, without a `dotnet` launcher or adjacent managed assemblies. The actual AOT publish plus successful isolated execution establish the evidence; a PE signature alone does not distinguish Native AOT from a managed apphost.

Independent Windows PDF inspection uses `pdfinfo`, `pdftotext` and `pdftoppm` from the [official MSYS2 UCRT64 Poppler package](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-poppler). CI pins the [MSYS2 setup Action](https://github.com/msys2/setup-msys2) to a full commit and installs through normal signed `pacman` repositories. These tools are verification dependencies only. To run the same check locally on Windows, install that package in your existing trusted MSYS2 installation, put its `ucrt64\bin` directory on the current process PATH, and use a fresh output directory:

```powershell
python -m unittest discover -s scripts -p test_windows_aot.py -v
python scripts/verify_windows_aot.py --executable artifacts/windows-native/FactsPDF.Cli.exe --output artifacts/windows-aot-evidence
```

The check requires a real Windows x64 host. It inspects both pages of the existing ASCII sample through Poppler, verifies repeat bytes, rejects an actual reordered PDF, and tests the exact unsupported-script diagnostic with exit 3, empty stdout and preservation of existing/absent targets. Successful conversion after failure must replace the preserved sentinel with the original successful PDF bytes. The job retains publish/tool versions, command logs, hashes, PDFs and page previews, and emits a small result summary without embedding PDF bytes. A Linux test run is not Windows execution evidence; the corresponding Windows CI job must pass on the commit being evaluated.

These AOT binaries still use platform libraries. The Windows acceptance covers this ASCII/diagnostic slice, not every font/resource combination or Windows architecture. Windows/Linux/macOS also run managed tests; this does not validate their entire AOT matrix. The separate [browser WASM probe](browser-wasm-probe.md) has protected-browser execution evidence for the shared engine; it is an experimental probe, not a published WASM SDK. The [Node/CLI probe](node-cli-probe.md) similarly validates subprocess integration. Other language bindings remain planned.

CI independently inspects real output using qpdf/Poppler and, for subset inspection, fontTools. Those are verification-only tools, not runtime parser/font/PDF dependencies. The stylesheet workflow compares a bilingual stylesheet template against a hand-authored inline equivalent, including exact bytes/text/pixels. Fonts are not committed or uploaded as standalone files. Local NuGet validation checks exact license/README content and no runtime package dependencies, then actually restores/builds/runs an independent package consumer outside repository build inheritance. The first-use check above records package and runtime provenance. No registry publication occurs and all licensing release gates remain.

Historical execution evidence is retained in the first-renderer, Unicode, subsetting and CSS ledgers. A green run for one commit is not proof for later changes. Performance observations identify their exact source/corpus/environment; no universal throughput, cold-start or peak-memory guarantee is created by these documents.

Explicit font-weight/font-style selection and independent four-role chains are documented in the [font style guide](font-styles.md).

Decimal/disc lists, two-level nesting, marker gutters and list pagination limits are documented in the [list guide](lists.md).
