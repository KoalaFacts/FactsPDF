using System.Globalization;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PageNumberTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 120, Margin = 20 };
    private const string Css = "<style>p,h2{font-size:10pt;line-height:2;margin-top:0;margin-bottom:0;font-weight:normal}</style>";

    private static PdfOptions Numbered(PdfOptions options) => options with { PageNumbers = true };
    private static byte[] Pdf(string html, PdfOptions options) => TableLayoutTests.Pdf(html, options);
    private static string Ascii(string html, PdfOptions options) => Encoding.ASCII.GetString(Pdf(html, options));
    private static string Explicit(int count) => Css + string.Concat(Enumerable.Range(1,count)
        .Select(i => $"<p style='break-before:{(i == 1 ? "auto" : "page")}'>A</p>"));
    private static void Fails(string html, PdfOptions options, string code)
    {
        using var stream = new MemoryStream();stream.Write("sentinel"u8);stream.Position=3;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html,stream,options))!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }

    [Test]
    public void CliAcceptsPageNumbersAndWritesSearchableNumber()
    {
        using var input=new MemoryStream(Encoding.UTF8.GetBytes("<p>A</p>"));using var output=new MemoryStream();using var error=new StringWriter();
        Assert.That(CliApplication.Run(["-","-","--page-numbers"],input,output,error), Is.Zero, error.ToString());
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Does.Contain("<31> Tj"));
    }

    [Test]
    public void CliHelpDocumentsPageNumbers()
    {
        using var output=new MemoryStream();using var error=new StringWriter();
        Assert.That(CliApplication.Run(["--help"],Stream.Null,output,error), Is.Zero);
        Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Contain("--page-numbers"));
    }

    [Test]
    public void EmptyDocumentHasOneCenteredNumber()
    {
        var pdf=Ascii("",Numbered(Page));
        Assert.That(pdf, Does.Contain("/Count 1"));
        Assert.That(pdf, Does.Contain("1 0 0 1 117 7 Tm <31> Tj"));
    }

    [Test]
    public void AllActualPagesAreNumberedAndDigitTransitionsRecenter()
    {
        var pdf=Ascii(Explicit(100),Numbered(Page));
        Assert.That(pdf, Does.Contain("/Count 100"));
        Assert.That(pdf, Does.Contain("1 0 0 1 117 7 Tm <39> Tj"));
        Assert.That(pdf, Does.Contain("1 0 0 1 114 7 Tm <3130> Tj"));
        Assert.That(pdf, Does.Contain("1 0 0 1 114 7 Tm <3939> Tj"));
        Assert.That(pdf, Does.Contain("1 0 0 1 111 7 Tm <313030> Tj"));
    }

    [TestCase("")] [TestCase("<p style='break-after:page'>A</p>")] [TestCase("<p></p>")]
    public void EmptyAndTrailingBreakDoNotCreateFooterPages(string html)
    {
        using var output=new MemoryStream();
        var result=PdfConverter.Convert(html,output,Numbered(Page));
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Does.Contain("<31> Tj"));
    }

    [TestCase(0)] [TestCase(15.999)]
    public void InsufficientMarginFailsOnlyWhenEnabled(double margin)
    {
        var options=Page with { Margin=margin };
        Assert.DoesNotThrow(() => Pdf("",options));
        Fails("",Numbered(options),"FPDF1302");
    }

    [Test]
    public void ExactSixteenPointMarginFitsCourierLineWithTwoPointClearance()
        => Assert.That(Ascii("",Numbered(Page with { Margin=16 })), Does.Contain("1 0 0 1 117 5 Tm <31> Tj"));

    [Test]
    public void DisplayAndOutputBudgetsIncludePageNumbersExactly()
    {
        var options=Numbered(Page);var html=Explicit(2);var expected=Pdf(html,options);
        Assert.That(Pdf(html,options with { MaxDisplayCommands=4,MaxPages=2,MaxOutputBytes=expected.Length }), Is.EqualTo(expected));
        Fails(html,options with { MaxDisplayCommands=3 },"FPDF1401");
        Fails(html,options with { MaxPages=1 },"FPDF1303");
        Fails(html,options with { MaxOutputBytes=expected.Length-1 },"FPDF1401");
    }

    [Test]
    public void SuppliedNormalFontMissingDigitsDoesNotUseCourier()
        => Fails("<p>A</p>",Numbered(Page with { Fonts=[PdfFont.LoadTrueType(FontFixture.Create())] }),"FPDF1504");

    [Test]
    public void BodyDisplayLimitPrecedesMissingNumberGlyph()
        => Fails("<p>A</p>",Numbered(Page with { Fonts=[PdfFont.LoadTrueType(FontFixture.Create())],MaxDisplayCommands=1 }),"FPDF1401");

    [Test]
    public void NumberGlyphErrorPrecedesSerializedOutputLimit()
        => Fails("<p>A</p>",Numbered(Page with { Fonts=[PdfFont.LoadTrueType(FontFixture.Create())],MaxOutputBytes=1 }),"FPDF1504");

    [Test]
    public void BodyFailureStillPrecedesFooterFailure()
        => Fails("<script>x</script>",Numbered(Page with { Margin=0 }),"FPDF1102");

    [Test]
    public void CultureDoesNotChangeDecimalCounters()
    {
        var prior=CultureInfo.CurrentCulture;
        try
        {
            var expected=Pdf(Explicit(10),Numbered(Page));CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ar-EG");
            Assert.That(Pdf(Explicit(10),Numbered(Page)), Is.EqualTo(expected));
        }
        finally { CultureInfo.CurrentCulture=prior; }
    }

    [Test]
    public void PreCancellationPreservesDestination()
    {
        using var stream=new MemoryStream();stream.Write("sentinel"u8);
        Assert.Throws<OperationCanceledException>(()=>PdfConverter.Convert("",stream,Numbered(Page),new CancellationToken(true)));
        Assert.That(stream.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
    }
    private static List<LayoutPage> Layout(string html, PdfOptions options)
    {
        var pages=TableLayoutTests.Layout(html,options);
        if(options.PageNumbers)
        {
            long commands=pages.Sum(p => (long)p.Runs.Count+p.Discs.Count+p.Images.Count+p.PaintBoxes.Sum(b=>b.CommandCount));
            PageNumberLayout.Append(pages,options,commands,default);
        }
        return pages;
    }

    private static PdfFont Font(bool digits=true, int ymin=0, int xmax=900, bool missingZero=false, int ascent=800)
    {
        var bytes=FontFixture.Create(listDigits:digits);
        FontFixture.U16(bytes,FontFixture.Table(bytes,"head")+38,ymin);
        FontFixture.U16(bytes,FontFixture.Table(bytes,"head")+40,xmax);
        FontFixture.U16(bytes,FontFixture.Table(bytes,"OS/2")+68,ascent);
        if(missingZero)
        {
            var sub=FontFixture.Table(bytes,"cmap")+12;
            var count=(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(sub+12));
            for(var i=0;i<count;i++)
            {
                var at=sub+16+12*i;
                if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at))=='0') FontFixture.U32(bytes,at+8,0);
            }
        }
        return PdfFont.LoadTrueType(bytes);
    }

    [Test]
    public void DisabledAndExplicitFalseProduceIdenticalBytes()
        => Assert.That(Pdf(Explicit(2),Page with { PageNumbers=false }),Is.EqualTo(Pdf(Explicit(2),Page)));

    [TestCase(false)] [TestCase(true)]
    public void NumberingPreservesBodyRunsAndActualHeadingTablePages(bool table)
    {
        var html=table ? TablePaginationTests.Table(10) : HeadingKeepTests.Css+HeadingKeepTests.Before(2)+HeadingKeepTests.Heading()+"<p>A<br>B</p>";
        var original=Layout(html,Page);var numbered=Layout(html,Numbered(Page));
        Assert.That(numbered,Has.Count.EqualTo(original.Count));
        for(var i=0;i<original.Count;i++)
        {
            Assert.That(numbered[i].Runs.Take(original[i].Runs.Count),Is.EqualTo(original[i].Runs));
            Assert.That(numbered[i].Runs.Last().Text,Is.EqualTo((i+1).ToString(CultureInfo.InvariantCulture)));
            Assert.That(numbered[i].PaintBoxes,Is.EqualTo(original[i].PaintBoxes));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void EmptyDocumentCanIntroduceEmbeddedNormalFont(bool subset)
    {
        var options=Numbered(Page with { Fonts=[Font()],SubsetFonts=subset });
        var run=Layout("",options).Single().Runs.Single();
        Assert.That(run.Text,Is.EqualTo("1"));Assert.That(run.Font,Is.SameAs(options.Fonts[0]));
        Assert.That(Encoding.ASCII.GetString(Pdf("",options)),Does.Contain("/Subtype /Type0"));
    }

    [Test]
    public void StyledBodyDoesNotChangeNormalPageNumberAndCanIntroduceCourier()
    {
        var html="<p style='font-weight:bold;font-style:italic;color:red;font-size:20pt'>A</p>";
        var options=Numbered(Page with { BoldItalicFonts=[Font(false)] });
        var run=Layout(html,options).Single().Runs.Last();
        Assert.That(run.Style.FontSize,Is.EqualTo(10));Assert.That(run.Style.Color,Is.EqualTo(new Rgb(0,0,0)));
        Assert.That(run.Style.Bold || run.Style.Italic,Is.False);Assert.That(run.Font,Is.Null);
        Assert.That(Encoding.ASCII.GetString(Pdf(html,options)),Does.Contain("/BaseFont /Courier"));
    }

    [Test]
    public void MissingZeroIsReportedOnlyWhenPageTenNeedsIt()
    {
        var options=Numbered(Page with { Fonts=[Font(missingZero:true)] });
        Assert.DoesNotThrow(()=>Pdf(Explicit(9),options));
        Fails(Explicit(10),options,"FPDF1504");
    }

    [Test]
    public void DigitFallbackChargesEveryActualTextRun()
    {
        var first=Font(missingZero:true);var fallback=Font();
        var options=Numbered(Page with { Fonts=[first,fallback] });
        var runs=Layout(Explicit(10),options)[9].Runs;
        Assert.That(runs.Select(r=>r.Text),Is.EqualTo(new[]{"A","1","0"}));
        Assert.That(runs[1].Font,Is.SameAs(first));Assert.That(runs[2].Font,Is.SameAs(fallback));
        Assert.That(runs[1].X,Is.EqualTo(114));Assert.That(runs[2].X,Is.EqualTo(120));
        Assert.DoesNotThrow(()=>Pdf(Explicit(10),options with { MaxDisplayCommands=21 }));
        Fails(Explicit(10),options with { MaxDisplayCommands=20 },"FPDF1401");
    }

    [Test]
    public void UnusedFallbackBoundsDoNotAffectNumbers()
    {
        var first=Font();var options=Numbered(Page with { Fonts=[first,Font(ymin:-4000,xmax:30000)] });
        Assert.That(Pdf("",options),Is.EqualTo(Pdf("",options with { Fonts=[first] })));
    }

    [Test]
    public void ActualFallbackBoundsAndFirstFontStrutAreChecked()
    {
        Fails(Explicit(10),Numbered(Page with { Fonts=[Font(missingZero:true),Font(ymin:-4000)] }),"FPDF1302");
        Fails("",Numbered(Page with { Fonts=[Font(digits:false,ascent:4000),Font()] }),"FPDF1302");
    }

    [TestCase(-900,true)] [TestCase(-901,false)]
    public void BodyInkBoundMustLeaveTwoPointClearanceDespiteValidLineBoxes(int ymin,bool fits)
    {
        var html=Css+"<p style='font-weight:bold'>A<br>A<br>A<br>A</p>";
        var options=Page with { Fonts=[Font()],BoldFonts=[Font(ymin:ymin)] };
        Assert.DoesNotThrow(()=>Pdf(html,options));
        if(fits) Assert.DoesNotThrow(()=>Pdf(html,Numbered(options)));
        else Fails(html,Numbered(options),"FPDF1302");
    }

    [TestCase(11800,true)] [TestCase(11801,false)]
    public void HorizontalFontBoundsUseEveryDigitOrigin(int xmax,bool fits)
    {
        // Page 10: second digit origin 120, right bound 118 => physical edge 238.
        var options=Numbered(Page with { Fonts=[Font(xmax:xmax)] });
        Assert.DoesNotThrow(()=>Pdf(Explicit(9),options));
        if(fits) Assert.DoesNotThrow(()=>Pdf(Explicit(10),options));
        else Fails(Explicit(10),options,"FPDF1302");
    }

    [Test]
    public void CliFlagIsSingleUseAndMatchesDirectApi()
    {
        var html=Encoding.UTF8.GetBytes(Explicit(2));using var input=new MemoryStream(html);using var output=new MemoryStream();using var error=new StringWriter();
        Assert.That(CliApplication.Run(["-","-","--page-numbers"],input,output,error),Is.Zero);
        Assert.That(output.ToArray(),Is.EqualTo(Pdf(Encoding.UTF8.GetString(html),new PdfOptions { PageNumbers=true })));
        using var duplicate=new MemoryStream();
        Assert.That(CliApplication.Run(["-","-","--page-numbers","--page-numbers"],Stream.Null,duplicate,error),Is.EqualTo(2));
        Assert.That(duplicate.Length,Is.Zero);
    }

    [Test]
    public void CliLateNumberFailurePreservesExistingFile()
    {
        var directory=Directory.CreateTempSubdirectory("factspdf-page-numbers-").FullName;
        try
        {
            var font=Path.Combine(directory,"font.ttf");File.WriteAllBytes(font,FontFixture.Create());
            var output=Path.Combine(directory,"output.pdf");File.WriteAllText(output,"sentinel");
            using var input=new MemoryStream(Encoding.UTF8.GetBytes("<p>A</p>"));using var error=new StringWriter();
            Assert.That(CliApplication.Run(["-",output,"--page-numbers","--font",font,"--overwrite"],input,Stream.Null,error),Is.EqualTo(3));
            Assert.That(error.ToString(),Does.Contain("FPDF1504"));Assert.That(File.ReadAllText(output),Is.EqualTo("sentinel"));
            Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(2));
        }
        finally { Directory.Delete(directory,true); }
    }

}
