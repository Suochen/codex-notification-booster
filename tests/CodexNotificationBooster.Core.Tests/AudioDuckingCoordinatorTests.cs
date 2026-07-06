using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class AudioDuckingCoordinatorTests
{
    private readonly AudioSessionInfo _music = new("music-session", "spotify", 101, "Spotify", null);
    private readonly AudioSessionInfo _helper = new("helper-session", "CodexNotificationBooster", 900, "Helper", null);
    private readonly AudioSessionInfo _codex = new("codex-session", "Codex", 102, "Codex", "OpenAI.Codex_123");
    private readonly List<AudioDuckingEvent> _events = [];
    private AppState _state = new();
    private DateTimeOffset _now = new(2026, 5, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DuckSnapshotsOnlyReadableEligiblePlaybackSessions()
    {
        var unreadable = new AudioSessionInfo("unreadable-session", "browser", 103, "Browser", null);
        var controller = new FakeAudioController
        {
            Sessions = [_music, _helper, _codex, unreadable],
            Volumes =
            {
                [_music.SessionId] = 0.8f,
                [_helper.SessionId] = 0.7f,
                [_codex.SessionId] = 0.6f
            },
            ReadFailures = { [unreadable.SessionId] = "access denied" }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();

        Assert.Equal(AudioDuckingCoordinator.DuckVolume, controller.Volumes[_music.SessionId]);
        Assert.Equal(0.7f, controller.Volumes[_helper.SessionId]);
        Assert.Equal(0.6f, controller.Volumes[_codex.SessionId]);
        var snapshot = Assert.Single(_state.AudioDucking!.Snapshots);
        Assert.Equal(_music.SessionId, snapshot.SessionId);
        Assert.Equal(0.8f, snapshot.OriginalVolume);
        Assert.Contains(_events, item => item.Code == "audio-duck-session-skipped-unreadable");
    }

    [Fact]
    public void CodexSessionIdentifierIsExcludedEvenWhenProcessAndDisplayDoNotNameCodex()
    {
        var codexBySessionId = new AudioSessionInfo(
            "OpenAI.Codex_2p2nqsd0c76g0!App\\opaque-session",
            "ApplicationFrameHost",
            104,
            "Desktop App",
            null);
        var controller = new FakeAudioController
        {
            Sessions = [codexBySessionId],
            Volumes = { [codexBySessionId.SessionId] = 0.6f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();

        Assert.Equal(0.6f, controller.Volumes[codexBySessionId.SessionId]);
        Assert.Null(_state.AudioDucking);
    }

    [Fact]
    public void RestoreDueRestoresOriginalSnapshotVolume()
    {
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.8f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();
        _now = _now.AddMilliseconds(1000);
        coordinator.RestoreIfDue();

        Assert.Equal(0.8f, controller.Volumes[_music.SessionId]);
        Assert.Null(_state.AudioDucking);
    }

    [Fact]
    public void ManualRestoreRestoresOutstandingDuckedSessionsImmediately()
    {
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.55f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();
        _now = _now.AddMilliseconds(200);
        coordinator.RestoreNow();

        Assert.Equal(0.55f, controller.Volumes[_music.SessionId]);
        Assert.Null(_state.AudioDucking);
    }

    [Fact]
    public void BurstNotificationsShareWindowAndCapAtThreeSeconds()
    {
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.9f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();
        var startedAt = _state.AudioDucking!.WindowStartedAt;

        _now = startedAt.AddMilliseconds(2500);
        coordinator.DuckForNotificationPlayback();

        Assert.Equal(startedAt, _state.AudioDucking!.WindowStartedAt);
        Assert.Equal(startedAt.AddMilliseconds(3000), _state.AudioDucking.RestoreDueAt);
        Assert.Single(_state.AudioDucking.Snapshots);
    }

    [Fact]
    public void RestoreFailureKeepsSnapshotAndProducesUserVisibleEvent()
    {
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.4f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();
        controller.WriteFailures[_music.SessionId] = "session gone";
        coordinator.RestoreNow();

        Assert.NotNull(_state.AudioDucking);
        Assert.Single(_state.AudioDucking!.Snapshots);
        Assert.Contains(_events, item => item.Code == "audio-duck-restore-session-failed" && item.UserVisible);
    }

    [Fact]
    public void StartupRecoveryRestoresPersistedSnapshotsWithoutUnknownFallback()
    {
        _state = new AppState
        {
            AudioDucking = new AudioDuckingState(
                _now.AddSeconds(-30),
                _now.AddSeconds(-29),
                [new AudioDuckingSnapshot(_music.SessionId, 0.33f, _music.ProcessName, _music.DisplayName)])
        };
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.2f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.RecoverPriorState();

        Assert.Equal(0.33f, controller.Volumes[_music.SessionId]);
        Assert.Null(_state.AudioDucking);
    }

    [Fact]
    public void DisabledDuckingDoesNotTouchAudioSessions()
    {
        _state = new AppState { IsAudioDuckingEnabled = false };
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.8f }
        };
        var coordinator = CreateCoordinator(controller);

        coordinator.DuckForNotificationPlayback();

        Assert.Equal(0.8f, controller.Volumes[_music.SessionId]);
        Assert.Null(_state.AudioDucking);
    }

    [Fact]
    public void DuckingPlaybackDucksBeforeInnerPlayback()
    {
        var order = new List<string>();
        var controller = new FakeAudioController
        {
            Sessions = [_music],
            Volumes = { [_music.SessionId] = 0.8f },
            AfterSet = (_, _) => order.Add("duck")
        };
        var coordinator = CreateCoordinator(controller);
        var playback = new AudioDuckingNotificationPlayback(
            new RecordingPlayback(() => order.Add("play")),
            coordinator);

        playback.Play(new NotificationRecord(), new NotificationMatchDecision(
            Matched: true,
            Reason: "test",
            MatchedRule: "test",
            TargetApp: TargetNotificationApp.Codex));

        Assert.Equal(["duck", "play"], order);
        Assert.Equal(AudioDuckingCoordinator.DuckVolume, controller.Volumes[_music.SessionId]);
    }

    private AudioDuckingCoordinator CreateCoordinator(FakeAudioController controller)
    {
        return new AudioDuckingCoordinator(
            controller,
            () => _state,
            state => _state = state,
            helperProcessId: 900,
            _events.Add,
            () => _now);
    }

    private sealed class FakeAudioController : IAudioSessionVolumeController
    {
        public IReadOnlyList<AudioSessionInfo> Sessions { get; init; } = [];

        public Dictionary<string, float> Volumes { get; init; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> ReadFailures { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> WriteFailures { get; } = new(StringComparer.Ordinal);

        public Action<string, float>? AfterSet { get; init; }

        public IReadOnlyList<AudioSessionInfo> EnumeratePlaybackSessions()
        {
            return Sessions;
        }

        public bool TryGetVolume(AudioSessionInfo session, out float volume, out string? error)
        {
            if (ReadFailures.TryGetValue(session.SessionId, out error))
            {
                volume = 0f;
                return false;
            }

            if (Volumes.TryGetValue(session.SessionId, out volume))
            {
                error = null;
                return true;
            }

            error = "missing";
            return false;
        }

        public bool TrySetVolume(string sessionId, float volume, out string? error)
        {
            if (WriteFailures.TryGetValue(sessionId, out error))
            {
                return false;
            }

            Volumes[sessionId] = volume;
            AfterSet?.Invoke(sessionId, volume);
            error = null;
            return true;
        }
    }

    private sealed class RecordingPlayback : INotificationPlayback
    {
        private readonly Action _onPlay;

        public RecordingPlayback(Action onPlay)
        {
            _onPlay = onPlay;
        }

        public void Play(NotificationRecord record, NotificationMatchDecision matchDecision)
        {
            _onPlay();
        }
    }
}
