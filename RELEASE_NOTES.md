# KathanaBot 1.0.156

## What changed

- **Loot Scanner (Alt) now clicks matched items directly instead of only alarming.** The calibrated loot area is divided into a configurable grid mesh (Columns x Rows, up to 40x40, default 6x4, in the Auto-Loot tab); on each scan it reads item labels, finds the grid square containing an allowed item name, and left-clicks that square's center. A new "Show Loot Grid Overlay" toggle lets you drag the loot polygon/corners and see the mesh lines directly over the game window, and a brief on-screen flash highlights the detected item's square and click point after each match.
- **Fixed a profile-loading edge case** where unsaved edits to the outgoing profile could be lost instead of flushed before switching to a new one.

## Recent change history - last 5

1. **Renamed the tab, heading, toggle, and calibration window to "Full Support (Vidya only)"** for clarity, and added a blue calibration notice explaining that the blue dots are character click points used for buffs and auras.
2. **Fixed the active Profile not surviving an app restart.** The loaded/saved profile name is now stored in the settings file, so KathanaBot reopens with the same profile still selected instead of falling back to unnamed settings. The Home dashboard header now also shows "Profile: " followed by its name at a glance, and the Profiles menu bolds/checks whichever profile is currently active. Also fixed Save Settings not updating the active profile, so editing settings while a named profile is loaded now writes those changes back into that profile's file.
3. **Overhauled EXP and Rupiah OCR reading for confirmed-only telemetry**, reworked Home and Achievements earned-stat tracking to a fixed baseline per bot run and character with a Reset Stats control, changed Full Support's Vidya reselection after a heal to three backtick (`) presses, added multi-select to Buff Watch's skill picker, disabled the Leveling guardrail while Full Support is active, and moved the feedback contact to Discord (`mando1545`).
4. **Added Vidya self-survival and nearby party resurrection to Full Support**, organized Buff Watch skills by race alongside Library, added a Loot Scan Matching frequency control, a chat message input toggle, and an optional in-game chat key pause for Full Combat.
5. **Improved Full Support and Buff Watch:** added bulk HP-row alignment, fixed single-injured-member group heals, added the configurable high-priority assist skill, standardized shared HP-row sizing, and changed Buff Watch self-target to the backtick (`) key.
