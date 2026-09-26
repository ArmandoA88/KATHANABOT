param(
    [switch]$Configure,
    [switch]$ValidateOnly,
    [switch]$SeparateWindows
)

$ErrorActionPreference = 'Stop'
$settingsPath = Join-Path $PSScriptRoot 'RemoteDesktopMosaic.settings.json'

function Find-Chrome {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google/Chrome/Application/chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google/Chrome/Application/chrome.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $command = Get-Command chrome.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'Google Chrome was not found. Install Chrome and run this launcher again.'
}

# The mosaic now owns all of its own data (computers, nicknames, window position) in one
# RemoteDesktopMosaic.settings.json next to it - first-run setup, adding, renaming, and removing
# computers all happen inside the mosaic itself. This launcher only reads that same file, so
# -ValidateOnly/-SeparateWindows stay in sync with whatever the mosaic currently has saved.
function Get-SavedLinks {
    if (-not (Test-Path -LiteralPath $settingsPath)) { return @() }
    try {
        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        if ($null -eq $settings.Computers) { return @() }
        return @($settings.Computers | ForEach-Object { $_.Link } | Where-Object { $_ })
    } catch { return @() }
}

try {
    if ($ValidateOnly) {
        $links = Get-SavedLinks
        Write-Output "Chrome: $(Find-Chrome)"
        Write-Output "Saved computers: $($links.Count)"
        if ($links.Count -eq 0) { Write-Output 'No computers saved yet - the mosaic will prompt for at least one on first launch.' }
        exit 0
    }
    if ($SeparateWindows) {
        $chromePath = Find-Chrome
        $links = Get-SavedLinks
        if ($links.Count -eq 0) { throw 'No computers saved yet. Launch the mosaic once (without -SeparateWindows) to add computers first.' }
        foreach ($link in $links) {
            Start-Process -FilePath $chromePath -ArgumentList @('--new-window', ('"' + $link.Replace('"', '%22') + '"'))
            Start-Sleep -Milliseconds 400
        }
        exit 0
    }
    # -Configure used to open a separate link-editing dialog; that editing now happens inline in the
    # mosaic (Add computer / right-click a tile to rename or remove), so both paths just launch it.
    $mosaicPath = Join-Path $PSScriptRoot 'RemoteDesktopMosaic_20260909_NoDuplicates.exe'
    if (-not (Test-Path -LiteralPath $mosaicPath -PathType Leaf)) {
        throw 'RemoteDesktopMosaic_20260909_NoDuplicates.exe is missing from the launcher folder.'
    }
    Start-Process -FilePath $mosaicPath
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Open All Remote Desktops') | Out-Null
    exit 1
}
