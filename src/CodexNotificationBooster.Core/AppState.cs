namespace CodexNotificationBooster.Core;

public sealed record AppState
{
    public bool IsEnabled { get; init; } = true;

    public bool IsAudioDuckingEnabled { get; init; } = true;
}
