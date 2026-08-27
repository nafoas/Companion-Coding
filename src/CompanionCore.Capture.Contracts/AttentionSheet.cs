using System.Security.Cryptography;

namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Transfer-of-ownership wrapper for a single lossless PNG attention sheet. The image
/// is valid only until disposal, remains in RAM, and is zeroed before release.
/// </summary>
public sealed class AttentionSheet : IDisposable
{
    public const int MaximumEncodedBytes = 8 * 1024 * 1024;
    public const int MaximumRetainedSheets = 2;
    public const string MediaType = "image/png";

    private byte[]? _encodedImage;

    internal AttentionSheet(AttentionSheetMetadata metadata, byte[] encodedImage)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _encodedImage = encodedImage ?? throw new ArgumentNullException(nameof(encodedImage));
        if (!metadata.IsProtocolSafe()
            || encodedImage.Length != metadata.EncodedByteLength
            || encodedImage.Length > MaximumEncodedBytes)
        {
            CryptographicOperations.ZeroMemory(encodedImage);
            _encodedImage = null;
            throw new ArgumentException("Attention-sheet payload does not match its metadata.");
        }
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
