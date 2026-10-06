# Controlled raw keyboard feasibility probe

This independent probe measures whether `SendInput` F24 events reach a separate background process registered with `RIDEV_INPUTSINK`, while measuring legacy keyboard delivery to an owned foreground sentinel. It does not itself solve background routing: `SendInput` is global, and the foreground application may receive the keys. A successful controlled result does not establish game compatibility or explain the game's suspicious-activity terminations.

The parent creates one hidden native receiver and registers keyboard usage page 1 / usage 6 once. A child copy of this executable creates a visible native sentinel. A current-user named pipe, random session nonce, actual child PID and HWND establish ownership. Each global down/up event requires that exact child HWND to be foreground immediately beforehand. The two methods are virtual-key F24 and mapped scan-code F24, each with a 150 ms hold. Unmapped or unsupported scan codes are recorded as skipped. The report distinguishes `RIM_INPUTSINK` from `RIM_INPUT`, receiver-window/process focus, accepted injection counts, raw zero/nonzero device handles, `RAWKEYBOARD` fields, and foreground legacy counters.

The 2026-10-03 user-launched manual test measured all four injected events in the separate background receiver, with matching injection markers and `RIM_INPUTSINK`. The foreground sentinel also received both down/up pairs. Background raw delivery is established for this fixture; foreground isolation failed. Focus changed after the final release during the last observation period, so the original report says aborted even though both presses completed. No key remained held and cleanup succeeded. See [the preserved report](../../input-diagnostics/raw-keyboard-manual-focus_20261003_145037.json) and [R02 in the experiment log](../../BACKGROUND_INPUT_EXPERIMENTS.md). A repeat of the baseline is unnecessary.

Only raw F24 packets and owned sentinel F24 observations are retained. Queue/message modes also record a bounded set of actual callback marker values, including mismatches, to diagnose failed attribution. Other keys are discarded. The probe reads the initial foreground owner to refuse `--run` when `KathanaGame` is foreground, and refuses an already-held F24; it never looks up a game HWND, sends a game message, loads a hook into the game, installs a driver, or modifies game files/settings. The baseline has no keyboard hook; the explicit filtered modes below add one bounded hook running in this probe. It restores the original HWND only while the owned fixture is still foreground and its captured PID still matches. An owned F24 release is attempted only with the owned sentinel foreground. Focus checking has an unavoidable check-to-send race; aborted runs and pending cleanup are reported explicitly.

Build a self-contained single-file executable from the workspace root:

```powershell
dotnet publish .\tools\RawKeyboardProbe\RawKeyboardProbe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is `tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe`. It does nothing on launch without an explicit mode. Run the controlled sending experiment only when a non-game app is initially active:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --run --report .\input-diagnostics\raw-keyboard-controlled.json
```

If Windows refuses automatic sentinel activation, double-click [Run-ControlledProbe.cmd](Run-ControlledProbe.cmd), then click the labeled test window once within 45 seconds. This mode shows only the owned sentinel on top without requesting activation or using a thread attachment. The exact owned foreground guard still decides when measurement can begin; timing out sends zero keys. Topmost is removed from the owned window on acquisition and cleanup. This is a controlled self-test, not a new bot backend.

The equivalent command is:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --run --wait-for-focus 45 --report .\input-diagnostics\raw-keyboard-manual-focus.json
```

The manual wait accepts 1-60 seconds. Its deadline is separate from the short measurement deadline, which starts only after the owned window actually receives foreground focus. The original automatic mode still aborts within its short bounded deadline when activation is denied.

Reports now distinguish `injectionSequenceCompleted` from `observationCompleted` and record `foregroundChangedDuringFinalObservation`. Once the last key is released, a foreground change no longer aborts the remaining 400 ms observation. Every injection and held-key phase retains the exact foreground guard. The original R02 report is preserved with its original abort status.

## Low-level filtering experiment (R03)

[Run-FilteredProbe.cmd](Run-FilteredProbe.cmd) reproduces the low-level experiment, with a separate report at `input-diagnostics/raw-keyboard-filtered.json`. The user has completed this test; another identical run is unnecessary. This adds `--filter-owned-legacy` to the manual command:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --run --wait-for-focus 45 --filter-owned-legacy --report .\input-diagnostics\raw-keyboard-filtered.json
```

After owned foreground acquisition, this mode installs `WH_KEYBOARD_LL` on the receiver's message-loop thread for the short measurement. It consumes only known key messages with `VK_F24`, `LLKHF_INJECTED`, and the exact per-run injection marker; all unmatched input passes to the next hook without being recorded. Matching already-enqueued events remain filtered if foreground changes, while further injection still requires the owned sentinel. The filter expires after eight seconds and is explicitly removed during cleanup. The report includes installation/removal, consumed down/up counts, callback errors and expired matches. Capture-only mode rejects this option before creating windows or installing hooks.

The question is whether the four background raw packets survive while the foreground legacy count becomes zero. Microsoft documents suppression of later hooks/window delivery, but does not guarantee raw delivery after suppression. SDL's shortcut hook and separate raw handler suggest a reason to measure coexistence; that is an inference, not proof about injected F24. If filtering removes raw packets too, this branch cannot provide the requested keys. If raw packets survive, the result still applies only to this fixture: other registered raw receivers may also observe them, and game compatibility remains untested. Sources: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct), [WM_INPUT](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input), and [SDL 3.4.8 Windows event source](https://github.com/libsdl-org/SDL/blob/release-3.4.8/src/video/windows/SDL_windowsevents.c#L438).

Publish and no-input argument checks passed. The earlier agent-launched attempt expired before focus and installed no hook or input; its [report](../../input-diagnostics/raw-keyboard-filtered-agent_20261003.json) is a preflight failure. The subsequent [user report](../../input-diagnostics/raw-keyboard-filtered_20261004_015726.json) is complete and valid: the hook consumed 2 down/2 up, foreground legacy events were zero, and background raw events were also zero. Hook removal/raw unregistration passed, no key remained held and the child exited normally. Initial foreground restoration was attempted but not confirmed. R03 therefore fails useful background delivery. `filterObservationValid=true` means the experiment has complete evidence; it does not mean its candidate delivers keys.

## Later queue filtering investigation (R04)

R04 uses ordinary `WH_KEYBOARD` inside the owned sentinel's thread, later in the pipeline than R03's `WH_KEYBOARD_LL`. The single bounded trial first passes through two tagged F24 presses and validates callback attribution, then repeats them with only matching F24 consumed. No artificial extra message information is set. `HC_NOREMOVE`, negative hook codes and unmatched keys pass untouched. No DLL is loaded into another app or the game. Even raw retention with suppressed sentinel legacy messages would establish the owned fixture only; it would not automatically suppress keys in Chrome or prove game compatibility. Sources: [KeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/keyboardproc), [SetWindowsHookEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw), [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput), and [GetMessageExtraInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessageextrainfo).

Build/publish, argument guards and a [capture-only smoke check](../../input-diagnostics/raw-keyboard-r04-capture-only.json) passed. The subsequent user run stopped at the attribution gate, as detailed below. [Run-QueueFilteredProbe.cmd](Run-QueueFilteredProbe.cmd) and the following command reproduce R04; another unchanged run is unnecessary:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --run --wait-for-focus 45 --filter-owned-queue --report .\input-diagnostics\raw-keyboard-queue-filtered.json
```

The parent validates actual background raw pairs before requesting suppression. The child validates the pass-through legacy pairs and callback markers before enabling its filter. Invalid attribution aborts before filtered injection. The report separates `queue-baseline-*` and `queue-filtered-*` method counts; baseline messages are expected in aggregate legacy totals. `queueObservationValid` means the paired measurement is complete and attributable. `queueFilteredDeliveryObserved` additionally requires actual background raw pairs in the filtered methods. Zero filtered raw packets would mean this branch fails useful delivery, even if the test completed normally.

The [completed user attempt](../../input-diagnostics/raw-keyboard-queue-filtered_20261004_025505.json) reached the baseline attribution gate and stopped: four callback marker mismatches, normal background raw 4, normal sentinel legacy 2/2, and zero filtered-stage injections. Suppression was never enabled. This is an attribution failure, not evidence that queue filtering removes raw input. Actual full-width callback values were not captured, and the normal handler checked only the low 32 bits; no zero-value or upper-bit cause is proven. Do not repeat R04 unchanged.

## Retrieved-message investigation (R05)

R05 measures the later `WH_GETMESSAGE` callback, which receives the actual retrieved MSG and supports changing it before return to the application. It first observes actual full-width marker values for only the owned sentinel's removed F24 messages, and records actual full-width values in normal dispatch. After exact marker/raw/legacy baseline validation, matching MSG values can be changed to WM_NULL and passed through the hook chain; every other message remains untouched. No queue extra information is manufactured or marker checks relaxed. The completed user repeat retained background raw input while suppressing sentinel legacy input for both methods. This still covers only the owned fixture. References: [GetMsgProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/getmsgproc), [MSG](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msg), and [message queue information](https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues).

The user completed R05; another identical run is unnecessary. To reproduce it, double-click [Run-MessageFilteredProbe.cmd](Run-MessageFilteredProbe.cmd), click the labeled CONTROLLED F24 PROBE window once within 45 seconds, and keep it foreground until it closes after the short measurement. This writes `input-diagnostics/raw-keyboard-message-filtered.json`. The equivalent command is:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --run --wait-for-focus 45 --filter-owned-message --report .\input-diagnostics\raw-keyboard-message-filtered.json
```

The report separates `message-baseline-*` and `message-filtered-*`. `messageObservationValid` requires complete attribution and cleanup; `messageFilteredDeliveryObserved` also requires actual background raw pairs during filtering. A failed baseline aborts before filtering and prints the callback/dispatch marker values; zero packets in an unstarted filtered stage cannot establish a delivery result. Build/publish and argument guards passed. The [capture-only smoke check](../../input-diagnostics/raw-keyboard-r05-capture-only.json) sent zero keys, installed no hook, requested no activation and completed cleanly with normal child exit. This does not establish a working game backend.

The [first user attempt](../../input-diagnostics/raw-keyboard-message-filtered_20261004_041059.json) completed one virtual-key baseline pair with exact full-width callback/dispatch markers, two matching background raw packets and normal legacy down/up 1/1. Focus then left the sentinel before the scan-code baseline or filtering began. Cleanup passed. This partial run is not a completed negative filtering result.

The [completed repeat](../../input-diagnostics/raw-keyboard-message-filtered_20261004_042219.json) sent all four pairs and passed both message outcome flags. Each filtered method produced two matching background raw packets and zero foreground legacy events. All eight callback markers matched in full width; suppressed down/up counts were 2/2 and cleanup passed. The aggregate legacy down/up 2/2 belongs only to the intentional baseline. Focus changed only after the final release during observation, so it did not abort completion. This establishes controlled raw retention with legacy suppression in this sentinel, not filtering in other applications or game acceptance.

For a two-second observation that sends no input and requests no foreground activation:

```powershell
.\tools\RawKeyboardProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe --capture-only --report .\input-diagnostics\raw-keyboard-capture-only.json
```

The measurement deadline is bounded; the child closes automatically. Reports in the workspace's `input-diagnostics` directory are already ignored. This project is intentionally separate from the managed regression suite. A zero device handle or timing match alone is insufficient to attribute a raw event to synthetic input. No source-level guarantee of `SendInput` producing `WM_INPUT` is assumed.

If ordinary sentinel activation has not completed after 500 ms, the controlled fixture may briefly attach its thread input to the identified non-game foreground thread, call `SetForegroundWindow` for the verified owned sentinel only, and detach in `finally`. It then waits at most another 500 ms. Failure or an unknown/game foreground owner aborts before input. This helper belongs only to this diagnostic; it is not a background game input method and sends no fake activation keys.

The child reports visibility before and after its first show request, any repeated show request, and its native window rectangle. A hidden startup instruction can override the first `ShowWindow` call; a still-hidden owned sentinel receives one repeated explicit show request. The parent verifies actual visibility before acquisition or input. This makes a hidden-fixture failure distinguishable from foreground activation denial.

Native API references: [RegisterRawInputDevices](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices), [WM_INPUT](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input), [RAWKEYBOARD](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard), and [RAWINPUTHEADER](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputheader).
