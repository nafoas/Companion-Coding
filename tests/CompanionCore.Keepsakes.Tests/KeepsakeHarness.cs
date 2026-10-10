using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Keepsakes.Tests;

/// <summary>One isolated synthetic test root. Nothing here can resolve a development or production root.</summary>
internal sealed class KeepsakeHarness : IAsyncDisposable
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    internal static readonly Guid CameraId = Guid.Parse("00000000-0000-4000-8000-0000000ca3e0");

    private long _sequence;

    private KeepsakeHarness(string basePath, MemoryStoreLocation location)
    {
        BasePath = basePath;
        MemoryLocation = location;
        Location = KeepsakeLocation.For(location);
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation MemoryLocation { get; }

    internal KeepsakeLocation Location { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal MemoryRepository Repository { get; private set; } = null!;

    internal KeepsakeCamera Camera { get; private set; } = null!;

    internal KeepsakeStore Store { get; private set; } = null!;

    internal CaptureAuthorizationGrant Grant { get; private set; } = null!;

    internal static async Task<KeepsakeHarness> CreateAsync(KeepsakeConfiguration? configuration = null)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Keepsakes.Tests", Guid.NewGuid().ToString("N"));
        var harness = new KeepsakeHarness(basePath, TestDataRootPolicy.Create(basePath, Guid.NewGuid()));
        harness.Repository = await MemoryRepository.OpenAsync(harness.MemoryLocation, harness.Privacy);
        harness.Camera = new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, configuration, CameraId);
        harness.Store = new KeepsakeStore(harness.Repository, harness.Location);
        harness.Grant = harness.NewGrant();
        return harness;
    }

    internal CaptureAuthorizationGrant NewGrant(Guid? session = null) =>
        CaptureAuthorizationGrant.Issue(session ?? Guid.NewGuid(), Privacy.Snapshot.Generation, Target());

    internal static CaptureTargetIdentity Target() => new(0x1234, 4321, "synthetic-game.exe", new string('A', 64));

    internal PhotographFrame Frame(CaptureAuthorizationGrant grant, DateTimeOffset at, int width = 64, int height = 48, byte seed = 7)
    {
        var stride = width * 4 + 8;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                pixels[offset] = (byte)(x * 3 + seed);
                pixels[offset + 1] = (byte)(y * 5 + seed);
                pixels[offset + 2] = (byte)(x + y + seed);
                pixels[offset + 3] = 255;
            }
        }

        return new PhotographFrame(new CaptureFrameMetadata(grant, ++_sequence, at, width, height), pixels, stride);
    }

    internal static PhotographFrame Uniform(CaptureFrameMetadata metadata, byte b, byte g, byte r, byte a)
    {
        var pixels = new byte[metadata.Width * metadata.Height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = b;
            pixels[index + 1] = g;
            pixels[index + 2] = r;
            pixels[index + 3] = a;
        }

        return new PhotographFrame(metadata, pixels, metadata.Width * 4);
    }

    internal async Task<PhotographResult> TakeAsync(
        CameraAction action,
        PhotographFrame frame,
        DateTimeOffset now,
        KeepsakeContext? context = null,
        TargetContentPolicy policy = TargetContentPolicy.TrustedGame,
        PrivacyAssessment? assessment = null) =>
        await Camera.TakeAsync(action, frame, context ?? new KeepsakeContext("game.alpha", "save.one", "session.one"), policy, assessment ?? PrivacyAssessment.Clear, now);

    internal async Task<(CameraAction Action, PhotographResult Result)> PhotographAsync(DateTimeOffset at, byte seed = 7)
    {
        var begun = Camera.BeginCameraAction(Grant, at);
        Assert.Equal(KeepsakeRefusal.None, begun.Refusal);
        var result = await TakeAsync(begun.Action!, Frame(Grant, at.AddSeconds(1), seed: seed), at.AddSeconds(1));
        Assert.Equal(KeepsakeRefusal.None, result.Refusal);
        return (begun.Action!, result);
    }

    internal string[] Files() =>
        Directory.Exists(Location.RootPath) ? Directory.GetFiles(Location.RootPath).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray() : [];

    public async ValueTask DisposeAsync()
    {
        await Repository.DisposeAsync();
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}

/// <summary>A strict PNG reader for tests: chunk CRCs, IHDR, single IDAT, and exact inflated size.</summary>
internal static class PngProbe
{
    internal static (int Width, int Height, byte[] Rgba) Decode(ReadOnlySpan<byte> png)
    {
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.True(png[..8].SequenceEqual(signature));
        var offset = 8;
        int width = 0, height = 0;
        using var idat = new MemoryStream();
        var sawEnd = false;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            var data = png.Slice(offset + 8, length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png[(offset + 8 + length)..]);
            Assert.Equal(Crc(png.Slice(offset + 4, 4 + length)), crc);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    Assert.Equal(8, data[8]);
                    Assert.Equal(6, data[9]);
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    sawEnd = true;
                    break;
            }

            offset += 12 + length;
        }

        Assert.True(sawEnd);
        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        Assert.Equal((width * 4 + 1) * height, raw.Length);
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Assert.Equal(0, raw[y * (width * 4 + 1)]);
            Array.Copy(raw, y * (width * 4 + 1) + 1, rgba, y * width * 4, width * 4);
        }

        return (width, height, rgba);
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
