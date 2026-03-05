$filePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$content = Get-Content -Raw -Path $filePath

# 1. Add _lastRightAltAt var
$content = $content -replace 'Private _zeroPairConfirmCount As Integer = 0', "Private _zeroPairConfirmCount As Integer = 0`r`n    Private _lastRightAltAt As DateTime = DateTime.MinValue"

# 2. Add RMENU to KeyMap
$content = $content -replace '\{"SPACE", \&H20\}', "{`"RMENU`", &HA5}, {`"RALT`", &HA5},`r`n        {`"SPACE`", &H20}"

# 3. Reset in Start()
$content = $content -replace '_zeroPairConfirmCount = 0\r?\n            _task = Task\.Run', "_zeroPairConfirmCount = 0`r`n            _lastRightAltAt = DateTime.MinValue`r`n            _task = Task.Run"

# 4. Add the 10s loop logic
$findLoop = [regex]::Escape("Dim now As DateTime = DateTime.UtcNow") + "\r?\n(\s*)SavePeriodicSnapshot\(frame, now\)"
$replaceLoop = "Dim now As DateTime = DateTime.UtcNow`r`n`r`n`$1If (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then`r`n`$1    If SendKey(hwnd, `"RMENU`", 200) Then`r`n`$1        _lastRightAltAt = now`r`n`$1        SetLastAction(`"RMENU (auto right-alt)`")`r`n`$1        RaiseEvent LogLine(`"Auto right-alt sent.`")`r`n`$1    End If`r`n`$1End If`r`n`r`n`$1SavePeriodicSnapshot(frame, now)"
$content = [regex]::Replace($content, $findLoop, $replaceLoop)

Set-Content -Path $filePath -Value $content -NoNewline
Write-Host "Patch applied using PowerShell."
