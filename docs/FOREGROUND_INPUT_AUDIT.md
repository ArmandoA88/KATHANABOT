# Foreground input audit — KathanaBotControlPanel 1.0.230

This is the historical v1.0.230 audit for the retained foreground backend. Current
v1.0.231 defaults to background keyboard messages; see [input testing](INTERNAL_INPUT_HOOK.md).

Scope: the original `ui/KathanaBotControlPanel` project and all its compiled feature files. Kathana Bot Reloaded is separate and was not changed for this request.

## Current Trade foreground sender (1.0.244)

Trade's explicit foreground sender creates a separate native input platform bound
to the selected HWND and captured PID. It checks live/restored window identity and
exact foreground focus without opening the game process to query its executable
path. The parameterless platform and background/hook installation restrictions
retain their existing validation. This avoids reporting a focus failure for a
foreground client installed elsewhere or whose image path Windows cannot read.

Before the first Enter, activation is attempted once and focus is allowed up to
10 seconds to settle or be given by a user click. Stop/F12, changed/closed/minimized
windows and timeout stop before input. Later rows do not activate or wait to
reacquire focus. Typing still checks exact focus between events, and owned releases
and submitted-row receipts remain intact. Native validation is checked against an
owned external fixture; fake input records test the delivery and cancellation
sequence without sending keys to the game.

## Input boundary

`WindowsInput.Current` defaults to `ForegroundWindowsInput` with `NativeInputPlatform`. In normal operation, the sole native injection call is `SendInput`. Legacy message-shaped requests are converted to keyboard, Unicode, client-coordinate cursor and mouse packets; they are never delivered through PostMessage. The optional legacy SendKey flags remain API-compatible but cannot select background injection.

New down/move/text events require a bound, valid, non-minimized target, matching process ID and foreground window. Native target checks also validate the exact installed Kathana executable before activation or input. They reuse the retained background platform's read-only image validation without dispatching messages. Mouse down additionally verifies that the cursor's root window is the intended target. Click-client mapping checks client bounds. Cursor positioning uses normalized virtual-desktop SendInput coordinates, including monitors with negative origins.

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

BackgroundOnly always reads false; old saved flags cannot restore message injection. The old toggle is replaced by a Game must stay focused indicator. Existing feature/skill enablement settings are preserved. Features may request foreground activation; if Windows denies it, the attempted input is skipped. This release requires foreground operation; earlier background standalone files are retained.

## Validation

`run-all-tests.ps1` includes the new ForegroundInput.Tests suite, covering denied activation, focus loss between events, key/button cleanup, retrying blocked releases, Unicode, extended keys, pointer coverage, negative desktop coordinates, invalid windows/PIDs, native INPUT layout, legacy-setting migration and an audit rejecting legacy native injection imports.

The Trade sequence regression now uses the existing input-injection test seam instead of requiring native messages to an off-screen window. The production backend is tested separately with a simulated platform. The full suite covers combat, feature controls, Direct KP, Trade, Quiz, RESU, navigation, overlays, logs, translation and dialogs. Tests do not exercise live game acceptance of SendInput.

## 1.0.217 timing update
Every new key-down (including Unicode and modifiers) and mouse-button-down receives a fresh 5-15 ms wait. The wait runs outside the release lock, before foreground/coverage checks. Key-up, button-up, watchdog cleanup and cursor moves have no added delay. Configured skill cooldowns remain unchanged. Windows scheduling can make the observed wait longer than the requested milliseconds.

## 1.0.230 alternate method

Restored this backend after another user-reported termination with the SDL message approach. Short plain keys, Ctrl/Alt shortcuts and manual API keys retain the v1.0.229 randomized range: `max(150, requested)` through 250 ms. Holds of 250 ms or longer stay exact. Eligibility helpers now inspect the selected backend, so foreground test overrides are checked for actual focus/availability rather than being assumed eligible. Production background diagnostic modes are rejected before game activation in this build.

Controlled routing and cleanup tests do not identify the detection trigger or prove enforcement compatibility. No live game input was sent for this change.

## 1.0.250 Background mode (borrowed focus)

The input boundary above is unchanged: every key still goes out through the single `SendInput` entry point of
`NativeInputPlatform`, and no `PostMessage`, `SendMessage`, `keybd_event`, `mouse_event`, `SetCursorPos` or
`SendKeys` import exists. Background mode only changes the focus handling around a key:

* `ForegroundWindowsInput` takes the keyboard just before a new key-down (never for text, mouse or releases),
  under a separate hand-off lock that is never held with the send lock, and hands it back from the 20 ms watchdog.
* Activation uses the existing sequence (direct request, then attach to the foreground thread). Returning to the
  user's window uses the same sequence without validating it as a game, because it is not one.
* The game is re-stacked with `SetWindowPos(... SWP_NOACTIVATE)` anchored on the nearest visible window of another
  process; if a system moves activation when that is done, re-stacking is disabled for the session.
* New native imports are window-stack, class and idle queries only: `GetWindow`, `SetWindowPos`,
  `GetWindowLongPtrW`, `GetClassNameW`, `IsWindowVisible`, `GetAsyncKeyState` (user activity, not game input),
  `GetLastInputInfo` and `SHQueryUserNotificationState`.
* Mouse requests never borrow; with Background mode on `WindowsInput.KeyboardOnlyMode` is true so mouse
  workflows are paused by the existing keyboard-only policy. Trade keeps its own explicit foreground sender.
* The same limits apply: foreground checking and input dispatch cannot be made atomic, and nothing here makes
  injected input undetectable. See [Background mode](BACKGROUND_MODE.md).
