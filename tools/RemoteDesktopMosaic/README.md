# Remote Desktop Mosaic — per-screen input fix, 2026-09-29

Latest portable Windows build: [RemoteDesktopMosaic 1.0.5](../../RemoteDesktopMosaic_20261007_AllScreens.exe)
([SHA-256](../../RemoteDesktopMosaic_20261007_AllScreens.exe.sha256)).
Keep your existing `RemoteDesktopMosaic.settings.json` beside the executable to
reuse saved computers and hotkeys. Without settings, the app asks you to add a
computer. Personal settings, session links, browser profiles, logs, and backups
are excluded from Git.

Version 1.0.4 binds each tile to the exact Chrome page created with its window.
Previously, input was matched by the saved computer URL. During reconnect or
slow navigation that could select an older page, or stop trying before the new
page reached its session URL. The preview and the input connection could then
refer to different windows, leaving only the first computer usable.

Launch now records existing page IDs, pairs the newly opened page with the new
window, and retains that page ID through redirects and session loading. The
window ownership ledger remembers the binding for later attachment. A failed
input connection retries automatically against the same page. It never falls
back to another page merely because that page has the same URL.

The regression suite now opens three simultaneous Chrome windows through the
production launch path with delayed redirects and duplicate URLs. It verifies
each page/window pair using a temporary title in the local fixture, types by
clicking screens 3/2/1/3, checks that other screens are unchanged, reconnects the
third screen, and recovers the second screen's disconnected input channel.

Version 1.0.3 forwards keyboard events over the same ordered local Chrome
connection as mouse input. The previous build still posted keyboard messages to
an unfocused, off-desktop HWND, so typing only worked in Original cursor mode.
Focus emulation keeps the page receptive without exposing or activating Chrome.
Physical key codes, text, modifiers, repeats and key releases now reach the
selected session in both grid and expanded views, including after reconnect.

The keyboard hook tracks modifier transitions itself: suppressed key events are
not reflected reliably in GetAsyncKeyState. Mosaic shortcuts consume both key
transitions, and changing tiles or losing focus releases held remote keys. A held
modifier is restored on the next input to a newly selected tile. Windows-key
shortcuts continue to work locally. Original cursor keeps using normal OS input.

Real Chrome tests exercise click-then-type ordering, text editing, Shift/Caps
Lock, Ctrl+A, Alt+Tab events, arrow keys, Enter, Backspace, repeated game keys,
focus-loss release, selection isolation, and expand/return shortcuts. They run
in grid, expanded and reconnected sessions alongside the existing hover tests.
If another app covers the test window, rendering is verified through a hidden
page capture; the report distinguishes that from a DWM screen-pixel check.

Protocol references:
https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-dispatchKeyEvent
https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setFocusEmulationEnabled

Version 1.0.2 keeps Chrome's session windows outside the entire virtual desktop
and removes their taskbar/Alt+Tab entries. Reconnect starts Chrome hidden/minimized,
detects hidden session windows, and parks the restored rendering surface beyond
the rightmost monitor. A refresh reapplies this only if Chrome changes its window
state or a display change exposes it. Native occlusion tracking is disabled only
in Mosaic's dedicated browser to keep off-desktop previews rendering.

Original cursor explicitly brings the selected window onto Mosaic's monitor;
returning to Mosaic hides it again. Cursor forwarding and hover stabilization are
unchanged. The Chrome regression test checks actual changing preview pixels,
off-desktop bounds and taskbar styles, reconnection through an existing browser
process, and hover after reconnect, in addition to the tests below.

The running HoverDwell build forwarded WM_MOUSEMOVE to Chrome's renderer HWND.
Chrome called TrackMouseEvent, noticed that the physical pointer was over Mosaic
instead of its covered window, and emitted WM_MOUSELEAVE using physical desktop
coordinates. This canceled item hover and could move the remote pointer to a
screen edge. Suppressing subsequent WM_MOUSEMOVE messages could not fix it.

ChromePointer now forwards mouse moves, button transitions and wheel input via
Chrome's local Input.dispatchMouseEvent interface. Only Mosaic's dedicated
Chrome profile enables the loopback-only, automatically assigned endpoint. The
normal Chrome profile is unaffected. The page receives coordinates relative to
its rendering viewport, adjusted for device scale. Input is ordered; consecutive
moves can coalesce without dropping a button release. No SetCursorPos, SendInput,
mouse locking, cursor parking, or hover refresh timer is used.

Small jitter within four Mosaic pixels is suppressed until deliberate movement,
with no 3.2-second expiry. Toolbar transitions preserve the remote hover.
Clicks, dragging, tile changes, and source/layout changes release/reset the hold.
Win32 forwarding remains only for attached non-Chrome windows. A Chrome input
failure is shown in the tile instead of reverting to the broken HWND route.

On the first launch after upgrading, only old session windows with Mosaic's
matching ownership markers are closed gracefully and reopened with the existing
dedicated profile. Computers, nicknames, hotkeys, and sign-in data stay in their
existing locations. An untracked Chrome window selected manually uses Original
cursor until Reconnect selected opens a managed session.

Build and regression check:

```powershell
dotnet build .\RemoteDesktopMosaic.csproj -c Release
.\bin\Release\net9.0-windows10.0.19041.0\RemoteDesktopMosaic.exe --chrome-input-test
```

The test opens a separate local HTML fixture in a disposable Chrome profile. It
reproduces the old mouse-leave event, then checks a seven-second tooltip dwell
under jitter and refresh, immediate deliberate movement, clicks, drag/release,
positive/negative and partial wheel deltas, expanded mapping, and a monitor with
negative coordinates when available. Existing layout, hotkey, native-cursor,
window-lifecycle, and ownership tests also run. Test results are written beside
the test executable. Runtime connection status is stored in
`%LOCALAPPDATA%\RemoteDesktopMosaic\input-status.txt` without session URLs.

Source was recovered from repository commit af28a51 because only build outputs
remained locally. Source and the latest versioned executable are now tracked;
personal runtime data remains ignored. Keep older builds for rollback.

Background on Chrome's HWND behavior:
https://raw.githubusercontent.com/chromium/chromium/main/content/browser/renderer_host/legacy_render_widget_host_win.cc
