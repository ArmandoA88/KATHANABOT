# Implementation Notes

## Scope
- Added auto resurrection prompt acceptance.
- Kept existing auto party invite acceptance.
- Unified both into a single auto-accept flow that clicks the configured OK region.

## Behavior
- OCR scans the configured prompt region (`party_invite_scan_rect`).
- If prompt text matches:
  - Party invite prompt, or
  - Resurrection/revive prompt,
  then bot clicks the configured OK region (`party_invite_ok_rect`).
- Cooldown is applied to avoid repeated rapid clicks.

## UI/Settings
- Existing toggle now covers both prompt types.
- Button text changed to:
  - `Auto Accept Party/Ress: ON`
  - `Auto Accept Party/Ress: OFF`
- Toggle state is now persisted in `user_lists.json` under:
  - `PromptAutoAcceptEnabled`

## Detection Rules
- Party prompt detection:
  - Matches invite/join/party phrases.
- Resurrection prompt detection:
  - Matches OCR variants like `resurrect`, `resurrection`, `resurect`, `ressurect`, `revive`, `revival`.
  - Also supports fuzzy prompt text requiring resurrection-like text + prompt words (`accept`, `request`, `yes`, `ok`, `want`).

## Files Changed
- `ui/KathanaBotControlPanel/BotEngine.vb`
  - Replaced party-only handler with combined auto-accept prompt handler.
  - Added resurrection prompt detector.
  - Updated logs/reason messages to generic prompt acceptance.
- `ui/KathanaBotControlPanel/Form1.vb`
  - Updated toggle labels/log text to Party/Ress wording.
  - Added persisted setting `PromptAutoAcceptEnabled`.
  - Loads/saves prompt auto-accept toggle state.

## Quick Test
1. Run bot with `Auto Accept Party/Ress` enabled.
2. Trigger a party invite popup; verify bot clicks OK.
3. Trigger a resurrection popup; verify bot clicks OK.
4. Disable toggle and repeat; verify no auto-accept click occurs.
