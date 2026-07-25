# KathanaBot 1.0.80

## What changed

- Fixed arrow unbundle clicks stealing focus from whatever window the user had active. The double right-click is now posted directly to the game's window handle instead of sent as a real, system-wide click, so it can't land on or take focus from some other window sitting on top of the game at that screen point.
- Fixed arrow unbundle silently doing nothing when the game window wasn't already the active/foreground window (many games only process clicks while active). The bot now briefly forces the game to the foreground just long enough to send the click, then restores whatever had focus before - using AttachThreadInput to get around Windows' normal focus-stealing prevention for background processes.
- Fixed arrow unbundle being over-eager about rejecting valid attempts: cursor-position verification now allows an 8px tolerance instead of requiring an exact pixel match, since minor cursor drift shouldn't cancel an otherwise-good click.
- Fixed a crash (OverflowException) in the loot name pickup click when the game window sits on a monitor to the left of or above the primary display, which produces negative screen coordinates that couldn't be converted to the unsigned values `mouse_event` expects.
- Arrow unbundle's skip log message now reports the specific reason (bad window handle, failed screen mapping, cursor readback mismatch, etc.) instead of a single generic "cursor could not be verified" message, making it easier to tell why a click was skipped.

## Recent change history - last 5

1. **Arrow unbundle no longer steals focus:** clicks are posted directly to the game window instead of firing as real system-wide clicks.
2. **Arrow unbundle works while the game isn't active:** the game is briefly foregrounded for the click and focus is restored immediately after.
3. **Less trigger-happy verification:** an 8px cursor tolerance stops good clicks from being wrongly skipped.
4. **Loot pickup crash fix:** negative screen coordinates (multi-monitor setups) no longer crash the click.
5. **Better diagnostics:** arrow unbundle skip messages now say exactly what failed.
