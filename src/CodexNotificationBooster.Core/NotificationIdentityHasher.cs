using System.Security.Cryptography;
using System.Text;

namespace CodexNotificationBooster.Core;

public static class NotificationIdentityHasher
{
    public static string CreateDedupKey(NotificationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Sha256Hex(string.Join(
            "|",
            record.AppUserModelId,
            record.NotificationId?.ToString(),
            record.CreationTime?.ToString("O"),
            record.AppId,
            record.PackageFamilyName,
            record.AppDisplayName,
            string.Join("\n", record.TextLines),
            record.RawXmlSha256));
    }

    public static string CreateVisibleStableKey(NotificationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Sha256Hex(string.Join(
            "|",
            record.AppUserModelId,
            record.AppId,
            record.PackageFamilyName,
            record.AppDisplayName,
            string.Join("\n", record.TextLines)));
    }

    public static string Sha256Hex(string? text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
