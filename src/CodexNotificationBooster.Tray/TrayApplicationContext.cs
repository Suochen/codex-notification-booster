using System.Diagnostics;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppPaths _paths = AppPaths.CreateDefault();
    private readonly AppStateStore _stateStore;
    private readonly HelperSoundAssetProvider _soundAssetProvider;
    private readonly RedactingFileLogger _logger;
    private readonly AudioDuckingCoordinator _audioDuckingCoordinator;
    private readonly NotificationListenerService _notificationListenerService;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _duckingTimer;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _audioDuckingMenuItem;
    private readonly ToolStripMenuItem _testSoundMenuItem;
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
        _audioDuckingCoordinator = new AudioDuckingCoordinator(
            new WindowsAudioSessionVolumeController(),
            () => _state,
            SaveState,
            Process.GetCurrentProcess().Id,
            HandleAudioDuckingEvent);
        _notificationListenerService = new NotificationListenerService(
            new WindowsNotificationSource(_logger),
            new AudioDuckingNotificationPlayback(
                new HelperSoundPlayback(_soundAssetProvider),
                _audioDuckingCoordinator),
            new VisibleNotificationProcessor(),
            _logger,
            () => _state.IsEnabled,
            (title, message) => PostToUi(() => ShowStatusBalloon(title, message)));

        _paths.EnsureDirectories();
        _soundAssetProvider.EnsurePresent();
        _logger.ApplyRetention();
        _logger.Log(LogLevel.Info, "tray-started", "Tray helper shell started.");

        _enabledMenuItem = new ToolStripMenuItem();
        _audioDuckingMenuItem = new ToolStripMenuItem();
        _testSoundMenuItem = new ToolStripMenuItem("测试提示音", null, (_, _) => RunMenuAction("test-sound-menu", TestSound));
        _restoreVolumeMenuItem = new ToolStripMenuItem("恢复音量", null, (_, _) => RunMenuAction("restore-volume-menu", RestoreVolume));
        _openLogsMenuItem = new ToolStripMenuItem("打开日志目录", null, (_, _) => RunMenuAction("open-logs-menu", OpenLogsDirectory));
        _exitMenuItem = new ToolStripMenuItem("退出", null, (_, _) => ExitRequested());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true,
            Text = "Codex Notification Booster",
            ContextMenuStrip = new ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _enabledMenuItem,
            _audioDuckingMenuItem,
            new ToolStripSeparator(),
            _testSoundMenuItem,
            _restoreVolumeMenuItem,
            _openLogsMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem
        ]);

        _notifyIcon.DoubleClick += (_, _) => ShowStatusBalloon("Codex Notification Booster", "托盘助手正在运行。");

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

        _audioDuckingCoordinator.RecoverPriorState();
        _ = PollNotificationsAsync();
    }

    private void ToggleEnabled()
    {
        _state = _state with { IsEnabled = !_state.IsEnabled };
        PersistState("state-enabled-toggled", $"Enabled state changed to {_state.IsEnabled}.");
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

    private void TestSound()
    {
        var soundPath = _soundAssetProvider.EnsurePresent();
        using var player = new SoundPlayer(soundPath);
        player.Load();
        player.PlaySync();

        _logger.Log(LogLevel.Info, "test-sound-played", "Tray menu test sound playback completed.");
        ShowStatusBalloon("Codex Notification Booster", "测试提示音已播放。");
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
        _audioDuckingMenuItem.Text = _state.IsAudioDuckingEnabled ? "音频闪避：开" : "音频闪避：关";

        _enabledMenuItem.Click -= EnabledMenuClicked;
        _audioDuckingMenuItem.Click -= AudioDuckingMenuClicked;
        _enabledMenuItem.Click += EnabledMenuClicked;
        _audioDuckingMenuItem.Click += AudioDuckingMenuClicked;
    }

    private void EnabledMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-enabled-menu", ToggleEnabled);

    private void AudioDuckingMenuClicked(object? sender, EventArgs e) => RunMenuAction("toggle-audio-ducking-menu", ToggleAudioDucking);

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

    private void ShowStatusBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }
}
