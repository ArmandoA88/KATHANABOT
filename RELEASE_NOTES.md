# KathanaBot 1.0.158

## What changed

- **Auto-Loot now targets the nearest allowed item to the character** instead of just the best OCR text match anywhere on screen, so it goes after whatever is actually closest first.
- **Added "Cannot pick up item yet" handling.** If that prompt appears after an interaction, the bot deprioritizes that item and tries the nearest different allowed item instead of getting stuck retrying one that isn't available yet.
- **Seeded the default Loot Filter with a starter list of ~80 common item names** so Auto-Loot has a useful list out of the box; existing lists get these added in automatically on next load without losing anything already there.
- Minor Home dashboard polish: the Play/Pause button no longer flickers and its running state is kept consistent through a single shared update path.

## Recent change history - last 5

1. **Added centered auto-loot pickup and Timed Arrow Key Holds to Auto-Loot.** The Centered Loot Pickup (F) key and Loot After Kill now only fire once a fresh OCR label is inside the central 10% of the game client, using the label's actual position; Timed Arrow Key Holds adds independent, non-overlapping Left/Right arrow hold timers that release automatically on stop, chat pause, or focus loss.
2. **Loot Scanner (Alt) now clicks matched items directly instead of only alarming.** The calibrated loot area is divided into a configurable grid mesh (Columns x Rows, up to 40x40, default 6x4, in the Auto-Loot tab), with a "Show Loot Grid Overlay" toggle and a brief on-screen flash over each detected item's square and click point.
3. **Renamed the tab, heading, toggle, and calibration window to "Full Support (Vidya only)"** for clarity, and added a blue calibration notice explaining that the blue dots are character click points used for buffs and auras.
4. **Fixed the active Profile not surviving an app restart.** The loaded/saved profile name is now stored in the settings file, so KathanaBot reopens with the same profile still selected instead of falling back to unnamed settings. The Home dashboard header now also shows "Profile: " followed by its name at a glance, and the Profiles menu bolds/checks whichever profile is currently active. Also fixed Save Settings not updating the active profile.
5. **Overhauled EXP and Rupiah OCR reading for confirmed-only telemetry**, reworked Home and Achievements earned-stat tracking to a fixed baseline per bot run and character with a Reset Stats control, changed Full Support's Vidya reselection after a heal to three backtick (`) presses, added multi-select to Buff Watch's skill picker, disabled the Leveling guardrail while Full Support is active, and moved the feedback contact to Discord (`mando1545`).
