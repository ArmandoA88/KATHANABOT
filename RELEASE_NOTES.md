# KathanaBot 1.0.109

## What changed

- **Added an "Achievements" tab** (Bot Debug Log, next to Key Summary and Loot History) showing Rupiah earned and EXP gained in rolling 10m / 30m / 60m / 24h windows - Rupiah as a plain number, EXP as a percentage. EXP tracking correctly handles leveling up mid-window: since the EXP bar resets to ~0% on a level-up, a naive "current % minus % N minutes ago" would show a nonsense negative number right after leveling - instead it tracks a running total that treats a level-up reset as continued forward progress, so a window spanning multiple level-ups still shows the true amount gained.
- **Key Summary's tracked window widened from 60 minutes to 24 hours**, with a new "Last 24h" column alongside Last 10m/30m/60m.
- **"Evade Dadati" now also recognizes Sachi Agua I** and its OCR-garbled variants (e.g. "Sachi Agua 1", "Sach1 Agua l", "SachI Agua I"), using the same glyph-folding technique already used for Dadati OCR variants. The checkbox is now labeled "Evade Dadati/Sachi Agua I + OCR variants".

## Recent change history - last 5

1. **Added an Achievements tab** tracking Rupiah earned and EXP gained over rolling 10m/30m/60m/24h windows, correctly handling level-ups mid-window.
2. **Key Summary now tracks a full 24 hours** instead of 60 minutes, with a new "Last 24h" column.
3. **"Evade Dadati" now also evades Sachi Agua I** and its OCR-misread variants.
4. **New installs now seed from a calibrated default profile** (timing, regions, combat skills, filters, autopot, vision) instead of bare factory numbers, excluding personal notification credentials.
5. **Fixed Hold on Place / Navigation interrupting combat to reposition**: movement and attacks can no longer fire in the same tick, and corrections now wait 600ms after the last attack.
