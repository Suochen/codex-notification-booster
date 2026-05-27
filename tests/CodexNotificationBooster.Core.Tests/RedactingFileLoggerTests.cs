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
    public void ApplyRetentionDeletesOlderLogFilesBeyondLimit()
    {
        var paths = new AppPaths(_rootPath);
        paths.EnsureDirectories();

        foreach (var day in new[] { "20260501", "20260502", "20260503" })
        {
            File.WriteAllText(Path.Combine(paths.LogsDirectory, $"{day}.jsonl"), "{}");
        }

        var logger = new RedactingFileLogger(paths, retainedFileCount: 2);

        logger.ApplyRetention();

        var remainingFiles = Directory.GetFiles(paths.LogsDirectory, "*.jsonl")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "20260502.jsonl", "20260503.jsonl" }, remainingFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
