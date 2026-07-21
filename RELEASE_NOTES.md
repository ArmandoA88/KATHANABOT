# KathanaBot 1.0.53

## What changed

- Lite AutoPots now include a dedicated HP/MP calibration overlay in the same AutoPots panel.
- Lite HP/MP rectangles are persisted under Lite settings and never read or write the Full Vision rectangles.
- Lite uses the copied whole-bar detector without inheriting Full custom color settings.
- Moving or resizing a Lite bar preserves its selected potion-trigger percentage.
- The Lite Status panel and window are taller so every status line remains visible.
- Combat Full detection, overlay behavior, and saved settings are unchanged.

## Recent change history - last 5

1. **Lite-only HP/MP overlay:** Lite rectangles and calibration controls are completely separate from Full user settings.
2. **Lite whole-bar AutoPots:** Full combat's HP/MP scanner is reused by Lite without sharing configuration.
3. **Automatic update status:** bot starts trigger a check and the tab color reports latest or available.
4. **Dadati evade:** avoids the unkillable Dadati target by moving and retargeting instead of attacking forever.
5. **Party status:** counts non-full parties more accurately and separates living from dead members.
