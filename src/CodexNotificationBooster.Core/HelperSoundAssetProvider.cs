namespace CodexNotificationBooster.Core;

public sealed class HelperSoundAssetProvider
{
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
            File.WriteAllBytes(_paths.HelperSoundPath, CreateThreeToneWavBytes());
        }

        return _paths.HelperSoundPath;
    }

    private static byte[] CreateThreeToneWavBytes()
    {
        const int sampleRate = 16_000;
        const short bitsPerSample = 16;
        const short channels = 1;
        const short bytesPerSample = bitsPerSample / 8;
        const short blockAlign = channels * bytesPerSample;
        const int byteRate = sampleRate * blockAlign;
        const short amplitude = 9_000;

        var tones = new (double Frequency, double DurationSeconds)[]
        {
            (880d, 0.12d),
            (0d, 0.05d),
            (1174.66d, 0.12d),
            (0d, 0.05d),
            (1567.98d, 0.18d)
        };

        var samples = new List<short>();

        foreach (var tone in tones)
        {
            var sampleCount = (int)(sampleRate * tone.DurationSeconds);
            for (var index = 0; index < sampleCount; index++)
            {
                short value = 0;
                if (tone.Frequency > 0d)
                {
                    var time = index / (double)sampleRate;
                    value = (short)(Math.Sin(2d * Math.PI * tone.Frequency * time) * amplitude);
                }

                samples.Add(value);
            }
        }

        var dataSize = samples.Count * bytesPerSample;

        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
