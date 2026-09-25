# Foreground input audit — KathanaBotControlPanel 1.0.216

Scope: the original `ui/KathanaBotControlPanel` project and all its compiled feature files. Kathana Bot Reloaded is separate and was not changed for this request.

## Input boundary

`WindowsInput.Current` defaults to `ForegroundWindowsInput` with `NativeInputPlatform`. The sole native injection call is `SendInput`. Legacy message-shaped requests are converted to keyboard, Unicode, client-coordinate cursor and mouse packets; they are never delivered through PostMessage. The optional legacy SendKey flags remain API-compatible but cannot select background injection.

New down/move/text events require a bound, valid, non-minimized target, matching process ID and foreground window. Mouse down additionally verifies that the cursor's root window is the intended target. Click-client mapping checks client bounds. Cursor positioning uses normalized virtual-desktop SendInput coordinates, including monitors with negative origins.

Owned key/button releases are allowed after focus loss to prevent stuck input. A 20 ms watchdog requests releases on focus loss or PID change and retries failed key-up/button-up calls. Shutdown also requests releases. It does not release keys the bot never pressed. Input failures are logged; no alternative injection API is attempted. Foreground checking and OS input dispatch cannot be made atomic, and this change does not make automation undetectable.

## Feature coverage

| Feature | Output route |
| --- | --- |
| Full and Lite attacks, HP/MP recovery, targeting, repair, buffs | BotEngine.SendKey ? common foreground request ? SendInput |
| Direct KP, auto-assist, loot-after-kill F holds | Same SendKey path with cancellation/release |
| Ctrl/Alt skill chords | Bound target ? shared keyboard SendInput with modifier cleanup |
| Loot scanner Right Alt, timed loot arrow holds | Explicit target binding ? keyboard SendInput; focus-loss release watchdog |
| Navigation, leveling, hold-place, Dadati movement | SendKey / owned movement releases |
| Buff self-click, support heal/tank/assist/resurrection, party acceptance, loot clicks and arrow-unbundle clicks | Client-point or verified-click helpers ? foreground mouse SendInput |
| Auto-party invitations/messages, party asks, resurrection asks, chat macros | Shared click/key helpers; clipboard Ctrl+V or UTF-16 SendInput text; no partial-text submission after failure |
| Trade whisper queue | Foreground checks between characters; Enter / UTF-16 text / Enter through shared backend; cancellation avoids submission |
| Quiz answer click burst | Explicit target binding, serialized burst ? mouse SendInput |
| RESU targeting, skill casts, trade clicks | Existing SendKey / verified-click helpers ? shared backend |
| Disconnect OK | Explicit target binding and foreground mouse SendInput |
| Post-relaunch click steps | Verified game/client target only; no fallback desktop click coordinates |
| Foreground activation | Window activation APIs only; synthetic Alt activation trick removed |
| Overlays, OCR/capture, freeze monitor | No input injection; SendMessageTimeout sends WM_NULL only for responsiveness |

## Profile and UI behavior

BackgroundOnly always reads false; old saved flags cannot restore message injection. The old toggle is replaced by a Foreground SendInput only indicator. Existing feature/skill enablement settings are preserved. Features may request foreground activation; if Windows denies it, the attempted input is skipped. There is no promise that every feature works while the game is minimized or behind another application.

## Validation

`run-all-tests.ps1` includes the new ForegroundInput.Tests suite, covering denied activation, focus loss between events, key/button cleanup, retrying blocked releases, Unicode, extended keys, pointer coverage, negative desktop coordinates, invalid windows/PIDs, native INPUT layout, legacy-setting migration and an audit rejecting legacy native injection imports.

The Trade sequence regression now uses the existing input-injection test seam instead of requiring native messages to an off-screen window. The production backend is tested separately with a simulated platform. The full suite covers combat, feature controls, Direct KP, Trade, Quiz, RESU, navigation, overlays, logs, translation and dialogs. Tests do not exercise live game acceptance of SendInput.

## 1.0.217 timing update
Every new key-down (including Unicode and modifiers) and mouse-button-down receives a fresh 5-15 ms wait. The wait runs outside the release lock, before foreground/coverage checks. Key-up, button-up, watchdog cleanup and cursor moves have no added delay. Configured skill cooldowns remain unchanged. Windows scheduling can make the observed wait longer than the requested milliseconds.
