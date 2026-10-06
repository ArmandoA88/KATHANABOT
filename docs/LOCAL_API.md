# Local bot API

Run `KathanaBotControlPanel_Standalone_1.0.231.exe` and select the game process in the bot. Close older bot copies first. The API starts automatically and listens on a dynamically assigned **127.0.0.1** port. The bot log shows the address and session-file location. It stops when the bot closes.

This controls the bot, not an official Kathana game API. Version 1.0.231 sends background keyboard messages to the exact installed Kathana executable without a DLL connection or foreground activation. Keep the game open and not minimized; another app can stay active. Short key taps sample 150-250 ms and preserve explicit left/right modifiers. Mouse and text output are unavailable; `/click` returns an error before any input.

Status identifies the selected method as `background-keys-posted-scan`, `background-keys-send-scan`, `background-keys-posted-zero` or `background-keys-send-zero`. Stop automation and select a method with the Background keys button, or start with `--background-key-mode posted-scan|send-scan|posted-zero|send-zero`. Posted scan codes are the default. API success establishes delivery/enqueueing, not live skill acceptance or enforcement compatibility.

The user subsequently reported suspicious-activity termination using v1.0.231, with posted scan codes selected. See S03 in [the experiment log](../BACKGROUND_INPUT_EXPERIMENTS.md). An empty API `inputError` does not establish that the game's session remains connected, and selected process IDs must be rechecked after a game restart.

From the repository root in PowerShell:

```powershell
.\tools\Invoke-BotApi.ps1 status
.\tools\Invoke-BotApi.ps1 start
.\tools\Invoke-BotApi.ps1 stop
# Replace 1234 with processId from status; another app may remain active.
.\tools\Invoke-BotApi.ps1 key -GameProcessId 1234 -Key 65
```

`65` is virtual-key A. Key taps hold for at least 150 ms. Manual key requests require other automation to be stopped. This keyboard-only release rejects click requests before sending any input. Start/stop use the same controls as the bot UI, including the existing stop-movement behavior.

For your own client, read `%LOCALAPPDATA%\KathanaBot\api\<botProcessId>.json`. It contains `address`, `token`, and `botProcessId`. Send `Authorization: Bearer <token>` with every request. Tokens change on each launch; treat the file as a local credential. Multiple bot copies have separate files and ports; the helper accepts `-BotProcessId` to choose one. Crashes can leave stale session files; the helper filters out exited processes.

| Method | Path | JSON body | Result |
| --- | --- | --- | --- |
| GET | /status | none | running, processId, windowTitle, inputMode, inputError, botProcessId |
| POST | /start | none | Start selected full bot using current UI configuration |
| POST | /stop | none | Stop full combat bot (not all independent workflows) |
| POST | /key | `{"processId":1234,"key":65}` | Target-only background key tap |
| POST | /click | `{"processId":1234}` | Rejected in this keyboard-only release |

Success returns HTTP 200 and current status. HTTP 401 means missing/incorrect token or a browser Origin header; 404 means unsupported route/method; 400 means malformed/oversized input; 409 means an unavailable target, conflicting mode, or rejected input; 500 means an unexpected command failure. Input bodies are limited to 2 KB. Browser origins are rejected. The transport is local HTTP and is not intended for remote access. Do not forward its port.

API integration tests exercise a fake command handler and send no game inputs. Packaged v1.0.231 inventory trials verified all four key methods, with separate tests through the Lite engine; full combat and skill acceptance remain unverified.
