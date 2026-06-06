namespace CodexNotificationBooster.Core;

public sealed record AppState
{
    public bool IsEnabled { get; init; } = true;

    public bool IsCodexEnabled { get; init; } = true;

    public bool IsClaudeDesktopEnabled { get; init; } = true;

    public bool IsAudioDuckingEnabled { get; init; } = true;

    public AudioDuckingState? AudioDucking { get; init; }
}
