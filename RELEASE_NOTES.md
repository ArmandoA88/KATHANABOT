# KathanaBot 1.0.55

## What changed

- Full Combat's Dadati evasion recognizes OCR-confused variants including `DadatI`, `Dadatl`, and `Dadat1`.
- Auto-Loot detections are silent: the Windows alert and console beeps were removed.
- Pickup by matched loot name evaluates every OCR label and uses the strongest match instead of the first match.
- The dynamic pickup target is the matched label center plus the configured offsets, avoiding the previous overly-low click position.
- Pickup `F` presses use foreground physical input for better game compatibility.
- If an aggregate OCR match has no label rectangle, an existing saved pickup point can be used as a fallback.
- Disconnect recovery OCR reads `OK` inside `disconnect_ok_rect`, left-clicks the button, and waits for the old game process to close before starting Auto Relaunch.
- Relaunch is withheld if the old process remains open, preventing duplicate game instances.
- Lite fixed AutoPots track HP and MP recovery independently. After three sends with no upward bar movement, that potion is paused instead of repeating forever.
- A paused Lite potion automatically re-arms when its corresponding bar reading rises by a reliable margin.

## Recent change history - last 5

1. **Lite AutoPot anti-spam:** HP and MP independently pause when repeated sends show no bar recovery.
2. **OCR-gated disconnect recovery:** clicks OK and confirms the old process closed before reopening.
3. **Full loot and Dadati reliability:** quieter detection, best-region clicks, and OCR-safe Dadati aliases.
4. **Lite resource skill roles:** multiple slots can trigger at separate HP/MP percentages.
5. **Lite-only HP/MP overlay:** Lite rectangles and calibration controls are separate from Full user settings.
