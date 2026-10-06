# Internal input hook — control panel 1.0.224

**Current release: 1.0.231 restores background keyboard input.** Keep Kathana
open and not minimized while another app stays active. The Background keys button
selects queued or synchronous delivery with normal or zero scan codes; queued
normal scan codes are the default. Each owned key retains its original release
format and method. Mouse/text output and automatic relaunch are disabled for this
keyboard build, with runtime restrictions kept out of saved profile snapshots.
It preserves 150-250 ms randomized short holds and exact longer holds. No DLL
loading, focus change or system cursor movement is required. The prior v1.0.230
foreground build is retained but did not satisfy the background requirement.

The packaged v1.0.231 EXE passed live inventory checks with all four methods,
then opened and closed inventory through two isolated Lite-engine runs using
the default. Another application retained foreground ownership within every
trial. All 13 managed test programs passed, including 2,401 background
assertions. Sustained combat and enforcement compatibility remain unverified;
see the [current experiment evidence](../BACKGROUND_INPUT_EXPERIMENTS.md).

Subsequent user use of v1.0.231 was terminated for suspicious activity, with
posted scan codes selected in the running panel. S03 records that continued-use
failure; the short inventory checks do not establish a usable detection solution.

Earlier version 1.0.229 independently randomizes short attack/role taps, Ctrl/Alt skill
shortcuts and manual API keys from 150 to 250 ms. Requested durations above 150 ms
set the lower bound; configured holds of 250 ms or longer stay exact. Cancellation
still releases immediately, and explicit left/right modifiers are preserved.
This timing change has not been tested against the live game or its enforcement.
The following live evidence is from v1.0.228.
All 13 managed test programs passed; the final expanded SDL suite passed 676
assertions. The packaged EXE opened and closed inventory through an isolated
real engine against the restarted game (PID 30728/HWND 26942334), with foreground
HWND 1706252 unchanged. One `1` skill press was acknowledged but showed no visible
cast; live skill acceptance remains unconfirmed. Details and capture links are
in the [experiment log](../BACKGROUND_INPUT_EXPERIMENTS.md).

## SDL background input, 2026-10-02

A bounded printable-string scan of the installed `KathanaGame.exe` identified
SDL3 input handling and the exact `SDL_WINDOWS_RAW_KEYBOARD_INPUTSINK` hint.
SDL's [Windows input source](https://raw.githubusercontent.com/libsdl-org/SDL/main/src/video/windows/SDL_windowsevents.c)
marks zero-scan-code key messages as virtual keys and accepts them even while
raw keyboard input is enabled. Its [keyboard state source](https://raw.githubusercontent.com/libsdl-org/SDL/main/src/events/SDL_keyboard.c)
updates state and can enqueue an event without a keyboard-focused SDL window.
Those source observations suggested a test; they alone did not prove how the
installed game would behave.

Live tests against PID 26168 / HWND 2297302 established that:

- Posted I down/up with `lParam=1` / `0xC0000001` opened the inventory.
- Synchronous `SendMessageTimeout` using the same virtual-key pair closed it.
- Foreground HWND remained 399112 throughout each test and was never the game.
- Before/after game captures visibly confirmed both inventory state changes.

The reports were `background-post-zero_457784343` and
`background-send-zero_457833859`. The production backend now sends all key
requests through this zero-scan-code path. Normal scan-code fields supplied by
legacy bot callers are deliberately stripped at the message boundary.

A later controlled comparison also accepted ordinary scan-code I messages:
`background-send-scan_460909093` opened inventory and
`background-post-scan_460944078` closed it, with foreground HWND 399112 unchanged
and both capture pairs inspected. Zero scan code is therefore a verified path,
but is not established as necessary in the client's current state. SDL also
allows ordinary key messages when text input is active; these results do not
identify the runtime raw-keyboard flag. The installed binary contains the version
marker `SDL-3.4.8-release-3.4.8`; ongoing source checks use that release where possible.
The running [experiment log](../BACKGROUND_INPUT_EXPERIMENTS.md) records each new
test and its evidence.

`SdlBackgroundWindowsInput` binds HWND and PID together, validates the confirmed
Steam game path with query-only access, and rejects closed, reused or minimized
targets. Synchronous calls time out after one second. Owned releases retain the
original HWND/PID across retargeting; failed releases are retried by cleanup.
Mouse requests use a virtual cursor in client coordinates and send messages
only to the target game. Text uses `WM_CHAR`. The production focus, click and
clipboard workflows recognize this backend so they do not activate the game,
move the physical cursor or modify the clipboard.

The legacy `BackgroundOnly` profile flag remains a compatibility no-op; this
release's default backend itself supplies background input. It needs the game
window open and not minimized. The keyboard tests prove inventory input;
they do not establish acceptance for every modifier, text or combat workflow.

Production validation also completed:

- The default backend opened inventory with foreground HWND 399112 unchanged
  (`background-backend-inventory_458850875`).
- A fresh, isolated Lite engine completed one I buff action using normal engine
  key timing, stopped, and visibly closed inventory while foreground HWND
  399112 remained unchanged (`background-engine-inventory_459749296`).
- The packaged root v1.0.227 EXE repeated that real-engine test and visibly
  opened inventory (`background-engine-inventory_460142187`).
- Mouse tests acknowledged synchronous and posted messages, but inventory/HUD
  controls and the Options OK button showed no expected state change. Reports
  included `background-backend-mouse_458896328`, `458946953`,
  `background-post-mouse_459440734` and packaged `460008359`. Background mouse
  acceptance is unresolved. `WM_CHAR` delivery has managed coverage, but live
  chat/text acceptance was not tested and no chat messages were submitted.
- All 13 managed test programs passed after correcting the retained foreground
  backend's audit to inspect its own source instead of forbidding the new
  intentional diagnostic message imports across the entire application.
  Native controlled loader/input tests also passed. Package audit metadata
  produced NU1900 warnings while nuget.org was unreachable; compilation and
  functional tests completed using cached dependencies.

The isolated engine profile is created in memory; it loads/saves no user profile
and enables only an I buff action with a 60-second cooldown. It disables retarget,
party acceptance and other gameplay workflows, runs for at most three seconds,
checks unchanged foreground ownership and always stops in `Finally`.

The root artifact is `KathanaBotControlPanel_Standalone_1.0.227.exe`, 70,493,544
bytes, file version 1.0.227.0. Its SHA-256 matched the published executable:
`3F3C3E7FE0331AB05EB04927C85A744920EAABB4A7C5860F00B5000B650A9A6B`.
The visible standalone panel PID 36952 selected the live game and reported
`running=false`, `inputMode=sdl-background`, and no backend error. The final game
capture confirmed the test panels closed; game and panel remained responsive.

To reproduce the production inventory check with another window active:

```powershell
.\KathanaBotControlPanel_Standalone_1.0.227.exe --input-diagnostics background-backend-inventory
.\KathanaBotControlPanel_Standalone_1.0.227.exe --input-diagnostics background-engine-inventory
```

The diagnostic aborts if the foreground window changes or is the game. It saves
before/after captures and distinguishes backend acknowledgement from visible
game acceptance. Run it twice to open and then close inventory.

SDL also [documents the INPUTSINK hint](https://wiki.libsdl.org/SDL3/SDL_HINT_WINDOWS_RAW_KEYBOARD_INPUTSINK)
for background raw keyboard delivery. It is an additional lead, not the method
used by this release: setting an environment variable in the bot cannot change
an already-running game's environment. No game restart, configuration edit or
security-policy change was performed for these tests.

## Additional live loader checks, 2026-10-02

The user requires the existing game window on the same Windows desktop to
receive input while another window is active. Foreground SendInput does not
meet that requirement.

An experimental `native/InputHook/HookLoadProbe.cpp` validates the Steam
installation, native x64 architecture, actual granted process rights, and
in-process callback execution. Its bridge heartbeat stays zero, so the native
probes do not enqueue gameplay commands. The full DLL pins itself before patching
imports so removing a temporary Windows hook cannot invalidate installed pointers.
`HookMarker.cpp` is a separate minimal DLL with no CRT, worker thread, import
patches or input handling. It distinguishes DLL callback delivery from bridge
initialization failures.

The extended live checks produced these results:

| Loader/check | Controlled child | Live Kathana client |
| --- | --- | --- |
| `WH_GETMESSAGE`, `WH_CALLWNDPROC`, `WH_CALLWNDPROCRET` | In-process callback and expected handshake | Hook registration accepted; no in-process callback or handshake |
| Normal `LoadLibraryEx` initialization, minimal marker DLL | In-process marker handshake | No in-process callback or handshake |
| `WH_CBT`, with real window creation in the child and foreground inventory activity in the game | In-process marker handshake | No in-process callback or handshake |
| `SetWinEventHook(WINEVENT_INCONTEXT)`, console and GUI loaders | In-process callback and expected handshake | Game event observed, but DLL callback executed in the loader process; no game handshake |
| `WH_DEBUG` paired with `WH_CALLWNDPROC`, distinct debug counter | In-process marker readiness and four distinct DEBUG callbacks in the focused test | Hook registrations accepted; readiness and distinct DEBUG counter both zero |
| Process access requested for the remote loader (`0x43A`) | Granted `0x143A`; required rights present | Granted `0x1000`; required rights absent |

`NtQueryObject(ObjectBasicInformation)` reports the actual handle rights. In the
live game, both requests for `0x410` (query/read) and `0x43A` (query/read/write/
allocation/thread creation) returned valid handles with only `0x1000`,
`PROCESS_QUERY_LIMITED_INFORMATION`. A successful `OpenProcess` therefore does
not establish permission to load this DLL. The managed remote loader now checks
the granted mask before module inspection, allocation or thread creation and
reports the requested and granted masks. Its focused tests exercise both a
sufficient handle and a deliberately query-only handle against a controlled
process. The managed live rights check reproduced the native result.

The WinEvent frame notification came from game UI thread 47676 in PID 26168,
but the supposed in-context callback ran in the caller, including when using
the GUI-subsystem loader. Receiving that event is not an internal connection.
The minimal marker avoids interpreting a failed bridge worker as a failed hook
callback. Marker readiness is `2` for diagnostics only; it is never an input
bridge. Full bridge readiness is `1`.

Caller and game both had medium integrity, no elevation, no AppContainer or
UIAccess, and the same Default desktop. Queried signature-only, extension-point
and strict CFG restrictions were disabled. These observations do not establish
which component removed the process rights or prevented in-process callbacks.
They do establish why the tested remote loader cannot proceed with its current
handle.

An additional token check found `TokenHasRestrictions=1` and zero restricted
SIDs in both caller and live game, with no query errors. Marker telemetry still
depends on shared-mapping/window-property access; a zero value is absence of
observed execution, not proof of which component prevented execution. The CBT
probe now records actual game foreground transitions. A drawn inventory panel
and an I key alone do not prove a native CBT event; this limits the earlier live
CBT conclusion.

**These DLL loaders delivered no verified background gameplay input.** The
experimental loader is not included in the v1.0.226 standalone executable, and
that historical executable remains foreground-only. The bot was confirmed
stopped after the checks. Its existing API closed the inventory and Options
panels used during testing; a subsequent capture confirmed both were closed and
the game remained responsive.

To reproduce the isolated tests:

```powershell
.\native\InputHook\build.ps1 -Test
```

To reproduce a connection-only live probe from `native/InputHook/out`, replace
the PID with the current game PID:

```powershell
.\HookLoadProbe.exe 26168 get
.\HookLoadProbe.exe 26168 call
.\HookLoadProbe.exe 26168 inspect
.\HookLoadProbe.exe 26168 event marker normal frame
.\HookLoadProbeGui.exe 26168 event marker normal frame
.\HookLoadProbe.exe 26168 cbt marker normal
.\HookLoadProbe.exe 26168 debug marker normal
```

The CBT live probe waits for window activity for up to 25 seconds; registration
alone is not a passing result. The frame option requests a frame refresh without
moving, resizing or activating the game. All hooks are removed on probe exit.
The self-tests create and terminate only their own controlled child process.

To reproduce managed validation and the read-only live rights check:

```powershell
dotnet run --project tests/InternalHookInput.Tests -c Release
dotnet run --project tests/InternalHookInput.Tests -c Release --no-build -- --probe-game-rights
```

The live rights check exits with code 2 when the requested loader rights are
missing. This is an expected diagnostic failure with the observed client.

See Microsoft's [SetWindowsHookEx documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw),
[SetWinEventHook documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
and [NtQueryObject documentation](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryobject)
for the loader and granted-access contracts.

## Historical v1.0.224 implementation

The v1.0.224 `WindowsInput` backend routes keyboard, mouse, cursor and Unicode
text commands to an x64 DLL inside the selected game process. There is no
SendInput or PostMessage fallback. The old `ForegroundWindowsInput` class remains
for its regression tests but is not selected by production code. Kathana Bot
Reloaded is separate and was not changed.

## Target and implementation

The confirmed target is
`C:\Program Files (x86)\Steam\steamapps\common\Kathana\KathanaGame.exe`.
The installed executable is PE32+ x64 and imports `GetRawInputBuffer`, keyboard
state, cursor APIs and `PeekMessageA/W`. Its static imports do not include
`GetRawInputData` or DirectInput. This release adapts the requested internal Raw
Input approach to those imported APIs.

The bot verifies the executable path and architecture, extracts its embedded DLL
to a directory named with its SHA-256 hash under
`%LOCALAPPDATA%\KathanaBot\InputHook`, and loads it into the selected process.
The remote loader address is resolved using the corresponding remote module.

The DLL patches the **main executable's import address table**. These are
process-local import hooks, not global entry-point patches or DirectInput vtable
hooks. Each replacement calls the original API first, then adds queued RAWINPUT
records, overlays bot-owned keyboard state, returns the virtual cursor, or
supplies input and UTF-16 `WM_CHAR` results through `PeekMessage`. No synthetic
messages are posted to HWNDs. Physical input remains available when the game is
actually foreground. Background keyboard-state polling does not borrow the
user's foreground keys. Focus queries and activation messages are virtualized
inside the game's main input thread while the bridge is active.

Bot workflows check target validity instead of foreground ownership. Verified
clicks acknowledge process-local cursor updates instead of reading the Windows
cursor. Clipboard-paste requests use Unicode text commands. The game must remain
non-minimized because capture and rendering may stop while minimized.

## Lifetime and failures

A bounded shared-memory queue preserves command order. The DLL acknowledges
acceptance; this is **not proof that gameplay processed a command**. The bot
checks HWND/PID validity and maintains a heartbeat every 20 ms. Engine stop and
application exit stop the target heartbeat. Losing the heartbeat releases virtual
keys/buttons and discards stale queued work; bot crashes expire within two
seconds. Keyboard releases for keys the bridge does not own are ignored.

Only one controller may own a game's bridge. Rejecting another controller does
not clear the first one's heartbeat. The DLL remains resident until game exit;
inactive import hooks forward to the originals while cleanup releases can drain.
Restart the game before switching to a different hook build.

Process access, loading, coverage and acknowledgement errors appear as
`Internal input hook failed` runtime-journal events. Input is skipped. If the game
is elevated, run the bot as administrator too. Process protection is not bypassed
and game files are not modified.

## Build and validation

Install .NET 9 and Visual Studio C++ x64 build tools, then run:

```powershell
.\build-standalone-release.ps1
```

The script builds/tests the DLL, runs the bot regression programs, publishes a
self-contained win-x64 single-file application and verifies its root-folder EXE
copy. Ordinary builds also rebuild the DLL when its source changes. The EXE
contains the DLL; no companion file needs to be distributed.

The isolated native test program exercises actual import patches: keyboard
state, Raw Input size/alignment and ordering, mouse buttons, Unicode, message
filters, virtual cursor isolation, heartbeat cleanup and reconnect. Managed tests
check command routing, coordinates, failures and selection of the hook backend.
All 12 bot regression programs passed during implementation.

## Live connection result and 1.0.225 diagnostics

A subsequent live test failed to connect to the current client. Module snapshot
and remote-memory allocation probes returned Windows error 5, `Access denied`.
The game was not elevated; the running bot was elevated. A controlled inventory
key request through that bot also failed, and no hook handshake mapping existed.
The bot was left stopped. No live gameplay input was delivered by the bridge.

Version 1.0.225 now requires a successful connection before an engine starts and
reports Windows error codes and descriptions. Local API status reports
`inputMode: internal-hook` and `inputError`. This improves diagnosis; it does not
remove the live client's access restriction. The DLL injection method is not
working with the current client under the observed conditions.

Static imports identify available APIs, not every runtime input path. Reads in
other modules, dynamically resolved APIs or a client that stops its input loop in
the background may also require additional work.
