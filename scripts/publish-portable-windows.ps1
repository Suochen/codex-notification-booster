[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$OutputPath,
    [string]$DotNetPath = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path -Path $repoRoot -ChildPath 'src\CodexNotificationBooster.Tray\CodexNotificationBooster.Tray.csproj'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path -Path $repoRoot -ChildPath "artifacts\portable-windows\$Configuration\$Runtime"
}

$resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)

New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null

$dotnetCommand = Get-Command -Name $DotNetPath -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw "Could not find '$DotNetPath'. Install the .NET 8 SDK or pass -DotNetPath with the full path to dotnet.exe. The published portable app does not require the SDK at runtime."
}

& $dotnetCommand.Source publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:EnableCompressionInSingleFile=false `
    --output $resolvedOutputPath

$exePath = Join-Path -Path $resolvedOutputPath -ChildPath 'CodexNotificationBooster.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected portable executable was not produced at $exePath."
}

Write-Host "Portable Windows tray build published to:"
Write-Host $resolvedOutputPath
Write-Host ""
Write-Host "Launch from Windows 11:"
Write-Host "  $exePath"
