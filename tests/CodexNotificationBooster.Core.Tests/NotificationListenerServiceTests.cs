using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class NotificationListenerServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "cnb-listener-tests-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task PollFailureLogsRedactedErrorAndKeepsServiceAlive()
    {
        var paths = new AppPaths(_rootPath);
        var logger = new RedactingFileLogger(paths);
        var balloons = new List<(string Title, string Message)>();
        var service = new NotificationListenerService(
            new ThrowingSource(new InvalidOperationException("listener permission denied")),
            new RecordingPlayback(),
            new VisibleNotificationProcessor(),
            logger,
            () => true,
            (title, message) => balloons.Add((title, message)));

        await service.PollOnceAsync(CancellationToken.None);

        Assert.Single(balloons);
        var logText = File.ReadAllText(logger.CurrentLogFilePath);
        Assert.Contains("listener-poll-failed", logText, StringComparison.Ordinal);
        Assert.Contains("listener permission denied", logText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessingLogsDoNotContainRawNotificationTextOrXml()
    {
        var paths = new AppPaths(_rootPath);
        var logger = new RedactingFileLogger(paths);
        var source = new SequenceSource(
            [],
            [
                new NotificationRecord
                {
                    CreationTime = new DateTimeOffset(2026, 05, 28, 10, 0, 0, TimeSpan.Zero),
                    NotificationId = 1,
                    AppDisplayName = "Codex",
                    AppUserModelId = CodexNotificationMatcher.CodexAppUserModelId,
                    PackageFamilyName = CodexNotificationMatcher.CodexPackageFamilyName,
                    RawXml = "<toast>secret xml</toast>",
                    RawXmlSha256 = "hash-only"
                }.WithSanitizedTextLines(["secret title", "secret body"])
            ]);
        var service = new NotificationListenerService(
            source,
            new RecordingPlayback(),
            new VisibleNotificationProcessor(),
            logger,
            () => true,
            (_, _) => { });

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        var logText = File.ReadAllText(logger.CurrentLogFilePath);
        Assert.Contains("matched-playback-requested", logText, StringComparison.Ordinal);
        Assert.Contains("hash-only", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret title", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret body", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret xml", logText, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class ThrowingSource : INotificationSource
    {
        private readonly Exception _exception;

        public ThrowingSource(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask<IReadOnlyList<NotificationRecord>> GetVisibleNotificationsAsync(CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    private sealed class SequenceSource : INotificationSource
    {
        private readonly Queue<IReadOnlyList<NotificationRecord>> _polls;

        public SequenceSource(params IReadOnlyList<NotificationRecord>[] polls)
        {
            _polls = new Queue<IReadOnlyList<NotificationRecord>>(polls);
        }

        public ValueTask<IReadOnlyList<NotificationRecord>> GetVisibleNotificationsAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(_polls.Count == 0 ? Array.Empty<NotificationRecord>() : _polls.Dequeue());
        }
    }

    private sealed class RecordingPlayback : INotificationPlayback
    {
        public void Play(NotificationRecord record)
        {
        }
    }
}
