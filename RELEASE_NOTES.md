# KathanaBot 1.0.161

## What changed

- **Added "Apply Recommended Leveling Setup" to the Leveling tab.** One button enables the leveling agent, fast 300 ms target search/recovery, a 30-second no-target guardrail, short 240 ms navigation bursts, 500 ms position corrections, 3.5-second stall recovery, and repathing; if a recorded route destination is already selected it also enables localization, route preview, and guarded travel. Combat rows, preferred mobs, and recorded routes are all preserved.
- **Fixed leveling navigation overshooting and wandering near a waypoint.** A waypoint now counts as reached as soon as the player enters its configured radius instead of requiring an exact OCR coordinate match, and retarget scans now run between movement bursts so the target key can no longer cancel movement mid-loop.
- **"Game Not Responding" phone alerts now repeat every 5 minutes** while the game stays frozen, instead of sending a single alert and going quiet until it recovers.

## Recent change history - last 5

1. **Added "Game Not Responding" phone alerts.** While Full or Lite is running, an independent background check pings the selected game window's Windows responsiveness every 5 seconds and, if it stays unresponsive for 15 seconds straight, sends one alert through your configured Discord webhook or ntfy topic - then a recovery notification once it responds again. Short stalls, a missing window, or long gaps between checks don't trigger it, and stopping the bot resets monitoring.
2. **Fixed Loot Scanner (Alt) losing its scan interval under load.** It now keeps scanning on schedule even while adaptive performance mode or Hold on Place's coordinate OCR are active, and never lets a slow OCR pass overlap with the next scheduled one.
3. **Updated the default calibration coordinates** for the resurrect/death-message/disconnect dialogs and the party list to match the current client layout; these five regions now hold their exact configured pixel position instead of auto-scaling with window size, and existing saved calibrations are always preserved over the new defaults.
4. **Fixed the Loot Grid detection flash being invisible to screen recording/streaming software.** It was previously excluded from display capture along with the other click-point overlays; it now stays visible in recordings while remaining click-through and non-activating in the live window.
5. **Auto-Loot now targets the nearest allowed item to the character** instead of just the best OCR text match anywhere on screen, added "Cannot pick up item yet" handling that deprioritizes an unavailable item and tries the next-nearest one, and seeded the default Loot Filter with a starter list of ~80 common item names (existing lists get these merged in automatically).
