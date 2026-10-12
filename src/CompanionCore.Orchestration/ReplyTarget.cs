using CompanionCore.Conversation;

namespace CompanionCore.Orchestration;

/// <summary>
/// FIX-10: which of Prince's openings a placeholder reply answers. Answering an ambient
/// comment or an offer must name it (the coordinator refuses an unreferenced reply when no
/// thread is active); once a thread is running, replies go to that thread. Boss's Reply
/// buttons go through this, so a reply to the latest comment starts its Conversation Thread.
/// </summary>
public sealed class ReplyTarget
{
    private readonly object _gate = new();
    private Guid? _reference;

    /// <summary>The opening the next reply answers, or null to answer the active thread.</summary>
    public Guid? Current
    {
        get
        {
            lock (_gate)
            {
                return _reference;
            }
        }
    }

    public void Observe(CompanionNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (notice.Conversation is not { } intent)
        {
            return;
        }

        lock (_gate)
        {
            switch (intent.Kind)
            {
                case ConversationIntentKind.AmbientExpression or ConversationIntentKind.OfferOpening or ConversationIntentKind.ResumeOffered
                    when intent.ReferenceId is { } reference:
                    _reference = reference;
                    break;
                case ConversationIntentKind.ThreadStarted or ConversationIntentKind.ThreadResumed:
                    _reference = null;
                    break;
            }
        }
    }

    /// <summary>
    /// Replies to the latest opening; an opening that has already expired falls back to the
    /// active thread, if there is one.
    /// </summary>
    public async Task<ConversationRefusal> ReplyAsync(CompanionOrchestrator orchestrator, UserTurnKind kind)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        var reference = Current;
        var result = await orchestrator.SubmitUserTurnAsync(new UserTurn(kind, reference)).ConfigureAwait(false);
        if (reference is null || result is not (ConversationRefusal.None or ConversationRefusal.UnknownReference))
        {
            return result;
        }

        lock (_gate)
        {
            if (_reference == reference)
            {
                _reference = null;
            }
        }

        return result == ConversationRefusal.UnknownReference
            ? await orchestrator.SubmitUserTurnAsync(new UserTurn(kind)).ConfigureAwait(false)
            : result;
    }
}
