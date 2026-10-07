# Extras

Open **Home** and type **126974** to reveal EXTRAS with the other hidden tabs.
It follows the same per-version remembered unlock. The presets and bundled
Mosaic executable are embedded in the standalone EXE.

## Change zoom

1. Stop bot automation and close all Kathana windows.
2. Open **EXTRAS** and select the installation folder containing
   `KathanaGame.exe`. Browse works for other drives and Steam library locations.
3. Select **x2 zoom** or **x3 POV / zoom**, then click **Apply selected zoom**.
4. Start Kathana again to load the chosen preset.

The target is always `userdata/engine.cfg` relative to the chosen installation,
not a fixed drive or machine path. The original is renamed in that same userdata
folder as `engine.cfg.<UTC timestamp>.<unique ID>.backup`. Existing backups are
never overwritten. Failed installation attempts recover the original where
possible and report the preserved backup if recovery cannot finish. Linked or
redirected target paths are refused to keep writes inside the selected folder.

**Restore previous engine.cfg** restores the most recent backup and preserves
the currently installed config in another unique backup. All older backups stay
available. If Windows denies writes to Program Files, run the app with permission
to write that installation; the app does not change permissions or stop games.

The exact supplied files are embedded unchanged:

| Preset | Supplied file | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| x2 | enginex2.cfg | 1,503 | `1DC74003524758C59A3311EB8BEDF8C08D5A64A4342CA8FDC487B5E4BEE20B46` |
| x3 | engine.cfg | 1,518 | `D27C09A7539D565F0990B5BC0F36798587CECC81AF3B30AB9ECEAD66E63ED562` |

## Remote Desktop Mosaic

**Save bundled Mosaic (1.0.4)** works offline. **Download latest Mosaic** reads
the newest dated Mosaic executable and its matching checksum from
`ArmandoA88/KATHANABOT` on `agent-ai`. Both files are read from the same pinned
revision. The download checks its expected size, executable header and SHA-256
before installation; cancellation or failed checks keep the current copy.
Downloads have a ten-minute deadline, including response-body reads, so a stalled
transfer cannot leave the controls busy indefinitely.

The destination is `Extras/RemoteDesktopMosaic.exe` next to the running standalone
app. A replaced executable is kept as `RemoteDesktopMosaic.exe.previous`.
**Open Mosaic** launches it only on an explicit click and passes an existing
`RemoteDesktopMosaic.settings.json` beside KathanaBot when present. Otherwise,
Mosaic uses its normal first-run setup and settings in its own Extras folder.

Keep the app's folder writable and move the whole folder when moving its saved
Mosaic files. No downloads or game config changes happen merely by opening the
app or unlocking EXTRAS.

## Verification

`tests/Extras.Tests` checks exact embedded resources, backup/restore and recovery,
game-closed and path checks, mock GitHub downloads, checksum/size failures,
cancellation, saved-file preservation, password-gated visibility and owned UI
layout. Tests use temporary installations and mock network responses; they do
not replace the live game's config or launch Mosaic.
