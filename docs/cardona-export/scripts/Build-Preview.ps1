[CmdletBinding()]
param([switch]$Test, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..\..\src'))
$locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $locator)) { throw 'Microsoft desktop build tools are required.' }
$builder = & $locator -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$builder) { throw 'MSBuild was not found.' }
$previousLocation = Get-Location
$restoreArguments = @()
if (!$NoRestore) { $restoreArguments += '/restore' }
$graphArguments = @()
if ($NoRestore) { $graphArguments += '/p:BuildProjectReferences=false', '/p:MSBuildCopyContentTransitively=false' }
try {
    Set-Location -LiteralPath $sourceRoot
    if ($NoRestore) {
        # Reuse the unchanged helper; cached packages and prior plugin builds are prerequisites.
        if (!(Test-Path -LiteralPath 'Greenshot.BuildTasks\bin\Debug\net480\Greenshot.BuildTasks.dll')) {
            throw 'No-restore builds require an existing build helper. Build once with package restore available.'
        }
        foreach ($library in @('Greenshot.Base', 'Greenshot.Editor')) {
            & $builder "$library\$library.csproj" @graphArguments /p:Configuration=Debug /p:SolutionName=Greenshot "/p:SolutionDir=$sourceRoot\" /verbosity:minimal /nologo
            if ($LASTEXITCODE) { throw "$library no-restore build failed." }
        }
    } else {
        & $builder 'Greenshot.BuildTasks\Greenshot.BuildTasks.csproj' @restoreArguments /p:Configuration=Debug /verbosity:minimal /nologo
        if ($LASTEXITCODE) { throw 'Build helper failed.' }
    }
    & $builder 'Greenshot\Greenshot.csproj' @restoreArguments @graphArguments /p:Configuration=DebugLight /p:SolutionName=Greenshot "/p:SolutionDir=$sourceRoot\" /verbosity:minimal /nologo
    if ($LASTEXITCODE) { throw 'Preview build failed.' }
    if ($Test) {
        $testRunner = & $locator -latest -products '*' -requires Microsoft.Component.MSBuild -find 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
        if (!$testRunner) { throw 'The Visual Studio test runner was not found.' }
        if ($NoRestore) {
            # The tests reference the Debug executable, separate from the lightweight preview.
            & $builder 'Greenshot\Greenshot.csproj' @graphArguments /p:Configuration=Debug /p:SolutionName=Greenshot "/p:SolutionDir=$sourceRoot\" /verbosity:minimal /nologo
            if ($LASTEXITCODE) { throw 'No-restore test-host build failed.' }
        }
        & $builder 'Greenshot.Tests\Greenshot.Tests.csproj' @restoreArguments @graphArguments /p:Configuration=Debug /p:SolutionName=Greenshot "/p:SolutionDir=$sourceRoot\" /verbosity:minimal /nologo
        if ($LASTEXITCODE) { throw 'Test build failed.' }
        & $testRunner 'Greenshot.Tests\bin\Debug\net480\Greenshot.Tests.dll' '/TestCaseFilter:FullyQualifiedName~ExportDpiTests|FullyQualifiedName~ExportSummaryTests|FullyQualifiedName~UserInteractionWindowsTests|FullyQualifiedName~ImageIOSaveToStreamTests|FullyQualifiedName~SettingsWindowTests|FullyQualifiedName~ResizeSettingsWindowTests|FullyQualifiedName~ResizeImageTests|FullyQualifiedName~WebpExportTests|FullyQualifiedName~WebpQualityWindowTests|FullyQualifiedName~QuickDpiPreferencesTests|FullyQualifiedName~FileFormatRegistryTests|FullyQualifiedName~UiScaleTests|FullyQualifiedName~ImageHelperAndWpfFormsTests.MigratedWpfDialogs_CanBeInstantiatedOnStaThread' '/Logger:trx;LogFileName=export-dpi.trx' "/ResultsDirectory:$projectRoot\test-results"
        if ($LASTEXITCODE) { throw 'Export tests failed.' }
    }
    Write-Output "Preview built at $sourceRoot\Greenshot\bin\Debug-Light\net480. Nothing was installed or launched."
} finally {
    Set-Location -LiteralPath $previousLocation.Path
}
