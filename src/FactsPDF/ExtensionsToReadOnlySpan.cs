#if !NET9_0_OR_GREATER
// Extensions live in the namespace of the type they extend, so callers need no extra using.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130

/// <summary>
/// .NET 8 stand-ins for the <see cref="MemoryExtensions"/> span members that arrived in .NET 9, with the
/// same results, so the library source compiles unchanged on every target. .NET 9 and later use the
/// runtime's own members.
/// </summary>
internal static class ExtensionsToReadOnlySpan
{
    /// <summary>Whether <paramref name="span"/> starts with <paramref name="value"/>, like .NET 9's <c>MemoryExtensions.StartsWith</c>.</summary>
    internal static bool StartsWith(this ReadOnlySpan<char> span, char value) => !span.IsEmpty && span[0] == value;

    /// <summary>
    /// Splits <paramref name="span"/> at any of <paramref name="separators"/> (white space when it is empty) into
    /// ranges, exactly like .NET 9's <c>MemoryExtensions.SplitAny</c>: adjacent, leading and trailing separators
    /// yield empty ranges, and an empty span yields one empty range.
    /// </summary>
    internal static SpanSplitAnyEnumerator SplitAny(this ReadOnlySpan<char> span, ReadOnlySpan<char> separators) => new(span, separators);

    internal ref struct SpanSplitAnyEnumerator
    {
        private readonly ReadOnlySpan<char> _span;
        private readonly ReadOnlySpan<char> _separators;
        private int _start;
        private bool _done;

        internal SpanSplitAnyEnumerator(ReadOnlySpan<char> span, ReadOnlySpan<char> separators)
        {
            _span = span;
            _separators = separators;
        }

        public Range Current { get; private set; }

        public readonly SpanSplitAnyEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_done) return false;
            var rest = _span[_start..];
            var index = _separators.IsEmpty ? IndexOfWhiteSpace(rest) : rest.IndexOfAny(_separators);
            if (index < 0)
            {
                Current = _start.._span.Length;
                _done = true;
                return true;
            }

            Current = _start..(_start + index);
            _start += index + 1;
            return true;
        }

        private static int IndexOfWhiteSpace(ReadOnlySpan<char> text)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsWhiteSpace(text[i])) return i;
            return -1;
        }
    }
}
#endif
