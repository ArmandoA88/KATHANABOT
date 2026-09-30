# Gameplay observation recorder

Open Kathana, enter the game, stop bot automation, then double-click **Record-Kathana-Session.cmd** in the repository root. Switch back to the game during the 10-second countdown. Spoken prompts announce five phases, each lasting approximately 30 seconds:

1. Idle: do nothing.
2. Inventory: open and close inventory.
3. Movement: move normally.
4. Skill: use one skill a few times.
5. Recovery: remain idle again.

The recorder samples the selected process's TCP connections and UDP bindings, and watches changes throughout the game folder. It sends no game input and installs no driver. It works independently of the bot/API. Voice prompts require Windows speech support; otherwise the console shows instructions. Phase markers describe requested activity, not automatically detected actions. Keep other activity minimal for a cleaner comparison.

Each run creates a unique `recordings/<timestamp-id>/` folder containing `summary.md`, `phases.csv`, `connections.csv`, `file-changes.csv`, and an incrementally flushed `events.jsonl`. Start with the summary to compare endpoints and changed paths across phases. Empty CSVs mean no observations of that type; sample records in JSONL distinguish successful empty samples from failures. Close normally by waiting for completion; Ctrl+C may preserve partial results, but forced termination may leave only JSONL.

For another installation, multiple game clients, or different timing:

```powershell
.\tools\Record-KathanaSession.ps1 -TargetProcessId 1234 -WatchPath 'D:\SteamLibrary\steamapps\common\Kathana' -PhaseSeconds 30 -SampleSeconds 2
```

`-Silent` disables speech. The output directory must be outside the watched folder. No administrator rights are requested automatically; access errors are recorded in the report. The recorder stops if the selected process exits. It does not follow launcher or child processes.

Limitations: folder notifications cannot attribute changes to a process and do not record reads. TCP/UDP snapshots do not capture packets, byte counts, DNS requests, decrypted commands, or identify application protocols. Short-lived endpoints may be missed. A persistent connection can carry every gameplay action without changing these snapshots. Duplicate file notifications are normal. These observations can identify candidates for further investigation, but do not establish API access. Local paths and network addresses are included in the output.

Underlying Windows facilities: [Get-NetTCPConnection](https://learn.microsoft.com/en-us/powershell/module/nettcpip/get-nettcpconnection) and [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher).
