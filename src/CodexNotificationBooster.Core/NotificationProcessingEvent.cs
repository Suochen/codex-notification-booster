namespace CodexNotificationBooster.Core;

public sealed record NotificationProcessingEvent(
    string Code,
    string Message,
    LogLevel Level,
    NotificationRecord Record,
    NotificationPlaybackResult? PlaybackResult);

public sealed record NotificationPollResult(
    IReadOnlyList<NotificationProcessingEvent> Events,
    int Matched,
    int Ignored,
    int PlaybackRequests,
    int DuplicatesSkipped,
    int PlaybackFailures);
