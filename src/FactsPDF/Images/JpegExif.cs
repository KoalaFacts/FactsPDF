using System.Buffers.Binary;

namespace FactsPDF;

/// <summary>Only bounded TIFF IFD0 ranges/orientation are interpreted; no sub-IFD or thumbnail traversal.</summary>
internal static class JpegExif
{
    internal static void Validate(ReadOnlySpan<byte> tiff, CancellationToken cancellation)
    {
        JpegErrors.Need(tiff.Length >= 8, "Truncated Exif TIFF header.");
        var little = tiff[..2].SequenceEqual("II"u8);
        JpegErrors.Need(little || tiff[..2].SequenceEqual("MM"u8), "Invalid Exif byte order.");
        JpegErrors.Need(U16(tiff[2..], little) == 42, "Invalid Exif TIFF signature.");
        var offset = U32(tiff[4..], little);
        JpegErrors.Need(offset >= 8 && offset <= tiff.Length - 2L, "Exif IFD0 offset is outside its segment.");
        var directory = tiff[(int)offset..]; var count = U16(directory, little);
        if (count > 256) throw JpegErrors.Limit("Exif exceeds 256 IFD0 entries.");
        JpegErrors.Need(directory.Length >= 2 + 12 * count + 4, "Truncated Exif IFD0 entries.");
        var orientationSeen = false;
        for (var i = 0; i < count; i++)
        {
            if ((i & 63) == 0) cancellation.ThrowIfCancellationRequested();
            var entry = directory.Slice(2 + 12 * i, 12); var tag = U16(entry, little);
            var type = U16(entry[2..], little); var items = U32(entry[4..], little);
            var size = type switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 => 8, _ => 0 };
            if (size == 0) throw JpegErrors.Unsupported("Unsupported Exif IFD0 field type.");
            var bytes = checked((long)size * items);
            if (bytes > 4)
            {
                var at = U32(entry[8..], little);
                JpegErrors.Need(at <= tiff.Length && bytes <= tiff.Length - (long)at, "Exif field range is outside its segment.");
            }
            if (tag != 274) continue;
            JpegErrors.Need(!orientationSeen && type == 3 && items == 1, "Invalid or duplicate Exif orientation field.");
            orientationSeen = true; var orientation = U16(entry[8..], little);
            JpegErrors.Need(orientation is >= 1 and <= 8, "Invalid Exif orientation.");
            if (orientation != 1) throw JpegErrors.Unsupported("Image must already be upright; Exif rotation/reflection is unsupported.");
        }
    }

    private static int U16(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    private static uint U32(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
}
