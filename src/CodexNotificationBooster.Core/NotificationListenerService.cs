namespace CodexNotificationBooster.Core;

public interface INotificationSource
{
    ValueTask<IReadOnlyList<NotificationRecord>> GetVisibleNotificationsAsync(CancellationToken cancellationToken);
}

public interface INotificationPlayback
{
    void Play(NotificationRecord record);
}

public sealed class NotificationListenerService
{
    private readonly INotificationSource _source;
    private readonly INotificationPlayback _playback;
    private readonly VisibleNotificationProcessor _processor;
    private readonly RedactingFileLogger _logger;
    private readonly Func<bool> _isEnabledProvider;
    private readonly Action<string, string> _statusNotifier;

    public NotificationListenerService(
        INotificationSource source,
        INotificationPlayback playback,
        VisibleNotificationProcessor processor,
        RedactingFileLogger logger,
        Func<bool> isEnabledProvider,
        Action<string, string> statusNotifier)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isEnabledProvider = isEnabledProvider ?? throw new ArgumentNullException(nameof(isEnabledProvider));
        _statusNotifier = statusNotifier ?? throw new ArgumentNullException(nameof(statusNotifier));
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var records = await _source.GetVisibleNotificationsAsync(cancellationToken).ConfigureAwait(false);
            var result = _processor.Process(records, _isEnabledProvider(), record => _playback.Play(record));

            foreach (var item in result.Events)
            {
                LogProcessingEvent(item);
            }

            if (result.PlaybackFailures > 0)
            {
                _statusNotifier("Codex Notification Booster", "提示音播放失败，托盘助手仍在运行。");
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
                ["error"] = ex.Message
            });

            _statusNotifier("Codex Notification Booster", "通知监听暂时不可用，托盘助手仍在运行。");
        }
    }

    private void LogProcessingEvent(NotificationProcessingEvent item)
    {
        var metadata = new Dictionary<string, object?>(item.Record.ToRedactedLogMetadata())
        {
            ["status"] = item.PlaybackResult?.Status,
            ["playbackRequested"] = item.PlaybackResult?.PlaybackRequested,
            ["matched"] = item.PlaybackResult?.MatchDecision.Matched,
            ["matchedRule"] = item.PlaybackResult?.MatchDecision.MatchedRule
        };

        _logger.Log(item.Level, item.Code, item.Message, metadata);
    }
}
