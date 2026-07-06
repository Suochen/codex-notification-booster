[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$triggerPath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-playback-trigger.ps1'
$adapterPath = Join-Path -Path $PSScriptRoot -ChildPath 'play-notification-sound.ps1'
$fixtureRoot = Join-Path -Path $repoRoot -ChildPath 'tests\fixtures\notification-metadata'

foreach ($scriptPath in @($triggerPath, $adapterPath)) {
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) {
        $parseErrors | Format-List *
        throw "PowerShell parser found $($parseErrors.Count) error(s) in $scriptPath."
    }
}

. $triggerPath

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

function Assert-Result {
    param(
        $Result,
        [string]$ExpectedStatus,
        [bool]$ExpectedPlaybackRequested,
        [string]$ExpectedDiagnosticCode
    )

    if ($null -eq $Result) {
        throw 'Playback trigger returned no result.'
    }
    if ($Result.status -ne $ExpectedStatus) {
        throw "Expected status=$ExpectedStatus but got status=$($Result.status), diagnostic=$($Result.diagnosticCode)."
    }
    if ($Result.playbackRequested -ne $ExpectedPlaybackRequested) {
        throw "Expected playbackRequested=$ExpectedPlaybackRequested but got playbackRequested=$($Result.playbackRequested)."
    }
    if ($Result.diagnosticCode -ne $ExpectedDiagnosticCode) {
        throw "Expected diagnosticCode=$ExpectedDiagnosticCode but got diagnosticCode=$($Result.diagnosticCode)."
    }
    if ([string]::IsNullOrWhiteSpace([string]$Result.diagnosticMessage)) {
        throw "Result for $ExpectedDiagnosticCode did not include a diagnostic message."
    }
}

$tempRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('codex-notification-booster-playback-check-' + [guid]::NewGuid().ToString('n'))
$wavPath = Join-Path -Path $tempRoot -ChildPath 'custom.wav'
$unsupportedPath = Join-Path -Path $tempRoot -ChildPath 'custom.mp3'
$calls = New-Object System.Collections.Generic.List[string]

try {
    New-MinimalWavFile -Path $wavPath
    Set-Content -LiteralPath $unsupportedPath -Value 'not audio' -Encoding ASCII

    $fakePlayback = {
        param([string]$SoundPath)
        [void]$calls.Add($SoundPath)
    }.GetNewClosure()

    $codexResult = Invoke-NotificationPlaybackTrigger `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -Config @{ soundPath = $wavPath } `
        -PlaybackInvoker $fakePlayback

    Assert-Result `
        -Result $codexResult `
        -ExpectedStatus 'played' `
        -ExpectedPlaybackRequested $true `
        -ExpectedDiagnosticCode 'matched-playback-requested'

    if ($calls.Count -ne 1) {
        throw "Expected one fake playback call for target app metadata, got $($calls.Count)."
    }
    if ($calls[0] -ne $wavPath) {
        throw "Expected fake playback sound path '$wavPath', got '$($calls[0])'."
    }

    foreach ($fixtureName in @('qq.json', 'topnotify.json', 'edge.json')) {
        $beforeCount = $calls.Count
        $result = Invoke-NotificationPlaybackTrigger `
            -Record (Read-Fixture -Name $fixtureName) `
            -Config @{ soundPath = $wavPath } `
            -PlaybackInvoker $fakePlayback

        Assert-Result `
            -Result $result `
            -ExpectedStatus 'ignored' `
            -ExpectedPlaybackRequested $false `
            -ExpectedDiagnosticCode 'ignored-notification'

        if ($calls.Count -ne $beforeCount) {
            throw "$fixtureName should not request fake playback."
        }
    }

    $invalidConfigResult = Invoke-NotificationPlaybackTrigger `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -Config @{} `
        -PlaybackInvoker $fakePlayback
    Assert-Result `
        -Result $invalidConfigResult `
        -ExpectedStatus 'failed' `
        -ExpectedPlaybackRequested $false `
        -ExpectedDiagnosticCode 'invalid-config'

    $missingFileResult = Invoke-NotificationPlaybackTrigger `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -Config @{ soundPath = (Join-Path -Path $tempRoot -ChildPath 'missing.wav') } `
        -PlaybackInvoker $fakePlayback
    Assert-Result `
        -Result $missingFileResult `
        -ExpectedStatus 'failed' `
        -ExpectedPlaybackRequested $false `
        -ExpectedDiagnosticCode 'missing-sound-file'

    $unsupportedFileResult = Invoke-NotificationPlaybackTrigger `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -Config @{ soundPath = $unsupportedPath } `
        -PlaybackInvoker $fakePlayback
    Assert-Result `
        -Result $unsupportedFileResult `
        -ExpectedStatus 'failed' `
        -ExpectedPlaybackRequested $false `
        -ExpectedDiagnosticCode 'unsupported-sound-file'

    $failingPlayback = {
        param([string]$SoundPath)
        throw "simulated playback failure for $SoundPath"
    }
    $playbackFailureResult = Invoke-NotificationPlaybackTrigger `
        -Record (Read-Fixture -Name 'codex-general.json') `
        -Config @{ soundPath = $wavPath } `
        -PlaybackInvoker $failingPlayback
    Assert-Result `
        -Result $playbackFailureResult `
        -ExpectedStatus 'failed' `
        -ExpectedPlaybackRequested $true `
        -ExpectedDiagnosticCode 'playback-failure'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "notification playback checks passed from $repoRoot"
