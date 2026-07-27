# KathanaBot 1.0.84

## What changed

- Added **Pause Combat On Death** (Auto-Pot tab, inside Auto Resurrect): detects the death message ("If you click 'OK', you will respawn at the last saved location.") in a calibrated `death_message_rect` region. Once confirmed by 3 consecutive OCR reads - a single stray misread won't trigger it - every combat-skill row (attack, buff, heal, mana, max_health, special) is paused. Auto Resurrect, Auto Accept Party/Ress, disconnect detection, and everything else keep running normally during the pause; only the Actions list is suppressed. Combat resumes automatically once HP is back to full.
- This is fully independent detection from Auto Resurrect - separate scan region, separate confirm counter - since the two dialogs can appear at different times and positions.

## Recent change history - last 5

1. **Pause Combat On Death:** stops all combat skills after 3 confirmed reads of the death message, resumes at full life, without pausing Auto Resurrect or anything else.
2. **Bundle Icon matching:** Arrow Unbundle only clicks a slot that still looks like a bundle, avoiding the move-quantity prompt entirely.
3. **Auto Resurrect:** a separate, calibrated auto-accept for resurrection dialogs, with a reliable verified click.
4. **Simplified arrow-unbundle dialog handling:** removed the now-unnecessary scan region, OCR detector, Cancel Point, and Escape fallback.
5. **Auto-Pot layout cleanup:** more room for Notifications + Loot Matching; removed three manual test/apply buttons.
