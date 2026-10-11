using System.Security.Cryptography;

namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Transfer-of-ownership wrapper for a single lossless PNG attention sheet. The image
/// is valid only until disposal, remains in RAM, and is zeroed before release.
/// </summary>
public sealed class AttentionSheet : IDisposable
{
    private static ReadOnlySpan<byte> PngSignature =>
        [137, 80, 78, 71, 13, 10, 26, 10];

    public const int MaximumEncodedBytes = 8 * 1024 * 1024;
    public const int MaximumRetainedSheets = 2;

    /// <summary>A photograph sheet's longest edge (the keepsake camera's source bound).</summary>
    public const int MaximumPhotographEdge = 8192;
    public const string MediaType = "image/png";

    private byte[]? _encodedImage;

    internal AttentionSheet(AttentionSheetMetadata metadata, byte[] encodedImage)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);
        if (metadata is null
            || !metadata.IsProtocolSafe()
            || encodedImage.Length != metadata.EncodedByteLength
            || encodedImage.Length > MaximumEncodedBytes
            || encodedImage.Length < PngSignature.Length
            || !encodedImage.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            CryptographicOperations.ZeroMemory(encodedImage);
            throw new ArgumentException("Attention-sheet payload does not match its metadata.");
        }

        Metadata = metadata;
        _encodedImage = encodedImage;
    }

    public AttentionSheetMetadata Metadata { get; }

    public int Length => Volatile.Read(ref _encodedImage)?.Length ?? 0;

    public ReadOnlyMemory<byte> EncodedImage =>
        Volatile.Read(ref _encodedImage) is { } image
            ? image
            : throw new ObjectDisposedException(nameof(AttentionSheet));

    public void Dispose()
    {
        var image = Interlocked.Exchange(ref _encodedImage, null);
        if (image is not null)
        {
            CryptographicOperations.ZeroMemory(image);
        }
    }
}
