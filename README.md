# Kathana Bot (Single EXE)

This app now runs as a single VB WinForms executable.

- No API URL required.
- No Python backend required.
- Bot logic runs inside the UI executable.

## Build / Rebuild

```powershell
cd ui\KathanaBotControlPanel
dotnet build -t:Rebuild
```

After build/rebuild, output is copied to:

- `KATHANABOT\KathanaBotControlPanel.exe`

## Run

Double-click:

- `KATHANABOT\KathanaBotControlPanel.exe`

## Tabs

- `Combat`: key matrix, priorities/cooldowns, start/stop, realtime log, live detected mob name
- `Vision`: window title, loop settings, calibration regions, snapshot, low-opacity overlay
- `Auto-Pot`: quick trigger updates for heal/mana rows + HP=0 alarm volume
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
5. Optional: use `Bypass HP/MP Limits` button in Combat to ignore per-key MinHP/MinMP gating.
6. Optional: use `Bypass Stuck Target` in Combat to auto-send `E` when target HP stays unchanged (prevents getting stuck on non-attackable targets).
7. Optional: use `Retarget Now (E)` in Combat for an immediate manual retarget.
8. Monster blacklist is enforced from `Monster Filter` when enabled; detected mob name is shown live in Combat status.
9. Optional: click `Show Overlay` in Vision to draw calibration rectangles over the game window.
10. Click `Save Settings`, then `Attack`.
11. Bot automatically saves a screenshot every 15 minutes to your Pictures gallery folder: `Pictures\KathanaBot`.

### Default calibration coordinates (saved baseline)

Use this baseline if calibration is reset:

- `hp_bar`: `x=11, y=25, w=151, h=11`
- `mp_bar`: `x=3, y=40, w=161, h=11`
- `mob_name_rect`: `x=862, y=0, w=162, h=23`
- `mob_hp_rect`: `x=859, y=20, w=165, h=11`

## Notes

- Background key input is sent directly to window title:
  `Kathana - The Coming of the Dark Ages`
- Default at startup: bot auto-starts in attacking mode and `Bypass Stuck Target` is ON.
- Default unstuck movement pulse: while bot is running, it sends `W` every 10 seconds, then `S` after the next 10 seconds, alternating continuously.
- Monster filter defaults: enabled with `avara kara` preloaded in blacklist.
- Default heal trigger is `80%` for the key `6` heal row.
- Use `Test HP=0 Alarm` in `Auto-Pot` to preview current alarm loudness.
- If the EXE is already running, rebuild can warn about locked files.
