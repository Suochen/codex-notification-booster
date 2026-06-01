namespace CodexNotificationBooster.Core;

public interface IStartupEntryStore
{
    string? ReadCommand(string name);

    void WriteCommand(string name, string command);

    void DeleteCommand(string name);
}
