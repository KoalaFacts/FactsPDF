# FactsPDF private managed CLI — 0.1.0-preview.1

This software is an **unverified, privately staged evaluation component**, not
an authorized distribution, a public NuGet release or a production-ready API.
Review the repository's unchanged LICENSE.md and licensing requirements before
building or running it. The package contains no fonts or customer assets.

Requires Microsoft .NET runtime **10.0.13** on a compatible host (the evaluation
consumer is pinned to runtime 10.0.13 with roll-forward disabled). From the
extracted `FactsPDF.Cli` directory, run:

```sh
dotnet FactsPDF.Cli.dll /absolute/path/report.html /absolute/path/report.pdf
```

An existing output file requires `--overwrite`. Failures return a nonzero
exit code and preserve existing output. Run `dotnet FactsPDF.Cli.dll --help`
for the supported bounded HTML/CSS options. JPEG/PNG resources must be bound
explicitly with `--image name=path`. Chinese text, bold/italic and subsetting
need appropriately licensed caller-provided TrueType fonts, supplied with
`--font`, `--font-bold`, `--font-italic`, `--font-bold-italic` and optional
`--subset-fonts`. The CLI does not discover fonts or fetch remote resources.

For the authoritative supported subset, see README.md and docs/evaluation.md
in the exact source revision. This ZIP alone is **not** the final five-payload
private candidate; independent extracted artifact/consumer acceptance remains
required. Never claim customer delivery or public release based on this ZIP.
