# Explicit font styles

FactsPDF selects normal, bold, italic and bold-italic from four independent, explicitly supplied fallback chains. Static fonts can contain any of these styles. The renderer uses their actual outlines and advances; it does not synthesize weight or slant, search installed fonts, match families or silently downgrade a missing style.

```csharp
var options = new PdfOptions {
    Fonts = [latinRegular, chineseRegular],       // existing normal chain
    BoldFonts = [latinBold, chineseBold],
    ItalicFonts = [latinItalic, chineseItalic],
    BoldItalicFonts = [latinBoldItalic, chineseBoldItalic],
    SubsetFonts = true
};
using var output = new MemoryStream();
PdfConverter.Convert("<p>Normal <strong>粗体</strong> <em>Italic</em> <b><i>粗斜体</i></b></p>", output, options);
```

Each value above is an immutable `PdfFont` loaded with the existing `PdfFont.LoadTrueType` API. A chain uses its first font containing each scalar. Families, chain lengths and naming need not match across roles. Every chain is optional until its role is used. For example, an italic-only chain can render `<p>Courier ASCII <i>中文</i></p>` with the original fontless normal mode. Normal non-ASCII still needs `Fonts`; it never borrows another role's fonts.

Explicit role declarations are authoritative. A static italic font passed through the existing `Fonts` property keeps working as before. Putting a regular font in `ItalicFonts` does not create italic outlines or cause metadata-based matching. A supported, licensed system font **file** can be passed explicitly using the same API or CLI; there is no automatic system fallback.

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

- `b` / `strong` default to absolute bold; `i` / `em` default to italic. Nesting selects bold-italic. Repeated tags do not add extra weight/slant.
- `font-weight`: `normal`, `bold`, integer hundred-step `100`, `200`, ... `900` (including equivalent CSS number tokens), `inherit`, `initial`, `unset`. Values `100`–`500` select the explicitly supplied **normal** role, while `600`–`900` select the explicitly supplied **bold** role. This selects a real font chain; it does not synthesize intermediate weights or interpolate variable-font axes.
- `font-style`: `normal`, `italic`, `inherit`, `initial`, `unset`.
- Both properties inherit independently and use the existing cascade and `!important`. Author CSS can override a tag's default. Headings retain the previous font-weight default.
- `oblique`, relative `bolder`/`lighter`, non-hundred or out-of-range numeric weights, font-family matching, `font` shorthand, `@font-face`, variable fonts, shaping and font synthesis remain unsupported.

Style boundaries do not add whitespace or word breaks. Paragraph struts and inline line metrics use the first font of the selected role chain, preserving the previous fallback metric model. Empty inline elements do not require resources. The existing break-only inline boundary remains: `<p><i><br></i></p>` does not contribute an italic metric, but `<p style="font-style:italic"><br></p>` creates an italic paragraph strut and requires that chain. Anonymous paragraphs retain the first inline style as their paragraph style.

## Failure behavior

`FPDF1507` means a requested styled chain is missing. `FPDF1504` means the chosen role chain contains no glyph; styled diagnostics name the role and scalar and report the available element offset. Original normal diagnostics stay compatible. Missing roles never use another role, Courier, synthetic styling or an undocumented warning/downgrade path.

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
