namespace CompanionCore.Capture.Worker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = WorkerHostOptions.Parse(args);
            await using IWorkerCaptureSource source = options.UseSyntheticSource
                ? new SyntheticCaptureSource()
                : new WindowsGraphicsCaptureSource();
            await using var engine = new CaptureWorkerEngine(
                source,
                maximumFrames: options.UseSyntheticSource
                    ? CompanionCore.Capture.Contracts.CaptureWorkerMetrics.MaximumSourceFrames
                    : 2,
                minimumFrameSpacing: options.UseSyntheticSource
                    ? TimeSpan.Zero
                    : CaptureWorkerEngine.AgreedLocalFrameSpacing);
            await using var host = new WorkerIpcHost(options, engine);
            return await host.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return 64;
        }
        catch (Exception)
        {
            return 70;
        }
    }
}
