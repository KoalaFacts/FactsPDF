# Tagged PDF

FactsPDF writes **Tagged PDF** by default: a structure tree that follows the HTML elements, marked content that ties
every painted item to it, the document language and title, and (when the document can meet it) the PDF/UA-1
(ISO 14289-1) identification. Chrome's print-to-PDF tags by default too; this contract aligns with its structure types
where Chrome tags and uses the standard PDF 1.7 types where Chrome writes `NonStruct`.

```csharp
using FactsPDF;

var result = Pdf.Write().WithFonts(f => f.Regular(latinRegular).Bold(latinBold)).ToStream(html, Stream.Null); // tagged
Console.WriteLine(result.PdfUa); // true when the PDF identifies itself as PDF/UA-1
var untagged = Pdf.Write().WithTaggedPdf(false).ToBytes(html);                                                // the untagged PDF of earlier releases
```

The CLI tags by default; `--untagged` writes the untagged PDF. An untagged PDF is byte-identical to what earlier
releases wrote for the same input.

## Structure types

| HTML | Structure type | Notes |
| --- | --- | --- |
| `html`, `body` | `Document` | One `Document` holds everything. |
| `div`, `header`, `footer`, `main`, `nav`, `hgroup`, `search`, `address`, `figure` | `Div` | |
| `section`, `article` | `Sect`, `Art` | |
| `aside` | `Aside` | Chrome's type; a PDF 2.0 type, so the `RoleMap` maps it to `Sect` (written only when used). |
| `blockquote` | `BlockQuote` | |
| `p`, `pre` | `P` | |
| `h1`–`h6` | `H1`–`H6` | |
| `ul`, `ol`, `menu`, `dl` | `L` | |
| `li` | `LI` with `Lbl` and `LBody` | The marker is the `Lbl`: a decimal or alphabetic marker is its text, a bullet, circle or square is drawn as a shape and its `Lbl` has the character as `ActualText`. The item's content is the `LBody`. |
| `dt`, `dd` | `LI` with `Lbl` (the term), `LI` with `LBody` (the definition) | PDF has no description list. |
| `table`, `thead`, `tbody`, `tfoot`, `tr` | `Table`, `THead`, `TBody`, `TFoot`, `TR` | Rows directly in a `table` stay `TR` in the `Table`. |
| `th`, `td` | `TH`, `TD` | `TH` has a `Scope` (`Column` in a `thead` or when it is not first in a row of data cells, `Row` when it starts one); `colspan` is `ColSpan`. An empty cell of a row that has content is kept, so the table stays regular. |
| `caption`, `figcaption` | `Caption` | |
| `a` | `Link` | Holds the text and an object reference to the link annotation, which carries `StructParent` and `Contents`. |
| `q` | `Quote` | |
| `code`, `kbd`, `samp` | `Code` | |
| `strong`, `em` | `Strong`, `Em` | Mapped to `Span` in the `RoleMap`, written only when used. |
| `img`, `svg` | `Figure` | `Alt` is the `alt` attribute (an svg: `aria-label`, else its `title`). An `img` with `alt=""` and an svg with `role="presentation"` or `aria-hidden="true"` are decorative: an artifact, no `Figure`. |
| an inline element with `lang` | `Span` with `Lang` | |
| `abbr` with `title` | `Span` with `E` (the expansion) | |
| `span`, `b`, `i`, `u`, `s`, `small`, `br`, `hr` and the other inline elements | none | The content belongs to the enclosing structure element. |

Text directly inside a grouping element (`Div`, `Sect`, a list or table part) goes into an implied `P`. Elements with no
content (empty or hidden) leave no structure element. Kids are in reading order: list markers and images are read in line
order although they are painted before the text; text that continues on the next page continues the same element (a marked
content reference with that page).

## Marked content and artifacts

Every painted item is either tagged content or an artifact, and the two are never nested:

* text, a figure's image or vector paint and a list marker's shape are tagged content with an `MCID`; adjacent runs of one
  element share one marked-content sequence unless an artifact (a text decoration) lies between them;
* backgrounds, borders, outlines, rules, an image's own padding and border, text decorations (underline, strike-through,
  overline) and decorative images are `/Artifact`;
* page furniture (header and footer text, page numbers, page furniture regions, `@page` margin boxes) is
  `/Artifact` with `/Type /Pagination`.

Each page with tagged content has `/StructParents`, and the `ParentTree` maps every `MCID` and every link annotation to its
element. A page with link annotations has `/Tabs /S`.

## Document level

* `/MarkInfo << /Marked true >>`, `/StructTreeRoot` with `ParentTree` and `ParentTreeNextKey`.
* `/Lang` from the `lang` attribute of `html` (a malformed tag is not written); an element with its own valid `lang`
  carries `/Lang`.
* The title is `--title` / `WithMetadata(m => m.Title(...))`, else the text of the `title` element (ASCII whitespace
  collapsed, at most 1,024 UTF-16 units). It is the Info `/Title`, the XMP `dc:title` and, with
  `/ViewerPreferences << /DisplayDocTitle true >>`, what a viewer shows.
* XMP metadata (`dc:title`, and `pdfuaid:part` 1 when the PDF claims PDF/UA-1).

## PDF/UA-1 identification

A PDF claims PDF/UA-1 (`pdfuaid:part` in the XMP, `PdfWriteResult.PdfUa` true) only when it can meet the standard:

* every font is embedded (a supplied TrueType font, with its ToUnicode map; the built-in Courier is not embedded, so a
  document that shows text with it is tagged but never claims PDF/UA);
* a document title and a document language exist;
* every `img` and `svg` has alt text or is decorative (`alt=""`);
* headings start at `H1` and never skip a level going deeper.

Anything else still gets the structure tree and marked content. In non-strict mode a figure without alt text is reported
as warning `FPDF1105`; strict mode does not fail on it (it is not a CSS error), and the figure is written without `Alt`.

Validate with [veraPDF](https://verapdf.org/) (the PDF/UA-1 profile); `scripts/verify_tagged_pdf.py` runs it over the
examples: every example whose PDF claims PDF/UA-1 passes the profile, and a PDF that does not claim it is not judged.

## Differences from Chrome

Probed with Chrome 158 (headless print-to-PDF) on synthetic pages, element by element. Chrome writes `NonStruct` for
`div`, `header`, `footer`, `main`, `nav`, `search`, `section`, list bodies and every text run, writes no structure for
`dl`, no `THead`, `TBody` or `TFoot`, no `LBody`, no `Quote`, nests a `Figure` inside a `Figure` for `figure` and leaves
`Strong`, `Em` and `Aside` unmapped (veraPDF's PDF/UA-1 profile fails Chrome's own output). FactsPDF writes the standard
PDF 1.7 grouping types for those elements and `LBody`, `THead`, `TBody`, `TFoot`, `Quote` and one `Figure` per image;
the structure types it shares with Chrome (`Document`, `P`, `H1`–`H6`, `L`, `LI`, `Lbl`, `BlockQuote`, `Table`, `TR`,
`TH`, `TD`, `Caption`, `Link`, `Figure`, `Code`, `Art`, `Aside`, `Strong`, `Em`) nest the same way.

## Limits

The structure is built from the element tree, so it is only as meaningful as the HTML: FactsPDF does not infer table
headers (`Headers` and `ID`), heading levels or reading order beyond the document's own. A run is split where its glyphs
change structure element, so a document near `MaxDisplayCommands` has slightly more display commands tagged than untagged.
