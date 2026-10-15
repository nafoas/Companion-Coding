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

    internal static readonly TimeSpan KeepFinishBound = TimeSpan.FromSeconds(10);

    private readonly Queue<PendingKeep> _keeping = new();
    private bool _keepInFlight;
    private Task _keepTask = Task.CompletedTask;

    internal sealed record PendingKeep(Sense WhatHappened, PageTags Tags, IReadOnlyList<string> Sources, DateTimeOffset At, string Session, long Generation, Sense? Heard = null);

    /// <summary>The last thing that reached him this session: what "this" means when Boss says "Remember this, Prince".</summary>
    private PendingKeep? _lastMoment;

    /// <summary>Moments waiting to reach him (for tests and the pulse).</summary>
    internal int PendingKeeps => _keeping.Count;

    /// <summary>Every memory newly committed from a look reaches him as something he saw.</summary>
    private async Task OfferNewMemoriesAsync(ActiveSession session, BridgeMemoryResult memory)
    {
        if (_c.Keeper is null || NewlyCommitted(memory) is not { } committed)
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

    /// <summary>Only memories this look newly committed; a replayed look (already committed) offers nothing again.</summary>
    internal static WriteGateResult? NewlyCommitted(BridgeMemoryResult memory) =>
        memory.GateResult is { Status: WriteGateStatus.Committed, RecordIds.Count: > 0 } committed ? committed : null;

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

        _keeping.Enqueue(_lastMoment = new PendingKeep(whatHappened, tags, sources, Now(), session.Reference, session.Grant.Generation));
        while (_keeping.Count > MaximumPendingKeeps)
        {
            // The oldest moment fades; it is still in his subconscious memory.
            _keeping.Dequeue();
        }
    }

    /// <summary>
    /// "Remember this, Prince" (M5b, Boss 2026-10-14): an earnest ask from Boss, never a command.
    /// It reaches him as Boss's own words with what just happened ("maybees I should write dis
    /// down"), ahead of anything else waiting; what he does with it is his. It changes nothing
    /// underneath by itself: if he writes about it, delights in it, or comes back to it, those are
    /// his own experiences, and they count like any other. False when there is no one to hear it.
    /// </summary>
    public Task<bool> RememberThisAsync(string words) =>
        InvokeAsync(() =>
        {
            if (_c.Keeper is null || _session is not { } session || !_c.Privacy.IsCurrent(session.Grant.Generation) || Membrane.BossSaid(words ?? string.Empty) is not { } heard)
            {
                return Task.FromResult(false);
            }

            var now = Now();
            var moment = _lastMoment is { } last && string.Equals(last.Session, session.Reference, StringComparison.Ordinal) && now - last.At <= KeepFadesAfter
                ? last with { Heard = heard, At = now, Generation = session.Grant.Generation }
                : new PendingKeep(heard, new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, session.Game, null, [], now), [], now, session.Reference, session.Grant.Generation);
            var waiting = _keeping.ToArray();
            _keeping.Clear();
            _keeping.Enqueue(moment);
            foreach (var other in waiting.Take(MaximumPendingKeeps - 1))
            {
                _keeping.Enqueue(other);
            }

            KeepNext();
            return Task.FromResult(true);
        });

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
        _keepTask = Task.Run(async () =>
        {
            try
            {
                if (_c.Privacy.IsCurrent(moment.Generation))
                {
                    await keeper.KeepAsync(moment.Tags.Role, moment.Tags, moment.WhatHappened, token, moment.Sources, moment.Heard).ConfigureAwait(false);
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

    /// <summary>Waits, bounded, for a moment he is writing; past the bound it is cancelled and awaited briefly.</summary>
    private async Task FinishKeepingAsync()
    {
        try
        {
            await _keepTask.WaitAsync(KeepFinishBound).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
            await Task.WhenAny(_keepTask, Task.Delay(KeepFinishBound)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Its own failure was already recorded; shutdown continues.
        }
    }
}
