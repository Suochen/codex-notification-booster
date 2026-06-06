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
$publishOutputPath = $resolvedOutputPath
$tempPublishPath = $null

New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null

$dotnetCommand = Get-Command -Name $DotNetPath -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw "Could not find '$DotNetPath'. Install the .NET 8 SDK or pass -DotNetPath with the full path to dotnet.exe. The published portable app does not require the SDK at runtime."
}

try {
    if ($resolvedOutputPath.StartsWith('\\', [StringComparison]::Ordinal)) {
        $tempPublishPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('codex-notification-booster-publish-' + [guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $tempPublishPath -Force | Out-Null
        $publishOutputPath = $tempPublishPath
    }

    & $dotnetCommand.Source publish $projectPath `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:EnableCompressionInSingleFile=false `
        --output $publishOutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    if ($null -ne $tempPublishPath) {
        Copy-Item -LiteralPath (Join-Path -Path $tempPublishPath -ChildPath '*') -Destination $resolvedOutputPath -Recurse -Force
    }
}
finally {
    if ($null -ne $tempPublishPath -and (Test-Path -LiteralPath $tempPublishPath)) {
        Remove-Item -LiteralPath $tempPublishPath -Recurse -Force
    }
}

$exePath = Join-Path -Path $resolvedOutputPath -ChildPath 'CodexNotificationBooster.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected portable executable was not produced at $exePath."
}

Write-Host "Portable Windows tray build published to:"
Write-Host $resolvedOutputPath
Write-Host ""
Write-Host "Launch from Windows 11:"
Write-Host "  $exePath"
