# KathanaBot 1.0.112

## What changed

- **Party invites (and in-party resurrect prompts) are now accepted by pressing Enter** instead of clicking a calibrated "OK" button position. The `party_invite_ok_rect` calibration region has been removed entirely - there's nothing left to calibrate or miscalibrate for this dialog, since Enter accepts it regardless of screen position or resolution.

## Recent change history - last 5

1. **Party invite/ress-prompt auto-accept now presses Enter** instead of clicking a calibrated point; removed the now-unneeded `party_invite_ok_rect` calibration region.
2. **Fixed disconnect recovery never clicking OK**: the OCR reader used for the dialog's "OK" button was tuned for the wrong text polarity (light-on-dark HUD text vs. the button's dark-on-light face) and its scoring favored longer, noisier reads over a correct short "OK".
3. **Buff icon library moved to live next to the exe** (was `%AppData%`), so it travels with the standalone exe across computers; existing libraries migrate automatically on first run.
4. **Added an "Achievements" tab** tracking Rupiah earned and EXP gained over rolling 10m/30m/60m/24h windows, correctly handling level-ups mid-window.
5. **Key Summary now tracks a full 24 hours** instead of 60 minutes, with a new "Last 24h" column.
