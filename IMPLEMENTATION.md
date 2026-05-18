# Implementation Notes

## Release EXE Versioning

All standalone EXE outputs must be serialized into separate versions.

- Do not overwrite an existing EXE build.
- Save every release/test EXE with a unique version suffix, preferably `yyyyMMdd_HHmmss`.
- Use `dist\versions\KathanaBotControlPanel_yyyyMMdd_HHmmss.exe` for final copied EXE files.
- Keep at least the last known working EXE so a bad build can be rolled back quickly.
- If a temporary publish folder is needed, use `dist\versions\<version>\` and copy the final EXE beside it with the same version string.

Recommended publish pattern:

```powershell
$version = Get-Date -Format "yyyyMMdd_HHmmss"
$publishDir = ".\dist\versions\$version"
dotnet publish .\ui\KathanaBotControlPanel\KathanaBotControlPanel.vbproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o $publishDir
Copy-Item "$publishDir\KathanaBotControlPanel.exe" ".\dist\versions\KathanaBotControlPanel_$version.exe"
```

This rule exists so new EXEs never destroy older working builds.

## Discord Screenshot Command

The `shot` command is implemented as a lightweight Discord REST poller, not as a webhook listener.

- Webhooks only send messages; they cannot read a channel.
- The app requires a Discord bot token plus the data channel ID to read messages.
- The app watches the configured data channel for new user messages whose content is exactly `shot`.
- When `shot` is detected, the app uploads the newest rolling screenshot from `%AppData%\KathanaBotControlPanel\screenshots` to the configured Discord Stats/Data webhook.
- The bot needs `View Channel`, `Read Message History`, and Message Content Intent enabled.
- The Stats webhook should point at the same data channel if the response image should appear where `shot` was typed.
