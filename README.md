# Kathana Bot (Single EXE)

This app now runs as a single VB WinForms executable.

- No API URL required.
- No Python backend required.
- Bot logic runs inside the UI executable.

## Build / Rebuild

```powershell
dotnet build .\ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj -c Release
```

## Publish Standalone EXE

```powershell
$version = Get-Date -Format "yyyyMMdd_HHmmss"
$publishDir = ".\dist\versions\$version"
dotnet publish .\ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o $publishDir
Copy-Item "$publishDir\KathanaBotControlPanel.exe" ".\dist\versions\KathanaBotControlPanel_$version.exe"
```

The standalone output is copied to a versioned EXE:

- `KATHANABOT\dist\versions\KathanaBotControlPanel_yyyyMMdd_HHmmss.exe`

Important release rule: never overwrite old EXE builds. Every release/test EXE must be saved as a separate versioned file so older working builds can be restored.

## Run

Double-click:

- the newest versioned EXE in `KATHANABOT\dist\versions`

## Discord `shot` Command

The app can watch one Discord data channel for the text command `shot`. When it sees that command, it uploads the newest rolling screenshot to the Discord Stats/Data webhook channel.

Discord setup:

1. In Discord Developer Portal, create an application and add a bot.
2. Enable the bot's Message Content Intent.
3. Invite the bot to your server.
4. In the data channel, give the bot `View Channel`, `Read Message History`, and `Send Messages`.
5. Turn on Discord Developer Mode, right-click the data channel, and copy the channel ID.
6. In KathanaBot `Auto-Pot` > notifications, set provider to `discord`, set the Stats webhook for the data channel, paste the bot token into `Discord Bot Token (Shot)`, and paste the copied channel ID into `Discord Data Channel ID`.

Type `shot` in that data channel. The image is posted through the Stats webhook. The app must be running, and the rolling screenshot folder must have at least one screenshot.

## Tabs

- `Combat`: key matrix, priorities/cooldowns, start/stop, realtime log, live detected mob name, monster + loot filter
- `Vision`: window title, loop settings, calibration regions, snapshot, low-opacity overlay
- `Auto-Pot`: quick trigger updates for heal/mana rows + HP=0 alarm volume + test alarm + phone alert test
- `Unstuck`: retarget interval helper
- `Diagnostics`: live bot status

## Calibration

1. Open game in windowed mode at `1024x768`, DPI `100%`.
2. In `Vision`, click `Capture Snapshot`.
3. Set regions (`x,y,w,h`) for:
   - `hp_bar`
   - `mp_bar`
   - `mob_name_rect`
   - `mob_hp_rect`
4. In `Combat`, configure keys (`1..0` and optional `F1..F10`), roles, cooldowns, and priorities.
5. Optional: use `Ignore Skill Min HP/MP` button in Combat to ignore per-key MinHP/MinMP gating.
6. Optional: use `Auto Retarget If Stuck` in Combat to auto-send `E` when target HP stays unchanged (prevents getting stuck on non-attackable targets).
7. Optional: use `Retarget Now (E)` in Combat for an immediate manual retarget.
8. Monster blacklist is enforced from `Monster Filter` when enabled; detected mob name is shown live in Combat status.
9. Optional: enable `Loot Pickup Filter (F)` and set interval seconds in `Combat`; names in the filter list are also used as allowed loot names.
10. Loot logic: bot presses `F`, waits about `200ms`, reads the selected name from the same name box, waits `700ms` when name is allowed, and sends random `W` or `S` when name is not on the list.
11. Optional: click `Show Overlay` in Vision to draw calibration rectangles over the game window.
12. Click `Save Settings`, then `Attack`.
13. Bot automatically saves a screenshot every 15 minutes to your Pictures gallery folder: `Pictures\KathanaBot`.

### Default calibration coordinates (saved baseline)

Use this baseline if calibration is reset:

- `hp_bar`: `x=11, y=25, w=151, h=11`
- `mp_bar`: `x=3, y=40, w=161, h=11`
- `mob_name_rect`: `x=862, y=0, w=162, h=23`
- `mob_hp_rect`: `x=859, y=20, w=165, h=11`

## Notes

- Background key input is sent directly to window title:
  `Kathana - The Coming of the Dark Ages`
- Default at startup: bot auto-starts in attacking mode and `Auto Retarget If Stuck` is ON.
- Periodic unstuck movement pulses (`W/S`) are disabled.
- Monster filter defaults: enabled with `avara kara` preloaded in blacklist.
- Default heal trigger is `80%` for the key `6` heal row.
- `HP=0 Alarm Volume %` controls loudness only; alarm trigger is fixed to HP=0.
- Real HP=0 alerts use a 60-second grace period before sound + notification (to avoid false alarms).
- Use `Test Alarm + Phone` in `Auto-Pot` to trigger alarm sound and ntfy together.
- `Test Phone Alert` and HP=0 automatic alerts publish to the ntfy channel set in `Auto-Pot` (`ntfy.sh/<your-channel>`).
- Most setting changes auto-apply while the bot is running; stop/start is not required.
- If the EXE is already running, rebuild can warn about locked files.
- Keep EXE builds serialized/versioned. Do not replace an older EXE with a new one using the same filename.
