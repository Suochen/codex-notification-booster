# Codex Notification Booster

[中文](README.md) | [GitHub Release](https://github.com/Suochen/codex-notification-booster/releases/tag/v0.1.0)

Make Codex and Claude Desktop notifications noticeable, especially when music is playing.

Codex Notification Booster is a Windows 11 tray helper for Codex and Claude Desktop users.

It solves a small but annoying problem: Codex or Claude Desktop can notify you while you are focused on another window, listening to music, or watching something, and the normal Windows notification is easy to miss. Sometimes the built-in Windows notification sound is too quiet, covered by music, or does not play clearly.

This helper watches the Windows notification center for Codex and Claude Desktop notifications. When it sees a new target app notification, it plays its own clearer sound and can briefly lower other app audio while the sound plays.

The goal is narrow: make Codex and Claude Desktop notifications easier to hear. It does not raise the target app's volume and does not change the Windows master volume.

### Features

- Watches for Codex and Claude Desktop Windows notifications.
- Plays an extra sound when a target app notification appears.
- Optional audio ducking: briefly lowers other app audio while the helper sound plays.
- Runs in the system tray with no main window.
- Chinese tray menu.
- Pause or resume notification boosting.
- Optional startup on Windows login.
- Test the helper sound from the tray menu.
- Manually restore audio volume from the tray menu.
- Local logs for troubleshooting.
- Portable zip release: unzip and run.

### How To Use

1. Download `CodexNotificationBooster-win-x64.zip` from GitHub Releases.
2. Unzip the whole archive.
3. Open Codex settings and set `Turn completion notifications` to `Always`; Claude Desktop can use normal system notifications.
4. Open the `win-x64` folder.
5. Double-click `CodexNotificationBooster.exe`.
6. The app appears in the Windows system tray.
7. Right-click the tray icon to open the menu.

On first run, Windows may ask for notification access. This permission is used to identify Codex and Claude Desktop notifications. The app does not clear, reply to, modify, or upload notification content.

### Current Status

- Distribution: portable folder, unzip/copy and double-click to run.
- Supported system: Windows 11 x64.
- No installer, Start Menu entry, or background service.
- Optional startup on Windows login, off by default, enabled manually from the tray menu.
- No sound picker yet; the app uses one built-in notification sound.
- WSL is only used for development and builds, not for runtime.

### How It Works

The helper does not monitor the Codex or Claude Desktop process. It listens to toast notifications exposed by the Windows notification center.

Flow:

1. A Windows notification appears.
2. The helper reads the notification metadata exposed by Windows.
3. If the notification is identified as a Codex or Claude Desktop notification, the helper plays its own sound.
4. If audio ducking is enabled, the helper briefly lowers other readable playback sessions before restoring them.

This means the app can work on another Windows 11 machine as long as Codex or Claude Desktop sends normal Windows notifications there and Windows grants notification access to the helper.

### Tray Menu

The tray menu labels are currently Chinese:

- `已启用` / `已暂停`: enable or pause target app notification boosting.
- `音频闪避：开` / `音频闪避：关`: turn audio ducking on or off.
- `开机自启动：开` / `开机自启动：关`: enable or disable startup for the current Windows user.
- `测试提示音`: play the built-in test sound.
- `恢复音量`: try to restore ducked audio volume.
- `打开日志目录`: open the local log directory.
- `退出`: quit the app.

### Startup On Login

Startup on login is off by default. To enable it, right-click the tray icon and click `开机自启动：关`; the menu changes to `开机自启动：开`.

The app writes one current-user Windows startup entry:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

It does not need administrator permission, does not create a Windows service, and does not write a machine-wide startup entry.

When startup is disabled, the app removes its own `CodexNotificationBooster` startup entry. If the portable folder is moved while startup is enabled, the next app run refreshes the startup path to the current exe path.

### Audio Behavior

The helper plays its own notification sound. It does not turn up the target app itself.

When audio ducking is enabled:

- The helper reads current Windows output playback sessions.
- It briefly lowers readable sessions that are safe to adjust.
- It restores them after the notification sound.
- If a session cannot be read or adjusted, it is skipped and logged.
- It does not adjust microphone or input device volume.
- If restore fails, use `恢复音量` from the tray menu.

The Windows master volume still applies. If the whole system volume is very low, the helper cannot exceed that limit. Audio ducking only makes the helper sound stand out relative to other playback.

### Logs And Privacy

Logs are stored locally:

```text
%LOCALAPPDATA%\CodexNotificationBooster\logs
```

Logs help troubleshoot startup, notification access, target app matching, sound playback, audio ducking, restore behavior, and recoverable errors.

Normal tray logs do not record raw notification title, body, text lines, or raw XML. Do not upload local logs to GitHub unless you have checked them for sensitive information.

### Copying To Another PC

Copy the whole `win-x64` folder, not just the exe.

Target machine requirements:

1. Windows 11 x64.
2. Codex or Claude Desktop for Windows can send notifications.
3. Notification access is granted to this helper.
4. The corresponding notifications appear in Windows notification center.

If a target app uses a different app identity on another machine, the helper may not recognize it. Check the local logs for app identity fields before changing matching rules.

### GitHub Release Zip

For normal users, publish the whole `win-x64` output folder as a zip in GitHub Releases. Users download the zip, unzip it, then run:

```text
CodexNotificationBooster.exe
```

Do not zip only the exe. The current portable release needs the files next to it. The release is self-contained, so users do not need to install the .NET runtime separately.

### FAQ

#### Double-clicking The Exe Does Nothing

Make sure you copied the whole `win-x64` folder, not only `CodexNotificationBooster.exe`.

You can also start it from PowerShell to see errors:

```powershell
Set-Location C:\path\to\win-x64
.\CodexNotificationBooster.exe
```

#### I Cannot See The Tray Icon

Check the hidden icons area in the Windows system tray. The icon name is:

```text
Codex Notification Booster
```

#### No Sound Plays

Check in this order:

1. Right-click the tray icon and click `测试提示音`.
2. Make sure Windows master volume is not muted.
3. Make sure this helper is not muted in the volume mixer.
4. Make sure Codex or Claude Desktop notifications appear in Windows notification center.
5. Open the log directory and check for notification access or playback errors.

#### "Notification Listener Temporarily Unavailable"

This usually means the Windows notification listener API is unavailable or permission is not granted.

Try:

1. Quit and restart the app.
2. Check Windows notification permissions.
3. Make sure you are not running the exe from WSL.
4. Open the log directory and look for `listener-poll-failed` or `listener-access-status`.

#### Windows Notification Sound Is Hard To Hear While Music Is Playing

That is the main reason this helper exists. Windows toast sound can be unclear or unreliable while other audio is playing. This helper plays its own sound and can briefly lower other app volume so target app notifications are easier to notice.

### Build From Source

Requires the .NET 8 SDK.

From Windows PowerShell:

```powershell
Set-Location C:\path\to\codex-notification-booster
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1
```

If `dotnet.exe` is not in PATH:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1 -DotNetPath "C:\Program Files\dotnet\dotnet.exe"
```

Default output:

```text
artifacts\portable-windows\Release\win-x64
```

Underlying publish command:

```powershell
dotnet publish .\src\CodexNotificationBooster.Tray\CodexNotificationBooster.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\artifacts\portable-windows\Release\win-x64
```

### Development Checks

```powershell
dotnet build CodexNotificationBooster.sln
dotnet test tests\CodexNotificationBooster.Core.Tests\CodexNotificationBooster.Core.Tests.csproj
```

Suggested Windows portable smoke test:

1. Publish the portable build.
2. Start `CodexNotificationBooster.exe`.
3. Confirm the tray icon appears.
4. Right-click the tray icon and click `测试提示音`.
5. Toggle startup on and off from the tray menu, and confirm the startup entry is removed when off.
6. Trigger a Codex or Claude Desktop notification.
7. Confirm the helper sound plays once and does not loop.
8. Check logs are not repeatedly writing `listener-poll-failed`.

### Project Boundaries

Current version does not:

- Install a Windows service.
- Enable startup by default.
- Write installer uninstall metadata.
- Change Windows master volume.
- Raise target app volume.
- Clear or modify notifications.
- Upload logs.

Future installer, single-file release, sound picker, or more complex notification matching work should be handled in separate issues or PRDs.
