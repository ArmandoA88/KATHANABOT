$ErrorActionPreference = 'Stop'
Write-Host 'Choose ONE action for this recording: Inventory, Move, or Skill.'
$action = Read-Host 'Action'
if ($action -notin 'Inventory','Move','Skill') { throw 'Enter Inventory, Move, or Skill.' }
Write-Host 'Choose the actual keyboard binding you will use, for example I, W, D1 (number 1), or F1.'
Add-Type -AssemblyName System.Windows.Forms
$binding = Read-Host 'Key'
$keyValue = [int]([System.Enum]::Parse([System.Windows.Forms.Keys], $binding, $true))
if ($keyValue -lt 8 -or $keyValue -gt 254) { throw 'Choose one keyboard key without modifiers.' }
Write-Host "This records only key $binding while the selected game is foreground. Use that key once per trial."
& (Join-Path $PSScriptRoot 'Record-KathanaNetwork.ps1') -ControlledAction $action -ActionKey $keyValue
