using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class VisibleNotificationProcessorTests
{
    [Fact]
    public void StartupBaselineDoesNotPlayExistingVisibleNotification()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        var result = processor.Process([CodexNotification(1, "task complete")], isEnabled: true, played.Add);

        Assert.Empty(played);
        Assert.Equal("startup-baseline-notification-skipped", Assert.Single(result.Events).Code);
    }

    [Fact]
    public void NewCodexNotificationPlaysHelperSoundAfterBaseline()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        processor.Process([], isEnabled: true, played.Add);
        var result = processor.Process([CodexNotification(1, "task complete")], isEnabled: true, played.Add);

        Assert.Single(played);
        Assert.Equal("matched-playback-requested", Assert.Single(result.Events).Code);
        Assert.Equal(1, result.PlaybackRequests);
    }

    [Fact]
    public void NewClaudeDesktopNotificationPlaysHelperSoundAfterBaseline()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        processor.Process([], isEnabled: true, played.Add);
        var result = processor.Process([ClaudeDesktopNotification(1, "message ready")], isEnabled: true, played.Add);

        Assert.Single(played);
        Assert.Equal("matched-playback-requested", Assert.Single(result.Events).Code);
        Assert.Equal(1, result.PlaybackRequests);
    }

    [Fact]
    public void CodexAndClaudeDesktopPlaybackCanBeDisabledIndependently()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        processor.Process([], _ => true, (record, _) => played.Add(record));
        var result = processor.Process(
            [
                CodexNotification(1, "task complete"),
                ClaudeDesktopNotification(2, "message ready")
            ],
            decision => decision.TargetApp == TargetNotificationApp.Codex,
            (record, _) => played.Add(record));

        var playedRecord = Assert.Single(played);
        Assert.Equal("Codex", playedRecord.AppDisplayName);
        Assert.Equal(2, result.Matched);
        Assert.Equal(1, result.PlaybackRequests);
        Assert.Equal(1, result.Ignored);
        Assert.Contains(result.Events, item => item.PlaybackResult?.MatchDecision.TargetApp == TargetNotificationApp.ClaudeDesktop &&
                                               item.Code == "playback-paused");
    }

    [Fact]
    public void StillVisibleNotificationDoesNotReplayOnEveryPoll()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();
        var notification = CodexNotification(1, "task complete");

        processor.Process([], isEnabled: true, played.Add);
        processor.Process([notification], isEnabled: true, played.Add);
        var result = processor.Process([notification], isEnabled: true, played.Add);

        Assert.Single(played);
        Assert.Empty(result.Events);
        Assert.Equal(1, result.DuplicatesSkipped);
    }

    [Fact]
    public void MultipleDifferentCodexNotificationsInOnePollEachPlay()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        processor.Process([], isEnabled: true, played.Add);
        var result = processor.Process(
            [
                CodexNotification(1, "task complete"),
                CodexNotification(2, "second task complete")
            ],
            isEnabled: true,
            played.Add);

        Assert.Equal(2, played.Count);
        Assert.Equal(2, result.PlaybackRequests);
        Assert.All(result.Events, item => Assert.Equal("matched-playback-requested", item.Code));
    }

    [Fact]
    public void HelperOwnedNotificationDoesNotSelfTriggerPlayback()
    {
        var processor = new VisibleNotificationProcessor();
        var played = new List<NotificationRecord>();

        processor.Process([], isEnabled: true, played.Add);
        var result = processor.Process(
            [
                CodexNotification(1, "helper status") with
                {
                    AppDisplayName = HelperOwnedNotificationFilter.HelperDisplayName,
                    AppUserModelId = HelperOwnedNotificationFilter.HelperDisplayName,
                    PackageFamilyName = CodexNotificationMatcher.CodexPackageFamilyName
                }
            ],
            isEnabled: true,
            played.Add);

        Assert.Empty(played);
        Assert.Equal("ignored-helper-owned-notification", Assert.Single(result.Events).Code);
    }

    [Fact]
    public void PlaybackFailureIsRecoverableProcessingEvent()
    {
        var processor = new VisibleNotificationProcessor();

        processor.Process([], isEnabled: true, _ => { });
        var result = processor.Process(
            [CodexNotification(1, "task complete")],
            isEnabled: true,
            _ => throw new InvalidOperationException("sound device unavailable"));

        var item = Assert.Single(result.Events);
        Assert.Equal("playback-failure", item.Code);
        Assert.Equal(LogLevel.Error, item.Level);
        Assert.Equal(1, result.PlaybackFailures);
    }

    private static NotificationRecord CodexNotification(uint id, string text)
    {
        return new NotificationRecord
        {
            SchemaVersion = 1,
            CreationTime = new DateTimeOffset(2026, 05, 28, 10, 0, (int)id, TimeSpan.Zero),
            NotificationId = id,
            AppDisplayName = "Codex",
            AppUserModelId = CodexNotificationMatcher.CodexAppUserModelId,
            AppId = "App",
            PackageFamilyName = CodexNotificationMatcher.CodexPackageFamilyName,
            RawXmlSha256 = "xml-" + id
        }.WithSanitizedTextLines(["Codex", text]);
    }

    private static NotificationRecord ClaudeDesktopNotification(uint id, string text)
    {
        return new NotificationRecord
        {
            SchemaVersion = 1,
            CreationTime = new DateTimeOffset(2026, 06, 04, 10, 0, (int)id, TimeSpan.Zero),
            NotificationId = id,
            AppDisplayName = "Claude",
            AppUserModelId = CodexNotificationMatcher.ClaudeDesktopAppUserModelId,
            AppId = "Claude",
            PackageFamilyName = CodexNotificationMatcher.ClaudeDesktopPackageFamilyName,
            PackageFullName = "Claude_1.10628.2.0_x64__pzs8sxrjxfjjc",
            RawXmlSha256 = "claude-xml-" + id
        }.WithSanitizedTextLines(["Claude", text]);
    }
}
