$enginePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$engineContent = Get-Content -Raw -Path $enginePath -Encoding UTF8

$configOld = @"
    Public Property PartyAskText As String = ""
"@
$configNew = @"
    Public Property PartyAskText As String = ""
    Public Property LootScannerEnabled As Boolean = True
    Public Property ItemNtfyTopic As String = ""
"@

$engineContent = $engineContent -replace [regex]::Escape($configOld), $configNew

Set-Content -Path $enginePath -Value $engineContent -Encoding UTF8 -NoNewline
Write-Host "BotConfig Patched"
