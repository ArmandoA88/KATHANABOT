# Background mode (1.0.259)

The game stays behind other applications while the bot continuously observes its window and borrows keyboard focus only for short actions. It uses the existing foreground SendInput transport.

## Observation and decisions

- A Windows Graphics Capture session is bound to the exact HWND and process ID. Capture does not activate the game, inspect its memory, or inject code into it.
- At most ten frames per second are copied from the GPU. A single latest-frame buffer replaces old frames; processing never accumulates a backlog. Each reader receives its own bitmap.
- The client image is cropped from the captured window using its current bounds. A move or resize during readback discards the image; a resize recreates the frame pool. Minimized, closed or reused windows cannot supply frames. Source timestamps survive readback rather than being replaced by completion timestamps.
- Background mode exclusively uses window capture. It never falls back to copying the desktop, which could read an application covering the game. Unsupported or blocked capture pauses actions.
- Accepted frames must be at most **300 ms** old and pass the existing black-frame check. Each engine iteration records the source timestamp of its first accepted observation. Its decisions expire after **500 ms**; additional captures cannot extend that timestamp. Input checks freshness before and after activation.
- Full combat reads the latest continuous frame every iteration instead of falling back to old loop frames. Lite observes the window even when only attack keys are enabled. Direct KP obtains a fresh observation for each E/R pair and expires its engine permission after 500 ms.

## Keyboard bursts

1. Wait for user inactivity (default **1 second**) and check shell, locked-screen and full-screen blockers.
2. Remember the previous foreground window and the game's stack position, activate the game, and restack it behind the user's windows without deactivating it.
3. Send an owned key-down/up sequence. Combat rotations and Direct KP E/R pairs explicitly share one borrow.
4. Return focus **as soon as the key sequence or planned burst ends**. Production has no linger delay.
5. Leave **200 ms** before another borrow. Remaining actions are reconsidered from fresh observations rather than queued for catch-up.

The total borrow has a **750 ms budget**, measured from acquisition regardless of ongoing key activity. The 20 ms watchdog releases held keys and ends expired bursts. Remaining keys in the expired burst cannot reacquire focus. Windows scheduling or native activation calls can delay the actual hand-off, so 750 ms is a dispatch budget rather than a guaranteed wall-clock ceiling. Existing key-hold timing is preserved within that budget; hold waits stop early when focus is lost.

Typing, clicking, switching windows, Stop/F12, mode-off and shutdown end borrowing. A takeover between keys is checked synchronously too, so the next key cannot regain focus before the watchdog sees the change. Owned releases retry before handing focus to another application. A delivered key whose action sequence is interrupted consumes the action's cooldown without recording a completed action, preventing an immediate duplicate.

## Failures and controls

- Activation refusal skips input and retains the existing 750 ms retry pacing.
- Restacking or hand-back failure pauses new input. Select **Background mode OFF**, then **ON** to retry. If the previous window and its fallback neighbour have closed, the same recovery pause applies.
- The status line shows capture availability/age, current borrowing state, emitted keys and longest borrow. Emitted keys measure insertion into Windows' input stream, not confirmation by the game.
- The wait control remains 0–30 seconds; zero gives the bot priority over typing. Full-screen protection remains enabled by default. The game must be open and **not minimized**.
- It remains keyboard-only. Mouse workflows are paused through the existing runtime policy; saved feature settings are preserved. Trade whispers retain their explicit foreground input handling.
- Foreground-mode and background-mode command-line overrides and the local API's foreground-borrow mode name are preserved. Turning the mode off restores foreground supervision.

Capture sessions close on bot stop, mode-off and application shutdown. Switching targets retires old sessions; the capture registry is bounded. Capture startup and teardown run independently of the UI.

## Validation and limits

ForegroundInput.Tests simulates input and focus: bounded bursts, immediate return, rest intervals, stale capture/decision rejection, takeover between keys, release retries, hand-back failure, chord routing, interrupted-action cooldowns and Direct KP pairs. WindowCapture.Tests checks buffer ownership, source age, replacement, resize rejection and late callbacks. Its optional --native fixture captures owned dummy windows and verifies that a green covering window does not replace the blue source pixels, including movement, odd-width resize, minimization, PID mismatch and close. The fixture sends no keyboard or mouse input and does not use a game window.

No live Kathana trial or long-session result is established. The game may restrict capture or throttle its background rendering; that causes a visible capture pause. Polling and focus switching leave a race in which the first user key can reach the game; the idle wait reduces but cannot eliminate it. The same injected SendInput transport remains, and these changes do not establish enforcement compatibility. The earlier native focus measurements are preserved in [the experiment log](../BACKGROUND_INPUT_EXPERIMENTS.md).
