namespace CodexNotificationBooster.Core;

public sealed class AppPaths
{
    public AppPaths(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
        }

        RootDirectory = rootDirectory;
        AssetsDirectory = Path.Combine(rootDirectory, "assets");
        LogsDirectory = Path.Combine(rootDirectory, "logs");
        StateFilePath = Path.Combine(rootDirectory, "state.json");
        HelperSoundPath = Path.Combine(AssetsDirectory, "helper-notification.wav");
        CodexSoundPath = Path.Combine(AssetsDirectory, "codex-notification.wav");
        ClaudeDesktopSoundPath = HelperSoundPath;
    }

    public string RootDirectory { get; }

    public string AssetsDirectory { get; }

    public string LogsDirectory { get; }

    public string StateFilePath { get; }

    public string HelperSoundPath { get; }

    public string CodexSoundPath { get; }

    public string ClaudeDesktopSoundPath { get; }

    public static AppPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(localAppData, "CodexNotificationBooster"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
