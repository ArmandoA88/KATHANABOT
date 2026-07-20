[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'tools\SecurePakBrowser\SecurePakBrowser.csproj'
$publishPath = Join-Path $repositoryRoot 'tools\SecurePakBrowser\publish'
$rootExecutable = Join-Path $repositoryRoot 'HTRD KAT MOD Browser.exe'

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishPath

$publishedExecutable = Join-Path $publishPath 'HTRD KAT MOD Browser.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publish did not produce $publishedExecutable"
}

try {
    Copy-Item -LiteralPath $publishedExecutable -Destination $rootExecutable -Force
}
catch {
    $publishedVersion = (Get-Item -LiteralPath $publishedExecutable).VersionInfo.FileVersion
    $shortVersion = ($publishedVersion -split '\.')[0..2] -join '.'
    $rootExecutable = Join-Path $repositoryRoot "HTRD KAT MOD Browser-v$shortVersion.exe"
    Copy-Item -LiteralPath $publishedExecutable -Destination $rootExecutable -Force
    Write-Warning "The unversioned executable is currently open. Published to $rootExecutable instead."
}

$file = Get-Item -LiteralPath $rootExecutable
$hash = Get-FileHash -LiteralPath $rootExecutable -Algorithm SHA256
Write-Host "Created $($file.FullName)"
Write-Host "Size: $($file.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"
