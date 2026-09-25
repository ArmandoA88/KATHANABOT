# KATHANA BOT RELOADED 1.4.0

Self-contained Windows x64 bot. Keyboard actions use SendInput and the selected game must have focus. Close older Reloaded instances before opening a newer EXE.

## Skills and timing

Enable the skills in **Keys**, assign their roles and intervals, select the game, and press Start. Regular mode now has **no shared input-rate cap or artificial one-second gap**. Every key follows its own configured interval after release, with a 60 ms hold. Ready keys are serviced one at a time; there are no simultaneous holds or catch-up bursts. Windows, the 20 ms timer and key holds still limit physical throughput.

Heal and Mana retain their HP/MP thresholds and require fresh calibrated readings. Repair retains five separate confirmed sightings within ten minutes. These conditions were not removed. Recovery roles take priority, followed by the oldest due ordinary skill. A key held by the user is skipped so other ready keys can run. Manual Stop/F12 stops; focus loss pauses; Keep Game Active attempts to restore and focus the selected game once per second.

## Home and overlays

**Home** shows session duration, input counts and recent inputs/second, last action, enabled skills, focus, HP/MP, repair state, target name/HP/numbers, EXP/Prana, Rupiahs, coordinates, character/level, map name, chat, buffs, party, death/resurrection, loot and relaunch-screen readings. Unknown values are marked as uncalibrated; old values show their age. Values are OCR text or bar estimates, not direct server data. There are no inferred kill counts or fabricated EXP totals.

In **Overlays**, use **Set area** for each game display, then drag to draw/move/resize and press Enter to save (Escape cancels). The 22 regions include all named rectangular calibration areas in the old Full bot plus a rectangular loot area, relaunch screen and character/map text. Areas scale with the game client. Existing Reloaded HP/MP/repair/disconnect calibration is preserved. Old bot pixel-based settings are not imported automatically.

Toggle all outlines or individual regions, and show/hide the draggable, resizable Go/Stop game panel. It now includes target name, map X/Y and input counts/rate. Move the panel outside calibrated areas. The buff region shows an image on Home; icon identities are not classified. Buff previews hide when the game loses focus. The added monitors do not implement the old bot's automatic party clicks, resurrection, loot polygon/pathing, translation or relaunch actions. This is the first dashboard/overlay port, not a replacement of those engines.

Extra OCR scans run sequentially in the background; keyboard actions do not wait for Home OCR. Calibrating more regions increases refresh latency. The game must remain visible and unobscured. Home preserves last readings with stale labels when unfocused.

## Stress test

Stress test remains an optional, separately paced diagnostic mode. Set starting rate, increment, maximum and **duration per stage in active seconds** (1–86,400). Calibrate the disconnect message area and customize its phrases. The starting stage retains per-key intervals; increases shorten them proportionally. A global stage-rate limit applies only in stress mode. Holds are 60 ms, shortened to fit high rates. The maximum stage ends after its configured duration. Test mode is off on each application launch; configuration is saved.

Two fresh matching OCR scans stop a test. The first match pauses input, and a ten-second OCR timeout stops with an explicit error. Focus recovery runs before OCR; focus loss and unavailable disconnect scans pause stage timing. There is no auto-reconnect. Only test servers you are allowed to test.

Records are saved every second and at stage changes/end/disconnect in `%APPDATA%\KathanaBotReloaded\stress-<UTC timestamp>.jsonl`. They include requested and measured inputs/second, stage duration, active time, input counts and outcome/evidence. Measurements are successful Windows key-downs, not server-accepted actions or proof of the disconnect cause.

## Sharing results

Use **Home > Export results** to save a ZIP containing current session/key counts and the newest 20 stress logs. Export excludes captured window titles and OCR evidence from stress logs, and does not include settings, screenshots or chat. No results are uploaded automatically.

## Updates

The **Updates** tab checks automatically at startup by default, or with **Check now**. It uses GitHub releases from `ArmandoA88/KATHANABOT`, accepting only stable tags `reloaded-vX.Y.Z` with both `KathanaBotReloaded-win-x64-standalone.exe` and its `.sha256` asset. It never installs the original Kathana bot's assets. Checks use GitHub's public [releases API](https://docs.github.com/en/rest/releases/releases#list-releases); a private repository or rate limit is reported as a check failure.

When a newer release is available, **Download, install and restart** stops input, verifies SHA-256, atomically replaces the EXE while retaining `.previous`, and restarts stopped. Settings/results stay in AppData. Installation needs write permission to the EXE directory. Failed installations write `update-error.txt` under AppData. Checksums detect corrupted downloads; they rely on the configured release publisher being trusted.

Run `./build-reloaded-release.ps1` to build, test and prepare the standalone plus update assets locally. Publish those two assets under a stable `reloaded-vX.Y.Z` GitHub release to distribute an update. The build script does not upload/publish. Automatic checks cannot offer a build until its release is published.

## Verification and data

Run the EXE with `--self-test --ocr` for scheduler, stress timing/eligibility, detection, migration, sharing filter, update channel/checksum and atomic replacement tests. They send no keyboard input. Preview modes (`--preview <file.png> home|overlays|updates|stress|hud|small`) render without starting the bot or checking updates.

Settings are in `%APPDATA%\KathanaBotReloaded\settings.json`; per-key input logs remain in `keys.log`. The new version preserves existing keys and calibration. The default new-profile window is larger for Home; saved window sizes remain supported.
