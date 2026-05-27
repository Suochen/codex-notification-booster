# Codex Notification Booster

A Windows 11 helper for making Codex notifications easier to hear without raising all Codex app audio.

## Workflow

This repo is governed by the standalone Matt AI Workflow pack at `Suochen/matt-ai-workflow`.

Current development state: product discovery. Do not implement product behavior until Requirement Analysis, `grill-me`, PRD approval, and issue publication gates have completed.

Runtime boundary: the helper must run directly on Windows 11. WSL is only an editing and repository-management environment.

## Portable Windows tray build

Maintainers can publish a portable, single-directory Windows 11 tray build with:

```powershell
Set-Location C:\path\to\codex-notification-booster
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1
```

Publishing requires the .NET 8 SDK on the build machine. If `dotnet.exe` is not
on PATH, pass it explicitly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1 -DotNetPath "C:\Program Files\dotnet\dotnet.exe"
```

The default output directory is:

```text
artifacts\portable-windows\Release\win-x64
```

The script wraps this publish shape:

```powershell
dotnet publish .\src\CodexNotificationBooster.Tray\CodexNotificationBooster.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\artifacts\portable-windows\Release\win-x64
```

This is a portable directory build, not an installer. It does not create an
MSI, setup wizard, Windows service, Start Menu entry, or startup-on-boot task.
The published app runs directly on Windows 11 and does not require WSL at
runtime.

### Launch

On Windows 11, open the publish directory and start:

```text
CodexNotificationBooster.exe
```

The app appears as a tray icon named `Codex Notification Booster`. Right-click
the tray icon for the current controls: enabled/paused, audio ducking on/off,
test sound, restore volume, open log directory, and exit.

Local runtime data is stored outside the portable directory:

```text
%LOCALAPPDATA%\CodexNotificationBooster
%LOCALAPPDATA%\CodexNotificationBooster\logs
```

Logs are JSONL files with local diagnostics. They redact raw notification
title, body, text lines, and raw XML.

### Portable manual verification

Run these checks directly on Windows 11 from the published portable directory:

1. Launch `CodexNotificationBooster.exe` and confirm the tray icon appears.
2. Right-click the tray icon and choose `测试提示音`; confirm the fixed helper
   sound plays.
3. Trigger or wait for a real Codex notification; confirm helper playback occurs
   for the Codex notification.
4. Start the app while old notifications are already visible in Windows
   notification center; confirm the baseline/backlog does not cause repeated
   helper playback for already-seen notifications.
5. With another non-Codex audio source playing, trigger a Codex notification and
   confirm audio ducking lowers eligible non-Codex audio during helper playback.
6. Confirm volume returns automatically after the short ducking window.
7. If volume does not recover, choose `恢复音量` from the tray menu and confirm
   the affected session volume is restored.
8. Choose `打开日志目录` and confirm JSONL logs are under
   `%LOCALAPPDATA%\CodexNotificationBooster\logs`.
9. Inspect only event codes and redacted metadata needed for verification, such
   as `tray-started`, `listener-access-status`, `matched-playback-requested`,
   `duplicate-notification-skipped`, `audio-duck-restore-due`, and
   `tray-exit`. Do not upload, paste, or request raw notification title, body,
   text lines, or XML.
10. Leave the app running for several polling intervals after a temporary
    listener or playback error and confirm it remains in the tray.
11. Choose `退出` from the tray menu and confirm the tray icon disappears and
    the latest log includes `tray-exit`.

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
4. Trigger or wait for one Codex notification during the polling window.
5. Trigger at least one non-Codex notification for comparison.
6. Open the JSONL log under `%LOCALAPPDATA%\CodexNotificationBooster\notification-probe.jsonl` or the path supplied with `-LogPath`.
7. Confirm records include broad metadata such as capture time, notification creation time, notification ID, app display name, app identity fields when available, text lines/title/body, raw XML when available, and a dedup key.
8. Confirm the probe did not play sound, change volume, dismiss, clear, move, reply to, or modify notifications.

### Development checks

From Windows PowerShell or WSL with `powershell.exe` available:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-notification-probe.ps1
```

The matcher-only check uses redacted/minimal fixtures and does not require
Windows notification listener APIs:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-notification-matcher.ps1
```

The first playback-slice check also uses redacted/minimal fixtures and a fake
playback implementation. It proves Codex metadata requests helper-owned
playback, QQ/TopNotify/Edge metadata is ignored, and config/playback failures
return diagnostics without notification listener APIs or volume changes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-notification-playback.ps1
```

To manually verify real helper-owned playback on Windows 11, pass a local `.wav`
file to the isolated playback adapter:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\play-notification-sound.ps1 -SoundPath "C:\path\to\custom.wav"
```

The playback adapter only plays the configured local `.wav` file from the helper
process. It does not read notifications, suppress the original notification
sound, change Codex app mixer volume, change global Windows volume, change
Windows notification volume, or change other application audio.

## Foreground notification helper

The first runnable helper slice is a foreground Windows PowerShell loop. It
polls visible Windows notifications, deduplicates repeated records, matches
Codex notification metadata, requests helper-owned WAV playback for matches,
and writes local diagnostics.

Run it directly from Windows PowerShell on Windows 11:

```powershell
Set-Location C:\path\to\codex-notification-booster
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-helper-loop.ps1 -SoundPath "C:\path\to\custom.wav"
```

Use `-PollSeconds`, `-DurationMinutes`, or `-Once` for bounded manual runs:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-helper-loop.ps1 -SoundPath "C:\path\to\custom.wav" -DurationMinutes 5 -PollSeconds 2
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-helper-loop.ps1 -SoundPath "C:\path\to\custom.wav" -Once
```

The helper requires a local `.wav` file. It validates the path at startup and
logs `config-valid`, `missing-sound-file`, `unsupported-sound-file`, or related
diagnostic codes.

By default, JSONL diagnostics are written outside this repository:

```text
%LOCALAPPDATA%\CodexNotificationBooster\helper-diagnostics.jsonl
```

Override the diagnostics path when needed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\notification-helper-loop.ps1 -SoundPath "C:\path\to\custom.wav" -LogPath "$env:LOCALAPPDATA\CodexNotificationBooster\manual-helper.jsonl"
```

Diagnostics include timestamps, event codes, app identity fields, notification
IDs, dedup keys, match/playback status, listener permission status, config
status, and loop errors. They omit raw notification body text, title text,
text lines, and raw XML by default.

The helper prints listener permission status on startup. If permission is
unavailable or denied, enable notification access for the PowerShell host in
Windows Settings, then run the helper again. This slice reuses the same
foreground PowerShell notification listener path as the metadata probe.

Stop the foreground helper by pressing `Ctrl+C`, closing the PowerShell window,
using `-Once`, or choosing a bounded `-DurationMinutes` value. There is no
installer, tray UI, service, autostart, or background daemon in this slice.

### Helper manual verification

1. Run the helper directly from Windows PowerShell, not as a WSL runtime.
2. Pass a known local `.wav` path with `-SoundPath`.
3. Confirm startup output reports the diagnostics path and notification
   listener permission status.
4. Trigger or wait for a Codex notification during the foreground run.
5. Trigger or wait for at least one non-Codex notification.
6. Inspect `%LOCALAPPDATA%\CodexNotificationBooster\helper-diagnostics.jsonl`
   or the path passed with `-LogPath`.
7. Confirm matched Codex notifications emit `matched-playback-requested` once
   per dedup key, repeated visible notifications emit
   `duplicate-notification-skipped`, and non-Codex notifications emit
   `ignored-notification`.
8. Confirm diagnostics do not include raw notification body text, title text,
   text lines, or raw XML.

The helper remains read-only with respect to notifications. It does not dismiss,
clear, reply to, move, or otherwise mutate notifications. It also does not
suppress original notification audio, change Codex app mixer volume, change
global Windows volume, change Windows notification volume, or change other
application audio.

The helper-loop check uses fake notification records and fake playback, so it
does not require live notification listener APIs:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-notification-helper-loop.ps1
```
