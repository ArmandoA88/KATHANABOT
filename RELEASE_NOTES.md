# KathanaBot 1.0.118

## What changed

- **Bundled a portable Buff Watch icon library** (232 starter icons) directly inside the standalone EXE: a `BuffWatchIcons` folder is created beside the EXE and missing starter icons are extracted into it automatically on first launch, so the library travels with the EXE instead of depending on a separate install step. Drop PNG/BMP/JPG/JPEG files straight into that folder (or its `general`/`ashura_rakshasa`/etc. subfolders) and the filename becomes the buff name - no folder structure required. The icon selector adds a "Library" category for root-level icons plus **"Open Icon Library Folder"** buttons on both the Buff Watch tab and the icon picker for quick access, and a **Refresh** button to pick up newly dropped files without reopening the picker.

## Recent change history - last 5

1. **Bundled a portable, self-extracting Buff Watch icon library** (232 starter icons) into the standalone EXE, added a root-level "Library" category plus Open Folder/Refresh buttons, and supports BMP/JPG alongside PNG.
2. **Added Item Awards tracking with a skip-terms filter**, reworked the Home dashboard with a Compact/Standard/Detailed selector, a custom sidebar rail, and kill-efficiency analytics cards, and fixed the session kill counter undercounting long fights.
3. **Redesigned the in-game HUD with live HP/MP/target stats**, added progress bars and an EXP rate sparkline to the dashboard cards, split Rupiah into its own card with session earnings, and added a session kill counter.
4. **Added the Home dashboard tab and blue theme redesign**; leveling guardrails pause-and-resume instead of stopping the engine, death-message pause now clears on its own, and target-name OCR refreshes faster with better Dadati folding.
5. **Added "Loot After Kill"**, switched chat text to clipboard-paste (with save/restore), and tightened click precision across Auto Party/Auto Resurrect/Arrow Unbundle.
