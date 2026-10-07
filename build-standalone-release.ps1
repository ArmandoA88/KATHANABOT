[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $dotnetDirectory = Join-Path $env:ProgramFiles 'dotnet'
    if (!(Test-Path -LiteralPath (Join-Path $dotnetDirectory 'dotnet.exe'))) { throw '.NET SDK is required.' }
    $env:PATH = "$dotnetDirectory;$env:PATH"
}
& (Join-Path $PSScriptRoot 'run-all-tests.ps1')
$project = Join-Path $PSScriptRoot 'ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj'
[xml]$metadata = Get-Content -LiteralPath $project
$version = [string]$metadata.Project.PropertyGroup.Version
$output = Join-Path $PSScriptRoot "dist\KathanaBot_$version"
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Standalone publish failed.' }
$source = Join-Path $output 'KathanaBotControlPanel.exe'
$destination = Join-Path $PSScriptRoot "KathanaBotControlPanel_Standalone_$version.exe"
Copy-Item -LiteralPath $source -Destination $destination
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'EXE copy verification failed.' }
Write-Host "Created and verified $destination"
if ([string]$metadata.Project.PropertyGroup.InputBuildVariant -eq 'Foreground') {
    $foregroundDestination = Join-Path $PSScriptRoot "KathanaBotControlPanel_Standalone_Foreground_$version.exe"
    Copy-Item -LiteralPath $source -Destination $foregroundDestination
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $foregroundDestination).Hash) { throw 'Foreground EXE copy verification failed.' }
    Write-Host "Created and verified $foregroundDestination"
}
