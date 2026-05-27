using System.Text.Json;
using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class RedactingFileLoggerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "cnb-log-tests-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public void LogRedactsRawNotificationFields()
    {
        var paths = new AppPaths(_rootPath);
        var logger = new RedactingFileLogger(paths);

        logger.Log(
            LogLevel.Warning,
            "sample",
            "A recoverable problem occurred.",
            new Dictionary<string, object?>
            {
                ["title"] = "secret title",
                ["body"] = "secret body",
                ["textLines"] = new[] { "line1", "line2" },
                ["rawXml"] = "<xml />",
                ["safeField"] = "kept"
            });

        var line = File.ReadAllLines(logger.CurrentLogFilePath).Single();
        using var document = JsonDocument.Parse(line);
        var metadata = document.RootElement.GetProperty("metadata");

        Assert.Equal("[REDACTED]", metadata.GetProperty("title").GetString());
        Assert.Equal("[REDACTED]", metadata.GetProperty("body").GetString());
        Assert.Equal("[REDACTED]", metadata.GetProperty("textLines").GetString());
        Assert.Equal("[REDACTED]", metadata.GetProperty("rawXml").GetString());
        Assert.Equal("kept", metadata.GetProperty("safeField").GetString());
    }

    [Fact]
    public void ApplyRetentionDeletesLogsOlderThanRetentionWindow()
    {
        var paths = new AppPaths(_rootPath);
        paths.EnsureDirectories();

        var oldPath = Path.Combine(paths.LogsDirectory, "20260430.jsonl");
        var freshPath = Path.Combine(paths.LogsDirectory, "20260507.jsonl");
        File.WriteAllText(oldPath, "{}");
        File.WriteAllText(freshPath, "{}");
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2026, 04, 30, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(freshPath, new DateTime(2026, 05, 07, 0, 0, 0, DateTimeKind.Utc));

        var logger = new RedactingFileLogger(
            paths,
            retentionDays: 7,
            utcNowProvider: () => new DateTime(2026, 05, 08, 12, 0, 0, DateTimeKind.Utc));

        logger.ApplyRetention();

        var remainingFiles = Directory.GetFiles(paths.LogsDirectory, "*.jsonl")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "20260507.jsonl" }, remainingFiles);
    }

    [Fact]
    public void ApplyRetentionDeletesOldestLogsUntilTotalSizeFitsLimit()
    {
        var paths = new AppPaths(_rootPath);
        paths.EnsureDirectories();

        WriteSizedLog(paths, "20260507.jsonl", 80, new DateTime(2026, 05, 07, 0, 0, 0, DateTimeKind.Utc));
        WriteSizedLog(paths, "20260508.jsonl", 80, new DateTime(2026, 05, 08, 0, 0, 0, DateTimeKind.Utc));
        WriteSizedLog(paths, "20260509.jsonl", 80, new DateTime(2026, 05, 09, 0, 0, 0, DateTimeKind.Utc));

        var logger = new RedactingFileLogger(
            paths,
            retentionDays: 7,
            maxTotalBytes: 170,
            maxFileBytes: 170,
            utcNowProvider: () => new DateTime(2026, 05, 09, 12, 0, 0, DateTimeKind.Utc));

        logger.ApplyRetention();

        var remainingFiles = Directory.GetFiles(paths.LogsDirectory, "*.jsonl")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "20260508.jsonl", "20260509.jsonl" }, remainingFiles);
    }

    [Fact]
    public void LogRollsToAnotherFileWhenCurrentFileWouldExceedSizeLimit()
    {
        var paths = new AppPaths(_rootPath);
        var logger = new RedactingFileLogger(
            paths,
            retentionDays: 7,
            maxTotalBytes: 1024,
            maxFileBytes: 220,
            utcNowProvider: () => new DateTime(2026, 05, 09, 12, 0, 0, DateTimeKind.Utc));

        var metadata = new Dictionary<string, object?>
        {
            ["safeField"] = new string('x', 100)
        };

        logger.Log(LogLevel.Info, "event-1", "first entry", metadata);
        logger.Log(LogLevel.Info, "event-2", "second entry", metadata);
        logger.Log(LogLevel.Info, "event-3", "third entry", metadata);

        var remainingFiles = Directory.GetFiles(paths.LogsDirectory, "*.jsonl")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "20260509-01.jsonl", "20260509-02.jsonl", "20260509.jsonl" }, remainingFiles);
    }

    private static void WriteSizedLog(AppPaths paths, string fileName, int size, DateTime lastWriteTimeUtc)
    {
        var fullPath = Path.Combine(paths.LogsDirectory, fileName);
        File.WriteAllText(fullPath, new string('x', size));
        File.SetLastWriteTimeUtc(fullPath, lastWriteTimeUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
