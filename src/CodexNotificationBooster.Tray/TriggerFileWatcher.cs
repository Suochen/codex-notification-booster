using System.IO;

namespace CodexNotificationBooster.Tray;

/// <summary>
/// Watches a single trigger file. Any create/change/rename of that file invokes
/// the callback. This is the foreground entry point: an external process (for
/// example a Claude Code hook) writes or touches the file, and the helper plays
/// sound + ducks without depending on an OS notification.
///
/// FileSystemWatcher can raise several events for one logical write; de-bouncing
/// is the trigger's job (see <c>ManualPlaybackTrigger</c> throttling), not this
/// watcher's.
/// </summary>
internal sealed class TriggerFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action<string> _onTriggered;

    public TriggerFileWatcher(string triggerFilePath, Action<string> onTriggered)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerFilePath);
        _onTriggered = onTriggered ?? throw new ArgumentNullException(nameof(onTriggered));

        var directory = Path.GetDirectoryName(triggerFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Trigger file path must include a directory.", nameof(triggerFilePath));
        }

        Directory.CreateDirectory(directory);

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(triggerFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => _onTriggered("file:" + e.ChangeType);

    private void OnRenamed(object sender, RenamedEventArgs e) => _onTriggered("file:renamed");

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
    }
}
