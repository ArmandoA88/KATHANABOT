# Controlled gameplay recording

Right-click **Record-Kathana-Controlled.cmd** and choose **Run as administrator**. Enter one action (`Inventory`, `Move`, or `Skill`) and its actual key binding (`I`, `W`, `D1` for number 1, `F1`, etc.). Stop bot automation and keep the game foreground. Follow the spoken prompts.

Each run has a 30-second idle baseline, five 30-second trials, and a 30-second recovery. Perform the same action once at each trial prompt, then remain idle. For movement use a brief press of the chosen movement key. For a skill choose one whose cooldown permits repetition. For inventory use one toggle per trial and note that opening and closing are different actions; the trials will alternate unless your binding behaves differently. Record different action types in separate sessions.

`action-keys.csv` in the observation subfolder records UTC timestamps for the selected key's observed down/up transitions only while the target process is foreground. `focus-lost` means a held key stopped being observed because focus changed, not a confirmed release. A background timer polls at a nominal 5 ms; Windows scheduling and very short taps limit precision. These observations do not prove the game accepted the action. No other keys, typed text, screenshots, or synthetic input are recorded. Phase timestamps remain instruction timestamps.

Network and phase files use the same system UTC clock. Key observation continues independently while connection sampling runs. Capture filters and Packet Monitor requirements are the same as the normal network recorder. Keep the window open until it finishes; results appear under `recordings/network-...`.

The existing capture has TCP sequence gaps despite reporting no lost Packet Monitor events. A future capture may still be incomplete; gap checks must precede any attempt to infer message layouts. No network commands are sent or replayed by these tools.
