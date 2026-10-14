# B2: browser WASM compatibility probe

This private probe compiles the existing FactsPDF core for a real browser WASM host. It is not a public browser SDK or an AOT release. The core and its public API are unchanged. Successful compilation alone does not prove browser conversion works.

The 2026-10-14 local investigation successfully published the host with .NET SDK 10.0.100 and the official wasm-tools workload/runtime 10.0.0. The same adapter passed five managed tests within the 520-test solution suite. Ten Node tests cover resource loading, deadlines and the static server. System Chromium and official Playwright Chromium both failed to start with sandboxing enabled in the local cloud environment. An independent Node-hosted diagnostic of the published WASM matched all three CLI PDFs and core error codes, but does not verify browser execution. Actual browser generation and measurements therefore remain pending until a protected run succeeds; do not merge or mark B2 accepted on build results alone.

## Build and resource contract

`probes/browser-wasm` references the existing core and calls `PdfFont.LoadTrueType` and `PdfConverter.Convert`. `[JSExport]` exposes a test-only method accepting HTML, two ordered font byte arrays, and the existing subset flag. Empty arrays select the existing no-font ASCII/Courier mode. The response contains base64 PDF bytes, page count, or the original `FactsPdfException` code/message. No prior PDF is retained. Source-generated JSON metadata works with trimming.

The browser host loads explicit HTML/font URLs with HTTP status checks and fatal UTF-8 decoding. Missing or empty selected fonts fail before conversion. The two-font limit belongs to this small probe, not the library. Fonts are never discovered, packaged in the repository, or fetched implicitly. The test server serves an explicit map of static bytes on loopback and rejects non-GET/HEAD requests; it cannot generate a PDF.

The project targets `browser-wasm`, uses `WasmBuildNative=false`, and rejects AOT/multithreaded overrides. It uses the SDK's default non-AOT execution mode and bundled runtime; default runtime optimizations such as the jiterpreter are not disabled or described as application AOT. Under SDK 10.0.100, explicitly assigning `RunAOTCompilation=false` triggers the SDK relinking-property check against an empty default, so the property is left unset and a build guard rejects `true`. The report preserves effective properties instead of substituting assumed values for empty ones. The default SIMD setting is also recorded.

## Reproduce

Prerequisites: .NET 10 SDK, Node 22+, official Playwright Chromium capable of sandboxed execution, qpdf, Poppler (`pdftotext`, `pdfinfo`, `pdftoppm`), and explicit static TrueType fonts covering the fixtures. The CI workflow installs official tooling and distro verification packages. Local setup must not require system-security changes.

From the repository root:

```sh
dotnet workload install wasm-tools --skip-manifest-update
npm ci --prefix scripts/wasm_probe --ignore-scripts --no-audit --no-fund
cd scripts/wasm_probe
npx playwright install chromium
cd ../..
dotnet test FactsPDF.slnx -c Release
npm test --prefix scripts/wasm_probe
dotnet publish probes/browser-wasm/FactsPDF.WasmProbe.csproj -c Release -o artifacts/wasm
dotnet build src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release
dotnet msbuild probes/browser-wasm/FactsPDF.WasmProbe.csproj -p:Configuration=Release -getProperty:NETCoreSdkVersion,RuntimeFrameworkVersion,TargetFramework,RuntimeIdentifier,RunAOTCompilation,WasmBuildNative,WasmEnableThreads,PublishTrimmed,WasmEnableSIMD > artifacts/wasm-build-properties.json
```

Use a fresh publish directory for an independently measured build to avoid counting files left by earlier publishes. Record `dotnet --info` and `dotnet workload list` alongside the JSON property report.

Set the following environment variables to actual absolute paths:

| Variable | Value |
| --- | --- |
| `FACTSPDF_WASM_ROOT` | `artifacts/wasm/wwwroot` |
| `FACTSPDF_WASM_BUILD_INFO` | `artifacts/wasm-build-properties.json` |
| `FACTSPDF_PROBE_OUTPUT` | An output directory for this run |
| `FACTSPDF_CLI` | `dotnet` executable, or a built CLI executable |
| `FACTSPDF_CLI_DLL` | CLI DLL for managed invocation; omit for a direct executable |
| `FACTSPDF_PROBE_FONTS` | JSON array of DejaVu Sans and Droid Sans Fallback paths, in that order |

Optional `FACTSPDF_QPDF`, `FACTSPDF_PDFTOTEXT`, `FACTSPDF_PDFINFO`, and `FACTSPDF_PDFTOPPM` select verification executables. Standard names otherwise resolve on PATH. Run `node scripts/wasm_probe/run.mjs`. The process exits nonzero on startup, conversion, or comparison failure and writes `report.json` once the run has passed file/configuration preflight.

## Browser evidence and measurements

The runner explicitly sets `chromiumSandbox:true`, removes Playwright's default self-XSS-warning suppression and unsafe SwiftShader option, and checks the actual browser command line. On Linux it also requires observed renderer processes with seccomp filtering (`2`) and `NoNewPrivs=1`. It never retries without sandboxing or changes host security. Browser print APIs are never called. Requests are restricted to the local static origin; the .NET export must confirm `OperatingSystem.IsBrowser()` and the request log must include real WASM assets.

ASCII, explicit-font bilingual, and wrapped/two-page block-layout fixtures use identical bytes/options in WASM and CLI. Each successful pair must have identical PDF bytes, pass qpdf, contain the exact expected normalized text, have the expected page count, and match Poppler text bounding boxes and rendered page bytes. Missing/empty/corrupt fonts, invalid UTF-8, absent glyph coverage and unsupported HTML are explicit failures with no PDF; a final ASCII conversion must recover after those failures.

The report records source revision/dirty status, build properties and asset hashes, input/font/output hashes, browser version/arguments and renderer protection, request/resource entries, and case results. Size fields distinguish raw build assets from actual observed HTTP transfer/body sizes. Compressed build sidecars are excluded from the raw uncompressed-asset total; the simple probe server serves uncompressed bodies with caching disabled. This is not a CDN or production bundle-size benchmark.

There is one initial conversion and three repetitions per fixture in a single runtime. Only the first ASCII call is globally cold; later fixtures already share a warmed runtime. Font files are fetched and parsed each time. `resourceLoadMs` covers explicit input/font fetching and UTF-8 decoding. `convertMs` covers interop, font parsing, conversion, base64/JSON return and JS parsing. Runtime-ready timings include importing/loading the runtime and binding its exports. Raw samples are retained without declaring a performance budget.

Managed heap snapshots use `GC.GetTotalMemory(false)` immediately before font parsing and after PDF/base64 creation (before JSON serialization). They are not allocated-byte totals, JS heap, WASM linear-memory capacity, RSS, or a peak. Unsupported measurements are explicitly unavailable. This probe is single-threaded and synchronous during conversion; it does not establish worker responsiveness, cancellation, mobile viability, additional browsers, production security against untrusted fonts, or WASM AOT compatibility.

The read-only, pinned-action `FactsPDF browser WASM probe` workflow runs the same contract and retains JSON evidence even when browser startup fails. Conversion calls have a 30-second deadline; inspectors and browser cleanup are also bounded, and the report is saved before cleanup. These are harness deadlines, not performance budgets or cooperative cancellation. CI evidence and local evidence must be reported separately. A failed protected launch remains a failed run, never a skipped passing test.

Current evidence limitations: browser resource timing entries are captured after successful comparisons, so earlier failures retain the server request log but not those timing entries. CI retains JSON/text reports, not the generated PDFs/rasters. The server request log's `bytes` field denotes selected asset length (also for HEAD), not measured body transfer; the successful browser resource entries provide transfer/body sizes.
