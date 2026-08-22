# KathanaBot 1.0.113

## What changed

- **Added "Auto Party"** on the Auto-Loot tab, hidden behind Developer Mode since it's still being finished: an invite loop that presses a user-picked key (1-0 or F1-F10) then clicks a calibrated fixed point (pickable by clicking directly in the game window, or typed in as exact X/Y), and a separate chat-message loop - each on its own interval, with an optional click overlay to preview the invite point. Replaces the old "Pickup By Name (Dynamic Label Click)" box. Along the way, fixed the invite key's click not actually landing (the key press wasn't foregrounding the game before the click) and fixed click overlays (Arrow Unbundle, Auto Resurrect, Auto Party, Auto Relaunch) disappearing whenever the control panel window is minimized.

## Recent change history - last 5

1. **Added "Auto Party"** (hidden behind Developer Mode, still being finished): key+click invite loop plus a separate chat-message loop, each with its own interval and a typeable/pickable click point.
2. **Fixed click overlays disappearing whenever the control panel window is minimized**, and fixed the Auto Party invite key's click not landing (key press now foregrounds the game before the click).
3. **Added "Ask For Resurrection"**: nags party chat for a resurrection on a timer while death-paused, with optional map coordinates, without interrupting Auto Resurrect or Pause Combat On Death.
4. **Party invite/ress-prompt auto-accept now presses Enter** instead of clicking a calibrated point; removed the now-unneeded `party_invite_ok_rect` calibration region.
5. **Fixed disconnect recovery never clicking OK**: the OCR reader used for the dialog's "OK" button was tuned for the wrong text polarity (light-on-dark HUD text vs. the button's dark-on-light face) and its scoring favored longer, noisier reads over a correct short "OK".
