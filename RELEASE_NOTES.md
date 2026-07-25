# KathanaBot 1.0.69

## What changed

- Fixed Arrow Unbundle right-clicking wherever your real mouse happened to be hovering instead of the configured inventory slot. The old click was sent as a posted window message, but the game reads the actual system cursor position to decide where a click landed - so the intended slot got clicked correctly, but so did whatever you were hovering over in the meantime. The click now moves the real cursor to the exact target point, re-checks that it landed exactly there, and only clicks if it's confirmed on target; otherwise it skips the click entirely.
- Fixed the game's taskbar button flashing orange every time Arrow Unbundle fired. That was a side effect of the click briefly trying to force the game window to the foreground; Windows blocks that from a background process and flashes the taskbar instead of switching focus. The click no longer requests foreground focus - a real mouse click lands on whichever window is under the cursor regardless of focus, so this wasn't needed.
- Rebuilt the standalone EXE and published this release through the in-app updater channel.

## Recent change history - last 5

1. **Arrow Unbundle clicks land on target:** the right-click now only fires after verifying the real cursor is exactly on the configured point, instead of trusting a background message that some games resolve against wherever the mouse actually is.
2. **No more orange taskbar flash:** Arrow Unbundle no longer asks Windows to foreground the game window, removing the denied-focus-request flash that fired on every cycle.
3. **Scoped fix:** only Arrow Unbundle's click path changed - Loot Auto-Pick's click behavior is untouched.
4. **Still safe by default:** if the cursor can't be verified exactly on the target point, no click is sent at all rather than risking a misplaced one.
5. **Fresh standalone build:** rebuilt and versioned for this release.
