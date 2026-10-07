[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory,
      [Parameter(Mandatory)][string]$LauncherDirectory,
      [string]$PreviousPackageDirectory)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$launcher = [IO.Path]::GetFullPath($LauncherDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files.PSObject.Properties) {
    $path = [IO.Path]::GetFullPath((Join-Path $package $entry.Name))
    if (!$path.StartsWith($package.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package path.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value) { throw "Package hash mismatch: $($entry.Name)" }
}
$previous = if ($PreviousPackageDirectory) { (Resolve-Path -LiteralPath $PreviousPackageDirectory).Path } else { $null }
New-Item -ItemType Directory -Path $launcher -Force | Out-Null
$pointerPath = Join-Path $launcher 'current-preview.json'
if (Test-Path -LiteralPath $pointerPath) {
    $old = Get-Content -LiteralPath $pointerPath -Raw | ConvertFrom-Json
    if (!$previous) { $previous = $old.packageDirectory }
    Copy-Item -LiteralPath $pointerPath -Destination (Join-Path $launcher ('previous-preview-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffff') + '.json'))
}
foreach ($name in @('Start-Greenshot.ps1', 'Get-PreviewDiagnostics.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $launcher $name)
}
[IO.File]::WriteAllText((Join-Path $launcher 'Start-Greenshot.cmd'), "@echo off`r`npowershell.exe -NoProfile -File `"%~dp0Start-Greenshot.ps1`"`r`nif errorlevel 1 pause`r`n", [Text.Encoding]::ASCII)
$pointer = @{ version = 1; packageDirectory = $package; previousPackageDirectory = $previous; sourceCommit = $manifest.sourceCommit }
$temporary = Join-Path $launcher ('pointer-' + [Guid]::NewGuid().ToString('N') + '.tmp')
[IO.File]::WriteAllText($temporary, ($pointer | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporary -Destination $pointerPath -Force
Write-Output "Stable launcher ready: $(Join-Path $launcher 'Start-Greenshot.ps1')"
Write-Output 'Normally Exit a different running preview before launching. Existing preferences migrate only after Exit.'
