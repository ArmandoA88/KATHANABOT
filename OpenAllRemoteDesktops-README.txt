OPEN ALL REMOTE DESKTOPS

Double-click the OpenAllRemoteDesktops desktop shortcut.
If the mosaic is already running, the shortcut brings it forward instead of
opening another set of remote sessions.
On first use, the mosaic itself asks you to paste a Chrome Remote Desktop
computer session link (repeat for as many computers as you want). Use
"Open computer list" (inside that prompt) to find your computers.

After setup, the launcher opens a single Remote Desktop Mosaic with a grid
for your saved computers. It opens Chrome session windows behind the mosaic
and displays their live Windows previews in the tiles.

Every tile has two zones:
- The content area (the big part) is the live remote view - click, drag, and
  type there and it goes straight through to the real remote session, the
  same as if you were looking at that Chrome window directly.
- The thin title strip along the top is mosaic-only: drag it onto another
  tile to reorder them, or right-click it for "Rename tile..." and
  "Remove this computer".

Toolbar buttons: "Maximize selected" (or double-click a tile) expands the
selected tile's Chrome window full screen for PIN entry or remote control;
Esc returns it to the mosaic. "Reconnect selected" and "Reconnect all" open
a fresh session for one or every tile - use these any time a tile looks
frozen or disconnected; the mosaic does not reconnect on its own.
"Choose window..." manually attaches a Chrome window if automatic
attachment ever fails to identify one. "Add computer" adds another saved
computer without restarting.

Keyboard shortcuts (while the mosaic window is focused and no tile is
maximized): Ctrl+1 through Ctrl+9 select a tile by number. Ctrl+F toggles
focus mode, which fills the mosaic with just the selected tile (the others
are hidden, not maximized - no browser chrome, no window switch). Every
other key, including plain 1-9 and F, is forwarded straight into the
selected tile's remote session so normal typing is unaffected.

All session windows are kept unminimized for simultaneous live previews.
Esc restores the expanded source behind the mosaic; it does not minimize it.
If a source is accidentally minimized, the mosaic restores it automatically.
Closing the mosaic leaves your Chrome sessions open.

To use the original separate-window launcher explicitly:
powershell -NoProfile -ExecutionPolicy Bypass -File .\OpenAllRemoteDesktops.ps1 -SeparateWindows

The mosaic uses a dedicated persistent Chrome profile in
%LOCALAPPDATA%\RemoteDesktopMosaic\ChromeProfile, with background-window,
background-timer, and renderer throttling disabled for its browser process.
This lets covered session windows keep rendering simultaneously without changing
or restarting your normal Chrome browser. Sign in to Google once in this profile,
and enter remote computer PINs when prompted. All saved computers must be
connected before the mosaic can show all of their remote screens. The launcher
does not enter PINs automatically or bypass authentication. Manually attaching
a window from normal Chrome does not give that browser the mosaic's
background-rendering flags.

Everything the mosaic remembers - saved computers, their nicknames, and the
window's own position and size - lives in one file next to the exe:
RemoteDesktopMosaic.settings.json. There is nothing else to edit by hand;
use "Add computer", or right-click a tile's title strip to rename or remove
it, from inside the mosaic. Upgrading from an older version that used
separate RemoteDesktopLinks.txt / RemoteDesktopNicknames.json files (and a
window-state.json under %LOCALAPPDATA%) migrates them into the new
settings.json automatically the first time you launch.

Keep OpenAllRemoteDesktops.ps1 and RemoteDesktopMosaic_20260909_NoDuplicates.exe
in this folder so the desktop shortcuts work. RemoteDesktopMosaic.settings.json
will appear alongside them once you have saved at least one computer.
