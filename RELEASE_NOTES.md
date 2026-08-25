# KathanaBot 1.0.117

## What changed

- **Added "Item Awards" tracking** (Auto-Loot tab): parses "PlayerName has earned ItemName" award messages from the existing scan area, lists each unique award with a live grid, and skips configured terms (Rupiah by default) so currency drops don't clutter the list - duplicate OCR reads of the same line are suppressed instead of re-listing the same award repeatedly. **Reworked the Home dashboard**: a new Compact/Standard/Detailed view selector, a custom-painted sidebar rail (replacing the native Windows tab chrome to avoid theme-color flicker), a summary bar with kills/hour, and new analytics cards for kill efficiency (kills per hour, average kill/search time, loot-per-kill). **Fixed the session kill counter undercounting** long fights: it now confirms a kill from the last time the target's HP bar was actually seen rather than the last attack keypress, since long auto-attack sequences outlast the old fixed window.

## Recent change history - last 5

1. **Added Item Awards tracking with a skip-terms filter**, reworked the Home dashboard with a Compact/Standard/Detailed selector, a custom sidebar rail, and kill-efficiency analytics cards, and fixed the session kill counter undercounting long fights.
2. **Redesigned the in-game HUD with live HP/MP/target stats**, added progress bars and an EXP rate sparkline to the dashboard cards, split Rupiah into its own card with session earnings, and added a session kill counter.
3. **Added the Home dashboard tab and blue theme redesign**; leveling guardrails pause-and-resume instead of stopping the engine, death-message pause now clears on its own, and target-name OCR refreshes faster with better Dadati folding.
4. **Added "Loot After Kill"**, switched chat text to clipboard-paste (with save/restore), and tightened click precision across Auto Party/Auto Resurrect/Arrow Unbundle.
5. **Added "Auto Party"** (hidden behind Developer Mode, still being finished): key+click invite loop plus a separate chat-message loop, each with its own interval and a typeable/pickable click point.
