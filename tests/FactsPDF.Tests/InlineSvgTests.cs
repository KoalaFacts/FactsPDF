using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Inline svg renders as PDF vector paths, not a raster: the path data grammar becomes m/l/c/h
// operators, paint attributes become rg/RG/w/J/j/M and f/f*/S/B/B*, and the svg is sized as a
// replaced box from its width/height attributes, viewBox and CSS. Before, any svg failed FPDF1102.
// Default options: A4 (841.89pt high) with a 36pt margin, so a first block's content box starts
// at x 36 and 36pt below the page top.
public sealed class InlineSvgTests
{
    private static List<SvgSegment> Path(string data, int maxCommands = 10_000)
    {
        var segments = new List<SvgSegment>();
        SvgPathData.Parse(data, 0, new SvgLimits(maxCommands, default), segments);
        return segments;
    }

    // The 36pt margin offsets above are measured from the page margin, so the 8px user-agent body margin is reset.
    private const string NoBodyMargin = "<style>html,body{margin:0}</style>";

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(NoBodyMargin + html, options);

    private static string Pdf(string html, PdfOptions? options = null)
        => Encoding.Latin1.GetString(TableLayoutTests.Pdf(NoBodyMargin + html, options));

    // The content-stream text of the first svg: from its viewport clip to its closing Q.
    private static string Svg(string html, PdfOptions? options = null)
    {
        var pdf = Pdf(html, options);
        var clip = pdf.IndexOf(" re W n ", StringComparison.Ordinal);
        Assert.That(clip, Is.GreaterThan(0), "No svg viewport clip in the content stream.");
        var start = pdf.LastIndexOf('\n', clip) + 1;
        var end = pdf.IndexOf("\nQ\n", clip, StringComparison.Ordinal);
        return pdf[start..(end + 3)];
    }

    private static (double Width, double Height) Size(string html)
    {
        var image = Layout(html).Single().Images.Single();
        Assert.That(image.Vector, Is.Not.Null);
        return (image.Width, image.Height);
    }

    private static FactsPdfException Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("retained"u8);
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!;
        Assert.That(failure.Code, Is.EqualTo(code), failure.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("retained"));
        return failure;
    }

    private static void Near(SvgSegment segment, SvgSegmentKind kind, double x, double y)
    {
        Assert.That(segment.Kind, Is.EqualTo(kind));
        Assert.That(segment.X, Is.EqualTo(x).Within(1e-9));
        Assert.That(segment.Y, Is.EqualTo(y).Within(1e-9));
    }

    private static void Controls(SvgSegment segment, double x1, double y1, double x2, double y2)
    {
        Assert.That(segment.X1, Is.EqualTo(x1).Within(1e-9));
        Assert.That(segment.Y1, Is.EqualTo(y1).Within(1e-9));
        Assert.That(segment.X2, Is.EqualTo(x2).Within(1e-9));
        Assert.That(segment.Y2, Is.EqualTo(y2).Within(1e-9));
    }

    // Path data grammar ---------------------------------------------------------------------

    [Test]
    public void AbsoluteAndRelativeCommandsWithImplicitRepeats()
    {
        Assert.That(Path("M10 20 30 40 l5 5 h-5 v-5 H0 V0 z m1 1 2 2"), Is.EqualTo(new[]
        {
            SvgSegment.Move(10, 20), SvgSegment.Line(30, 40), SvgSegment.Line(35, 45), SvgSegment.Line(30, 45),
            SvgSegment.Line(30, 40), SvgSegment.Line(0, 40), SvgSegment.Line(0, 0), SvgSegment.Close,
            SvgSegment.Move(11, 21), SvgSegment.Line(13, 23),
        }));
        // A first relative moveto is absolute; a command after closepath restarts at the subpath start.
        Assert.That(Path("m5 5 h10 z l0 10"), Is.EqualTo(new[]
        {
            SvgSegment.Move(5, 5), SvgSegment.Line(15, 5), SvgSegment.Close, SvgSegment.Move(5, 5), SvgSegment.Line(5, 15),
        }));
    }

    [Test]
    public void NumberSyntaxAcceptsSignsFractionsExponentsAndCommas()
    {
        Assert.That(Path("M.5.5L-1e1+2E-1,3e+0 4 , 5-6"), Is.EqualTo(new[]
        {
            SvgSegment.Move(0.5, 0.5), SvgSegment.Line(-10, 0.2), SvgSegment.Line(3, 4), SvgSegment.Line(5, -6),
        }));
        Assert.That(Path(" \t\nM 1 , 2 \r\n"), Is.EqualTo(new[] { SvgSegment.Move(1, 2) }));
        Assert.That(Path(""), Is.Empty);
        Assert.That(Path("none"), Is.Empty);
    }

    [Test]
    public void SmoothCurvesReflectThePreviousControlPoint()
    {
        Assert.That(Path("M0 0 C10 0 20 10 20 20 S30 40 40 40"), Is.EqualTo(new[]
        {
            SvgSegment.Move(0, 0), SvgSegment.Cubic(10, 0, 20, 10, 20, 20), SvgSegment.Cubic(20, 30, 30, 40, 40, 40),
        }));
        // Without a preceding cubic the first control point is the current point.
        Assert.That(Path("M0 0 L5 5 s5 5 10 0")[2], Is.EqualTo(SvgSegment.Cubic(5, 5, 10, 10, 15, 5)));
    }

    [Test]
    public void QuadraticCurvesBecomeExactCubics()
    {
        var path = Path("M0 0 Q30 30 60 0 T120 0");
        Assert.That(path, Has.Count.EqualTo(3));
        Controls(path[1], 20, 20, 40, 20);
        Near(path[1], SvgSegmentKind.Cubic, 60, 0);
        // The reflected quadratic control point is (90, -30).
        Controls(path[2], 80, -20, 100, -20);
        Near(path[2], SvgSegmentKind.Cubic, 120, 0);
    }

    [TestCase("M0 0 A10 10 0 0 1 20 0", -10)]
    [TestCase("M0 0 A10 10 0 0 0 20 0", 10)]
    // Radii too small to reach the end point scale up to a semicircle.
    [TestCase("M0 0 A1 1 0 0 1 20 0", -10)]
    // Flags need no separators; the relative end point follows directly.
    [TestCase("M0 0a10 10 0 0120 0", -10)]
    public void ArcsBecomeQuarterCubicsThroughTheSweepSide(string data, double middleY)
    {
        var path = Path(data);
        Assert.That(path, Has.Count.EqualTo(3));
        Near(path[1], SvgSegmentKind.Cubic, 10, middleY);
        Near(path[2], SvgSegmentKind.Cubic, 20, 0);
        // A quarter circle's control points lie 4/3 tan(22.5 degrees) of the radius along the tangents.
        Assert.That(Math.Abs(path[1].Y1), Is.EqualTo(10 * 4d / 3 * Math.Tan(Math.PI / 8)).Within(1e-9));
    }

    [Test]
    public void LargeArcsUseUpToFourPiecesAndDegenerateArcsFollowTheSpec()
    {
        var large = Path("M0 0 A10 10 0 1 1 10 10");
        Assert.That(large.Count(s => s.Kind == SvgSegmentKind.Cubic), Is.EqualTo(3));
        Near(large[^1], SvgSegmentKind.Cubic, 10, 10);
        Assert.That(Path("M0 0 A0 5 0 0 1 10 10")[1], Is.EqualTo(SvgSegment.Line(10, 10)));
        Assert.That(Path("M3 4 A5 5 0 0 1 3 4"), Is.EqualTo(new[] { SvgSegment.Move(3, 4) }));
    }

    [TestCase("L0 0")]
    [TestCase("10 10")]
    [TestCase("M0")]
    [TestCase("M0 0 L1 1,")]
    [TestCase("M0 0 z 1 1")]
    [TestCase("M0 0 A1 1 0 2 1 2 2")]
    [TestCase("M0 0 X1 1")]
    [TestCase("M0 0 L1 -")]
    [TestCase("M1e999 0")]
    [TestCase("M2000000 0")]
    [TestCase("M999999 0 l999999 0")]
    public void MalformedOrUnboundedPathDataFails(string data)
    {
        var failure = Assert.Throws<FactsPdfException>(() => Path(data))!;
        Assert.That(failure.Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void PathSegmentsChargeTheDisplayCommandBudget()
    {
        Assert.That(Path("M0 0 1 1 2 2", maxCommands: 3), Has.Count.EqualTo(3));
        Assert.That(Assert.Throws<FactsPdfException>(() => Path("M0 0 1 1 2 2 3 3", maxCommands: 3))!.Code,
            Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void TransformListsComposeLeftToRight()
    {
        Assert.That(SvgReader.Transform("translate(10 5) scale(2)", 0), Is.EqualTo(new SvgMatrix(2, 0, 0, 2, 10, 5)));
        Assert.That(SvgReader.Transform("matrix(1,2,3,4,5,6)", 0), Is.EqualTo(new SvgMatrix(1, 2, 3, 4, 5, 6)));
        Assert.That(SvgReader.Transform("translate(7)", 0), Is.EqualTo(new SvgMatrix(1, 0, 0, 1, 7, 0)));
        var rotated = SvgReader.Transform("rotate(90 10 0)", 0);
        var (x, y) = rotated.Apply(20, 0);
        Assert.That(x, Is.EqualTo(10).Within(1e-9));
        Assert.That(y, Is.EqualTo(10).Within(1e-9));
        Assert.That(SvgReader.Transform("skewX(45)", 0).C, Is.EqualTo(1).Within(1e-12));
        foreach (var bad in new[] { "rotate(1 2)", "scale()", "translate(1 2 3)", "spin(4)", "scale(2", "scale(1e7)" })
            Assert.That(Assert.Throws<FactsPdfException>(() => SvgReader.Transform(bad, 0))!.Code, Is.EqualTo("FPDF1103"), bad);
    }

    // PDF operators --------------------------------------------------------------------------

    [Test]
    public void ViewBoxPathIsFilledInsideTheViewportClip()
    {
        Assert.That(Svg("<svg width='40' height='20' viewBox='0 0 80 40' xmlns='http://www.w3.org/2000/svg'>" +
                "<path d='M0 0H80V40H0Z' fill='#ff0000'/></svg>"),
            Is.EqualTo("q 36 790.89 30 15 re W n 0.375 0 0 -0.375 36 805.89 cm\n" +
                "q 1 0 0 rg 0 0 m 80 0 l 80 40 l 0 40 l h f Q\nQ\n"));
    }

    [Test]
    public void WithoutViewBoxOneUserUnitIsOneCssPixel()
        => Assert.That(Svg("<svg width='40' height='20'><rect x='2' y='3' width='4' height='5'/></svg>"),
            Is.EqualTo("q 36 790.89 30 15 re W n 0.75 0 0 -0.75 36 805.89 cm\n" +
                "q 0 0 0 rg 2 3 m 6 3 l 6 8 l 2 8 l h f Q\nQ\n"));

    [Test]
    public void StrokePaintRuleCapsAndJoinsBecomeGraphicsState()
    {
        var svg = Svg("<svg width='40' height='20'><path d='M0 0h10v10z' fill='blue' fill-rule='evenodd' " +
            "stroke='rgb(0, 128, 0)' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='bevel' stroke-miterlimit='7'/>" +
            "<line x1='0' y1='0' x2='5' y2='0' stroke='black'/></svg>");
        Assert.That(svg, Does.Contain("q 0 0 1 rg 0 0.502 0 RG 2.5 w 1 J 2 j 7 M 0 0 m 10 0 l 10 10 l h B* Q\n"));
        // A line has no fill area: only its stroke, with the default 1-unit width and miter limit 4.
        Assert.That(svg, Does.Contain("q 0 0 0 RG 1 w 4 M 0 0 m 5 0 l S Q\n"));
    }

    [Test]
    public void GroupPaintInheritsAndTransformsBecomeCm()
        => Assert.That(Svg("<svg width='40' height='20'><g fill='blue' transform='translate(10 5)'>" +
                "<rect width='4' height='4' transform='scale(2)'/></g></svg>"),
            Does.Contain("q 2 0 0 2 10 5 cm 0 0 1 rg 0 0 m 4 0 l 4 4 l 0 4 l h f Q\n"));

    [Test]
    public void BasicShapesBecomeTheirEquivalentPaths()
    {
        var svg = Svg("<svg width='100' height='100'><circle cx='10' cy='10' r='10'/>" +
            "<polygon points='0,0 4,0 4,4'/><polyline points='0 0 1 1' fill='none' stroke='red'/>" +
            "<ellipse cx='5' cy='5' rx='5' ry='2'/><rect width='10' height='10' rx='2'/></svg>");
        Assert.That(svg, Does.Contain("q 0 0 0 rg 20 10 m 20 15.5228 15.5228 20 10 20 c"));
        Assert.That(svg, Does.Contain("q 0 0 0 rg 0 0 m 4 0 l 4 4 l h f Q\n"));
        Assert.That(svg, Does.Contain("q 1 0 0 RG 1 w 4 M 0 0 m 1 1 l S Q\n"));
        Assert.That(svg, Does.Contain("q 0 0 0 rg 10 5 m 10 6.1046 7.7614 7 5 7 c"));
        Assert.That(svg, Does.Contain("q 0 0 0 rg 2 0 m 8 0 l 9.1046 0 10 0.8954 10 2 c 10 8 l"));
    }

    [Test]
    public void CurrentColorAndColorAttributeUseTheElementColour()
    {
        Assert.That(Svg("<div style='color:#00ff00'><svg width='4' height='4'><path d='M0 0h1v1z' fill='currentColor'/></svg></div>"),
            Does.Contain("q 0 1 0 rg"));
        Assert.That(Svg("<svg width='4' height='4' color='red'><g color='blue'><path d='M0 0h1v1z' fill='currentColor'/></g></svg>"),
            Does.Contain("q 0 0 1 rg"));
        // Author CSS on the svg wins over its color attribute.
        Assert.That(Svg("<svg width='4' height='4' color='red' style='color:#0000ff'><path d='M0 0h1v1z' fill='currentcolor'/></svg>"),
            Does.Contain("q 0 0 1 rg"));
    }

    [Test]
    public void PaintAttributesTakeCssColoursAndCustomProperties()
    {
        Assert.That(Svg("<svg width='4' height='4'><path d='M0 0h1v1z' fill='hsl(240, 100%, 50%)'/></svg>"),
            Does.Contain("q 0 0 1 rg"));
        Assert.That(Svg("<svg width='4' height='4'><path d='M0 0h1v1z' fill='RED'/></svg>"), Does.Contain("q 1 0 0 rg"));
        Assert.That(Svg("<html><head><style>:root{--brand-red:#ff0000}</style></head><body>" +
                "<svg width='4' height='4'><path d='M0 0h1v1z' fill='var(--brand-red)'/></svg></body></html>"),
            Does.Contain("q 1 0 0 rg"));
        Assert.That(Svg("<div style='--ink:#0000ff'><svg width='4' height='4' style='--ink:#00ff00'>" +
                "<path d='M0 0h1v1z' stroke='var(--ink)' fill='var(--none, red)'/></svg></div>"),
            Does.Contain("q 1 0 0 rg 0 1 0 RG"));
        Reject("<svg width='4' height='4'><path d='M0 0h1v1z' fill='var(--brand-red)'/></svg>", "FPDF1202");
    }

    [Test]
    public void ShapesWithoutVisiblePaintEmitNothing()
    {
        const string empty = "<svg width='40' height='20'></svg>";
        foreach (var hidden in new[]
        {
            "<path d='M0 0h1v1z' fill='none'/>",
            "<path d='M0 0h1v1z' fill='transparent' stroke='red' stroke-width='0'/>",
            "<defs><path d='M0 0h1v1z'/></defs>",
            "<g display='none'><path d='M0 0h1v1z'/></g>",
            "<g visibility='hidden'><path d='M0 0h1v1z'/></g>",
            "<rect width='0' height='5'/><circle r='0'/><path d='M1 1'/>",
            "<title>Logo</title><desc>Company mark</desc>",
            "<clipPath id='c'><rect width='5' height='5'/></clipPath>",
        })
            Assert.That(Pdf("<svg width='40' height='20'>" + hidden + "</svg>"), Is.EqualTo(Pdf(empty)), hidden);
        // A visible child of a hidden group still paints, as visibility inherits.
        Assert.That(Svg("<svg width='4' height='4'><g visibility='hidden'><path d='M0 0h1v1z' visibility='visible'/></g></svg>"),
            Does.Contain(" f Q\n"));
    }

    // A clip-path naming an id no element has is ignored, as in Chrome: the logo shape renders unclipped.
    [Test]
    public void MissingClipPathReferenceClipsNothing()
        => Assert.That(Pdf("<svg width='108' height='36' viewBox='0 0 485 164' fill='none' xmlns='http://www.w3.org/2000/svg'>" +
                "<g clip-path='url(#clip0_1)'><path d='M12.3 45.6C40 10 80 10 120 45.6L120 140H12.3Z' fill='#2c323c'/></g></svg>"),
            Is.EqualTo(Pdf("<svg width='108' height='36' viewBox='0 0 485 164' fill='none'>" +
                "<g><path d='M12.3 45.6C40 10 80 10 120 45.6L120 140H12.3Z' fill='#2c323c'/></g></svg>")));

    [Test]
    public void ClipPathClipsInTheReferencingUserSpace()
    {
        // The clip is defined after its use and in another svg; it is placed through the group's
        // translate before the shape's own transform.
        var svg = Svg("<svg width='40' height='20'><g transform='translate(10 0)' clip-path='url(\"#c\")'>" +
            "<rect width='8' height='8' transform='scale(2)'/></g></svg>" +
            "<svg width='1' height='1'><clipPath id='c' clip-rule='evenodd'><rect x='1' y='2' width='3' height='4'/></clipPath></svg>");
        Assert.That(svg, Does.Contain("q 11 2 m 14 2 l 14 6 l 11 6 l h W* n 2 0 0 2 10 0 cm 0 0 0 rg"));
    }

    [Test]
    public void ClipReferencesResolveToTheFirstElementWithTheId()
    {
        // The first element with the id is a div, not a clipPath, so nothing is clipped.
        const string shape = "<rect width='9' height='9'/>";
        Assert.That(Pdf("<div id='c'></div><svg width='10' height='10'><clipPath id='c'><rect width='1' height='1'/></clipPath>" +
                "<rect width='9' height='9' clip-path='url(#c)'/></svg>"),
            Is.EqualTo(Pdf("<div id='c'></div><svg width='10' height='10'><clipPath id='d'><rect width='1' height='1'/></clipPath>" + shape + "</svg>")));
        // An empty clipPath clips everything away.
        Assert.That(Pdf("<svg width='10' height='10'><clipPath id='e'/><rect width='9' height='9' clip-path='url(#e)'/></svg>"),
            Is.EqualTo(Pdf("<svg width='10' height='10'></svg>")));
    }

    [Test]
    public void SvgsInTableCellsAndAfterTextPaintAtTheirBoxes()
    {
        var page = Layout(TableLayoutTests.Table("<svg width='20' height='8'><rect width='20' height='8'/></svg>")).Single();
        Assert.That(page.Images.Single().Vector, Is.Not.Null);
        Assert.That((page.Images.Single().Width, page.Images.Single().Height), Is.EqualTo((15d, 6d)));
        var pdf = Pdf("<p>Above</p><svg width='4' height='4'><rect width='4' height='4'/></svg><p>Below</p>");
        Assert.That(pdf, Does.Contain(" re W n 0.75 0 0 -0.75 "));
        Assert.That(pdf, Does.Not.Contain("/XObject"));
    }

    // Sizing ------------------------------------------------------------------------------------

    [TestCase("<svg width='108' height='36'></svg>", 81, 27)]
    [TestCase("<svg width='108px' height='36px' viewBox='0 0 485 164'></svg>", 81, 27)]
    // No viewBox: a missing dimension takes the 300 by 150 px replaced-element default (Chrome).
    [TestCase("<svg width='108'></svg>", 81, 112.5)]
    [TestCase("<svg height='36'></svg>", 225, 27)]
    [TestCase("<svg></svg>", 225, 112.5)]
    // With a viewBox: a missing dimension follows its ratio; with neither, the svg fills the width.
    [TestCase("<svg width='108' viewBox='0 0 100 50'></svg>", 81, 40.5)]
    [TestCase("<svg height='36' viewBox='0 0 100 50'></svg>", 54, 27)]
    [TestCase("<div style='width:200pt'><svg viewBox='0 0 100 50'></svg></div>", 200, 100)]
    // The attributes are presentational hints: CSS width replaces the width and keeps the height.
    [TestCase("<svg width='108' height='36' style='width:162pt'></svg>", 162, 27)]
    [TestCase("<svg width='108' height='36' style='max-width:30pt' viewBox='0 0 485 164'></svg>", 30, 27)]
    [TestCase("<div style='width:200pt'><svg width='50%' height='36'></svg></div>", 100, 27)]
    [TestCase("<svg width='auto' height='36' viewBox='0 0 100 50'></svg>", 54, 27)]
    public void SvgSizeFollowsAttributesViewBoxAndCss(string html, double width, double height)
    {
        var (actualWidth, actualHeight) = Size(html);
        Assert.That(actualWidth, Is.EqualTo(width).Within(1e-9));
        Assert.That(actualHeight, Is.EqualTo(height).Within(1e-9));
    }

    [Test]
    public void PreserveAspectRatioPlacesTheViewBox()
    {
        // A 10x10 viewBox in a 30x15pt box: meet scales by 1.5 and centres 7.5pt in.
        const string svg = "<svg width='40' height='20' viewBox='0 0 10 10'{0}><rect width='1' height='1'/></svg>";
        Assert.That(Svg(string.Format(CultureInfo.InvariantCulture, svg, "")), Does.StartWith("q 36 790.89 30 15 re W n 1.5 0 0 -1.5 43.5 805.89 cm\n"));
        Assert.That(Svg(string.Format(CultureInfo.InvariantCulture, svg, " preserveAspectRatio='xMinYMid meet'")), Does.StartWith("q 36 790.89 30 15 re W n 1.5 0 0 -1.5 36 805.89 cm\n"));
        Assert.That(Svg(string.Format(CultureInfo.InvariantCulture, svg, " preserveAspectRatio='xMidYMax slice'")), Does.StartWith("q 36 790.89 30 15 re W n 3 0 0 -3 36 820.89 cm\n"));
        Assert.That(Svg(string.Format(CultureInfo.InvariantCulture, svg, " preserveAspectRatio='none'")), Does.StartWith("q 36 790.89 30 15 re W n 3 0 0 -1.5 36 805.89 cm\n"));
    }

    [Test]
    public void BoxDecorationAndResetsApplyLikeAnImage()
    {
        // A lone svg taller than the line's strut has its top at the line top either way, so with
        // nothing after it display:block paints the same page; a zero margin is its initial value.
        const string plain = "<svg width='40' height='20'><rect width='4' height='4'/></svg>";
        Assert.That(Pdf("<style>*{margin:0;padding:0;box-sizing:border-box}svg{display:block}</style>" + plain),
            Is.EqualTo(Pdf(plain)));
        // An inline svg's padding, border and background travel with it as its decoration.
        var page = Layout("<svg width='40' height='20' style='padding:2pt;border:1pt solid red'></svg>").Single();
        Assert.That(page.Images.Single().X, Is.EqualTo(39));
        Assert.That(page.Images.Single().Decoration!.Width, Is.EqualTo(36));
        Assert.That(page.PaintBoxes, Is.Empty);
        var block = Layout("<svg width='40' height='20' style='display:block;padding:2pt;border:1pt solid red'></svg>").Single();
        Assert.That(block.Images.Single().X, Is.EqualTo(39));
        Assert.That(block.PaintBoxes.Single().Width, Is.EqualTo(36));
    }

    // Failure codes -------------------------------------------------------------------------------

    [TestCase("<svg><text>Logo</text></svg>")]
    [TestCase("<svg><use href='#a'/></svg>")]
    [TestCase("<svg><defs><linearGradient id='g'/></defs></svg>")]
    [TestCase("<svg><svg></svg></svg>")]
    [TestCase("<svg><style>rect{}</style></svg>")]
    [TestCase("<svg><clipPath id='c'><g></g></clipPath></svg>")]
    [TestCase("<svg><path d='M0 0h1'><g></g></path></svg>")]
    [TestCase("<math></math>")]
    public void UnsupportedSvgElementsFailFpdf1102(string html) => Reject(html, "FPDF1102");

    [TestCase("<svg><path d='M0 0h1v1z' fill-opacity='half'/></svg>")]
    [TestCase("<svg opacity='50%%'></svg>")]
    [TestCase("<svg><path d='M0 0h1v1z' fill='url(#gradient)'/></svg>")]
    [TestCase("<svg><path d='M0 0h1v1z' stroke='red' stroke-dasharray='2 1'/></svg>")]
    [TestCase("<svg xmlns='http://www.w3.org/1999/xhtml'></svg>")]
    [TestCase("<svg height='50%'></svg>")]
    [TestCase("<svg width='-1'></svg>")]
    [TestCase("<svg viewBox='0 0 0 10'></svg>")]
    [TestCase("<svg viewBox='0 0 10'></svg>")]
    [TestCase("<svg preserveAspectRatio='xMidYMid stretch'></svg>")]
    [TestCase("<svg overflow='visible'></svg>")]
    [TestCase("<svg transform='scale(2)'></svg>")]
    [TestCase("<svg><clipPath id='c'><rect width='1' height='1'/><rect width='2' height='2'/></clipPath></svg>")]
    [TestCase("<svg><clipPath id='c' clipPathUnits='objectBoundingBox'/></svg>")]
    [TestCase("<svg><rect width='1' height='1' mask='url(#m)'/></svg>")]
    [TestCase("<svg><rect width='1' height='1' clip-path='circle(5px)'/></svg>")]
    [TestCase("<svg><rect width='1' height='1' transform='spin(3)'/></svg>")]
    [TestCase("<svg><circle r='-1'/></svg>")]
    [TestCase("<svg><polygon points='0 0 1'/></svg>")]
    [TestCase("<svg><rect width='10%' height='1'/></svg>")]
    [TestCase("<svg><path d='M0 0 L5'/></svg>")]
    [TestCase("<svg><path d='M0 0h1v1z' stroke-linejoin='arcs'/></svg>")]
    [TestCase("<svg><path d='M0 0h1v1z' fill='red !important'/></svg>")]
    public void UnsupportedSvgAttributesAndValuesFailFpdf1103(string html) => Reject(html, "FPDF1103");

    [TestCase("<p>Logo <svg width='4' height='4' style='display:block'></svg></p>")]
    [TestCase("<div><span><svg style='display:block'></svg></span></div>")]
    [TestCase("<ul><li><svg style='display:block'></svg></li></ul>")]
    [TestCase("<svg><g></svg>")]
    [TestCase("<svg><g></g></g></svg>")]
    [TestCase("<svg><title><g/></title></svg>")]
    public void MisplacedOrMalformedSvgFailsFpdf1101(string html) => Reject(html, "FPDF1101");

    [TestCase("<style>*{color:red}</style><svg><path d='M0 0h1v1z' fill='currentColor'/></svg>")]
    [TestCase("<style>path{display:none}</style><svg><path d='M0 0h1v1z'/></svg>")]
    [TestCase("<style>rect{width:5px}</style><svg><rect width='1' height='1'/></svg>")]
    [TestCase("<style>g{--brand:red}</style><svg><g></g></svg>")]
    [TestCase("<svg><path d='M0 0h1v1z' style='fill:red'/></svg>")]
    [TestCase("<svg style='display:none'></svg>")]
    public void CssThatWouldChangeSvgContentFailsFpdf1201(string html) => Reject(html, "FPDF1201");

    [Test]
    public void CssWithoutEffectOnSvgContentIsIgnored()
    {
        const string plain = "<svg width='4' height='4'><g><path d='M0 0h1v1z' fill='#123456'/></g></svg>";
        Assert.That(Pdf("<style>*{color:red;margin:0;padding:0;font-size:20pt}</style>" + plain),
            Is.EqualTo(Pdf("<style>*{margin:0;padding:0;font-size:20pt}</style>" + plain)));
    }

    [TestCase("<p>Logo <svg width='4' height='4'></svg></p>")]
    [TestCase("<div><span><svg width='4' height='4'></svg></span></div>")]
    [TestCase("<ul><li><svg width='4' height='4'></svg></li></ul>")]
    public void DefaultInlineSvgRendersInsideParagraphsInlineElementsAndListItems(string html)
        => Assert.That(Layout(html).Single().Images.Single().Vector, Is.Not.Null);

    [Test]
    public void SvgNestingCountsTowardsMaxDepth()
    {
        Reject("<svg><g><g><g></g></g></g></svg>", "FPDF1003", new PdfOptions { MaxDepth = 3 });
        Assert.That(Pdf("<svg><g><g></g></g></svg>", new PdfOptions { MaxDepth = 3 }), Does.StartWith("%PDF"));
    }

    [Test]
    public void SvgPathSegmentsCountAsDisplayCommands()
    {
        const string svg = "<svg width='4' height='4'><path d='M0 0 1 1 2 2 3 3 4 4'/></svg>";
        Assert.That(Pdf(svg, new PdfOptions { MaxDisplayCommands = 6 }), Does.StartWith("%PDF"));
        Reject(svg, "FPDF1401", new PdfOptions { MaxDisplayCommands = 5 });
        Reject("<svg width='4' height='4'><path d='M0 0 1 1 2 2'/></svg><svg width='4' height='4'><path d='M0 0 1 1 2 2'/></svg>",
            "FPDF1401", new PdfOptions { MaxDisplayCommands = 7 });
    }

    [Test]
    public void FailuresPointAtTheSvgContentElement()
    {
        const string html = "<p>Before</p>\n<svg width='4' height='4'><path d='M0 0 Q'/></svg>";
        var failure = Reject(html, "FPDF1103");
        Assert.That(failure.SourceOffset, Is.EqualTo(html.IndexOf("<path", StringComparison.Ordinal)));
    }
}
