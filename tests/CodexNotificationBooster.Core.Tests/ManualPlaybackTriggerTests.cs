using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class ManualPlaybackTriggerTests
{
    private sealed class FakePlayback : INotificationPlayback
    {
        public int PlayCount { get; private set; }

        public NotificationMatchDecision? LastDecision { get; private set; }

        public bool ThrowOnPlay { get; init; }

        public void Play(NotificationRecord record, NotificationMatchDecision matchDecision)
        {
            PlayCount++;
            LastDecision = matchDecision;
            if (ThrowOnPlay)
            {
                throw new InvalidOperationException("sound device unavailable");
            }
        }
    }

    private static ManualPlaybackTrigger Create(
        FakePlayback playback,
        Func<NotificationMatchDecision, bool> enabled,
        Func<DateTimeOffset> now,
        TimeSpan? minInterval = null,
        List<string>? codes = null)
    {
        return new ManualPlaybackTrigger(
            playback,
            enabled,
            (level, code, message, metadata) => codes?.Add(code),
            now,
            minInterval ?? TimeSpan.FromMilliseconds(750));
    }

    [Fact]
    public void FirePlaysWhenEnabledAndTargetsClaudeDesktop()
    {
        var playback = new FakePlayback();
        var codes = new List<string>();
        var trigger = Create(playback, _ => true, () => DateTimeOffset.UnixEpoch, codes: codes);

        var outcome = trigger.Fire("test");

        Assert.Equal(ManualPlaybackTriggerOutcome.Played, outcome);
        Assert.Equal(1, playback.PlayCount);
        Assert.Equal(TargetNotificationApp.ClaudeDesktop, playback.LastDecision?.TargetApp);
        Assert.True(playback.LastDecision?.Matched);
        Assert.Contains("manual-trigger-played", codes);
    }

    [Fact]
    public void FireSkipsWhenPlaybackDisabled()
    {
        var playback = new FakePlayback();
        var trigger = Create(playback, _ => false, () => DateTimeOffset.UnixEpoch);

        var outcome = trigger.Fire("test");

        Assert.Equal(ManualPlaybackTriggerOutcome.SkippedDisabled, outcome);
        Assert.Equal(0, playback.PlayCount);
    }

    [Fact]
    public void RapidSecondFireIsThrottled()
    {
        var playback = new FakePlayback();
        var now = DateTimeOffset.UnixEpoch;
        var trigger = Create(playback, _ => true, () => now, TimeSpan.FromMilliseconds(750));

        Assert.Equal(ManualPlaybackTriggerOutcome.Played, trigger.Fire("a"));
        now = now.AddMilliseconds(100);
        Assert.Equal(ManualPlaybackTriggerOutcome.Throttled, trigger.Fire("b"));
        Assert.Equal(1, playback.PlayCount);
    }

    [Fact]
    public void FireAgainAfterIntervalPlays()
    {
        var playback = new FakePlayback();
        var now = DateTimeOffset.UnixEpoch;
        var trigger = Create(playback, _ => true, () => now, TimeSpan.FromMilliseconds(750));

        Assert.Equal(ManualPlaybackTriggerOutcome.Played, trigger.Fire("a"));
        now = now.AddMilliseconds(800);
        Assert.Equal(ManualPlaybackTriggerOutcome.Played, trigger.Fire("b"));
        Assert.Equal(2, playback.PlayCount);
    }

    [Fact]
    public void PlaybackFailureReturnsFailedOutcome()
    {
        var playback = new FakePlayback { ThrowOnPlay = true };
        var codes = new List<string>();
        var trigger = Create(playback, _ => true, () => DateTimeOffset.UnixEpoch, codes: codes);

        var outcome = trigger.Fire("test");

        Assert.Equal(ManualPlaybackTriggerOutcome.Failed, outcome);
        Assert.Contains("manual-trigger-playback-failed", codes);
    }

    [Fact]
    public void ThrottleWindowMeasuredFromLastPlayNotLastAttempt()
    {
        // A throttled attempt must not extend the throttle window.
        var playback = new FakePlayback();
        var now = DateTimeOffset.UnixEpoch;
        var trigger = Create(playback, _ => true, () => now, TimeSpan.FromMilliseconds(750));

        Assert.Equal(ManualPlaybackTriggerOutcome.Played, trigger.Fire("a"));    // t=0 plays
        now = now.AddMilliseconds(500);
        Assert.Equal(ManualPlaybackTriggerOutcome.Throttled, trigger.Fire("b")); // t=500 throttled
        now = now.AddMilliseconds(300);                                          // t=800: 800ms since last *play*
        Assert.Equal(ManualPlaybackTriggerOutcome.Played, trigger.Fire("c"));    // plays
        Assert.Equal(2, playback.PlayCount);
    }

    [Fact]
    public void TargetAppCanBeOverridden()
    {
        var playback = new FakePlayback();
        var trigger = new ManualPlaybackTrigger(
            playback,
            _ => true,
            (_, _, _, _) => { },
            () => DateTimeOffset.UnixEpoch,
            TimeSpan.FromMilliseconds(750),
            TargetNotificationApp.Codex);

        trigger.Fire("test");

        Assert.Equal(TargetNotificationApp.Codex, playback.LastDecision?.TargetApp);
    }
}
