using System.Text;

namespace FactsPDF;

/// <summary>
/// The two text builders a serializer writes page content and object text through, kept per thread between
/// conversions so a steady-state conversion allocates neither the builders nor the chunks they grow into. A
/// builder is taken out of its slot while in use, so a conversion that starts during another on the same
/// thread gets builders of its own; one grown past <see cref="MaxRetainedCapacity"/> is not kept.
/// </summary>
internal static class ReusableBuilders
{
    internal const int MaxRetainedCapacity = 1 << 20;

    [ThreadStatic] private static StringBuilder? content;
    [ThreadStatic] private static StringBuilder? objects;

    internal static StringBuilder RentContent() => Take(ref content);
    internal static StringBuilder RentObjects() => Take(ref objects);
    internal static void ReturnContent(StringBuilder builder) => Keep(ref content, builder);
    internal static void ReturnObjects(StringBuilder builder) => Keep(ref objects, builder);

    private static StringBuilder Take(ref StringBuilder? slot)
    {
        var builder = slot ?? new StringBuilder();
        slot = null;
        return builder.Clear();
    }

    private static void Keep(ref StringBuilder? slot, StringBuilder builder)
    {
        if (builder.Capacity <= MaxRetainedCapacity) slot = builder.Clear();
    }
}
