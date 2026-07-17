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

## Velopack Internet Updates

Validation: the Release build succeeded with zero errors and zero warnings.

To activate online updates, push these changes to `agent-ai` and publish version `1.0.43` using the `Build and publish Velopack release` GitHub workflow. Later, increase the application version and publish `1.0.44`; the standalone `1.0.43` EXE will detect it from the `Update` tab and can replace itself automatically.

To activate and test internet updates:

1. Commit and push the update implementation to the `agent-ai` branch.
2. In GitHub Actions, run `Build and publish Velopack release` with version `1.0.43`. The workflow explicitly checks out and targets `agent-ai`.
3. Run either `KathanaBot-Setup-v1.0.43-<timestamp>.exe` or the standalone `1.0.43` EXE. The standalone updater requires no installation or permanent companion files; it downloads and verifies a temporary replacement automatically.
4. Bump the application version to `1.0.44`, commit and push it to `agent-ai`, and run the release workflow again with version `1.0.44`.
5. Start the installed `1.0.43` application or press `Check Now` in its `Update` tab. Confirm that it reports version `1.0.44`.
6. Press `Update and Restart`. Confirm the download progress reaches completion, SHA-256 verification succeeds, an active bot stops safely, the EXE is replaced or the installed copy is updated, and KathanaBot relaunches as version `1.0.44`.

Each GitHub Release must contain `KathanaBotControlPanel-win-x64-standalone.exe` and `KathanaBotControlPanel-win-x64-standalone.exe.sha256`. The release script and GitHub workflow create and upload both assets automatically from `agent-ai`.

The workflow uses GitHub's built-in `GITHUB_TOKEN` and follows Velopack's official [GitHub Actions distribution flow](https://docs.velopack.io/distributing/github-actions).

## Discord Screenshot Command

The `shot` command is implemented as a lightweight Discord REST poller, not as a webhook listener.

- Webhooks only send messages; they cannot read a channel.
- The app requires a Discord bot token plus the data channel ID to read messages.
- The app watches the configured data channel for new user messages whose content is exactly `shot`.
- When `shot` is detected, the app uploads the newest rolling screenshot from `%AppData%\KathanaBotControlPanel\screenshots` to the configured Discord Stats/Data webhook.
- The bot needs `View Channel`, `Read Message History`, and Message Content Intent enabled.
- The Stats webhook should point at the same data channel if the response image should appear where `shot` was typed.
