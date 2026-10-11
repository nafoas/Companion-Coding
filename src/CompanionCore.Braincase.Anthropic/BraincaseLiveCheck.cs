using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CompanionCore.Api;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Braincase;

public enum LiveCheckVerdict
{
    Passed = 1,
    NoSignIn = 2,
    ProviderFailed = 3,
    InvalidResponse = 4,
}

public sealed record LiveCheckResult(
    LiveCheckVerdict Verdict,
    string? Summary,
    int Observations,
    long InputUnits,
    long OutputUnits,
    decimal EstimatedCost,
    TimeSpan Latency,
    ProviderFailureKind FailureKind,
    ProviderUnavailableReason UnavailableReason,
    SemanticResponseInvalidReason InvalidReason);

/// <summary>
/// Boss's first live test: exactly one Claude call with a synthetic, private-safe image
/// (no screen capture, no memory, no grant), validated by the bridge's own strict parser.
/// It reports what came back, how long it took, and what it cost.
/// </summary>
public static class BraincaseLiveCheck
{
    internal const int Width = 320;
    internal const int Height = 180;

    public static async Task<LiveCheckResult> RunAsync(BuilderBraincaseSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!AnthropicSignIn.IsAvailable())
        {
            return Failed(LiveCheckVerdict.NoSignIn);
        }

        // An empty local store: the SDK's own sign-in (ant auth login or ANTHROPIC_API_KEY) is used.
        using var credentials = new InMemoryCredentialStore();
        var provider = new AnthropicSemanticProvider(credentials, settings.ToProviderOptions() with { LiveCallsEnabled = true });
        return await RunWithAsync(provider, settings, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<LiveCheckResult> RunWithAsync(
        AnthropicSemanticProvider provider,
        BuilderBraincaseSettings settings,
        CancellationToken cancellationToken)
    {
        using (provider)
        {
            var image = SyntheticScene();
            var operation = Guid.NewGuid();
            var request = new SemanticRequest(
                operation,
                SemanticOperationKind.InterpretAttentionSheet,
                1,
                new ResumePacket(Guid.NewGuid(), DateTimeOffset.UtcNow, "target-session:live-check", null, [], [], 0, false),
                new AttentionSheetDescription(
                    AttentionSheetKind.Orientation,
                    [AttentionRegionKind.FullContext],
                    Width,
                    Height,
                    Width,
                    Height,
                    AttentionSheet.MediaType,
                    image.Length,
                    Convert.ToHexStringLower(SHA256.HashData(image))),
                image);

            var clock = Stopwatch.StartNew();
            var reply = await provider.InterpretAsync(request, cancellationToken).ConfigureAwait(false);
            var latency = clock.Elapsed;
            if (!reply.IsSuccess)
            {
                return Failed(LiveCheckVerdict.ProviderFailed) with
                {
                    Latency = latency,
                    FailureKind = reply.FailureKind,
                    UnavailableReason = reply.UnavailableReason,
                };
            }

            if (!SemanticResponseParser.TryParse(reply.ResponseJson!, operation, [AttentionRegionKind.FullContext], out var parsed, out var reason))
            {
                return Failed(LiveCheckVerdict.InvalidResponse) with { Latency = latency, InvalidReason = reason };
            }

            var input = parsed!.InputUnits ?? 0;
            var output = parsed.OutputUnits ?? 0;
            var cost = AnthropicPricing.BudgetFor(settings.Model, 1m)?.CostOf(input, output) ?? 0m;
            return new LiveCheckResult(
                LiveCheckVerdict.Passed,
                parsed.Summary,
                parsed.Observations.Count,
                input,
                output,
                cost,
                latency,
                ProviderFailureKind.None,
                ProviderUnavailableReason.None,
                SemanticResponseInvalidReason.None);
        }
    }

    private static LiveCheckResult Failed(LiveCheckVerdict verdict) =>
        new(verdict, null, 0, 0, 0, 0m, TimeSpan.Zero, ProviderFailureKind.None, ProviderUnavailableReason.None, SemanticResponseInvalidReason.None);

    /// <summary>A plain synthetic picture: sky, grass, and a red square. Nothing private.</summary>
    internal static byte[] SyntheticScene()
    {
        var stride = (Width * 4) + 1;
        var raw = new byte[stride * Height];
        for (var y = 0; y < Height; y++)
        {
            raw[y * stride] = 0;
            for (var x = 0; x < Width; x++)
            {
                var offset = (y * stride) + 1 + (x * 4);
                var (r, g, b) = y < Height * 2 / 3 ? ((byte)110, (byte)170, (byte)235) : ((byte)70, (byte)150, (byte)60);
                if (x is >= 140 and < 180 && y is >= 70 and < 110)
                {
                    (r, g, b) = (220, 40, 40);
                }

                raw[offset] = r;
                raw[offset + 1] = g;
                raw[offset + 2] = b;
                raw[offset + 3] = 255;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8;
        header[9] = 6;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        output.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }
}
