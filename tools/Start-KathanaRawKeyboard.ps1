[CmdletBinding()]
param([switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Kathana\KathanaGame.exe'
$reportDirectory = Join-Path $workspaceRoot 'input-diagnostics'
$reportStamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff')
$reportMode = if ($CheckOnly) { 'check-only' } else { 'launch' }
$reportPath = Join-Path $reportDirectory ('kathana-raw-keyboard-' + $reportMode + '_' + $reportStamp + '.json')
$latestReportPath = Join-Path $reportDirectory 'kathana-raw-keyboard-launch.json'
$launchErrors = [System.Collections.Generic.List[string]]::new()
$launchReport = [ordered]@{
    mode = $reportMode
    scope = 'Child-process launch environment only; same Windows session. No input, hooks, permanent environment changes or game-file changes.'
    startedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    finishedUtc = $null
    gamePath = $gamePath
    gameSha256 = $null
    gameVersion = $null
    windowsSessionId = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    requestedEnvironment = [ordered]@{
        SDL_WINDOWS_RAW_KEYBOARD = '1'
        SDL_WINDOWS_RAW_KEYBOARD_INPUTSINK = '1'
    }
    gameProcessesBefore = @()
    gameProcessesAfter = @()
    steamProcessesBefore = @()
    launchedProcessId = $null
    launchedProcessStartUtc = $null
    launchedProcessStillRunning = $false
    launchedProcessExitCode = $null
    replacementDetected = $false
    environmentPropagation = 'not-launched'
    effectiveSdlSettings = 'unverified'
    status = 'preflight'
    errors = $launchErrors
}

function Get-KathanaProcessRecords {
    $records = @()
    foreach ($gameProcess in @(Get-Process -Name KathanaGame -ErrorAction SilentlyContinue)) {
        $imagePath = $null
        try { $imagePath = $gameProcess.Path } catch { }
        $records += [ordered]@{
            pid = $gameProcess.Id
            path = $imagePath
            windowsSessionId = $gameProcess.SessionId
        }
    }
    return $records
}

function Save-LaunchReport {
    [System.IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
    $json = $launchReport | ConvertTo-Json -Depth 8
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($reportPath, $json, $utf8)
    if (!$CheckOnly) { [System.IO.File]::WriteAllText($latestReportPath, $json, $utf8) }
}

$launchExit = 0
try {
    if (!(Test-Path -LiteralPath $gamePath -PathType Leaf)) {
        throw 'The confirmed Steam Kathana executable was not found.'
    }
    $gameFile = Get-Item -LiteralPath $gamePath
    $launchReport.gameSha256 = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    $launchReport.gameVersion = $gameFile.VersionInfo.FileVersion
    $launchReport.gameProcessesBefore = @(Get-KathanaProcessRecords)
    $launchReport.steamProcessesBefore = @(
        Get-Process -Name steam -ErrorAction SilentlyContinue |
            Where-Object { $_.SessionId -eq $launchReport.windowsSessionId } |
            ForEach-Object { [ordered]@{ pid = $_.Id; windowsSessionId = $_.SessionId } }
    )
    if ($CheckOnly) {
        $launchReport.status = 'check-only-completed'
        Write-Host 'Read-only check completed. No game was started and no settings were changed.'
        Write-Host ('Running Kathana processes: ' + $launchReport.gameProcessesBefore.Count)
    } else {
        if ($launchReport.gameProcessesBefore.Count -ne 0) {
            throw 'Close Kathana normally, then run this launcher again. It will not close or replace an existing game process.'
        }
        if ($launchReport.steamProcessesBefore.Count -eq 0) {
            throw 'Open Steam normally in this Windows session first, then run this launcher. It will not start a new Steam client with the test environment.'
        }
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $gamePath
        $startInfo.WorkingDirectory = $gameFile.DirectoryName
        $startInfo.UseShellExecute = $false
        $startInfo.EnvironmentVariables['SDL_WINDOWS_RAW_KEYBOARD'] = '1'
        $startInfo.EnvironmentVariables['SDL_WINDOWS_RAW_KEYBOARD_INPUTSINK'] = '1'
        $startedGame = [System.Diagnostics.Process]::Start($startInfo)
        if ($null -eq $startedGame) { throw 'Windows did not return the started game process.' }
        try {
            $launchReport.launchedProcessId = $startedGame.Id
            $launchReport.environmentPropagation = 'passed-to-started-process'
            try { $launchReport.launchedProcessStartUtc = $startedGame.StartTime.ToUniversalTime().ToString('o') } catch { }
            # Observe only: Steam may exit this process and replace it. A replacement
            # cannot be assumed to inherit these two child-only variables.
            $null = $startedGame.WaitForExit(3000)
            $startedGame.Refresh()
            $launchReport.launchedProcessStillRunning = !$startedGame.HasExited
            if ($startedGame.HasExited) { $launchReport.launchedProcessExitCode = $startedGame.ExitCode }
            $launchReport.gameProcessesAfter = @(Get-KathanaProcessRecords)
            $otherGameProcesses = @($launchReport.gameProcessesAfter | Where-Object { $_.pid -ne $launchReport.launchedProcessId })
            $launchReport.replacementDetected = $otherGameProcesses.Count -ne 0
            if (!$launchReport.launchedProcessStillRunning -or $launchReport.replacementDetected) {
                $launchReport.environmentPropagation = 'unknown-for-replacement'
                $launchReport.status = 'launch-not-verified'
                $launchExit = 2
                Write-Host 'The started process exited or another Kathana process appeared. The inventory probe will refuse this launch report.'
                Write-Host 'The game was left as it is. Review the report; background SDL settings are unverified.'
            } else {
                $launchReport.status = 'started-process-retained'
                Write-Host ('Kathana started as PID ' + $startedGame.Id + ' in this Windows session.')
                Write-Host 'Both raw-keyboard variables were passed to this process; effective SDL/game behavior remains unverified.'
                Write-Host 'Log into the game normally. Then run tools\GameInventoryProbe\Run-GameInventoryProbe.cmd.'
            }
        } finally { $startedGame.Dispose() }
    }
} catch {
    $launchExit = 2
    $launchReport.status = 'refused-or-failed'
    $launchErrors.Add($_.Exception.Message)
    Write-Host $_.Exception.Message
} finally {
    $launchReport.finishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Save-LaunchReport
    Write-Host ('Report: ' + $reportPath)
}
exit $launchExit
