using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ExtensionsToStringBuilderTests
{
    private static readonly double[] Values =
    [
        0, -0.0, 1, -1, 0.5, 0.0004, -0.0004, 0.0005, 1.23456789, -841.889764, 595.2756, 14400,
        1e-7, 123456789.987654321, 1e20, -1e300, double.MaxValue, double.Epsilon,
        double.NaN, double.PositiveInfinity, double.NegativeInfinity,
    ];

    [TestCase("0.###")]
    [TestCase("0.######")]
    public void AppendInvariantMatchesToString(string format)
    {
        foreach (var value in Values)
        {
            var output = new StringBuilder("x");
            output.AppendInvariant(value, format);
            Assert.That(output.ToString(), Is.EqualTo("x" + value.ToString(format, CultureInfo.InvariantCulture)), value.ToString("R", CultureInfo.InvariantCulture));
        }
    }

    [Test]
    public void AppendInvariantIgnoresTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.That(new StringBuilder().AppendInvariant(-1.5, "0.###").ToString(), Is.EqualTo("-1.5"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase(0, 2)]
    [TestCase(0x0A, 2)]
    [TestCase(0xFF, 2)]
    [TestCase(0x4E2D, 2)]
    [TestCase(0x10FFFF, 2)]
    [TestCase(0, 4)]
    [TestCase(0x1F, 4)]
    [TestCase(0xFFFF, 4)]
    [TestCase(0x1F600, 4)]
    public void AppendHexMatchesToString(int value, int digits)
    {
        var output = new StringBuilder("<").AppendHex(value, digits).Append('>');
        Assert.That(output.ToString(), Is.EqualTo("<" + value.ToString("X" + digits, CultureInfo.InvariantCulture) + ">"));
    }
}
