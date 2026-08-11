using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class CaptureSignalTracker
{
    private readonly TimeSpan _noFrameThreshold;
    private DateTimeOffset _lastFrameAt;
    private CaptureWorkerStatus _status = CaptureWorkerStatus.Stopped;

    internal CaptureSignalTracker(TimeSpan noFrameThreshold)
    {
        if (noFrameThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(noFrameThreshold));
        }

        _noFrameThreshold = noFrameThreshold;
    }

    internal CaptureWorkerStatus Status => _status;

    internal void Reset(DateTimeOffset now)
    {
        _lastFrameAt = now;
        _status = CaptureWorkerStatus.Running;
    }

    internal void MarkFrame(DateTimeOffset now)
    {
        _lastFrameAt = now;
        _status = CaptureWorkerStatus.Running;
    }

    internal CaptureSourceStatusChanged? Evaluate(
        DateTimeOffset now,
        bool isMinimized)
    {
        if (isMinimized)
        {
            return TryTransition(
                CaptureWorkerStatus.PausedMinimized,
                CaptureWorkerStatusReason.Minimized,
                isStall: false);
        }

        return now - _lastFrameAt >= _noFrameThreshold
            ? TryTransition(
                CaptureWorkerStatus.NoSignal,
                CaptureWorkerStatusReason.FrameArrivalStalled,
                isStall: true)
            : null;
    }

    internal CaptureSourceStatusChanged? TryTransition(
        CaptureWorkerStatus status,
        CaptureWorkerStatusReason reason,
        bool isStall)
    {
        if (_status == status)
        {
            return null;
        }

        _status = status;
        return new CaptureSourceStatusChanged(
            status,
            reason,
            ClearRetainedFrames: true,
            IsStall: isStall);
    }

    internal void Stop() => _status = CaptureWorkerStatus.Stopped;
}
