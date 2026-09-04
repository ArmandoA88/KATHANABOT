# KathanaBot 1.0.131

## What changed

- **Overhauled EXP and Rupiah OCR reading for confirmed-only telemetry.** EXP now tracks in hundredths of a percent to remove float drift, accepts its first read immediately as the baseline, and requires two consistent readings before accepting a backward jump or an unusually large advance so a single misread can no longer resync or poison the counters. Rupiah wallet reads use the same confirm-before-accept logic for large jumps, and EXP/Rupiah OCR now run sequentially instead of in parallel to stop the stalls that parallel WinRT OCR passes caused on some systems. Read intervals and retry logging are faster across the board.
- **Reworked Home and Achievements earned-stat tracking to a fixed baseline per bot run and character.** EXP and Rupiah gains are now odometers that only move forward off confirmed OCR, survive character swaps and restarts without resetting to zero, and separate "earned" from "spent" for the Rupiah wallet. A new **Reset Stats** control next to the dashboard view selector clears the EXE-session counters on demand without touching the in-game wallet or EXP.
- **Full Support Vidya reselection after a tank or individual heal now sends three backtick (`) presses** timed across ~300 ms instead of one, making the return to Vidya-targeted actions (self-survival, resurrection) more reliable after a party heal.
- **Buff Watch's skill picker now supports multi-select.** Ctrl+click adds any number of skill icons to the selection, and Apply adds a slot for each one in a single action instead of one at a time.
- **Disabled the Leveling guardrail while Full Support is active** to stop the two systems from fighting over control of the character.
- **Moved the feedback contact from an in-game PM to Discord.** The startup notice and its highlighted contact line now point to `mando1545` on Discord in all three languages instead of `xSAITAMAx` in-game.

## Recent change history - last 5

1. **Added Vidya self-survival and nearby party resurrection to Full Support**, organized Buff Watch skills by race alongside Library, added a Loot Scan Matching frequency control, a chat message input toggle, and an optional in-game chat key pause for Full Combat.
2. **Improved Full Support and Buff Watch:** added bulk HP-row alignment, fixed single-injured-member group heals, added the configurable high-priority assist skill, standardized shared HP-row sizing, and changed Buff Watch self-target to the backtick (`) key.
3. **Bundled a portable, self-extracting Buff Watch icon library** (232 starter icons) into the standalone EXE, added a root-level "Library" category plus Open Folder/Refresh buttons, and supports BMP/JPG alongside PNG.
4. **Added Item Awards tracking with a skip-terms filter**, reworked the Home dashboard with a Compact/Standard/Detailed selector, a custom sidebar rail, and kill-efficiency analytics cards, and fixed the session kill counter undercounting long fights.
5. **Redesigned the in-game HUD with live HP/MP/target stats**, added progress bars and an EXP rate sparkline to the dashboard cards, split Rupiah into its own card with session earnings, and added a session kill counter.
