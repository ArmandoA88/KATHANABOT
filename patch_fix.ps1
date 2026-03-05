$filePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$content = Get-Content -Raw -Path $filePath -Encoding UTF8

$pattern = '(?s)[ \t]*<DllImport\("user32\.dll", SetLastError:=True\)>[\r\n \t]*Friend Function GetForegroundWindow\(\) As IntPtr[\r\n \t]*End Function'
$content = $content -replace $pattern, ""

$insertPoint = "    Friend Delegate Function EnumWindowsProc"
$newImport = @"
    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetForegroundWindow() As IntPtr
    End Function

    Friend Delegate Function EnumWindowsProc
"@

$content = $content -replace $insertPoint, $newImport

Set-Content -Path $filePath -Value $content -Encoding UTF8 -NoNewline
Write-Host "Cleanup complete."
