# KathanaBot 1.0.169

## What changed

- **Added "Restore factory regions" to the Vision calibration panel** and recalibrated every factory region (HP/MP bars, mob name/HP, unreachable text, EXP, Rupiahs, party invite/list, disconnect dialog, map coordinates, chat, buff area) plus the Loot Scan Area to match the current client layout. The button restores and saves the factory coordinates; your own saved edits continue to persist unchanged otherwise.
- **Fixed EXP and Rupiah OCR reading the wrong spot on non-reference window sizes.** These calibration-table rectangles now stay at their exact configured client-pixel coordinates instead of being scaled a second time.
- **Added "Delete selected" to Items Won** (also bound to the Delete key). It removes only the highlighted award; Clear still removes the full list.
- **Combat Skills now displays the `retarget` role as `retarget/assist`** in the role dropdown. Saved configurations and runtime behavior continue using the same internal `retarget` role.

## Recent change history - last 5

1. **Reset Stats on the main dashboard now also resets the Runtime tile back to 0**, in addition to the existing EXP and Rupiah counter reset. The confirmation dialog and log entry now mention Runtime too.
2. Internal maintenance and performance improvements.
3. **Added "Apply Recommended Leveling Setup" to the Leveling tab.** One button enables the leveling agent, fast 300 ms target search/recovery, a 30-second no-target guardrail, short 240 ms navigation bursts, 500 ms position corrections, 3.5-second stall recovery, and repathing; if a recorded route destination is already selected it also enables localization, route preview, and guarded travel. Combat rows, preferred mobs, and recorded routes are all preserved.
4. **Fixed leveling navigation overshooting and wandering near a waypoint.** A waypoint now counts as reached as soon as the player enters its configured radius instead of requiring an exact OCR coordinate match, and retarget scans now run between movement bursts so the target key can no longer cancel movement mid-loop.
5. **"Game Not Responding" phone alerts now repeat every 5 minutes** while the game stays frozen, instead of sending a single alert and going quiet until it recovers.
