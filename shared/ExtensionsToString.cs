namespace FactsPDF.Text;

// Source-linked by the CLI and its tests, not part of the core library API.

internal static class ExtensionsToString
{
    /// <summary>
    /// Splits text into lines the way editors number them: CRLF (Windows) is checked first and counts
    /// as one break, then a lone CR (classic Mac) or LF (Unix). Unlike <see cref="MemoryExtensions.EnumerateLines"/>,
    /// form feed, NEL and U+2028/U+2029 do not end a line. Each range excludes its terminator; text
    /// ending in a break yields a final empty line.
    /// </summary>
    internal static IEnumerable<Range> SplitLines(this string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            yield return start..i;
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }
        yield return start..text.Length;
    }

    /// <summary>One-based line and column of a UTF-16 offset; a surrogate pair counts as one column.</summary>
    internal static (int Line, int Column) LineAndColumnAt(this string text, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, text.Length);
        var line = 0;
        foreach (var range in text.SplitLines())
        {
            line++;
            var (start, length) = range.GetOffsetAndLength(text.Length);
            if (offset > start + length) continue;
            var column = 1;
            for (var i = start; i < offset; i++)
                if (!char.IsLowSurrogate(text[i]) || i == start || !char.IsHighSurrogate(text[i - 1])) column++;
            return (line, column);
        }
        throw new InvalidOperationException("The final line always ends at text.Length.");
    }
}
