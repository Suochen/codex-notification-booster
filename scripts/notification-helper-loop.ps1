<#
.SYNOPSIS
Runs the foreground Codex notification helper loop with local diagnostics.

.DESCRIPTION
This helper polls visible Windows toast notifications, deduplicates already
processed records, reuses the Codex metadata matcher and helper-owned playback
trigger, and writes local JSONL diagnostics. It is intentionally foreground-only
and does not package, autostart, mutate notifications, suppress original audio,
or change any Windows/application volume setting.
#>
[CmdletBinding(DefaultParameterSetName = 'Duration')]
param(
    [string]$SoundPath,

    [ValidateRange(1, 60)]
    [int]$PollSeconds = 2,

    [Parameter(ParameterSetName = 'Duration')]
    [ValidateRange(1, 1440)]
    [int]$DurationMinutes = 10,

    [Parameter(ParameterSetName = 'Once')]
    [switch]$Once,

    [string]$LogPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:HelperVersion = '0.1.0'
$probePath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-metadata-probe.ps1'
$triggerPath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-playback-trigger.ps1'
$adapterPath = Join-Path -Path $PSScriptRoot -ChildPath 'play-notification-sound.ps1'

. $probePath
. $triggerPath

function Get-DefaultDiagnosticsPath {
    $basePath = $env:LOCALAPPDATA
    if ([string]::IsNullOrWhiteSpace($basePath)) {
        $basePath = [System.IO.Path]::GetTempPath()
    }

    Join-Path -Path $basePath -ChildPath 'CodexNotificationBooster\helper-diagnostics.jsonl'
}

function Resolve-DiagnosticsLogPath {
    param([string]$ConfiguredPath)

    if ([string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        return Get-DefaultDiagnosticsPath
    }

    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ConfiguredPath)
}

function Write-HelperStatus {
    param([string]$Message)
    Write-Host "[notification-helper] $Message"
}

function New-RedactedNotificationMetadata {
    param($Record)

    if ($null -eq $Record) {
        return $null
    }

    [ordered]@{
        dedupKey = Get-MetadataValue -Record $Record -PropertyName 'dedupKey'
        notificationId = Get-MetadataValue -Record $Record -PropertyName 'notificationId'
        creationTime = Get-MetadataValue -Record $Record -PropertyName 'creationTime'
        appDisplayName = Get-MetadataValue -Record $Record -PropertyName 'appDisplayName'
        appUserModelId = Get-MetadataValue -Record $Record -PropertyName 'appUserModelId'
        appId = Get-MetadataValue -Record $Record -PropertyName 'appId'
        packageFamilyName = Get-MetadataValue -Record $Record -PropertyName 'packageFamilyName'
        packageFullName = Get-MetadataValue -Record $Record -PropertyName 'packageFullName'
        rawXmlSha256 = Get-MetadataValue -Record $Record -PropertyName 'rawXmlSha256'
    }
}

function New-HelperDiagnosticEvent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Code,

        [string]$Message,

        [ValidateSet('info', 'warning', 'error')]
        [string]$Level = 'info',

        $Record,

        $Result,

        [hashtable]$Data
    )

    $event = [ordered]@{
        schemaVersion = 1
        helperVersion = $script:HelperVersion
        timestamp = [System.DateTimeOffset]::UtcNow.ToString('o')
        level = $Level
        code = $Code
        message = $Message
    }

    if ($null -ne $Record) {
        $event['notification'] = New-RedactedNotificationMetadata -Record $Record
    }

    if ($null -ne $Result) {
        $event['playback'] = [ordered]@{
            status = $Result.status
            playbackRequested = $Result.playbackRequested
            diagnosticCode = $Result.diagnosticCode
            diagnosticMessage = $Result.diagnosticMessage
            matched = $Result.matchDecision.matched
            matchedRule = $Result.matchDecision.matchedRule
        }
    }

    if ($null -ne $Data) {
        foreach ($key in $Data.Keys) {
            $event[$key] = $Data[$key]
        }
    }

    [pscustomobject]$event
}

function Write-HelperDiagnosticEvent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        $Event
    )

    Append-JsonLine -Path $Path -JsonLine ($Event | ConvertTo-Json -Depth 24 -Compress)
}

function Get-RecordDedupKey {
    param($Record)

    $existingKey = Get-MetadataValue -Record $Record -PropertyName 'dedupKey'
    if (-not [string]::IsNullOrWhiteSpace([string]$existingKey)) {
        return [string]$existingKey
    }

    return Get-NotificationDedupKey -Record $Record
}

function Set-RecordDedupKey {
    param(
        $Record,
        [string]$DedupKey
    )

    if ($Record -is [System.Collections.IDictionary]) {
        $Record['dedupKey'] = $DedupKey
        return $Record
    }

    $property = $Record.PSObject.Properties['dedupKey']
    if ($null -eq $property) {
        $Record | Add-Member -NotePropertyName dedupKey -NotePropertyValue $DedupKey
    }
    else {
        $property.Value = $DedupKey
    }

    return $Record
}

function Invoke-NotificationHelperLoop {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$NotificationProvider,

        [Parameter(Mandatory = $true)]
        [scriptblock]$PlaybackInvoker,

        [Parameter(Mandatory = $true)]
        $Config,

        [Parameter(Mandatory = $true)]
        [string]$LogPath,

        [ValidateRange(1, 60)]
        [int]$PollSeconds = 2,

        [ValidateRange(1, 1440)]
        [int]$DurationMinutes = 10,

        [int]$MaxPolls = 0
    )

    $resolvedLogPath = Resolve-DiagnosticsLogPath -ConfiguredPath $LogPath
    $directory = Split-Path -Parent $resolvedLogPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $configDecision = Test-NotificationPlaybackConfig -Config $Config
    Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
            -Code 'helper-started' `
            -Message 'Foreground notification helper loop started.' `
            -Data @{ pollSeconds = $PollSeconds; durationMinutes = $DurationMinutes; maxPolls = $MaxPolls })
    Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
            -Code 'log-path-ready' `
            -Message "Writing local diagnostics to $resolvedLogPath." `
            -Data @{ logPath = $resolvedLogPath })
    Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
            -Code $configDecision.code `
            -Message $configDecision.message `
            -Level $(if ($configDecision.valid) { 'info' } else { 'error' }) `
            -Data @{ soundPath = $configDecision.soundPath })

    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    $deadline = [System.DateTimeOffset]::UtcNow.AddMinutes($DurationMinutes)
    $pollCount = 0
    $matched = 0
    $ignored = 0
    $playbackRequests = 0
    $duplicatesSkipped = 0
    $errors = 0

    do {
        $pollCount += 1
        try {
            $notifications = @(& $NotificationProvider)
            foreach ($record in @($notifications)) {
                $dedupKey = Get-RecordDedupKey -Record $record
                $record = Set-RecordDedupKey -Record $record -DedupKey $dedupKey

                if (-not $seen.Add($dedupKey)) {
                    $duplicatesSkipped += 1
                    Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
                            -Code 'duplicate-notification-skipped' `
                            -Message 'Skipped repeated visible notification with an already processed dedup key.' `
                            -Record $record)
                    continue
                }

                $result = Invoke-NotificationPlaybackTrigger `
                    -Record $record `
                    -Config $Config `
                    -PlaybackInvoker $PlaybackInvoker

                if ($result.matchDecision.matched) {
                    $matched += 1
                }
                if ($result.status -eq 'ignored') {
                    $ignored += 1
                }
                if ($result.playbackRequested) {
                    $playbackRequests += 1
                }

                $eventLevel = if ($result.status -eq 'failed') { 'error' } else { 'info' }
                Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
                        -Code $result.diagnosticCode `
                        -Message $result.diagnosticMessage `
                        -Level $eventLevel `
                        -Record $record `
                        -Result $result)
            }
        }
        catch {
            $errors += 1
            Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
                    -Code 'loop-error' `
                    -Message $_.Exception.Message `
                    -Level 'error')
        }

        if ($MaxPolls -gt 0 -and $pollCount -ge $MaxPolls) {
            break
        }

        Start-Sleep -Seconds $PollSeconds
    } while ([System.DateTimeOffset]::UtcNow -lt $deadline)

    $summary = [pscustomobject]@{
        polls = $pollCount
        matched = $matched
        ignored = $ignored
        playbackRequests = $playbackRequests
        duplicatesSkipped = $duplicatesSkipped
        errors = $errors
        logPath = $resolvedLogPath
    }

    Write-HelperDiagnosticEvent -Path $resolvedLogPath -Event (New-HelperDiagnosticEvent `
            -Code 'helper-stopped' `
            -Message 'Foreground notification helper loop stopped.' `
            -Data @{ summary = $summary })

    return $summary
}

function New-LiveNotificationProvider {
    param($Listener)

    {
        $notifications = Get-CurrentToastNotifications -Listener $Listener
        foreach ($notification in @($notifications)) {
            $record = ConvertTo-NotificationRecord -UserNotification $notification
            $dedupKey = Get-NotificationDedupKey -Record $record
            $record['dedupKey'] = $dedupKey
            [pscustomobject]$record
        }
    }.GetNewClosure()
}

function Initialize-LiveNotificationProvider {
    param([string]$DiagnosticsPath)

    try {
        Initialize-WindowsNotificationTypes
        $listener = [Windows.UI.Notifications.Management.UserNotificationListener, Windows.UI.Notifications, ContentType = WindowsRuntime]::Current
    }
    catch {
        Write-HelperDiagnosticEvent -Path $DiagnosticsPath -Event (New-HelperDiagnosticEvent `
                -Code 'listener-api-unavailable' `
                -Message "Windows notification listener APIs are unavailable from this host. Run directly on Windows 11 with Windows PowerShell. Original error: $($_.Exception.Message)" `
                -Level 'error')
        throw
    }

    try {
        $status = Get-NotificationListenerAccess -Listener $listener
        Write-HelperDiagnosticEvent -Path $DiagnosticsPath -Event (New-HelperDiagnosticEvent `
                -Code 'listener-access-status' `
                -Message "Windows notification listener access status: $status." `
                -Level $(if ([string]$status -eq 'Allowed') { 'info' } else { 'error' }) `
                -Data @{ accessStatus = [string]$status })
        Assert-NotificationAccessAllowed -Status $status
    }
    catch {
        Write-HelperDiagnosticEvent -Path $DiagnosticsPath -Event (New-HelperDiagnosticEvent `
                -Code 'listener-access-unavailable' `
                -Message "Could not obtain Windows notification listener access. Enable notification access for the PowerShell host in Windows Settings, then run the helper again. Original error: $($_.Exception.Message)" `
                -Level 'error')
        throw
    }

    New-LiveNotificationProvider -Listener $listener
}

function Invoke-ForegroundNotificationHelper {
    param(
        [string]$ConfiguredSoundPath,
        [string]$ConfiguredLogPath,
        [int]$ConfiguredPollSeconds,
        [int]$ConfiguredDurationMinutes,
        [switch]$SinglePass
    )

    $resolvedLogPath = Resolve-DiagnosticsLogPath -ConfiguredPath $ConfiguredLogPath
    Write-HelperStatus "writing local diagnostics to $resolvedLogPath"
    Write-HelperStatus 'diagnostics omit raw notification body/text/XML by default'

    $configDecision = Test-NotificationPlaybackConfig -Config @{ soundPath = $ConfiguredSoundPath }
    Write-HelperStatus "playback config status: $($configDecision.code) - $($configDecision.message)"

    $provider = Initialize-LiveNotificationProvider -DiagnosticsPath $resolvedLogPath
    $playback = {
        param([string]$SoundPath)
        & $adapterPath -SoundPath $SoundPath
    }.GetNewClosure()

    $maxPolls = if ($SinglePass) { 1 } else { 0 }
    $summary = Invoke-NotificationHelperLoop `
        -NotificationProvider $provider `
        -PlaybackInvoker $playback `
        -Config @{ soundPath = $ConfiguredSoundPath } `
        -LogPath $resolvedLogPath `
        -PollSeconds $ConfiguredPollSeconds `
        -DurationMinutes $ConfiguredDurationMinutes `
        -MaxPolls $maxPolls

    Write-HelperStatus "complete; polls=$($summary.polls), matched=$($summary.matched), playbackRequests=$($summary.playbackRequests), duplicatesSkipped=$($summary.duplicatesSkipped), ignored=$($summary.ignored), errors=$($summary.errors)"
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($SoundPath)) {
        throw 'SoundPath is required. Pass -SoundPath with a local .wav file.'
    }

    Invoke-ForegroundNotificationHelper `
        -ConfiguredSoundPath $SoundPath `
        -ConfiguredLogPath $LogPath `
        -ConfiguredPollSeconds $PollSeconds `
        -ConfiguredDurationMinutes $DurationMinutes `
        -SinglePass:$Once
}
