using System.Buffers.Binary;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PageMarginTextTests
{
    private static readonly PdfOptions Page = new() { PageWidth=240, PageHeight=120, Margin=20 };
    private const string Css = "<style>p,h2{font-size:10pt;line-height:2;margin-top:0;margin-bottom:0;font-weight:normal}</style>";
    private static PdfOptions With(PdfOptions options, string? header=null, string? footer=null)
        => options with { HeaderText=header, FooterText=footer };
    private static byte[] Pdf(string html,PdfOptions options) => TableLayoutTests.Pdf(html,options);
    private static string Ascii(string html,PdfOptions options) => Encoding.ASCII.GetString(Pdf(html,options));
    private static string Explicit(int n) => Css+string.Concat(Enumerable.Range(1,n).Select(i=>$"<p style='break-before:{(i==1?"auto":"page")}'>A</p>"));
    private static void Fails(string html,PdfOptions options,string code)
    {
        using var stream=new MemoryStream();stream.Write("sentinel"u8);stream.Position=3;
        Assert.That(Assert.Throws<FactsPdfException>(()=>PdfConverter.Convert(html,stream,options))!.Code,Is.EqualTo(code));
        Assert.That(stream.ToArray(),Is.EqualTo("sentinel"u8.ToArray()));Assert.That(stream.Position,Is.EqualTo(3));
    }
    private static List<LayoutPage> Layout(string html,PdfOptions options)
    {
        var pages=TableLayoutTests.Layout(html,options);
        long count=pages.Sum(p=>(long)p.Runs.Count+p.Discs.Count+p.Images.Count+p.PaintBoxes.Sum(b=>b.CommandCount));
        PageMarginLayout.Append(pages,options,count,default);return pages;
    }
    private static PdfFont Font(int? missing=null,int ymax=700,int ymin=0,bool pipe=false)
    {
        var bytes=FontFixture.Create(listDigits:true);var sub=FontFixture.Table(bytes,"cmap")+12;
        var count=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(sub+12));
        for(var i=0;i<count;i++)
        {
            var at=sub+16+12*i;var scalar=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            if(scalar==missing)FontFixture.U32(bytes,at+8,0);
            // Reassign the supplementary fixture glyph to the ASCII footer separator.
            if(pipe && scalar==0x20000){FontFixture.U32(bytes,at,124);FontFixture.U32(bytes,at+4,124);}
        }
        if(pipe)
        {
            var groups=Enumerable.Range(0,count).Select(i=>bytes.AsSpan(sub+16+i*12,12).ToArray())
                .OrderBy(b=>BinaryPrimitives.ReadUInt32BigEndian(b)).ToArray();
            for(var i=0;i<count;i++)groups[i].CopyTo(bytes,sub+16+i*12);
        }
        FontFixture.U16(bytes,FontFixture.Table(bytes,"head")+38,ymin);
        FontFixture.U16(bytes,FontFixture.Table(bytes,"head")+42,ymax);
        return PdfFont.LoadTrueType(bytes);
    }

    [Test]
    public void HeaderFooterAndNumberUseOneLineInEachExistingMargin()
    {
        var pdf=Ascii("",With(Page with { PageNumbers=true },"A","B"));
        Assert.That(pdf,Does.Contain("/Count 1"));
        Assert.That(pdf,Does.Contain("1 0 0 1 117 107 Tm <41> Tj"));
        Assert.That(pdf,Does.Contain("1 0 0 1 105 7 Tm <42207C2031> Tj"));
    }
    [TestCase(false)] [TestCase(true)]
    public void DisabledFieldsPreserveDefaultOrNumberOnlyBytes(bool numbers)
    {
        var options=Page with { PageNumbers=numbers };var expected=Pdf(Explicit(2),options);
        foreach(var value in new string?[]{null,"","   "})Assert.That(Pdf(Explicit(2),With(options,value,value)),Is.EqualTo(expected));
    }
    [TestCase("",1)] [TestCase("<p style='break-after:page'>A</p>",1)]
    public void EmptyAndTrailingBreakRepeatOnlyOnActualPages(string html,int expected)
    {
        var pages=Layout(html,With(Page,"A","B"));Assert.That(pages,Has.Count.EqualTo(expected));
        Assert.That(pages[0].Runs.TakeLast(2).Select(r=>r.Text),Is.EqualTo(new[]{"A","B"}));
    }
    [Test]
    public void EveryExplicitPageHasLiteralHeaderAndSequentialCombinedFooter()
    {
        var pages=Layout(Explicit(10),With(Page with { PageNumbers=true },"A","B"));
        Assert.That(pages,Has.Count.EqualTo(10));
        for(var i=0;i<10;i++)Assert.That(pages[i].Runs.TakeLast(2).Select(r=>r.Text),Is.EqualTo(new[]{"A",$"B | {i+1}"}));
        Assert.That(pages[9].Runs.Last().X,Is.EqualTo(102));
    }
    [Test]
    public void MarkupEntitiesAndCounterLookingTextRemainLiteral()
    {
        var text="<b>&amp; {page}</b>";
        Assert.That(Layout("",With(Page,text)).Single().Runs.Single().Text,Is.EqualTo(text));
    }
    [Test]
    public void AsciiSpacesNormalizeBeforeResolvingMissingSpaceGlyph()
    {
        var options=With(Page with { Fonts=[Font(missing:32)] },"  A  ");
        Assert.DoesNotThrow(()=>Pdf("",options));Assert.That(Layout("",options).Single().Runs.Single().Text,Is.EqualTo("A"));
        Assert.That(Layout("",With(Page,"  A   B ")).Single().Runs.Single().Text,Is.EqualTo("A B"));
    }
    [TestCase("A\nB")] [TestCase("A\rB")] [TestCase("A\tB")] [TestCase("A\fB")] [TestCase("A\0B")]
    [TestCase("A\u0085B")] [TestCase("A\u2028B")] [TestCase("A\u2029B")]
    public void ControlsAndExplicitMultilineFailWithoutFonts(string value) => Fails("",With(Page,value),"FPDF1305");
    [Test]
    public void WholeFieldLexicalValidationPrecedesMissingGlyph()
    {
        Fails("",With(Page,"\u4e2d\ud800"),"FPDF1304");
        Fails("",With(Page,"\u4e2d\n"),"FPDF1305");
    }
    [TestCase(false)] [TestCase(true)]
    public void MultiwordWrapAndUnbreakableOverflowAreRejected(bool words)
        => Fails("",With(Page,words?string.Join(' ',Enumerable.Repeat("AA",20)):new string('A',34)),"FPDF1302");
    [Test]
    public void ExactSingleLineWidthFitsButNumberTransitionCanOverflow()
    {
        Assert.DoesNotThrow(()=>Pdf("",With(Page,new string('A',33))));
        var options=With(Page with { PageNumbers=true },footer:new string('A',29));
        Assert.DoesNotThrow(()=>Pdf(Explicit(9),options));Fails(Explicit(10),options,"FPDF1302");
    }
    [Test]
    public void WrappedFallbackLineFailsGeometryBeforeChargingActualRuns()
    {
        var options=With(Page with { Fonts=[Font(missing:66),Font()],MaxDisplayCommands=1 },string.Join(' ',Enumerable.Repeat("A B",20)));
        Fails("",options,"FPDF1302");
    }
    [TestCase(0)] [TestCase(15.999)]
    public void InsufficientTopOrBottomMarginFails(double margin)
    {
        Fails("",With(Page with { Margin=margin },"A"),"FPDF1302");
        Fails("",With(Page with { Margin=margin },footer:"B"),"FPDF1302");
    }
    [Test]
    public void ExactSixteenPointMarginsFitBothLines()
    {
        var pdf=Ascii("",With(Page with { Margin=16 },"A","B"));
        Assert.That(pdf,Does.Contain("117 109 Tm <41>"));Assert.That(pdf,Does.Contain("117 5 Tm <42>"));
    }
    [TestCase(1500,true)] [TestCase(1501,false)]
    public void HeaderRejectsBodyInkOverhangDespiteValidLineBox(int ymax,bool fits)
    {
        var options=Page with { Fonts=[Font()],BoldFonts=[Font(ymax:ymax)] };var html=Css+"<p style='font-weight:bold'>A</p>";
        Assert.DoesNotThrow(()=>Pdf(html,options));
        if(fits)Assert.DoesNotThrow(()=>Pdf(html,With(options,"A")));else Fails(html,With(options,"A"),"FPDF1302");
    }
    [TestCase(false)] [TestCase(true)]
    public void HeadingAndTableBodyGeometryIsUnchanged(bool table)
    {
        var html=table?TablePaginationTests.Table(10):HeadingKeepTests.Css+HeadingKeepTests.Before(2)+HeadingKeepTests.Heading()+"<p>A<br>B</p>";
        var original=TableLayoutTests.Layout(html,Page);var changed=Layout(html,With(Page with { PageNumbers=true },"Header","Footer"));
        Assert.That(changed,Has.Count.EqualTo(original.Count));
        for(var i=0;i<original.Count;i++)
        {
            Assert.That(changed[i].Runs.Take(original[i].Runs.Count),Is.EqualTo(original[i].Runs));
            Assert.That(changed[i].PaintBoxes,Is.EqualTo(original[i].PaintBoxes));
        }
    }
    [Test]
    public void NormalFallbackRunsUseExistingEmbeddingAndExactDisplayBudget()
    {
        var first=Font(missing:66);var second=Font();var options=With(Page with { Fonts=[first,second],SubsetFonts=true },"AB");
        var runs=Layout("",options).Single().Runs;Assert.That(runs.Select(r=>r.Font),Is.EqualTo(new[]{first,second}));
        Assert.DoesNotThrow(()=>Pdf("",options with { MaxDisplayCommands=2 }));Fails("",options with { MaxDisplayCommands=1 },"FPDF1401");
        Fails("",With(Page with { Fonts=[first] },"B"),"FPDF1504");
        Fails("",With(Page with { Fonts=[Font()] ,PageNumbers=true },footer:"A"),"FPDF1504"); // separator glyph required
    }
    [Test]
    public void BodyStylesDoNotChangeFurnitureNormalRoleColorOrSize()
    {
        var options=With(Page with { BoldItalicFonts=[Font()],FontSize=20 },"A","B");
        var html="<p style='font-weight:bold;font-style:italic;color:red;font-size:20pt'>A</p>";
        foreach(var run in Layout(html,options).Single().Runs.TakeLast(2))
        {Assert.That(run.Style.FontSize,Is.EqualTo(10));Assert.That(run.Style.Color,Is.EqualTo(new Rgb(0,0,0)));Assert.That(run.Font,Is.Null);Assert.That(run.Style.Bold||run.Style.Italic,Is.False);}
    }
    [Test]
    public void RawInputLengthsIncludeDiscardedSpacesAndBothFields()
    {
        var options=With(Page," A ","B");var html="<p>A</p>";var count=html.Length+4;
        Assert.DoesNotThrow(()=>Pdf(html,options with { MaxInputCharacters=count }));Fails(html,options with { MaxInputCharacters=count-1 },"FPDF1001");
        Assert.That(Pdf("",With(Page,new string(' ',1024))),Is.EqualTo(Pdf("",Page)));
        Fails("",With(Page,new string(' ',1025)),"FPDF1001");Fails("",With(Page,footer:new string(' ',1025)),"FPDF1001");
        Fails("",With(Page with { MaxInputCharacters=1 },"  "),"FPDF1001");
    }
    [Test]
    public void BodyBudgetThenHeaderThenFooterThenOutputErrorsHaveDeterministicOrder()
    {
        Fails("<script>x</script>",With(Page,"\n"),"FPDF1102");
        Fails("<p>A</p>",With(Page with { MaxDisplayCommands=1 },"\n"),"FPDF1401");
        Fails("",With(Page,"\u4e2d","\n"),"FPDF1301");
        Fails("",With(Page with { MaxDisplayCommands=1 },"A","\n"),"FPDF1401");
        Fails("",With(Page with { MaxOutputBytes=1 },"\n"),"FPDF1305");
    }
    [Test]
    public void AllFurnitureCountsTowardExactOutputPageAndDisplayLimits()
    {
        var html=Explicit(2);var options=With(Page with { PageNumbers=true },"A","B");var expected=Pdf(html,options);
        Assert.That(Pdf(html,options with { MaxDisplayCommands=6,MaxPages=2,MaxOutputBytes=expected.Length }),Is.EqualTo(expected));
        Fails(html,options with { MaxDisplayCommands=5 },"FPDF1401");Fails(html,options with { MaxPages=1 },"FPDF1303");Fails(html,options with { MaxOutputBytes=expected.Length-1 },"FPDF1401");
    }
    [Test]
    public void PreCancelledConversionPreservesDestination()
    {
        var options=With(Page,"A");using var stream=new MemoryStream();stream.Write("sentinel"u8);
        Assert.Throws<OperationCanceledException>(()=>PdfConverter.Convert("",stream,options,new CancellationToken(true)));
        Assert.That(stream.ToArray(),Is.EqualTo("sentinel"u8.ToArray()));
    }
    [TestCase("")] [TestCase("-")] [TestCase("--literal")]
    public void CliTakesNextTokenAsLiteralAndMatchesApi(string text)
    {
        using var output=new MemoryStream();using var error=new StringWriter();
        Assert.That(CliApplication.Run(["-","-","--header-text",text,"--footer-text","B","--page-numbers"],Stream.Null,output,error),Is.Zero,error.ToString());
        Assert.That(output.ToArray(),Is.EqualTo(Pdf("",With(new PdfOptions { PageNumbers=true },text,"B"))));
    }
    [Test]
    public void CliRejectsMissingAndDuplicateEvenEmptyValuesAndDocumentsOptions()
    {
        foreach(var args in new[]{new[]{"-","-","--header-text"},new[]{"-","-","--footer-text"},new[]{"-","-","--header-text","","--header-text","A"},new[]{"-","-","--footer-text","","--footer-text","A"}})
        {using var output=new MemoryStream();using var error=new StringWriter();Assert.That(CliApplication.Run(args,Stream.Null,output,error),Is.EqualTo(2));Assert.That(output.Length,Is.Zero);}
        using var help=new MemoryStream();Assert.That(CliApplication.Run(["--help"],Stream.Null,help,TextWriter.Null),Is.Zero);
        Assert.That(Encoding.UTF8.GetString(help.ToArray()),Does.Contain("--header-text").And.Contain("--footer-text"));
    }
    [Test]
    public void CliLateFooterFailurePreservesExistingFileAndRemovesTemporary()
    {
        var directory=Directory.CreateTempSubdirectory("factspdf-page-margins-").FullName;
        try
        {
            var output=Path.Combine(directory,"out.pdf");File.WriteAllText(output,"sentinel");using var error=new StringWriter();
            Assert.That(CliApplication.Run(["-",output,"--header-text","A","--footer-text",new string('B',100),"--overwrite"],Stream.Null,Stream.Null,error),Is.EqualTo(3));
            Assert.That(error.ToString(),Does.Contain("FPDF1302"));Assert.That(File.ReadAllText(output),Is.EqualTo("sentinel"));Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
        }
        finally{Directory.Delete(directory,true);}
    }
    [TestCase(false)] [TestCase(true)]
    public void ExplicitNormalFontsEmbedHeaderFooterAndSeparator(bool subset)
    {
        var options=With(Page with { Fonts=[Font(pipe:true)],PageNumbers=true,SubsetFonts=subset },"中文","A");
        var runs=Layout("",options).Single().Runs;
        Assert.That(runs.Select(r=>r.Text),Is.EqualTo(new[]{"中文","A | 1"}));
        Assert.That(runs.All(r=>ReferenceEquals(r.Font,options.Fonts[0])),Is.True);
        Assert.That(Ascii("",options),Does.Contain("/Subtype /Type0"));
    }
    [Test]
    public void CjkWrappingIsRejectedAndDoesNotCreateAnotherHeaderLine()
        => Fails("",With(Page with { Fonts=[Font()] },new string('中',21)),"FPDF1302");
    [Test]
    public void NonAsciiSpaceRemainsLiteralAndRequiresItsOwnGlyph()
    {
        var options=With(Page with { Fonts=[Font()] },"A\u00a0B");
        Assert.That(Layout("",options).Single().Runs.Single().Text,Is.EqualTo("A\u00a0B"));
        Fails("",With(Page,"A\u00a0B"),"FPDF1301");
    }
    [Test]
    public void InvalidTextAfterEarlierHeaderGlyphErrorStillUsesHeaderFirstOrder()
    {
        Fails("",With(Page,"中","\ud800"),"FPDF1301");
        Fails("",With(Page,"\n\ud800"),"FPDF1305");
        Fails("",With(Page,"\ud800\n"),"FPDF1304");
    }
    [Test]
    public void UnusedLargeFallbackFontDoesNotChangeHeaderBytes()
    {
        var first=Font();var options=With(Page with { Fonts=[first] },"A");
        Assert.That(Pdf("",options with { Fonts=[first,Font(ymax:4000,ymin:-4000)] }),Is.EqualTo(Pdf("",options)));
        Fails("",With(Page with { Fonts=[Font(missing:65),Font(ymax:4000)] },"A"),"FPDF1302");
    }
    [Test]
    public void PostFailureRecoveryUsesNoSharedMutablePageTextState()
    {
        var options=With(Page,"A","B");var expected=Pdf("",options);
        Fails("",options with { FooterText=new string('B',34) },"FPDF1302");
        Assert.That(Pdf("",options),Is.EqualTo(expected));
    }

}
