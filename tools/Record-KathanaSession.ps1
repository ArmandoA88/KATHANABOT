[CmdletBinding()]
param(
    [int]$TargetProcessId,
    [string]$WatchPath = 'C:\Program Files (x86)\Steam\steamapps\common\Kathana',
    [string]$OutputRoot = '',
    [ValidateRange(1,300)][int]$PhaseSeconds = 30,
    [ValidateRange(0,60)][int]$PreparationSeconds = 10,
    [ValidateRange(1,30)][int]$SampleSeconds = 2,
    [switch]$Silent,
    [ValidateSet('Inventory','Move','Skill')][string]$ControlledAction,
    [ValidateRange(1,254)][int]$ActionKey = 73
)
$ErrorActionPreference = 'Stop'
# Resolve after parameter binding: Windows PowerShell -File can evaluate defaults
# before PSScriptRoot is available.
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'recordings'
}
if ($TargetProcessId) { $target = Get-Process -Id $TargetProcessId } else {
    $targets = @(Get-Process -Name KathanaGame -ErrorAction SilentlyContinue)
    if ($targets.Count -ne 1) { throw 'Open Kathana first. For multiple copies, supply -TargetProcessId with the game PID.' }
    $target = $targets[0]
}
$WatchPath = (Resolve-Path -LiteralPath $WatchPath).Path
$targetStart = $target.StartTime.ToUniversalTime()
$session = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($session.StartsWith($WatchPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the monitored folder.' }
[void](New-Item -ItemType Directory -Path $session -Force)
$writer = New-Object IO.StreamWriter (Join-Path $session 'events.jsonl'), $false, ([Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
$phaseRows = New-Object 'Collections.Generic.List[object]'
$connectionRows = New-Object 'Collections.Generic.List[object]'
$fileRows = New-Object 'Collections.Generic.List[object]'
$warnings = New-Object 'Collections.Generic.List[string]'
$subscriptions = @()
$watcher = $null
$speech = $null
$completed = $false
$phase = 'preparation'
$keyObserver = $null
$keyWriter = $null

function Drain-ActionKeys {
    if (!$keyObserver) { return }
    $entry = $null
    while ($keyObserver.Events.TryDequeue([ref]$entry)) { $keyWriter.WriteLine($entry) }
}

function Write-Record($record) { $writer.WriteLine(($record | ConvertTo-Json -Compress -Depth 6)) }
function Warn-Recorder([string]$message) {
    $warnings.Add($message)
    Write-Record ([ordered]@{ type='warning'; utc=[datetime]::UtcNow.ToString('o'); phase=$phase; message=$message })
    Write-Warning $message
}
function Announce([string]$message) {
    Write-Host $message -ForegroundColor Cyan
    if ($speech) { [void]$speech.SpeakAsync($message) }
}
function Drain-Files {
    foreach ($id in $subscriptions) {
        foreach ($eventItem in @(Get-Event -SourceIdentifier $id -ErrorAction SilentlyContinue)) {
            $argsValue = $eventItem.SourceEventArgs
            $eventUtc = $eventItem.TimeGenerated.ToUniversalTime()
            $eventPhase = 'preparation'
            foreach ($p in $phaseRows) { if ($eventUtc -ge [datetime]::Parse($p.startUtc).ToUniversalTime()) { $eventPhase = $p.phase } }
            if ($argsValue -is [IO.ErrorEventArgs]) {
                Warn-Recorder ('File watcher lost events or failed: ' + $argsValue.GetException().Message)
            } else {
                $oldPath = if ($argsValue -is [IO.RenamedEventArgs]) { $argsValue.OldFullPath } else { '' }
                $row = [pscustomobject][ordered]@{ type='file'; utc=$eventUtc.ToString('o'); phase=$eventPhase; change=$argsValue.ChangeType.ToString(); path=$argsValue.FullPath; oldPath=$oldPath }
                $fileRows.Add($row)
                Write-Record $row
            }
            Remove-Event -EventIdentifier $eventItem.EventIdentifier
        }
    }
}
function Sample-Network {
    foreach ($protocol in @('TCP','UDP')) {
        $stamp = [datetime]::UtcNow.ToString('o')
        try {
            # Query then filter avoids treating 'no endpoints for this PID' as a CIM error.
            $items = if ($protocol -eq 'TCP') { @(Get-NetTCPConnection -ErrorAction Stop | Where-Object OwningProcess -eq $target.Id) } else { @(Get-NetUDPEndpoint -ErrorAction Stop | Where-Object OwningProcess -eq $target.Id) }
            foreach ($item in $items) {
                $remoteAddress = if ($protocol -eq 'TCP') { $item.RemoteAddress } else { '' }
                $remotePort = if ($protocol -eq 'TCP') { $item.RemotePort } else { '' }
                $state = if ($protocol -eq 'TCP') { $item.State.ToString() } else { 'Bound' }
                $row = [pscustomobject][ordered]@{ type='connection'; utc=$stamp; phase=$phase; processId=$target.Id; protocol=$protocol; localAddress=$item.LocalAddress; localPort=$item.LocalPort; remoteAddress=$remoteAddress; remotePort=$remotePort; state=$state }
                $connectionRows.Add($row)
                Write-Record $row
            }
            Write-Record ([ordered]@{ type='sample'; utc=$stamp; phase=$phase; protocol=$protocol; count=$items.Count })
        } catch { Warn-Recorder ($protocol + ' sampling failed: ' + $_.Exception.Message) }
    }
}

try {
    if ($ControlledAction) {
        Add-Type -Path (Join-Path $PSScriptRoot 'ActionKeyObserver.cs')
        $keyWriter = New-Object IO.StreamWriter (Join-Path $session 'action-keys.csv'), $false, ([Text.UTF8Encoding]::new($false))
        $keyWriter.AutoFlush = $true
        $keyWriter.WriteLine('utc,virtualKey,event')
        $keyObserver = New-Object KathanaActionKeyObserver ([uint32]$target.Id), $ActionKey
        Write-Record ([ordered]@{ type='actionObserver'; utc=[datetime]::UtcNow.ToString('o'); action=$ControlledAction; virtualKey=$ActionKey; nominalPollMilliseconds=5; note='Observed key state, not proof of game acceptance. Very short presses may be missed.' })
    }
    Write-Record ([ordered]@{ type='session'; utc=[datetime]::UtcNow.ToString('o'); processId=$target.Id; processName=$target.ProcessName; processStartUtc=$targetStart.ToString('o'); watchPath=$WatchPath; phaseSeconds=$PhaseSeconds; sampleSeconds=$SampleSeconds })
    $watcher = New-Object IO.FileSystemWatcher $WatchPath
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [IO.NotifyFilters]'FileName, DirectoryName, LastWrite, Size'
    $watcher.InternalBufferSize = 65536
    foreach ($kind in @('Changed','Created','Deleted','Renamed','Error')) {
        $id = 'KathanaRecorder.' + [guid]::NewGuid().ToString('N')
        [void](Register-ObjectEvent -InputObject $watcher -EventName $kind -SourceIdentifier $id)
        $subscriptions += $id
    }
    $watcher.EnableRaisingEvents = $true
    if (!$Silent) {
        try { Add-Type -AssemblyName System.Speech; $speech = New-Object System.Speech.Synthesis.SpeechSynthesizer }
        catch { Warn-Recorder 'Voice prompts unavailable. Follow the phase labels in this console.' }
    }
    Write-Host "Recording to $session"
    Announce "Switch to the game. Recording starts in $PreparationSeconds seconds."
    Start-Sleep -Seconds $PreparationSeconds
    $instructions = [ordered]@{
        idle='Idle baseline. Do nothing.'
        inventory='Inventory phase. Open and close your inventory.'
        movement='Movement phase. Move your character normally.'
        skill='Skill phase. Use one skill a few times.'
        recovery='Recovery phase. Stop actions and remain idle.'
    }
    if ($ControlledAction) {
        $instructions = [ordered]@{ idle='Idle baseline. Do nothing.' }
        for ($trial = 1; $trial -le 5; $trial++) {
            $instructions["trial-$trial"] = "Trial $trial. Perform $ControlledAction once now using your chosen key, then remain idle."
        }
        $instructions['recovery'] = 'Recovery. Remain idle.'
    }
    foreach ($name in $instructions.Keys) {
        $phase = $name
        $phaseRow = [pscustomobject]@{ phase=$phase; startUtc=[datetime]::UtcNow.ToString('o'); endUtc=''; instruction=$instructions[$name] }
        $phaseRows.Add($phaseRow)
        Write-Record ([ordered]@{ type='phaseStart'; utc=$phaseRow.startUtc; phase=$phase; instruction=$phaseRow.instruction })
        Announce ($phaseRow.instruction + " $PhaseSeconds seconds.")
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $nextSample = 0.0
        while ($timer.Elapsed.TotalSeconds -lt $PhaseSeconds) {
            Drain-ActionKeys
            $target.Refresh()
            if ($target.HasExited) { throw 'Target process exited; recording stopped.' }
            Drain-Files
            if ($timer.Elapsed.TotalSeconds -ge $nextSample) {
                Sample-Network
                $nextSample = $timer.Elapsed.TotalSeconds + $SampleSeconds
            }
            Start-Sleep -Milliseconds 100
        }
        $phaseRow.endUtc = [datetime]::UtcNow.ToString('o')
        Write-Record ([ordered]@{ type='phaseEnd'; utc=$phaseRow.endUtc; phase=$phase })
    }
    $completed = $true
    Announce 'Recording complete. You can continue playing.'
} catch { Warn-Recorder $_.Exception.Message }
finally {
    if ($keyObserver) { $keyObserver.Dispose(); Drain-ActionKeys }
    if ($keyWriter) { $keyWriter.Dispose() }
    if ($watcher) { $watcher.EnableRaisingEvents = $false; Drain-Files; $watcher.Dispose() }
    foreach ($id in $subscriptions) { Unregister-Event -SourceIdentifier $id -ErrorAction SilentlyContinue }
    if ($speech) { $speech.SpeakAsyncCancelAll(); $speech.Dispose() }
    $phaseRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $session 'phases.csv')
    $connectionRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $session 'connections.csv')
    $fileRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $session 'file-changes.csv')
    $report = New-Object 'Collections.Generic.List[string]'
    $report.Add('# Kathana observation session')
    $report.Add('')
    $report.Add("Completed: $completed. Process: $($target.ProcessName), PID $($target.Id). All timestamps are UTC.")
    $report.Add('')
    $report.Add('File events are folder changes, not proof that this process wrote them. Reads are not recorded. Network samples show endpoints, not packet contents or per-action traffic. Brief connections can be missed. Phase timing can overrun while Windows collects a sample. Repeated file notifications are not separate gameplay actions. An endpoint is not evidence of a supported game API.')
    $report.Add('')
    $report.Add('| Phase | Connection observations | Distinct changed paths |')
    $report.Add('| --- | ---: | ---: |')
    foreach ($p in $phaseRows) {
        $connections = @($connectionRows | Where-Object phase -eq $p.phase)
        $files = @($fileRows | Where-Object phase -eq $p.phase | Select-Object -ExpandProperty path -Unique)
        $report.Add("| $($p.phase) | $($connections.Count) | $($files.Count) |")
        foreach ($f in $files) { Write-Record ([ordered]@{ type='changedPathSummary'; phase=$p.phase; path=$f }) }
    }
    $report.Add('')
    $report.Add('## Endpoint comparison')
    $report.Add('')
    $report.Add('Grouped by protocol, addresses, ports and state. Phase presence is correlation only; sample counts are not traffic volumes.')
    foreach ($group in @($connectionRows | Group-Object protocol,localAddress,localPort,remoteAddress,remotePort,state)) {
        $seen = ($group.Group.phase | Select-Object -Unique) -join ', '
        $report.Add('- ' + $group.Name + ' | phases: ' + $seen)
    }
    $report.Add('')
    $report.Add('## Changed paths')
    foreach ($group in @($fileRows | Group-Object path)) { $report.Add('- ' + $group.Name + ' | phases: ' + (($group.Group.phase | Select-Object -Unique) -join ', ')) }
    $report.Add('')
    $report.Add('## Warnings')
    foreach ($message in $warnings) { $report.Add('- ' + $message) }
    $report | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $session 'summary.md')
    Write-Record ([ordered]@{ type='sessionEnd'; utc=[datetime]::UtcNow.ToString('o'); completed=$completed; warningCount=$warnings.Count })
    $writer.Dispose()
    Write-Host "Saved: $session\summary.md"
}
if (!$completed) { exit 1 }
