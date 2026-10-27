# Fix a first-use font or image error

FactsPDF reports unsupported or incomplete input instead of silently substituting
resources. This guide explains five common resource failures. It does not grant
evaluation rights or announce a package release; see [evaluation](evaluation.md)
and [licensing](../LICENSING.md) before building or running the engine.

The first stderr line retains the original `FPDF` diagnostic. These five codes
also receive one static `Hint:` line naming the existing resource options.
Conversion still exits with code **3**; scripts should use the exit status and
error code rather than depend on the human-readable hint wording. Hints are not
written to stdout, where `-` may carry binary PDF output.

## FPDF1301: non-ASCII text without a font

Supply your own trusted, appropriately licensed static TrueType font covering
the text. FactsPDF does not discover installed fonts or download them. For an
input containing `<p>Hello, 中文。</p>`:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --font /path/to/Chinese.ttf
```

The path is an explicit placeholder, not a bundled font. ASCII-only text can
still use the built-in Courier without a font file. See [Unicode and fonts](unicode-fonts.md)
for supported font formats and script limits.

## FPDF1504: the supplied fallback chain lacks a character

Read the reported Unicode code point and check the corresponding text's style
in your template; the normal-font diagnostic does not name a style role. Add
a font covering that character to the **same role's** ordered fallback chain:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --font /path/to/Latin.ttf --font /path/to/Chinese.ttf
```

Repeated `--font` applies to normal text only. For bold text, repeat
`--font-bold` instead; italic and bold-italic have their own chains. There is no
silent missing-glyph replacement and no cross-style fallback. A font with a
matching family name does not prove glyph coverage or embedding permission.

## FPDF1505: font embedding permissions do not allow this PDF

A supplied font has restricted, preview-and-print-only, bitmap-only or unsupported
`OS/2 fsType` embedding permissions. FactsPDF conservatively refuses these fonts
rather than embedding them without authorisation. This is not a missing-glyph
error, and `--subset-fonts` is **not** a workaround.

Check the actual font licence and the font's embedding-permission metadata. Ask
the font provider for an appropriately licensed **different** static TrueType
file that permits installable or editable PDF embedding; then supply it using
the same normal or style-specific `--font*` option. Verify the replacement's
glyphs and actual font style rather than assuming the family name is enough.

**Do not edit `fsType` bits, rename the restricted file, disable validation or
treat possession of the font as an embedding grant.** Even if the permission bits
allow embedding, the separate font licence still controls what you may do.
Conversion exits 3 and preserves an existing output file when this error occurs.

## FPDF1507: a requested font style has no explicit chain

Provide the role named by the diagnostic:

| Text role | CLI option | .NET option |
| --- | --- | --- |
| Normal | `--font` | `PdfOptions.Fonts` |
| Bold | `--font-bold` | `PdfOptions.BoldFonts` |
| Italic | `--font-italic` | `PdfOptions.ItalicFonts` |
| Bold and italic | `--font-bold-italic` | `PdfOptions.BoldItalicFonts` |

For normal Chinese text and a bold heading, provide both normal and bold fonts:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --font /path/to/Chinese-Regular.ttf --font-bold /path/to/Chinese-Bold.ttf
```

Do not relabel a normal font as proof of a real bold or italic face. FactsPDF
does not synthesize these styles. The complete command may declare at most
eight font paths across all roles. See [font styles](font-styles.md).

## FPDF1604: an image resource is not bound

An HTML `src` is an ordinal resource name, not a request to open a file or URL.
For `<img src="logo">`, bind the same case-sensitive name explicitly:

```sh
dotnet run --project src/FactsPDF.Cli -c Release -- input.html output.pdf --image logo=/path/to/logo.png
```

Supported static PNG and baseline JPEG formats are detected from the supplied
bytes, not the extension. In .NET, bind a loaded `PdfImage` under the same name
in `PdfOptions.Images`. See [image constraints](images.md) before changing a
format; a resource hint does not make SVG, network images or other unsupported
formats valid.

## Retry without losing an earlier report

A failed conversion must not replace an existing destination. Once the input
or resource is corrected, explicitly add `--overwrite` to replace a previous
PDF **after successful conversion**. Without that flag, an existing destination
is an I/O refusal (exit 4), not one of the conversion errors above.

Use a trusted working directory. The CLI's temporary-file policy is not a
hostile-filesystem sandbox. For the .NET API, opening an existing file with
`File.Create` truncates it before conversion; use your own temporary-file policy
or write to a new stream and commit the result after success.

Unrelated input, budget, font-format and shaping errors
retain their existing diagnostics; adding fonts or images is not a generic
workaround for them. The core library's exception contract is unchanged.
