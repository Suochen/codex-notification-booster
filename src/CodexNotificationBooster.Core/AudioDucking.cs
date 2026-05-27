namespace CodexNotificationBooster.Core;

public sealed record AudioSessionInfo(
    string SessionId,
    string? ProcessName,
    int? ProcessId,
    string? DisplayName,
    string? AppUserModelId);

public sealed record AudioDuckingSnapshot(
    string SessionId,
    float OriginalVolume,
    string? ProcessName,
    string? DisplayName);

public sealed record AudioDuckingState(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset RestoreDueAt,
    IReadOnlyList<AudioDuckingSnapshot> Snapshots);

public sealed record AudioDuckingEvent(
    LogLevel Level,
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?> Metadata,
    bool UserVisible,
    string? UserMessage);

public interface IAudioSessionVolumeController
{
    IReadOnlyList<AudioSessionInfo> EnumeratePlaybackSessions();

    bool TryGetVolume(AudioSessionInfo session, out float volume, out string? error);

    bool TrySetVolume(string sessionId, float volume, out string? error);
}

public sealed class AudioDuckingCoordinator
{
    public const float DuckVolume = 0.2f;
    public static readonly TimeSpan BaseDuckDuration = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan MaxDuckDuration = TimeSpan.FromMilliseconds(3000);

    private readonly IAudioSessionVolumeController _controller;
    private readonly Func<AppState> _stateProvider;
    private readonly Action<AppState> _statePersister;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly Action<AudioDuckingEvent> _eventSink;
    private readonly int _helperProcessId;

    public AudioDuckingCoordinator(
        IAudioSessionVolumeController controller,
        Func<AppState> stateProvider,
        Action<AppState> statePersister,
        int helperProcessId,
        Action<AudioDuckingEvent> eventSink,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _statePersister = statePersister ?? throw new ArgumentNullException(nameof(statePersister));
        _helperProcessId = helperProcessId;
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public void DuckForNotificationPlayback()
    {
        var state = _stateProvider();
        if (!state.IsAudioDuckingEnabled)
        {
            return;
        }

        var now = _nowProvider();
        var existing = state.AudioDucking;
        var windowStartedAt = existing?.WindowStartedAt ?? now;
        var restoreDueAt = Min(now.Add(BaseDuckDuration), windowStartedAt.Add(MaxDuckDuration));
        var snapshots = existing?.Snapshots.ToDictionary(item => item.SessionId, StringComparer.Ordinal) ??
                        new Dictionary<string, AudioDuckingSnapshot>(StringComparer.Ordinal);

        IReadOnlyList<AudioSessionInfo> sessions;
        try
        {
            sessions = _controller.EnumeratePlaybackSessions();
        }
        catch (Exception ex)
        {
            EmitFailure("audio-duck-enumerate-failed", "Audio ducking session enumeration failed.", ex.Message);
            PersistAudioState(state with
            {
                AudioDucking = snapshots.Count == 0
                    ? null
                    : new AudioDuckingState(windowStartedAt, restoreDueAt, snapshots.Values.ToArray())
            });
            return;
        }

        foreach (var session in sessions)
        {
            if (snapshots.ContainsKey(session.SessionId) || !IsEligible(session))
            {
                continue;
            }

            if (!_controller.TryGetVolume(session, out var originalVolume, out var readError))
            {
                Emit(LogLevel.Info, "audio-duck-session-skipped-unreadable", "Skipped unreadable playback session during audio ducking.", new Dictionary<string, object?>
                {
                    ["sessionId"] = session.SessionId,
                    ["processName"] = session.ProcessName,
                    ["displayName"] = session.DisplayName,
                    ["error"] = readError
                });
                continue;
            }

            if (!TryNormalizeVolume(originalVolume, out var normalizedOriginalVolume))
            {
                Emit(LogLevel.Info, "audio-duck-session-skipped-invalid-volume", "Skipped playback session with invalid readable volume during audio ducking.", new Dictionary<string, object?>
                {
                    ["sessionId"] = session.SessionId,
                    ["processName"] = session.ProcessName,
                    ["displayName"] = session.DisplayName
                });
                continue;
            }

            if (!_controller.TrySetVolume(session.SessionId, DuckVolume, out var setError))
            {
                EmitFailure("audio-duck-session-set-failed", "Failed to duck eligible playback session.", setError, new Dictionary<string, object?>
                {
                    ["sessionId"] = session.SessionId,
                    ["processName"] = session.ProcessName,
                    ["displayName"] = session.DisplayName
                });
                continue;
            }

            snapshots[session.SessionId] = new AudioDuckingSnapshot(
                session.SessionId,
                normalizedOriginalVolume,
                session.ProcessName,
                session.DisplayName);
        }

        PersistAudioState(state with
        {
            AudioDucking = snapshots.Count == 0
                ? null
                : new AudioDuckingState(windowStartedAt, restoreDueAt, snapshots.Values.ToArray())
        });
    }

    public void RestoreIfDue()
    {
        var state = _stateProvider();
        if (state.AudioDucking is null || _nowProvider() < state.AudioDucking.RestoreDueAt)
        {
            return;
        }

        RestoreOutstanding("audio-duck-restore-due", state.AudioDucking);
    }

    public void RestoreNow()
    {
        var state = _stateProvider();
        if (state.AudioDucking is null)
        {
            return;
        }

        RestoreOutstanding("audio-duck-restore-manual", state.AudioDucking);
    }

    public void RecoverPriorState()
    {
        var state = _stateProvider();
        if (state.AudioDucking is null)
        {
            return;
        }

        RestoreOutstanding("audio-duck-restore-startup", state.AudioDucking);
    }

    private void RestoreOutstanding(string eventCode, AudioDuckingState audioState)
    {
        var failed = 0;
        var failedSnapshots = new List<AudioDuckingSnapshot>();
        foreach (var snapshot in audioState.Snapshots)
        {
            if (!_controller.TrySetVolume(snapshot.SessionId, ClampVolume(snapshot.OriginalVolume), out var error))
            {
                failed += 1;
                failedSnapshots.Add(snapshot);
                EmitFailure("audio-duck-restore-session-failed", "Failed to restore snapshot playback session volume.", error, new Dictionary<string, object?>
                {
                    ["sessionId"] = snapshot.SessionId,
                    ["processName"] = snapshot.ProcessName,
                    ["displayName"] = snapshot.DisplayName
                });
            }
        }

        var nextState = failedSnapshots.Count == 0
            ? null
            : new AudioDuckingState(audioState.WindowStartedAt, _nowProvider().Add(MaxDuckDuration), failedSnapshots);
        PersistAudioState(_stateProvider() with { AudioDucking = nextState });
        Emit(LogLevel.Info, eventCode, "Completed audio ducking restore pass.", new Dictionary<string, object?>
        {
            ["snapshotCount"] = audioState.Snapshots.Count,
            ["failedCount"] = failed
        });
    }

    private bool IsEligible(AudioSessionInfo session)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            return false;
        }

        if (session.ProcessId == _helperProcessId)
        {
            return false;
        }

        return !LooksLikeCodexSession(session);
    }

    private static bool LooksLikeCodexSession(AudioSessionInfo session)
    {
        return ContainsCodexIdentity(session.SessionId) ||
               ContainsCodexIdentity(session.ProcessName) ||
               ContainsCodexIdentity(session.DisplayName) ||
               ContainsCodexIdentity(session.AppUserModelId);
    }

    private static bool ContainsCodexIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Codex", StringComparison.OrdinalIgnoreCase);
    }

    private void PersistAudioState(AppState state)
    {
        _statePersister(state);
    }

    private void EmitFailure(string code, string message, string? error, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var values = metadata is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(metadata);

        values["error"] = error;
        Emit(LogLevel.Error, code, message, values, userVisible: true, userMessage: "音频闪避或恢复失败，托盘助手仍在运行。");
    }

    private void Emit(string code, string message, IReadOnlyDictionary<string, object?> metadata)
    {
        Emit(LogLevel.Info, code, message, metadata);
    }

    private void Emit(LogLevel level, string code, string message, IReadOnlyDictionary<string, object?> metadata, bool userVisible = false, string? userMessage = null)
    {
        _eventSink(new AudioDuckingEvent(level, code, message, metadata, userVisible, userMessage));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }

    private static float ClampVolume(float volume)
    {
        return Math.Clamp(volume, 0f, 1f);
    }

    private static bool TryNormalizeVolume(float volume, out float normalizedVolume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume))
        {
            normalizedVolume = 0f;
            return false;
        }

        normalizedVolume = ClampVolume(volume);
        return true;
    }
}

public sealed class AudioDuckingNotificationPlayback : INotificationPlayback
{
    private readonly INotificationPlayback _inner;
    private readonly AudioDuckingCoordinator _coordinator;

    public AudioDuckingNotificationPlayback(INotificationPlayback inner, AudioDuckingCoordinator coordinator)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public void Play(NotificationRecord record)
    {
        _coordinator.DuckForNotificationPlayback();
        _inner.Play(record);
    }
}
