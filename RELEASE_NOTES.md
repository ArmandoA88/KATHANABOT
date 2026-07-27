# KathanaBot 1.0.83

## What changed

- Added a **Bundle Icon** calibration to Arrow Unbundle: capture a crop of what a bundle's slot icon actually looks like, and a configured point is only double-right-clicked when its current slot still matches - this prevents the "enter the number of items to move" prompt from ever opening in the first place, instead of reacting to it afterward. A Tolerance value controls how strict the match is.
- Removed the older reactive move-quantity-dialog handling (the `arrow_move_dialog_rect` scan region, its OCR detector, the Cancel Point picker, and the Escape-key fallback) now that Bundle Icon matching handles this at the source. Fewer moving parts, no risk of an Escape press closing the whole inventory.
- Added **Auto Resurrect**: a dedicated resurrection/revive confirmation dialog detector (Auto-Pot tab), fully independent from Auto Accept Party/Ress since this dialog can appear at a different screen position. Calibrate the `resurrect_scan_rect` region (Vision tab) and an OK click point (live-click picker, with its own red click overlay), then turn it on. The OK click uses the same verified-click approach as Arrow Unbundle (real cursor move + confirm + brief foreground if needed) so it lands correctly even when the game isn't focused or the cursor is on another monitor.
- Reworked the Auto-Pot tab layout: Unstuck/Retarget and Auto Resurrect now sit side by side instead of stacked, freeing up vertical space so the Notifications + Loot Matching panel isn't cut off. Removed the "Apply To Heal/Mana/Max-HP Rows", "Test Alarm + Notify", and "Test Notification" buttons.

## Recent change history - last 5

1. **Bundle Icon matching:** Arrow Unbundle only clicks a slot that still looks like a bundle, avoiding the move-quantity prompt entirely.
2. **Auto Resurrect:** a separate, calibrated auto-accept for resurrection dialogs, with a reliable verified click.
3. **Simplified arrow-unbundle dialog handling:** removed the now-unnecessary scan region, OCR detector, Cancel Point, and Escape fallback.
4. **Auto-Pot layout cleanup:** more room for Notifications + Loot Matching; removed three manual test/apply buttons.
5. **FS (Full Support) role:** a dedicated support-only mode that never targets or retargets, for characters that only heal/buff.
