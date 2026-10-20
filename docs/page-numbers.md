# Decimal page numbers (F2)

Page numbers are opt-in. The default output is unchanged.

```csharp
PdfConverter.Convert(html, output, new PdfOptions { PageNumbers = true });
```

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html numbered.pdf --page-numbers
```

The CLI flag can appear once and works with files or stdin/stdout. It uses the same core and existing atomic file replacement. No CSS rule or custom HTML attribute is needed.

Every actual output page receives a searchable decimal number beginning at 1, including the existing single page for empty content. Trailing breaks do not create footer-only pages. Numbers are centered horizontally by advance width and vertically by their occupied extent within the existing bottom margin. Adding numbers does not change body geometry, page count, table header repetition or heading keeps.

Numbers use fixed normal black 10pt text with line-height 1.2, independent of body CSS and `PdfOptions.FontSize`. The existing normal `Fonts` fallback chain supplies the digits; an empty chain retains Courier. Bold/italic chains do not substitute for missing normal glyphs. Missing digits fail `FPDF1504`; for example, a font lacking `0` can number nine pages but fails on page ten. Existing full embedding/subsetting behavior applies, including digits introduced by the footer.

The compositor conservatively measures the line box and declared global bounding boxes of the fonts actually drawing each digit, at each digit's advance origin. It requires 2pt clearance from the body content area and physical page edges. It also checks existing body text bounds, discs, images and painted boxes, since small body line heights may allow ink to extend beyond line boxes. Potential overlap or insufficient margin fails `FPDF1302`; the compositor does not shrink fonts, move body content or add a page. Large declared font bounds may conservatively reject a document even when individual glyphs would fit. Fonts remain trusted resources, not fully sanitized outlines.

The fontless 12pt line box needs at least a 16pt bottom margin. Explicit fonts can need more. Margin zero still works when numbering is disabled. PDF coordinates round at the existing serializer precision (up to approximately 0.001pt for ASCII text/paint and 0.000001pt for Unicode text); independent checks account for this rounding.

Actual page-number text runs count toward `MaxDisplayCommands`, including runs split across fallback fonts. PDF bytes count toward `MaxOutputBytes`; `MaxPages` continues to bound actual body pages. Existing body parsing/layout/font/page/display-budget errors occur before numbering. Page-number errors occur before serialization/output-byte errors. Cancellation and conversion destination guarantees are unchanged: errors before final copying write nothing, while final-copy failures on arbitrary streams cannot be rolled back.

This increment adds no total-page label, custom formatting/start/skip, page-number font/position settings, general page header/footer template, `@page` support or publication capability.

The native inspector `scripts/verify_page_numbers.py` compares API/CLI/Native AOT bytes, numbered vs unnumbered body text/coordinates, every page's number/centering/clearance and mutation rejection. The normal CI runs this corpus with explicit fonts. Local verification also compares default output with the original F1 executable and visually inspects page previews and the complete 100-number footer strip set. These are implementation checks, not a claim of user approval for general report page references.
