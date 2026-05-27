using System.Collections.ObjectModel;

namespace CodexNotificationBooster.Core;

public sealed record NotificationRecord
{
    public int? SchemaVersion { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public DateTimeOffset? CreationTime { get; init; }

    public uint? NotificationId { get; init; }

    public string? AppDisplayName { get; init; }

    public string? AppUserModelId { get; init; }

    public string? AppId { get; init; }

    public string? PackageFamilyName { get; init; }

    public string? PackageFullName { get; init; }

    public string? ExecutionContext { get; init; }

    public IReadOnlyList<string> TextLines { get; init; } = Array.Empty<string>();

    public string? Title { get; init; }

    public string? Body { get; init; }

    public string? RawXml { get; init; }

    public string? RawXmlSha256 { get; init; }

    public string? DedupKey { get; init; }

    public NotificationRecord WithSanitizedTextLines(IEnumerable<string?> textLines)
    {
        ArgumentNullException.ThrowIfNull(textLines);

        var sanitized = textLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!)
            .ToArray();

        return this with
        {
            TextLines = new ReadOnlyCollection<string>(sanitized),
            Title = sanitized.Length > 0 ? sanitized[0] : null,
            Body = sanitized.Length > 1 ? string.Join(Environment.NewLine, sanitized.Skip(1)) : null
        };
    }

    public IReadOnlyDictionary<string, object?> ToRedactedLogMetadata()
    {
        return new Dictionary<string, object?>
        {
            ["dedupKey"] = DedupKey,
            ["notificationId"] = NotificationId,
            ["creationTime"] = CreationTime?.ToString("O"),
            ["appDisplayName"] = AppDisplayName,
            ["appUserModelId"] = AppUserModelId,
            ["appId"] = AppId,
            ["packageFamilyName"] = PackageFamilyName,
            ["packageFullName"] = PackageFullName,
            ["executionContext"] = ExecutionContext,
            ["rawXmlSha256"] = RawXmlSha256
        };
    }
}
