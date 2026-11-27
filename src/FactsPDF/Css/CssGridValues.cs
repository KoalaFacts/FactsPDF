using System.Globalization;

namespace FactsPDF;

internal enum GridTrackKind { Fixed, Fraction, Auto }

/// <summary>One explicit grid column: a length or percentage, a flexible fraction or auto.</summary>
internal readonly record struct GridTrack(GridTrackKind Kind, CssLength Length = default, double Fraction = 0);

/// <summary>
/// The grid column list the report renderer lays out: fixed lengths and percentages, fr
/// fractions and auto, one track per value. repeat(), minmax(), fit-content(), min-content,
/// max-content and named lines fail with FPDF1202.
/// </summary>
internal static class CssGridValues
{
    internal const int MaxTracks = 64;

    // Validates and normalizes one track term as written (lower case, no CSS-wide keyword).
    internal static GridTrack Track(string term, int offset, double? rootFontSize = null, double? elementFontSize = null)
    {
        if (term == "auto") return new(GridTrackKind.Auto);
        if (term.EndsWith("fr", StringComparison.Ordinal))
        {
            if (!double.TryParse(term.AsSpan(0, term.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction)
                || !double.IsFinite(fraction) || fraction < 0 || fraction > 1_000_000)
                throw new FactsPdfException("FPDF1202", "A grid fr track needs a nonnegative number.", offset);
            return new(GridTrackKind.Fraction, Fraction: fraction);
        }
        if (term is "min-content" or "max-content")
            throw new FactsPdfException("FPDF1202",
                "grid-template-columns supports lengths, percentages, fr and auto; min-content and max-content tracks are not supported.", offset);
        return new(GridTrackKind.Fixed, CssBoxValues.Length(term, offset, rootFontSize, allowRem: true,
            elementFontSize: elementFontSize, allowEm: true));
    }

    // null is none: a single implicit auto column.
    internal static GridTrack[]? Columns(string value, int offset, double? rootFontSize = null, double? elementFontSize = null)
    {
        if (value == "none") return null;
        var terms = value.Split(' ');
        if (terms.Length > MaxTracks)
            throw new FactsPdfException("FPDF1202", $"grid-template-columns supports at most {MaxTracks} tracks.", offset);
        var tracks = new GridTrack[terms.Length];
        for (var i = 0; i < terms.Length; i++) tracks[i] = Track(terms[i], offset, rootFontSize, elementFontSize);
        return tracks;
    }

    // justify-items/justify-self keywords; null is justify-self:auto.
    internal static FlexAlign? Justify(string value, int offset, bool self) => value switch
    {
        "auto" when self => null,
        "normal" or "stretch" => FlexAlign.Stretch,
        "start" or "self-start" or "flex-start" or "left" => FlexAlign.Start,
        "end" or "self-end" or "flex-end" or "right" => FlexAlign.End,
        "center" => FlexAlign.Center,
        _ => throw new FactsPdfException("FPDF1202", (self ? "justify-self supports auto, " : "justify-items supports ")
            + "normal, stretch, start, end, center, left and right.", offset)
    };
}
