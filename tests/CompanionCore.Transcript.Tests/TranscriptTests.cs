using System.Reflection;
using System.Text;
using CompanionCore.Attention;
using CompanionCore.Conversation;
using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Transcript.Tests;

internal sealed class FixedChance : IExpressionChance
{
    public double Next() => 0.0;
}

/// <summary>An isolated synthetic test root with a coordinator wired through the recorder.</summary>
internal sealed class TranscriptHarness : IDisposable
{
    internal static readonly Guid Session = Guid.Parse("5e55105e-0000-4000-8000-0000000000e1");
    internal static readonly Guid CoordinatorIdentity = Guid.Parse("c0011d00-0000-4000-8000-0000000000e1");
    internal static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal TranscriptHarness(string? basePath = null, Func<string, bool>? containsSecret = null, TranscriptOptions? options = null)
    {
        BasePath = basePath ?? Path.Combine(Path.GetTempPath(), "CompanionCore.Transcript.Tests", Guid.NewGuid().ToString("N"));
        Location = TranscriptLocation.For(TestDataRootPolicy.Create(BasePath, Guid.Parse("00000000-0000-4000-8000-0000000000aa")));
        ContainsSecret = containsSecret;
        Options = options;
        Coordinator = new ConversationCoordinator(CoordinatorIdentity, Start, new FixedChance());
        Open();
    }

    internal string BasePath { get; }

    internal TranscriptLocation Location { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal Func<string, bool>? ContainsSecret { get; }

    internal TranscriptOptions? Options { get; }

    internal SessionTranscript Transcript { get; private set; } = null!;

    internal TranscriptRecorder Recorder { get; private set; } = null!;

    internal ConversationCoordinator Coordinator { get; set; }

    internal long Generation => Privacy.Snapshot.Generation;

    internal static DateTimeOffset At(double seconds) => Start + TimeSpan.FromSeconds(seconds);

    internal void Open(double seconds = 0)
    {
        Transcript = SessionTranscript.Open(Location, Session, Privacy, At(seconds), ContainsSecret, Options);
        Recorder = new TranscriptRecorder(Transcript);
    }

    internal void Restart(double seconds)
    {
        Transcript.Dispose();
        Open(seconds);
    }

    internal TranscriptContext Read() => TranscriptReader.Reconstruct(Location, Session);

    internal void Say(Speaker speaker, string text, double seconds)
    {
        Assert.True(Recorder.RecordUtterance(speaker, text, Coordinator.Current.Thread, Generation, At(seconds)).Accepted);
        if (speaker == Speaker.Boss)
        {
            Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(seconds));
        }
    }

    internal void Apply(ConversationUpdate update, double seconds) =>
        Assert.All(Recorder.Record(update, Generation, At(seconds)), result => Assert.True(result.Accepted));

    internal string FileText() =>
        File.Exists(Location.SessionPath(Session)) ? Encoding.UTF8.GetString(ReadShared(Location.SessionPath(Session))) : string.Empty;

    internal static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        Transcript.Dispose();
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}

public sealed class TranscriptTests
{
    private static void Converse(TranscriptHarness harness)
    {
        harness.Apply(harness.Coordinator.StartGameConversation("game.lore.ancient-door", false, TranscriptHarness.At(1)), 1);
        harness.Say(Speaker.Boss, "Who sealed the ancient door?", 2);
        harness.Say(Speaker.Prince, "Synthetic answer one.", 3);
        harness.Say(Speaker.Boss, "And why only at night?", 4);
    }

    private static void Interrupt(TranscriptHarness harness, double seconds)
    {
        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, TranscriptHarness.At(seconds));
        var urgent = harness.Coordinator.OnUrgent("threat.ambush", TranscriptHarness.At(seconds));
        Assert.All(
            harness.Recorder.RecordUrgent("threat.ambush", "Synthetic urgent observation.", urgent, harness.Generation, TranscriptHarness.At(seconds)),
            result => Assert.True(result.Accepted));
    }

    [Fact]
    public void Interruption_ReconstructsTheExactPreInterruptionContext()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        Interrupt(harness, 5);

        var context = harness.Read();

        var thread = context.CurrentThread!;
        Assert.Equal("game.lore.ancient-door", thread.Topic);
        Assert.Equal(ReconstructedThreadStatus.Interrupted, thread.Status);
        Assert.Equal(
            ["Who sealed the ancient door?", "Synthetic answer one.", "And why only at night?"],
            thread.PreInterruptionUtterances.Select(utterance => utterance.Text));
        Assert.Equal([Speaker.Boss, Speaker.Prince, Speaker.Boss], thread.PreInterruptionUtterances.Select(utterance => utterance.Speaker));
        Assert.Equal(thread.PreInterruptionUtterances[^1].EventId, thread.ResumePoint);
        Assert.Contains(TranscriptEventKind.UrgentObservation, context.Kinds);
        Assert.Contains(TranscriptEventKind.BnuyModeInterrupted, context.Kinds);
    }

    [Fact]
    public void OneThreadOrdering_SurvivesInterruptionRestartAndResumption()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        var threadId = harness.Coordinator.Current.Thread!.ThreadId;
        Interrupt(harness, 5);
        Assert.True(harness.Recorder.RecordCheckpoint(harness.Coordinator.Checkpoint(), harness.Generation, TranscriptHarness.At(5)).Accepted);

        // Crash: no SessionEnded. Restart restores the coordinator from the transcript.
        harness.Restart(60);
        Assert.True(harness.Transcript.Recovery.ReopenedUnended);
        var restoredFrom = harness.Read();
        harness.Coordinator = ConversationCoordinator.Restore(restoredFrom.LatestCheckpoint!, new FixedChance());

        var cleared = harness.Coordinator.ObserveAttention(AttentionState.Noticing, TranscriptHarness.At(61));
        harness.Apply(cleared, 61);
        var offer = Assert.Single(cleared.Intents, intent => intent.Kind == ConversationIntentKind.ResumeOffered);
        harness.Apply(harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), TranscriptHarness.At(62)), 62);
        harness.Say(Speaker.Prince, "Synthetic answer two.", 63);

        var context = harness.Read();
        var thread = context.CurrentThread!;
        Assert.Equal(threadId, thread.ThreadId);
        Assert.Equal(ReconstructedThreadStatus.Active, thread.Status);
        Assert.Equal(
            ["Who sealed the ancient door?", "Synthetic answer one.", "And why only at night?", "Synthetic answer two."],
            thread.RecentUtterances.Select(utterance => utterance.Text));
        Assert.Equal(thread.RecentUtterances.Select(utterance => utterance.Sequence).Order(), thread.RecentUtterances.Select(utterance => utterance.Sequence));
        Assert.NotNull(thread.InterruptionId);

        // The pre-interruption context stays exactly what preceded the interruption.
        Assert.Equal(
            ["Who sealed the ancient door?", "Synthetic answer one.", "And why only at night?"],
            thread.PreInterruptionUtterances.Select(utterance => utterance.Text));

        // The resumption references the very interruption it ends.
        var records = harness.FileText().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .ToArray();
        var interruption = records.Single(record => record.GetProperty("kind").GetString() == "bnuyModeInterrupted");
        var resumption = records.Single(record => record.GetProperty("kind").GetString() == "conversationResumed");
        Assert.Equal(interruption.GetProperty("eventId").GetGuid(), resumption.GetProperty("interruptionId").GetGuid());
        Assert.Equal(thread.InterruptionId, interruption.GetProperty("eventId").GetGuid());
        var kinds = context.Kinds.ToList();
        Assert.True(kinds.IndexOf(TranscriptEventKind.BnuyModeInterrupted) < kinds.LastIndexOf(TranscriptEventKind.SessionStarted));
        Assert.True(kinds.LastIndexOf(TranscriptEventKind.SessionStarted) < kinds.IndexOf(TranscriptEventKind.ReturnOffered));
        Assert.True(kinds.IndexOf(TranscriptEventKind.ReturnOffered) < kinds.IndexOf(TranscriptEventKind.ConversationResumed));
    }

    [Fact]
    public void LockedThread_SurvivesRestartThroughTheRecordedCheckpoint()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        harness.Apply(harness.Coordinator.SetLock(true, TranscriptHarness.At(5)), 5);
        var thread = harness.Coordinator.Current.Thread!;
        Assert.True(harness.Recorder.RecordCheckpoint(harness.Coordinator.Checkpoint(), harness.Generation, TranscriptHarness.At(5)).Accepted);

        harness.Restart(3600);
        var restored = ConversationCoordinator.Restore(harness.Read().LatestCheckpoint!, new FixedChance());

        Assert.Equal(thread.ThreadId, restored.Current.Thread!.ThreadId);
        Assert.True(restored.Current.Thread.Locked);
        Assert.Equal(ThreadStatus.Active, restored.Current.Thread.Status);
    }

    [Fact]
    public void AllRequiredStructuralEvents_AppearWithStableUniqueIdentifiers()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        Interrupt(harness, 5);
        var cleared = harness.Coordinator.ObserveAttention(AttentionState.Noticing, TranscriptHarness.At(60));
        harness.Apply(cleared, 60);
        var offer = Assert.Single(cleared.Intents, intent => intent.Kind == ConversationIntentKind.ResumeOffered);
        harness.Apply(harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), TranscriptHarness.At(61)), 61);
        harness.Transcript.End(TranscriptHarness.At(62));

        var context = harness.Read();
        Assert.Contains(TranscriptEventKind.ConversationActive, context.Kinds);
        Assert.Contains(TranscriptEventKind.BnuyModeInterrupted, context.Kinds);
        Assert.Contains(TranscriptEventKind.UrgentObservation, context.Kinds);
        Assert.Contains(TranscriptEventKind.ReturnOffered, context.Kinds);
        Assert.Contains(TranscriptEventKind.ConversationResumed, context.Kinds);
        Assert.True(context.SessionEnded);

        var lines = harness.FileText().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var ids = lines.Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement.GetProperty("eventId").GetGuid()).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, lines.Length).Select(sequence => SessionTranscript.DeriveEventId(TranscriptHarness.Session, sequence)),
            ids);
    }

    [Fact]
    public void ExcludedContent_IsRefusedAndNeverReachesDisk()
    {
        const string Secret = "SYNTHETIC-CREDENTIAL-ERPP-TEST";
        using var harness = new TranscriptHarness(containsSecret: text => text.Contains(Secret, StringComparison.Ordinal));
        harness.Apply(harness.Coordinator.StartGameConversation("game.chat", false, TranscriptHarness.At(1)), 1);
        var thread = harness.Coordinator.Current.Thread;

        Assert.Equal(TranscriptRefusal.CredentialEcho, harness.Recorder.RecordUtterance(Speaker.Boss, $"my key is {Secret}", thread, harness.Generation, TranscriptHarness.At(2)).Refusal);
        Assert.Equal(TranscriptRefusal.InvalidText, harness.Recorder.RecordUtterance(Speaker.Boss, new string('x', 4001), thread, harness.Generation, TranscriptHarness.At(3)).Refusal);
        Assert.Equal(TranscriptRefusal.InvalidText, harness.Recorder.RecordUtterance(Speaker.Boss, "bell\u0007OVERSIZE-MARKER", thread, harness.Generation, TranscriptHarness.At(4)).Refusal);
        Assert.Equal(TranscriptRefusal.InvalidText, harness.Recorder.RecordUtterance(Speaker.Boss, "   ", thread, harness.Generation, TranscriptHarness.At(5)).Refusal);

        var staleGeneration = harness.Generation;
        harness.Privacy.PauseAndRevoke();
        Assert.Equal(TranscriptRefusal.PrivacyPausedOrStale, harness.Recorder.RecordUtterance(Speaker.Boss, "PAUSED-MARKER", thread, staleGeneration, TranscriptHarness.At(6)).Refusal);
        harness.Privacy.ResumeExplicitly();
        Assert.Equal(TranscriptRefusal.PrivacyPausedOrStale, harness.Recorder.RecordUtterance(Speaker.Boss, "STALE-MARKER", thread, staleGeneration, TranscriptHarness.At(7)).Refusal);

        Assert.True(harness.Transcript.End(TranscriptHarness.At(8)).Accepted);
        Assert.Equal(TranscriptRefusal.SessionEnded, harness.Recorder.RecordUtterance(Speaker.Boss, "AFTER-END-MARKER", thread, harness.Generation, TranscriptHarness.At(9)).Refusal);
        Assert.Equal(TranscriptRefusal.SessionEnded, harness.Transcript.End(TranscriptHarness.At(10)).Refusal);

        var text = harness.FileText();
        foreach (var marker in new[] { Secret, "OVERSIZE-MARKER", "PAUSED-MARKER", "STALE-MARKER", "AFTER-END-MARKER", "xxxxxxxxxx" })
        {
            Assert.DoesNotContain(marker, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TranscriptHasNoImageOrByteField()
    {
        var properties = typeof(TranscriptEvent).GetProperties();

        Assert.DoesNotContain(properties, property =>
            property.PropertyType == typeof(byte[])
            || property.PropertyType == typeof(ReadOnlyMemory<byte>)
            || property.PropertyType == typeof(Memory<byte>));
    }

    [Theory]
    [InlineData("{\"v\":1,\"seq\":")]
    [InlineData("unterminated")]
    public void TornTail_IsTruncatedOnReopen_AndIgnoredByTheReader(string torn)
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        harness.Transcript.Dispose();
        File.AppendAllText(harness.Location.SessionPath(TranscriptHarness.Session), torn);

        Assert.Equal(3, harness.Read().CurrentThread!.RecentUtterances.Count);
        harness.Open(10);

        Assert.True(harness.Transcript.Recovery.TornTailTruncated);
        harness.Say(Speaker.Prince, "Synthetic answer after recovery.", 11);
        Assert.Equal(4, harness.Read().CurrentThread!.RecentUtterances.Count);
    }

    [Fact]
    public void InteriorCorruption_IsPreservedAside_AndAContinuationBegins()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        harness.Transcript.Dispose();
        var path = harness.Location.SessionPath(TranscriptHarness.Session);
        var original = File.ReadAllText(path);
        var tampered = original.Replace("Who sealed", "Who forged", StringComparison.Ordinal);
        File.WriteAllText(path, tampered);

        Assert.Throws<InvalidDataException>(() => harness.Read());
        harness.Open(10);

        Assert.True(harness.Transcript.Recovery.CorruptTranscriptPreserved);
        var preserved = Assert.Single(Directory.GetFiles(harness.Location.CorruptDirectoryPath));
        Assert.Equal(tampered, File.ReadAllText(preserved));
        var context = harness.Read();
        Assert.Equal([TranscriptEventKind.SessionStarted], context.Kinds);
    }

    [Fact]
    public void SequenceGap_IsTreatedAsCorruption()
    {
        using var harness = new TranscriptHarness();
        Converse(harness);
        harness.Transcript.Dispose();
        var path = harness.Location.SessionPath(TranscriptHarness.Session);
        var lines = File.ReadAllLines(path).ToList();
        lines.RemoveAt(2);
        File.WriteAllText(path, string.Join('\n', lines) + "\n");

        Assert.Throws<InvalidDataException>(() => harness.Read());
        harness.Open(10);
        Assert.True(harness.Transcript.Recovery.CorruptTranscriptPreserved);
    }

    [Fact]
    public void SecondWriter_IsFencedWhileTheFirstIsOpen()
    {
        using var harness = new TranscriptHarness();

        Assert.Throws<TranscriptBusyException>(() =>
            SessionTranscript.Open(harness.Location, TranscriptHarness.Session, harness.Privacy, TranscriptHarness.At(1)));
    }

    [Fact]
    public void EndedSession_ReopensReadOnly()
    {
        using var harness = new TranscriptHarness();
        harness.Transcript.End(TranscriptHarness.At(1));
        harness.Restart(2);

        Assert.True(harness.Transcript.IsEnded);
        Assert.False(harness.Transcript.Recovery.ReopenedUnended);
        Assert.Equal(TranscriptRefusal.SessionEnded, harness.Recorder.RecordUtterance(Speaker.Boss, "late", null, harness.Generation, TranscriptHarness.At(3)).Refusal);
    }

    [Fact]
    public void SessionBounds_HoldAndStillAllowAnEnd()
    {
        using var harness = new TranscriptHarness(options: new TranscriptOptions { MaximumEvents = 6 });
        harness.Apply(harness.Coordinator.StartGameConversation("game.chat", false, TranscriptHarness.At(1)), 1);
        var results = Enumerable.Range(0, 10)
            .Select(index => harness.Recorder.RecordUtterance(Speaker.Boss, $"line {index}", harness.Coordinator.Current.Thread, harness.Generation, TranscriptHarness.At(2 + index)))
            .ToArray();

        Assert.Contains(results, result => result.Refusal == TranscriptRefusal.BoundExceeded);
        Assert.True(harness.Transcript.End(TranscriptHarness.At(20)).Accepted);
        Assert.True(harness.Transcript.EventCount <= 6);
        Assert.Equal(harness.Transcript.EventCount, harness.Read().Kinds.Count);
    }

    [Fact]
    public void TranscriptWork_NeverReachesCommittedMemory()
    {
        var forbidden = new[] { typeof(LocalWriteGate), typeof(MemoryRepository) };
        foreach (var type in typeof(SessionTranscript).Assembly.GetTypes())
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var members = type.GetFields(all).Select(field => field.FieldType)
                .Concat(type.GetProperties(all).Select(property => property.PropertyType))
                .Concat(type.GetMethods(all).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                .Concat(type.GetConstructors(all).SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
            Assert.DoesNotContain(members, member => forbidden.Contains(member));
        }

        using var harness = new TranscriptHarness();
        Converse(harness);
        Interrupt(harness, 5);
        Assert.Empty(Directory.GetFiles(harness.BasePath, "*.db", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(harness.Location.RootPath)!, "Memory")));
    }

    [Fact]
    public void IdenticalInputs_ProduceIdenticalTranscripts()
    {
        static byte[] Run()
        {
            using var harness = new TranscriptHarness();
            Converse(harness);
            Interrupt(harness, 5);
            harness.Recorder.RecordCheckpoint(harness.Coordinator.Checkpoint(), harness.Generation, TranscriptHarness.At(6));
            harness.Transcript.End(TranscriptHarness.At(7));
            harness.Transcript.Dispose();
            return TranscriptHarness.ReadShared(harness.Location.SessionPath(TranscriptHarness.Session));
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Location_IsAValidatedSiblingAndRejectsUnrecognizedRoots()
    {
        using var harness = new TranscriptHarness();
        Assert.Equal(DataRootKind.Test, harness.Location.Kind);
        Assert.EndsWith("Transcripts", harness.Location.RootPath, StringComparison.Ordinal);
        Assert.StartsWith(harness.BasePath, harness.Location.RootPath, StringComparison.Ordinal);

        var root = Path.Combine(Path.GetTempPath(), "CompanionCore.Transcript.Tests", Guid.NewGuid().ToString("N"), "Memory");
        Assert.Throws<DataRootViolationException>(() => TranscriptLocation.For(new MemoryStoreLocation((DataRootKind)99, "CompanionCore", root, "memory-v1.db")));
        Assert.Throws<DataRootViolationException>(() => TranscriptLocation.For(new MemoryStoreLocation(DataRootKind.Test, "CompanionCore", root, "memory-v1.db")));
    }

    [Fact]
    public void InvalidOptionsAndArguments_AreRejected()
    {
        using var harness = new TranscriptHarness();
        Assert.ThrowsAny<ArgumentException>(() => SessionTranscript.Open(harness.Location, Guid.Empty, harness.Privacy, TranscriptHarness.At(1)));
        Assert.ThrowsAny<ArgumentException>(() => SessionTranscript.Open(harness.Location, Guid.NewGuid(), harness.Privacy, TranscriptHarness.At(1), options: new TranscriptOptions { MaximumEvents = 1 }));
        Assert.ThrowsAny<ArgumentException>(() => harness.Recorder.RecordUtterance((Speaker)9, "x", null, harness.Generation, TranscriptHarness.At(1)));
    }
}
