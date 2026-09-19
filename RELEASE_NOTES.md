# KathanaBot 1.0.202

## What changed

- **Pop-up dialogs (warnings, errors, confirmations) no longer play the Windows alert sound.** They keep the same text, icon, and OK/Cancel/Yes/No buttons; only the system sound that Windows normally triggers for a dialog with an icon has been silenced.

## Recent change history - last 5

1. **Removed the local audible alarm sound on HP-zero death and game-freeze alerts.** Phone notifications (Discord webhook / ntfy) continue to fire exactly as before; only the local system-sound pulse and its temporary volume override were removed.
2. **Added an "Activity & Setup" tab** with a Timeline (recent actions with HP/MP/target context, skip reasons, and mode changes), an OCR readings view showing reading age and confidence when available, and a Route preview that plots the selected Leveling route with waypoint numbers, direction arrows, and long jumps highlighted in red.
3. **The Diagnostics tab now always shows current status** at the top - what the bot is waiting on or blocked by, plus whether settings are saved/applied, with error details on save or apply failures instead of failing silently.
4. **Log lines are now color-coded by category** (errors, warnings, waiting, success, combat, loot, OCR, navigation) with a legend, and the Combat Full tab's panels are now resizable by dragging their splitters.
5. **HP/MP checks and death recovery now always run before optional scans** (OCR, loot, map, stats) each frame, so a critical health drop is never delayed behind lower-priority work.
