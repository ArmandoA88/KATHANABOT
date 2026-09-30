[CmdletBinding()]
param(
    [int]$TargetProcessId,
    [ValidateRange(1,65535)][int]$RemotePort = 40001,
    [string]$WatchPath = '',
    [ValidateSet('Inventory','Move','Skill')][string]$ControlledAction,
    [ValidateRange(1,254)][int]$ActionKey = 73
)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Packet Monitor requires administrator rights. Right-click Record-Kathana-Network.cmd and choose Run as administrator.'
}
if ($TargetProcessId) { $target = Get-Process -Id $TargetProcessId } else {
    $targets = @(Get-Process KathanaGame -ErrorAction SilentlyContinue)
    if ($targets.Count -ne 1) { throw 'Open one Kathana game first, or specify -TargetProcessId.' }
    $target = $targets[0]
}
if ([string]::IsNullOrWhiteSpace($WatchPath)) {
    # Some game processes do not expose Path, even from an elevated shell.
    $gameExecutable = $target.Path
    if (![string]::IsNullOrWhiteSpace($gameExecutable)) {
        $WatchPath = Split-Path -Path $gameExecutable -Parent
    } else {
        $WatchPath = 'C:\Program Files (x86)\Steam\steamapps\common\Kathana'
        Write-Host 'Game executable path is unavailable; using the known Steam installation folder.'
    }
}
if (!(Test-Path -LiteralPath (Join-Path $WatchPath 'KathanaGame.exe') -PathType Leaf)) {
    throw 'Game folder not found. Supply -WatchPath with the folder containing KathanaGame.exe.'
}
$WatchPath = (Resolve-Path -LiteralPath $WatchPath).Path
$endpoints = @(Get-NetTCPConnection -OwningProcess $target.Id -State Established -ErrorAction Stop | Where-Object RemotePort -eq $RemotePort)
if (!$endpoints.Count) { throw "No established connection to port $RemotePort for the selected game. Enter the game and retry." }

function Invoke-PacketMonitor([string[]]$Arguments) {
    $output = & "$env:SystemRoot\System32\pktmon.exe" @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('pktmon ' + ($Arguments -join ' ') + ': ' + ($output -join "`n")) }
    return ($output -join "`n")
}
# PktMon is machine-wide. Never alter another recording or its filters.
$status = Invoke-PacketMonitor -Arguments @('status')
if ($status -notmatch '(?i)not running|stopped') { throw "Packet Monitor is already active or its status was not recognized. No changes made.`n$status" }
$filters = Invoke-PacketMonitor -Arguments @('filter','list')
if ($filters -notmatch '(?is)^\s*(?:No filters\.?|Packet Filters:\s*None\.?)\s*$') { throw "Existing Packet Monitor filters found or output not recognized. No changes made.`n$filters" }
$root = Split-Path $PSScriptRoot -Parent
$session = Join-Path $root ('recordings\network-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory -Path $session)
$etl = Join-Path $session 'packets.etl'
$pcap = Join-Path $session 'packets.pcapng'
$ownedFilters = @()
$started = $false
$stopped = $false
$recordingExit = -1
$metadata = [ordered]@{ processId=$target.Id; processName=$target.ProcessName; startUtc=[datetime]::UtcNow.ToString('o'); remotePort=$RemotePort; endpoints=@($endpoints | Select-Object LocalAddress,LocalPort,RemoteAddress,RemotePort); capture='Full packets, NIC components only, 128 MB circular limit'; phaseRecorderExitCode=$null }
try {
    foreach ($endpoint in $endpoints) {
        $name = 'Kathana-' + [guid]::NewGuid().ToString('N').Substring(0,12)
        # Both ports plus remote IP constrain this to the observed connection tuple.
        Invoke-PacketMonitor -Arguments @('filter','add',$name,'-t','TCP','-i',[string]$endpoint.RemoteAddress,'-p',[string]$endpoint.LocalPort,[string]$endpoint.RemotePort) | Out-Host
        $ownedFilters += $name
    }
    $metadata | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $session 'capture.json')
    Invoke-PacketMonitor -Arguments @('start','--capture','--comp','nics','--pkt-size','0','--file-name',$etl,'--file-size','128','--log-mode','circular') | Out-Host
    $started = $true
    Write-Host 'Capture started. Follow the spoken prompts. Keep this window open until completion.' -ForegroundColor Cyan
    $recorderArguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Record-KathanaSession.ps1'),'-TargetProcessId',$target.Id,'-WatchPath',$watchPath,'-OutputRoot',$session)
    if ($ControlledAction) { $recorderArguments += @('-ControlledAction',$ControlledAction,'-ActionKey',$ActionKey) }
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @recorderArguments
    $recordingExit = $LASTEXITCODE
} finally {
    if ($started) {
        try { Invoke-PacketMonitor -Arguments @('stop') | Set-Content -Encoding UTF8 (Join-Path $session 'capture-stop.txt'); $stopped = $true }
        catch { Write-Warning "Could not stop capture: $_. Run pktmon stop in an administrator terminal." }
    }
    if ($ownedFilters.Count -gt 0) {
        # This Windows version only supports clearing the filter table. It was
        # required to be empty before this exclusive recording session began.
        try { Invoke-PacketMonitor -Arguments @('filter','remove') | Out-Host }
        catch { Write-Warning "Could not clear temporary capture filters: $_" }
    }
    $metadata.phaseRecorderExitCode = $recordingExit
    $metadata.endUtc = [datetime]::UtcNow.ToString('o')
    $metadata.captureStopped = $stopped
    $metadata | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $session 'capture.json')
    if ($stopped -and (Test-Path -LiteralPath $etl)) {
        try { Invoke-PacketMonitor -Arguments @('etl2pcap',$etl,'--out',$pcap) | Out-Host
        }
        catch { Write-Warning "Conversion failed; packets.etl was preserved: $_" }
    }
    Write-Host "Results: $session" -ForegroundColor Green
}
if ($recordingExit -ne 0) { throw 'Phase recording did not complete. Any partial capture has been preserved.' }
if (!(Test-Path -LiteralPath $pcap)) { throw 'PCAP conversion did not complete. See packets.etl and warnings.' }
