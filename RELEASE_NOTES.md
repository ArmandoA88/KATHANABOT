# KathanaBot 1.0.116

## What changed

- **Redesigned the in-game bot HUD**: it now shows edition, active/stopped state, HP/MP percent, EXP/hour, and current target with its HP right on the overlay - not just an on/off button - with a rounded panel, hover/press feedback, and a bigger default/resizable footprint. **Dashboard cards got richer**: the target card now has a live HP progress bar, the EXP card shows a level-up ETA plus session EXP gained and a recent-rate sparkline, and Rupiah now has its own card with wallet total, session earnings, and hourly rate. **Added an application-session kill counter** (shown on the Home status card) that only counts a kill after this engine actually attacked a living target and it then stayed gone across several reliable frames, so retargets/filters/avoids can't inflate it.

## Recent change history - last 5

1. **Redesigned the in-game HUD with live HP/MP/target stats**, added progress bars and an EXP rate sparkline to the dashboard cards, split Rupiah into its own card with session earnings, and added a session kill counter.
2. **Added the Home dashboard tab and blue theme redesign**; leveling guardrails pause-and-resume instead of stopping the engine, death-message pause now clears on its own, and target-name OCR refreshes faster with better Dadati folding.
3. **Added "Loot After Kill"**, switched chat text to clipboard-paste (with save/restore), and tightened click precision across Auto Party/Auto Resurrect/Arrow Unbundle.
4. **Added "Auto Party"** (hidden behind Developer Mode, still being finished): key+click invite loop plus a separate chat-message loop, each with its own interval and a typeable/pickable click point.
5. **Fixed click overlays disappearing whenever the control panel window is minimized**, and fixed the Auto Party invite key's click not landing (key press now foregrounds the game before the click).
