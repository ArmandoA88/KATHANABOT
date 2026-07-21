# KathanaBot 1.0.54

## What changed

- The Party Ask panel was removed from Lite and Lite party automation is now forced off.
- Every Lite primary and secondary skill slot can be assigned `Attack`, `HP`, `MP`, or `Buff`.
- Each HP/MP slot has an independent `At %` trigger and retains its own cooldown.
- Multiple thresholds are supported, such as HP 70% on slot 1, HP 30% on slot 2, and MP 10% on slot 3.
- Fixed key 9/0 AutoPots remain optional and operate independently from resource skill roles.
- Lite automatically scans the required bar whenever any enabled HP/MP skill needs it.
- Combat Full behavior and settings are unchanged.

## Recent change history - last 5

1. **Lite resource skill roles:** multiple slots can trigger at separate HP/MP percentages.
2. **Lite-only HP/MP overlay:** Lite rectangles and calibration controls are separate from Full user settings.
3. **Lite whole-bar AutoPots:** Full combat's HP/MP scanner is reused by Lite without sharing configuration.
4. **Automatic update status:** bot starts trigger a check and the tab color reports latest or available.
5. **Dadati evade:** avoids the unkillable Dadati target by moving and retargeting instead of attacking forever.
