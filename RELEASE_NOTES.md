# KathanaBot 1.0.65

## What changed

- The Leveling Agent now leaves `Fighting` much faster after a real target disappears. Lost-target confirmation is two frames, the normal grace window is 0.6-1.5 seconds, and a six-second hard ceiling releases any stale combat lock so search/travel can resume.
- Fixed the self-sustaining combat-lock loop: attacks can no longer keep their own target permission alive after the target is gone, and noisy mob-HP color is trusted only after a recently visible target window.
- Background OCR work is now session-safe. Results that finish after Stop/Start are discarded, OCR failures are logged without flooding, and stale unreachable text must clear before it can retrigger recovery.
- Added named Full + Lite settings profiles. Use `Profiles` in Combat Full to save, load, and delete configurations for different characters or farming spots.
- Added persistent session history for runs lasting at least 30 seconds, including duration, EXP/rate, rupiahs/rate, restarts, repairs, and unreachable events. Open it from Diagnostics with `Open Session History`.
- Settings saves now use an atomic temporary-file swap and retain a backup; startup falls back to that backup when the primary settings file is missing, empty, or corrupt.
- Reorganized the Leveling tab into Getting Started, Safety Stops, Map & Travel, and Route Recording sections with clearer descriptions.
- Low/zero HP no longer stops the Leveling Agent because a bad pixel reading could cause a false shutdown; AutoPots, heal actions, and the HP=0 alarm remain active.

## Recent change history - last 5

1. **Target-loss recovery:** combat lock now clears quickly and cannot be extended by the attacks it permits.
2. **OCR session safety:** late results cannot leak across bot restarts; repeated OCR faults are visible but rate-limited.
3. **Named profiles and safer settings:** switch complete configurations and recover automatically from a damaged primary settings file.
4. **Session history:** compare farming runs using the new CSV history and Diagnostics shortcut.
5. **Clearer Leveling workflow:** grouped setup/travel controls, safer target evidence, and no false stop from a single bad HP reading.
