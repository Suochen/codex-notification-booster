[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$matcherPath = Join-Path -Path $PSScriptRoot -ChildPath 'notification-metadata-matcher.ps1'
$fixtureRoot = Join-Path -Path $repoRoot -ChildPath 'tests\fixtures\notification-metadata'

if (-not (Test-Path -LiteralPath $matcherPath)) {
    throw "Matcher script not found: $matcherPath"
}

$tokens = $null
$parseErrors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile($matcherPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    $parseErrors | Format-List *
    throw "PowerShell parser found $($parseErrors.Count) error(s)."
}

. $matcherPath

function Read-Fixture {
    param([string]$Name)

    $path = Join-Path -Path $fixtureRoot -ChildPath $Name
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-Decision {
    param(
        [string]$FixtureName,
        [bool]$ExpectedMatched
    )

    $record = Read-Fixture -Name $FixtureName
    $decision = Test-CodexNotificationMetadata -Record $record

    if ($null -eq $decision) {
        throw "$FixtureName returned no decision."
    }
    if ($decision.matched -ne $ExpectedMatched) {
        throw "$FixtureName expected matched=$ExpectedMatched but got matched=$($decision.matched), reason=$($decision.reason), matchedRule=$($decision.matchedRule)."
    }
    if ([string]::IsNullOrWhiteSpace([string]$decision.reason) -and [string]::IsNullOrWhiteSpace([string]$decision.matchedRule)) {
        throw "$FixtureName decision did not include a reason or matched rule."
    }
}

Assert-Decision -FixtureName 'codex-general.json' -ExpectedMatched $true
Assert-Decision -FixtureName 'codex-no-completion-text.json' -ExpectedMatched $true
Assert-Decision -FixtureName 'qq.json' -ExpectedMatched $false
Assert-Decision -FixtureName 'topnotify.json' -ExpectedMatched $false
Assert-Decision -FixtureName 'edge.json' -ExpectedMatched $false

Write-Host "notification matcher checks passed from $repoRoot"
