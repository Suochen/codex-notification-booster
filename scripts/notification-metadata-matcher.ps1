<#
.SYNOPSIS
Identifies Codex notifications from plain metadata records.

.DESCRIPTION
This matcher is intentionally metadata-only. It accepts records shaped like the
notification probe JSONL output and returns a structured match/ignore decision.
It does not call Windows notification APIs, play sound, change volume, or modify
notifications.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:CodexAppUserModelId = 'OpenAI.Codex_2p2nqsd0c76g0!App'
$script:CodexPackageFamilyName = 'OpenAI.Codex_2p2nqsd0c76g0'

function Get-MetadataValue {
    param(
        $Record,
        [string]$PropertyName
    )

    if ($null -eq $Record) {
        return $null
    }

    if ($Record -is [System.Collections.IDictionary]) {
        if ($Record.Contains($PropertyName)) {
            return $Record[$PropertyName]
        }
        return $null
    }

    $property = $Record.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function New-NotificationMatchDecision {
    param(
        [bool]$Matched,
        [string]$Reason,
        [string]$MatchedRule
    )

    [pscustomobject]@{
        matched = $Matched
        reason = $Reason
        matchedRule = $MatchedRule
    }
}

function Test-CodexNotificationMetadata {
    param(
        [Parameter(Mandatory = $true)]
        $Record
    )

    $appUserModelId = [string](Get-MetadataValue -Record $Record -PropertyName 'appUserModelId')
    if ($appUserModelId -eq $script:CodexAppUserModelId) {
        return New-NotificationMatchDecision `
            -Matched $true `
            -Reason 'appUserModelId matches observed Codex identity' `
            -MatchedRule 'codex-app-user-model-id'
    }

    $packageFamilyName = [string](Get-MetadataValue -Record $Record -PropertyName 'packageFamilyName')
    if ($packageFamilyName -eq $script:CodexPackageFamilyName) {
        return New-NotificationMatchDecision `
            -Matched $true `
            -Reason 'packageFamilyName matches observed Codex identity' `
            -MatchedRule 'codex-package-family-name'
    }

    return New-NotificationMatchDecision `
        -Matched $false `
        -Reason 'notification metadata does not match observed Codex identity fields' `
        -MatchedRule 'no-codex-identity-match'
}
