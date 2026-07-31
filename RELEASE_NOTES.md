# KathanaBot 1.0.97

## What changed

- **Fixed Auto Relaunch's post-launch clicks crashing on multi-monitor setups.** Clicking a calibrated game coordinate after an auto-relaunch threw "Arithmetic operation resulted in an overflow" whenever the game window sat on a monitor positioned left of or above the primary monitor (negative absolute screen coordinates). The click now works the same regardless of monitor layout.
- **Fixed Auto Resurrect and Auto Accept Party/Ress not detecting or clicking their dialogs reliably.** Both used a "best single line" OCR read that could silently discard the line containing "resurrect"/"invited...party" on a multi-line dialog, and a "skip if nothing changed" optimization that could freeze re-scanning forever after a single misread on a static dialog. Both now use full-block OCR and re-scan continuously while a dialog might be showing.
- **Fixed the click-overlay corrupting the bot's own screen reads.** The always-on-top "Show Click Overlay" marker (used by Auto Resurrect, Auto Relaunch click steps, and Arrow Unbundle) could get baked directly into the bot's screen capture on games that require the raw-desktop capture fallback, painting over the exact dialog text/buttons it needed to read. The overlay is now excluded from all screen capture while remaining visible to you.
- **Auto Accept Party and Auto Accept Ress are now two separate toggles** instead of one combined switch, so either can be enabled independently.
- **Removed the Retarget Now (E) button** from the Combat panel.

## Recent change history - last 5

1. **Auto Resurrect/Auto Accept Party-Ress reliability fixes**, an overlay-vs-OCR capture conflict fix, split Auto Accept Party/Ress into two buttons, and removed Retarget Now (E).
2. **Fixed Auto Relaunch's post-launch clicks crashing** with an overflow error on multi-monitor setups.
3. **Buff Watch**: auto-recast a buff when its icon disappears from a shared, calibrated Buff Area, with a categorized icon library/picker. Also: Hold to Show Game Window now works reliably across multiple monitors.
4. **Much more complete Fixed AutoPots help (EN/ES/FIL)** in the Lite tab, replacing the old terse EN/ES-only blurb with a full step-by-step walkthrough.
5. **Zoom Selected Region defaults to on** in the Vision tab's Snapshot panel, matching Live.
