using System.Diagnostics;
using CodexNotificationBooster.Core;
using NAudio.CoreAudioApi;

namespace CodexNotificationBooster.Tray;

internal sealed class WindowsAudioSessionVolumeController : IAudioSessionVolumeController
{
    public IReadOnlyList<AudioSessionInfo> EnumeratePlaybackSessions()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        var results = new List<AudioSessionInfo>(sessions.Count);

        for (var index = 0; index < sessions.Count; index += 1)
        {
            var session = sessions[index];
            results.Add(CreateInfo(session));
        }

        return results;
    }

    public bool TryGetVolume(AudioSessionInfo session, out float volume, out string? error)
    {
        ArgumentNullException.ThrowIfNull(session);
        volume = 0f;

        var readVolume = 0f;
        var succeeded = TryWithSession(session.SessionId, control =>
        {
            readVolume = control.SimpleAudioVolume.Volume;
            return true;
        }, out error);

        volume = readVolume;
        return succeeded;
    }

    public bool TrySetVolume(string sessionId, float volume, out string? error)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            error = "Session id is empty.";
            return false;
        }

        var targetVolume = Math.Clamp(volume, 0f, 1f);
        return TryWithSession(sessionId, control =>
        {
            control.SimpleAudioVolume.Volume = targetVolume;
            return true;
        }, out error);
    }

    private static bool TryWithSession(string sessionId, Func<AudioSessionControl, bool> action, out string? error)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (var index = 0; index < sessions.Count; index += 1)
            {
                var control = sessions[index];
                if (string.Equals(ReadSessionIdentifier(control), sessionId, StringComparison.Ordinal))
                {
                    error = null;
                    return action(control);
                }
            }

            error = "Playback session was not found.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static AudioSessionInfo CreateInfo(AudioSessionControl control)
    {
        var processId = ReadProcessId(control);
        return new AudioSessionInfo(
            SessionId: ReadSessionIdentifier(control),
            ProcessName: ResolveProcessName(processId),
            ProcessId: processId == 0 ? null : (int)processId,
            DisplayName: ReadDisplayName(control),
            AppUserModelId: null);
    }

    private static string ReadSessionIdentifier(AudioSessionControl control)
    {
        try
        {
            return string.IsNullOrWhiteSpace(control.GetSessionIdentifier) ? string.Empty : control.GetSessionIdentifier;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ReadDisplayName(AudioSessionControl control)
    {
        try
        {
            return string.IsNullOrWhiteSpace(control.DisplayName) ? null : control.DisplayName;
        }
        catch
        {
            return null;
        }
    }

    private static uint ReadProcessId(AudioSessionControl control)
    {
        try
        {
            return control.GetProcessID;
        }
        catch
        {
            return 0;
        }
    }

    private static string? ResolveProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.ProcessName) ? null : process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
