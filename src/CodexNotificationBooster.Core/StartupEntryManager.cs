namespace CodexNotificationBooster.Core;

public sealed class StartupEntryManager
{
    public const string DefaultEntryName = "CodexNotificationBooster";

    private readonly IStartupEntryStore _store;
    private readonly Func<string> _executablePathProvider;

    public StartupEntryManager(IStartupEntryStore store, Func<string> executablePathProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executablePathProvider = executablePathProvider ?? throw new ArgumentNullException(nameof(executablePathProvider));
    }

    public bool IsEnabled()
    {
        return string.Equals(
            _store.ReadCommand(DefaultEntryName),
            CreateCurrentEntry().Command,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        var entry = CreateCurrentEntry();
        _store.WriteCommand(entry.Name, entry.Command);
    }

    public void Disable()
    {
        _store.DeleteCommand(DefaultEntryName);
    }

    public void RefreshIfEnabled()
    {
        var command = _store.ReadCommand(DefaultEntryName);
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var entry = CreateCurrentEntry();
        if (!string.Equals(command, entry.Command, StringComparison.OrdinalIgnoreCase))
        {
            _store.WriteCommand(entry.Name, entry.Command);
        }
    }

    private StartupEntry CreateCurrentEntry()
    {
        return new StartupEntry(DefaultEntryName, _executablePathProvider());
    }
}
