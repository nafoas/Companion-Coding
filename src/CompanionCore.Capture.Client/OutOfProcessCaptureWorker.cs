using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Threading.Channels;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Client;

/// <summary>
/// Main-process control proxy. It never captures or owns pixels; a dedicated child
/// owns WGC/native resources. All starts still require Task 4's sealed grant.
/// </summary>
public sealed class OutOfProcessCaptureWorker : ICaptureWorker
{
    private const string PipePrefix = "CompanionCoreCapture_";
    private readonly CaptureWorkerLaunchOptions _options;
    private readonly ISystemClock _clock;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _stateGate = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<CaptureIpcMessage>> _pending = new();
    private readonly Channel<ClientEvent> _events = Channel.CreateBounded<ClientEvent>(
        new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private readonly CancellationTokenSource _eventLifetime = new();
    private readonly Task _eventDispatcher;
    private NamedPipeServerStream? _pipe;
    private Process? _process;
    private CancellationTokenSource? _readerLifetime;
    private Task? _reader;
    private CaptureAuthorizationGrant? _currentGrant;
    private CaptureWorkerMetrics _lastMetrics = CaptureWorkerMetrics.Empty;
    private long _lastSequence;
    private long _nextControlSequence;
    private long _restartCount;
    private long _workerEpoch;
    private long _lastDispatchedSequence;
    private bool _admitFrames;
    private bool _expectedExit;
    private bool _disposed;

    public OutOfProcessCaptureWorker(
        CaptureWorkerLaunchOptions? options = null,
        ISystemClock? clock = null)
    {
        _options = options ?? CaptureWorkerLaunchOptions.ForSiblingWorker();
        _clock = clock ?? SystemClock.Instance;
        _eventDispatcher = DispatchEventsAsync(_eventLifetime.Token);
    }

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    public int WorkerProcessId
    {
        get
        {
            lock (_stateGate)
            {
                return _process is { HasExited: false } process ? process.Id : 0;
            }
        }
    }

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public async Task StartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StartCoreAsync(authorization, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _ = await StopAndClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        RevokeLocalGrant();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RestartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        RevokeLocalGrant();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await TearDownProcessAsync().ConfigureAwait(false);
                SetStatus(CaptureWorkerStatus.Stopped);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _restartCount);
            SetStatus(CaptureWorkerStatus.Restarting);
            await StartCoreAsync(authorization, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsPipeConnected())
            {
                return _lastMetrics with
                {
                    Status = Status,
                    WorkerProcessId = 0,
                    RingFrameCount = 0,
                    RingBytes = 0,
                    QueueDepth = 0,
                    CurrentSourceFrames = 0,
                    CurrentAccountedBytes = 0,
                    OldestFrameLifetime = TimeSpan.Zero,
                    WorkingSetBytes = 0,
                    PrivateMemoryBytes = 0,
                    NativeHandleCount = 0,
                };
            }

            var response = await SendCommandAsync(
                new CaptureIpcMessage { Kind = CaptureIpcMessageKind.GetMetrics },
                cancellationToken).ConfigureAwait(false);
            UpdateMetrics(response.Metrics);
            return _lastMetrics;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task StartCoreAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Status is not CaptureWorkerStatus.Stopped and not CaptureWorkerStatus.Restarting
            || _process is not null)
        {
            throw new InvalidOperationException("The capture worker is already active.");
        }

        if (!File.Exists(_options.WorkerExecutablePath))
        {
            throw new FileNotFoundException(
                "The dedicated capture worker executable is unavailable.",
                _options.WorkerExecutablePath);
        }

        var workerEpoch = AdvanceWorkerEpoch();
        if (Status == CaptureWorkerStatus.Restarting)
        {
            SetStatus(CaptureWorkerStatus.Restarting, epoch: workerEpoch);
        }

        SetStatus(CaptureWorkerStatus.Starting, epoch: workerEpoch);
        try
        {
            await LaunchAndHandshakeAsync(workerEpoch, cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                EnsureCurrentEpochUnsafe(workerEpoch);
                _currentGrant = authorization;
                _lastSequence = 0;
                _lastDispatchedSequence = 0;
                _admitFrames = false;
            }

            var response = await SendCommandAsync(
                new CaptureIpcMessage
                {
                    Kind = CaptureIpcMessageKind.Start,
                    Authorization = CaptureIpcAuthorization.FromGrant(authorization),
                },
                cancellationToken).ConfigureAwait(false);
            UpdateMetrics(response.Metrics);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_stateGate)
            {
                if (!ReferenceEquals(_currentGrant, authorization))
                {
                    throw new InvalidOperationException(
                        "Capture authorization changed before worker start completed.");
                }

                _admitFrames = true;
            }

            SetStatus(CaptureWorkerStatus.Running, epoch: workerEpoch);
        }
        catch (Exception exception)
        {
            RevokeLocalGrant();
            await TearDownProcessAsync().ConfigureAwait(false);
            var wasCancelled = exception is OperationCanceledException
                && cancellationToken.IsCancellationRequested;
            SetStatus(
                wasCancelled
                    ? CaptureWorkerStatus.Stopped
                    : CaptureWorkerStatus.Faulted,
                wasCancelled
                    ? CaptureWorkerStatusReason.None
                    : CaptureWorkerStatusReason.CaptureUnavailable);
            throw;
        }
    }

    private async Task<CaptureStopResult> StopCoreAsync(CancellationToken cancellationToken)
    {
        var cleared = new CaptureStopResult(0);
        if (Status == CaptureWorkerStatus.Faulted || WorkerProcessId == 0)
        {
            await TearDownProcessAsync().ConfigureAwait(false);
            SetStatus(CaptureWorkerStatus.Stopped);
            return cleared;
        }

        if (IsPipeConnected())
        {
            try
            {
                var response = await SendCommandAsync(
                    new CaptureIpcMessage { Kind = CaptureIpcMessageKind.StopAndClear },
                    cancellationToken).ConfigureAwait(false);
                cleared = new CaptureStopResult(response.ClearedFrameCount, response.ClearedBytes);
                UpdateMetrics(response.Metrics);
            }
            finally
            {
                await RequestShutdownAndTearDownAsync().ConfigureAwait(false);
            }
        }
        else
        {
            await TearDownProcessAsync().ConfigureAwait(false);
        }

        SetStatus(CaptureWorkerStatus.Stopped);
        return cleared;
    }

    private async Task LaunchAndHandshakeAsync(
        long workerEpoch,
        CancellationToken cancellationToken)
    {
        var pipeName = PipePrefix + RandomNumberGenerator.GetHexString(32);
        var nonce = RandomNumberGenerator.GetHexString(CaptureIpcProtocol.HandshakeNonceHexLength);
        var pipeOptions = PipeOptions.Asynchronous;
        if (OperatingSystem.IsWindows())
        {
            pipeOptions |= PipeOptions.CurrentUserOnly;
        }

        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            pipeOptions);
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.WorkerExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_options.WorkerExecutablePath)!,
        };
        startInfo.ArgumentList.Add($"--pipe={pipeName}");
        startInfo.ArgumentList.Add($"--nonce={nonce}");
        if (_options.UseSyntheticPrivateTestSource)
        {
            startInfo.ArgumentList.Add("--synthetic-private-test-source");
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        process.Exited += OnWorkerExited;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The dedicated capture worker did not start.");
            }
        }
        catch
        {
            process.Exited -= OnWorkerExited;
            process.Dispose();
            pipe.Dispose();
            throw;
        }

        lock (_stateGate)
        {
            EnsureCurrentEpochUnsafe(workerEpoch);
            _pipe = pipe;
            _process = process;
            _expectedExit = false;
        }

        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(_options.ConnectTimeout);
        await pipe.WaitForConnectionAsync(connectTimeout.Token).ConfigureAwait(false);
        var helloId = Guid.NewGuid();
        await CaptureIpcProtocol.WriteAsync(
            pipe,
            new CaptureIpcMessage
            {
                Kind = CaptureIpcMessageKind.Hello,
                CorrelationId = helloId,
                HandshakeNonce = nonce,
            },
            connectTimeout.Token).ConfigureAwait(false);
        var response = await CaptureIpcProtocol.ReadAsync(pipe, connectTimeout.Token).ConfigureAwait(false);
        if (response.Kind != CaptureIpcMessageKind.HelloAccepted
            || response.CorrelationId != helloId
            || response.ControlSequence != 0
            || response.HandshakeNonce is not null
            || response.Authorization is not null
            || response.SequenceNumber != 0
            || response.Timestamp != default
            || response.Width != 0
            || response.Height != 0
            || response.AccountedBytes != 0
            || response.Status != CaptureWorkerStatus.Stopped
            || response.StatusReason != CaptureWorkerStatusReason.None
            || response.Metrics is not null
            || response.ClearedFrameCount != 0
            || response.ClearedBytes != 0
            || response.ErrorCode != CaptureWorkerErrorCode.None)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.InvalidHandshake);
        }

        _readerLifetime = new CancellationTokenSource();
        Interlocked.Exchange(ref _nextControlSequence, 0);
        _reader = ReadLoopAsync(workerEpoch, _readerLifetime.Token);
    }

    private async Task<CaptureIpcMessage> SendCommandAsync(
        CaptureIpcMessage command,
        CancellationToken cancellationToken)
    {
        var pipe = _pipe;
        if (pipe is null || !pipe.IsConnected)
        {
            throw new IOException("The capture worker connection is unavailable.");
        }

        var correlationId = Guid.NewGuid();
        var controlSequence = Interlocked.Increment(ref _nextControlSequence);
        command = command with
        {
            CorrelationId = correlationId,
            ControlSequence = controlSequence,
        };
        var completion = new TaskCompletionSource<CaptureIpcMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(correlationId, completion))
        {
            throw new InvalidOperationException("A duplicate capture command correlation was generated.");
        }

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await CaptureIpcProtocol.WriteAsync(pipe, command, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            commandTimeout.CancelAfter(_options.CommandTimeout);
            var response = await completion.Task.WaitAsync(commandTimeout.Token).ConfigureAwait(false);
            if (response.Kind != CaptureIpcMessageKind.CommandSucceeded)
            {
                throw new CaptureWorkerCommandException(command.Kind.ToString());
            }

            if (response.CorrelationId != correlationId
                || response.ControlSequence != controlSequence)
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
            }

            return response;
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    private async Task ReadLoopAsync(long workerEpoch, CancellationToken cancellationToken)
    {
        try
        {
            var pipe = _pipe ?? throw new InvalidOperationException("Reader started without a pipe.");
            while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
            {
                var message = await CaptureIpcProtocol.ReadAsync(pipe, cancellationToken)
                    .ConfigureAwait(false);
                switch (message.Kind)
                {
                    case CaptureIpcMessageKind.CommandSucceeded:
                    case CaptureIpcMessageKind.CommandFailed:
                        ValidateResponseShape(message);
                        if (message.CorrelationId == Guid.Empty
                            || !_pending.TryRemove(message.CorrelationId, out var completion))
                        {
                            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
                        }

                        completion.TrySetResult(message);
                        break;

                    case CaptureIpcMessageKind.FrameProduced:
                        PublishFrameIfCurrent(message, workerEpoch);
                        break;

                    case CaptureIpcMessageKind.StatusChanged:
                        ApplyWorkerStatus(message, workerEpoch);
                        break;

                    default:
                        throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException)
        {
            AbortOwnedWorker(workerEpoch, CaptureWorkerStatusReason.WorkerExited);
        }
        catch (Exception)
        {
            AbortOwnedWorker(workerEpoch, CaptureWorkerStatusReason.ProtocolFailure);
        }
    }

    private void PublishFrameIfCurrent(CaptureIpcMessage message, long workerEpoch)
    {
        CaptureAuthorizationGrant? grant;
        lock (_stateGate)
        {
            grant = _currentGrant;
            if (workerEpoch != _workerEpoch)
            {
                return;
            }

            if (message.CorrelationId != Guid.Empty
                || message.ControlSequence != 0
                || message.HandshakeNonce is not null
                || message.Authorization is null
                || message.SequenceNumber <= 0
                || message.Width <= 0
                || message.Height <= 0
                || message.AccountedBytes <= 0
                || message.AccountedBytes > CaptureWorkerMetrics.ScreenshotBudgetBytes
                || message.Timestamp == default
                || message.Status != CaptureWorkerStatus.Stopped
                || message.StatusReason != CaptureWorkerStatusReason.None
                || message.Metrics is not null
                || message.ClearedFrameCount != 0
                || message.ClearedBytes != 0
                || message.ErrorCode != CaptureWorkerErrorCode.None)
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
            }

            // A well-formed notification can legitimately be in flight while a local
            // privacy stop or epoch transition closes admission. It is stale work, not
            // evidence that the current IPC peer is corrupt, so reject it quietly.
            if (grant is null
                || !_admitFrames
                || Status != CaptureWorkerStatus.Running)
            {
                return;
            }

            // Once admission is open for this worker epoch, a mismatched target or a
            // duplicate/out-of-order sequence is a protocol failure. Tear down the
            // disposable worker rather than silently accepting a compromised peer.
            if (!message.Authorization.Matches(grant)
                || message.SequenceNumber <= _lastSequence)
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
            }

            _lastSequence = message.SequenceNumber;
        }

        var frame = new CaptureFrameMetadata(
            grant,
            message.SequenceNumber,
            message.Timestamp,
            message.Width,
            message.Height,
            message.AccountedBytes);
        _events.Writer.TryWrite(new FrameClientEvent(workerEpoch, grant, frame));
    }

    private async Task RequestShutdownAndTearDownAsync()
    {
        lock (_stateGate)
        {
            _expectedExit = true;
        }

        try
        {
            if (IsPipeConnected())
            {
                await SendCommandAsync(
                    new CaptureIpcMessage { Kind = CaptureIpcMessageKind.Shutdown },
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }

        await TearDownProcessAsync().ConfigureAwait(false);
    }

    private async Task TearDownProcessAsync()
    {
        Process? process;
        NamedPipeServerStream? pipe;
        CancellationTokenSource? readerLifetime;
        Task? reader;
        lock (_stateGate)
        {
            _expectedExit = true;
            process = _process;
            pipe = _pipe;
            readerLifetime = _readerLifetime;
            reader = _reader;
            _process = null;
            _pipe = null;
            _readerLifetime = null;
            _reader = null;
            _currentGrant = null;
            _lastSequence = 0;
            _admitFrames = false;
            _lastDispatchedSequence = 0;
            _workerEpoch = checked(_workerEpoch + 1);
        }

        readerLifetime?.Cancel();
        pipe?.Dispose();
        if (reader is not null)
        {
            try
            {
                await reader.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        readerLifetime?.Dispose();
        if (process is not null)
        {
            process.Exited -= OnWorkerExited;
            try
            {
                if (!process.HasExited)
                {
                    using var exitTimeout = new CancellationTokenSource(_options.ExitTimeout);
                    try
                    {
                        await process.WaitForExitAsync(exitTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var pending in _pending.Values)
        {
            pending.TrySetException(new IOException("The capture worker stopped."));
        }

        _pending.Clear();
    }

    private void OnWorkerExited(object? sender, EventArgs eventArgs)
    {
        long workerEpoch;
        lock (_stateGate)
        {
            if (_expectedExit || !ReferenceEquals(sender, _process))
            {
                return;
            }

            _currentGrant = null;
            workerEpoch = _workerEpoch;
        }

        FailPendingAndMarkExited(workerEpoch, CaptureWorkerStatusReason.WorkerExited);
    }

    private void FailPendingAndMarkExited(
        long workerEpoch,
        CaptureWorkerStatusReason reason = CaptureWorkerStatusReason.WorkerExited)
    {
        bool expectedExit;
        lock (_stateGate)
        {
            if (workerEpoch != _workerEpoch)
            {
                return;
            }

            expectedExit = _expectedExit;
        }

        RevokeLocalGrant();
        foreach (var pending in _pending.Values)
        {
            pending.TrySetException(new IOException("The capture worker exited."));
        }

        if (!expectedExit)
        {
            SetStatus(CaptureWorkerStatus.Faulted, reason, epoch: workerEpoch);
        }
    }

    private void AbortOwnedWorker(long workerEpoch, CaptureWorkerStatusReason reason)
    {
        FailPendingAndMarkExited(workerEpoch, reason);
        Process? process;
        NamedPipeServerStream? pipe;
        lock (_stateGate)
        {
            if (_expectedExit || workerEpoch != _workerEpoch)
            {
                return;
            }

            _expectedExit = true;
            process = _process;
            pipe = _pipe;
        }

        try
        {
            pipe?.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
        }
    }

    private void RevokeLocalGrant()
    {
        lock (_stateGate)
        {
            _currentGrant = null;
            _lastSequence = 0;
            _lastDispatchedSequence = 0;
            _admitFrames = false;
        }
    }

    private void UpdateMetrics(CaptureWorkerMetrics? metrics)
    {
        var processId = WorkerProcessId;
        if (metrics is null
            || !metrics.IsProtocolSafe()
            || processId <= 0
            || metrics.WorkerProcessId != processId)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }

        _lastMetrics = metrics with
        {
            RestartCount = Math.Max(metrics.RestartCount, Interlocked.Read(ref _restartCount)),
        };
    }

    private bool IsPipeConnected()
    {
        try
        {
            return _pipe is { IsConnected: true };
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static void ValidateResponseShape(CaptureIpcMessage message)
    {
        var errorShapeIsValid = message.Kind == CaptureIpcMessageKind.CommandSucceeded
            ? message.ErrorCode == CaptureWorkerErrorCode.None
            : Enum.IsDefined(message.ErrorCode)
                && message.ErrorCode != CaptureWorkerErrorCode.None;
        if (message.ControlSequence <= 0
            || message.HandshakeNonce is not null
            || message.Authorization is not null
            || message.SequenceNumber != 0
            || message.Timestamp != default
            || message.Width != 0
            || message.Height != 0
            || message.AccountedBytes != 0
            || message.Status != CaptureWorkerStatus.Stopped
            || message.StatusReason != CaptureWorkerStatusReason.None
            || message.Metrics is null
            || message.ClearedFrameCount < 0
            || message.ClearedBytes < 0
            || !errorShapeIsValid)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }
    }

    private void ApplyWorkerStatus(CaptureIpcMessage message, long workerEpoch)
    {
        if (!Enum.IsDefined(message.Status)
            || !Enum.IsDefined(message.StatusReason)
            || message.Timestamp == default
            || message.CorrelationId != Guid.Empty
            || message.ControlSequence != 0)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }

        if (message.HandshakeNonce is not null
            || message.Authorization is not null
            || message.SequenceNumber != 0
            || message.Width != 0
            || message.Height != 0
            || message.AccountedBytes != 0
            || message.Metrics is not null
            || message.ClearedFrameCount != 0
            || message.ClearedBytes != 0
            || message.ErrorCode != CaptureWorkerErrorCode.None)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }

        lock (_stateGate)
        {
            if (workerEpoch != _workerEpoch)
            {
                return;
            }

            if (_currentGrant is not null
                && !_admitFrames
                && message.Status == CaptureWorkerStatus.Running)
            {
                return;
            }

            if (message.Status is CaptureWorkerStatus.Faulted or CaptureWorkerStatus.Stopped)
            {
                _currentGrant = null;
                _lastSequence = 0;
                _lastDispatchedSequence = 0;
                _admitFrames = false;
            }
        }

        SetStatus(
            message.Status,
            message.StatusReason,
            message.Timestamp,
            workerEpoch);
    }

    private void SetStatus(
        CaptureWorkerStatus status,
        CaptureWorkerStatusReason reason = CaptureWorkerStatusReason.None,
        DateTimeOffset? timestamp = null,
        long? epoch = null)
    {
        long eventEpoch;
        lock (_stateGate)
        {
            if (epoch.HasValue && epoch.Value != _workerEpoch)
            {
                return;
            }

            Status = status;
            eventEpoch = _workerEpoch;
        }

        var change = new CaptureWorkerStatusChanged(status, timestamp ?? _clock.UtcNow, reason);
        _events.Writer.TryWrite(new StatusClientEvent(eventEpoch, change));
    }

    private long AdvanceWorkerEpoch()
    {
        lock (_stateGate)
        {
            _workerEpoch = checked(_workerEpoch + 1);
            _lastDispatchedSequence = 0;
            return _workerEpoch;
        }
    }

    private void EnsureCurrentEpochUnsafe(long workerEpoch)
    {
        if (workerEpoch != _workerEpoch)
        {
            throw new InvalidOperationException("The capture worker instance was superseded.");
        }
    }

    private async Task DispatchEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var clientEvent in _events.Reader.ReadAllAsync(cancellationToken))
            {
                switch (clientEvent)
                {
                    case FrameClientEvent frameEvent:
                        if (!TryAdmitDispatchedFrame(frameEvent))
                        {
                            continue;
                        }

                        foreach (EventHandler<CaptureFrameMetadata> handler in
                                 FrameProduced?.GetInvocationList()
                                     .Cast<EventHandler<CaptureFrameMetadata>>()
                                 ?? [])
                        {
                            try
                            {
                                handler(this, frameEvent.Frame);
                            }
                            catch (Exception)
                            {
                            }
                        }

                        break;

                    case StatusClientEvent statusEvent:
                        lock (_stateGate)
                        {
                            if (statusEvent.WorkerEpoch != _workerEpoch)
                            {
                                continue;
                            }
                        }

                        foreach (EventHandler<CaptureWorkerStatusChanged> handler in
                                 StatusChanged?.GetInvocationList()
                                     .Cast<EventHandler<CaptureWorkerStatusChanged>>()
                                 ?? [])
                        {
                            try
                            {
                                handler(this, statusEvent.Change);
                            }
                            catch (Exception)
                            {
                            }
                        }

                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool TryAdmitDispatchedFrame(FrameClientEvent frameEvent)
    {
        lock (_stateGate)
        {
            if (frameEvent.WorkerEpoch != _workerEpoch
                || !_admitFrames
                || !ReferenceEquals(frameEvent.Grant, _currentGrant)
                || frameEvent.Frame.SequenceNumber <= _lastDispatchedSequence)
            {
                return false;
            }

            _lastDispatchedSequence = frameEvent.Frame.SequenceNumber;
            return true;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RevokeLocalGrant();
        try
        {
            _operationLock.Wait();
            try
            {
                StopCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (Exception)
        {
            TearDownProcessAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _events.Writer.TryComplete();
            _eventLifetime.Cancel();
            _operationLock.Dispose();
            _writeLock.Dispose();
            Status = CaptureWorkerStatus.Stopped;
            _ = _eventDispatcher.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _eventLifetime,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private abstract record ClientEvent(long WorkerEpoch);

    private sealed record FrameClientEvent(
        long WorkerEpoch,
        CaptureAuthorizationGrant Grant,
        CaptureFrameMetadata Frame) : ClientEvent(WorkerEpoch);

    private sealed record StatusClientEvent(
        long WorkerEpoch,
        CaptureWorkerStatusChanged Change) : ClientEvent(WorkerEpoch);
}
