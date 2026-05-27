<#
.SYNOPSIS
Plays a configured helper-owned notification sound on Windows.

.DESCRIPTION
This focused adapter plays a local WAV file using System.Media.SoundPlayer. It
does not inspect notifications, change Codex/system/app volume, suppress the
original notification sound, or mutate notification state.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SoundPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-AdapterSoundPath {
    param([string]$ConfiguredPath)

    if ([string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        throw 'SoundPath must be non-empty.'
    }

    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ConfiguredPath)
}

function Invoke-WindowsNotificationSoundPlayback {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedSoundPath
    )

    if (-not (Test-Path -LiteralPath $ResolvedSoundPath -PathType Leaf)) {
        throw "Sound file does not exist: $ResolvedSoundPath"
    }

    $extension = [System.IO.Path]::GetExtension($ResolvedSoundPath).ToLowerInvariant()
    if ($extension -ne '.wav') {
        throw "Unsupported sound file extension '$extension'. Use a local .wav file."
    }

    $player = [System.Media.SoundPlayer]::new($ResolvedSoundPath)
    try {
        $player.Load()
        $player.PlaySync()
    }
    finally {
        $player.Dispose()
    }
}

$resolvedSoundPath = Resolve-AdapterSoundPath -ConfiguredPath $SoundPath
Invoke-WindowsNotificationSoundPlayback -ResolvedSoundPath $resolvedSoundPath
Write-Host "[notification-playback] played helper-owned sound: $resolvedSoundPath"
