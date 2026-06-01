using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Core.Tests;

public sealed class StartupEntryManagerTests
{
    [Fact]
    public void IsEnabledReturnsFalseWhenEntryIsMissing()
    {
        var store = new InMemoryStartupEntryStore();
        var manager = CreateManager(store, @"C:\Apps\CodexNotificationBooster.exe");

        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void EnableWritesQuotedCurrentExecutableCommand()
    {
        var store = new InMemoryStartupEntryStore();
        var manager = CreateManager(store, @"C:\Apps\Codex Notification Booster\CodexNotificationBooster.exe");

        manager.Enable();

        Assert.Equal(
            @"""C:\Apps\Codex Notification Booster\CodexNotificationBooster.exe""",
            store.Commands[StartupEntryManager.DefaultEntryName]);
        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void DisableDeletesEntryAndTreatsMissingEntryAsOff()
    {
        var store = new InMemoryStartupEntryStore();
        var manager = CreateManager(store, @"C:\Apps\CodexNotificationBooster.exe");
        manager.Enable();

        manager.Disable();

        Assert.False(store.Commands.ContainsKey(StartupEntryManager.DefaultEntryName));
        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void RefreshIfEnabledUpdatesMovedExecutablePath()
    {
        var store = new InMemoryStartupEntryStore
        {
            Commands =
            {
                [StartupEntryManager.DefaultEntryName] = @"""C:\Old\CodexNotificationBooster.exe"""
            }
        };
        var manager = CreateManager(store, @"D:\Tools\CodexNotificationBooster.exe");

        manager.RefreshIfEnabled();

        Assert.Equal(
            @"""D:\Tools\CodexNotificationBooster.exe""",
            store.Commands[StartupEntryManager.DefaultEntryName]);
    }

    [Fact]
    public void RefreshIfEnabledDoesNothingWhenEntryIsMissing()
    {
        var store = new InMemoryStartupEntryStore();
        var manager = CreateManager(store, @"D:\Tools\CodexNotificationBooster.exe");

        manager.RefreshIfEnabled();

        Assert.Empty(store.Commands);
    }

    private static StartupEntryManager CreateManager(InMemoryStartupEntryStore store, string executablePath)
    {
        return new StartupEntryManager(store, () => executablePath);
    }

    private sealed class InMemoryStartupEntryStore : IStartupEntryStore
    {
        public Dictionary<string, string> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? ReadCommand(string name)
        {
            return Commands.GetValueOrDefault(name);
        }

        public void WriteCommand(string name, string command)
        {
            Commands[name] = command;
        }

        public void DeleteCommand(string name)
        {
            Commands.Remove(name);
        }
    }
}
