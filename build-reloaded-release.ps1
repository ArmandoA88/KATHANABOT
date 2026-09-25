[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'KathanaBotReloaded\KathanaBotReloaded.csproj'
[xml]$metadata = Get-Content -LiteralPath $project
$version = [string]$metadata.Project.PropertyGroup.Version
$output = Join-Path $PSScriptRoot "dist\Reloaded-$version"
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Reloaded publish failed.' }
$exe = Join-Path $output 'KATHANA BOT RELOADED.exe'
$test = Start-Process -FilePath $exe -ArgumentList '--self-test --ocr' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw "Self-tests failed. See $output\self-test-error.txt" }
$standalone = Join-Path $PSScriptRoot "KATHANA BOT RELOADED - v$version.exe"
Copy-Item -LiteralPath $exe -Destination $standalone -Force
$asset = Join-Path $output 'KathanaBotReloaded-win-x64-standalone.exe'
Copy-Item -LiteralPath $exe -Destination $asset -Force
$hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-FileHash -LiteralPath $standalone -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Standalone copy mismatch.' }
[System.IO.File]::WriteAllText("$asset.sha256", "$hash  KathanaBotReloaded-win-x64-standalone.exe`n", [System.Text.Encoding]::ASCII)
Write-Output "Verified standalone: $standalone"
Write-Output "Release tag: reloaded-v$version"
Write-Output "Release assets: $asset and $asset.sha256"
Write-Output 'Files are prepared locally. This script does not publish a release.'
