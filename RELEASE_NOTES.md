# KathanaBot 1.0.86

## What changed

- **Trimmed redundant text from notification bodies:** the "Character: ..." line is gone from the Stats and On-Demand Status notifications now that the character's name is already shown in the title. The "Party: ..." (members/alive) line is also removed from the Stats notification - no longer needed.
- **Stats Interval (min) now supports 0-9999:** the maximum went from 1440 to 9999, and setting it to **0 disables periodic stats notifications entirely** instead of clamping to a 1-minute minimum.

## Recent change history - last 5

1. **Notification body cleanup + Stats Interval 0=off:** dropped the redundant Character/Party lines from Stats and On-Demand notification bodies, and Stats Interval (min) now allows 0-9999, with 0 disabling periodic stats notifications.
2. **HP alert speed-up + character name in every notification title, fixed name detection cadence:** 30s/50-sample death confirmation (was 60s/100), all alert titles now show the character's name, and character-name OCR now refreshes every 5s instead of every 30 minutes.
3. **Pause Combat On Death:** stops all combat skills after 3 confirmed reads of the death message, resumes at full life, without pausing Auto Resurrect or anything else.
4. **Bundle Icon matching:** Arrow Unbundle only clicks a slot that still looks like a bundle, avoiding the move-quantity prompt entirely.
5. **Auto Resurrect:** a separate, calibrated auto-accept for resurrection dialogs, with a reliable verified click.
