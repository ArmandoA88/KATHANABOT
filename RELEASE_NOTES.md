# KathanaBot 1.0.99

## What changed

- **Fixed Auto Loot picking up everything nearby instead of just your allow-list.** After pressing F, the bot checked the revealed item's name using the same lookup the combat loop uses for monster names - which, if no fresh OCR result was ready yet, silently returned whatever name was cached from *before* you pressed F (e.g. your last combat target) instead of the actual item. The filter almost never saw the real name, so it kept whatever F grabbed. It now does a direct, blocking read of the freshly-revealed name every time.
- **Added a "Verify (ms)" setting next to Loot Pickup's interval**, so you can give the game more time to render the item's name before the bot reads it (default raised from an effective 120ms to 220ms) - raise it further if drops still get picked up incorrectly on a slower connection.
- **Fixed the Loot Scanner (Alt) silently freezing on a still drop.** Same class of bug fixed earlier for the resurrect/party-invite dialogs: a drop that isn't moving looked "unchanged" to the scanner's pixel-change gate, which stopped it from ever re-reading that spot again after a single missed OCR pass. The gate is now removed for this scan.
- **Exposed the mob max-HP/life-text OCR interval in the UI** as "Mob Life OCR ms" next to "Mob OCR ms" (Diagnostics tab) - previously hardcoded at 450ms. Both now show a tooltip explaining the recommended default and that lowering them trades CPU/OCR load for faster refresh.

## Recent change history - last 5

1. **Auto Loot reliability fixes**: the F-press name filter now reads the real revealed item name instead of a stale cached one, a tunable verify delay, and removed a gate that could freeze the Loot Scanner on a still drop. Also exposed "Mob Life OCR ms" in the UI.
2. **Lite AutoPots' "Show HP/MP Overlay" jumps to a zoomed Vision tab preview** of hp_bar automatically, making the bars much easier to see while aligning the overlay.
3. **Auto Resurrect/Auto Accept Party-Ress reliability fixes**, an overlay-vs-OCR capture conflict fix, split Auto Accept Party/Ress into two buttons, and removed Retarget Now (E).
4. **Fixed Auto Relaunch's post-launch clicks crashing** with an overflow error on multi-monitor setups.
5. **Buff Watch**: auto-recast a buff when its icon disappears from a shared, calibrated Buff Area, with a categorized icon library/picker. Also: Hold to Show Game Window now works reliably across multiple monitors.
