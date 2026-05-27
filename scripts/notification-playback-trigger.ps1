<#
.SYNOPSIS
Decides whether notification metadata should request helper-owned sound playback.

.DESCRIPTION
This module consumes plain notification metadata and a playback configuration,
reuses the Codex notification matcher, and requests playback only for matched
Codex metadata. It does not call Windows notification APIs, change any volume
setting, suppress notification audio, or mutate notification state.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$matcherPath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-metadata-matcher.ps1'
. $matcherPath

$script:SupportedSoundExtensions = @('.wav')

function New-PlaybackTriggerResult {
    param(
        [string]$Status,
        [bool]$PlaybackRequested,
        [string]$DiagnosticCode,
        [string]$DiagnosticMessage,
        [string]$SoundPath,
        $MatchDecision
    )

    [pscustomobject]@{
        status = $Status
        playbackRequested = $PlaybackRequested
        diagnosticCode = $DiagnosticCode
        diagnosticMessage = $DiagnosticMessage
        soundPath = $SoundPath
        matchDecision = $MatchDecision
    }
}

function Get-PlaybackConfigValue {
    param(
        $Config,
        [string]$PropertyName
    )

    if ($null -eq $Config) {
        return $null
    }

    if ($Config -is [System.Collections.IDictionary]) {
        if ($Config.Contains($PropertyName)) {
            return $Config[$PropertyName]
        }
        return $null
    }

    $property = $Config.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Resolve-PlaybackSoundPath {
    param([string]$SoundPath)

    if ([string]::IsNullOrWhiteSpace($SoundPath)) {
        return $null
    }

    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SoundPath)
}

function Test-NotificationPlaybackConfig {
    param(
        [Parameter(Mandatory = $true)]
        $Config
    )

    $configuredSoundPath = [string](Get-PlaybackConfigValue -Config $Config -PropertyName 'soundPath')
    if ([string]::IsNullOrWhiteSpace($configuredSoundPath)) {
        return [pscustomobject]@{
            valid = $false
            code = 'invalid-config'
            message = 'Playback config must include a non-empty soundPath.'
            soundPath = $null
        }
    }

    $resolvedSoundPath = Resolve-PlaybackSoundPath -SoundPath $configuredSoundPath
    if (-not (Test-Path -LiteralPath $resolvedSoundPath -PathType Leaf)) {
        return [pscustomobject]@{
            valid = $false
            code = 'missing-sound-file'
            message = "Configured sound file does not exist: $resolvedSoundPath"
            soundPath = $resolvedSoundPath
        }
    }

    $extension = [System.IO.Path]::GetExtension($resolvedSoundPath).ToLowerInvariant()
    if ($script:SupportedSoundExtensions -notcontains $extension) {
        return [pscustomobject]@{
            valid = $false
            code = 'unsupported-sound-file'
            message = "Unsupported sound file extension '$extension'. Supported extensions: $($script:SupportedSoundExtensions -join ', ')."
            soundPath = $resolvedSoundPath
        }
    }

    return [pscustomobject]@{
        valid = $true
        code = 'config-valid'
        message = 'Playback config is valid.'
        soundPath = $resolvedSoundPath
    }
}

function Invoke-NotificationPlaybackTrigger {
    param(
        [Parameter(Mandatory = $true)]
        $Record,

        [Parameter(Mandatory = $true)]
        $Config,

        [Parameter(Mandatory = $true)]
        [scriptblock]$PlaybackInvoker
    )

    $matchDecision = Test-CodexNotificationMetadata -Record $Record
    if (-not $matchDecision.matched) {
        return New-PlaybackTriggerResult `
            -Status 'ignored' `
            -PlaybackRequested $false `
            -DiagnosticCode 'ignored-notification' `
            -DiagnosticMessage $matchDecision.reason `
            -SoundPath $null `
            -MatchDecision $matchDecision
    }

    $configDecision = Test-NotificationPlaybackConfig -Config $Config
    if (-not $configDecision.valid) {
        return New-PlaybackTriggerResult `
            -Status 'failed' `
            -PlaybackRequested $false `
            -DiagnosticCode $configDecision.code `
            -DiagnosticMessage $configDecision.message `
            -SoundPath $configDecision.soundPath `
            -MatchDecision $matchDecision
    }

    try {
        & $PlaybackInvoker -SoundPath $configDecision.soundPath
    }
    catch {
        return New-PlaybackTriggerResult `
            -Status 'failed' `
            -PlaybackRequested $true `
            -DiagnosticCode 'playback-failure' `
            -DiagnosticMessage $_.Exception.Message `
            -SoundPath $configDecision.soundPath `
            -MatchDecision $matchDecision
    }

    return New-PlaybackTriggerResult `
        -Status 'played' `
        -PlaybackRequested $true `
        -DiagnosticCode 'matched-playback-requested' `
        -DiagnosticMessage 'Matched Codex notification requested helper-owned playback.' `
        -SoundPath $configDecision.soundPath `
        -MatchDecision $matchDecision
}
