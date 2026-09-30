[CmdletBinding()]
param(
    [ValidateSet('status','start','stop','key','click')][string]$Command = 'status',
    [int]$BotProcessId,
    [int]$GameProcessId,
    [ValidateRange(8,254)][int]$Key = 65
)
$ErrorActionPreference = 'Stop'
$folder = Join-Path $env:LOCALAPPDATA 'KathanaBot\api'
$sessions = @(Get-ChildItem -LiteralPath $folder -Filter '*.json' | ForEach-Object {
    $session = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    if ((Get-Process -Id $session.botProcessId -ErrorAction SilentlyContinue) -and
        (!$BotProcessId -or $session.botProcessId -eq $BotProcessId)) { $session }
})
if ($sessions.Count -ne 1) { throw 'Open the API-enabled bot. If multiple copies are open, specify -BotProcessId.' }
$session = $sessions[0]
$parameters = @{
    Uri = "$($session.address)/$Command"
    Headers = @{ Authorization = "Bearer $($session.token)" }
    Method = $(if ($Command -eq 'status') { 'Get' } else { 'Post' })
    TimeoutSec = 15
}
if ($Command -in 'key','click') {
    if ($GameProcessId -le 0) { throw 'Specify -GameProcessId using the processId returned by status.' }
    $parameters.ContentType = 'application/json'
    $parameters.Body = @{ processId = $GameProcessId; key = $Key } | ConvertTo-Json
}
Invoke-RestMethod @parameters
