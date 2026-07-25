# KathanaBot 1.0.67

## What changed

- Settings no longer feel "stuck" while the bot is running. Every control change still applies to the running bot instantly, but the disk save behind it now happens on a background thread and coalesces rapid edits into one write instead of freezing the UI on every keystroke/click.
- Fixed a bug where toggling several Combat Skill checkboxes (or Calibration Region cells) back-to-back only applied the first change to the running bot - the rest looked toggled in the grid but the bot kept using the old values until something else nudged a refresh. Every toggle now reaches the running bot immediately, no matter how fast you click.
- Rebuilt the standalone EXE and published this release through the in-app updater channel.

## Recent change history - last 5

1. **No more dropped rapid grid edits:** quick back-to-back checkbox toggles in Combat Skills/Calibration Regions all reach the running bot now, not just the first one.
2. **Non-blocking settings persistence:** the full settings save moved off the UI thread and is debounced, so editing anything no longer freezes the window while it writes to disk.
3. **Live config push unchanged and instant:** the fix only touched disk persistence timing - pushing your changes into the running bot was already immediate and still is.
4. **Explicit saves stay synchronous:** Save Settings, Save Profile, and app close still write to disk immediately so nothing is lost.
5. **Fresh standalone build:** rebuilt and versioned for this release.
