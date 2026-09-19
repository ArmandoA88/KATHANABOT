# KathanaBot 1.0.196

## What changed

- Internal maintenance and performance improvements.

## Recent change history - last 5

1. **Chat Translation now draws each translation directly over its original message** - same position, font size, and color - instead of a separate stacked panel, and keeps translations alive through brief OCR gaps and chat scrolling instead of restarting from scratch.
2. **The disconnected-game phone alert now repeats every 5 minutes** while still disconnected, instead of alerting once and going quiet.
3. **Home > Reset Stats now also clears Mobs Killed, Kills/Hour, and the internal kill-tracking baseline**, in addition to the existing EXP, Rupiah, and Runtime reset. Pre-reset kills no longer count toward the new stats session.
4. Internal maintenance and performance improvements.
5. **Fixed auto-update not restarting the bot and leaving the old version in place.** The standalone EXE's update-and-restart used a cooperative shutdown request that didn't reliably free the running EXE file in time; the background helper script's file-replace step could then fail silently for up to 30 seconds and give up without ever reopening the app. The app now exits the same hard, guaranteed way the installed (Setup.exe) update path already did, and as a safety net the helper now reopens the app even in the rare case the file replace still can't win the lock, instead of leaving nothing running.
