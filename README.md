# Codex Notification Booster

中文 | [English](README.en.md)

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
