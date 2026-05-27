[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$probePath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-metadata-probe.ps1'
$tokens = $null
$parseErrors = $null

[void][System.Management.Automation.Language.Parser]::ParseFile($probePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    $parseErrors | Format-List *
    throw "PowerShell parser found $($parseErrors.Count) error(s)."
}

$source = Get-Content -LiteralPath $probePath -Raw
$forbiddenPatterns = @(
    'RemoveNotification',
    'ClearNotifications',
    'ToastNotifier',
    'AudioEndpointVolume',
    'SystemSounds',
    'Media.SoundPlayer',
    'SetAudioEndpoint',
    'SendKeys'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($source -match [regex]::Escape($pattern)) {
        throw "Forbidden out-of-scope API/text found in probe: $pattern"
    }
}

$global:LASTEXITCODE = 0
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $probePath -SelfTest
$selfTestExitCode = $global:LASTEXITCODE
if ($selfTestExitCode -ne 0) {
    throw "Probe self-test failed with exit code $selfTestExitCode."
}

Write-Host "notification probe checks passed from $repoRoot"
