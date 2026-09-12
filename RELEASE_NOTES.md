# KathanaBot 1.0.170

## What changed

- **Fixed auto-update not restarting the bot and leaving the old version in place.** The standalone EXE's update-and-restart used a cooperative shutdown request that didn't reliably free the running EXE file in time; the background helper script's file-replace step could then fail silently for up to 30 seconds and give up without ever reopening the app. The app now exits the same hard, guaranteed way the installed (Setup.exe) update path already did, and as a safety net the helper now reopens the app even in the rare case the file replace still can't win the lock, instead of leaving nothing running.

## Recent change history - last 5

1. **Added "Restore factory regions" to the Vision calibration panel** and recalibrated every factory region (HP/MP bars, mob name/HP, unreachable text, EXP, Rupiahs, party invite/list, disconnect dialog, map coordinates, chat, buff area) plus the Loot Scan Area to match the current client layout. The button restores and saves the factory coordinates; your own saved edits continue to persist unchanged otherwise.
2. **Fixed EXP and Rupiah OCR reading the wrong spot on non-reference window sizes.** These calibration-table rectangles now stay at their exact configured client-pixel coordinates instead of being scaled a second time.
3. **Added "Delete selected" to Items Won** (also bound to the Delete key). It removes only the highlighted award; Clear still removes the full list.
4. **Combat Skills now displays the `retarget` role as `retarget/assist`** in the role dropdown. Saved configurations and runtime behavior continue using the same internal `retarget` role.
5. **Reset Stats on the main dashboard now also resets the Runtime tile back to 0**, in addition to the existing EXP and Rupiah counter reset. The confirmation dialog and log entry now mention Runtime too.
