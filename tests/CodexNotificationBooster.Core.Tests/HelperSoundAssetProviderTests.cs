using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class HelperSoundAssetProviderTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "cnb-sound-tests-" + Guid.NewGuid().ToString("n"));

    [Fact]
    public void EnsurePresentCreatesDeterministicWavFile()
    {
        var provider = new HelperSoundAssetProvider(new AppPaths(_rootPath));

        var path = provider.EnsurePresent();

        Assert.Equal(".wav", Path.GetExtension(path));
        Assert.True(File.Exists(path));

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 12);
        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Equal((byte)'W', bytes[8]);
        Assert.Equal((byte)'A', bytes[9]);
        Assert.Equal((byte)'V', bytes[10]);
        Assert.Equal((byte)'E', bytes[11]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
