namespace CodexNotificationBooster.Core;

public sealed record NotificationPlaybackResult(
    string Status,
    bool PlaybackRequested,
    string DiagnosticCode,
    string DiagnosticMessage,
    NotificationMatchDecision MatchDecision)
{
    public static NotificationPlaybackResult Ignored(NotificationMatchDecision decision)
    {
        return new NotificationPlaybackResult(
            Status: "ignored",
            PlaybackRequested: false,
            DiagnosticCode: "ignored-notification",
            DiagnosticMessage: decision.Reason,
            MatchDecision: decision);
    }

    public static NotificationPlaybackResult Played(NotificationMatchDecision decision)
    {
        return new NotificationPlaybackResult(
            Status: "played",
            PlaybackRequested: true,
            DiagnosticCode: "matched-playback-requested",
            DiagnosticMessage: "Matched Codex notification requested helper-owned playback.",
            MatchDecision: decision);
    }

    public static NotificationPlaybackResult Failed(NotificationMatchDecision decision, string diagnosticCode, string diagnosticMessage)
    {
        return new NotificationPlaybackResult(
            Status: "failed",
            PlaybackRequested: true,
            DiagnosticCode: diagnosticCode,
            DiagnosticMessage: diagnosticMessage,
            MatchDecision: decision);
    }
}
