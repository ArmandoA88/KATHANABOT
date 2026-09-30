param()
$ErrorActionPreference = 'Stop'
$profile = Join-Path $env:LOCALAPPDATA 'RemoteDesktopMosaic\ChromeProfile'
$port = (Get-Content -LiteralPath (Join-Path $profile 'DevToolsActivePort'))[0]
$targets = Invoke-RestMethod -Uri "http://127.0.0.1:$port/json/list" -TimeoutSec 3
$settings = Get-Content -LiteralPath 'RemoteDesktopMosaic.settings.json' -Raw | ConvertFrom-Json
function Read-CDP($endpoint, $method, $params) {
    $socket = New-Object Net.WebSockets.ClientWebSocket
    $cancel = New-Object Threading.CancellationTokenSource
    $cancel.CancelAfter(5000)
    try {
        $socket.ConnectAsync([Uri]$endpoint,$cancel.Token).GetAwaiter().GetResult()
        $json = @{id=1;method=$method;params=$params} | ConvertTo-Json -Depth 8 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $segment = New-Object 'System.ArraySegment[byte]' -ArgumentList @(,$bytes)
        $socket.SendAsync($segment,[Net.WebSockets.WebSocketMessageType]::Text,$true,$cancel.Token).GetAwaiter().GetResult()
        while ($true) {
            $stream = New-Object IO.MemoryStream
            do {
                $buffer = New-Object byte[] 65536
                $segment = New-Object 'System.ArraySegment[byte]' -ArgumentList @(,$buffer)
                $received = $socket.ReceiveAsync($segment,$cancel.Token).GetAwaiter().GetResult()
                $stream.Write($buffer,0,$received.Count)
            } while (!$received.EndOfMessage)
            $response = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
            $stream.Dispose()
            if ($response.id -eq 1) { return $response }
        }
    } finally { $socket.Dispose(); $cancel.Dispose() }
}
$index=0
foreach ($computer in $settings.Computers) {
    $index++
    $session = ([uri]$computer.Link).AbsolutePath -replace '^/(u/\d+/)?access/session/',''
    foreach ($target in @($targets | Where-Object { $_.type -eq 'page' -and $_.url -match ('/access/session/'+[regex]::Escape($session)+'(?:[/?#]|$)') })) {
        $expression = 'JSON.stringify({ready:document.readyState,focused:document.hasFocus(),visible:document.visibilityState,active:{tag:document.activeElement?.tagName,id:document.activeElement?.id},frames:Array.from(document.querySelectorAll("iframe")).map(f=>({id:f.id,name:f.name,host:(()=>{try{return new URL(f.src).host}catch{return ""}})()})),embeds:document.querySelectorAll("embed,object").length})'
        $result = Read-CDP $target.webSocketDebuggerUrl 'Runtime.evaluate' @{expression=$expression;returnByValue=$true}
        $bounds = Read-CDP $target.webSocketDebuggerUrl 'Browser.getWindowForTarget' @{targetId=$target.id}
        [pscustomobject]@{Tile=$index;Page=($result.result.result.value | ConvertFrom-Json);Window=$bounds.result} | ConvertTo-Json -Depth 9 -Compress
    }
}
