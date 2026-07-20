# KathanaBot 1.0.51

## What changed

- Every successful Full or Lite bot start now checks for updates automatically without delaying bot startup.
- The Update tab turns green when the installed version is current and yellow when a newer semantic version is available. Checking is blue and failed checks are red.
- Auto-update version control identifies this build as `1.0.51`; standalone update selection rejects older or equal versions.

## Recent change history - last 5

1. **Automatic update status:** bot starts trigger a check and the tab color reports latest or available.
2. **Dadati evade:** avoids the unkillable Dadati target by moving and retargeting instead of attacking forever.
3. **Party status:** counts non-full parties more accurately and separates living from dead members.
4. **Mob-name OCR:** compares multiple enhanced samples and keeps the strongest complete name through capture flicker.
5. **Combat cooldowns and startup Notice:** stable per-skill timing plus automatic Notice closing after five seconds.
