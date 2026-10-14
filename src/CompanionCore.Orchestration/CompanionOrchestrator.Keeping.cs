using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.BunDex;
using CompanionCore.Curtain;
using CompanionCore.Memory;
using CompanionCore.Sightings;

namespace CompanionCore.Orchestration;

/// <summary>
/// Keeping during play (M5a, Boss 2026-10-14: "The Little Self decides. The subconscious helps
/// facilitate it. He decides."). The subconscious notices that something new happened, or that a
/// pattern grew into a new magnitude, and the moment reaches him through his Metaconscious. Nothing
/// tells him how worth keeping it is, and nothing mentions not writing: whether he writes, how much,
/// and where, are his. Moments reach him one at a time, never during a conversation or an intense
/// moment; one he doesn't get to simply fades from the queue, and stays in his subconscious memory.
/// The BrainCase keeps hidden links from what he writes to where it came from.
/// </summary>
public sealed partial class CompanionOrchestrator
{
    internal const int MaximumPendingKeeps = 8;
    internal static readonly TimeSpan KeepFadesAfter = TimeSpan.FromMinutes(10);

    private readonly Queue<PendingKeep> _keeping = new();
    private bool _keepInFlight;

    internal sealed record PendingKeep(Sense WhatHappened, PageTags Tags, IReadOnlyList<string> Sources, DateTimeOffset At, string Session, long Generation);

    /// <summary>Moments waiting to reach him (for tests and the pulse).</summary>
    internal int PendingKeeps => _keeping.Count;

    /// <summary>Every memory newly committed from a look reaches him as something he saw.</summary>
    private async Task OfferNewMemoriesAsync(ActiveSession session, BridgeMemoryResult memory)
    {
        if (_c.Keeper is null || memory.GateResult is not { Status: WriteGateStatus.Committed, RecordIds.Count: > 0 } committed)
        {
            return;
        }

        IReadOnlyList<RetrievedMemory> records;
        try
        {
            records = await _c.Repository.RetrieveAsync(new MemoryQuery { RecordIds = committed.RecordIds }, _shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
        {
            RecordFault(exception);
            return;
        }

        foreach (var record in records.OrderBy(record => record.JournalSequence))
        {
            Offer(
                session,
                Membrane.Saw(record.Record.VisibleRecollection, record.Record.Confidence),
                new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, session.Game, record.Record.SaveReference, [record.Record.SubjectKey], record.Record.CreatedAtUtc),
                [PageSources.Memory(record.Record.RecordId)]);
        }

        KeepNext();
    }

    /// <summary>A pattern grew into a new magnitude: "saws MANYS trees!!", never once per tree.</summary>
    private void OnPatternGrew(Impression grown) => Post(() =>
    {
        if (_session is { } session && string.Equals(grown.Game, session.Game, StringComparison.Ordinal))
        {
            Offer(
                session,
                ImpressionSense.Of(grown),
                new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, grown.Game, null, [grown.Thing], grown.Last),
                [PageSources.Sightings(grown.Game, grown.Thing, grown.Count)]);
            KeepNext();
        }

        return Task.CompletedTask;
    });

    private void Offer(ActiveSession session, Sense? whatHappened, PageTags tags, IReadOnlyList<string> sources)
    {
        // Privacy is checked when a moment would reach him, and again just before he writes.
        if (_c.Keeper is null || whatHappened is null)
        {
            return;
        }

        _keeping.Enqueue(new PendingKeep(whatHappened, tags, sources, Now(), session.Reference, session.Grant.Generation));
        while (_keeping.Count > MaximumPendingKeeps)
        {
            // The oldest moment fades; it is still in his subconscious memory.
            _keeping.Dequeue();
        }
    }

    /// <summary>One moment at a time, when nothing else is asking for him.</summary>
    private void KeepNext()
    {
        if (_c.Keeper is not { } keeper || _keepInFlight || _session is not { } session)
        {
            return;
        }

        var now = Now();
        while (_keeping.TryPeek(out var next)
               && (now - next.At > KeepFadesAfter || !string.Equals(next.Session, session.Reference, StringComparison.Ordinal) || !_c.Privacy.IsCurrent(next.Generation)))
        {
            _keeping.Dequeue();
        }

        if (_keeping.Count == 0 || _conversation.Current.Thread is not null || _conversation.Current.Attention == AttentionState.HighAttention)
        {
            return;
        }

        if (_c.BunDex is not { Health: BunDexHealth.Healthy })
        {
            // A damaged (read-only) BunDex keeps nothing until it is repaired.
            _keeping.Clear();
            return;
        }

        var moment = _keeping.Dequeue();
        _keepInFlight = true;
        var token = _shutdown.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                if (_c.Privacy.IsCurrent(moment.Generation))
                {
                    await keeper.KeepAsync(moment.Tags.Role, moment.Tags, moment.WhatHappened, token, moment.Sources).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
            {
                RecordFault(exception);
            }
            finally
            {
                Post(() =>
                {
                    _keepInFlight = false;
                    KeepNext();
                    return Task.CompletedTask;
                });
            }
        });
    }
}
