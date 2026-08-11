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
            var pipeline = options.UseSyntheticSource
                ? null
                : new CaptureFramePipeline(maximumFrames: 2);
            await using var engine = new CaptureWorkerEngine(source, pipeline);
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
