# Local bot API

Run `KathanaBotControlPanel_Standalone_1.0.222.exe` and select the game process in the bot. The API starts automatically and listens on a dynamically assigned **127.0.0.1** port. The bot log shows the address and session-file location. It stops when the bot closes.

This controls the bot, not an official Kathana game API. Input uses the existing foreground SendInput backend. It does not deliver input to an unfocused game window or use PostMessage.

From the repository root in PowerShell:

```powershell
.\tools\Invoke-BotApi.ps1 status
.\tools\Invoke-BotApi.ps1 start
.\tools\Invoke-BotApi.ps1 stop
# Replace 1234 with processId from status. During the delay, switch to the game.
Start-Sleep -Seconds 3
.\tools\Invoke-BotApi.ps1 key -GameProcessId 1234 -Key 65
# Run separately; position the cursor over the game during the delay.
Start-Sleep -Seconds 3
.\tools\Invoke-BotApi.ps1 click -GameProcessId 1234
```

`65` is virtual-key A. Key taps send an immediate down/up pair; games that require a longer hold may not register them. Click sends a left-button down/up at the current cursor position, which must be over the selected game window. Manual key/click endpoints require other automation to be stopped. They do not activate the window. Start/stop use the same controls as the bot UI, including the existing stop-movement behavior.

For your own client, read `%LOCALAPPDATA%\KathanaBot\api\<botProcessId>.json`. It contains `address`, `token`, and `botProcessId`. Send `Authorization: Bearer <token>` with every request. Tokens change on each launch; treat the file as a local credential. Multiple bot copies have separate files and ports; the helper accepts `-BotProcessId` to choose one. Crashes can leave stale session files; the helper filters out exited processes.

| Method | Path | JSON body | Result |
| --- | --- | --- | --- |
| GET | /status | none | running, processId, windowTitle, inputMode, botProcessId |
| POST | /start | none | Start selected full bot using current UI configuration |
| POST | /stop | none | Stop full combat bot (not all independent workflows) |
| POST | /key | `{"processId":1234,"key":65}` | Foreground key tap |
| POST | /click | `{"processId":1234}` | Foreground left click |

Success returns HTTP 200 and current status. HTTP 401 means missing/incorrect token or a browser Origin header; 404 means unsupported route/method; 400 means malformed/oversized input; 409 means an unavailable target, conflicting mode, or rejected input; 500 means an unexpected command failure. Input bodies are limited to 2 KB. Browser origins are rejected. The transport is local HTTP and is not intended for remote access. Do not forward its port.

API integration tests exercise a fake command handler and send no game inputs. Actual acceptance of keys/clicks by Kathana still requires testing in the game.
