using System.Reflection;
using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using static CompanionCore.Keepsakes.Tests.KeepsakeHarness;

namespace CompanionCore.Keepsakes.Tests;

public sealed class KeepsakeTests
{
    [Fact]
    public async Task Scenario1_CameraActionAndDurableWrite_AreVisiblyPaired()
    {
        await using var harness = await CreateAsync();
        var begun = harness.Camera.BeginCameraAction(harness.Grant, T0);
        var shown = Assert.Single(begun.Intents);
        Assert.Equal(KeepsakeIntentKind.CameraShown, shown.Kind);
        Assert.Empty(harness.Files());

        var saved = await harness.TakeAsync(begun.Action!, harness.Frame(harness.Grant, T0.AddSeconds(2)), T0.AddSeconds(2));

        var written = Assert.Single(saved.Intents);
        Assert.Equal(KeepsakeIntentKind.PhotographSaved, written.Kind);
        Assert.Equal(shown.ActionId, written.ActionId);
        Assert.Equal(saved.PhotographId, written.PhotographId);
        Assert.Equal([$"{shown.ActionId:N}.png"], harness.Files());
        var record = Assert.Single(await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = [saved.PhotographId!.Value] }));
        Assert.Equal($"photo:{shown.ActionId:N}", record.Record.SubjectKey);
    }

    [Fact]
    public async Task Scenario1_TakeWithoutAValidAction_WritesNothing()
    {
        await using var harness = await CreateAsync();
        var forged = new CameraAction(Guid.NewGuid(), harness.Grant.TargetSessionId, harness.Grant.Generation, harness.Grant.Target, T0, T0.AddSeconds(10));
        Assert.Equal(KeepsakeRefusal.UnknownAction, (await harness.TakeAsync(forged, harness.Frame(harness.Grant, T0.AddSeconds(1)), T0.AddSeconds(1))).Refusal);

        var begun = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var altered = begun with { ExpiresAt = begun.ExpiresAt.AddHours(1) };
        Assert.Equal(KeepsakeRefusal.UnknownAction, (await harness.TakeAsync(altered, harness.Frame(harness.Grant, T0.AddSeconds(1)), T0.AddSeconds(1))).Refusal);

        var otherCamera = new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location);
        Assert.Equal(KeepsakeRefusal.UnknownAction, (await otherCamera.TakeAsync(begun, harness.Frame(harness.Grant, T0.AddSeconds(1)), new KeepsakeContext(), TargetContentPolicy.TrustedGame, PrivacyAssessment.Clear, T0.AddSeconds(1))).Refusal);

        Assert.Equal(KeepsakeRefusal.ActionExpired, (await harness.TakeAsync(begun, harness.Frame(harness.Grant, T0.AddSeconds(5)), T0.AddSeconds(10.001))).Refusal);
        Assert.Equal(KeepsakeRefusal.UnknownAction, (await harness.TakeAsync(begun, harness.Frame(harness.Grant, T0.AddSeconds(5)), T0.AddSeconds(5))).Refusal);
        Assert.Empty(harness.Files());
        Assert.Empty(await harness.Store.ListAsync());
    }

    [Fact]
    public async Task Scenario2_OnlyTheAuthorizedTargetInsideTheWindowIsWritten()
    {
        await using var harness = await CreateAsync();
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var otherSession = harness.NewGrant();

        Assert.Equal(KeepsakeRefusal.WrongTarget, (await harness.TakeAsync(action, harness.Frame(otherSession, T0.AddSeconds(1)), T0.AddSeconds(1))).Refusal);
        Assert.Equal(KeepsakeRefusal.OutsideWindow, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(-1)), T0.AddSeconds(1))).Refusal);
        Assert.Equal(KeepsakeRefusal.OutsideWindow, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(11)), T0.AddSeconds(9))).Refusal);
        Assert.Equal(KeepsakeRefusal.PrivacyRejected, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(1)), T0.AddSeconds(1), policy: TargetContentPolicy.Standard, assessment: PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential))).Refusal);
        Assert.Equal(KeepsakeRefusal.PrivacyRejected, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(1)), T0.AddSeconds(1), policy: TargetContentPolicy.Standard, assessment: PrivacyAssessment.Unavailable)).Refusal);
        Assert.Empty(harness.Files());

        harness.Privacy.PauseAndRevoke();
        Assert.Equal(KeepsakeRefusal.PrivacyStale, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(2)), T0.AddSeconds(2))).Refusal);
        Assert.Equal(KeepsakeRefusal.PrivacyStale, harness.Camera.BeginCameraAction(harness.Grant, T0.AddHours(1)).Refusal);
        Assert.Empty(harness.Files());
        Assert.Empty(await harness.Store.ListAsync());
    }

    [Fact]
    public async Task Scenario2_StandardPolicyWithClearAssessment_IsAllowed()
    {
        await using var harness = await CreateAsync();
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;

        var result = await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(1)), T0.AddSeconds(1), policy: TargetContentPolicy.Standard, assessment: PrivacyAssessment.Clear);

        Assert.Equal(KeepsakeRefusal.None, result.Refusal);
    }

    [Fact]
    public async Task Scenario2_InvalidFramesAreRefused()
    {
        await using var harness = await CreateAsync(new KeepsakeConfiguration { MaximumSourceEdge = 256, MaximumSavedEdge = 128 });
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var good = harness.Frame(harness.Grant, T0.AddSeconds(1));

        Assert.Equal(KeepsakeRefusal.InvalidFrame, (await harness.TakeAsync(action, good with { Stride = 63 * 4 }, T0.AddSeconds(1))).Refusal);
        Assert.Equal(KeepsakeRefusal.InvalidFrame, (await harness.TakeAsync(action, good with { Bgra32 = good.Bgra32[..(good.Stride * 47 + 64 * 4 - 1)] }, T0.AddSeconds(1))).Refusal);
        Assert.Equal(KeepsakeRefusal.InvalidFrame, (await harness.TakeAsync(action, harness.Frame(harness.Grant, T0.AddSeconds(1), width: 257, height: 10), T0.AddSeconds(1))).Refusal);
        Assert.Empty(harness.Files());

        // The exact minimum buffer (no trailing stride padding on the last row) is accepted.
        var exact = good with { Bgra32 = good.Bgra32[..(good.Stride * 47 + 64 * 4)] };
        Assert.Equal(KeepsakeRefusal.None, (await harness.TakeAsync(action, exact, T0.AddSeconds(1))).Refusal);
    }

    [Fact]
    public void Scenario3_OnlyTakeAcceptsPixels_AndNoScreenshotTypeReachesKeepsakes()
    {
        var assembly = typeof(KeepsakeCamera).Assembly;
        var pixelTaking = assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.DeclaringType != typeof(PhotographFrame))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PhotographFrame)))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();
        Assert.Equal(["KeepsakeCamera.TakeAsync"], pixelTaking);
        Assert.DoesNotContain(assembly.GetExportedTypes().SelectMany(type => type.GetMethods()).SelectMany(method => method.GetParameters()), parameter =>
            parameter.ParameterType == typeof(AttentionSheet) || parameter.ParameterType == typeof(AttentionSheetMetadata));
        Assert.DoesNotContain(typeof(AttentionSheet).Assembly.GetReferencedAssemblies(), name => name.Name == assembly.GetName().Name);
    }

    [Fact]
    public async Task Scenario4_SavedImageIsABoundedValidPng_WithMatchingDigestAndNeutralRecord()
    {
        await using var harness = await CreateAsync();
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var frame = Uniform(new CaptureFrameMetadata(harness.Grant, 1, T0.AddSeconds(1), 2560, 1440), b: 10, g: 20, r: 30, a: 255);

        var result = await harness.TakeAsync(action, frame, T0.AddSeconds(1));

        var png = await File.ReadAllBytesAsync(Path.Combine(harness.Location.RootPath, $"{action.ActionId:N}.png"));
        var (width, height, rgba) = PngProbe.Decode(png);
        Assert.Equal((1280, 720), (width, height));
        Assert.Equal([30, 20, 10, 255], rgba[..4]);
        Assert.Equal([30, 20, 10, 255], rgba[^4..]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), result.Sha256);
        Assert.True(png.Length < 2560 * 1440 * 4 / 100);

        var entry = Assert.Single(await harness.Store.ListAsync());
        Assert.Equal((result.PhotographId!.Value, action.ActionId, png.Length, 1280, 720), (entry.PhotographId, entry.ActionId, (int)entry.ByteLength, entry.Width, entry.Height));
        Assert.Equal("[neutral photograph caption]", entry.Caption);
        Assert.Equal(("game.alpha", "save.one", "session.one"), (entry.Game, entry.Save, entry.Session));
        Assert.Equal(T0.AddSeconds(1), entry.TakenAt);
        Assert.False(entry.Deleted);
        var record = Assert.Single(await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = [entry.PhotographId] })).Record;
        Assert.Equal(MemoryScope.Save, record.Scope);
        Assert.Equal(MemorySourceKind.Observed, record.SourceKind);
    }

    [Theory]
    [InlineData(64, 48, 64, 48)]
    [InlineData(1280, 1280, 1280, 1280)]
    [InlineData(1281, 100, 1280, 100)]
    [InlineData(100, 4000, 32, 1280)]
    [InlineData(1, 8192, 1, 1280)]
    public void Scenario4_DownscaleKeepsAspectWithinTheEdgeBound(int width, int height, int expectedWidth, int expectedHeight)
    {
        var pixels = new byte[width * height * 4];
        var (png, outWidth, outHeight) = KeepsakePng.Encode(pixels, width, height, width * 4, 1280);

        Assert.Equal((expectedWidth, expectedHeight), (outWidth, outHeight));
        Assert.Equal((expectedWidth, expectedHeight), (PngProbe.Decode(png).Width, PngProbe.Decode(png).Height));
    }

    [Fact]
    public void Scenario4_BoxDownscaleAveragesSourcePixels()
    {
        // A 2x1 frame of black and white averages to mid-grey at 1x1.
        byte[] pixels = [0, 0, 0, 255, 255, 255, 255, 255];
        var (png, _, _) = KeepsakePng.Encode(pixels, 2, 1, 8, 1);

        Assert.Equal([128, 128, 128, 255], PngProbe.Decode(png).Rgba);
    }

    [Fact]
    public async Task Scenario5_PhotographsAreRare()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(KeepsakeRefusal.None, harness.Camera.BeginCameraAction(harness.Grant, T0).Refusal);
        Assert.Equal(KeepsakeRefusal.TooSoon, harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(4.99)).Refusal);
        Assert.Equal(KeepsakeRefusal.TooSoon, harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(-1)).Refusal);
        for (var index = 1; index < 12; index++)
        {
            Assert.Equal(KeepsakeRefusal.None, harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(5 * index)).Refusal);
        }

        Assert.Equal(KeepsakeRefusal.DailyLimit, harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(60)).Refusal);
        Assert.Equal(KeepsakeRefusal.DailyLimit, harness.Camera.BeginCameraAction(harness.Grant, T0.AddDays(1).AddSeconds(-1)).Refusal);
        Assert.Equal(KeepsakeRefusal.None, harness.Camera.BeginCameraAction(harness.Grant, T0.AddDays(1)).Refusal);
    }

    [Fact]
    public async Task Scenario6_InspectionReturnsVerifiedBytes_AndReportsTamperingOrLoss()
    {
        await using var harness = await CreateAsync();
        var (action, result) = await harness.PhotographAsync(T0);
        var path = Path.Combine(harness.Location.RootPath, $"{action.ActionId:N}.png");
        var original = await File.ReadAllBytesAsync(path);

        var verified = await harness.Store.InspectAsync(result.PhotographId!.Value);
        Assert.Equal(InspectionStatus.Verified, verified.Status);
        Assert.Equal(original, verified.Png.ToArray());

        var flipped = original.ToArray();
        flipped[^20] ^= 0x01;
        await File.WriteAllBytesAsync(path, flipped);
        var tampered = await harness.Store.InspectAsync(result.PhotographId.Value);
        Assert.Equal(InspectionStatus.Tampered, tampered.Status);
        Assert.True(tampered.Png.IsEmpty);

        await File.WriteAllBytesAsync(path, original[..^1]);
        Assert.Equal(InspectionStatus.Tampered, (await harness.Store.InspectAsync(result.PhotographId.Value)).Status);

        File.Delete(path);
        Assert.Equal(InspectionStatus.Missing, (await harness.Store.InspectAsync(result.PhotographId.Value)).Status);
        Assert.Equal(InspectionStatus.Unknown, (await harness.Store.InspectAsync(Guid.NewGuid())).Status);
        Assert.Equal(InspectionStatus.Unknown, (await harness.Store.InspectAsync(Guid.Empty)).Status);
    }

    [Fact]
    public async Task Scenario7_ExplicitDeletion_RemovesOnlyThatFile_AndKeepsTheRecord()
    {
        await using var harness = await CreateAsync();
        var (first, firstResult) = await harness.PhotographAsync(T0, seed: 1);
        var (second, _) = await harness.PhotographAsync(T0.AddMinutes(5), seed: 2);

        var deleted = await harness.Store.DeleteAsync(firstResult.PhotographId!.Value, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddMinutes(20));

        Assert.Equal(DeletionStatus.Deleted, deleted.Status);
        var intent = Assert.Single(deleted.Intents);
        Assert.Equal(KeepsakeIntentKind.PhotographDeleted, intent.Kind);
        Assert.Equal(first.ActionId, intent.ActionId);
        Assert.Equal([$"{second.ActionId:N}.png"], harness.Files());

        var subject = await harness.Repository.RetrieveBySubjectAsync($"photo:{first.ActionId:N}");
        Assert.Equal(2, subject.Count);
        Assert.Equal(MemorySourceKind.UserCorrection, subject[0].Record.SourceKind);
        Assert.Equal("[neutral photograph deletion note]", subject[0].Record.VisibleRecollection);
        Assert.Contains(subject[0].Record.Links, link => link.Kind == MemoryLinkKind.Supersedes && link.TargetRecordId == firstResult.PhotographId);
        Assert.Equal(firstResult.PhotographId, subject[1].Record.RecordId);
        Assert.False(subject[1].IsCurrent);

        Assert.Equal(InspectionStatus.Deleted, (await harness.Store.InspectAsync(firstResult.PhotographId.Value)).Status);
        var listed = await harness.Store.ListAsync();
        Assert.Equal([false, true], listed.Select(entry => entry.Deleted));

        var again = await harness.Store.DeleteAsync(firstResult.PhotographId.Value, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddMinutes(30));
        Assert.Equal(DeletionStatus.AlreadyDeleted, again.Status);
        Assert.Empty(again.Intents);
        Assert.Equal(2, (await harness.Repository.RetrieveBySubjectAsync($"photo:{first.ActionId:N}")).Count);
        Assert.Equal(DeletionStatus.Unknown, (await harness.Store.DeleteAsync(Guid.NewGuid(), KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0)).Status);
        Assert.Equal(DeletionStatus.Unknown, (await harness.Store.DeleteAsync(subject[0].Record.RecordId, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0)).Status);
        await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Store.DeleteAsync(firstResult.PhotographId.Value, null!, T0));
    }

    [Fact]
    public async Task Scenario7_DeletionWhilePrivacyPaused_IsRejected_AndKeepsTheFile()
    {
        await using var harness = await CreateAsync();
        var (action, result) = await harness.PhotographAsync(T0);
        harness.Privacy.PauseAndRevoke();

        var refused = await harness.Store.DeleteAsync(result.PhotographId!.Value, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddMinutes(1));

        Assert.Equal(DeletionStatus.RecordRejected, refused.Status);
        Assert.Equal([$"{action.ActionId:N}.png"], harness.Files());
    }

    [Fact]
    public async Task Scenario8_NoCleanupSurface_OrphansAreReportedNotRemoved()
    {
        var assembly = typeof(KeepsakeStore).Assembly;
        Assert.DoesNotContain(
            assembly.GetExportedTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).Where(method => !method.IsSpecialName),
            method => new[] { "Delete", "Remove", "Clean", "Purge", "Prune", "Compact", "Trim" }.Any(word => method.Name.Contains(word, StringComparison.Ordinal)));
        Assert.False(typeof(KeepsakeDeletionAuthority).IsPublic);
        Assert.Empty(typeof(KeepsakeDeletionAuthority).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        await using var harness = await CreateAsync();
        var (kept, keptResult) = await harness.PhotographAsync(T0, seed: 1);
        var (gone, goneResult) = await harness.PhotographAsync(T0.AddMinutes(5), seed: 2);
        await harness.Store.DeleteAsync(goneResult.PhotographId!.Value, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddMinutes(6));
        await File.WriteAllBytesAsync(Path.Combine(harness.Location.RootPath, $"{gone.ActionId:N}.png"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(harness.Location.RootPath, $"{Guid.NewGuid():N}.png"), [4]);
        await File.WriteAllBytesAsync(Path.Combine(harness.Location.RootPath, ".stray.tmp"), [5]);
        // A non-canonical-case twin can only exist on a case-sensitive file system; on NTFS
        // it would be the kept photograph itself, so it is created only where it is distinct.
        var nonCanonical = $"{kept.ActionId.ToString("N").ToUpperInvariant()}.png";
        var caseSensitive = !File.Exists(Path.Combine(harness.Location.RootPath, nonCanonical));
        if (caseSensitive)
        {
            await File.WriteAllBytesAsync(Path.Combine(harness.Location.RootPath, nonCanonical), [6]);
        }

        var orphans = await harness.Store.FindOrphansAsync();

        Assert.Equal(caseSensitive ? 4 : 3, orphans.Count);
        if (caseSensitive)
        {
            Assert.Contains(nonCanonical, orphans);
            File.Delete(Path.Combine(harness.Location.RootPath, nonCanonical));
        }

        Assert.Contains($"{gone.ActionId:N}.png", orphans);
        Assert.Contains(".stray.tmp", orphans);
        Assert.DoesNotContain($"{kept.ActionId:N}.png", orphans);
        Assert.Equal(4, harness.Files().Length);
        Assert.Equal(InspectionStatus.Verified, (await harness.Store.InspectAsync(keptResult.PhotographId!.Value)).Status);

        var report = harness.Store.MeasureDiskGrowth();
        Assert.Equal(4, report.KeepsakeFiles);
        Assert.Equal(harness.Files().Sum(name => new FileInfo(Path.Combine(harness.Location.RootPath, name)).Length), report.KeepsakeBytes);
        Assert.True(report.MemoryRootFiles >= 2);
        Assert.True(report.MemoryRootBytes > 0);
        Assert.Equal(4, harness.Files().Length);
        Assert.Equal(2, (await harness.Store.ListAsync()).Count);
    }

    [Fact]
    public async Task Scenario9_RetryIsIdempotent_AndCompletesAnInterruptedWrite()
    {
        await using var harness = await CreateAsync();
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var frame = harness.Frame(harness.Grant, T0.AddSeconds(1));
        var first = await harness.TakeAsync(action, frame, T0.AddSeconds(1));
        var second = await harness.TakeAsync(action, frame, T0.AddSeconds(2));

        Assert.True(second.AlreadySaved);
        Assert.Empty(second.Intents);
        Assert.Equal(first.PhotographId, second.PhotographId);
        Assert.Single(harness.Files());
        Assert.Single(await harness.Store.ListAsync());

        // Interrupted earlier attempt: the file landed but the record did not.
        var interrupted = harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(5)).Action!;
        var interruptedFrame = harness.Frame(harness.Grant, T0.AddMinutes(5).AddSeconds(1), seed: 9);
        var (png, _, _) = KeepsakePng.Encode(interruptedFrame.Bgra32.Span, 64, 48, interruptedFrame.Stride, 1280);
        await File.WriteAllBytesAsync(Path.Combine(harness.Location.RootPath, $"{interrupted.ActionId:N}.png"), png);
        Assert.Single(await harness.Store.FindOrphansAsync());
        var completed = await harness.TakeAsync(interrupted, interruptedFrame, T0.AddMinutes(5).AddSeconds(2));
        Assert.Equal(KeepsakeRefusal.None, completed.Refusal);
        Assert.Equal(2, harness.Files().Length);
        Assert.Empty(await harness.Store.FindOrphansAsync());

        // A different file already under that name is never overwritten.
        var clash = harness.Camera.BeginCameraAction(harness.Grant, T0.AddMinutes(10)).Action!;
        var clashPath = Path.Combine(harness.Location.RootPath, $"{clash.ActionId:N}.png");
        await File.WriteAllBytesAsync(clashPath, [9, 9, 9]);
        Assert.Equal(KeepsakeRefusal.StoredFileMismatch, (await harness.TakeAsync(clash, harness.Frame(harness.Grant, T0.AddMinutes(10).AddSeconds(1)), T0.AddMinutes(10).AddSeconds(1))).Refusal);
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(clashPath));
        Assert.Equal(2, (await harness.Store.ListAsync()).Count);
    }

    [Fact]
    public async Task OversizedEncodings_AreRefusedWithoutWriting()
    {
        await using var harness = await CreateAsync(new KeepsakeConfiguration { MaximumEncodedBytes = 1024 });
        var action = harness.Camera.BeginCameraAction(harness.Grant, T0).Action!;
        var noise = new byte[256 * 256 * 4];
        new Random(42).NextBytes(noise);

        var result = await harness.TakeAsync(action, new PhotographFrame(new CaptureFrameMetadata(harness.Grant, 1, T0.AddSeconds(1), 256, 256), noise, 256 * 4), T0.AddSeconds(1));

        Assert.Equal(KeepsakeRefusal.EncodedTooLarge, result.Refusal);
        Assert.Empty(harness.Files());
    }

    [Fact]
    public async Task ARejectedRecord_IsReported_AndNeverClaimsASavedPhotograph()
    {
        await using var harness = await CreateAsync();
        var (first, _) = await harness.PhotographAsync(T0, seed: 3);

        // A second camera with the same identity derives the same action ID. The same pixels
        // match the existing file, but a different scope makes a conflicting record.
        var twin = new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, cameraId: CameraId);
        var action = twin.BeginCameraAction(harness.Grant, T0).Action!;
        Assert.Equal(first.ActionId, action.ActionId);
        var frame = harness.Frame(harness.Grant, T0.AddSeconds(1), seed: 3);
        var conflict = await twin.TakeAsync(action, frame, new KeepsakeContext("game.beta"), TargetContentPolicy.TrustedGame, PrivacyAssessment.Clear, T0.AddSeconds(1));

        Assert.Equal(KeepsakeRefusal.RecordConflict, conflict.Refusal);
        Assert.Null(conflict.PhotographId);
        Assert.Empty(conflict.Intents);
        Assert.Single(await harness.Store.ListAsync());
    }

    [Fact]
    public async Task Location_IsATestSiblingDirectory_AndConfigurationIsValidated()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(DataRootKind.Test, harness.Location.Kind);
        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(harness.MemoryLocation.RootPath)!, "Keepsakes"),
            harness.Location.RootPath);
        Assert.Throws<ArgumentNullException>(() => KeepsakeLocation.For(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, new KeepsakeConfiguration { MaximumActionsPerDay = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, new KeepsakeConfiguration { MaximumSavedEdge = 9000 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, new KeepsakeConfiguration { MaximumSavedEdge = 15 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, new KeepsakeConfiguration { ActionWindow = TimeSpan.Zero }));
        Assert.Throws<ArgumentException>(() => new KeepsakeCamera(harness.Repository, harness.Privacy, harness.Location, cameraId: Guid.Empty));
    }

    [Fact]
    public void Metadata_RoundTripsAndRejectsForeignShapes()
    {
        var metadata = new KeepsakeMetadata(Guid.NewGuid(), new string('a', 64), 10, 2, 3, T0, Deleted: false);

        Assert.Equal(metadata, KeepsakeMetadata.Parse(metadata.ToJson()));
        Assert.Null(KeepsakeMetadata.Parse(null));
        Assert.Null(KeepsakeMetadata.Parse("{}"));
        Assert.Null(KeepsakeMetadata.Parse("not json"));
        Assert.Null(KeepsakeMetadata.Parse(metadata.ToJson().Replace(new string('a', 64), new string('A', 64), StringComparison.Ordinal)));
        Assert.Null(KeepsakeMetadata.Parse(metadata.ToJson().Replace("\"png\"", "\"jpg\"", StringComparison.Ordinal)));
        Assert.Null(KeepsakeMetadata.Parse("{\"keepsake\":{\"actionId\":\"x\"}}"));
    }
}
