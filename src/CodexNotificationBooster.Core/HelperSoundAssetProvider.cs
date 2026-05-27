namespace CodexNotificationBooster.Core;

public sealed class HelperSoundAssetProvider
{
    // A tiny valid PCM WAV payload that gives the helper a deterministic first-run sound.
    private static readonly byte[] WavBytes =
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
        0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
        0x40, 0x1F, 0x00, 0x00, 0x80, 0x3E, 0x00, 0x00,
        0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
        0x00, 0x00, 0x00, 0x00
    ];

    private readonly AppPaths _paths;

    public HelperSoundAssetProvider(AppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public string EnsurePresent()
    {
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.HelperSoundPath))
        {
            File.WriteAllBytes(_paths.HelperSoundPath, WavBytes);
        }

        return _paths.HelperSoundPath;
    }
}
