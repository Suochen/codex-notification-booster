<#
.SYNOPSIS
Reads Windows toast notification metadata and appends local JSONL records.

.DESCRIPTION
This first-stage discovery probe is intentionally read-only. It asks Windows
for notification listener access when the status is unspecified, reads visible
toast notifications, writes broad metadata to a local JSONL file, and exits.
It does not play sounds, change volume, dismiss, clear, move, reply to, or
otherwise modify notifications.
#>
[CmdletBinding()]
param(
    [switch]$Once,

    [ValidateRange(1, 1440)]
    [int]$DurationMinutes = 10,

    [ValidateRange(1, 60)]
    [int]$PollSeconds = 2,

    [string]$LogPath,

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProbeVersion = '0.1.0'

function Get-DefaultLogPath {
    $basePath = $env:LOCALAPPDATA
    if ([string]::IsNullOrWhiteSpace($basePath)) {
        $basePath = [System.IO.Path]::GetTempPath()
    }

    Join-Path -Path $basePath -ChildPath 'CodexNotificationBooster\notification-probe.jsonl'
}

function Resolve-ProbeLogPath {
    param([string]$ConfiguredPath)

    if ([string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        return Get-DefaultLogPath
    }

    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ConfiguredPath)
}

function Write-Status {
    param([string]$Message)
    Write-Host "[notification-probe] $Message"
}

function ConvertTo-NullableIsoString {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }

    try {
        if ($Value -is [System.DateTimeOffset]) {
            return $Value.ToUniversalTime().ToString('o')
        }
        if ($Value -is [System.DateTime]) {
            return $Value.ToUniversalTime().ToString('o')
        }
        return [string]$Value
    }
    catch {
        return $null
    }
}

function Get-PropertyValue {
    param(
        $InputObject,
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    try {
        $property = $InputObject.PSObject.Properties[$PropertyName]
        if ($null -eq $property) {
            return $null
        }
        return $property.Value
    }
    catch {
        return $null
    }
}

function Get-NestedPropertyValue {
    param(
        $InputObject,
        [string[]]$PropertyPath
    )

    $current = $InputObject
    foreach ($propertyName in $PropertyPath) {
        $current = Get-PropertyValue -InputObject $current -PropertyName $propertyName
        if ($null -eq $current) {
            return $null
        }
    }

    return $current
}

function Get-MethodValue {
    param(
        $InputObject,
        [string]$MethodName,
        [object[]]$ArgumentList = @()
    )

    if ($null -eq $InputObject) {
        return $null
    }

    try {
        $method = $InputObject.PSObject.Methods[$MethodName]
        if ($null -eq $method) {
            return $null
        }
        return $method.Invoke($ArgumentList)
    }
    catch {
        return $null
    }
}

function ConvertTo-StringArray {
    param($Value)

    $result = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Value) {
        return @()
    }

    foreach ($item in @($Value)) {
        if ($null -ne $item) {
            $text = [string]$item
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                [void]$result.Add($text)
            }
        }
    }

    return $result.ToArray()
}

function Get-Sha256Hex {
    param([string]$Text)

    if ($null -eq $Text) {
        $Text = ''
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-DedupRecordValue {
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

    return Get-PropertyValue -InputObject $Record -PropertyName $PropertyName
}

function Get-NotificationDedupKey {
    param($Record)

    $parts = @(
        (Get-DedupRecordValue -Record $Record -PropertyName 'appUserModelId')
        (Get-DedupRecordValue -Record $Record -PropertyName 'notificationId')
        (Get-DedupRecordValue -Record $Record -PropertyName 'creationTime')
        (Get-DedupRecordValue -Record $Record -PropertyName 'appId')
        (Get-DedupRecordValue -Record $Record -PropertyName 'packageFamilyName')
        (Get-DedupRecordValue -Record $Record -PropertyName 'appDisplayName')
        ((ConvertTo-StringArray -Value (Get-DedupRecordValue -Record $Record -PropertyName 'textLines')) -join "`n")
        (Get-DedupRecordValue -Record $Record -PropertyName 'rawXmlSha256')
    )

    Get-Sha256Hex -Text ($parts -join '|')
}

function ConvertTo-JsonLine {
    param($Record)

    $Record | ConvertTo-Json -Depth 24 -Compress
}

function Append-JsonLine {
    param(
        [string]$Path,
        [string]$JsonLine
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Add-Content -Path $Path -Value $JsonLine -Encoding UTF8
}

function Initialize-WindowsNotificationTypes {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime

    [void][Windows.UI.Notifications.Management.UserNotificationListener, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.UI.Notifications.NotificationKinds, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.UI.Notifications.KnownNotificationBindings, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.UI.Notifications.UserNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]
}

function Wait-WinRtAsyncOperation {
    param(
        [Parameter(Mandatory = $true)]
        $Operation,

        [Parameter(Mandatory = $true)]
        [Type]$ResultType
    )

    $asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and
            $_.IsGenericMethodDefinition -and
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        } |
        Select-Object -First 1

    if ($null -eq $asTaskMethod) {
        throw 'Could not locate WindowsRuntimeSystemExtensions.AsTask for IAsyncOperation<T>.'
    }

    $task = $asTaskMethod.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.Wait()
    return $task.Result
}

function Get-NotificationListenerAccess {
    param($Listener)

    $status = $Listener.GetAccessStatus()
    Write-Status "notification listener access status: $status"

    if ([string]$status -eq 'Unspecified') {
        Write-Status 'requesting Windows notification listener access'
        $request = $Listener.RequestAccessAsync()
        $statusType = [Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus, Windows.UI.Notifications, ContentType = WindowsRuntime]
        $status = Wait-WinRtAsyncOperation -Operation $request -ResultType $statusType
        Write-Status "notification listener access status after request: $status"
    }

    return $status
}

function Assert-NotificationAccessAllowed {
    param($Status)

    if ([string]$Status -eq 'Allowed') {
        return
    }

    Write-Error @"
Windows notification listener access is '$Status'. Enable notification access for Windows PowerShell, then run the probe again.

Suggested remediation:
1. Open Windows Settings.
2. Search for "Notifications" or "Notification access".
3. Allow notification access for the PowerShell host you are using.
4. Re-run this script directly from Windows PowerShell.
"@
}

function Get-ToastBindingText {
    param($ToastNotification)

    $visual = Get-PropertyValue -InputObject $ToastNotification -PropertyName 'Visual'
    $genericBindingName = [Windows.UI.Notifications.KnownNotificationBindings, Windows.UI.Notifications, ContentType = WindowsRuntime]::ToastGeneric
    $binding = Get-MethodValue -InputObject $visual -MethodName 'GetBinding' -ArgumentList @($genericBindingName)
    $textElements = Get-MethodValue -InputObject $binding -MethodName 'GetTextElements'

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($element in @($textElements)) {
        $text = Get-PropertyValue -InputObject $element -PropertyName 'Text'
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            [void]$lines.Add([string]$text)
        }
    }

    return $lines.ToArray()
}

function Get-RawNotificationXml {
    param($ToastNotification)

    $xml = Get-MethodValue -InputObject $ToastNotification -MethodName 'GetXml'
    if ($null -eq $xml) {
        $content = Get-PropertyValue -InputObject $ToastNotification -PropertyName 'Content'
        $xml = Get-MethodValue -InputObject $content -MethodName 'GetXml'
    }
    if ($null -eq $xml) {
        return $null
    }

    $xmlText = Get-MethodValue -InputObject $xml -MethodName 'GetXml'
    if ($null -eq $xmlText) {
        return [string]$xml
    }

    return [string]$xmlText
}

function ConvertTo-NotificationRecord {
    param($UserNotification)

    $notification = Get-PropertyValue -InputObject $UserNotification -PropertyName 'Notification'
    $appInfo = Get-PropertyValue -InputObject $UserNotification -PropertyName 'AppInfo'
    $displayInfo = Get-PropertyValue -InputObject $appInfo -PropertyName 'DisplayInfo'
    $package = Get-PropertyValue -InputObject $appInfo -PropertyName 'Package'
    $textLines = ConvertTo-StringArray -Value (Get-ToastBindingText -ToastNotification $notification)
    $rawXml = Get-RawNotificationXml -ToastNotification $notification

    [ordered]@{
        schemaVersion = 1
        probeVersion = $script:ProbeVersion
        capturedAt = [System.DateTimeOffset]::UtcNow.ToString('o')
        creationTime = ConvertTo-NullableIsoString -Value (Get-PropertyValue -InputObject $UserNotification -PropertyName 'CreationTime')
        notificationId = Get-PropertyValue -InputObject $UserNotification -PropertyName 'Id'
        appDisplayName = Get-PropertyValue -InputObject $displayInfo -PropertyName 'DisplayName'
        appUserModelId = Get-PropertyValue -InputObject $appInfo -PropertyName 'AppUserModelId'
        appId = Get-PropertyValue -InputObject $appInfo -PropertyName 'Id'
        packageFamilyName = Get-PropertyValue -InputObject $appInfo -PropertyName 'PackageFamilyName'
        packageFullName = Get-NestedPropertyValue -InputObject $package -PropertyPath @('Id', 'FullName')
        executionContext = Get-NestedPropertyValue -InputObject $notification -PropertyPath @('Visual', 'Binding', 'ExecutionContext')
        textLines = $textLines
        title = if ($textLines.Count -gt 0) { $textLines[0] } else { $null }
        body = if ($textLines.Count -gt 1) { ($textLines[1..($textLines.Count - 1)] -join "`n") } else { $null }
        rawXml = $rawXml
        rawXmlSha256 = if ($null -ne $rawXml) { Get-Sha256Hex -Text $rawXml } else { $null }
    }
}

function Get-CurrentToastNotifications {
    param($Listener)

    $operation = $Listener.GetNotificationsAsync([Windows.UI.Notifications.NotificationKinds, Windows.UI.Notifications, ContentType = WindowsRuntime]::Toast)
    $notificationType = [Windows.UI.Notifications.UserNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]
    $resultType = [System.Collections.Generic.IReadOnlyList``1].MakeGenericType($notificationType)
    Wait-WinRtAsyncOperation -Operation $operation -ResultType $resultType
}

function Capture-Notifications {
    param(
        [string]$OutputPath,
        [switch]$SinglePass,
        [int]$Duration,
        [int]$Interval
    )

    try {
        Initialize-WindowsNotificationTypes
        $listener = [Windows.UI.Notifications.Management.UserNotificationListener, Windows.UI.Notifications, ContentType = WindowsRuntime]::Current
    }
    catch {
        Write-Error "Windows notification listener APIs are unavailable from this host. Run the probe directly on Windows 11 with Windows PowerShell. Original error: $($_.Exception.Message)"
    }

    try {
        $status = Get-NotificationListenerAccess -Listener $listener
        Assert-NotificationAccessAllowed -Status $status
    }
    catch {
        Write-Error "Could not obtain Windows notification listener access. Use Windows Settings to enable notification access for the PowerShell host, then run the probe again. Original error: $($_.Exception.Message)"
    }

    Write-Status "writing JSONL metadata to $OutputPath"
    Write-Status 'logs may contain raw notification text; keep them local and out of Git/GitHub'

    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    $deadline = [System.DateTimeOffset]::UtcNow.AddMinutes($Duration)
    $captured = 0

    do {
        $notifications = Get-CurrentToastNotifications -Listener $listener

        foreach ($notification in @($notifications)) {
            $record = ConvertTo-NotificationRecord -UserNotification $notification
            $dedupKey = Get-NotificationDedupKey -Record $record
            if ($seen.Add($dedupKey)) {
                $record['dedupKey'] = $dedupKey
                Append-JsonLine -Path $OutputPath -JsonLine (ConvertTo-JsonLine -Record $record)
                $captured += 1
                Write-Status "captured notification metadata: app='$($record.appDisplayName)' id='$($record.notificationId)'"
            }
        }

        if ($SinglePass) {
            break
        }

        Start-Sleep -Seconds $Interval
    } while ([System.DateTimeOffset]::UtcNow -lt $deadline)

    Write-Status "complete; new records written this run: $captured"
}

function Invoke-SelfTest {
    $originalLocalAppData = $env:LOCALAPPDATA
    $env:LOCALAPPDATA = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'cnb-selftest-localappdata'
    try {
        $defaultPath = Get-DefaultLogPath
        if (-not $defaultPath.EndsWith('CodexNotificationBooster\notification-probe.jsonl')) {
            throw "Unexpected default log path: $defaultPath"
        }

        $record = [ordered]@{
            appUserModelId = 'sample.app'
            appId = 'App'
            packageFamilyName = 'sample.package'
            appDisplayName = 'Sample App'
            notificationId = 42
            creationTime = '2026-05-27T00:00:00.0000000Z'
            textLines = @('Title', 'Body')
            rawXmlSha256 = 'abc'
        }
        $key1 = Get-NotificationDedupKey -Record $record
        $key2 = Get-NotificationDedupKey -Record $record
        if ($key1 -ne $key2) {
            throw 'Dedup key is not stable.'
        }

        $sameVisibleNotification = [ordered]@{
            appUserModelId = 'sample.app'
            appId = 'App'
            packageFamilyName = 'sample.package'
            appDisplayName = 'Sample App'
            notificationId = 43
            creationTime = '2026-05-27T00:00:01.0000000Z'
            textLines = @('Title', 'Body')
            rawXmlSha256 = 'def'
        }
        $key3 = Get-NotificationDedupKey -Record $sameVisibleNotification
        if ($key1 -eq $key3) {
            throw 'Archival dedup key should distinguish different notification instances.'
        }

        $jsonLine = ConvertTo-JsonLine -Record ([ordered]@{
            schemaVersion = 1
            textLines = @('Title', 'Body')
        })
        if ($jsonLine -notmatch '"schemaVersion":1' -or $jsonLine -notmatch '"textLines":\["Title","Body"\]') {
            throw "Unexpected JSONL output: $jsonLine"
        }

        $tempLog = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('cnb-selftest-' + [System.Guid]::NewGuid().ToString('N') + '.jsonl')
        Append-JsonLine -Path $tempLog -JsonLine $jsonLine
        if (-not (Test-Path -LiteralPath $tempLog)) {
            throw 'Append-JsonLine did not create the log file.'
        }
        Remove-Item -LiteralPath $tempLog -Force

        Write-Status 'self-test passed'
    }
    finally {
        $env:LOCALAPPDATA = $originalLocalAppData
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ($SelfTest) {
        Invoke-SelfTest
        return
    }

    $resolvedLogPath = Resolve-ProbeLogPath -ConfiguredPath $LogPath
    Capture-Notifications -OutputPath $resolvedLogPath -SinglePass:$Once -Duration $DurationMinutes -Interval $PollSeconds
}
