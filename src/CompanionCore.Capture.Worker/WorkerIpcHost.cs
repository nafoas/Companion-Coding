using System.IO.Pipes;
using System.Threading.Channels;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class WorkerIpcHost : IAsyncDisposable
{
    private const int NotificationCapacity = 32;
    private readonly WorkerHostOptions _options;
    private readonly CaptureWorkerEngine _engine;
    private readonly NamedPipeClientStream _pipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Channel<CaptureIpcMessage> _notifications;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _notificationWriter;
    private long _lastControlSequence;
    private bool _disposed;

    internal WorkerIpcHost(WorkerHostOptions options, CaptureWorkerEngine engine)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _pipe = new NamedPipeClientStream(
            ".",
            options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        _notifications = Channel.CreateBounded<CaptureIpcMessage>(new BoundedChannelOptions(NotificationCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _engine.FrameProduced += OnFrameProduced;
        _engine.StatusChanged += OnStatusChanged;
    }

    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        await _pipe.ConnectAsync(TimeSpan.FromSeconds(10), linked.Token).ConfigureAwait(false);
        var hello = await CaptureIpcProtocol.ReadAsync(_pipe, linked.Token).ConfigureAwait(false);
        if (hello.Kind != CaptureIpcMessageKind.Hello
            || hello.CorrelationId == Guid.Empty
            || hello.ControlSequence != 0
            || hello.Authorization is not null
            || hello.SequenceNumber != 0
            || hello.Timestamp != default
            || hello.Width != 0
            || hello.Height != 0
            || hello.AccountedBytes != 0
            || hello.Status != CaptureWorkerStatus.Stopped
            || hello.StatusReason != CaptureWorkerStatusReason.None
            || hello.Metrics is not null
            || hello.ClearedFrameCount != 0
            || hello.ClearedBytes != 0
            || hello.ErrorCode != CaptureWorkerErrorCode.None
            || !string.Equals(
                hello.HandshakeNonce,
                _options.HandshakeNonce,
                StringComparison.Ordinal))
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.InvalidHandshake);
        }

        await WriteAsync(
            new CaptureIpcMessage
            {
                Kind = CaptureIpcMessageKind.HelloAccepted,
                CorrelationId = hello.CorrelationId,
            },
            linked.Token).ConfigureAwait(false);
        _notificationWriter = WriteNotificationsAsync(linked.Token);

        while (!linked.IsCancellationRequested && _pipe.IsConnected)
        {
            CaptureIpcMessage command;
            try
            {
                command = await CaptureIpcProtocol.ReadAsync(_pipe, linked.Token).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            if (command.CorrelationId == Guid.Empty)
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
            }

            if (command.ControlSequence <= 0
                || command.ControlSequence != checked(_lastControlSequence + 1))
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
            }

            _lastControlSequence = command.ControlSequence;
            ValidateCommandShape(command);

            var shouldExit = await HandleCommandAsync(command, linked.Token).ConfigureAwait(false);
            if (shouldExit)
            {
                break;
            }
        }

        _lifetime.Cancel();
        if (_notificationWriter is not null)
        {
            try
            {
                await _notificationWriter.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        return 0;
    }

    private async Task<bool> HandleCommandAsync(
        CaptureIpcMessage command,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (command.Kind)
            {
                case CaptureIpcMessageKind.Start:
                    if (command.Authorization is null)
                    {
                        throw new CaptureProtocolException(CaptureWorkerErrorCode.InvalidAuthorization);
                    }

                    await _engine.StartAsync(command.Authorization, cancellationToken)
                        .ConfigureAwait(false);
                    await SendSuccessAsync(command, cancellationToken).ConfigureAwait(false);
                    return false;

                case CaptureIpcMessageKind.Stop:
                case CaptureIpcMessageKind.StopAndClear:
                    var result = await _engine.StopAndClearAsync(cancellationToken).ConfigureAwait(false);
                    await WriteAsync(
                        new CaptureIpcMessage
                        {
                            Kind = CaptureIpcMessageKind.CommandSucceeded,
                            CorrelationId = command.CorrelationId,
                            ControlSequence = command.ControlSequence,
                            ClearedFrameCount = result.ClearedMetadataCount,
                            ClearedBytes = result.ClearedBytes,
                            Metrics = _engine.GetMetrics(),
                        },
                        cancellationToken).ConfigureAwait(false);
                    return false;

                case CaptureIpcMessageKind.GetMetrics:
                    await WriteAsync(
                        new CaptureIpcMessage
                        {
                            Kind = CaptureIpcMessageKind.CommandSucceeded,
                            CorrelationId = command.CorrelationId,
                            ControlSequence = command.ControlSequence,
                            Metrics = _engine.GetMetrics(),
                        },
                        cancellationToken).ConfigureAwait(false);
                    return false;

                case CaptureIpcMessageKind.Shutdown:
                    if (_engine.Status != CaptureWorkerStatus.Stopped)
                    {
                        await _engine.StopAndClearAsync(CancellationToken.None).ConfigureAwait(false);
                    }

                    await SendSuccessAsync(command, cancellationToken).ConfigureAwait(false);
                    return true;

                default:
                    throw new CaptureProtocolException(CaptureWorkerErrorCode.InvalidState);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = exception is CaptureProtocolException protocol
                ? protocol.ErrorCode
                : exception is ArgumentException
                    ? CaptureWorkerErrorCode.InvalidAuthorization
                    : CaptureWorkerErrorCode.CaptureFault;
            await WriteAsync(
                new CaptureIpcMessage
                {
                    Kind = CaptureIpcMessageKind.CommandFailed,
                    CorrelationId = command.CorrelationId,
                    ControlSequence = command.ControlSequence,
                    ErrorCode = error,
                    Metrics = _engine.GetMetrics(),
                },
                cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    private static void ValidateCommandShape(CaptureIpcMessage command)
    {
        var authorizationShapeIsValid = command.Kind == CaptureIpcMessageKind.Start
            ? command.Authorization is not null
            : command.Authorization is null;
        if (!authorizationShapeIsValid
            || command.Kind is not (
                CaptureIpcMessageKind.Start
                or CaptureIpcMessageKind.Stop
                or CaptureIpcMessageKind.StopAndClear
                or CaptureIpcMessageKind.GetMetrics
                or CaptureIpcMessageKind.Shutdown)
            || command.HandshakeNonce is not null
            || command.SequenceNumber != 0
            || command.Timestamp != default
            || command.Width != 0
            || command.Height != 0
            || command.AccountedBytes != 0
            || command.Status != CaptureWorkerStatus.Stopped
            || command.StatusReason != CaptureWorkerStatusReason.None
            || command.Metrics is not null
            || command.ClearedFrameCount != 0
            || command.ClearedBytes != 0
            || command.ErrorCode != CaptureWorkerErrorCode.None)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }
    }

    private Task SendSuccessAsync(
        CaptureIpcMessage command,
        CancellationToken cancellationToken) =>
        WriteAsync(
            new CaptureIpcMessage
            {
                Kind = CaptureIpcMessageKind.CommandSucceeded,
                CorrelationId = command.CorrelationId,
                ControlSequence = command.ControlSequence,
                Metrics = _engine.GetMetrics(),
            },
            cancellationToken);

    private void OnFrameProduced(object? sender, CaptureEngineFrame frame)
    {
        _notifications.Writer.TryWrite(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.FrameProduced,
            Authorization = frame.Authorization,
            SequenceNumber = frame.SequenceNumber,
            Timestamp = frame.Timestamp,
            Width = frame.Width,
            Height = frame.Height,
            AccountedBytes = frame.AccountedBytes,
        });
    }

    private void OnStatusChanged(object? sender, CaptureWorkerStatusChanged change)
    {
        _notifications.Writer.TryWrite(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.StatusChanged,
            Status = change.Status,
            StatusReason = change.Reason,
            Timestamp = change.Timestamp,
        });
    }

    private async Task WriteNotificationsAsync(CancellationToken cancellationToken)
    {
        await foreach (var notification in _notifications.Reader.ReadAllAsync(cancellationToken))
        {
            await WriteAsync(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(CaptureIpcMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CaptureIpcProtocol.WriteAsync(_pipe, message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.FrameProduced -= OnFrameProduced;
        _engine.StatusChanged -= OnStatusChanged;
        _notifications.Writer.TryComplete();
        _lifetime.Cancel();
        if (_notificationWriter is not null)
        {
            try
            {
                await _notificationWriter.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _pipe.Dispose();
        _writeLock.Dispose();
        _lifetime.Dispose();
    }
}
