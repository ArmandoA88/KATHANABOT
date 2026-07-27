# KathanaBot 1.0.82

## What changed

- Fixed Arrow Unbundle getting stuck when a configured slot holds loose (already-unbundled) arrows instead of a bundle. Right-clicking a bundle silently unbundles it, but right-clicking loose arrows opens the game's "enter the number of items to move" quantity prompt instead - a blocking dialog that stalled combat until closed, with no way to predict in advance which a given slot holds. The bot now scans for that prompt every loop, ahead of everything else, and closes it immediately with Escape the moment it appears (Escape only - never a click near the quantity field or OK/Cancel, to avoid any risk of actually moving real arrows).
- Requires calibrating a new region: `arrow_move_dialog_rect` in the Vision tab's region list, sized around where "Please enter the number of items to move" appears on your screen. A placeholder default is set, but it must be calibrated per game window/resolution the same as every other detection region.

## Recent change history - last 5

1. **Arrow unbundle move-quantity dialog fix:** loose arrows in a bundle slot no longer stall combat behind a blocking prompt.
2. **New calibration region:** `arrow_move_dialog_rect` detects the prompt so it can be auto-dismissed.
3. **Safe dismissal:** the prompt is closed with Escape only, never a click, so no arrows can be accidentally moved.
4. **FS (Full Support) role:** a dedicated support-only mode that never targets or retargets, for characters that only heal/buff.
5. **All roles work without a target:** buffs, attacks, and other roles no longer wait on a target that FS will never acquire.
