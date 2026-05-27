namespace CodexNotificationBooster.Core;

public static class HelperOwnedNotificationFilter
{
    public const string HelperDisplayName = "Codex Notification Booster";

    public static bool IsHelperOwned(NotificationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return string.Equals(record.AppDisplayName, HelperDisplayName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.AppId, HelperDisplayName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.AppUserModelId, HelperDisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
