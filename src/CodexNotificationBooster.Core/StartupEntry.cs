namespace CodexNotificationBooster.Core;

public sealed record StartupEntry(string Name, string ExecutablePath)
{
    public string Command => QuoteExecutablePath(ExecutablePath);

    public static string QuoteExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path must be provided.", nameof(executablePath));
        }

        return $"\"{executablePath}\"";
    }
}
