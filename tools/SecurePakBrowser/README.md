# HTRD KAT MOD Browser

This is a browser and safe editor for the SecurePak v4 archive loaded by the
matching `KathanaGame.exe`. It reconstructs the original folder tree and
filenames, searches and previews files, replaces arbitrary entries, edits Unicode
text, and builds a new loader-compatible archive.

## Use

Run `HTRD KAT MOD Browser.exe` from the repository root. When `data.pak` is
beside it, the browser opens that archive automatically. You can also use **Open
PAK**, drag a `.pak` file onto the window, or pass a path on the command line.

Double-click a file to preview it. Use **Extract selected** or **Extract all**
to write copies to a destination you choose. Every original entry read verifies
its CRC32. Archive paths are checked before extraction so an entry cannot escape
the selected destination folder.

Search is global whenever the Find box contains text. Plain words match complete
archive paths; use `name:forge`, `folder:resource/map`, or `ext:tcc` to target a
field. Multiple terms must all match, quoted phrases keep spaces together, and a
leading minus excludes matches (for example `ext:tcc -folder:test`). Press
`Ctrl+F` to focus search, Enter to select the first result, and Escape to clear it.

## Modify files

1. Select one entry and choose **Replace file** to import any replacement, or
   **Edit text** to use the built-in UTF-8/UTF-16/UTF-32 editor.
2. Modified entries are bold, orange, and marked `Modified`. Preview and extract
   use the pending version.
3. Use **Revert selected** to discard selected pending replacements.
4. Choose **Save modified PAK**. The editor writes a new `.pak`, authenticates
   its header, encrypts its filename index, updates sizes and CRC32 values, and
   reopens the result.

The currently open source archive cannot be overwritten directly. Save under a
new name such as `data.modified.pak`, test it, and keep the original as a backup.
To use the result with the game, close both programs and deliberately rename or
copy the tested archive to the filename expected by the game.

## Built-in TANTRA_MAP (`.tcc`) editor

Select a `.tcc` entry and choose **Edit TCC map**, or double-click it. The editor
validates the `TANTRA_MAP` header, dimensions, cell count, file size, and every
stored X/Y coordinate before enabling editing.

The map window provides:

- a visual grid with **Flags** and **Map value** views;
- 1×, 2×, 4×, 8×, and 16× nearest-neighbor zoom;
- direct coordinate navigation and a live cell inspector;
- editable 16-bit map value and hexadecimal flags;
- right-click sampling from an existing cell;
- drag painting with configurable brush size;
- per-stroke undo/redo, including `Ctrl+Z` and `Ctrl+Y`;
- color categories for the observed flags `0x0000`, `0x0010`, `0x4000`, and
  `0x4010`.

Choose **Apply map changes** to return the edited binary to the archive workspace.
This does not touch the open source archive; finish with **Save modified PAK**.
The flag categories are deliberately not given gameplay names because their exact
behavior has not yet been proven. Sample known working cells before painting.

## Matching files and verification

The implementation was derived from these local files:

- `KathanaGame.exe` version 4.0.0.7, SHA-256
  `4FE36C46E4A819862D1062DA7EB3914311FAAF3934C5714B1400359584C37E25`
- `data.pak`, SHA-256
  `294F2C7F134C413616D472E578986BCBFDCA2E1C30EBCA5E23809559E43ACA89`

The matching archive contains 20,096 entries. A full validation pass decoded
every original path, decompressed every compressed entry, and matched every
stored CRC32. A second full integration pass modified an entry, rebuilt the
archive, authenticated and reopened it, recovered every original path, and
validated all 20,096 rebuilt entries.

The TCC-specific integration test also changed a cell in
`MAP_Jina8thCave.tcc`, serialized the fixed-size grid, rebuilt the SecurePak,
reopened the edited TCC, confirmed the changed value/flags, and CRC-validated all
20,096 entries in the resulting archive.

## Build

From the repository root:

```powershell
.\build-securepak-browser.ps1
```

The script publishes a self-contained, single-file Windows x64 application and
copies `HTRD KAT MOD Browser.exe` to the repository root. The source targets
.NET 9 and uses Monocypher for the loader-compatible cryptography and
K4os.Compression.LZ4 for raw LZ4 blocks.

## Recovered SecurePak v4 layout

- The 108-byte authenticated header is XChaCha20-Poly1305 encrypted.
- The final 32 archive bytes are the Argon2id salt.
- The decrypted header identifies `PAK!`, version 4, data/index offsets, counts,
  compression type, and sizes.
- The `IDX2` index stores encrypted UTF-8 paths plus hash, relative offset,
  packed/original sizes, flags, and CRC32 for each entry.
- Entry payloads begin with an eight-byte block header. Flagged payloads are raw
  LZ4 blocks; unflagged payloads are copied directly.

The source archive is always opened with `FileAccess.Read` and `FileShare.Read`.
Saving streams a rebuilt archive through a uniquely named temporary file and
only moves the completed result to the destination after all data, index, salt,
and authenticated header bytes have been written successfully.
