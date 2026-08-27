# KathanaBot 1.0.125

## What changed

- **Added a visual calibration wizard for Full Support** ("Calibrate party + HP bars"): click-drag over a live game capture to set the party list area, then place each member's HP bar and select-click point per party slot (up to 7), with a member editor for naming and enabling/disabling slots. Full Support now heals off these confirmed HP-bar reads instead of guessed coordinates, and the tab shows the calibrated party area and member count at a glance.
- **Fixed false Home EXP gains and impossible hourly rates caused by OCR misreads.** EXP now requires a confirmed startup baseline, rejects backward/impossible jumps, recognizes level rollover only from 90%+ to 10% or less, keeps accepted pace below 10% per hour, and resets the Home session counters on each Full run.
- **Added an optional Full Combat in-game chat key pause, off by default.** When enabled, Enter in the selected game window pauses all automated key output for typing; the next Enter resumes the already-running bot. The Combat Skills panel shows OFF, READY/ARMED, or PAUSED status.

## Recent change history - last 5

1. **Improved Full Support and Buff Watch:** added bulk HP-row alignment, fixed single-injured-member group heals, added the configurable high-priority assist skill, standardized shared HP-row sizing, and changed Buff Watch self-target to the backtick (`) key.
2. **Bundled a portable, self-extracting Buff Watch icon library** (232 starter icons) into the standalone EXE, added a root-level "Library" category plus Open Folder/Refresh buttons, and supports BMP/JPG alongside PNG.
3. **Added Item Awards tracking with a skip-terms filter**, reworked the Home dashboard with a Compact/Standard/Detailed selector, a custom sidebar rail, and kill-efficiency analytics cards, and fixed the session kill counter undercounting long fights.
4. **Redesigned the in-game HUD with live HP/MP/target stats**, added progress bars and an EXP rate sparkline to the dashboard cards, split Rupiah into its own card with session earnings, and added a session kill counter.
5. **Added the Home dashboard tab and blue theme redesign**; leveling guardrails pause-and-resume instead of stopping the engine, death-message pause now clears on its own, and target-name OCR refreshes faster with better Dadati folding.
