using CodexNotificationBooster.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace CodexNotificationBooster.Tray;

internal sealed class WindowsNotificationSource : INotificationSource
{
    private readonly RedactingFileLogger _logger;
    private UserNotificationListener? _listener;

    public WindowsNotificationSource(RedactingFileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<IReadOnlyList<NotificationRecord>> GetVisibleNotificationsAsync(CancellationToken cancellationToken)
    {
        var listener = GetListener();
        await EnsureAccessAllowedAsync(listener, cancellationToken).ConfigureAwait(false);

        var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
        cancellationToken.ThrowIfCancellationRequested();

        return notifications
            .Select(ConvertToRecord)
            .ToArray();
    }

    private UserNotificationListener GetListener()
    {
        try
        {
            return _listener ??= UserNotificationListener.Current;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "listener-api-unavailable", "Windows notification listener APIs are unavailable.", new Dictionary<string, object?>
            {
                ["exceptionType"] = ex.GetType().FullName,
                ["error"] = ex.Message
            });
            throw;
        }
    }

    private async Task EnsureAccessAllowedAsync(UserNotificationListener listener, CancellationToken cancellationToken)
    {
        var status = listener.GetAccessStatus();
        if (status == UserNotificationListenerAccessStatus.Unspecified)
        {
            status = await listener.RequestAccessAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        _logger.Log(
            status == UserNotificationListenerAccessStatus.Allowed ? LogLevel.Info : LogLevel.Error,
            "listener-access-status",
            "Windows notification listener access status checked.",
            new Dictionary<string, object?>
            {
                ["accessStatus"] = status.ToString()
            });

        if (status != UserNotificationListenerAccessStatus.Allowed)
        {
            throw new InvalidOperationException("Windows notification listener access is not allowed.");
        }
    }

    private static NotificationRecord ConvertToRecord(UserNotification userNotification)
    {
        var notification = userNotification.Notification;
        var appInfo = userNotification.AppInfo;
        var displayInfo = appInfo?.DisplayInfo;
        var package = appInfo?.Package;
        var textLines = GetToastTextLines(notification).ToArray();

        return new NotificationRecord
        {
            SchemaVersion = 1,
            CapturedAt = DateTimeOffset.UtcNow,
            CreationTime = userNotification.CreationTime.ToUniversalTime(),
            NotificationId = userNotification.Id,
            AppDisplayName = displayInfo?.DisplayName,
            AppUserModelId = appInfo?.AppUserModelId,
            AppId = appInfo?.Id,
            PackageFamilyName = appInfo?.PackageFamilyName,
            PackageFullName = package?.Id?.FullName
        }.WithSanitizedTextLines(textLines);
    }

    private static IEnumerable<string> GetToastTextLines(Notification? notification)
    {
        var binding = notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
        var textElements = binding?.GetTextElements();
        if (textElements is null)
        {
            yield break;
        }

        foreach (var element in textElements)
        {
            if (!string.IsNullOrWhiteSpace(element.Text))
            {
                yield return element.Text;
            }
        }
    }

}
