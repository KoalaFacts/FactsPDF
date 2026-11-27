namespace FactsPDF;

/// <summary>
/// A fixed, case-insensitive (ordinal) set of ASCII names that is looked up by span without allocating.
/// On .NET 9 and later it is a <see cref="HashSet{T}"/> alternate lookup; .NET 8 has no alternate
/// lookups, so there it scans the few names linearly.
/// </summary>
internal sealed class SpanStringSet
{
#if NET9_0_OR_GREATER
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public SpanStringSet(IEnumerable<string> values) =>
        _lookup = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase).GetAlternateLookup<ReadOnlySpan<char>>();

    public bool Contains(ReadOnlySpan<char> value) => _lookup.Contains(value);

    /// <summary>Finds the stored spelling of <paramref name="value"/>.</summary>
    public bool TryGetValue(ReadOnlySpan<char> value, out string actual)
    {
        if (_lookup.TryGetValue(value, out var found))
        {
            actual = found;
            return true;
        }

        actual = string.Empty;
        return false;
    }
#else
    private readonly string[] _values;

    public SpanStringSet(IEnumerable<string> values) => _values = [.. new HashSet<string>(values, StringComparer.OrdinalIgnoreCase)];

    public bool Contains(ReadOnlySpan<char> value) => TryGetValue(value, out _);

    /// <summary>Finds the stored spelling of <paramref name="value"/>.</summary>
    public bool TryGetValue(ReadOnlySpan<char> value, out string actual)
    {
        foreach (var candidate in _values)
        {
            if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                actual = candidate;
                return true;
            }
        }

        actual = string.Empty;
        return false;
    }
#endif
}
