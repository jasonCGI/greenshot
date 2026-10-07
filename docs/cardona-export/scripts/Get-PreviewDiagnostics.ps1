[CmdletBinding()]
param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$pointerPath = Join-Path $PSScriptRoot 'current-preview.json'
$pointer = if (Test-Path -LiteralPath $pointerPath) { Get-Content -LiteralPath $pointerPath -Raw | ConvertFrom-Json } else { $null }
$settings = Join-Path $PSScriptRoot 'state\settings\greenshot.ini'
$processes = @(Get-Process -Name Greenshot -ErrorAction SilentlyContinue | ForEach-Object {
    @{ id = $_.Id; responding = $_.Responding; selectedPreview = $pointer -and $_.Path -and [string]::Equals($_.Path, (Join-Path $pointer.packageDirectory 'app\Greenshot.exe'), [StringComparison]::OrdinalIgnoreCase) }
})
$version = if ($pointer -and (Test-Path -LiteralPath (Join-Path $pointer.packageDirectory 'app\Greenshot.exe'))) {
    [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $pointer.packageDirectory 'app\Greenshot.exe')).ProductVersion
} else { $null }
$report = @{ schemaVersion = 1; collectedUtc = [DateTime]::UtcNow.ToString('o'); windowsVersion = [Environment]::OSVersion.Version.ToString();
    sourceCommit = $pointer.sourceCommit; productVersion = $version; processCount = $processes.Count; processes = $processes;
    sharedPreferencesExist = Test-Path -LiteralPath $settings; sharedLogsExist = Test-Path -LiteralPath (Join-Path $PSScriptRoot 'state\logs');
    guidance = 'If no process is listed, launch with Start-Greenshot.ps1. If a different preview is listed, normally Exit it first. Check the Windows hidden-icons area for the tray icon. No logs or preference contents are included.' }
$json = $report | ConvertTo-Json -Depth 5
if ($OutputPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false)) }
else { $json }
