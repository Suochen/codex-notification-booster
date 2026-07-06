using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppPaths _paths = AppPaths.CreateDefault();
    private readonly AppStateStore _stateStore;
    private readonly HelperSoundAssetProvider _soundAssetProvider;
    private readonly RedactingFileLogger _logger;
    private readonly StartupEntryManager _startupEntryManager;
    private readonly AudioDuckingCoordinator _audioDuckingCoordinator;
    private readonly INotificationPlayback _notificationPlayback;
    private readonly NotificationListenerService _notificationListenerService;
    private readonly ManualPlaybackTrigger _manualPlaybackTrigger;
    private readonly TriggerFileWatcher _triggerFileWatcher;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _duckingTimer;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _codexEnabledMenuItem;
    private readonly ToolStripMenuItem _claudeDesktopEnabledMenuItem;
    private readonly ToolStripMenuItem _audioDuckingMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly ToolStripMenuItem _testCodexSoundMenuItem;
    private readonly ToolStripMenuItem _testClaudeDesktopSoundMenuItem;
    private readonly ToolStripMenuItem _restoreVolumeMenuItem;
    private readonly ToolStripMenuItem _openLogsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    private AppState _state;
    private bool _isPolling;

    public TrayApplicationContext()
    {
        _stateStore = new AppStateStore(_paths);
        _soundAssetProvider = new HelperSoundAssetProvider(_paths);
        _logger = new RedactingFileLogger(_paths);
        _state = _stateStore.Load();
        _startupEntryManager = new StartupEntryManager(
            new HkcuRunStartupEntryStore(),
            () => Environment.ProcessPath ?? Application.ExecutablePath);
        _audioDuckingCoordinator = new AudioDuckingCoordinator(
            new WindowsAudioSessionVolumeController(),
            () => _state,
            SaveState,
            Process.GetCurrentProcess().Id,
            HandleAudioDuckingEvent);
        _notificationPlayback = new AudioDuckingNotificationPlayback(
            new HelperSoundPlayback(_soundAssetProvider),
            _audioDuckingCoordinator);
        _notificationListenerService = new NotificationListenerService(
            new WindowsNotificationSource(_logger),
            _notificationPlayback,
            new VisibleNotificationProcessor(),
            _logger,
            IsPlaybackEnabled,
            (title, message) => PostToUi(() => ShowStatusBalloon(title, message)));

        _paths.EnsureDirectories();
        _soundAssetProvider.EnsureAllPresent();
        _logger.ApplyRetention();
        _startupEntryManager.RefreshIfEnabled();
        _logger.Log(LogLevel.Info, "tray-started", "Tray helper shell started.");

        _enabledMenuItem = new ToolStripMenuItem();
        _codexEnabledMenuItem = new ToolStripMenuItem();
        _claudeDesktopEnabledMenuItem = new ToolStripMenuItem();
        _audioDuckingMenuItem = new ToolStripMenuItem();
        _startupMenuItem = new ToolStripMenuItem();
        _testCodexSoundMenuItem = new ToolStripMenuItem("测试 Codex 提示音", null, (_, _) => RunMenuAction("test-codex-sound-menu", () => TestSound(TargetNotificationApp.Codex)));
        _testClaudeDesktopSoundMenuItem = new ToolStripMenuItem("测试 Claude 提示音", null, (_, _) => RunMenuAction("test-claude-desktop-sound-menu", () => TestSound(TargetNotificationApp.ClaudeDesktop)));
        _restoreVolumeMenuItem = new ToolStripMenuItem("恢复音量", null, (_, _) => RunMenuAction("restore-volume-menu", RestoreVolume));
        _openLogsMenuItem = new ToolStripMenuItem("打开日志目录", null, (_, _) => RunMenuAction("open-logs-menu", OpenLogsDirectory));
        _exitMenuItem = new ToolStripMenuItem("退出", null, (_, _) => ExitRequested());

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Visible = true,
            Text = "Codex Notification Booster",
            ContextMenuStrip = new ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _enabledMenuItem,
            _codexEnabledMenuItem,
            _claudeDesktopEnabledMenuItem,
            _audioDuckingMenuItem,
            _startupMenuItem,
            new ToolStripSeparator(),
            _testCodexSoundMenuItem,
            _testClaudeDesktopSoundMenuItem,
            _restoreVolumeMenuItem,
            _openLogsMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem
        ]);

        _notifyIcon.DoubleClick += (_, _) => ShowStatusBalloon("Codex Notification Booster", "托盘助手正在运行。");
        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Right)
            {
                ShowTrayMenu();
            }
        };

        RefreshMenuLabels();

        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };
        _pollTimer.Tick += async (_, _) => await PollNotificationsAsync();
        _pollTimer.Start();

        _duckingTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        _duckingTimer.Tick += (_, _) => _audioDuckingCoordinator.RestoreIfDue();
        _duckingTimer.Start();

        // 前台 hook 触发只认主开关 IsEnabled，刻意不受 per-app 的 IsClaudeDesktopEnabled 影响：
        // 后者用来单独关掉「桌面版 toast 监听」这条通道，而 hook 通知器要独立保留。
        // （原先共用 IsPlaybackEnabled，导致关 toast 会连带把 hook 一起关哑。）
        _manualPlaybackTrigger = new ManualPlaybackTrigger(
            _notificationPlayback,
            _ => _state.IsEnabled,
            (level, code, message, metadata) => _logger.Log(level, code, message, metadata));
        _triggerFileWatcher = new TriggerFileWatcher(
            _paths.TriggerFilePath,
            source => PostToUi(() => _manualPlaybackTrigger.Fire(source)));
        _logger.Log(LogLevel.Info, "manual-trigger-watching", "Watching foreground trigger file.", new Dictionary<string, object?>
        {
            ["triggerFile"] = _paths.TriggerFilePath
        });

        _audioDuckingCoordinator.RecoverPriorState();
        _ = PollNotificationsAsync();
    }

    private void ToggleEnabled()
    {
        _state = _state with { IsEnabled = !_state.IsEnabled };
        PersistState("state-enabled-toggled", $"Enabled state changed to {_state.IsEnabled}.");
        RefreshMenuLabels();
    }

    private void ToggleCodexEnabled()
    {
        _state = _state with { IsCodexEnabled = !_state.IsCodexEnabled };
        PersistState("state-codex-enabled-toggled", $"Codex notification playback state changed to {_state.IsCodexEnabled}.");
        RefreshMenuLabels();
    }

    private void ToggleClaudeDesktopEnabled()
    {
        _state = _state with { IsClaudeDesktopEnabled = !_state.IsClaudeDesktopEnabled };
        PersistState("state-claude-desktop-enabled-toggled", $"Claude Desktop notification playback state changed to {_state.IsClaudeDesktopEnabled}.");
        RefreshMenuLabels();
    }

    private void ToggleAudioDucking()
    {
        _state = _state with { IsAudioDuckingEnabled = !_state.IsAudioDuckingEnabled };
        PersistState("state-audio-ducking-toggled", $"Audio ducking state changed to {_state.IsAudioDuckingEnabled}.");
        if (!_state.IsAudioDuckingEnabled)
        {
            _audioDuckingCoordinator.RestoreNow();
        }

        RefreshMenuLabels();
    }

    private void ToggleStartup()
    {
        if (_startupEntryManager.IsEnabled())
        {
            _startupEntryManager.Disable();
            _logger.Log(LogLevel.Info, "startup-disabled", "Removed current-user startup entry.");
            ShowStatusBalloon("Codex Notification Booster", "已关闭开机自启动。");
        }
        else
        {
            _startupEntryManager.Enable();
            _logger.Log(LogLevel.Info, "startup-enabled", "Created current-user startup entry.");
            ShowStatusBalloon("Codex Notification Booster", "已开启开机自启动。");
        }

        RefreshMenuLabels();
    }

    private void TestSound(TargetNotificationApp targetApp)
    {
        _notificationPlayback.Play(CreateTestNotificationRecord(targetApp), CreateTestMatchDecision(targetApp));

        var appName = targetApp == TargetNotificationApp.Codex ? "Codex" : "Claude Desktop";
        _logger.Log(LogLevel.Info, "test-sound-played", "Tray menu test sound playback completed.", new Dictionary<string, object?>
        {
            ["targetApp"] = targetApp.ToString()
        });
        ShowStatusBalloon("Codex Notification Booster", $"{appName} 测试提示音已播放。");
    }

    private static NotificationRecord CreateTestNotificationRecord(TargetNotificationApp targetApp)
    {
        return new NotificationRecord
        {
            CapturedAt = DateTimeOffset.UtcNow,
            AppDisplayName = targetApp == TargetNotificationApp.Codex ? "Codex" : "Claude Desktop"
        };
    }

    private static NotificationMatchDecision CreateTestMatchDecision(TargetNotificationApp targetApp)
    {
        return new NotificationMatchDecision(
            Matched: true,
            Reason: "tray menu test sound playback",
            MatchedRule: "tray-test-sound",
            TargetApp: targetApp);
    }

    private void RestoreVolume()
    {
        _audioDuckingCoordinator.RestoreNow();
        _logger.Log(LogLevel.Info, "restore-volume-menu", "Restore volume menu action invoked.");
        ShowStatusBalloon("Codex Notification Booster", "已尝试恢复音量。");
    }

    private void OpenLogsDirectory()
    {
        _paths.EnsureDirectories();

        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.RootDirectory,
            UseShellExecute = true
        });

        _logger.Log(LogLevel.Info, "log-directory-opened", "Opened local application directory.");
    }

    private void ExitRequested()
    {
        RunMenuAction("tray-exit", () =>
        {
            _logger.Log(LogLevel.Info, "tray-exit", "Tray helper exiting normally.");
            _audioDuckingCoordinator.RestoreNow();
            _shutdownTokenSource.Cancel();
            _triggerFileWatcher.Dispose();
            _pollTimer.Stop();
            _duckingTimer.Stop();
            _pollTimer.Dispose();
            _duckingTimer.Dispose();
            _shutdownTokenSource.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            ExitThread();
        });
    }

    private void PersistState(string eventCode, string message)
    {
        _stateStore.Save(_state);
        _logger.Log(LogLevel.Info, eventCode, message, new Dictionary<string, object?>
        {
            ["isEnabled"] = _state.IsEnabled,
            ["isCodexEnabled"] = _state.IsCodexEnabled,
            ["isClaudeDesktopEnabled"] = _state.IsClaudeDesktopEnabled,
            ["isAudioDuckingEnabled"] = _state.IsAudioDuckingEnabled
        });
    }

    private void SaveState(AppState state)
    {
        _state = state;
        _stateStore.Save(_state);
    }

    private void HandleAudioDuckingEvent(AudioDuckingEvent item)
    {
        _logger.Log(item.Level, item.Code, item.Message, item.Metadata);
        if (item.UserVisible && !string.IsNullOrWhiteSpace(item.UserMessage))
        {
            PostToUi(() => ShowStatusBalloon("Codex Notification Booster", item.UserMessage));
        }
    }

    private void RefreshMenuLabels()
    {
        _enabledMenuItem.Text = _state.IsEnabled ? "已启用" : "已暂停";
        _codexEnabledMenuItem.Text = _state.IsCodexEnabled ? "Codex 提醒：开" : "Codex 提醒：关";
        _claudeDesktopEnabledMenuItem.Text = _state.IsClaudeDesktopEnabled ? "Claude Desktop 提醒：开" : "Claude Desktop 提醒：关";
        _audioDuckingMenuItem.Text = _state.IsAudioDuckingEnabled ? "音频闪避：开" : "音频闪避：关";
        _startupMenuItem.Text = _startupEntryManager.IsEnabled() ? "开机自启动：开" : "开机自启动：关";

        _enabledMenuItem.Click -= EnabledMenuClicked;
        _codexEnabledMenuItem.Click -= CodexEnabledMenuClicked;
        _claudeDesktopEnabledMenuItem.Click -= ClaudeDesktopEnabledMenuClicked;
        _audioDuckingMenuItem.Click -= AudioDuckingMenuClicked;
        _startupMenuItem.Click -= StartupMenuClicked;
        _enabledMenuItem.Click += EnabledMenuClicked;
        _codexEnabledMenuItem.Click += CodexEnabledMenuClicked;
        _claudeDesktopEnabledMenuItem.Click += ClaudeDesktopEnabledMenuClicked;
        _audioDuckingMenuItem.Click += AudioDuckingMenuClicked;
        _startupMenuItem.Click += StartupMenuClicked;
    }

    private bool IsPlaybackEnabled(NotificationMatchDecision decision)
    {
        if (!_state.IsEnabled)
        {
            return false;
        }

        return decision.TargetApp switch
        {
            TargetNotificationApp.Codex => _state.IsCodexEnabled,
            TargetNotificationApp.ClaudeDesktop => _state.IsClaudeDesktopEnabled,
            _ => false
        };
    }

    private void EnabledMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-enabled-menu", ToggleEnabled);

    private void CodexEnabledMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-codex-enabled-menu", ToggleCodexEnabled);

    private void ClaudeDesktopEnabledMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-claude-desktop-enabled-menu", ToggleClaudeDesktopEnabled);

    private void AudioDuckingMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-audio-ducking-menu", ToggleAudioDucking);

    private void StartupMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-startup-menu", ToggleStartup);

    private void RunMenuAction(string eventCode, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, eventCode, "Recoverable tray action failure.", new Dictionary<string, object?>
            {
                ["exceptionType"] = ex.GetType().FullName,
                ["error"] = ex.Message
            });

            ShowStatusBalloon("Codex Notification Booster", $"操作失败：{ex.Message}");
        }
    }

    private async Task PollNotificationsAsync()
    {
        if (_isPolling || _shutdownTokenSource.IsCancellationRequested)
        {
            return;
        }

        _isPolling = true;
        try
        {
            await _notificationListenerService.PollOnceAsync(_shutdownTokenSource.Token);
        }
        catch (OperationCanceledException) when (_shutdownTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            _isPolling = false;
        }
    }

    private void PostToUi(Action action)
    {
        if (_notifyIcon.ContextMenuStrip is { IsDisposed: false } menu && menu.InvokeRequired)
        {
            menu.BeginInvoke(action);
            return;
        }

        action();
    }

    private void ShowTrayMenu()
    {
        if (_notifyIcon.ContextMenuStrip is not { IsDisposed: false } menu)
        {
            return;
        }

        menu.Show(Cursor.Position);
    }

    private void ShowStatusBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }
}
