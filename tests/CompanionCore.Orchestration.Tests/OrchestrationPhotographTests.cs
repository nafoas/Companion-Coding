using CompanionCore.Capture.Contracts;
using CompanionCore.Keepsakes;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// Full-resolution keepsake photographs (deferred WIRE-01 D2): the camera action asks the
/// worker for one native-resolution photograph of the authorized target. Ordinary sheets
/// keep flowing to attention meanwhile, and a worker that cannot take one degrades honestly
/// to the sheet's full-context region.
/// </summary>
public sealed class OrchestrationPhotographTests
{
    [Fact]
    public async Task OrdinarySheetsWhileTheCameraWaits_NeverBecomeThePhotograph()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        Assert.Equal(KeepsakeRefusal.None, await harness.Orchestrator.TakePhotographAsync());
        Assert.Equal(1, harness.Worker.PhotographRequests);
        harness.Time.Advance(TimeSpan.FromSeconds(1));

        // A sheet composed before the request is still ordinary attention input.
        harness.Worker.EmitSheet(harness.Time.GetUtcNow(), inFlight: true);
        await harness.SettleAsync();
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);
        Assert.DoesNotContain(harness.Notices, n => n.Keepsake?.Kind == KeepsakeIntentKind.PhotographSaved);
        Assert.Equal(1, harness.Provider.CallCount);

        await harness.SheetAsync();
        var entry = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal((ScriptedCaptureWorker.SourceWidth, ScriptedCaptureWorker.SourceHeight), (entry.Width, entry.Height));
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task APhotographFromAFrameOlderThanTheAction_AsksAgain_AndTheNextIsKept()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var asked = harness.Time.GetUtcNow();
        await harness.Orchestrator.TakePhotographAsync();

        // The worker's next frame had been captured before the action was shown.
        harness.Worker.EmitSheet(asked - TimeSpan.FromSeconds(5));
        await harness.SettleAsync();
        Assert.Equal(2, harness.Worker.PhotographRequests);
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync();
        var entry = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal((ScriptedCaptureWorker.SourceWidth, ScriptedCaptureWorker.SourceHeight), (entry.Width, entry.Height));
    }

    [Fact]
    public async Task AFailedRetake_FallsBackToTheSheetResolution_InsideTheSameWindow()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var asked = harness.Time.GetUtcNow();
        await harness.Orchestrator.TakePhotographAsync();

        // The first photograph predates the action, and asking again fails.
        harness.Worker.PhotographRequestFailure = new InvalidOperationException("The worker restarted.");
        harness.Worker.EmitSheet(asked - TimeSpan.FromSeconds(5));
        await harness.SettleAsync();
        Assert.Equal(2, harness.Worker.PhotographRequests);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync();
        var entry = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal((ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight), (entry.Width, entry.Height));
    }

    [Fact]
    public void AWholeSheetPhotograph_IsConvertedInPlace_PixelExact()
    {
        const int width = 6, height = 3;
        var grant = CaptureAuthorizationGrant.Issue(Guid.NewGuid(), 1, Candidate().Identity);
        var png = ScriptedCaptureWorker.EncodeUniform(width, height, 70);
        using var sheet = new AttentionSheet(
            new AttentionSheetMetadata
            {
                TargetSessionId = grant.TargetSessionId,
                Generation = grant.Generation,
                Target = grant.Target,
                SourceSequenceNumber = 1,
                SourceTimestamp = DateTimeOffset.UnixEpoch.AddDays(1),
                SourceWidth = width,
                SourceHeight = height,
                SheetWidth = width,
                SheetHeight = height,
                EncodedByteLength = png.Length,
                Kind = AttentionSheetKind.Photograph,
                Regions =
                [
                    new AttentionSheetRegionMetadata
                    {
                        Kind = AttentionRegionKind.FullContext,
                        NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
                        SourcePixels = new PixelRect(0, 0, width, height),
                        SheetPixels = new PixelRect(0, 0, width, height),
                    },
                ],
            },
            png);

        var frame = SheetPhotographSource.TryCreate(sheet, grant);

        Assert.NotNull(frame);
        Assert.Equal((width, height, width * 4), (frame.Metadata.Width, frame.Metadata.Height, frame.Stride));
        var pixels = frame.Bgra32.Span;
        for (var x = 0; x < width; x++)
        {
            // UniformRows: R = shade, G = shade + x, B = 40, A = 255; the frame is BGRA.
            Assert.Equal(new byte[] { 40, (byte)(70 + x), 70, 255 }, pixels.Slice(((2 * width) + x) * 4, 4).ToArray());
        }
    }

    [Fact]
    public async Task AWorkerThatCannotTakeAPhotograph_FallsBackToTheSheetResolution()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Worker.PhotographRequestFailure = new NotSupportedException("An older worker.");

        Assert.Equal(KeepsakeRefusal.None, await harness.Orchestrator.TakePhotographAsync());
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync();

        var entry = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal((ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight), (entry.Width, entry.Height));
        Assert.Equal(0, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task APhotographWithoutACameraAction_IsReleasedAtOnce_AndNeverInterpreted()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        await harness.Worker.RequestPhotographAsync(grant, CancellationToken.None);

        var stray = await harness.SheetAsync();

        Assert.Equal(AttentionSheetKind.Photograph, stray.Metadata.Kind);
        Assert.Equal(0, stray.Length);
        Assert.Equal(0, harness.Provider.CallCount);
        Assert.Empty(await harness.Orchestrator.Keepsakes.ListAsync());
    }

    [Fact]
    public async Task APrivacyStopWhileTheCameraWaits_TakesNothing()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.Orchestrator.TakePhotographAsync();

        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(11));

        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);
        Assert.Empty(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.DoesNotContain(harness.Notices, n => n.Keepsake?.Kind == KeepsakeIntentKind.PhotographSaved);
    }
}
