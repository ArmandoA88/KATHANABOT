# KathanaBot 1.0.115

## What changed

- **Added a new "Home" dashboard tab** with a redesigned blue theme: a sidebar tab strip, an animated selection indicator, and live cards for HP/MP vitals, current target, and run duration, plus a play/pause control. **Leveling guardrails no longer stop the engine** - a triggered guardrail (low MP, no EXP progress, etc.) now pauses input and clears itself automatically once conditions recover, instead of cancelling the whole bot; guardrails are also suppressed while HP is near zero or a frame read is unreliable, since death and bad OCR frames were previously read as false guardrail trips. **Death-message detection now un-pauses combat on its own** if the death prompt disappears for 3 consecutive reads, instead of only clearing on full HP. Also tightened Dadati name-evasion OCR folding (`cl` -> `d`) and roughly doubled target-name refresh frequency for Monster Filter/Evade Dadati responsiveness.

## Recent change history - last 5

1. **Added the Home dashboard tab and blue theme redesign**; leveling guardrails pause-and-resume instead of stopping the engine, death-message pause now clears on its own, and target-name OCR refreshes faster with better Dadati folding.
2. **Added "Loot After Kill"**, switched chat text to clipboard-paste (with save/restore), and tightened click precision across Auto Party/Auto Resurrect/Arrow Unbundle.
3. **Added "Auto Party"** (hidden behind Developer Mode, still being finished): key+click invite loop plus a separate chat-message loop, each with its own interval and a typeable/pickable click point.
4. **Fixed click overlays disappearing whenever the control panel window is minimized**, and fixed the Auto Party invite key's click not landing (key press now foregrounds the game before the click).
5. **Added "Ask For Resurrection"**: nags party chat for a resurrection on a timer while death-paused, with optional map coordinates, without interrupting Auto Resurrect or Pause Combat On Death.
