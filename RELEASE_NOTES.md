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
