using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Conversation;
using CompanionCore.Keepsakes;
using CompanionCore.Orchestration;
using CompanionCore.Watchbun;

namespace CompanionCore.Presentation.Tests;

/// <summary>
/// Every orchestration notice, and every member of every intent family it can carry, maps to
/// a defined neutral placeholder: the mapping is total, deterministic, and never "unknown".
/// </summary>
public sealed class CompanionNoticeMappingTests
{
    private static readonly NeutralPersonalityAdapter Adapter = new();

    private static readonly string Unknown = PlaceholderStrings.ByContentKey[NeutralPersonalityAdapter.UnknownKey];

    public static IEnumerable<object[]> EveryNotice()
    {
        foreach (var kind in Enum.GetValues<CompanionNoticeKind>())
        {
            yield return [new CompanionNotice(kind)];
        }

        foreach (var member in Enum.GetValues<AttentionIntentKind>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.Attention, Attention: new AttentionIntent(member))];
        }

        foreach (var member in Enum.GetValues<ConversationIntentKind>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.Conversation, Conversation: new ConversationIntent(member))];
        }

        foreach (var member in Enum.GetValues<WatchbunIntentKind>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.Watchbun, Watchbun: new WatchbunIntent(member))];
        }

        foreach (var member in Enum.GetValues<KeepsakeIntentKind>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.Keepsake, Keepsake: new KeepsakeIntent(member, Guid.NewGuid()))];
        }

        foreach (var member in Enum.GetValues<BridgeOutcomeKind>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.Braincase, Braincase: member)];
        }

        foreach (var member in Enum.GetValues<KeepsakeRefusal>())
        {
            yield return [new CompanionNotice(CompanionNoticeKind.PhotographRefused, PhotographRefusal: member)];
        }
    }

    [Theory]
    [MemberData(nameof(EveryNotice))]
    public void EveryNotice_ResolvesToADefinedPlaceholder(CompanionNotice notice)
    {
        var content = Adapter.Map(notice);

        Assert.StartsWith(NeutralPersonalityAdapter.CompanionPrefix, content.ContentKey, StringComparison.Ordinal);
        var text = PlaceholderStrings.Resolve(content);
        Assert.NotEqual(Unknown, text);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("{", text, StringComparison.Ordinal);
        Assert.Equal(content, Adapter.Map(notice));
    }

    [Fact]
    public void TypedIntents_CarryTheirMemberAsNeutralDetail()
    {
        var content = Adapter.Map(new CompanionNotice(CompanionNoticeKind.Watchbun, Watchbun: new WatchbunIntent(WatchbunIntentKind.WatchTaskCompleted)));

        Assert.Equal("companion.watchbun.watch-task-completed", content.ContentKey);
        Assert.Equal("watch task completed", content.NeutralDetail);
        Assert.Equal("Watchbun: watch task completed.", PlaceholderStrings.Resolve(content));
    }

    [Fact]
    public void FixedNotices_UseExpressionIntentsForPrivacyAndRecovery()
    {
        Assert.Equal(ExpressionIntent.PrivacyPaused, Adapter.Map(new CompanionNotice(CompanionNoticeKind.PrivacyPaused)).Intent);
        Assert.Equal(ExpressionIntent.Recovering, Adapter.Map(new CompanionNotice(CompanionNoticeKind.Recovering)).Intent);
        Assert.Equal(ExpressionIntent.None, Adapter.Map(new CompanionNotice(CompanionNoticeKind.SessionStarted)).Intent);
        Assert.Equal("companion.vault-backup-failed", Adapter.Map(new CompanionNotice(CompanionNoticeKind.VaultBackupFailed)).ContentKey);
    }

    [Fact]
    public void UndefinedValues_StillProduceADefinedKey()
    {
        var undefinedKind = Adapter.Map(new CompanionNotice((CompanionNoticeKind)999));
        var undefinedMember = Adapter.Map(new CompanionNotice(CompanionNoticeKind.Watchbun, Watchbun: new WatchbunIntent((WatchbunIntentKind)999)));
        var noneRefusal = Adapter.Map(new CompanionNotice(CompanionNoticeKind.PhotographRefused, PhotographRefusal: KeepsakeRefusal.None));

        Assert.Equal("companion.unknown", undefinedKind.ContentKey);
        Assert.Equal("companion.watchbun", undefinedMember.ContentKey);
        Assert.Equal("companion.photograph-refused", noneRefusal.ContentKey);
        Assert.Throws<ArgumentNullException>(() => Adapter.Map((CompanionNotice)null!));
        Assert.Equal(Unknown, PlaceholderStrings.Resolve(new PresentationContent("target.ended.extra", ExpressionIntent.None)));
        Assert.Equal(Unknown, PlaceholderStrings.Resolve(new PresentationContent("companion.nonexistent.member", ExpressionIntent.None)));
    }

    [Theory]
    [InlineData("SessionStarted", "session-started")]
    [InlineData("A", "a")]
    [InlineData("WatchTaskCompleted", "watch-task-completed")]
    public void KebabCase_IsStable(string pascal, string expected) =>
        Assert.Equal(expected, NeutralPersonalityAdapter.Kebab(pascal));
}
