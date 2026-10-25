# FactsPDF private first-use examples — 0.1.0-preview.1

This component contains the existing synthetic first-use API example and HTML,
not customer data, fonts, binaries or an accepted delivery candidate. No new
API or licensing rights are granted. See LICENSE.md. Publication and distribution
remain unauthorized until separately approved.

Use the installed .NET SDK 10.0.402 and runtime 10.0.13. Extract to a fresh directory
outside the source checkout. Check EXAMPLES-MANIFEST.json and the accompanying
bundle receipts: the library package must have the same source commit as this
component. Do not silently substitute another package with the same version.

Create `feed/` beside `first-use/` and copy only the matching
`FactsPDF.0.1.0-preview.1.nupkg` from the library component into it. The supplied
NuGet.Config clears remote sources and maps FactsPDF only to that local feed.
From `FactsPDF.Examples`, run:

```sh
dotnet restore first-use/FirstUse.csproj --configfile first-use/NuGet.Config --packages ./packages -p:RuntimeFrameworkVersion=10.0.13 -p:RollForward=Disable
dotnet build first-use/FirstUse.csproj -c Release --no-restore -p:RuntimeFrameworkVersion=10.0.13 -p:RollForward=Disable
dotnet first-use/bin/Release/net10.0/FirstUse.dll first-use/input.html api.pdf
```

Use a fresh package cache and a directory without inherited Directory.Build.props,
Directory.Build.targets or Directory.Packages.props. The application converts before
opening the destination; `unsupported.html` exercises its existing failure path.
The API example permits overwriting an output; its final file write does not
provide the managed CLI atomic replacement guarantee.
`expected-pages.json` records the existing authored page text. This example alone
does not validate every PDF feature or establish full candidate acceptance.

For a CLI comparison, use the separately extracted managed CLI component:

```sh
dotnet /absolute/path/FactsPDF.Cli/FactsPDF.Cli.dll first-use/input.html cli.pdf
```

Compare `api.pdf` and `cli.pdf` bytes. No remote service or additional font is needed
for this synthetic example. Full corpus, font/reference approval and final bundle
installation verification remain separate gates.
