using System.Buffers;

namespace FactsPDF;

/// <summary>A growable list whose array is rented from the shared pool: a struct holding the array and the
/// count, so a list costs no object, and growing it returns the outgrown array. Always use it through a
/// field or a ref, never a copy.</summary>
internal struct RentedList<T>
{
    private T[]? items;

    internal int Count { get; private set; }

    internal readonly T[]? Array => items;

    internal readonly Span<T> Span => items is null ? [] : items.AsSpan(0, Count);

    internal readonly T this[int index]
    {
        get => (uint)index < (uint)Count ? items![index] : throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal void Add(T value)
    {
        if (items is null) items = ArrayPool<T>.Shared.Rent(16);
        else if (Count == items.Length)
        {
            var larger = ArrayPool<T>.Shared.Rent(checked(items.Length * 2));
            System.Array.Copy(items, larger, Count);
            ArrayPool<T>.Shared.Return(items, ClearOnReturn);
            items = larger;
        }
        items[Count++] = value;
    }

    internal void Clear() => Count = 0;

    /// <summary>Returns the array to the pool; the list is empty afterwards.</summary>
    internal void Release()
    {
        if (items is not null) ArrayPool<T>.Shared.Return(items, ClearOnReturn);
        items = null;
        Count = 0;
    }

    private static bool ClearOnReturn =>
        System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>();
}
