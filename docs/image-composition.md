# Image, list and font composition checks

The C1/C2/D1 combination checks add two actual documents to the existing individual-feature evidence:

- [Automatic image movement](../examples/image-list-composition.html): 24 ordered records, an image that moves intact to page 2, a restarted ordered list with a nested unordered item, and normal/bold/italic/bold-italic text inside a sliced bordered section.
- [Image-only middle page](../examples/image-only-middle-page.html): explicit transitions around a page containing only the image; searchable text and list markers resume on page 3 with different font roles.

Run from the repository root after building the managed CLI. The manifest is produced by the existing `scripts/font_style_fonts.py` verification-font preparation; it supplies the four DejaVu/Sarasa chains and their fingerprints. The JPEG is the existing authored `tests/fixtures/jpeg/rgb-420.jpg`.

```sh
python scripts/verify_image_composition.py \
  --manifest artifacts/wasm-evidence/font-manifest.json \
  --output artifacts/composition \
  --dotnet dotnet \
  --cli-dll src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll \
  --native-cli artifacts/style-native/FactsPDF.Cli
```

`--native-cli` is optional for local managed-only inspection. Normal protected CI supplies its freshly published Linux Native AOT executable. The inspector verifies all page counts, image-page resources, original DCT bytes, independently authored 400×200pt image matrices, ordered record IDs, restarted counters, actual PDF font resources for the four roles, Poppler text bounds, qpdf integrity and raster orientation/colors. Reordered real PDF pages and removal of the final-page ordered markers must fail inspection. Font bytes are explicitly supplied and are not distributed.

Nine new `ImageCompositionTests` cover exact page-edge fit, automatic image movement, image-only pages, adjacent/nested lists, inherited font roles, continued ancestor decoration, exact display/output budgets and unchanged destinations on failure. These cases passed against the already merged implementation; no defect was demonstrated and no engine behavior was changed.

First local outputs contain 2 and 3 pages respectively. All five complete page rasters were opened and inspected: records and markers are intact, the chart is upright, surrounding text does not overlap it, and the section has the expected sliced side/bottom borders. User approval as a page reference remains pending. These checks do not complete the full-report target, tables, page furniture, Windows AOT or release gates.

The next approved roadmap work package is E (tables). Its concrete [E1 design and fixture proposal](superpowers/specs/2026-10-18-fixed-column-table-e1-design.md) remains a proposal, with a single public syntax decision to review before engine implementation.
