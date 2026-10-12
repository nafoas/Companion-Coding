using CompanionCore.Attention;
using CompanionCore.Conversation;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// FIX-10: milestone check 3 by hand. After Bnuy Mode ends, Prince's opening (an offer to talk, or an
/// ambient comment) arrives with the Afterglow; Boss's Reply button answers that opening through <see cref="ReplyTarget"/>, and
/// the reply starts the one Conversation Thread. Before the fix the buttons sent no reference
/// and were refused with NoActiveThread, so nothing happened.
/// </summary>
public sealed class OrchestrationReplyTests
{
    private static bool IsOpening(CompanionNotice notice) =>
        notice.Conversation?.Kind is ConversationIntentKind.AmbientExpression or ConversationIntentKind.OfferOpening;

    [Fact]
    public async Task AReplyToTheAfterglowOpening_StartsTheOneConversationThread()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        var target = new ReplyTarget();

        // Nothing has opened yet: an unreferenced reply is refused, and the target is empty.
        Assert.Null(target.Current);
        Assert.Equal(ConversationRefusal.NoActiveThread, await target.ReplyAsync(harness.Orchestrator, UserTurnKind.Substantive));

        // A decisive moment: Bnuy Mode.
        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2)));
        await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}");
        await harness.SettleAsync();
        Assert.Equal(AttentionState.HighAttention, (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State);

        // It calms down: High Attention ends, and the Afterglow brings one opening.
        for (var step = 0; step < 60 && !harness.Notices.Any(IsOpening); step++)
        {
            await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(5));
        }

        foreach (var notice in harness.Notices)
        {
            target.Observe(notice);
        }

        var comment = Assert.Single(harness.Notices, IsOpening).Conversation!;
        Assert.Equal(comment.ReferenceId, target.Current);

        // Boss answers it: accepted, and exactly one thread starts.
        Assert.Equal(ConversationRefusal.None, await target.ReplyAsync(harness.Orchestrator, UserTurnKind.Substantive));
        await harness.SettleAsync();
        foreach (var notice in harness.Notices)
        {
            target.Observe(notice);
        }

        Assert.Single(harness.Notices, n => n.Conversation?.Kind == ConversationIntentKind.ThreadStarted);
        Assert.Null(target.Current);
        Assert.NotNull((await harness.Orchestrator.GetSnapshotAsync()).Conversation.Thread);

        // Further replies go to that thread.
        Assert.Equal(ConversationRefusal.None, await target.ReplyAsync(harness.Orchestrator, UserTurnKind.Acknowledgement));
    }
}
