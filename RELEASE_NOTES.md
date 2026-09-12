# KathanaBot 1.0.165

## What changed

- **Reset Stats on the main dashboard now also resets the Runtime tile back to 0**, in addition to the existing EXP and Rupiah counter reset. The confirmation dialog and log entry now mention Runtime too.

## Recent change history - last 5

1. Internal maintenance and performance improvements.
2. **Added "Apply Recommended Leveling Setup" to the Leveling tab.** One button enables the leveling agent, fast 300 ms target search/recovery, a 30-second no-target guardrail, short 240 ms navigation bursts, 500 ms position corrections, 3.5-second stall recovery, and repathing; if a recorded route destination is already selected it also enables localization, route preview, and guarded travel. Combat rows, preferred mobs, and recorded routes are all preserved.
3. **Fixed leveling navigation overshooting and wandering near a waypoint.** A waypoint now counts as reached as soon as the player enters its configured radius instead of requiring an exact OCR coordinate match, and retarget scans now run between movement bursts so the target key can no longer cancel movement mid-loop.
4. **"Game Not Responding" phone alerts now repeat every 5 minutes** while the game stays frozen, instead of sending a single alert and going quiet until it recovers.
5. **Added "Game Not Responding" phone alerts.** While Full or Lite is running, an independent background check pings the selected game window's Windows responsiveness every 5 seconds and, if it stays unresponsive for 15 seconds straight, sends one alert through your configured Discord webhook or ntfy topic - then a recovery notification once it responds again. Short stalls, a missing window, or long gaps between checks don't trigger it, and stopping the bot resets monitoring.
