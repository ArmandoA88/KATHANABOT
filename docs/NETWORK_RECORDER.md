# Network recording alongside gameplay phases

1. Enter Kathana and stop bot automation.
2. Right-click `Record-Kathana-Network.cmd` in the repository root and choose **Run as administrator**. Windows Packet Monitor requires elevation.
3. Switch back to the game during the spoken ten-second countdown. Follow the same five 30-second phases: idle, inventory, movement, skill, recovery.
4. Leave the recorder window open until completion. Find results in `recordings/network-<timestamp-id>/`.

`packets.pcapng` contains the packet capture for Wireshark or later analysis. `capture.json` records the selected endpoints and capture timestamps. The nested timestamp folder contains `phases.csv`, `events.jsonl`, and the connection/file summary. `capture-stop.txt` preserves Packet Monitor's stop statistics. The original `packets.etl` is retained for recovery or reconversion.

Only established TCP connections belonging to the selected game and using remote port 40001 at capture start are selected. Filters match remote IP and both ports in either direction; Packet Monitor itself does not attribute packets to a process. Reconnected sessions with a different tuple will be missed. HTTPS connections and unrelated traffic are excluded by these filters. A different port can be supplied with `-RemotePort`; multiple clients require `-TargetProcessId` when calling the PowerShell script directly.

The script requires Packet Monitor to be stopped with an empty filter table. Do not use another Packet Monitor session or change its filters during this recording: this Windows version only supports clearing the entire filter table, which the script does during cleanup. It recognizes English status output; unrecognized/localized output fails without changing state. Capture uses NIC components and full packets, with a 128 MB circular limit: unusually heavy traffic can overwrite early packets. Virtual adapters may produce duplicates. Packets may include account or chat data if the protocol is readable; keep captures local for analysis.

The capture does not inject input, replay packets, modify the client, or decrypt encrypted traffic. Phase timestamps mark instructions, not verified game actions. Opaque data alone does not prove encryption. Actual analysis must distinguish protocol headers, compression, encryption, and arbitrary binary formats.

If the window is forcibly closed before cleanup, run `pktmon stop` from an administrator terminal. Inspect `pktmon filter list`; if only this session's `Kathana-...` filters remain, clear them with `pktmon filter remove`. Do not stop or clear a separate capture belonging to someone else.

Recovery conversion: `pktmon etl2pcap "full-path\packets.etl" --out "full-path\packets.pcapng"`.
