# KathanaBot 1.0.81

## What changed

- Added an **FS (Full Support)** role to the Combat Full tab, replacing the old "Ignore Skill Min HP/MP" button. FS is a support-only mode: it never presses E, so the character never selects or changes a target - normal retargeting, forced retargeting (stuck-target recovery, avoid-high-HP, non-mob-target, unreachable-target), the manual "Retarget Now (E)" button, and the Dadati evade maneuver are all fully disabled while FS is on. "Auto Retarget If Stuck" and "Retarget Now (E)" are grayed out in the UI while FS is active, since neither can do anything.
- Fixed FS initially blocking every action (including buffs and attacks) because they were gated on having a valid target, which FS never acquires. All action roles now fire purely on their own cooldown/HP-MP thresholds while FS is active, regardless of targeting.
- The Lite bot's own longstanding behavior of ignoring per-row Min HP/MP thresholds is preserved unchanged; that bypass was only ever meant for Lite, and removing the Full-tab toggle no longer affects it.

## Recent change history - last 5

1. **FS (Full Support) role:** a dedicated support-only mode that never targets or retargets, for characters that only heal/buff.
2. **All roles work without a target:** buffs, attacks, and other roles no longer wait on a target that FS will never acquire.
3. **Retarget controls auto-disable:** "Auto Retarget If Stuck" and "Retarget Now (E)" gray out while FS is on.
4. **Lite bot unaffected:** Lite's always-on Min HP/MP bypass keeps working exactly as before.
5. **Fresh standalone build:** rebuilt and versioned for this release.
