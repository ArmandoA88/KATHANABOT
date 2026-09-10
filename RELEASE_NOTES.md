# KathanaBot 1.0.160

## What changed

- **Added "Game Not Responding" phone alerts.** While Full or Lite is running, an independent background check pings the selected game window's Windows responsiveness every 5 seconds and, if it stays unresponsive for 15 seconds straight, sends one alert through your configured Discord webhook or ntfy topic - then a recovery notification once it responds again. Short stalls, a missing window, or long gaps between checks don't trigger it, and stopping the bot resets monitoring. Use the existing Test Phone Alert to verify delivery.
- **Fixed Loot Scanner (Alt) losing its scan interval under load.** It now keeps scanning on schedule even while adaptive performance mode or Hold on Place's coordinate OCR are active, and never lets a slow OCR pass overlap with the next scheduled one.
- **Updated the default calibration coordinates** for the resurrect/death-message/disconnect dialogs and the party list to match the current client layout; these five regions now hold their exact configured pixel position instead of auto-scaling with window size, and existing saved calibrations are always preserved over the new defaults.

## Recent change history - last 5

1. **Fixed the Loot Grid detection flash being invisible to screen recording/streaming software.** It was previously excluded from display capture along with the other click-point overlays; it now stays visible in recordings while remaining click-through and non-activating in the live window.
2. **Auto-Loot now targets the nearest allowed item to the character** instead of just the best OCR text match anywhere on screen, added "Cannot pick up item yet" handling that deprioritizes an unavailable item and tries the next-nearest one, and seeded the default Loot Filter with a starter list of ~80 common item names (existing lists get these merged in automatically).
3. **Added centered auto-loot pickup and Timed Arrow Key Holds to Auto-Loot.** The Centered Loot Pickup (F) key and Loot After Kill now only fire once a fresh OCR label is inside the central 10% of the game client, using the label's actual position; Timed Arrow Key Holds adds independent, non-overlapping Left/Right arrow hold timers that release automatically on stop, chat pause, or focus loss.
4. **Loot Scanner (Alt) now clicks matched items directly instead of only alarming.** The calibrated loot area is divided into a configurable grid mesh (Columns x Rows, up to 40x40, default 6x4, in the Auto-Loot tab), with a "Show Loot Grid Overlay" toggle and a brief on-screen flash over each detected item's square and click point.
5. **Renamed the tab, heading, toggle, and calibration window to "Full Support (Vidya only)"** for clarity, and added a blue calibration notice explaining that the blue dots are character click points used for buffs and auras.
