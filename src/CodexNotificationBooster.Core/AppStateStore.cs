using System.Text.Json;

namespace CodexNotificationBooster.Core;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public AppStateStore(AppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public AppState Load()
    {
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.StateFilePath))
        {
            return new AppState();
        }

        using var stream = File.OpenRead(_paths.StateFilePath);
        return JsonSerializer.Deserialize<AppState>(stream, SerializerOptions) ?? new AppState();
    }

    public void Save(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _paths.EnsureDirectories();

        using var stream = File.Create(_paths.StateFilePath);
        JsonSerializer.Serialize(stream, state, SerializerOptions);
    }
}
