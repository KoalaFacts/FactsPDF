using System.Globalization;
using System.Text;

namespace FactsPDF;

/// <summary>One name and integer of a counter-reset, counter-increment or counter-set list.</summary>
internal readonly record struct CounterOp(string Name, int Value);

/// <summary>The computed counter-reset, counter-increment and counter-set of a box (never inherited).</summary>
internal sealed record CounterOps(CounterOp[]? Reset, CounterOp[]? Increment, CounterOp[]? Set)
{
    internal static readonly CounterOps None = new(null, null, null);

    /// <summary>Parses the normalised "name value name value" text of a compiled counter declaration.</summary>
    internal static CounterOp[]? Parse(string value)
    {
        if (value == "none") return null;
        var terms = value.Split(' ');
        var result = new CounterOp[terms.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = new(terms[2 * i], int.Parse(terms[2 * i + 1], CultureInfo.InvariantCulture));
        return result;
    }
}

/// <summary>counter() and counters() items of a content value, kept with the strings around them.</summary>
/// <remarks>
/// A content value without counters is <c>"</c> followed by its text. One with counters is
/// <c>U+0001</c> followed by length-prefixed items, <c>T&lt;length&gt;:text</c>, <c>C&lt;length&gt;:name NUL style</c>
/// and <c>S&lt;length&gt;:name NUL separator NUL style</c>, so no authored text can be mistaken for an item.
/// </remarks>
internal static class CounterContent
{
    internal const char Marker = '\u0001';

    internal static void AppendText(StringBuilder builder, string text) => Append(builder, 'T', text);
    internal static void AppendCounter(StringBuilder builder, string name, string style) =>
        Append(builder, 'C', name + "\0" + style);
    internal static void AppendCounters(StringBuilder builder, string name, string separator, string style) =>
        Append(builder, 'S', name + "\0" + separator + "\0" + style);

    private static void Append(StringBuilder builder, char kind, string payload) =>
        builder.Append(kind).Append(payload.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(payload);

    internal static bool IsEncoded(string value) => value.Length > 0 && value[0] == Marker;

    /// <summary>Evaluates an encoded content value against the counters in scope.</summary>
    internal static string Evaluate(string value, CounterScopes scopes, int offset)
    {
        var result = new StringBuilder();
        var at = 1;
        while (at < value.Length)
        {
            var kind = value[at++];
            var colon = value.IndexOf(':', at);
            var length = int.Parse(value.AsSpan(at, colon - at), CultureInfo.InvariantCulture);
            var payload = value.Substring(colon + 1, length);
            at = colon + 1 + length;
            if (kind == 'T') { result.Append(payload); continue; }
            var parts = payload.Split('\0');
            if (kind == 'C') result.Append(CounterStyles.Format(scopes.Value(parts[0], offset), parts[1]));
            else
            {
                var values = scopes.Values(parts[0], offset);
                for (var i = 0; i < values.Count; i++)
                {
                    if (i > 0) result.Append(parts[1]);
                    result.Append(CounterStyles.Format(values[i], parts[2]));
                }
            }
        }
        return result.ToString();
    }
}

/// <summary>The counter styles of counter() and counters() and how they print an integer.</summary>
internal static class CounterStyles
{
    internal static bool Supported(string style) => style is "decimal" or "decimal-leading-zero" or "lower-alpha"
        or "lower-latin" or "upper-alpha" or "upper-latin" or "lower-roman" or "upper-roman" or "none";

    internal static string Format(int value, string style)
    {
        switch (style)
        {
            case "none": return "";
            case "decimal-leading-zero":
                // Chrome pads 0 to 9 only; a negative number prints as plain decimal.
                return value is >= 0 and < 10
                    ? "0" + value.ToString(CultureInfo.InvariantCulture)
                    : value.ToString(CultureInfo.InvariantCulture);
            case "lower-alpha" or "lower-latin" when value >= 1: return Alphabetic(value, 'a');
            case "upper-alpha" or "upper-latin" when value >= 1: return Alphabetic(value, 'A');
            case "lower-roman" when value is >= 1 and <= 3999: return Roman(value).ToLowerInvariant();
            case "upper-roman" when value is >= 1 and <= 3999: return Roman(value);
            // Values outside an alphabetic or roman style's range print in decimal.
            default: return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string Alphabetic(int value, char first)
    {
        var chars = new StringBuilder(8);
        var ordinal = (long)value;
        while (ordinal > 0)
        {
            ordinal--;
            chars.Insert(0, (char)(first + ordinal % 26));
            ordinal /= 26;
        }
        return chars.ToString();
    }

    private static string Roman(int ordinal)
    {
        var result = new StringBuilder(16);
        foreach (var (value, label) in new (int Value, string Label)[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        })
        {
            while (ordinal >= value) { result.Append(label); ordinal -= value; }
        }
        return result.ToString();
    }
}

/// <summary>
/// The CSS counters in scope while the reader walks the tree in document order. A counter created by
/// an element is in scope for that element, its descendants, its following siblings and their
/// descendants, so it lives in the frame of the element's parent and goes out of scope when that frame
/// closes. A counter created where a sibling already created one of the same name replaces it; created
/// below an outer one it nests inside it (counters() lists them outermost first).
/// </summary>
internal sealed class CounterScopes
{
    private sealed class Instance(string name, int value, int frame, bool reversed, bool own = false)
    {
        internal readonly string Name = name;
        internal int Value = value;
        internal readonly int Frame = frame;
        internal readonly bool Reversed = reversed;
        // Created under an outer counter of the same name: in scope for the creating element's own
        // frame only, so a counter an element's children create is never a sibling that replaces it.
        internal readonly bool Own = own;
    }

    private readonly List<Instance> instances = [];
    // The index of the innermost open frame: counters are created in it.
    private int top;

    internal void SetTop(int frame) => top = frame;

    /// <summary>Drops the counters created in frames deeper than <paramref name="frame"/>, which closed.</summary>
    internal void Close(int frame)
    {
        top = frame;
        while (instances.Count > 0 && instances[^1].Frame > frame) instances.RemoveAt(instances.Count - 1);
    }

    private Instance? Innermost(string name)
    {
        for (var i = instances.Count - 1; i >= 0; i--)
            if (instances[i].Name == name) return instances[i];
        return null;
    }

    private Instance Create(string name, int value, bool reversed = false)
    {
        for (var i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i].Name != name) continue;
            // A counter of the same name created in this frame (by a sibling) is replaced in place.
            if (instances[i].Frame > top || instances[i].Frame == top && !instances[i].Own)
            {
                var replacement = new Instance(name, value, instances[i].Frame, reversed, instances[i].Own);
                instances[i] = replacement;
                return replacement;
            }
            // Below a counter of the same name from an outer frame the new one nests inside it. A
            // following sibling inherits the outer counter instead (a sibling's counter is inherited only
            // when no counter of its name came from the parent), so the nested one is in scope for the
            // creating element and its descendants alone: it belongs to that element's own frame.
            var nested = new Instance(name, value, top + 1, reversed, own: true);
            instances.Add(nested);
            return nested;
        }
        var created = new Instance(name, value, top, reversed);
        instances.Add(created);
        return created;
    }

    private static int Clamp(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    internal void Reset(string name, int value, bool reversed = false) => Create(name, value, reversed);

    internal void Increment(string name, int delta)
    {
        var counter = Innermost(name) ?? Create(name, 0);
        counter.Value = Clamp((long)counter.Value + delta);
    }

    internal void Set(string name, int value) => (Innermost(name) ?? Create(name, 0)).Value = value;

    internal int Value(string name, int offset) => Visible(name, offset).Value;

    private Instance Visible(string name, int offset)
    {
        var counter = Innermost(name) ?? Create(name, 0);
        if (counter.Reversed)
            throw new FactsPdfException("FPDF1201", "The list-item counter of an ol with reversed is not supported.", offset);
        return counter;
    }

    internal List<int> Values(string name, int offset)
    {
        _ = Visible(name, offset);
        var values = new List<int>(2);
        foreach (var instance in instances)
            if (instance.Name == name) values.Add(instance.Value);
        return values;
    }

    /// <summary>Applies an element's counter-reset, counter-increment and counter-set in that order.</summary>
    internal void Apply(CounterOps ops)
    {
        if (ops.Reset is { } reset) foreach (var op in reset) Reset(op.Name, op.Value);
        if (ops.Increment is { } increment) foreach (var op in increment) Increment(op.Name, op.Value);
        if (ops.Set is { } set) foreach (var op in set) Set(op.Name, op.Value);
    }
}
