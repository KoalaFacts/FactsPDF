# Static page headers and footers (F3)

`HeaderText` and `FooterText` are optional literal strings, defaulting to null. They repeat on every actual output page in the existing top and bottom margins. Leaving them disabled preserves default and number-only output.

```csharp
PdfConverter.Convert(html, output, new PdfOptions {
    HeaderText = "Quarterly report",
    FooterText = "Internal",
    PageNumbers = true
});
```

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html report.pdf \
  --header-text "Quarterly report" --footer-text "Internal" --page-numbers
```

The CLI consumes exactly one following argument as literal text, including an empty string, `-` or a value beginning `--`. Each option can appear only once, even if its first value is empty. A missing following argument is a usage error. File/stdin/stdout behavior and atomic destination replacement are unchanged.

Each field is one line. U+0020 ASCII spaces are trimmed and collapsed before glyph resolution; null, empty and all-ASCII-space values disable that field. Other Unicode spaces remain literal. HTML, entities, `{page}` and similar text have no special meaning. Controls (including tabs, CR/LF and C1 controls) and Unicode line/paragraph separators fail `FPDF1305`; invalid UTF-16 fails `FPDF1304`. The whole field is scanned left to right for lexical errors before resolving its glyphs. Existing simple-script/font restrictions still apply.

With `PageNumbers = true`, a nonempty footer becomes `normalized text | N`, where N is the actual decimal page number. Without footer text, F2 page-number behavior is unchanged. The separator is ordinary ASCII text and must exist in the supplied font chain. There are no placeholders, total-page labels, start/skip controls or per-page variations. Empty HTML retains one page; trailing breaks do not create extra pages.

All page text uses fixed normal black 10pt text, line-height 1.2, and the explicit normal `Fonts` fallback chain; an empty chain uses Courier. Body styles, `FontSize` and bold/italic font chains do not change it. Missing explicit glyphs fail `FPDF1504`; fontless non-ASCII text fails `FPDF1301`. Existing full embedding and subsetting apply, including glyphs introduced only by headers or footers.

Text is centered horizontally by advance width and vertically by its occupied extent within each existing margin. It must fit the body content width (`PageWidth - 2 * Margin`) on one line, including the separator and current page number. Wrapping, overwide text, insufficient margins or potential overlap fail `FPDF1302`. Long text is never truncated, shrunk or moved to another page; the transition from 9 to 10 can make a previously fitting footer too wide. Shorten the text or choose suitable document geometry explicitly.

The compositor preserves body pages, positions, heading keeps, table continuation and repeated table headers. It does not reserve additional space by shrinking/reflowing the body. Occupied bounds combine the first normal font's line strut with the declared global bounding boxes of every font actually drawing a glyph, at each glyph origin. It requires 2pt separation from physical edges and the body area, plus 2pt vertical separation from body text bounds, discs, images and painted boxes. A font's large global box or body ink outside a line box may conservatively reject a document even when those particular visible glyphs would fit. Fonts remain trusted resources; their outlines are not fully sanitized. Explicit body spacing can be needed for such fonts. Courier needs at least a 16pt margin; explicit fonts can require more. Existing serializer rounding is respected (approximately 0.001pt for ASCII text/paint and 0.000001pt for Unicode text).

Each raw field is limited to **1024 UTF-16 units**, including discarded spaces (`FPDF1001`). HTML plus both raw fields also counts once toward `MaxInputCharacters`; thus whitespace-only fields preserve output bytes only when these limits pass. Actual repeated text runs count toward `MaxDisplayCommands`, including fallback font changes. `MaxPages` still bounds body pages and `MaxOutputBytes` includes the complete PDF.

Error order is deterministic: existing arguments/cancellation/resource snapshots, raw input lengths, body parsing/layout/page/display checks, then page-major header followed by footer. Each enabled line checks for at least one remaining display command before lexical/glyph/geometry validation; it rejects a second line before appending or charging actual runs. Serialization/output-byte checks follow. Errors before final copying preserve the output stream or file; failures during final copying to an arbitrary stream cannot be rolled back. Cancellation remains cooperative.

This capability adds no arbitrary HTML templates, JavaScript, remote resources, custom styles/positions, multiline furniture, `@page`, new document language, combined-report acceptance or public release. `scripts/verify_page_margins.py` generates actual API/CLI/Native AOT PDFs, checks text/bounds/body preservation independently, rejects altered header/footer documents and checks atomic failures. Generated previews support visual inspection; they do not change pending user approval of general report page references.
