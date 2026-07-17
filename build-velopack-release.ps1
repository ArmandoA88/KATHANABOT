[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$RepositoryUrl = "https://github.com/ArmandoA88/KATHANABOT",
    [string]$TargetBranch = "agent-ai",
    [string]$GitHubToken = $env:GITHUB_TOKEN,
    [switch]$Publish,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root "ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj"
$icon = Join-Path $root "ui\KathanaBotControlPanel\assets\KathanaBot.ico"
$publishDir = Join-Path $root "dist\velopack\publish-win-x64"
$releaseDir = Join-Path $root "dist\velopack\Releases"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content -LiteralPath $project
    $Version = [string]$projectXml.Project.PropertyGroup.Version
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a valid release version (example: 1.0.43)."
}

New-Item -ItemType Directory -Force -Path $publishDir, $releaseDir | Out-Null

Write-Host "Restoring Velopack 1.2.0..."
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

Write-Host "Publishing KathanaBot $Version as a self-contained win-x64 application..."
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir `
    -p:Version=$Version `
    -p:FileVersion="$Version.0" `
    -p:AssemblyVersion="$Version.0" `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Trying to download the previous GitHub release for delta generation..."
$downloadArguments = @(
    "vpk", "download", "github",
    "--repoUrl", $RepositoryUrl,
    "--outputDir", $releaseDir,
    "--channel", "win"
)
if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
    $downloadArguments += @("--token", $GitHubToken)
}
if ($Prerelease) {
    $downloadArguments += @("--pre", "true")
}
& dotnet @downloadArguments
if ($LASTEXITCODE -ne 0) {
    Write-Warning "No previous compatible release was downloaded. A full package will still be created."
}

Write-Host "Creating Velopack setup, portable, and full packages (plus a delta when a prior release is available)..."
dotnet vpk pack `
    --packId "KathanaBot" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "KathanaBotControlPanel.exe" `
    --packTitle "KathanaBot" `
    --packAuthors "KathanaBot" `
    --icon $icon `
    --runtime "win-x64" `
    --channel "win" `
    --outputDir $releaseDir `
    --shortcuts "Desktop,StartMenuRoot"
if ($LASTEXITCODE -ne 0) { throw "Velopack packaging failed." }

$setup = Get-ChildItem -LiteralPath $releaseDir -Filter "*-Setup.exe" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $setup) {
    throw "Velopack completed but no setup executable was found."
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$rootSetup = Join-Path $root "KathanaBot-Setup-v$Version-$timestamp.exe"
Copy-Item -LiteralPath $setup.FullName -Destination $rootSetup
Write-Host "Versioned root installer: $rootSetup"

if ($Publish) {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        throw "Publishing requires -GitHubToken or the GITHUB_TOKEN environment variable."
    }

    $uploadArguments = @(
        "vpk", "upload", "github",
        "--repoUrl", $RepositoryUrl,
        "--token", $GitHubToken,
        "--outputDir", $releaseDir,
        "--channel", "win",
        "--publish", "true",
        "--tag", "v$Version",
        "--releaseName", "KathanaBot $Version",
        "--targetCommitish", $TargetBranch
    )
    if ($Prerelease) {
        $uploadArguments += @("--pre", "true")
    }
    & dotnet @uploadArguments
    if ($LASTEXITCODE -ne 0) { throw "Velopack GitHub upload failed." }
}

Write-Host "Velopack release files: $releaseDir"
