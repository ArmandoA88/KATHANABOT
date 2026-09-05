# KathanaBot 1.0.147

## What changed

- **Fixed the active Profile not surviving an app restart.** The loaded/saved profile name is now stored in the settings file, so KathanaBot reopens with the same profile still selected instead of falling back to unnamed settings. The Home dashboard header now also shows "Profile: " followed by its name at a glance, and the Profiles menu bolds/checks whichever profile is currently active.
- **Fixed Save Settings not updating the active profile.** Editing settings while a named profile is loaded now writes those changes back into that profile's file (not just the shared settings file), so the profile actually reflects what you last saved instead of reverting on next load.
- **RESU's periodic chat message now types character-by-character instead of pasting via Ctrl+V**, so it no longer touches or overwrites your clipboard.

## Recent change history - last 5

1. **Overhauled EXP and Rupiah OCR reading for confirmed-only telemetry**, reworked Home and Achievements earned-stat tracking to a fixed baseline per bot run and character with a Reset Stats control, changed Full Support's Vidya reselection after a heal to three backtick (`) presses, added multi-select to Buff Watch's skill picker, disabled the Leveling guardrail while Full Support is active, and moved the feedback contact to Discord (`mando1545`).
2. **Added Vidya self-survival and nearby party resurrection to Full Support**, organized Buff Watch skills by race alongside Library, added a Loot Scan Matching frequency control, a chat message input toggle, and an optional in-game chat key pause for Full Combat.
3. **Improved Full Support and Buff Watch:** added bulk HP-row alignment, fixed single-injured-member group heals, added the configurable high-priority assist skill, standardized shared HP-row sizing, and changed Buff Watch self-target to the backtick (`) key.
4. **Bundled a portable, self-extracting Buff Watch icon library** (232 starter icons) into the standalone EXE, added a root-level "Library" category plus Open Folder/Refresh buttons, and supports BMP/JPG alongside PNG.
5. **Added Item Awards tracking with a skip-terms filter**, reworked the Home dashboard with a Compact/Standard/Detailed selector, a custom sidebar rail, and kill-efficiency analytics cards, and fixed the session kill counter undercounting long fights.
