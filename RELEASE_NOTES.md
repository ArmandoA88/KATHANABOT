# KathanaBot 1.0.201

## What changed

- **Removed the local audible alarm sound on HP-zero death and game-freeze alerts.** Phone notifications (Discord webhook / ntfy) continue to fire exactly as before; only the local system-sound pulse and its temporary volume override were removed.

## Recent change history - last 5

1. **Added an "Activity & Setup" tab** with a Timeline (recent actions with HP/MP/target context, skip reasons, and mode changes), an OCR readings view showing reading age and confidence when available, and a Route preview that plots the selected Leveling route with waypoint numbers, direction arrows, and long jumps highlighted in red.
2. **The Diagnostics tab now always shows current status** at the top - what the bot is waiting on or blocked by, plus whether settings are saved/applied, with error details on save or apply failures instead of failing silently.
3. **Log lines are now color-coded by category** (errors, warnings, waiting, success, combat, loot, OCR, navigation) with a legend, and the Combat Full tab's panels are now resizable by dragging their splitters.
4. **HP/MP checks and death recovery now always run before optional scans** (OCR, loot, map, stats) each frame, so a critical health drop is never delayed behind lower-priority work.
5. **Chat Translation now draws each translation directly over its original message** - same position, font size, and color - instead of a separate stacked panel, and keeps translations alive through brief OCR gaps and chat scrolling instead of restarting from scratch.
