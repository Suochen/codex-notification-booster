using System.Text.Json;

namespace CodexNotificationBooster.Core;

public sealed class RedactingFileLogger
{
    private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "body",
        "textLines",
        "rawXml",
        "rawNotification"
    };

    private readonly AppPaths _paths;
    private readonly int _retainedFileCount;

    public RedactingFileLogger(AppPaths paths, int retainedFileCount = 7)
    {
        if (retainedFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedFileCount), "Retention must keep at least one file.");
        }

        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _retainedFileCount = retainedFileCount;
    }

    public string CurrentLogFilePath => Path.Combine(_paths.LogsDirectory, $"{DateTime.UtcNow:yyyyMMdd}.jsonl");

    public void Log(LogLevel level, string eventCode, string message, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(eventCode))
        {
            throw new ArgumentException("Event code must be provided.", nameof(eventCode));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must be provided.", nameof(message));
        }

        _paths.EnsureDirectories();

        var payload = new Dictionary<string, object?>
        {
            ["timestampUtc"] = DateTime.UtcNow.ToString("O"),
            ["level"] = level.ToString().ToLowerInvariant(),
            ["eventCode"] = eventCode,
            ["message"] = message
        };

        if (metadata is not null && metadata.Count > 0)
        {
            payload["metadata"] = RedactMetadata(metadata);
        }

        var json = JsonSerializer.Serialize(payload);
        File.AppendAllText(CurrentLogFilePath, json + Environment.NewLine);
    }

    public void ApplyRetention()
    {
        _paths.EnsureDirectories();

        var files = new DirectoryInfo(_paths.LogsDirectory)
            .GetFiles("*.jsonl")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToArray();

        foreach (var file in files.Skip(_retainedFileCount))
        {
            file.Delete();
        }
    }

    private static Dictionary<string, object?> RedactMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in metadata)
        {
            sanitized[pair.Key] = RedactedKeys.Contains(pair.Key)
                ? "[REDACTED]"
                : pair.Value;
        }

        return sanitized;
    }
}
