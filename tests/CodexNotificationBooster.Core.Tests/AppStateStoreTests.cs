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
            IsAudioDuckingEnabled = false
        };

        store.Save(expected);

        var actual = store.Load();

        Assert.False(actual.IsEnabled);
        Assert.False(actual.IsAudioDuckingEnabled);
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
