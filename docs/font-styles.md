# Explicit font styles

FactsPDF selects normal, bold, italic and bold-italic from four independent, explicitly supplied fallback chains, and CSS `font-family` from families the caller registers or a stylesheet declares with `@font-face`. Static fonts can contain any of these styles. The renderer uses their actual outlines and advances; it does not synthesize weight or slant, search installed fonts or silently downgrade a missing style.

```csharp
var pdf = Pdf.Write().WithFonts(f => f
    .Regular(latinRegular, chineseRegular)
    .Bold(latinBold, chineseBold)
    .Italic(latinItalic, chineseItalic)
    .BoldItalic(latinBoldItalic, chineseBoldItalic)
    .Subset());
byte[] bytes = pdf.ToBytes("<p>Normal <strong>粗体</strong> <em>Italic</em> <b><i>粗斜体</i></b></p>");
```

Each value above is an immutable `PdfFont` loaded with `PdfFont.FromFile`, `FromBytes` or `FromStream`. A chain uses its first font containing each scalar. Families, chain lengths and naming need not match across roles. Every chain is optional until its role is used. For example, an italic-only chain can render `<p>Courier ASCII <i>中文</i></p>` with the original fontless normal mode. Normal non-ASCII still needs `Fonts`; it never borrows another role's fonts.

Explicit role declarations are authoritative. A static italic font passed in the `Regular` chain keeps working as before. Putting a regular font in the `Italic` chain does not create italic outlines or cause metadata-based matching. A supported, licensed system font **file** can be passed explicitly using the same API or CLI; there is no automatic system fallback.

## CLI

```sh
FactsPDF.Cli examples/font-styles.html report.pdf --subset-fonts \
  --font Latin-Regular.ttf --font Chinese-Regular.ttf \
  --font-bold Latin-Bold.ttf --font-bold Chinese-Bold.ttf \
  --font-italic Latin-Italic.ttf --font-italic Chinese-Italic.ttf \
  --font-bold-italic Latin-BoldItalic.ttf --font-bold-italic Chinese-BoldItalic.ttf
```

Options may be interleaved; each chain retains its own declaration order. The combined limit is eight declared entries, including duplicates. Unique font objects across all .NET chains share the existing 64 MiB default total limit; loading a single font defaults to 32 MiB. The CLI loads each declared path separately. Every role path is protected from being used as an output path, even with `--overwrite`.

Full embedding remains the default. Four complete CJK fonts can exceed the existing 16 MiB PDF output limit even when their input bytes fit the font budget. The verification fixture does so and reports `FPDF1401`; its bilingual examples use `--subset-fonts`. Do not confuse input limits, output limits and process-memory usage.

## HTML and CSS

- `b` / `strong` default to bold; `i` / `em` default to italic. Nesting selects bold-italic. Repeated tags do not add extra weight/slant.
- `font-weight`: `normal`, `bold`, `bolder`, `lighter` and any number from 1 to 1000 (including equivalent CSS number tokens), `inherit`, `initial`, `unset`. `bolder`/`lighter` follow the CSS Fonts 4 table from the inherited weight. With the four default chains, weights above 500 select the **bold** role and the rest the **normal** role (CSS matching against faces of weight 400 and 700); a registered or `@font-face` family uses the full matching order below.
- `font-style`: `normal`, `italic`, `oblique` with an optional angle from 0deg to 90deg (`oblique 0deg` is upright), `inherit`, `initial`, `unset`. Italic and oblique select the **italic** role.
- `font-stretch`: a keyword or a non-negative percentage; it only matters for families with faces of several widths.
- All of them inherit independently and use the existing cascade and `!important`. Author CSS can override a tag's default. Headings and `th` are bold like `b`.
- Variable fonts, shaping, system font keywords in `font` (`caption`, `menu`, ...) and font synthesis remain unsupported.

Style boundaries do not add whitespace or word breaks. Paragraph struts and inline line metrics use the first font of the selected role chain, preserving the previous fallback metric model. Empty inline elements do not require resources. The existing break-only inline boundary remains: `<p><i><br></i></p>` does not contribute an italic metric, but `<p style="font-style:italic"><br></p>` creates an italic paragraph strut and requires that chain. Anonymous paragraphs retain the first inline style as their paragraph style.

## Font families and @font-face

Register faces for CSS `font-family` with `Faces` (each face answers to the family names in its name table) or `Family` (a name you choose), map CSS generic families to them with `Generic`, and bind `@font-face` sources with `WithFontResource`:

```csharp
var pdf = Pdf.Write()
    .WithFonts(f => f
        .Regular(fallbackRegular).Bold(fallbackBold)
        .Faces(sansRegular, sansBold, sansItalic)          // "Report Sans" from the name table
        .Family("Ledger Mono", monoRegular, monoBold)
        .Generic("sans-serif", "Report Sans")
        .Generic("monospace", "Ledger Mono"))
    .WithFontResource("fonts/brand.woff2", PdfFont.FromFile("brand.woff2"));
```

CLI: `--font-face path`, `--font-family name=path`, `--font-generic generic=family` and `--font-resource url=path` (at most 32 such fonts).

- **Font files.** `PdfFont.FromFile`, `FromBytes` and `FromStream` read plain TrueType-outline sfnt files, WOFF 1.0, WOFF 2.0 (Brotli, including the transformed glyf/loca and hmtx tables) and the first face of a TrueType collection; `PdfFont.FromCollection(bytes, index)` reads another face and `PdfFont.CountFaces` counts them. The decoded sfnt is what is embedded (`FontFile2`) and what counts against the byte limits. CFF-outline OpenType (`OTTO`), CFF2, variable (`fvar`/`gvar`) and color fonts fail `FPDF1502`; embedding-restricted fonts still fail `FPDF1505`.
- **Matching.** Each `font-family` entry names an `@font-face` family (which hides a registered family of the same name, as it hides an installed one), else a registered family, ASCII case-insensitively, quoted or not. Within the family the CSS Fonts 4 algorithm narrows the faces by `font-stretch`, then `font-style` (italic prefers italic, then oblique, then upright faces), then `font-weight` (400–500 look up to 500 first, lighter weights downward, heavier weights upward), using each registered face's OS/2 weight, width and italic/oblique flags or each `@font-face` rule's descriptor ranges. Unknown names are skipped. A generic family (`serif`, `sans-serif`, `monospace`, `cursive`, `fantasy`, `system-ui`, `math`) uses its mapped family; an unmapped one ends the list, as a browser always resolves a generic family. The default role chains come last in every list and draw the characters the matched faces lack.
- **No synthesis.** Where Chrome would embolden (a request of 600 or more against a face under 600; for registered faces also a request more than 200 above the face) or slant (italic or oblique against an upright face), FactsPDF fails `FPDF1507` naming the family, so a family needs real bold and italic faces wherever the page asks for them, exactly as the default chains do.
- **`@font-face`.** `font-family` and `src` are required. `src` entries are tried in order: `url()` names a font resource by its exact text (`WithFontResource`) or is a base64 `data:` URL decoded in place; `local()` matches a supplied font's full or PostScript name; an entry whose `format()` Chrome cannot load (for example `embedded-opentype`) is skipped. A face loads only when text uses it; if its URLs name no bound resource the conversion fails `FPDF1508` at the `url()`. `font-weight` (one value or a range), `font-style` (`normal`, `italic`, `oblique [angle{1,2}]`) and `font-stretch` give the matching ranges; `font-display` is accepted and changes nothing in print. `unicode-range`, `size-adjust` other than 100%, metric overrides, feature and variation settings, `font-named-instance` other than `auto`, `auto` ranges, negative oblique angles and `tech()` fail `FPDF1202`. An invalid rule (no `src`, a generic family name, an unknown descriptor) fails `FPDF1204` in strict mode and is dropped with a warning in non-strict mode, as Chrome drops it.
- **Monospace.** The user-agent sheet gives `pre`, `code`, `kbd` and `samp` `font-family:monospace`, as Chrome's does. Unmapped, that falls back to the default chains as before. Mapped, a list that is exactly `monospace` fails `FPDF1202` unless the font size is absolute: Chrome sizes such text from its 13px monospace default, which FactsPDF does not implement. `font-family: monospace, monospace` (normalize.css) or an absolute `font-size` avoids it, as in browsers.
- **Page-margin boxes** draw from the default chains only: a margin-box `font-family` that resolves to registered or `@font-face` fonts, including one inherited from the root element, fails `FPDF1201`.

`FontContainerTests`, `FontFamilyMatchingTests` and `FontFaceRuleTests` pin these rules with synthetic fonts.

## Failure behavior

`FPDF1507` means a requested styled chain is missing, or a matched family face would need synthetic bold or slant. `FPDF1508` means an `@font-face` source names no bound font resource. `FPDF1504` means the chosen chain (matched family faces, then the role chain) contains no glyph; styled diagnostics name the role and scalar and report the available element offset. Original normal diagnostics stay compatible. Missing roles never use another role, Courier, synthetic styling or an undocumented warning/downgrade path.

The four lists are snapshotted; do not modify them concurrently during conversion. Null lists/items or more than eight combined entries are argument errors. Existing embedding restrictions and font formats still apply. Errors before final copying preserve a caller stream's content/position. A final-copy I/O failure or cancellation may leave a prefix on an arbitrary stream. The CLI preserves existing targets through its normal temporary-file replacement policy.

## Evidence and scope

`font_style_fonts.py` verifies pinned Sarasa Gothic SC 1.0.42 static TTF files and explicit DejaVu Latin faces: eight original files, no preprocessing, ordered Latin/CJK fallback. `verify_font_styles.py` independently checks embedded outlines/advances, CID and Unicode mappings, assigned roles for expected phrases, text, bounds, full/subset parity, output protection, direct .NET/CLI parity and optional Linux Native AOT parity. `scripts/wasm_probe/style_fixtures.mjs` defines the same owned inputs and expected text for protected browser WASM.

The local reviewed sample set has a two-page report, five pages of 60 ordered records, three pages of long italic/bold-italic text, Latin full/subset output and Courier mixing. Records can split naturally across pages; keep-together behavior is not added. Agent page inspection is separate from user approval of frozen reference pages. Same-head CI supplies actual protected Chromium execution; local WASM publication alone is not browser acceptance.

The private browser probe retains its old two-font normal export and adds explicit role bytes. Its host errors (`B2_INPUT_FONTS`, `B2_RESOURCE_HTTP`, `B2_RESOURCE_EMPTY`, `B2_RESOURCE_LIMIT`) are distinct from unchanged core error codes. It remains single-threaded non-AOT and is not a public JavaScript SDK. Native AOT claims apply only to the actual tested build/platform. The nine product-target denominator and separate list work are unchanged; this stage does not claim the full Developer Preview is complete.

## Numeric font-weight report example (source only)

The [64-line facilities report](../examples/report-numeric-font-weights.html) uses
`font-weight:300/500/600/800/900` for ordinary labels, IDs, approvals and totals.
The separately authored [normal/bold role oracle](../examples/report-numeric-font-weights-oracle.html)
uses only 400/700; the Native AOT test verifies exact real PDF bytes, two
explicitly provided static TrueType roles, pagination, searchable text, and
FPDF1202/FPDF1507 output protection. For CLI first use, supply both
`--font path-to-regular.ttf` and `--font-bold path-to-bold.ttf`; no font files
are distributed with FactsPDF. Existing 400/700/default outputs are unchanged.
This is a strict two-role mapping, not full CSS Fonts/variable-weight support
or a publicly released installable candidate.
