[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$executable = [IO.Path]::GetFullPath((Join-Path $root 'app\Greenshot.exe'))
$running = @(Get-Process -Name Greenshot -ErrorAction SilentlyContinue)
if ($running.Count) {
    $matching = @($running | Where-Object { $_.Path -and [string]::Equals($_.Path, $executable, [StringComparison]::OrdinalIgnoreCase) })
    if ($running.Count -eq 1 -and $matching.Count -eq 1) {
        Start-Process -FilePath $executable -ArgumentList 'greenshot:settings' -WorkingDirectory $root
        return
    }
    throw 'A different Greenshot is running. Save your work and normally Exit it before launching this preview.'
}
foreach ($fixed in @((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Greenshot\greenshot-fixed.ini'), (Join-Path $root 'app\greenshot-fixed.ini'))) {
    if (Test-Path -LiteralPath $fixed) { throw 'A fixed Greenshot policy exists. Resolve its scope before using this isolated preview.' }
}
$settings = Join-Path $root 'settings'
$logs = Join-Path $root 'logs'
New-Item -ItemType Directory -Path $settings,$logs -Force | Out-Null
$logPath = Join-Path $logs 'Greenshot-preview.log'
$escapedLogPath = [Security.SecurityElement]::Escape($logPath)
$logXml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration><log4net><appender name="PreviewFile" type="log4net.Appender.RollingFileAppender"><file value="$escapedLogPath"/><appendToFile value="true"/><rollingStyle value="Size"/><maxSizeRollBackups value="3"/><maximumFileSize value="1MB"/><staticLogFileName value="true"/><layout type="log4net.Layout.PatternLayout"><conversionPattern value="%date{ISO8601} [%thread] %-5level - %m%n%exception"/></layout></appender><root><level value="INFO"/><appender-ref ref="PreviewFile"/></root></log4net></configuration>
"@
[IO.File]::WriteAllText((Join-Path $root 'app\log4net.xml'), $logXml, [Text.UTF8Encoding]::new($false))
Start-Process -FilePath (Join-Path $root 'app\Greenshot.exe') -ArgumentList @('--ini-directory', ('"' + $settings + '"')) -WorkingDirectory $root
