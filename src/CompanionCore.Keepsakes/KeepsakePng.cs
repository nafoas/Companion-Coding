using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CompanionCore.Keepsakes;

/// <summary>Box-downscales a BGRA32 frame to a bounded edge and encodes a compressed RGBA PNG.</summary>
internal static class KeepsakePng
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    internal static (byte[] Png, int Width, int Height) Encode(ReadOnlySpan<byte> bgra, int width, int height, int stride, int maximumEdge)
    {
        var scale = Math.Max(1.0, Math.Max(width, height) / (double)maximumEdge);
        var outWidth = Math.Max(1, (int)Math.Round(width / scale));
        var outHeight = Math.Max(1, (int)Math.Round(height / scale));
        outWidth = Math.Min(outWidth, maximumEdge);
        outHeight = Math.Min(outHeight, maximumEdge);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[checked(outWidth * 4 + 1)];
            row[0] = 0;
            for (var y = 0; y < outHeight; y++)
            {
                var y0 = (int)((long)y * height / outHeight);
                var y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * height / outHeight));
                for (var x = 0; x < outWidth; x++)
                {
                    var x0 = (int)((long)x * width / outWidth);
                    var x1 = Math.Max(x0 + 1, (int)((long)(x + 1) * width / outWidth));
                    long b = 0, g = 0, r = 0, a = 0;
                    for (var sy = y0; sy < y1; sy++)
                    {
                        var line = bgra.Slice(sy * stride, width * 4);
                        for (var sx = x0; sx < x1; sx++)
                        {
                            b += line[sx * 4];
                            g += line[sx * 4 + 1];
                            r += line[sx * 4 + 2];
                            a += line[sx * 4 + 3];
                        }
                    }

                    var count = (long)(y1 - y0) * (x1 - x0);
                    var offset = 1 + x * 4;
                    row[offset] = (byte)((r + count / 2) / count);
                    row[offset + 1] = (byte)((g + count / 2) / count);
                    row[offset + 2] = (byte)((b + count / 2) / count);
                    row[offset + 3] = (byte)((a + count / 2) / count);
                }

                zlib.Write(row);
            }
        }

        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, outWidth);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], outHeight);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return (output.ToArray(), outWidth, outHeight);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = UpdateCrc(UpdateCrc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(length, crc);
        output.Write(length);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
