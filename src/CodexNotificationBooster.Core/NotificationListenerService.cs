namespace CodexNotificationBooster.Core;

public interface INotificationSource
{
    ValueTask<IReadOnlyList<NotificationRecord>> GetVisibleNotificationsAsync(CancellationToken cancellationToken);
}

public interface INotificationPlayback
{
    void Play(NotificationRecord record, NotificationMatchDecision matchDecision);
}

public sealed class NotificationListenerService
{
    private readonly INotificationSource _source;
    private readonly INotificationPlayback _playback;
    private readonly VisibleNotificationProcessor _processor;
    private readonly RedactingFileLogger _logger;
    private readonly Func<NotificationMatchDecision, bool> _isEnabledProvider;
    private readonly Action<string, string> _statusNotifier;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly TimeSpan _statusNotificationCooldown;
    private readonly Dictionary<string, DateTimeOffset> _lastStatusNotificationByKey = new(StringComparer.Ordinal);

    public NotificationListenerService(
        INotificationSource source,
        INotificationPlayback playback,
        VisibleNotificationProcessor processor,
        RedactingFileLogger logger,
        Func<bool> isEnabledProvider,
        Action<string, string> statusNotifier,
        Func<DateTimeOffset>? nowProvider = null,
        TimeSpan? statusNotificationCooldown = null)
        : this(
            source,
            playback,
            processor,
            logger,
            _ => isEnabledProvider(),
            statusNotifier,
            nowProvider,
            statusNotificationCooldown)
    {
        ArgumentNullException.ThrowIfNull(isEnabledProvider);
    }

    public NotificationListenerService(
        INotificationSource source,
        INotificationPlayback playback,
        VisibleNotificationProcessor processor,
        RedactingFileLogger logger,
        Func<NotificationMatchDecision, bool> isEnabledProvider,
        Action<string, string> statusNotifier,
        Func<DateTimeOffset>? nowProvider = null,
        TimeSpan? statusNotificationCooldown = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isEnabledProvider = isEnabledProvider ?? throw new ArgumentNullException(nameof(isEnabledProvider));
        _statusNotifier = statusNotifier ?? throw new ArgumentNullException(nameof(statusNotifier));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        _statusNotificationCooldown = statusNotificationCooldown ?? TimeSpan.FromMinutes(5);
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var records = await _source.GetVisibleNotificationsAsync(cancellationToken).ConfigureAwait(false);
            var result = _processor.Process(records, _isEnabledProvider, (record, decision) => _playback.Play(record, decision));

            foreach (var item in result.Events)
            {
                LogProcessingEvent(item);
            }

            if (result.PlaybackFailures > 0)
            {
                NotifyStatusIfDue("playback-failure", "Codex Notification Booster", "提示音播放失败，托盘助手仍在运行。");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "listener-poll-failed", "Recoverable notification listener polling failure.", new Dictionary<string, object?>
            {
                ["exceptionType"] = ex.GetType().FullName,
                ["error"] = ex.Message,
                ["stackTrace"] = ex.ToString()
            });

            NotifyStatusIfDue("listener-poll-failed", "Codex Notification Booster", "通知监听暂时不可用，托盘助手仍在运行。");
        }
    }

    private void NotifyStatusIfDue(string key, string title, string message)
    {
        var now = _nowProvider();
        if (_lastStatusNotificationByKey.TryGetValue(key, out var last) &&
            now - last < _statusNotificationCooldown)
        {
            return;
        }

        _lastStatusNotificationByKey[key] = now;
        _statusNotifier(title, message);
    }

    private void LogProcessingEvent(NotificationProcessingEvent item)
    {
        var metadata = new Dictionary<string, object?>(item.Record.ToRedactedLogMetadata())
        {
            ["status"] = item.PlaybackResult?.Status,
            ["playbackRequested"] = item.PlaybackResult?.PlaybackRequested,
            ["matched"] = item.PlaybackResult?.MatchDecision.Matched,
            ["matchedRule"] = item.PlaybackResult?.MatchDecision.MatchedRule,
            ["targetApp"] = item.PlaybackResult?.MatchDecision.TargetApp.ToString()
        };

        _logger.Log(item.Level, item.Code, item.Message, metadata);
    }
}
