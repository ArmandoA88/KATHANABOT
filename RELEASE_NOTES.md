# KathanaBot 1.0.159

## What changed

- **Fixed the Loot Grid detection flash being invisible to screen recording/streaming software.** It was previously excluded from display capture along with the other click-point overlays; it now stays visible in recordings while remaining click-through and non-activating in the live window.

## Recent change history - last 5

1. **Auto-Loot now targets the nearest allowed item to the character** instead of just the best OCR text match anywhere on screen, added "Cannot pick up item yet" handling that deprioritizes an unavailable item and tries the next-nearest one, and seeded the default Loot Filter with a starter list of ~80 common item names (existing lists get these merged in automatically).
2. **Added centered auto-loot pickup and Timed Arrow Key Holds to Auto-Loot.** The Centered Loot Pickup (F) key and Loot After Kill now only fire once a fresh OCR label is inside the central 10% of the game client, using the label's actual position; Timed Arrow Key Holds adds independent, non-overlapping Left/Right arrow hold timers that release automatically on stop, chat pause, or focus loss.
3. **Loot Scanner (Alt) now clicks matched items directly instead of only alarming.** The calibrated loot area is divided into a configurable grid mesh (Columns x Rows, up to 40x40, default 6x4, in the Auto-Loot tab), with a "Show Loot Grid Overlay" toggle and a brief on-screen flash over each detected item's square and click point.
4. **Renamed the tab, heading, toggle, and calibration window to "Full Support (Vidya only)"** for clarity, and added a blue calibration notice explaining that the blue dots are character click points used for buffs and auras.
5. **Fixed the active Profile not surviving an app restart.** The loaded/saved profile name is now stored in the settings file, so KathanaBot reopens with the same profile still selected instead of falling back to unnamed settings. The Home dashboard header now also shows "Profile: " followed by its name at a glance, and the Profiles menu bolds/checks whichever profile is currently active. Also fixed Save Settings not updating the active profile.
