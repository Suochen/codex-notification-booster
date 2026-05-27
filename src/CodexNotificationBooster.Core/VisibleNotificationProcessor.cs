namespace CodexNotificationBooster.Core;

public sealed class VisibleNotificationProcessor
{
    private readonly CodexNotificationMatcher _matcher;
    private HashSet<string> _previousVisible = new(StringComparer.Ordinal);
    private bool _hasStartupBaseline;

    public VisibleNotificationProcessor(CodexNotificationMatcher? matcher = null)
    {
        _matcher = matcher ?? new CodexNotificationMatcher();
    }

    public NotificationPollResult Process(
        IEnumerable<NotificationRecord> visibleNotifications,
        bool isEnabled,
        Action<NotificationRecord> playbackInvoker)
    {
        ArgumentNullException.ThrowIfNull(visibleNotifications);
        ArgumentNullException.ThrowIfNull(playbackInvoker);

        var preparedRecords = PrepareVisibleRecords(visibleNotifications);
        var currentVisible = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<NotificationProcessingEvent>();
        var matched = 0;
        var ignored = 0;
        var playbackRequests = 0;
        var duplicatesSkipped = 0;
        var playbackFailures = 0;

        foreach (var entry in preparedRecords)
        {
            if (!currentVisible.Add(entry.VisibleInstanceKey))
            {
                duplicatesSkipped += 1;
                events.Add(new NotificationProcessingEvent(
                    Code: "duplicate-notification-skipped",
                    Message: "Skipped repeated visible notification with an already visible dedup key.",
                    Level: LogLevel.Info,
                    Record: entry.Record,
                    PlaybackResult: null));
                continue;
            }

            if (!_hasStartupBaseline)
            {
                events.Add(new NotificationProcessingEvent(
                    Code: "startup-baseline-notification-skipped",
                    Message: "Recorded currently visible notification during startup baseline without helper-owned playback.",
                    Level: LogLevel.Info,
                    Record: entry.Record,
                    PlaybackResult: null));
                continue;
            }

            if (_previousVisible.Contains(entry.VisibleInstanceKey))
            {
                duplicatesSkipped += 1;
                events.Add(new NotificationProcessingEvent(
                    Code: "duplicate-notification-skipped",
                    Message: "Skipped repeated visible notification with an already visible dedup key.",
                    Level: LogLevel.Info,
                    Record: entry.Record,
                    PlaybackResult: null));
                continue;
            }

            if (HelperOwnedNotificationFilter.IsHelperOwned(entry.Record))
            {
                ignored += 1;
                var helperDecision = new NotificationMatchDecision(
                    Matched: false,
                    Reason: "notification is owned by this helper",
                    MatchedRule: "helper-owned-notification");
                var helperResult = NotificationPlaybackResult.Ignored(helperDecision) with
                {
                    DiagnosticCode = "ignored-helper-owned-notification",
                    DiagnosticMessage = "Skipped helper-owned notification to avoid self-triggered playback."
                };

                events.Add(new NotificationProcessingEvent(
                    Code: helperResult.DiagnosticCode,
                    Message: helperResult.DiagnosticMessage,
                    Level: LogLevel.Info,
                    Record: entry.Record,
                    PlaybackResult: helperResult));
                continue;
            }

            var matchDecision = _matcher.Match(entry.Record);
            if (matchDecision.Matched)
            {
                matched += 1;
            }

            NotificationPlaybackResult result;
            if (!matchDecision.Matched)
            {
                ignored += 1;
                result = NotificationPlaybackResult.Ignored(matchDecision);
            }
            else if (!isEnabled)
            {
                ignored += 1;
                result = new NotificationPlaybackResult(
                    Status: "ignored",
                    PlaybackRequested: false,
                    DiagnosticCode: "playback-paused",
                    DiagnosticMessage: "Matched Codex notification while helper playback was paused.",
                    MatchDecision: matchDecision);
            }
            else
            {
                try
                {
                    playbackInvoker(entry.Record);
                    playbackRequests += 1;
                    result = NotificationPlaybackResult.Played(matchDecision);
                }
                catch (Exception ex)
                {
                    playbackRequests += 1;
                    playbackFailures += 1;
                    result = NotificationPlaybackResult.Failed(matchDecision, "playback-failure", ex.Message);
                }
            }

            events.Add(new NotificationProcessingEvent(
                Code: result.DiagnosticCode,
                Message: result.DiagnosticMessage,
                Level: result.Status == "failed" ? LogLevel.Error : LogLevel.Info,
                Record: entry.Record,
                PlaybackResult: result));
        }

        _previousVisible = currentVisible;
        _hasStartupBaseline = true;

        return new NotificationPollResult(
            Events: events,
            Matched: matched,
            Ignored: ignored,
            PlaybackRequests: playbackRequests,
            DuplicatesSkipped: duplicatesSkipped,
            PlaybackFailures: playbackFailures);
    }

    private static IReadOnlyList<VisibleRecordEntry> PrepareVisibleRecords(IEnumerable<NotificationRecord> records)
    {
        var prepared = records
            .Select(record =>
            {
                var dedupKey = string.IsNullOrWhiteSpace(record.DedupKey)
                    ? NotificationIdentityHasher.CreateDedupKey(record)
                    : record.DedupKey!;
                var preparedRecord = record with { DedupKey = dedupKey };

                return new VisibleRecordEntry(
                    preparedRecord,
                    dedupKey,
                    NotificationIdentityHasher.CreateVisibleStableKey(preparedRecord),
                    string.Empty);
            })
            .ToArray();

        foreach (var group in prepared.GroupBy(entry => entry.StableVisibleKey, StringComparer.Ordinal))
        {
            var ordinal = 0;
            foreach (var entry in group.OrderBy(item => item.DedupKey, StringComparer.Ordinal).ToArray())
            {
                ordinal += 1;
                entry.VisibleInstanceKey = $"{entry.StableVisibleKey}|{ordinal}";
            }
        }

        return prepared;
    }

    private sealed class VisibleRecordEntry
    {
        public VisibleRecordEntry(NotificationRecord record, string dedupKey, string stableVisibleKey, string visibleInstanceKey)
        {
            Record = record;
            DedupKey = dedupKey;
            StableVisibleKey = stableVisibleKey;
            VisibleInstanceKey = visibleInstanceKey;
        }

        public NotificationRecord Record { get; }

        public string DedupKey { get; }

        public string StableVisibleKey { get; }

        public string VisibleInstanceKey { get; set; }
    }
}
