# KathanaBot 1.0.50

## What changed

- Added **Evade Dadatis** in Vision. When fresh OCR sees Dadati, the bot blocks attacks, briefly taps W and S, and forces an E retarget. The game window must be on.
- Improved party counting for partial parties, dead members, dark-red HP bars, and different terrain backgrounds.
- Automatic Updates now shows release notes and a recent change history whether an update is available or the installed version is current.

## Recent change history — last 5

1. **Dadati evade:** avoids the unkillable Dadati target by moving and retargeting instead of attacking forever.
2. **Party status:** counts non-full parties more accurately and separates living from dead members.
3. **Mob-name OCR:** compares multiple enhanced samples and keeps the strongest complete name through capture flicker.
4. **Combat cooldowns:** uses monotonic per-skill timing so attacks do not remain incorrectly stuck on cooldown.
5. **Startup Notice:** automatically closes after five seconds while keeping the OK button available.
