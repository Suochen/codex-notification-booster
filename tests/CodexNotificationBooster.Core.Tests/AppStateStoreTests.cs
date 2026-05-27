using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class AppStateStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "cnb-state-tests-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public void LoadReturnsDefaultsWhenStateFileDoesNotExist()
    {
        var store = new AppStateStore(new AppPaths(_rootPath));

        var state = store.Load();

        Assert.True(state.IsEnabled);
        Assert.True(state.IsAudioDuckingEnabled);
    }

    [Fact]
    public void SaveAndLoadRoundTripsPersistedState()
    {
        var paths = new AppPaths(_rootPath);
        var store = new AppStateStore(paths);
        var expected = new AppState
        {
            IsEnabled = false,
            IsAudioDuckingEnabled = false,
            AudioDucking = new AudioDuckingState(
                new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
                [new AudioDuckingSnapshot("session-1", 0.42f, "music", "Music")])
        };

        store.Save(expected);

        var actual = store.Load();

        Assert.False(actual.IsEnabled);
        Assert.False(actual.IsAudioDuckingEnabled);
        Assert.NotNull(actual.AudioDucking);
        var snapshot = Assert.Single(actual.AudioDucking!.Snapshots);
        Assert.Equal("session-1", snapshot.SessionId);
        Assert.Equal(0.42f, snapshot.OriginalVolume);
        Assert.True(File.Exists(paths.StateFilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
