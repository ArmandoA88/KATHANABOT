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
