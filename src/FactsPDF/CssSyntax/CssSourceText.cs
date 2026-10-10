using System.Text;

namespace FactsPDF.CssSyntax;

/// <summary>Normalized Unicode code units with exact original UTF-16 boundary positions.</summary>
internal sealed class CssSourceText
{
    private readonly int[] boundaries;

    private CssSourceText(string normalized, int[] boundaries)
    {
        Normalized = normalized;
        this.boundaries = boundaries;
    }

    internal string Normalized { get; }

    internal static CssSourceText Create(
        string source, int absoluteStart, CssSyntaxLimits limits,
        IReadOnlyList<int>? originalOffsets = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        if (absoluteStart < 0) throw new ArgumentOutOfRangeException(nameof(absoluteStart));
        if (limits.MaxCharacters < 1 || limits.MaxNodes < 1 || limits.MaxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(limits), "All CSS syntax budgets must be positive.");
        limits.Cancellation.ThrowIfCancellationRequested();

        if (originalOffsets is null && (long)absoluteStart + source.Length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(absoluteStart), "CSS source offset overflows int.");
        if (originalOffsets is not null)
        {
            if (originalOffsets.Count != source.Length + 1)
                throw new ArgumentException("Expected one original boundary for every UTF-16 code unit plus EOF.", nameof(originalOffsets));
            for (var i = 0; i < originalOffsets.Count; i++)
            {
                if (originalOffsets[i] < 0 || i > 0 && originalOffsets[i] < originalOffsets[i - 1])
                    throw new ArgumentException("Original CSS offsets must be nonnegative and nondecreasing.", nameof(originalOffsets));
            }
        }
        if (source.Length > limits.MaxCharacters)
            throw new CssSyntaxLimitException("CSS source exceeds MaxCharacters.", new CssSourceSpan(absoluteStart, source.Length));

        int Origin(int index) => originalOffsets is null ? absoluteStart + index : originalOffsets[index];
        var text = new StringBuilder(source.Length);
        var mapped = new int[source.Length + 1];
        var count = 0;

        for (var i = 0; i < source.Length;)
        {
            if ((i & 1023) == 0) limits.Cancellation.ThrowIfCancellationRequested();
            var c = source[i];
            mapped[count] = Origin(i);
            if (c == '\r')
            {
                text.Append('\n');
                count++;
                i += i + 1 < source.Length && source[i + 1] == '\n' ? 2 : 1;
            }
            else if (c == '\f')
            {
                text.Append('\n'); count++; i++;
            }
            else if (c == '\0' || char.IsLowSurrogate(c))
            {
                text.Append('\uFFFD'); count++; i++;
            }
            else if (char.IsHighSurrogate(c))
            {
                if (i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
                {
                    text.Append(c); count++; i++;
                    mapped[count] = Origin(i);
                    text.Append(source[i]); count++; i++;
                }
                else
                {
                    text.Append('\uFFFD'); count++; i++;
                }
            }
            else
            {
                text.Append(c); count++; i++;
            }
        }
        mapped[count] = Origin(source.Length);
        return new CssSourceText(text.ToString(), mapped[..(count + 1)]);
    }

    internal CssSourceSpan Span(int normalizedStart, int normalizedLength)
    {
        if (normalizedStart < 0 || normalizedLength < 0 ||
            (long)normalizedStart + normalizedLength > Normalized.Length)
            throw new ArgumentOutOfRangeException(nameof(normalizedStart), "CSS source span is outside normalized input.");
        var start = boundaries[normalizedStart];
        return new CssSourceSpan(start, boundaries[normalizedStart + normalizedLength] - start);
    }
}
