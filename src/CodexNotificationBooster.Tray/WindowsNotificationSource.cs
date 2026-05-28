using CodexNotificationBooster.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace CodexNotificationBooster.Tray;

internal sealed class WindowsNotificationSource : INotificationSource
{
    private readonly RedactingFileLogger _logger;
    private UserNotificationListener? _listener;
    private UserNotificationListenerAccessStatus? _lastLoggedAccessStatus;

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

        var records = new List<NotificationRecord>();
        foreach (var notification in notifications)
        {
            try
            {
                records.Add(ConvertToRecord(notification));
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "notification-conversion-skipped", "Skipped one visible notification because its Windows metadata could not be converted.", new Dictionary<string, object?>
                {
                    ["exceptionType"] = ex.GetType().FullName,
                    ["error"] = ex.Message,
                    ["stackTrace"] = ex.ToString()
                });
            }
        }

        return records;
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

        LogAccessStatusIfChanged(status);

        if (status != UserNotificationListenerAccessStatus.Allowed)
        {
            throw new InvalidOperationException("Windows notification listener access is not allowed.");
        }
    }

    private void LogAccessStatusIfChanged(UserNotificationListenerAccessStatus status)
    {
        if (_lastLoggedAccessStatus == status)
        {
            return;
        }

        _lastLoggedAccessStatus = status;
        _logger.Log(
            status == UserNotificationListenerAccessStatus.Allowed ? LogLevel.Info : LogLevel.Error,
            "listener-access-status",
            "Windows notification listener access status checked.",
            new Dictionary<string, object?>
            {
                ["accessStatus"] = status.ToString()
            });
    }

    private static NotificationRecord ConvertToRecord(UserNotification userNotification)
    {
        var notification = SafeGet(() => userNotification.Notification);
        var appInfo = SafeGet(() => userNotification.AppInfo);
        var displayInfo = SafeGet(() => appInfo?.DisplayInfo);
        var package = SafeGet(() => appInfo?.Package);
        var textLines = GetToastTextLines(notification).ToArray();

        return new NotificationRecord
        {
            SchemaVersion = 1,
            CapturedAt = DateTimeOffset.UtcNow,
            CreationTime = SafeGet(() => userNotification.CreationTime.ToUniversalTime()),
            NotificationId = SafeGet<uint?>(() => userNotification.Id),
            AppDisplayName = SafeGet(() => displayInfo?.DisplayName),
            AppUserModelId = SafeGet(() => appInfo?.AppUserModelId),
            AppId = SafeGet(() => appInfo?.Id),
            PackageFamilyName = SafeGet(() => appInfo?.PackageFamilyName),
            PackageFullName = SafeGet(() => package?.Id?.FullName)
        }.WithSanitizedTextLines(textLines);
    }

    private static IEnumerable<string> GetToastTextLines(Notification? notification)
    {
        var visual = SafeGet(() => notification?.Visual);
        var binding = SafeGet(() => visual?.GetBinding(KnownNotificationBindings.ToastGeneric));
        var textElements = SafeGet(() => binding?.GetTextElements());
        if (textElements is null)
        {
            yield break;
        }

        foreach (var element in textElements)
        {
            var text = SafeGet(() => element.Text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
        }
    }

    private static T? SafeGet<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }

}
