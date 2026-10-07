# Background mode (1.0.250)

Background mode lets the Kathana window stay **behind your other windows** while the bot runs.
It keeps using the same foreground `SendInput` path as 1.0.246–1.0.249. It does not use
`PostMessage`, `SendMessage`, a hook, an injected DLL or any other way into the game process.

## Why it works this way

Windows delivers keyboard input only to the active (foreground) window. A window that is not active cannot
receive hardware-style input, so "background input" in the strict sense is not available without either
(a) synthetic window messages, which every earlier attempt in
[BACKGROUND_INPUT_EXPERIMENTS.md](../BACKGROUND_INPUT_EXPERIMENTS.md) shows the game terminating on, or
(b) code running inside the game process, which that log shows is not reachable and is not attempted here.

So the bot borrows the keyboard instead of keeping the game in front:

1. Just before a key goes out, it remembers the window you were using and where the game sits in the window stack.
2. It activates the game (about 10 ms) and immediately puts the game window back behind your windows
   without deactivating it, so nothing is drawn on top of your work.
3. It sends the key with the same `SendInput` call as before, holds it for the usual 150–250 ms, and releases it.
4. After the last key of a burst (250 ms linger) it gives focus back to your window (about 16 ms) and
   re-applies the game's original position in the window stack.

Keys in a burst share one borrow, so a normal combat rotation takes the keyboard briefly rather than once per key.

## What it does to the game window

Only window-management calls, the same kind the focus supervisor in 1.0.247–1.0.249 already made:
`SetForegroundWindow`/`BringWindowToTop` to take focus, `SetWindowPos` (stack position only, never size or
position on screen) to put the window back behind yours, and read-only queries (stack order, visibility, process
and thread IDs, window class). Taking focus from another application and handing it back both use the existing
sequence: a direct request first, then the calling thread's input is briefly attached to the current foreground
thread (`AttachThreadInput`) so Windows accepts the request. For the hand-back that foreground thread is the
game's UI thread for a few milliseconds. This shares input state only; it does not read, write or inject into the
game process, and it adds no process handle beyond the process-name check 1.0.249 already did. Alternative
hand-back methods that avoid touching the game's thread were not measured; if you would rather the bot never do
this, leave Background mode off.

## What it protects you from

| Situation | Behaviour |
| --- | --- |
| You are typing or clicking | The bot waits until you have been idle for the **wait** (default 1 s) before borrowing. If you start typing during a borrow, it releases its key and hands focus back at once. The very first key you press as a borrow begins can still reach the game, so a longer wait lowers that chance. `0` never waits and gives the bot priority. |
| You click the game, or switch to another window, during a borrow | The bot stands down and does not move focus again. After you switch windows it waits for the **wait** before borrowing again. |
| A full-screen app, game or presentation is in front | Key presses are skipped instead of taking the keyboard (option on by default). |
| The taskbar, Start menu, Alt+Tab or task view is in use, or the screen is locked | Skipped. |
| Windows refuses the focus request | The key is skipped and the bot retries after 750 ms; the window stack is put back as it was. |
| The previous window has closed | The game simply keeps the keyboard. |
| Stop, F12, turning the mode off, closing the app | Held keys are released and focus is handed back immediately. |

## What it does not do

* It is **keyboard-only**. It never moves your mouse or clicks, because the game is behind other windows and a
  click would land on whatever covers it. The keyboard-only policy pauses the features that need the mouse (loot
  scanner and centred pickup, arrow unbundling, party/resurrection macros and accept clicks, support click
  targeting) and every remaining click is skipped (buff self-click, Quiz, RESU). Movement keys, Ctrl/Alt skill
  chords, Loot After Kill (F), Direct KP, healing and buffs on keys keep working. Your saved settings are not
  changed; the pause applies only to the running configuration and lifts when the mode is turned off. The log
  lists what was paused when you turn the mode on.
* Trade whispers still need the game in front (they type chat text) and keep their own focus handling.
* The game must stay **open and not minimized**. A minimized game does not render and is not restored.
* The in-game bot toggle overlay is hidden during a borrow so it cannot flash over your windows.
* It does **not** make input undetectable. The input is the same injected `SendInput` as the foreground build;
  whether Kathana accepts it over long sessions is unverified. No game input was sent while developing this.

## Controls

On Combat Full, under **Stop Bot**:

* **Background mode: ON/OFF** – the saved switch. New and old settings files default to ON.
* **wait (s)** – how long you must be idle before the bot may take the keyboard (0–30 s, default 1).
* **Don't interrupt full-screen apps or presentations** – default on.
* A status line: borrows, waits for you, pauses, refusals and the last reason a key was skipped.

Command line: `--foreground-mode` or `--background-mode` overrides the saved choice for that run.
The local API reports `inputMode` as `foreground-borrow` while it is on.

## Verified here (and what was not)

Windows focus mechanics were measured with throwaway windows (no game, no key input) on this machine,
Windows 11 build 26200, while the system's in-memory foreground-lock timeout was at its maximum:

* 8 of 8 borrow cycles from a background process took focus from another application (about 10 ms) and gave it
  back (about 16 ms) using the same activation sequence the bot already uses.
* Re-stacking the activated window without activating it kept it foreground in 8 of 8 cycles.
* The order of all visible windows was identical before and after in 8 of 8 cycles. The first version anchored on
  the window directly above the game and drifted by one slot, because Windows keeps hidden input-helper windows
  directly above their owner; the anchor is now the nearest *visible* window of another process.

Covered by the automated suites (fake platform, no native focus or input calls): the borrow/linger/hand-back cycle,
burst sharing, yielding and idle thresholds, blockers, refusal back-off, user takeover and click handling, held
keys, mouse/text never borrowing, the re-stack fallback, vanished windows, Stop and mode-off hand-back, a second
game window, watchdog sampling, the user-activity tracker, shell-surface classes, supervisor stand-down and
settings persistence.

**Not verified:** any live Kathana session, game behaviour when it loses and regains focus every few hundred
milliseconds (for example cursor capture or pausing), interaction with every other application you run, and
detection. Try it with a short session first and watch the status line.
