using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.Calibration;

/// <summary>
/// Appends calibration samples as JSON lines in its own directory, bounded so the log can
/// never grow indefinitely: each file stops at <see cref="MaximumFileBytes"/>, and only the
/// newest <see cref="MaximumFiles"/> recorder-owned files are kept. A failed write is
/// counted, never thrown, so diagnostics can never disturb Prince.
/// </summary>
public sealed class CalibrationRecorder : IDisposable
{
    public const long MaximumFileBytes = 8L * 1024 * 1024;
    public const int MaximumFiles = 8;
    public const int MaximumLineBytes = 4 * 1024;
    internal const string FilePrefix = "calibration-";
    internal const string FileExtension = ".jsonl";

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private FileStream? _current;
    private int _fileSequence;
    private bool _disposed;

    public CalibrationRecorder(string directory, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("The calibration directory must be an absolute path.", nameof(directory));
        }

        Directory = Path.GetFullPath(directory);
        _time = time ?? TimeProvider.System;
    }

    public string Directory { get; }

    public long Written { get; private set; }

    public long Failures { get; private set; }

    public string? CurrentFile => _current?.Name;

    public void Append(CalibrationSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var line = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sample, Json) + "\n");
                if (line.Length > MaximumLineBytes)
                {
                    Failures++;
                    return;
                }

                if (_current is null || _current.Length + line.Length > MaximumFileBytes)
                {
                    Rotate();
                }

                _current!.Write(line);
                _current.Flush();
                Written++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Failures++;
                _current?.Dispose();
                _current = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _current?.Dispose();
            _current = null;
        }
    }

    internal static IEnumerable<string> OwnedFiles(string directory) =>
        System.IO.Directory.Exists(directory)
            ? System.IO.Directory.EnumerateFiles(directory, FilePrefix + "*" + FileExtension)
                .Where(path => Path.GetFileName(path) is { } name
                    && name.StartsWith(FilePrefix, StringComparison.Ordinal)
                    && name.EndsWith(FileExtension, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
            : [];

    private void Rotate()
    {
        _current?.Dispose();
        _current = null;
        System.IO.Directory.CreateDirectory(Directory);
        var name = $"{FilePrefix}{_time.GetUtcNow():yyyyMMddTHHmmssfff}-{++_fileSequence:D4}{FileExtension}";
        _current = new FileStream(Path.Combine(Directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);

        // Keep only the newest recorder-owned files, the new one included.
        var owned = OwnedFiles(Directory).ToList();
        foreach (var stale in owned.Take(Math.Max(0, owned.Count - MaximumFiles)))
        {
            File.Delete(stale);
        }
    }
}
