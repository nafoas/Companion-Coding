using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    internal static byte[] Encode(OwnedBgra32Buffer canvas) =>
        TryEncode(canvas) ?? throw new InvalidOperationException("The encoded attention sheet exceeds its hard bound.");

    /// <summary>Encodes within <see cref="AttentionSheet.MaximumEncodedBytes"/>, or returns null when it cannot fit.</summary>
    internal static byte[]? TryEncode(OwnedBgra32Buffer canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rawLength = checked((canvas.Width * 4 + 1) * canvas.Height);
        using var compressed = new MemoryStream(capacity: Math.Min(rawLength, AttentionSheet.MaximumEncodedBytes));
        MemoryStream? output = null;
        try
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                var row = GC.AllocateUninitializedArray<byte>(checked(canvas.Width * 4 + 1));
                try
                {
                    row[0] = 0;
                    var source = canvas.ReadOnlySpan;
                    for (var y = 0; y < canvas.Height; y++)
                    {
                        var sourceRow = source.Slice(y * canvas.Stride, canvas.Stride);
                        for (var x = 0; x < canvas.Width; x++)
                        {
                            var sourceOffset = x * 4;
                            var outputOffset = 1 + sourceOffset;
                            row[outputOffset] = sourceRow[sourceOffset + 2];
                            row[outputOffset + 1] = sourceRow[sourceOffset + 1];
                            row[outputOffset + 2] = sourceRow[sourceOffset];
                            row[outputOffset + 3] = sourceRow[sourceOffset + 3];
                        }

                        zlib.Write(row);
                        if (compressed.Length > AttentionSheet.MaximumEncodedBytes)
                        {
                            // Already past the bound: stop compressing early.
                            return null;
                        }
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(row);
                }
            }

            if (compressed.Length > AttentionSheet.MaximumEncodedBytes)
            {
                return null;
            }

            if (compressed.Length <= 0)
            {
                throw new InvalidOperationException("The encoded attention sheet is empty.");
            }

            output = new MemoryStream(capacity: checked((int)compressed.Length + 128));
            output.Write(Signature);
            Span<byte> ihdr = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr, canvas.Width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], canvas.Height);
            ihdr[8] = 8;
            ihdr[9] = 6;
            WriteChunk(output, "IHDR", ihdr);
            WriteChunk(output, "IDAT", compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
            WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
            if (output.Length > AttentionSheet.MaximumEncodedBytes)
            {
                return null;
            }

            return output.ToArray();
        }
        finally
        {
            ZeroStreamBuffer(compressed);
            if (output is not null)
            {
                ZeroStreamBuffer(output);
                output.Dispose();
            }
        }
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        Span<byte> typeBytes = stackalloc byte[4];
        Encoding.ASCII.GetBytes(type, typeBytes);
        output.Write(typeBytes);
        output.Write(data);

        var crc = uint.MaxValue;
        foreach (var value in typeBytes)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, ~crc);
        output.Write(checksum);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xedb88320U ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    private static void ZeroStreamBuffer(MemoryStream stream)
    {
        if (stream.TryGetBuffer(out var buffer) && buffer.Array is not null)
        {
            CryptographicOperations.ZeroMemory(buffer.Array);
        }
    }
}
