using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CompanionCore.Capture.Contracts;
using CompanionCore.Keepsakes;

namespace CompanionCore.Orchestration;

/// <summary>
/// Turns one authorized, RAM-only sheet into a keepsake frame: it decodes the capture
/// worker's own strict PNG format (RGBA8, filter 0, verified CRCs) and takes the
/// full-context region (the whole image of a photograph sheet) as a BGRA buffer. Nothing
/// is written here; the keepsake camera remains the only durable-image path.
/// </summary>
internal static class SheetPhotographSource
{
    private const int MaximumDimension = 16384;

    internal static PhotographFrame? TryCreate(AttentionSheet sheet, CaptureAuthorizationGrant grant)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(grant);
        var metadata = sheet.Metadata;
        if (!metadata.Matches(grant)
            || metadata.Regions.FirstOrDefault(region => region.Kind == AttentionRegionKind.FullContext) is not { } full)
        {
            return null;
        }

        if (!TryDecodeRgba(sheet.EncodedImage.Span, out var width, out var height, out var rgba)
            || width != metadata.SheetWidth
            || height != metadata.SheetHeight
            || !full.SheetPixels.IsValidWithin(width, height))
        {
            return null;
        }

        var rect = full.SheetPixels;
        var frame = new CaptureFrameMetadata(grant, Math.Max(1, metadata.SourceSequenceNumber), metadata.SourceTimestamp, rect.Width, rect.Height);
        if (rect == new PixelRect(0, 0, width, height))
        {
            // A whole-sheet region (a native-resolution photograph): swap channels in place,
            // so a large photograph never holds a second full-size copy.
            for (var offset = 0; offset < rgba.Length; offset += 4)
            {
                (rgba[offset], rgba[offset + 2]) = (rgba[offset + 2], rgba[offset]);
            }

            return new PhotographFrame(frame, rgba, width * 4);
        }

        var bgra = new byte[checked(rect.Width * rect.Height * 4)];
        for (var y = 0; y < rect.Height; y++)
        {
            var source = ((rect.Y + y) * width + rect.X) * 4;
            var target = y * rect.Width * 4;
            for (var x = 0; x < rect.Width; x++)
            {
                var s = source + (x * 4);
                var t = target + (x * 4);
                bgra[t] = rgba[s + 2];
                bgra[t + 1] = rgba[s + 1];
                bgra[t + 2] = rgba[s];
                bgra[t + 3] = rgba[s + 3];
            }
        }

        Array.Clear(rgba);
        return new PhotographFrame(frame, bgra, rect.Width * 4);
    }

    /// <summary>Strict decoder for the capture worker's sheet encoding only; anything else is refused.</summary>
    internal static bool TryDecodeRgba(ReadOnlySpan<byte> png, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = [];
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 8 || !png[..8].SequenceEqual(signature))
        {
            return false;
        }

        var offset = 8;
        var sawHeader = false;
        var sawEnd = false;
        using var idat = new MemoryStream();
        try
        {
            while (offset + 12 <= png.Length && !sawEnd)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
                if (length < 0 || offset + 12L + length > png.Length)
                {
                    return false;
                }

                var type = png.Slice(offset + 4, 4);
                var data = png.Slice(offset + 8, length);
                var crc = BinaryPrimitives.ReadUInt32BigEndian(png[(offset + 8 + length)..]);
                if (Crc(png.Slice(offset + 4, 4 + length)) != crc)
                {
                    return false;
                }

                var name = Encoding.ASCII.GetString(type);
                switch (name)
                {
                    case "IHDR":
                        if (sawHeader || length != 13)
                        {
                            return false;
                        }

                        width = BinaryPrimitives.ReadInt32BigEndian(data);
                        height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                        if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension
                            || data[8] != 8 || data[9] != 6 || data[10] != 0 || data[11] != 0 || data[12] != 0)
                        {
                            return false;
                        }

                        sawHeader = true;
                        break;
                    case "IDAT":
                        if (!sawHeader)
                        {
                            return false;
                        }

                        idat.Write(data);
                        break;
                    case "IEND":
                        sawEnd = true;
                        break;
                }

                offset += 12 + length;
            }

            if (!sawHeader || !sawEnd)
            {
                return false;
            }

            var stride = checked(width * 4 + 1);
            var expected = checked((long)stride * height);
            var raw = new byte[expected];
            idat.Position = 0;
            using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
            {
                var read = 0;
                while (read < raw.Length)
                {
                    var n = zlib.Read(raw, read, raw.Length - read);
                    if (n == 0)
                    {
                        return false;
                    }

                    read += n;
                }

                if (zlib.ReadByte() != -1)
                {
                    return false;
                }
            }

            rgba = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                if (raw[y * stride] != 0)
                {
                    Array.Clear(raw);
                    rgba = [];
                    return false;
                }

                Array.Copy(raw, (y * stride) + 1, rgba, y * width * 4, width * 4);
            }

            Array.Clear(raw);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentException)
        {
            rgba = [];
            return false;
        }
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
