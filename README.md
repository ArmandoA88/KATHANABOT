# Kathana Bot

KathanaBot is a self-contained VB WinForms application. Both the Velopack-installed build and the timestamped standalone EXE can update from GitHub Releases. The standalone build checks GitHub directly, verifies the downloaded EXE with SHA-256, replaces itself after closing, and reopens automatically.

- No API URL required.
- No Python backend required.
- Bot logic runs inside the UI executable.
- Vision includes an optional Dadati evade: block attacks on the detected Dadati, tap W/S to reposition, and force an E retarget.
- Every Full or Lite bot start checks for updates asynchronously; the Update tab is green when current and yellow when a newer version is available.

## Build / Rebuild

Version 1.0.250 adds **Background mode** (Combat Full, under Stop Bot; on by default). The game window can stay
behind other windows: for each key press the bot briefly takes the keyboard, keeps the game window where it was
in the window stack, sends the key with the same foreground SendInput, and gives focus back to the window you
were using. It waits for you to stop typing (default 1 s), never interrupts full-screen apps or presentations,
and is keyboard-only (mouse features are paused while it is on). Windows only delivers key presses to the active
window, so this borrows the keyboard instead of sending to an inactive window; it is not a detection fix and no
live game session was run. Details, measurements and limits: [Background mode](docs/BACKGROUND_MODE.md).
Turn it off for the 1.0.249 behavior below.

Version 1.0.249 fixes Home never becoming visible at launch: the first-run Activity & Setup handler
selected a page the sidebar removes, leaving no tab selected, so Home showed stale
stopped/pink pixels and no live stats while the bot ran. Home is now always selected.
Version 1.0.248 immediately synchronizes Home's active status, play/pause control,
green frame and sidebar indicator with the running engine at startup. It clears
the initial stopped tint without requiring a tab change, even before the first
game or HP reading. Late status callbacks cannot repaint a running bot as stopped.

This is the **Foreground SendInput** build. Every game key, click,
cursor movement and text request uses the same foreground input backend;
saved background-method preferences cannot switch it back. The input-method
button and its help notice have been removed. Running automation automatically
restores the selected Kathana window and reacquires its foreground focus. Focus
loss skips new input and releases owned keys/buttons while the engine keeps
running; input resumes after focus returns. Windows can deny activation, so the
supervisor retries instead of reporting success or stopping combat.

Stop, F12 and Ctrl+Shift pause cancel focus maintenance. F12 is reserved for
the user's emergency stop rather than automated skill presses. The control
panel allows three seconds to click Stop when activated; active modal dialogs
suspend restoration until they close. The supervisor validates the exact
selected game window and process before restoring even a minimized game.

Launch starts Full automatically after the initial process-list refresh. A
blocked or failed start remains pending and retries instead of being marked
complete. Stop/F12 cancels a pending launch start, and a successful launch start
does not restart after an intentional stop. The Home display follows the actual
engine state. The release script creates
`KathanaBotControlPanel_Standalone_Foreground_1.0.249.exe` plus the regular
versioned filename, with identical verified contents. Earlier background EXEs
are retained. SendInput remains injected software input; the game's detection
cause and continued-use compatibility remain unverified.

Version 1.0.245 adds **EXTRAS**, hidden until the existing **126974** Home-page
sequence unlocks the extra tabs. Choose a Kathana installation folder and apply
the bundled **x2 zoom** or **x3 POV** preset to its relative `userdata/engine.cfg`.
Close all Kathana windows first. Every existing config is renamed to a unique
backup; **Restore previous engine.cfg** rolls back a change while preserving the
replaced config too.

Both exact zoom presets and **Remote Desktop Mosaic 1.0.4** are embedded in the
standalone EXE. Extras can save that Mosaic build offline or download the newest
dated Mosaic build from this repository's `agent-ai` branch. Downloads verify
the size and SHA-256 before replacement, preserve the previous copy and support
cancellation. Mosaic is saved to `Extras/RemoteDesktopMosaic.exe` beside the app.
The Open button reuses `RemoteDesktopMosaic.settings.json` beside KathanaBot when
present. No additional preset or Mosaic source files are needed at runtime.

Loot After Kill now consumes each attacked-target disappearance before attempting
F, so a failed or cancelled send cannot repeat it every frame. It retains the
configured hold and combat input backend. This fixes a retry defect; in-game
detection compatibility remains unverified.

Version 1.0.244 replaces Quiz's model choices with **GPT-6 Luna — Fast / Cheap**
and **GPT-6 Astra — Ultrafast / Expensive**. The choice is saved; older model
selections migrate to Luna. Requests use the selected processing tier and do not
silently downgrade it. Trade analysis remains GPT-5 nano.

Trade foreground whispers now validate the selected window and process directly
instead of requiring access to a hardcoded Steam executable path. Start waits up
to 10 seconds for that exact game window to gain focus; click it with chat closed
if Windows does not activate it automatically. No typing begins before focus is
confirmed. Changing apps after typing begins still stops the queue.

Version 1.0.243 fixes Trade rejecting verifiable AI listings because of capitalized
items, collapsed whitespace or evidence including the post's author header.
Matches are checked within the original named post and restored to its source
spelling. Character names, punctuation, upgrade digits and buy/sell clauses still
must match; unverified results cannot create a queue. Trade keeps GPT-5 nano and
Quiz keeps its selected model. Failed validation identifies the affected batch.

Version 1.0.242 removes **Configure Discord**, **Import latest**, **Auto** and the
reader count from Trade. Saved reader Auto settings cannot restart polling.
Use **Browser capture** or paste names and messages, then analyze and review the
whisper queue. Existing posts and saved queues are preserved.

Version 1.0.241 makes Trade analysis use **GPT-5 nano** with minimal reasoning,
independently of the Quiz model. It processes up to 50 complete posts or 8,000
characters per batch, with up to four requests at once, and reuses verified
batch results in a bounded cache during the app session. Sorting, filtering,
item counts and whisper queue creation run locally without API charges.
Analysis still covers every post and keeps the previous queue on failure or
cancellation. There is no automatic fallback to a more expensive model.

Version 1.0.240 fixes Trade Start silently returning in the background keyboard
build. **Start whispers (foreground)** uses a separate foreground text sender
while combat input stops; it preserves the configured background combat backend.
Keep the selected game focused while whispers run. Changing apps, cancellation
or errors stop the queue; close any unfinished chat draft before retrying.
Successful completion resumes the original combat mode only if the original
game window is still valid and focused.

Version 1.0.239 adds **Unselect all** beside Select all matches. It clears every
checked item, including items hidden by a filter, and clears the whisper review
queue. The button is disabled when nothing is selected.

Version 1.0.238 fixes collapsed Discord name/message lines and analyzes large
captures in batches of up to 20 complete posts or 8,000 characters, with visible
progress. Analysis covers the whole source; failed or cancelled runs preserve
your previous reviewed queue. The status separates processed posts, extracted
buy/sell listings and selected recipients. Clear the item filter and click
**Select all matches** to include all detected items for the current Buy/Sell mode.

Version 1.0.237 adds **Select all matches** beside the detected-item search box in
Trade. It checks every visible search result and rebuilds the whisper review
queue in one action. Checked items outside the filter stay selected; an empty
filter selects all detected items for the current Buy/Sell mode.

Version 1.0.236 displays imported Discord posts as **name and message only**,
without generated timestamps or message links. Exact message text is preserved.
Already saved text keeps its previous format until imported again.

Version 1.0.235 slows browser capture to at least **10 seconds between scrolls**,
with a pause for message loading and rendering to settle. Slow pages receive up
to 45 seconds to load. Browser batches can run for up to 60 minutes, and the local
connection expires after 90 minutes, allowing larger batches at this slower pace.
Update/reload the unpacked extension as well as the app before starting again.

Version 1.0.234 adds **Browser capture** in Trade. A separately installed local
Chrome/Edge extension can scroll a signed-in Discord channel and send a selected
batch of 1 to 10,000 messages into KathanaBot, including embedded trade listings.
This requires a one-time extension installation and one connection setup per
capture session; it removes page-by-page copying. The browser session must remain
open. Personal-account automation is prohibited by Discord and carries an account
risk; this is not a supported Discord integration. The user confirmed that
collection starts in their browser; the slower 1.0.235 pacing still needs live
confirmation. See
[capture setup and limits](docs/DISCORD_TRADE_IMPORT.md#browser-capture).

Version 1.0.233 added reader-bot history imports through Discord Following.
Those controls and polling were retired in 1.0.242. See the current
[Discord Trade setup](docs/DISCORD_TRADE_IMPORT.md) for Browser capture and manual paste.

Version 1.0.231 supports **background keyboard input** while another application
stays active. Keep Kathana open and not minimized. It does not take game focus or
move the system cursor; mouse/text workflows are unavailable in this keyboard build.
Numbers, letters, function keys, movement keys and Ctrl/Alt shortcuts share the same
target-only route. Short holds independently sample 150-250 ms; longer configured
holds remain exact, and cancellation releases promptly.

Stop automation, then click **Background keys** to choose Queued / scan,
Direct / scan, Queued / virtual or Direct / virtual. Queued / scan is the default.
The method is saved with app settings. A mode cannot change while a key release is
pending. CLI override: `--background-key-mode posted-scan|send-scan|posted-zero|send-zero`.

Close older bot copies before opening the new standalone. Earlier executables are
retained. These are four delivery/encoding options within Windows keyboard messages;
they do not establish that the game's enforcement permits automation. No DLL
injection, driver or game-file change is required. Validation and experiments are
recorded in [input testing](docs/INTERNAL_INPUT_HOOK.md).
The packaged v1.0.231 EXE visibly toggled inventory with all four methods while
another window stayed active. The default also passed two isolated Lite-engine
checks. All 13 managed test programs passed; sustained combat and detection
behavior remain unverified.
Subsequent user use of v1.0.231 was reported terminated for suspicious activity;
the running panel selected posted scan codes. Background key acceptance alone
has therefore not solved continued-use detection. See S03 in the experiment log.
The running [background-input experiment log](BACKGROUND_INPUT_EXPERIMENTS.md)
records successes, failures, evidence and pending checks.
Create the root-folder standalone EXE with `./build-standalone-release.ps1`.

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
- Mob-name OCR compares multiple enlarged, pixel-preserving, high-contrast, and color-isolated samples, then keeps the strongest complete reading stable through brief capture flicker.
- Party status uses the seven fixed party rows, long HP/MP bar runs, and member-name pixels to count members and distinguish nonzero HP from dead rows without mistaking terrain or buff colors for party bars.
- If the EXE is already running, rebuild can warn about locked files.
- Keep EXE builds serialized/versioned. Do not replace an older EXE with a new one using the same filename.

## Default overlay coordinates

New configurations and first-run settings use these client-pixel rectangles (X, Y, width, height):

| Region | X | Y | Width | Height |
| --- | ---: | ---: | ---: | ---: |
| resurrect_scan_rect | 522 | 319 | 328 | 124 |
| death_message_rect | 515 | 322 | 328 | 124 |
| party_list_rect | 2 | 107 | 168 | 244 |
| disconnect_message_rect | 518 | 319 | 328 | 125 |
| disconnect_ok_rect | 741 | 415 | 54 | 15 |

Edit these in Vision; changes save to the active profile. Existing saved values take precedence over defaults. Missing regions fall back to these values. These five rectangles retain their configured pixel coordinates at runtime rather than being automatically scaled with the window size.

## Game not responding alerts

While either Full or Lite bot is running, an independent background timer checks the selected game's Windows responsiveness every 5 seconds. If Windows keeps reporting it unresponsive for 15 seconds, the bot sends one alert through the configured Discord global webhook or ntfy phone topic. It sends a recovery notification when the window responds again. Windows has its own initial hang-detection delay, so notification is not exactly 15 seconds after a freeze begins. Short stalls, missing windows, and long gaps between checks do not establish a freeze. Stopping the bot resets monitoring. This detects window hangs, not freezes that leave the window responsive. Use the existing Test Phone Alert to verify notification delivery.

## Centered auto-loot pickup

Loot Scanner keeps its configured scan interval during adaptive performance mode, including when Hold on Place keeps coordinate OCR active. A pending capture or OCR scan finishes before another starts, so slow loops cannot disable scanning or stack overlapping scans.

Enable Loot Scanner (Alt) and Centered Loot Pickup (F). The scanner clicks distant allowed loot to approach it. F is permitted only when a fresh OCR label is inside the central 10% of the game client, using the actual label position even with a coarse grid or an offset scan area. Cooldown (sec) is a minimum delay, not a repeating F timer. Each detection permits one pickup; stale detections expire after 1.5 seconds. Loot After Kill is separate: it presses F once when an attacked mob reaches zero HP or disappears, without requiring scanner detection or the centered-item filter. Scanner frequency still controls how often arrival is checked. Auto-loot settings continue to save automatically to the active profile.

## Faster leveling and easier setup

The Leveling tab starts with **Apply Recommended Leveling Setup**. It enables the leveling agent, fast 300 ms target search/recovery, a 30-second no-target guardrail, short 240 ms navigation bursts, 500 ms position corrections, 3.5-second stall recovery, and repathing. If a recorded route destination is already selected, the button also enables localization, route preview, and guarded travel. It preserves combat rows, preferred mobs, and recorded routes.

Navigation treats a waypoint as reached when the player enters the configured waypoint radius. It no longer requires an exact OCR coordinate match, which caused overshooting and stale-waypoint wandering. Retarget scans run between movement bursts so the target key cannot cancel movement in the same loop. New profiles and first-run settings use these responsive values; existing profiles keep their saved values until the recommended setup button is used.

## Timed arrow key holds

In Auto-Loot, enable Left arrow and/or Right arrow under Timed Arrow Key Holds. Each direction has an independent Hold (sec), from 0.1 to 60 seconds, and Wait (sec), from 0.1 to 3600 seconds. Wait begins after release; the first hold is due when enabled. Both directions take turns when due and never overlap. Holds run while the Full bot and game window are active and release on stop, chat pause, focus loss, or disabling that direction. All settings save automatically with the active profile. After an arrow hold, centered pickup requires a new scan.
