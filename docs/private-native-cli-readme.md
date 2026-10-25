# FactsPDF private Native AOT CLI — 0.1.0-preview.1

This is a privately staged, UNVERIFIED software component, not a release,
production approval or authorization to distribute. The unchanged LICENSE.md
governs use. Fonts and customer templates are not included.

The executable does not need a separately installed .NET runtime. Select
the archive matching Linux x64 or Windows x64 and extract all five files
into a fresh directory. Run the executable from the extracted FactsPDF.Native
directory:

Linux: ./FactsPDF.Cli /absolute/report.html /absolute/report.pdf

Windows: .\FactsPDF.Cli.exe C:\report.html C:\report.pdf

Use --overwrite to replace an existing destination. Failed conversion returns
a nonzero exit code and preserves an existing destination. Use --help for limits.
Bind PNG/JPEG images and appropriately licensed TrueType font files explicitly.
For structured page furniture use --page-furniture /absolute/config.json.

The notices/dotnet files are copied from the matching installed .NET
NativeAOT 10.0.13 runtime pack and bound to the private build receipt.
These bytes do not yet prove native compiler signatures or the full toolchain.

This ZIP is one private software component, not an approved five-payload
candidate. Independent extracted-binary, native-pack, resource and recovery
acceptance, human PDF review and release/legal gates are still required.
