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
        return EnsurePresent(TargetNotificationApp.ClaudeDesktop);
    }

    public string EnsurePresent(TargetNotificationApp targetApp)
    {
        _paths.EnsureDirectories();

        var soundPath = GetSoundPath(targetApp);
        if (!File.Exists(soundPath))
        {
            File.WriteAllBytes(soundPath, CreateWavBytes(targetApp));
        }

        return soundPath;
    }

    public void EnsureAllPresent()
    {
        EnsurePresent(TargetNotificationApp.Codex);
        EnsurePresent(TargetNotificationApp.ClaudeDesktop);
    }

    private string GetSoundPath(TargetNotificationApp targetApp)
    {
        return targetApp switch
        {
            TargetNotificationApp.Codex => _paths.CodexSoundPath,
            TargetNotificationApp.ClaudeDesktop => _paths.ClaudeDesktopSoundPath,
            _ => _paths.ClaudeDesktopSoundPath
        };
    }

    private static byte[] CreateWavBytes(TargetNotificationApp targetApp)
    {
        return targetApp switch
        {
            TargetNotificationApp.Codex => CreateDistinctPingWavBytes(),
            _ => CreateOriginalThreeToneWavBytes()
        };
    }

    private static byte[] CreateOriginalThreeToneWavBytes()
    {
        return CreateWavBytes(
        [
            new WavTone(880d, 0.12d, 1d, 0d, false),
            new WavTone(0d, 0.05d, 0d, 0d, false),
            new WavTone(1174.66d, 0.12d, 1d, 0d, false),
            new WavTone(0d, 0.05d, 0d, 0d, false),
            new WavTone(1567.98d, 0.18d, 1d, 0d, false)
        ]);
    }

    private static byte[] CreateDistinctPingWavBytes()
    {
        return CreateWavBytes(
        [
            new WavTone(740d, 0.075d, 0.86d, 0.24d, true),
            new WavTone(0d, 0.045d, 0d, 0d, false),
            new WavTone(554.37d, 0.105d, 0.78d, 0.18d, true),
            new WavTone(0d, 0.04d, 0d, 0d, false),
            new WavTone(1108.73d, 0.20d, 0.70d, 0.22d, true)
        ]);
    }

    private static byte[] CreateWavBytes(IReadOnlyList<WavTone> tones)
    {
        const int sampleRate = 16_000;
        const short bitsPerSample = 16;
        const short channels = 1;
        const short bytesPerSample = bitsPerSample / 8;
        const short blockAlign = channels * bytesPerSample;
        const int byteRate = sampleRate * blockAlign;
        const short amplitude = 9_000;

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
                    var envelope = tone.UseEnvelope ? Envelope(index, sampleCount) : 1d;
                    var fundamental = Math.Sin(2d * Math.PI * tone.Frequency * time);
                    var shimmer = tone.HarmonicGain * Math.Sin(2d * Math.PI * tone.Frequency * 2d * time);
                    value = (short)((fundamental + shimmer) / (1d + tone.HarmonicGain) * amplitude * tone.Gain * envelope);
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

    private static double Envelope(int index, int sampleCount)
    {
        if (sampleCount <= 1)
        {
            return 1d;
        }

        var attack = Math.Max(1, (int)(sampleCount * 0.10d));
        var release = Math.Max(1, (int)(sampleCount * 0.24d));
        if (index < attack)
        {
            return index / (double)attack;
        }

        if (index > sampleCount - release)
        {
            return Math.Max(0d, (sampleCount - index) / (double)release);
        }

        return 1d;
    }

    private sealed record WavTone(double Frequency, double DurationSeconds, double Gain, double HarmonicGain, bool UseEnvelope);
}
