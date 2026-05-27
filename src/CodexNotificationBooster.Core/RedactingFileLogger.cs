using System.Text.Json;

namespace CodexNotificationBooster.Core;

public sealed class RedactingFileLogger
{
    public const int DefaultRetentionDays = 7;
    public const long DefaultMaxTotalBytes = 5 * 1024 * 1024;
    public const long DefaultMaxFileBytes = 1024 * 1024;

    private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "body",
        "textLines",
        "rawXml",
        "rawNotification"
    };

    private readonly AppPaths _paths;
    private readonly int _retentionDays;
    private readonly long _maxTotalBytes;
    private readonly long _maxFileBytes;
    private readonly Func<DateTime> _utcNowProvider;

    public RedactingFileLogger(
        AppPaths paths,
        int retentionDays = DefaultRetentionDays,
        long maxTotalBytes = DefaultMaxTotalBytes,
        long maxFileBytes = DefaultMaxFileBytes,
        Func<DateTime>? utcNowProvider = null)
    {
        if (retentionDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention must keep at least one day.");
        }

        if (maxTotalBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTotalBytes), "Total retained log size must be positive.");
        }

        if (maxFileBytes < 1 || maxFileBytes > maxTotalBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes), "Single log file size must be positive and no larger than total retention.");
        }

        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _retentionDays = retentionDays;
        _maxTotalBytes = maxTotalBytes;
        _maxFileBytes = maxFileBytes;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    public string CurrentLogFilePath => ResolveWritableLogFilePath(0);

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

        var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
        var lineBytes = System.Text.Encoding.UTF8.GetByteCount(line);

        ApplyRetention();

        var logFilePath = ResolveWritableLogFilePath(lineBytes);
        File.AppendAllText(logFilePath, line);

        ApplyRetention();
    }

    public void ApplyRetention()
    {
        _paths.EnsureDirectories();

        var cutoffUtc = _utcNowProvider().Date.AddDays(-_retentionDays);
        var files = new DirectoryInfo(_paths.LogsDirectory)
            .GetFiles("*.jsonl")
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();

        foreach (var file in files.Where(file => file.LastWriteTimeUtc < cutoffUtc).ToArray())
        {
            file.Delete();
        }

        files = new DirectoryInfo(_paths.LogsDirectory)
            .GetFiles("*.jsonl")
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();

        long totalBytes = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (totalBytes <= _maxTotalBytes)
            {
                break;
            }

            totalBytes -= file.Length;
            file.Delete();
        }
    }

    private string ResolveWritableLogFilePath(int incomingLineBytes)
    {
        _paths.EnsureDirectories();

        var prefix = _utcNowProvider().ToString("yyyyMMdd");
        var candidates = Directory.GetFiles(_paths.LogsDirectory, $"{prefix}*.jsonl")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return Path.Combine(_paths.LogsDirectory, $"{prefix}.jsonl");
        }

        var current = candidates[^1];
        var currentLength = new FileInfo(current).Length;
        if (currentLength + incomingLineBytes <= _maxFileBytes)
        {
            return current;
        }

        var nextIndex = candidates.Length;
        return Path.Combine(_paths.LogsDirectory, $"{prefix}-{nextIndex:D2}.jsonl");
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
