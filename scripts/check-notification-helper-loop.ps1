[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$helperPath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-helper-loop.ps1'
$adapterPath = Join-Path -Path $PSScriptRoot -ChildPath 'play-notification-sound.ps1'
$fixtureRoot = Join-Path -Path $repoRoot -ChildPath 'tests\fixtures\notification-metadata'

foreach ($scriptPath in @($helperPath, $adapterPath)) {
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }

    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) {
        $parseErrors | Format-List *
        throw "PowerShell parser found $($parseErrors.Count) error(s) in $scriptPath."
    }
}

. $helperPath

function Read-Fixture {
    param([string]$Name)

    $path = Join-Path -Path $fixtureRoot -ChildPath $Name
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function New-MinimalWavFile {
    param([string]$Path)

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    $bytes = [byte[]]@(
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
        0x57, 0x41, 0x56, 0x45, 0x66, 0x6d, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
        0x40, 0x1f, 0x00, 0x00, 0x80, 0x3e, 0x00, 0x00,
        0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
        0x00, 0x00, 0x00, 0x00
    )
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

function Get-JsonlRecords {
    param([string]$Path)

    Get-Content -LiteralPath $Path | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    } | ForEach-Object {
        $_ | ConvertFrom-Json
    }
}

function Assert-EventCode {
    param(
        $Records,
        [string]$Code
    )

    if (-not @($Records | Where-Object { $_.code -eq $Code })) {
        throw "Expected diagnostic event code '$Code'."
    }
}

function Assert-EventCodeCount {
    param(
        $Records,
        [string]$Code,
        [int]$ExpectedCount
    )

    $actualCount = @($Records | Where-Object { $_.code -eq $Code }).Count
    if ($actualCount -ne $ExpectedCount) {
        throw "Expected diagnostic event code '$Code' count $ExpectedCount, got $actualCount."
    }
}

function Copy-RecordWithVolatileNotificationFields {
    param(
        $Record,
        [string]$NotificationId,
        [string]$CreationTime,
        [string]$RawXmlSha256
    )

    $copy = [ordered]@{}
    foreach ($property in $Record.PSObject.Properties) {
        if ($property.Name -ne 'dedupKey') {
            $copy[$property.Name] = $property.Value
        }
    }

    $copy['notificationId'] = $NotificationId
    $copy['creationTime'] = $CreationTime
    $copy['rawXmlSha256'] = $RawXmlSha256

    [pscustomobject]$copy
}

$tempRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('codex-notification-booster-helper-loop-check-' + [guid]::NewGuid().ToString('n'))
$wavPath = Join-Path -Path $tempRoot -ChildPath 'custom.wav'
$logPath = Join-Path -Path $tempRoot -ChildPath 'helper-diagnostics.jsonl'
$playbackCalls = New-Object System.Collections.Generic.List[string]

try {
    New-MinimalWavFile -Path $wavPath

    $startupBaselineCodexRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -NotificationId 'volatile-live-id-1' `
        -CreationTime '2026-05-27T00:00:01.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-1'
    $startupBaselineRepeatedCodexRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -NotificationId 'volatile-live-id-2' `
        -CreationTime '2026-05-27T00:00:02.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-2'
    $postStartCodexRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-no-completion-text.json') `
        -NotificationId 'volatile-live-id-4' `
        -CreationTime '2026-05-27T00:00:04.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-4'
    $postStartCodexSiblingRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-no-completion-text.json') `
        -NotificationId 'volatile-live-id-5' `
        -CreationTime '2026-05-27T00:00:05.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-5'
    $repeatedPostStartCodexRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-no-completion-text.json') `
        -NotificationId 'volatile-live-id-6' `
        -CreationTime '2026-05-27T00:00:06.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-6'
    $repeatedPostStartCodexSiblingRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-no-completion-text.json') `
        -NotificationId 'volatile-live-id-7' `
        -CreationTime '2026-05-27T00:00:07.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-7'
    $reappearedCodexRecord = Copy-RecordWithVolatileNotificationFields `
        -Record (Read-Fixture -Name 'codex-no-completion-text.json') `
        -NotificationId 'volatile-live-id-3' `
        -CreationTime '2026-05-27T00:00:03.0000000Z' `
        -RawXmlSha256 'volatile-live-xml-hash-3'
    $nonCodexRecord = Read-Fixture -Name 'edge.json'
    $nonCodexRecord | Add-Member -NotePropertyName dedupKey -NotePropertyValue 'edge-dedup-1' -Force

    $polls = @(
        @($startupBaselineCodexRecord, $startupBaselineRepeatedCodexRecord, $nonCodexRecord),
        @($startupBaselineCodexRecord, $startupBaselineRepeatedCodexRecord, $postStartCodexRecord, $postStartCodexSiblingRecord),
        @($repeatedPostStartCodexRecord, $repeatedPostStartCodexSiblingRecord),
        @(),
        @($reappearedCodexRecord)
    )
    $providerState = [pscustomobject]@{
        pollIndex = 0
    }
    $fakeProvider = {
        if ($providerState.pollIndex -ge $polls.Count) {
            return @()
        }

        $result = $polls[$providerState.pollIndex]
        $providerState.pollIndex += 1
        return $result
    }.GetNewClosure()

    $fakePlayback = {
        param([string]$SoundPath)
        [void]$playbackCalls.Add($SoundPath)
    }.GetNewClosure()

    $summary = Invoke-NotificationHelperLoop `
        -NotificationProvider $fakeProvider `
        -PlaybackInvoker $fakePlayback `
        -Config @{ soundPath = $wavPath } `
        -LogPath $logPath `
        -PollSeconds 1 `
        -MaxPolls 5

    if ($summary.playbackRequests -ne 3) {
        throw "Expected three playback requests, got $($summary.playbackRequests)."
    }
    if ($playbackCalls.Count -ne 3) {
        throw "Expected three fake playback calls, got $($playbackCalls.Count)."
    }
    if ($summary.duplicatesSkipped -ne 4) {
        throw "Expected four duplicate skips, got $($summary.duplicatesSkipped)."
    }
    if ($summary.ignored -ne 0) {
        throw "Expected zero ignored notifications because the non-Codex startup record should be baselined, got $($summary.ignored)."
    }

    $records = @(Get-JsonlRecords -Path $logPath)
    foreach ($code in @(
        'helper-started',
        'log-path-ready',
        'config-valid',
        'startup-baseline-notification-skipped',
        'helper-stopped'
    )) {
        Assert-EventCode -Records $records -Code $code
    }
    Assert-EventCodeCount -Records $records -Code 'startup-baseline-notification-skipped' -ExpectedCount 3
    Assert-EventCodeCount -Records $records -Code 'matched-playback-requested' -ExpectedCount 3
    Assert-EventCodeCount -Records $records -Code 'duplicate-notification-skipped' -ExpectedCount 4
    Assert-EventCodeCount -Records $records -Code 'ignored-notification' -ExpectedCount 0

    $diagnosticJson = Get-Content -LiteralPath $logPath -Raw
    foreach ($forbidden in @('"body"', '"rawXml"', '"textLines"', '"title"')) {
        if ($diagnosticJson -match [regex]::Escape($forbidden)) {
            throw "Diagnostics should not include raw notification content field $forbidden by default."
        }
    }

    $failureLogPath = Join-Path -Path $tempRoot -ChildPath 'helper-failure-diagnostics.jsonl'
    $failingCodexRecord = Read-Fixture -Name 'codex-general.json'
    $failingCodexRecord | Add-Member -NotePropertyName dedupKey -NotePropertyValue 'codex-dedup-failure' -Force
    $failingProviderState = [pscustomobject]@{
        pollIndex = 0
    }
    $failingProvider = {
        $result = if ($failingProviderState.pollIndex -eq 0) {
            @()
        }
        else {
            @($failingCodexRecord)
        }

        $failingProviderState.pollIndex += 1
        return $result
    }.GetNewClosure()
    $failingPlayback = {
        param([string]$SoundPath)
        throw "simulated playback failure for $SoundPath"
    }

    $failureSummary = Invoke-NotificationHelperLoop `
        -NotificationProvider $failingProvider `
        -PlaybackInvoker $failingPlayback `
        -Config @{ soundPath = $wavPath } `
        -LogPath $failureLogPath `
        -PollSeconds 1 `
        -MaxPolls 2

    if ($failureSummary.playbackRequests -ne 1) {
        throw "Expected failing playback to count one playback request, got $($failureSummary.playbackRequests)."
    }

    $failureRecords = @(Get-JsonlRecords -Path $failureLogPath)
    Assert-EventCode -Records $failureRecords -Code 'playback-failure'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "notification helper loop checks passed from $repoRoot"
