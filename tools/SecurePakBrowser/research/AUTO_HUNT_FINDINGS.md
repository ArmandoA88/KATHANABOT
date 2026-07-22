# Kathana Auto Hunt research

Research date: 2026-07-21

This note records read-only findings from the matching local game files. It is
intended to guide robust screen-driven bot behavior. It does not describe or
implement bypassing the game's charged Auto Hunt entitlement.

## Matching inputs

- `KathanaGame.exe` 4.0.0.7, Windows x64, SHA-256
  `4FE36C46E4A819862D1062DA7EB3914311FAAF3934C5714B1400359584C37E25`
- `data.pak`, SecurePak v4, SHA-256
  `294F2C7F134C413616D472E578986BCBFDCA2E1C30EBCA5E23809559E43ACA89`
- captured `settings.cfg`, SHA-256
  `32492BF305FCABB69EB39390A1DBD96CFE944F8BF626615E986F855D78EDD333`
- 20,096 archive entries; the full content scan read, decompressed, and CRC
  checked every entry without modifying the archive.

## Archive filename findings

The archive has no obviously named Auto Hunt script, state machine, or
configuration file. It does contain these UI resources:

| Resource | Client resource ID |
| --- | ---: |
| `resource/ui/MAIN_RESOURCE/UI_Charged_AutoHunt_7d.bmp` | `0x5ADD` |
| `resource/ui/MAIN_RESOURCE/UI_Charged_AutoHunt_30d.bmp` | `0x5ADE` |
| `resource/ui/MAINUI/UI_Button_AutoCombat_Default.bmp` | `0x5ADF` |
| `resource/ui/MAINUI/UI_Button_AutoCombat_Hover.bmp` | `0x5AE0` |
| `resource/ui/MAINUI/UI_Button_AutoCombat_Pressed.bmp` | `0x5AE1` |
| `resource/ui/MAINUI/CB_AutoCmombat_D.bmp` | n/a |
| `resource/ui/MAINUI/CB_AutoCmombat_N.bmp` | n/a |
| `resource/ui/MAINUI/CB_AutoCmombat_O.bmp` | n/a |

All eight images are 32 by 32 pixels. The three `CB_AutoCmombat_*` images are
visibly different character-state icons. The sword button also has distinct
default, hover, and pressed images. These exact assets are suitable as
terrain-independent templates for detecting the built-in control's screen
state. A matcher should ignore the transparent/color-key background, compare
only high-information foreground pixels, and require the same state in two
successive frames before acting.

The IDs above were recovered from the fixed records in `system/ClientRes.txl`.
The misspelling `Cmombat` is present in the original archive and must be kept in
searches.

## Full archive content search

An ASCII and UTF-16 content scan for `AutoHunt`, `Auto Hunt`, `AutoCombat`,
`Auto Combat`, `Automatic Hunt`, and `Automatic Combat` matched 13 entries:

- `system/ClientRes.txl`
- `system/ArenaDurga_NPC.txl.en`
- `system/Chaturanga_NPC.txl.en`
- `system/Jina1st_NPC.txl.en`
- `system/Jina3rd_NPC.txl.en`
- `system/Jina4th2_NPC.txl.en`
- `system/Jina4th_NPC.txl.en`
- `system/Jina7thCave_NPC.txl.en`
- `system/Kathana3_NPC.txl.en`
- `system/Mandara_NPC.txl.en`
- `system/Mudha_NPC.txl.en`
- `system/ShambalaAnu_NPC.txl.en`
- `system/Forge_NPC.txl.en`

The English NPC tables describe Automatic Hunt as an item effect that
automatically hunts nearby monsters and whose duration can be extended. These
are localization/dialog records, not executable bot logic.

## Client binary findings

Printable strings in `KathanaGame.exe` prove that the executable contains the
feature logic and exposes the following persisted Auto Hunt field names:

- `AUTOHUNT_%s`
- `AUTOHUNT_%d_%s`
- `atkIdx%d`
- `buffIdx%d`
- `hpPotionIndex`
- `tpPotionIndex`
- `hpThreshPct`
- `tpThreshPct`
- `healSkillIndex`
- `healSkillThreshPct`
- `repairItemIndex`
- `repairIntervalMin`
- `autoLoot`
- `autoSit`
- `maxRangeIdx`
- `autoAssist`
- `skipElites`
- `uiPosX`
- `uiPosY`

The same executable contains `userdata/settings.cfg`, `USERSETTING`, and its
general renderer/input setting names. The captured file below proves that the
`AUTOHUNT_*` strings are character-specific INI sections used by the client's
configuration system.

## Captured settings.cfg

The captured 10,854-byte file has this format:

- bytes 0-3: ASCII `KTCF`;
- bytes 4-7: little-endian version (`1`);
- bytes 8-11: plaintext length (`10,838`);
- bytes 12-15: standard CRC32 of the plaintext (`0x147FAF6D`);
- remaining bytes: XOR-obfuscated UTF-8 INI text.

The client seeds a 32-bit state with `version XOR 0xA5A5A5A5`. Before each
payload byte it updates the state as
`state = state * 0x0019660D + 0x3C6EF35F`, retaining the low 32 bits, then XORs
the byte with `(state >> 16) & 0xFF`. The read-only `SettingsProbe` reproduces
this loader logic and verifies the plaintext CRC. It does not rewrite the
captured file.

Two saved profiles are present. `AUTOHUNT_pollitochicken` has no selected
attacks, buffs, potions, healing skill, or repair item; it has `autoLoot=1` and
`skipElites=1`. `AUTOHUNT_xSAITAMAx` contains the configured profile:

| Purpose | Saved resource ID | Resolved quickslot(s) |
| --- | ---: | --- |
| Attack 0 | `3116` | bar 0/slot 4 and bar 1/slot 6 |
| Attack 1 | `3117` | bar 1/slot 2 |
| Attack 3 | `3162` | bar 1/slot 7 |
| Attack 4 | `3128` | bar 0/slot 3 |
| Buff 0 | `3121` | bar 0/slot 2 |
| Buff 1 | `3102` | bar 0/slot 7 |
| HP potion | `7005` | bar 0/slot 1, inventory 42 |
| TP potion | `7010` | bar 0/slot 8, inventory 31 |

That profile uses HP threshold 70%, TP threshold 20%, auto-loot off, auto-sit
off, range-choice index 0, auto-assist off, and skip-elites off. Its heal-skill
threshold remains saved as 70% even though `healSkillIndex=0`; therefore a
threshold alone does not enable an action.

The format proves six attack selections (`atkIdx0` through `atkIdx5`) and eight
buff selections (`buffIdx0` through `buffIdx7`). Auto Hunt stores skill and item
resource IDs rather than keyboard keys. The quickslot sections independently
store five bars of ten slots. A resource can occupy multiple slots, as ID 3116
does, so an external importer must resolve duplicates deliberately rather than
assuming a one-to-one ID/key mapping. `maxRangeIdx` is proven to be a saved UI
choice index; its actual world distance is not yet proven.

No cooldown values, monster-name list, or individual target IDs are stored in
the Auto Hunt profile. That suggests the native client obtains cooldown state
at runtime and applies general target-selection rules constrained by range and
`skipElites`.

## Concrete design for a better external bot

The verified built-in fields provide a useful feature checklist without
depending on unstable process-memory offsets:

1. Model attacks and buffs as ordered multi-slot lists, not one undifferentiated
   cooldown queue.
2. Keep potion thresholds separate from a heal-skill threshold and add
   hysteresis/debounce to all three.
3. Make acquisition radius explicit, mirroring `maxRangeIdx`, so the bot does
   not repeatedly chase targets outside its selected range.
4. Add deliberate `autoSit`, `autoAssist`, and `skipElites` policies rather than
   hiding those behaviors inside retarget rules.
5. Treat repair as a scheduled maintenance action with an item slot and minute
   interval.
6. Detect the exact 32x32 Auto Combat UI states with the PAK assets, using OCR
   only for target names and numbers. This can prevent repeated clicks and tell
   the bot when a control is unavailable or already active.
7. Use an explicit state machine: `Acquire`, `Engage`, `Recover`, `Loot`,
   `Assist`, `Repair`, and `Paused`. Each transition should require stable visual
   evidence and record its reason in diagnostics.

The next reliable research step is a controlled before/after capture: change
only `maxRangeIdx`, then only `autoAssist`, then one attack selection, saving a
copy after each change. This will map the range choices and confirm whether
attack list order is priority order without patching the executable or guessing
memory addresses.

## Reproduce the archive searches

Build `research/BrowserProbe`, then run:

```powershell
BrowserProbe.exe data.pak --find=autohunt
BrowserProbe.exe data.pak --find=autocombat
BrowserProbe.exe data.pak "--grep=AutoHunt|Auto Hunt|AutoCombat|Auto Combat|Automatic Hunt|Automatic Combat"
BrowserProbe.exe data.pak "--extract=system/ClientRes.txl::ClientRes.txl"
```

`--grep` reads every matched entry through `SecurePakArchive.ReadEntry`, so the
same CRC verification used by the browser is applied during the scan.

Analyze a captured settings file with:

```powershell
dotnet run --project tools/SecurePakBrowser/research/SettingsProbe/SettingsProbe.csproj -c Release -- settings.cfg
```

Add `--dump` to print the complete decrypted INI to the terminal. The default
output prints only the verified header, Auto Hunt profiles, and their resolved
quickslot locations.
