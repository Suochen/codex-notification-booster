# Codex Notification Booster

<details open name="language">
<summary><strong>中文</strong></summary>

让 Codex 完成任务时真正提醒你，尤其是在你听音乐的时候。

这是一个给 Codex 用的 Windows 11 托盘小工具。

它解决一个很具体的问题：Codex 干完活以后，Windows 通知经常不够明显，尤其是你在听音乐、看视频或者把注意力放到别的窗口时，很容易错过。更麻烦的是，Windows 自带通知声音有时不会正常响，或者被音乐盖过去。

这个工具会监听 Windows 通知中心里的 Codex 通知。识别到 Codex 有新通知后，它会用自己的进程播放一段更明显的提示音，并且可以在提示音播放时短暂降低其他应用的音量，让通知更容易被听见。

它的目标很窄：只让 Codex 通知更容易被听见。它不会提高 Codex 整个应用音量，也不会修改系统主音量。

## 功能

- 监听 Codex 的 Windows 通知。
- 识别到 Codex 通知后播放额外提示音。
- 可选音频闪避：提示音播放时短暂降低其他应用音量。
- 托盘常驻，不显示主窗口。
- 中文托盘菜单。
- 可暂停或恢复通知增强。
- 可开启或关闭开机自启动。
- 可手动测试提示音。
- 可手动恢复音量。
- 本地日志，方便排查问题。
- portable zip 发布，解压后直接运行。

## 使用方法

1. 在 GitHub Releases 下载 `CodexNotificationBooster-win-x64.zip`。
2. 解压整个 zip。
3. 打开 Codex 设置，把 `轮次完成通知` 设为 `始终`。
4. 打开 `win-x64` 文件夹。
5. 双击 `CodexNotificationBooster.exe`。
6. 程序会出现在 Windows 右下角托盘区域。
7. 右键托盘图标打开菜单。

首次运行时，Windows 可能会请求通知读取权限。这个权限用于识别 Codex 通知，不会清除、回复、修改或上传你的通知内容。

## 当前状态

- 运行方式：portable 目录，解压/复制后双击运行。
- 支持系统：Windows 11 x64。
- 当前没有安装器、开始菜单入口或后台服务。
- 支持可选开机自启动，默认关闭，需要用户在托盘菜单手动开启。
- 当前没有音效选择器；内置一段固定提示音。
- WSL 只用于开发和构建，不是运行环境。

## 工作原理

程序不是监控 Codex 进程，而是监听 Windows 通知中心里的 toast 通知。

流程是：

1. Windows 出现一条通知。
2. 程序读取 Windows 暴露的通知元数据。
3. 如果判断为 Codex 通知，就由本工具进程播放提示音。
4. 如果开启“音频闪避”，程序会在提示音播放前短暂降低其他可识别播放会话的音量，随后自动恢复。

这意味着：复制到另一台电脑后，只要那台电脑的 Codex 会正常发 Windows 通知，并且 Windows 授权本工具读取通知，就可以工作。

## 下载/运行

当前项目使用 portable 目录发布。发布目录类似：

```text
artifacts\portable-windows\Release\win-x64
```

运行文件是：

```text
CodexNotificationBooster.exe
```

但它不是单文件程序。不要只复制 `CodexNotificationBooster.exe`，必须复制整个 `win-x64` 目录，否则缺少依赖文件时可能无法启动。

在本机当前构建中，完整路径是：

```text
C:\Users\Shuhari\code\codex-notification-booster\artifacts\portable-windows\Release\win-x64\CodexNotificationBooster.exe
```

启动方式：

1. 打开 `win-x64` 目录。
2. 双击 `CodexNotificationBooster.exe`。
3. 程序会出现在 Windows 右下角托盘区域。
4. 右键托盘图标打开菜单。

退出方式：

1. 右键托盘图标。
2. 点击 `退出`。

## 首次运行权限

首次运行时，Windows 可能会要求授予读取通知的权限。

如果程序提示监听不可用，检查：

1. Windows 设置里是否允许应用读取通知。
2. Codex 自己是否能正常发 Windows 通知。
3. Windows 通知中心里是否能看到 Codex 通知。

程序需要读取通知元数据，但不会清除、移动、回复、关闭或修改任何通知。

## 托盘菜单

右键托盘图标可以看到中文菜单：

- `已启用` / `已暂停`：开启或暂停 Codex 通知增强提示音。
- `音频闪避：开` / `音频闪避：关`：开启或关闭短暂降低其他应用音量。
- `开机自启动：开` / `开机自启动：关`：为当前 Windows 用户开启或关闭登录后自动启动。
- `测试提示音`：立即播放一次内置提示音。
- `恢复音量`：手动尝试恢复被降低的应用音量。
- `打开日志目录`：打开本地日志目录。
- `退出`：关闭程序。

## 开机自启动

开机自启动默认关闭。需要时右键托盘图标，点击 `开机自启动：关`，菜单会切换为 `开机自启动：开`。

实现方式是写入当前用户的 Windows 启动项：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

它不需要管理员权限，不创建 Windows 服务，也不会写入所有用户的启动项。

关闭方式：

1. 右键托盘图标。
2. 点击 `开机自启动：开`。
3. 菜单切换为 `开机自启动：关`。

关闭时程序会删除自己创建的 `CodexNotificationBooster` 启动项。如果 zip 解压目录被移动，并且开机自启动已经开启，下次运行程序时会把启动路径刷新为当前 exe 路径。

## 音频行为

程序会播放自己的提示音，因此不会把 Codex 应用本身的音量调大。

如果开启音频闪避：

- 程序会尝试读取当前 Windows 音频播放会话。
- 对可识别、可安全调整的其他播放会话短暂降低音量。
- 播放提示音后自动恢复。
- 如果某些会话无法读取或无法调整，程序会跳过或记录诊断，不会调整麦克风输入音量。
- 如果恢复异常，可以在托盘菜单点击 `恢复音量`。

受 Windows 系统主音量限制：如果电脑总音量很低，提示音也不能真正突破系统主音量上限。音频闪避的作用是让提示音相对更突出。

## 日志和隐私

运行日志保存在本机：

```text
%LOCALAPPDATA%\CodexNotificationBooster\logs
```

日志用于排查：

- 程序是否启动；
- 通知监听权限状态；
- 是否识别到 Codex 通知；
- 是否播放提示音；
- 是否执行音频闪避和恢复；
- 是否出现可恢复错误。

正常托盘程序日志默认不会记录原始通知正文、标题、文本行或原始 XML。

不要把本机日志直接上传到 GitHub，除非已经检查并确认没有敏感内容。

## 复制到其他电脑

可以复制到其他 Windows 11 x64 电脑测试，但要复制整个发布目录：

```text
win-x64\
```

目标电脑需要满足：

1. Windows 11 x64。
2. Codex Windows 应用能正常发通知。
3. 首次运行时允许本工具读取通知。
4. Windows 通知中心里能出现 Codex 通知。

如果另一台电脑上的 Codex app identity 和本机不同，程序可能识别不到 Codex 通知。此时需要查看本地日志里的应用身份字段，再调整匹配规则。

## GitHub Release zip

面向普通用户分发时，建议把整个 `win-x64` 发布目录压缩成 zip 上传到 GitHub Releases。用户下载 zip 后解压，再双击：

```text
CodexNotificationBooster.exe
```

不要只压缩单独的 exe；当前 portable 版本需要同目录下的运行文件。发布包是 self-contained，用户不需要另外安装 .NET 运行时。

## 常见问题

### 双击 exe 没反应

确认你复制的是整个 `win-x64` 目录，而不是只有 `CodexNotificationBooster.exe`。

也可以从 PowerShell 里启动，观察是否有错误：

```powershell
Set-Location C:\path\to\win-x64
.\CodexNotificationBooster.exe
```

### 右下角看不到图标

检查 Windows 托盘隐藏图标区域。程序图标名称是：

```text
Codex Notification Booster
```

### 没有提示音

按顺序检查：

1. 右键托盘图标，点击 `测试提示音`。
2. 确认 Windows 主音量不是静音。
3. 确认本工具没有在音量混合器里被静音。
4. 确认 Codex 通知确实出现在 Windows 通知中心。
5. 打开日志目录，看是否有监听权限或播放失败记录。

### 一直弹“通知监听暂时不可用”

这通常表示 Windows 通知监听 API 当前不可用或权限异常。

处理方式：

1. 退出程序后重新启动。
2. 检查 Windows 通知权限。
3. 确认不是从 WSL 里运行 exe。
4. 打开日志目录查看 `listener-poll-failed` 或 `listener-access-status`。

### 音乐播放时 Windows 自带通知声音听不到

这正是本工具要解决的问题之一。Windows 自带 toast 声音可能在某些场景下不明显或不播放；本工具用自己的进程播放提示音，并可短暂降低其他应用音量，让 Codex 通知更容易被听见。

## 从源码构建 portable 版本

需要 .NET 8 SDK。

在 Windows PowerShell 里运行：

```powershell
Set-Location C:\path\to\codex-notification-booster
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1
```

如果 `dotnet.exe` 不在 PATH：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable-windows.ps1 -DotNetPath "C:\Program Files\dotnet\dotnet.exe"
```

默认输出目录：

```text
artifacts\portable-windows\Release\win-x64
```

底层发布形态：

```powershell
dotnet publish .\src\CodexNotificationBooster.Tray\CodexNotificationBooster.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\artifacts\portable-windows\Release\win-x64
```

## 开发验证

常用检查：

```powershell
dotnet build CodexNotificationBooster.sln
dotnet test tests\CodexNotificationBooster.Core.Tests\CodexNotificationBooster.Core.Tests.csproj
```

Windows portable smoke 建议：

1. 发布 portable build。
2. 启动 `CodexNotificationBooster.exe`。
3. 确认托盘图标出现。
4. 右键托盘图标，点击 `测试提示音`。
5. 右键托盘图标，点击 `开机自启动：关`，确认菜单变为 `开机自启动：开`。
6. 再点击 `开机自启动：开`，确认菜单变为 `开机自启动：关`，并确认启动项被删除。
7. 触发一条 Codex 通知。
8. 确认提示音播放一次，不循环触发。
9. 检查日志没有持续刷 `listener-poll-failed`。

## 项目边界

当前版本不做这些事：

- 不安装 Windows 服务。
- 不默认创建开机自启；只有用户手动开启托盘菜单开关时才写入当前用户启动项。
- 不写安装器卸载信息。
- 不修改系统主音量。
- 不提高 Codex 应用本身音量。
- 不清除或修改通知。
- 不上传日志。

后续如果需要安装包、单文件发布、音效选择器或更复杂的通知匹配，应单独开 issue/PRD。

</details>

<details name="language">
<summary><strong>English</strong></summary>

Make Codex completion notifications noticeable, especially when music is playing.

Codex Notification Booster is a Windows 11 tray helper for Codex users.

It solves a small but annoying problem: Codex can finish a task while you are focused on another window, listening to music, or watching something, and the normal Windows notification is easy to miss. Sometimes the built-in Windows notification sound is too quiet, covered by music, or does not play clearly.

This helper watches the Windows notification center for Codex notifications. When it sees a new Codex notification, it plays its own clearer sound and can briefly lower other app audio while the sound plays.

The goal is narrow: make Codex notifications easier to hear. It does not raise Codex's app volume and does not change the Windows master volume.

### Features

- Watches for Codex Windows notifications.
- Plays an extra sound when a Codex notification appears.
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
3. Open Codex settings and set `Turn completion notifications` to `Always`.
4. Open the `win-x64` folder.
5. Double-click `CodexNotificationBooster.exe`.
6. The app appears in the Windows system tray.
7. Right-click the tray icon to open the menu.

On first run, Windows may ask for notification access. This permission is used to identify Codex notifications. The app does not clear, reply to, modify, or upload notification content.

### Current Status

- Distribution: portable folder, unzip/copy and double-click to run.
- Supported system: Windows 11 x64.
- No installer, Start Menu entry, or background service.
- Optional startup on Windows login, off by default, enabled manually from the tray menu.
- No sound picker yet; the app uses one built-in notification sound.
- WSL is only used for development and builds, not for runtime.

### How It Works

The helper does not monitor the Codex process. It listens to toast notifications exposed by the Windows notification center.

Flow:

1. A Windows notification appears.
2. The helper reads the notification metadata exposed by Windows.
3. If the notification is identified as a Codex notification, the helper plays its own sound.
4. If audio ducking is enabled, the helper briefly lowers other readable playback sessions before restoring them.

This means the app can work on another Windows 11 machine as long as Codex sends normal Windows notifications there and Windows grants notification access to the helper.

### Tray Menu

The tray menu labels are currently Chinese:

- `已启用` / `已暂停`: enable or pause Codex notification boosting.
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

The helper plays its own notification sound. It does not turn up Codex itself.

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

Logs help troubleshoot startup, notification access, Codex matching, sound playback, audio ducking, restore behavior, and recoverable errors.

Normal tray logs do not record raw notification title, body, text lines, or raw XML. Do not upload local logs to GitHub unless you have checked them for sensitive information.

### Copying To Another PC

Copy the whole `win-x64` folder, not just the exe.

Target machine requirements:

1. Windows 11 x64.
2. Codex for Windows can send notifications.
3. Notification access is granted to this helper.
4. Codex notifications appear in Windows notification center.

If Codex uses a different app identity on another machine, the helper may not recognize it. Check the local logs for app identity fields before changing matching rules.

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
4. Make sure Codex notifications appear in Windows notification center.
5. Open the log directory and check for notification access or playback errors.

#### "Notification Listener Temporarily Unavailable"

This usually means the Windows notification listener API is unavailable or permission is not granted.

Try:

1. Quit and restart the app.
2. Check Windows notification permissions.
3. Make sure you are not running the exe from WSL.
4. Open the log directory and look for `listener-poll-failed` or `listener-access-status`.

#### Windows Notification Sound Is Hard To Hear While Music Is Playing

That is the main reason this helper exists. Windows toast sound can be unclear or unreliable while other audio is playing. This helper plays its own sound and can briefly lower other app volume so Codex completion is easier to notice.

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
6. Trigger a Codex notification.
7. Confirm the helper sound plays once and does not loop.
8. Check logs are not repeatedly writing `listener-poll-failed`.

### Project Boundaries

Current version does not:

- Install a Windows service.
- Enable startup by default.
- Write installer uninstall metadata.
- Change Windows master volume.
- Raise Codex app volume.
- Clear or modify notifications.
- Upload logs.

Future installer, single-file release, sound picker, or more complex notification matching work should be handled in separate issues or PRDs.

</details>
