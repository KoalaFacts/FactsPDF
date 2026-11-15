using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace FactsPDF;

/// <summary>A start tag's attributes in source order, as one exact-size array. Names are already
/// lower-cased by the tokenizer and unique; lookups ignore ASCII case as the earlier map did. Most
/// tags carry one or two attributes, so a linear scan beats hashing; a tag with more than
/// <see cref="IndexedAbove"/> attributes also keeps a map so a lookup stays constant-time.</summary>
internal sealed class HtmlAttributes : IReadOnlyDictionary<string, string>
{
    internal const int IndexedAbove = 16;

    // Start tags without attributes share this instance.
    internal static readonly HtmlAttributes None = new([]);

    private readonly KeyValuePair<string, string>[] items;
    private readonly Dictionary<string, string>? index;

    internal HtmlAttributes(KeyValuePair<string, string>[] items)
    {
        this.items = items;
        if (items.Length <= IndexedAbove) return;
        index = new(items.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Length; i++) index.Add(items[i].Key, items[i].Value);
    }

    public int Count => items.Length;

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        if (index is not null) return index.TryGetValue(key, out value);
        var items = this.items;
        for (var i = 0; i < items.Length; i++)
        {
            if (!string.Equals(items[i].Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            value = items[i].Value;
            return true;
        }
        value = null;
        return false;
    }

    public string? GetValueOrDefault(string key) => TryGetValue(key, out var value) ? value : null;

    public string this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

    public bool ContainsKey(string key) => TryGetValue(key, out _);

    public IEnumerable<string> Keys => items.Select(item => item.Key);

    public IEnumerable<string> Values => items.Select(item => item.Value);

    // A struct enumerator, so the reader's foreach allocates nothing.
    public Enumerator GetEnumerator() => new(items);

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, string>>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    internal struct Enumerator(KeyValuePair<string, string>[] items)
    {
        private int position = -1;

        public readonly KeyValuePair<string, string> Current => items[position];

        public bool MoveNext() => ++position < items.Length;
    }
}
