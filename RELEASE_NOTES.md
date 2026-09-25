## 1.0.217
- Added a fresh 5-15 ms delay before each generated key-down (including Unicode text and modifier presses) and mouse-button-down, across all features using the foreground input backend.
- Skill cooldowns remain unchanged. Key/button releases and cursor moves have no added delay; focus and click coverage are checked again after the wait.
- Timing variation is not a guarantee against server disconnections or automation detection.

## 1.0.216
- All generated game keyboard, mouse, cursor and Unicode text input now goes through a shared foreground-only SendInput backend; no PostMessage, keybd_event, mouse_event or SetCursorPos input fallbacks remain.
- Background Only is removed as an operating mode. Old profile flags are ignored; the UI reports Foreground SendInput only.
- Every new input checks the target window/process and foreground state. Clicks also reject covered cursor positions. A watchdog releases bot-owned keys/buttons on focus loss and retries failed releases.
- Combat/Lite/Direct KP, navigation, loot, buffs, party/support, Trade, Quiz, RESU, disconnect and relaunch paths use the shared input boundary. Relaunch no longer clicks unverified desktop coordinates.
- Chat preserves Unicode case/punctuation and does not submit partial text after input failure or focus loss. Modifier cleanup is unconditional.
- Added input-backend and native-import regression checks. No claim is made that foreground input is undetectable.

## 1.0.215
- Enabled skill rows are green; editable key cells are purple. Add skill creates additional saved Ctrl+number or Alt+number rows.
- Added a persistent Background Only button that disables foreground-dependent features, reports the changes, and blocks focus, mouse, and physical key presses while enabled.
- Loot After Kill now holds F for one second by default, with a configurable 0.05-5 second duration beside its toggle. Stopping releases the held key promptly.
- Added regression coverage for background input protection, shortcut key order, cancellable loot holds, and saved custom skill rows.

## 1.0.214
- Fixed Full mode continuing to attack a stale target indefinitely: Auto Retarget If Stuck now overrides the combat lock after the configured no-progress delay, with Loot After Kill either on or off.
- Successful retargets clear old target sightings and damage history so the next mob gets a fresh recovery window. Oscillating stale HP readings no longer count as repeated damage.
- Stuck recovery respects capture validity, death/recovery priority, mode exclusions, and retarget cooldowns; added a recovery log with the elapsed no-progress time.
- Added regressions for ten minutes of combat followed by a stale target, external kills, continued real damage, stale HP jitter, and optional post-kill pickup.

## 1.0.213
- Reset All Settings disables features and overlays, clears calibration and lists, and resets cooldowns to supported minimums while preserving saved character profiles.
- Home pulses red at zero character HP; stopped bots have a red surface tint.
- Hidden tabs remain unlocked across restarts of the same app version and require the password again after an update.
- Daily rupiah projection now uses wallet-sized gray text.
- Added a switch for the in-game bot overlay so Reset All Settings can turn it off too.

## 1.0.212
- Fixed Trade selections clearing the whisper queue when a custom message omitted {items}. Plain messages now work unchanged; optional item substitution and exact-case recipient names are preserved.

## 1.0.211
- Added Reset All Settings beside Profiles, with explicit confirmation; resets current settings and keeps saved character profiles.
- Suppressed persistence and live config pushes during profile application; committed overlay drags save immediately.
- Preserved saved calibration coordinates, including older factory coordinates, instead of migrating them on reload.
- Remembered Full/LITE Direct KP selection across bot stop/start and in saved profiles.
- Clarified target calibration labels: Mob HP - Red Bar and Mob HP - Numbers (OCR).
- Added a colorful monitor icon to Remote Desktop Mosaic.

# KathanaBot 1.0.210

## What changed

- **Restored direct F pickup for Loot After Kill** when an attacked mob reaches zero HP or disappears - it no longer requires the loot scanner or a centered item detection. Long fights stay armed using the latest living-mob observation instead of expiring 3 seconds after the last attack key, failed sends retry briefly, and a successful pickup is never repeated for the same death. Centered Loot Pickup and timed-arrow behavior are unchanged.
- **Fixed Home not refreshing reliably.** It now reads a live engine snapshot immediately on Start/Stop and on every UI tick instead of relying on cached status callbacks or a tab switch, keeping the play/pause icon, status, and session readings in sync.
- **Fixed stale/duplicated Home panels and layout gaps after startup or Start**, and switching tabs no longer resets your window size.
- **Added AUTO-ASSIST ONLY next to LITE Direct KP**: blocks every bot-generated E key press (configured E rows and normal/manual/forced retargeting) while leaving all other keys - including Direct KP's own R - working normally. Enabling it requires a warning plus a separate "Are you sure?" confirmation; canceling either leaves it off, and the setting always resets when the app closes.

# KathanaBot 1.0.207

## What changed

- **Added LITE Direct KP to Combat Full**: a gold-bordered red/green toggle with a configurable E-to-E cycle (200-60,000 ms; default 1,000 ms). Each pair presses E, then R 100 ms later, with at least 100 ms before the next E; delayed cycles never catch up in a burst. It bypasses normal/forced/configured/manual retargeting entirely - configured attacks, heals, buffs, and other enabled features keep running as normal.
- Because target recognition no longer gates attacks in this mode, a warning and a cancellable five-second notice appear before its first activation each launch: monster filters, target exclusions, kill/loot tracking, navigation, and target-based conditions may not work correctly with unconditional E/R. Main Stop and toggling it off immediately cancel any pending input; chat pauses, death, a missing/minimized window, or stale telemetry all suspend it the same as other features.
- While enabled, Combat Skills switches to a compact cream-colored card layout for the same underlying rows (role, cooldown, priority, trigger); turning it back off restores the normal grid without losing any edits. The separate Lite tab/start path has been retired - this is now the Lite experience, built into Combat Full.
- The Combat Full running button and the floating in-game status panel now read "FULL RUNNING" or "LITE RUNNING" depending on whether Direct KP is active; both still control the same running engine, only the displayed label changes.

# KathanaBot 1.0.204

## What changed

- Fixed the delayed Windows ding regression introduced by the 1.0.200 colored debug log. Above 160,000 characters, trimming tried to clear a selection in a read-only RichTextBox; the native edit was rejected and retried every five seconds. The onset depended on log volume, not an alarm timer or attack key.
- Log trimming now permits only the synchronous programmatic deletion, restores read-only mode in a Finally block, preserves category colors, and clears undo history.
- Trim boundaries now use RichTextBox's normalized LF newlines so retained messages remain complete.
- Added a regression test that crosses the production threshold repeatedly and checks bounded size, retained colors/messages, read-only restoration, and absence of read-only WM_CLEAR requests.

# KathanaBot 1.0.203

## What changed

- **Renamed "Hold on Place" to "Max Range"** throughout the app (tab, controls, log/status messages), and added a Help button explaining anchor, tolerance, restrictiveness, and leash settings. It also now shows a clear "inactive" readout while disabled instead of repeating disabled-state log entries.
- **Removed the "Hold to Show Game Window" hotkey controls entirely**, including its background hotkey worker; old saved settings can no longer reactivate it.
- **Removed the "General" category and its icons from the buff icon selector.**

## Recent change history - last 5

1. **Pop-up dialogs (warnings, errors, confirmations) no longer play the Windows alert sound.** They keep the same text, icon, and OK/Cancel/Yes/No buttons; only the system sound that Windows normally triggers for a dialog with an icon has been silenced.
2. **Removed the local audible alarm sound on HP-zero death and game-freeze alerts.** Phone notifications (Discord webhook / ntfy) continue to fire exactly as before; only the local system-sound pulse and its temporary volume override were removed.
3. **Added an "Activity & Setup" tab** with a Timeline (recent actions with HP/MP/target context, skip reasons, and mode changes), an OCR readings view showing reading age and confidence when available, and a Route preview that plots the selected Leveling route with waypoint numbers, direction arrows, and long jumps highlighted in red.
4. **The Diagnostics tab now always shows current status** at the top - what the bot is waiting on or blocked by, plus whether settings are saved/applied, with error details on save or apply failures instead of failing silently.
5. **Log lines are now color-coded by category** (errors, warnings, waiting, success, combat, loot, OCR, navigation) with a legend, and the Combat Full tab's panels are now resizable by dragging their splitters.
