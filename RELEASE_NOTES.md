# KathanaBot 1.0.114

## What changed

- **Added "Loot After Kill"** on the Combat Full tab: a toggle button that presses F right after a kill instead of waiting on Loot Pickup's timer - it fires whenever the current target disappears within a couple seconds of your last attack, since pressing F when there's nothing to loot is harmless. **Chat text (Party Ask, Ask For Resurrection, Auto Party Message) is now pasted via the clipboard** instead of typed key-by-key - faster, and it carries any character through instead of silently dropping ones the old per-character typing didn't know how to send - while saving and restoring whatever was already on your clipboard so it can't get clobbered. Also **tightened click precision** for Auto Party, Auto Resurrect, and Arrow Unbundle: clicks were sometimes landing a few pixels off their intended target closely enough to miss a small UI element (and, for Auto Party, to trigger an unwanted move instead of an invite) - the shared click helper now verifies the cursor landed almost exactly on target and retries immediately if not, instead of accepting the drift.

## Recent change history - last 5

1. **Added "Loot After Kill"**, switched chat text to clipboard-paste (with save/restore), and tightened click precision across Auto Party/Auto Resurrect/Arrow Unbundle.
2. **Added "Auto Party"** (hidden behind Developer Mode, still being finished): key+click invite loop plus a separate chat-message loop, each with its own interval and a typeable/pickable click point.
3. **Fixed click overlays disappearing whenever the control panel window is minimized**, and fixed the Auto Party invite key's click not landing (key press now foregrounds the game before the click).
4. **Added "Ask For Resurrection"**: nags party chat for a resurrection on a timer while death-paused, with optional map coordinates, without interrupting Auto Resurrect or Pause Combat On Death.
5. **Party invite/ress-prompt auto-accept now presses Enter** instead of clicking a calibrated point; removed the now-unneeded `party_invite_ok_rect` calibration region.
