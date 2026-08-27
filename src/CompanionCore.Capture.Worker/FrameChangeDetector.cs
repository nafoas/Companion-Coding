using System.Security.Cryptography;

namespace CompanionCore.Capture.Worker;

internal sealed class FrameChangeDetector : IDisposable
{
    internal const int MaximumSignatureWidth = 64;
    internal const int MaximumSignatureHeight = 36;
    internal const double NearDuplicateThreshold = 0.006;

    private byte[]? _baseline;
    private int _signatureWidth;
    private int _signatureHeight;
    private int _sourceWidth;
    private int _sourceHeight;

    internal FrameChangeResult Evaluate(OwnedBgra32Buffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var signatureWidth = Math.Min(MaximumSignatureWidth, pixels.Width);
        var signatureHeight = Math.Min(MaximumSignatureHeight, pixels.Height);
        var signature = GC.AllocateUninitializedArray<byte>(
            checked(signatureWidth * signatureHeight));
        BuildSignature(pixels, signature, signatureWidth, signatureHeight);

        if (_baseline is null
            || _sourceWidth != pixels.Width
            || _sourceHeight != pixels.Height
            || _signatureWidth != signatureWidth
            || _signatureHeight != signatureHeight)
        {
            ReplaceBaseline(signature, pixels.Width, pixels.Height, signatureWidth, signatureHeight);
            return new FrameChangeResult(1, IsDuplicate: false, GeometryChanged: true);
        }

        long difference = 0;
        for (var index = 0; index < signature.Length; index++)
        {
            difference += Math.Abs(signature[index] - _baseline[index]);
        }

        var score = difference / (signature.Length * 255d);
        if (score <= NearDuplicateThreshold)
        {
            CryptographicOperations.ZeroMemory(signature);
            return new FrameChangeResult(score, IsDuplicate: true, GeometryChanged: false);
        }

        ReplaceBaseline(signature, pixels.Width, pixels.Height, signatureWidth, signatureHeight);
        return new FrameChangeResult(score, IsDuplicate: false, GeometryChanged: false);
    }

    private static void BuildSignature(
        OwnedBgra32Buffer pixels,
        Span<byte> signature,
        int signatureWidth,
        int signatureHeight)
    {
        var source = pixels.ReadOnlySpan;
        for (var y = 0; y < signatureHeight; y++)
        {
            var sourceY = Math.Min(
                pixels.Height - 1,
                (int)(((y + 0.5) * pixels.Height) / signatureHeight));
            for (var x = 0; x < signatureWidth; x++)
            {
                var sourceX = Math.Min(
                    pixels.Width - 1,
                    (int)(((x + 0.5) * pixels.Width) / signatureWidth));
                var offset = checked((sourceY * pixels.Stride) + (sourceX * 4));
                var blue = source[offset];
                var green = source[offset + 1];
                var red = source[offset + 2];
                signature[(y * signatureWidth) + x] =
                    (byte)((red * 77 + green * 150 + blue * 29) >> 8);
            }
        }
    }

    private void ReplaceBaseline(
        byte[] signature,
        int sourceWidth,
        int sourceHeight,
        int signatureWidth,
        int signatureHeight)
    {
        var previous = Interlocked.Exchange(ref _baseline, signature);
        if (previous is not null)
        {
            CryptographicOperations.ZeroMemory(previous);
        }

        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _signatureWidth = signatureWidth;
        _signatureHeight = signatureHeight;
    }

    internal void Reset()
    {
        var baseline = Interlocked.Exchange(ref _baseline, null);
        if (baseline is not null)
        {
            CryptographicOperations.ZeroMemory(baseline);
        }

        _sourceWidth = 0;
        _sourceHeight = 0;
        _signatureWidth = 0;
        _signatureHeight = 0;
    }

    public void Dispose() => Reset();
}

internal readonly record struct FrameChangeResult(
    double Score,
    bool IsDuplicate,
    bool GeometryChanged);
