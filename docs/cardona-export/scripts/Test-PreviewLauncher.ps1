[CmdletBinding()]
param([Parameter(Mandatory)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath($TestDirectory)
if (Test-Path -LiteralPath $testRoot) { throw 'Use a fresh test directory.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$package = Join-Path $testRoot 'package'
$old = Join-Path $testRoot 'old-package'
$stable = Join-Path $testRoot 'stable'
New-Item -ItemType Directory -Path (Join-Path $package 'app'),(Join-Path $old 'settings') | Out-Null
[IO.File]::WriteAllText((Join-Path $package 'app\Greenshot.exe'), 'test fixture, never executed')
[IO.File]::WriteAllText((Join-Path $old 'settings\greenshot.ini'), "[Core]`nUiScalePercent=150`nOutputFileDpiPreset=Print`nOutputExportProfiles=preserved-fixture`n")
foreach ($name in @('Start-Preview.ps1','Start-Greenshot.ps1','Get-PreviewDiagnostics.ps1','Install-Preview.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $package $name)
}
$files = @{}
foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse) {
    $files[$file.FullName.Substring($package.Length + 1).Replace('\','/')] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
@{ sourceCommit = 'launcher-test'; files = $files } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'manifest.json')
$global:CardonaLauncherTestState = @{ Calls = @(); Running = @() }; $checks = 0
function Get-Process { param($Name,$ErrorAction) foreach ($process in $global:CardonaLauncherTestState.Running) { $process } }
function Start-Process { param($FilePath,$ArgumentList,$WorkingDirectory) $global:CardonaLauncherTestState.Calls += @{file=$FilePath;args=$ArgumentList} }
function Assert-That { param($Condition,$Message) if (!$Condition) { throw $Message } }
& (Join-Path $package 'Install-Preview.ps1') -PackageDirectory $package -LauncherDirectory $stable -PreviousPackageDirectory $old | Out-Null
Assert-That (Test-Path -LiteralPath (Join-Path $stable 'Start-Greenshot.ps1')) 'Stable launcher was not installed.'; $checks++
& (Join-Path $stable 'Start-Greenshot.ps1') -CheckOnly | Out-Null
Assert-That (!(Test-Path -LiteralPath (Join-Path $stable 'state'))) 'CheckOnly changed state.'; $checks++
& (Join-Path $stable 'Start-Greenshot.ps1') | Out-Null
$settings = Join-Path $stable 'state\settings\greenshot.ini'
Assert-That ((Get-FileHash -LiteralPath $settings).Hash -eq (Get-FileHash -LiteralPath (Join-Path $old 'settings\greenshot.ini')).Hash) 'Preferences did not migrate exactly.'; $checks++
Assert-That (Test-Path -LiteralPath (Join-Path $stable 'state\settings\pre-upgrade-greenshot.ini')) 'Migration backup missing.'; $checks++
Assert-That ($global:CardonaLauncherTestState.Calls.Count -eq 1 -and ($global:CardonaLauncherTestState.Calls[0].args -join ' ').Contains('state\settings')) 'Shared settings not passed to preview.'; $checks++
[IO.File]::WriteAllText($settings, 'new preferences')
& (Join-Path $stable 'Start-Greenshot.ps1') | Out-Null
Assert-That ([IO.File]::ReadAllText($settings) -eq 'new preferences') 'Existing shared preferences overwritten.'; $checks++
$global:CardonaLauncherTestState.Running = @([pscustomobject]@{Id=123;Path=(Join-Path $old 'app\Greenshot.exe');Responding=$true})
$blocked = $false
try { & (Join-Path $stable 'Start-Greenshot.ps1') | Out-Null } catch { $blocked = $_.Exception.Message.Contains('different Greenshot') }
Assert-That $blocked 'Different-preview guard failed.'; $checks++
$global:CardonaLauncherTestState.Running = @([pscustomobject]@{Id=123;Path=(Join-Path $package 'app\Greenshot.exe');Responding=$true})
$before = (Get-FileHash -LiteralPath $settings).Hash
& (Join-Path $stable 'Start-Greenshot.ps1') | Out-Null
Assert-That ($global:CardonaLauncherTestState.Calls[-1].args -eq 'greenshot:settings') 'Repeat launch did not request Preferences.'; $checks++
Assert-That ((Get-FileHash -LiteralPath $settings).Hash -eq $before) 'Repeat launch changed preferences.'; $checks++
$report = & (Join-Path $stable 'Get-PreviewDiagnostics.ps1') | ConvertFrom-Json
Assert-That ($report.processCount -eq 1 -and $report.processes[0].selectedPreview -and $report.sharedPreferencesExist) 'Diagnostics report incorrect.'; $checks++
Assert-That (!($report | ConvertTo-Json -Depth 5).Contains('new preferences')) 'Diagnostics leaked settings contents.'; $checks++
$pointerHash = (Get-FileHash -LiteralPath (Join-Path $stable 'current-preview.json')).Hash
[IO.File]::WriteAllText((Join-Path $package 'app\Greenshot.exe'), 'corrupted fixture')
$blocked = $false
try { & (Join-Path $package 'Install-Preview.ps1') -PackageDirectory $package -LauncherDirectory $stable | Out-Null } catch { $blocked = $_.Exception.Message.Contains('hash mismatch') }
Assert-That ($blocked -and (Get-FileHash -LiteralPath (Join-Path $stable 'current-preview.json')).Hash -eq $pointerHash) 'Corrupt package changed the pointer.'; $checks++
Write-Output "$checks launcher checks passed. Process starts were simulated; no app was launched."


Remove-Variable -Name CardonaLauncherTestState -Scope Global
