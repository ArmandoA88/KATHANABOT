# Controlled background game inventory probe

This independent tester sends **one scan-code I down/up pair**, held about 150 ms, while its own form is foreground and the existing Kathana game is background. It tests game reception after a raw-keyboard environment launch. It does not establish a new bot backend, continued-use compatibility, or suppression in other foreground applications.

Treat this as a quiescent reception test. A game with raw INPUTSINK enabled may also receive physical typing from other applications; routing is not exclusive to the injected key. Close this test game normally after the probe before typing elsewhere. The requested variables remain in that game until it exits; normal Steam relaunch returns to its ordinary environment. No Steam settings or persistent environment variables are changed by this tester.

Close the bot and game normally, leaving Steam open. Use [Start-KathanaRawKeyboard.cmd](../Start-KathanaRawKeyboard.cmd) to start the game with the requested SDL raw-keyboard settings, and log in. Leave that game unminimized in the background. The tester requires the launcher's latest `input-diagnostics/kathana-raw-keyboard-launch.json` receipt. It refuses stale/replaced/duplicate processes, an unknown or different installation, missing receipt identity, mismatched PID/start time/session/path/image SHA256, and unconfirmed environment propagation. Requested SDL settings are recorded; their effective values inside the game remain unverified.

Double-click [Run-GameInventoryProbe.cmd](Run-GameInventoryProbe.cmd), then click the labeled **CONTROLLED INVENTORY PROBE** window once within 45 seconds. Release I and Shift/Ctrl/Alt/Windows keys beforehand, and keep the test window active until it closes automatically. The form appears on top without activating itself, and removes its topmost status after the manual click. It never activates or restores another application.

The validated scan code is `0x17`, checked using `MapVirtualKey(MAPVK_VK_TO_VSC_EX)`. No inventory baseline, second press, or F24 input is sent. Exact owned HWND/PID/thread foreground guards apply immediately before every `SendInput` and throughout the hold. Game identity, duplicate processes, visibility and minimization are rechecked. If focus changes, further presses stop; a held I is released only while the exact owned form is foreground. Otherwise the report explicitly marks a pending release.

The only hook is a bounded eight-second `WH_GETMESSAGE` callback on the tester's own GUI thread, with no DLL or hook in the game or another application. A harmless posted WM_NULL to this owned form establishes callback PID/thread readiness before input. Only removed I messages for this exact form with the full unique 64-bit injection marker are rewritten to WM_NULL/0/0. Other keys, unmatched markers and non-removal observations pass untouched. The report retains actual marker samples, accepted/consumed counts, removal errors and cleanup state. The global input stream can still be visible through other raw-input or keyboard-state APIs; this fixture does not demonstrate exclusive routing.

Before/after full game-window PNGs use read-only `PrintWindow(PW_RENDERFULLCONTENT)`, without game keyboard/mouse messages. Captures run on background workers with a 650 ms wait limit, outside the held-key interval. Before-capture timeout aborts before input. Failed, black, pending or timed-out captures are marked inconclusive. The form remains responsive while capturing, and workers own their bitmap/DC until completion.

JSON and PNG filenames include a UTC timestamp and random suffix under ignored `input-diagnostics`. `scope.gameAcceptance` always remains `unverified`: compare the images and game inventory visually. Accepted SendInput or changed pixels alone do not establish an inventory toggle.

Build/publish from the workspace root (no external packages):

```powershell
dotnet publish .\tools\GameInventoryProbe\GameInventoryProbe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is `tools\GameInventoryProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\GameInventoryProbe.exe`.

Build/publish, independent source review, argument guards and a missing-receipt preflight passed. The latter created zero windows/hooks and sent zero input. No live game trial was run by the agent. The prepared executable is 138,433,964 bytes, SHA-256 `69520385E0557282001AE488D5FDAFACA3399BCE66B45B01316C11C51FBC6325`; a preserved copy is `GameInventoryProbe_20261004_045358.exe` beside it. Game acceptance remains pending.

No arguments and `--help` create no windows and send no input. Invalid arguments are rejected before preflight or fixture creation. Explicit execution is:

```powershell
.\tools\GameInventoryProbe\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\GameInventoryProbe.exe --run --launch-report .\input-diagnostics\kathana-raw-keyboard-launch.json --report-dir .\input-diagnostics
```

Optional `--game-pid PID` must select the sole validated running game. `--report PREFIX` sets a filename prefix; a unique timestamp suffix is always added. The launcher pauses afterwards to leave the report path visible.

Native semantics: [GetMsgProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/getmsgproc), [SetWindowsHookEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw), [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput), and [PrintWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow). The source makes no guarantee that SendInput generates game raw input or that requested SDL settings became effective.
