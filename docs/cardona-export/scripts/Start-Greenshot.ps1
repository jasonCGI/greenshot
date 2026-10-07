[CmdletBinding()]
param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$pointer = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'current-preview.json') -Raw | ConvertFrom-Json
if ($pointer.version -ne 1) { throw 'Unsupported preview pointer version.' }
$package = (Resolve-Path -LiteralPath $pointer.packageDirectory).Path
$executable = Join-Path $package 'app\Greenshot.exe'
if (!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath (Join-Path $package 'Start-Preview.ps1'))) { throw 'The selected preview is incomplete.' }
$running = @(Get-Process -Name Greenshot -ErrorAction SilentlyContinue)
if ($running.Count) {
    if ($running.Count -ne 1 -or !$running[0].Path -or ![string]::Equals($running[0].Path, $executable, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A different Greenshot is running. Save your work and normally Exit it, then use this stable launcher.'
    }
    if (!$CheckOnly) { & (Join-Path $package 'Start-Preview.ps1') }
    return
}
$state = Join-Path $PSScriptRoot 'state'
$settings = Join-Path $state 'settings'
$destination = Join-Path $settings 'greenshot.ini'
if ($CheckOnly) { Write-Output 'Launcher pointer and process guard passed. No settings copied and no app launched.'; return }
foreach ($fixed in @((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Greenshot\greenshot-fixed.ini'), (Join-Path $package 'app\greenshot-fixed.ini'))) {
    if (Test-Path -LiteralPath $fixed) { throw 'A fixed Greenshot policy exists. Resolve its scope before adopting shared preview preferences.' }
}
New-Item -ItemType Directory -Path $settings -Force | Out-Null
if (!(Test-Path -LiteralPath $destination) -and $pointer.previousPackageDirectory) {
    $old = Join-Path $pointer.previousPackageDirectory 'settings\greenshot.ini'
    if (Test-Path -LiteralPath $old) {
        $backup = Join-Path $settings 'pre-upgrade-greenshot.ini'
        Copy-Item -LiteralPath $old -Destination $backup
        Copy-Item -LiteralPath $backup -Destination $destination
        Write-Output 'Previous preview preferences preserved. Backup: state/settings/pre-upgrade-greenshot.ini'
    }
}
& (Join-Path $package 'Start-Preview.ps1') -StateDirectory $state

