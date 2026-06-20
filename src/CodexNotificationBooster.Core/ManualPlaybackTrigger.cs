namespace CodexNotificationBooster.Core;

/// <summary>
/// Drives the helper playback pipeline (sound + audio ducking) from an external
/// signal that does not depend on Windows notification listening.
///
/// This is the foreground entry point: when the watched app (Codex, Claude Code,
/// ...) is focused it raises no OS toast, so the notification listener never
/// fires. An outside trigger — such as a Claude Code hook — drives this instead,
/// reusing the exact same <see cref="INotificationPlayback"/> the listener uses,
/// so audio ducking and per-app sound selection still apply.
///
/// Behaviour mirrors the listener path: it respects the per-app playback toggle,
/// throttles rapid repeats, and never lets a playback failure escape as an
/// unhandled exception.
/// </summary>
public sealed class ManualPlaybackTrigger
{
    public static readonly TimeSpan DefaultMinimumInterval = TimeSpan.FromMilliseconds(750);

    private readonly INotificationPlayback _playback;
    private readonly Func<NotificationMatchDecision, bool> _isPlaybackEnabled;
    private readonly Action<LogLevel, string, string, IReadOnlyDictionary<string, object?>> _log;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly TimeSpan _minimumInterval;
    private readonly TargetNotificationApp _targetApp;
    private readonly object _gate = new();

    private DateTimeOffset? _lastPlayedAt;
    private bool _isPlaying;

    public ManualPlaybackTrigger(
        INotificationPlayback playback,
        Func<NotificationMatchDecision, bool> isPlaybackEnabled,
        Action<LogLevel, string, string, IReadOnlyDictionary<string, object?>> log,
        Func<DateTimeOffset>? nowProvider = null,
        TimeSpan? minimumInterval = null,
        TargetNotificationApp targetApp = TargetNotificationApp.ClaudeDesktop)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _isPlaybackEnabled = isPlaybackEnabled ?? throw new ArgumentNullException(nameof(isPlaybackEnabled));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        _minimumInterval = minimumInterval ?? DefaultMinimumInterval;
        _targetApp = targetApp;

        if (_minimumInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval), "Minimum interval cannot be negative.");
        }
    }

    /// <summary>
    /// Attempts to drive helper playback for the configured target app. Safe to
    /// call from any thread; callers sharing UI-thread state should still marshal.
    /// </summary>
    public ManualPlaybackTriggerOutcome Fire(string source)
    {
        var label = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        var decision = new NotificationMatchDecision(
            Matched: true,
            Reason: "foreground manual trigger",
            MatchedRule: "manual-trigger",
            TargetApp: _targetApp);

        lock (_gate)
        {
            if (_isPlaying)
            {
                _log(LogLevel.Info, "manual-trigger-reentrant-skipped", "Manual playback trigger skipped; playback already running.", Meta(label));
                return ManualPlaybackTriggerOutcome.SkippedReentrant;
            }

            var now = _nowProvider();
            if (_lastPlayedAt is { } last && now - last < _minimumInterval)
            {
                _log(LogLevel.Info, "manual-trigger-throttled", "Manual playback trigger throttled.", Meta(label));
                return ManualPlaybackTriggerOutcome.Throttled;
            }

            if (!_isPlaybackEnabled(decision))
            {
                _log(LogLevel.Info, "manual-trigger-disabled-skipped", "Manual playback trigger skipped; playback disabled for target app.", Meta(label));
                return ManualPlaybackTriggerOutcome.SkippedDisabled;
            }

            _lastPlayedAt = now;
            _isPlaying = true;
        }

        try
        {
            _playback.Play(CreateSyntheticRecord(label), decision);
            _log(LogLevel.Info, "manual-trigger-played", "Manual playback trigger drove helper playback.", Meta(label));
            return ManualPlaybackTriggerOutcome.Played;
        }
        catch (Exception ex)
        {
            _log(LogLevel.Error, "manual-trigger-playback-failed", "Manual playback trigger failed during playback.", new Dictionary<string, object?>
            {
                ["source"] = label,
                ["targetApp"] = _targetApp.ToString(),
                ["exceptionType"] = ex.GetType().FullName,
                ["error"] = ex.Message
            });
            return ManualPlaybackTriggerOutcome.Failed;
        }
        finally
        {
            lock (_gate)
            {
                _isPlaying = false;
            }
        }
    }

    private static NotificationRecord CreateSyntheticRecord(string source)
    {
        // The playback pipeline does not read the record's content, so a minimal
        // synthetic record is enough to drive sound + ducking.
        return new NotificationRecord
        {
            AppDisplayName = "manual-trigger",
            ExecutionContext = source
        };
    }

    private static IReadOnlyDictionary<string, object?> Meta(string source)
    {
        return new Dictionary<string, object?> { ["source"] = source };
    }
}

public enum ManualPlaybackTriggerOutcome
{
    Played,
    Throttled,
    SkippedReentrant,
    SkippedDisabled,
    Failed
}
