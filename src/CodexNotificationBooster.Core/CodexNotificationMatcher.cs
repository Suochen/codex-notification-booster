namespace CodexNotificationBooster.Core;

public sealed class CodexNotificationMatcher
{
    public const string CodexAppUserModelId = "OpenAI.Codex_2p2nqsd0c76g0!App";
    public const string CodexPackageFamilyName = "OpenAI.Codex_2p2nqsd0c76g0";
    public const string ClaudeDesktopAppUserModelId = "Claude_pzs8sxrjxfjjc!Claude";
    public const string ClaudeDesktopPackageFamilyName = "Claude_pzs8sxrjxfjjc";

    public NotificationMatchDecision Match(NotificationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.Equals(record.AppUserModelId, CodexAppUserModelId, StringComparison.Ordinal))
        {
            return new NotificationMatchDecision(
                Matched: true,
                Reason: "appUserModelId matches observed Codex identity",
                MatchedRule: "codex-app-user-model-id");
        }

        if (string.Equals(record.PackageFamilyName, CodexPackageFamilyName, StringComparison.Ordinal))
        {
            return new NotificationMatchDecision(
                Matched: true,
                Reason: "packageFamilyName matches observed Codex identity",
                MatchedRule: "codex-package-family-name");
        }

        if (string.Equals(record.AppUserModelId, ClaudeDesktopAppUserModelId, StringComparison.Ordinal))
        {
            return new NotificationMatchDecision(
                Matched: true,
                Reason: "appUserModelId matches observed Claude Desktop identity",
                MatchedRule: "claude-desktop-app-user-model-id");
        }

        if (string.Equals(record.PackageFamilyName, ClaudeDesktopPackageFamilyName, StringComparison.Ordinal))
        {
            return new NotificationMatchDecision(
                Matched: true,
                Reason: "packageFamilyName matches observed Claude Desktop identity",
                MatchedRule: "claude-desktop-package-family-name");
        }

        return new NotificationMatchDecision(
            Matched: false,
            Reason: "notification metadata does not match observed Codex or Claude Desktop identity fields",
            MatchedRule: "no-target-app-identity-match");
    }
}

public sealed record NotificationMatchDecision(bool Matched, string Reason, string MatchedRule);
