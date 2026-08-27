using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CompanionCore.Capture.Worker.Tests;

internal sealed record DecodedPng(int Width, int Height, byte[] Rgba)
{
    internal ReadOnlySpan<byte> Pixel(int x, int y) =>
        Rgba.AsSpan(checked((y * Width + x) * 4), 4);
}

internal static class PngTestDecoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    internal static DecodedPng Decode(ReadOnlySpan<byte> encoded)
    {
        Assert.True(encoded.Length >= Signature.Length);
        Assert.True(encoded[..Signature.Length].SequenceEqual(Signature));
        var offset = Signature.Length;
        var width = 0;
        var height = 0;
        using var compressed = new MemoryStream();
        while (offset < encoded.Length)
        {
            Assert.True(offset <= encoded.Length - 12);
            var length = BinaryPrimitives.ReadInt32BigEndian(encoded.Slice(offset, 4));
            offset += 4;
            var type = Encoding.ASCII.GetString(encoded.Slice(offset, 4));
            offset += 4;
            Assert.InRange(length, 0, encoded.Length - offset - 4);
            var data = encoded.Slice(offset, length);
            offset += length;
            offset += 4; // Production CRC is verified by ordinary PNG readers; skip here.
            switch (type)
            {
                case "IHDR":
                    Assert.Equal(13, length);
                    width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                    height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                    Assert.Equal(8, data[8]);
                    Assert.Equal(6, data[9]);
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
                case "IEND":
                    offset = encoded.Length;
                    break;
            }
        }

        Assert.True(width > 0 && height > 0);
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var scanlines = new byte[checked((width * 4 + 1) * height)];
        zlib.ReadExactly(scanlines);
        Assert.Equal(-1, zlib.ReadByte());
        var rgba = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var scanlineOffset = y * (width * 4 + 1);
            Assert.Equal(0, scanlines[scanlineOffset]);
            scanlines.AsSpan(scanlineOffset + 1, width * 4)
                .CopyTo(rgba.AsSpan(y * width * 4, width * 4));
        }

        return new DecodedPng(width, height, rgba);
    }
}
