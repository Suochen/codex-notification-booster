using System.Media;
using CodexNotificationBooster.Core;

namespace CodexNotificationBooster.Tray;

internal sealed class HelperSoundPlayback : INotificationPlayback
{
    private readonly HelperSoundAssetProvider _soundAssetProvider;

    public HelperSoundPlayback(HelperSoundAssetProvider soundAssetProvider)
    {
        _soundAssetProvider = soundAssetProvider ?? throw new ArgumentNullException(nameof(soundAssetProvider));
    }

    public void Play(NotificationRecord record, NotificationMatchDecision matchDecision)
    {
        var soundPath = _soundAssetProvider.EnsurePresent(matchDecision.TargetApp);
        using var player = new SoundPlayer(soundPath);
        player.Load();
        player.PlaySync();
    }
}
