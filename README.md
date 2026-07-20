# Kathana Bot

KathanaBot is a self-contained VB WinForms application. Both the Velopack-installed build and the timestamped standalone EXE can update from GitHub Releases. The standalone build checks GitHub directly, verifies the downloaded EXE with SHA-256, replaces itself after closing, and reopens automatically.

- No API URL required.
- No Python backend required.
- Bot logic runs inside the UI executable.

## Build / Rebuild

```powershell
dotnet build .\ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj -c Release
```

## Build the Installer and Update Packages

```powershell
.\build-velopack-release.ps1
```

This reads the version from the project, publishes a self-contained `win-x64` app, and creates Velopack setup/full/portable files in `dist\velopack\Releases`. When an earlier release exists, Velopack also creates a smaller delta package. The script copies a uniquely versioned installer to the repository root without overwriting older builds.

Install KathanaBot once with the new `KathanaBot-Setup-vX.Y.Z-<timestamp>.exe`. Installed copies can then use the `Update` tab to:

- check GitHub Releases manually or at startup;
- display an available version and package details;
- download with visible progress;
- stop an active bot safely, apply the update, and restart;
- optionally include prerelease versions.

The standalone EXE does not require Setup. It downloads the release asset named `KathanaBotControlPanel-win-x64-standalone.exe` and its `.sha256` file, verifies the download, safely stops the bot, replaces the running EXE, and reopens it. No permanent companion files are required.

## Publish an Update on GitHub

1. Commit and push the code containing the new version to the `agent-ai` branch.
2. In GitHub, open `Actions` > `Build and publish Velopack release` > `Run workflow`.
3. Enter the same semantic version as the project, such as `1.0.44`.
4. The workflow checks out `agent-ai`, builds the app, and publishes all required Velopack assets to a `v1.0.44` GitHub Release targeting that branch.
5. Installed users receive the update on their next startup check or when they press `Check Now`.

GitHub's built-in `GITHUB_TOKEN` is used by the workflow, so no paid update service or separate secret is required for a public repository. To package and publish from a local PowerShell session instead, set `GITHUB_TOKEN` and run:

```powershell
.\build-velopack-release.ps1 -Version 1.0.44 -Publish
```

### Validation and First Update Test

Validation: the Release build succeeded with zero errors and zero warnings.

To activate online updates, push these changes to `agent-ai` and publish version `1.0.43` using the `Build and publish Velopack release` GitHub workflow. Later, increase the application version and publish `1.0.44`; the standalone `1.0.43` EXE will detect it from the `Update` tab and can replace itself automatically.

To activate internet updates:

1. Commit and push these changes to the `agent-ai` branch.
2. Run the `Build and publish Velopack release` workflow in GitHub Actions with version `1.0.43`.
3. Run either the generated Setup installation or the standalone `1.0.43` EXE. The standalone EXE requires no installation.
4. Increase the application version and publish `1.0.44` from `agent-ai` using the same workflow.
5. Start the installed `1.0.43` application or press `Check Now` in the `Update` tab. It should report that version `1.0.44` is available.
6. Press `Update and Restart` to verify downloading, SHA-256 validation, safe bot shutdown, replacement/installation, and relaunch.

This process follows Velopack's official [GitHub Actions distribution flow](https://docs.velopack.io/distributing/github-actions).

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

When the selected Kathana game window is in the foreground, a small button appears in its client area. `BOT OFF` is red and starts Full, while `LITE BOT OFF` starts Lite; the green `BOT ON` / `LITE BOT ON` state stops that same edition. The overlay remains assigned to Lite after stopping it, even if the control panel is showing a Full tab. Drag the button to move it, or drag its bottom-right grip to resize it. Position and size are saved.

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
- `Vision`: window title, loop settings, calibration regions, snapshot, automatic screenshot interval/folder, low-opacity overlay
- `Auto-Pot`: quick trigger updates for heal/mana rows + HP=0 alarm volume + test alarm + phone alert test
- `Unstuck`: retarget interval helper
- `Diagnostics`: live bot status
- `Update`: GitHub/Velopack update settings, startup checks, release status, download progress, and Update and Restart

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
13. Optional: beneath the Vision snapshot, enable `Automatic Screenshots`, choose an interval from 1 to 999 minutes, and use `Browse...` to select the save folder. `Open Folder` opens that destination in File Explorer. The default folder is `Pictures\KathanaBot`.

### Default calibration coordinates (saved baseline)

Use this baseline if calibration is reset:

- `hp_bar`: `x=1, y=22, w=218, h=14`
- `mp_bar`: `x=3, y=39, w=216, h=10`
- `mob_name_rect`: `x=0, y=53, w=218, h=22`
- `mob_hp_rect`: `x=0, y=78, w=215, h=12`
- `mob_life_rect`: `x=0, y=78, w=215, h=12`

The default game window is `Kathana - The Reign of Shadow` from process `KathanaGame`. PrintWindow captures are normalized to the client area before HP/MP, target-bar, and OCR processing.

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
- Skill cooldown scheduling uses a monotonic per-action timer. Full Combat reports each blocked key with its remaining cooldown instead of leaving all skills stuck after a system-clock adjustment or unrelated use of the same key.
- The multilingual startup Notice closes automatically after five seconds; its OK button remains available for immediate dismissal.
- If the EXE is already running, rebuild can warn about locked files.
- Keep EXE builds serialized/versioned. Do not replace an older EXE with a new one using the same filename.
