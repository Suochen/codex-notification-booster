# Codex Notification Booster

A Windows 11 helper for making Codex completion notifications easier to hear without raising all Codex app audio.

## Workflow

This repo is governed by the standalone Matt AI Workflow pack at `Suochen/matt-ai-workflow`.

Current development state: product discovery. Do not implement product behavior until Requirement Analysis, `grill-me`, PRD approval, and issue publication gates have completed.

Runtime boundary: the helper must run directly on Windows 11. WSL is only an editing and repository-management environment.

## Windows notification metadata probe

The first-stage probe is a Windows PowerShell script for discovering what metadata Windows exposes for visible toast notifications. It is read-only: it does not play sounds, change volume, dismiss notifications, clear notifications, move notifications, reply to notifications, or change notification state.

Run it from Windows PowerShell on Windows 11:

```powershell
Set-Location C:\path\to\codex-notification-booster
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-metadata-probe.ps1
```

The default mode polls for a bounded window of 10 minutes. Use `-DurationMinutes` and `-PollSeconds` to change the window:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-metadata-probe.ps1 -DurationMinutes 5 -PollSeconds 2
```

Use `-Once` to capture only notifications currently visible to the Windows notification listener:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-metadata-probe.ps1 -Once
```

By default, JSONL records are written outside this repository:

```text
%LOCALAPPDATA%\CodexNotificationBooster\notification-probe.jsonl
```

You can override the local log path:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-metadata-probe.ps1 -LogPath "$env:LOCALAPPDATA\CodexNotificationBooster\manual-probe.jsonl"
```

The probe prints the Windows notification listener permission status on startup. If access is `Unspecified`, it requests access using the supported Windows API. If access is denied or unavailable, it exits with a non-zero code and prints remediation guidance. Enable notification access for the PowerShell host in Windows Settings, then run the probe again.

### Log sensitivity

Probe logs may contain raw notification titles, bodies, app identity fields, timestamps, and raw toast XML. Treat `notification-probe.jsonl` as sensitive local data. Do not commit it, upload it, paste it into GitHub, or share it unless you have reviewed and intentionally redacted the contents.

### Manual verification

1. Run the probe directly from Windows PowerShell, not from WSL.
2. Confirm the startup output reports notification listener permission status.
3. If prompted, grant notification listener access for the PowerShell host.
4. Trigger or wait for one Codex completion notification during the polling window.
5. Trigger at least one non-Codex notification for comparison.
6. Open the JSONL log under `%LOCALAPPDATA%\CodexNotificationBooster\notification-probe.jsonl` or the path supplied with `-LogPath`.
7. Confirm records include broad metadata such as capture time, notification creation time, notification ID, app display name, app identity fields when available, text lines/title/body, raw XML when available, and a dedup key.
8. Confirm the probe did not play sound, change volume, dismiss, clear, move, reply to, or modify notifications.

### Development checks

From Windows PowerShell or WSL with `powershell.exe` available:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-notification-probe.ps1
```
