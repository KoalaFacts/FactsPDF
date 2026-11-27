using System.Text;

namespace FactsPDF;

/// <summary>
/// The constant-alpha graphics states one page's content uses. Every translucent paint selects
/// the page's single ExtGState for its alpha byte, named /A followed by the two hex digits, which
/// sets both the fill (/ca) and stroke (/CA) alpha; each paint is enclosed in q/Q, so the state
/// never leaks into later operators. An opaque colour selects nothing and keeps its legacy bytes.
/// A plain struct of four bit words passed by reference: pages without translucency allocate nothing.
/// </summary>
internal struct PdfAlphaStates
{
    private ulong used0, used1, used2, used3;

    internal readonly bool Any => (used0 | used1 | used2 | used3) != 0;

    internal void Clear() => used0 = used1 = used2 = used3 = 0;

    /// <summary>Appends "q " and, for a translucent colour, the gs operator selecting its alpha.</summary>
    internal StringBuilder BeginPaint(StringBuilder output, Rgb color) => Select(output.Append("q "), color);

    /// <summary>Appends the gs operator selecting a translucent colour's alpha; nothing when opaque.</summary>
    internal StringBuilder Select(StringBuilder output, Rgb color)
    {
        if (color.A >= 1) return output;
        var alpha = AlphaByte(color);
        switch (alpha >> 6)
        {
            case 0: used0 |= 1UL << alpha; break;
            case 1: used1 |= 1UL << (alpha - 64); break;
            case 2: used2 |= 1UL << (alpha - 128); break;
            default: used3 |= 1UL << (alpha - 192); break;
        }
        return output.Append("/A").AppendHex(alpha, 2).Append(" gs ");
    }

    /// <summary>Appends " /ExtGState &lt;&lt; ... &gt;&gt;" with one entry per alpha used, in byte order.</summary>
    internal readonly void AppendResources(StringBuilder page)
    {
        if (!Any) return;
        page.Append(" /ExtGState <<");
        for (var alpha = 0; alpha < 255; alpha++)
        {
            var word = (alpha >> 6) switch { 0 => used0, 1 => used1, 2 => used2, _ => used3 };
            if ((word & (1UL << (alpha & 63))) == 0) continue;
            page.Append(" /A").AppendHex(alpha, 2).Append(" << /CA ").AppendInvariant(alpha / 255d, Number)
                .Append(" /ca ").AppendInvariant(alpha / 255d, Number).Append(" >>");
        }
        page.Append(" >>");
    }

    internal static int AlphaByte(Rgb color) =>
        (int)Math.Round(Math.Clamp(color.A, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private const string Number = "0.####";
}
