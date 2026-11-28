using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A printed page shows an animation at time zero. An animation that has started shows the keyframe its
/// direction starts from; one still waiting for its delay shows its first keyframe only with a backwards
/// fill; one that has already ended shows its last keyframe only with a forwards fill. The state is laid
/// over the cascade as the animations origin: above every normal declaration, below !important. States
/// between the keyframes, and keyframe values the renderer cannot honour, fail explicitly. Each case is
/// byte-identical to the page where the shown keyframe's declarations are written on the element.
/// </summary>
[TestFixture]
public sealed class AnimationKeyframeStateTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Keyframes = "@keyframes k{from{background:#f00}to{background:#00f}} @keyframes k2{from{background:#0a0}to{background:#fa0}}";

    private static string Animated(string animation, string rules = "") =>
        $"<style>{Keyframes} div{{height:30pt}} .a{{{animation}}} {rules}</style><div class=a>T</div>";

    private static string Static(string declaration) =>
        $"<style>div{{height:30pt}} .a{{{declaration}}}</style><div class=a>T</div>";

    private const string From = "background:#f00", To = "background:#00f", Nothing = "";

    [TestCase("animation:k 1s", From)]
    [TestCase("animation:k 1s reverse", To)]
    [TestCase("animation:k 1s alternate", From)]
    [TestCase("animation:k 1s alternate-reverse", To)]
    [TestCase("animation:k 1s infinite", From)]
    [TestCase("animation:k 1s paused", From)]
    [TestCase("animation:k 1s ease-in", From)]
    [TestCase("animation:k 1s cubic-bezier(0.5, 0, 0.5, 1)", From)]
    [TestCase("animation:k 1s steps(2, end)", From)]
    [TestCase("animation:k 1s 2s", Nothing)]
    [TestCase("animation:k 1s 2s backwards", From)]
    [TestCase("animation:k 1s 2s backwards reverse", To)]
    [TestCase("animation:k 1s 2s forwards", Nothing)]
    [TestCase("animation:k 1s 2s both", From)]
    [TestCase("animation:k 0s both", To)]
    [TestCase("animation:k 0s both reverse", From)]
    [TestCase("animation:k 0s forwards", To)]
    [TestCase("animation:k 0s", Nothing)]
    [TestCase("animation:k auto forwards", To)]
    [TestCase("animation:k 1s -1s both", To)]
    [TestCase("animation:k 1s -1s", Nothing)]
    [TestCase("animation:k 1s -2s 2 forwards", To)]
    [TestCase("animation:k 1s -2s 2 alternate both", From)]
    [TestCase("animation:k 1s -2s 2 alternate-reverse both", To)]
    [TestCase("animation:k 1s 0s 0 both", From)]
    [TestCase("animation:k 1s steps(1, start)", To)]
    [TestCase("animation:k 1s step-start", To)]
    [TestCase("animation:k 1s steps(1, jump-start)", To)]
    [TestCase("animation:k 500ms 1000ms backwards", From)]
    [TestCase("ANIMATION:k 1s", From)]
    [TestCase("-webkit-animation:k 1s", From)]
    [TestCase("animation:nope 1s", Nothing)]
    [TestCase("animation:K 1s", Nothing)]
    [TestCase("animation:none", Nothing)]
    [TestCase("animation:\"k\" 1s", From)]
    [TestCase("animation-name:k;animation-duration:1s", From)]
    [TestCase("animation-name:k;animation-duration:0s;animation-fill-mode:forwards", To)]
    [TestCase("animation:k 1s;animation-name:none", Nothing)]
    [TestCase("animation:k 1s;animation-name:k2", "background:#0a0")]
    [TestCase("animation:k 1s;animation-duration:0s;animation-fill-mode:both", To)]
    [TestCase("animation:k 1s, k2 1s", "background:#0a0")]
    [TestCase("animation:k2 1s, k 1s", From)]
    [TestCase("animation:k 1s 2s, k2 1s", "background:#0a0")]
    [TestCase("animation:k 1s, k2 1s 2s", From)]
    [TestCase("animation-name:k, k2;animation-duration:1s, 1s;animation-delay:0s, 3s;animation-fill-mode:none, backwards", "background:#0a0")]
    public void ShowsTheKeyframeStateAtTimeZero(string animation, string shown)
        => Assert.That(Pdf(Animated(animation)), Is.EqualTo(Pdf(Static(shown))));

    [Test]
    public void TheAnimationsOriginSitsBetweenNormalAndImportantDeclarations()
    {
        Assert.That(Pdf(Animated("animation:k 1s", ".a{background:#0a0}#x{background:#0a0}")), Is.EqualTo(Pdf(Static(From))));
        Assert.That(Pdf($"<style>{Keyframes} div{{height:30pt}} .a{{animation:k 1s}}</style><div class=a style='background:#0a0'>T</div>"),
            Is.EqualTo(Pdf(Static(From))));
        Assert.That(Pdf(Animated("animation:k 1s", ".a{background:#0a0!important}")), Is.EqualTo(Pdf(Static("background:#0a0"))));
        Assert.That(Pdf($"<style>{Keyframes} div{{height:30pt}} .a{{animation:k 1s}}</style><div class=a style='background:#0a0!important'>T</div>"),
            Is.EqualTo(Pdf(Static("background:#0a0"))));
    }

    [Test]
    public void KeyframeDeclarationsMarkedImportantAreIgnored()
        => Assert.That(Pdf($"<style>@keyframes z{{from{{background:#f00!important}}}} div{{height:30pt}} .a{{animation:z 1s}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(Nothing))));

    [Test]
    public void ALaterKeyframesRuleOfTheSameNameWinsAndOnlyTheFromAndToOffsetsCount()
    {
        Assert.That(Pdf($"<style>@keyframes z{{from{{background:#f00}}}} @keyframes z{{from{{background:#00f}}}} div{{height:30pt}} .a{{animation:z 1s}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(To))));
        Assert.That(Pdf($"<style>@keyframes z{{0%{{background:#f00}} 50%{{background:#0a0}} 100%{{background:#00f}}}} div{{height:30pt}} .a{{animation:z 1s}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(From))));
        Assert.That(Pdf($"<style>@keyframes z{{0%,100%{{background:#f00}}}} div{{height:30pt}} .a{{animation:z 0s forwards}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(From))));
        // Only a middle keyframe: both ends keep the cascaded value.
        Assert.That(Pdf($"<style>@keyframes z{{50%{{background:#f00}}}} div{{height:30pt}} .a{{animation:z 0s both}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(Nothing))));
        Assert.That(Pdf($"<style>@keyframes z{{from{{background:#f00}}}} div{{height:30pt}} .a{{animation:z 0s forwards}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(Nothing))));
    }

    [Test]
    public void KeyframesInsideMediaPrintAndTheirPrefixedSpellingApply()
    {
        Assert.That(Pdf($"<style>@media print{{@keyframes z{{from{{background:#f00}}}}}} div{{height:30pt}} .a{{animation:z 1s}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(From))));
        Assert.That(Pdf($"<style>@-webkit-keyframes z{{from{{background:#f00}}}} div{{height:30pt}} .a{{animation:z 1s}}</style><div class=a>T</div>"),
            Is.EqualTo(Pdf(Static(From))));
    }

    [Test]
    public void ASpinnerShowsItsStartingTransformWhichChangesNothing()
    {
        const string spin = "@keyframes spin{from{transform:rotate(0deg)}to{transform:rotate(360deg)}}";
        var plain = Pdf("<style>div{height:30pt;background:#cde}</style><div class=a>T</div>");
        Assert.That(Pdf($"<style>{spin} div{{height:30pt;background:#cde}} .a{{animation:spin 1s linear infinite}}</style><div class=a>T</div>"), Is.EqualTo(plain));
        // A whole turn is the identity too, so even the end state prints like none.
        Assert.That(Pdf($"<style>{spin} div{{height:30pt;background:#cde}} .a{{animation:spin 0s forwards}}</style><div class=a>T</div>"), Is.EqualTo(plain));
    }

    [Test]
    public void AFadeInShowsItsInvisibleStartAndFailsBecauseOpacityIsNotRendered()
    {
        const string fade = "@keyframes fade{from{opacity:0}to{opacity:1}}";
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<style>{fade}.a{{animation:fade 1s}}</style><p class=a>T</p>"))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("@keyframes fade"));
        // Its end state is the opacity already in effect, so it prints like the page without it.
        Assert.That(Pdf($"<style>{fade}.a{{animation:fade 0s forwards}}</style><p class=a>T</p>"), Is.EqualTo(Pdf("<p class=a>T</p>")));
        // The state is only compiled when an animation shows it.
        Assert.That(Pdf($"<style>{fade}.a{{animation:fade 1s 5s}}</style><p class=a>T</p>"), Is.EqualTo(Pdf("<p class=a>T</p>")));
        Assert.That(Pdf($"<style>{fade}</style><p class=a>T</p>"), Is.EqualTo(Pdf("<p class=a>T</p>")));
    }

    [TestCase("animation:k 1s -0.5s")]
    [TestCase("animation:k 1s -1.5s 3")]
    [TestCase("animation:k 1s -3s 2.5 forwards")]
    [TestCase("animation:k 1s -2.5s 3 both")]
    [TestCase("animation:k 1s steps(2, start)")]
    [TestCase("animation:k 1s steps(2, jump-both)")]
    [TestCase("animation:k 1s steps(3, start) -1s 3 both")]
    public void StatesBetweenTheKeyframesFailExplicitly(string animation)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(Animated(animation)))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("between its keyframes"));
    }

    [Test]
    public void InheritingAnAnimationFailsWhenKeyframesExistAndIsIgnoredOtherwise()
    {
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf($"<style>{Keyframes} .a{{animation:inherit}}</style><div class=a>T</div>"))!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(Pdf("<style>.a{animation:inherit}</style><div class=a>T</div>"), Is.EqualTo(Pdf("<div class=a>T</div>")));
    }

    [Test]
    public void AnimationsOnUnstyledElementsAndInlineStylesApply()
    {
        Assert.That(Pdf($"<style>{Keyframes} div{{height:30pt}}</style><div style='animation:k 1s'>T</div>"),
            Is.EqualTo(Pdf("<style>div{height:30pt}</style><div style='background:#f00'>T</div>")));
    }
}
