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
        Assert.True(bytes.Length > 44);
        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Equal((byte)'W', bytes[8]);
        Assert.Equal((byte)'A', bytes[9]);
        Assert.Equal((byte)'V', bytes[10]);
        Assert.Equal((byte)'E', bytes[11]);

        var dataMarkerOffset = FindChunk(bytes, "data");
        Assert.True(dataMarkerOffset >= 0);

        var dataLength = BitConverter.ToInt32(bytes, dataMarkerOffset + 4);
        Assert.True(dataLength > 0);
        Assert.True(bytes.Length >= dataMarkerOffset + 8 + dataLength);

        var hasNonSilentSample = false;
        for (var offset = dataMarkerOffset + 8; offset < dataMarkerOffset + 8 + dataLength; offset += 2)
        {
            if (BitConverter.ToInt16(bytes, offset) != 0)
            {
                hasNonSilentSample = true;
                break;
            }
        }

        Assert.True(hasNonSilentSample);
    }

    private static int FindChunk(byte[] wavBytes, string chunkId)
    {
        for (var offset = 12; offset <= wavBytes.Length - 8;)
        {
            var currentChunkId = System.Text.Encoding.ASCII.GetString(wavBytes, offset, 4);
            var chunkLength = BitConverter.ToInt32(wavBytes, offset + 4);
            if (currentChunkId == chunkId)
            {
                return offset;
            }

            offset += 8 + chunkLength;
        }

        return -1;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
