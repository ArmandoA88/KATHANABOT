# Kathana Discord browser capture

This local, unpacked Chrome/Edge extension collects posts from the electronic-board channel you open in your signed-in Discord web tab and imports them into KathanaBot. You paste one temporary connection, rather than copying every history page. KathanaBot controls the requested count (1–10,000).

**This is unsupported personal-account automation. Discord prohibits automating normal user accounts and may restrict or terminate the account. Using the normal web page does not make the automation supported.** See [Discord's policy](https://support.discord.com/hc/en-us/articles/115002192352-Automated-User-Accounts-Self-Bots). An administrator-installed Discord reader bot remains the supported automatic route.

## Install once

1. Use Chrome or Chromium-based Edge version 118 or later.
2. Open `chrome://extensions` in Chrome, or `edge://extensions` in Edge.
3. Turn on **Developer mode**.
4. Click **Load unpacked**, then choose this `discord-browser-capture` folder containing `manifest.json`.
5. Open the browser's Extensions menu and pin **Kathana Discord Capture**.

No Discord bot token, personal-account token, password, or cookie is entered in this extension.

## Capture posts

1. In KathanaBot's **Trade / Discord Whispers** tab, open **Browser capture**, choose **Posts to capture**, then click **Start capture → Copy setup**.
2. Open the configured source channel in Discord's web app. For the Kathana electronic-board channel, use [this channel link](https://discord.com/channels/1363839027114934315/1486808135883555037).
3. Close DevTools for that tab. Chrome cannot attach this collector while DevTools has its own debugger connection.
4. Click the extension icon, paste the connection JSON, and click **Start**. Chrome displays its normal debugging banner.
5. Keep this channel tab open and avoid manually scrolling during collection. The extension reloads the channel, captures normal message-history response bodies, and scrolls only a chat container identified from IDs in those responses. It waits at least **5 seconds after the initial response** and **10 seconds between scrolls**, then allows all matching history requests and message processing to finish with **3 seconds of network quiet** before moving again. It waits while KathanaBot is reviewing or sending a whisper queue.
6. Reopen the extension popup to see progress. Once the selected count is reached, or the browser stops loading older posts, the captured snapshot goes directly to KathanaBot. Review it and analyze it separately; importing does not send whispers.
7. **Stop**, closing the tab, navigating to a different channel, or cancelling Chrome's debugging banner ends capture. An unfinished capture is discarded.

The connection expires after a bounded session and must be copied again for another capture. The extension keeps the connection and captured messages only in service-worker memory; it does not write them to browser storage. The popup can close during capture because Chrome 118+ keeps an attached debugger worker alive.

## Limits and status

- The browser must already have access to the source channel. This does not bypass server permissions or publication restrictions.
- Requested history may be unavailable. If Discord returns an empty normal older-history response, the collector reports history ended. If three attempts load no new IDs, it imports the loaded snapshot with **browser stalled** status; that does not prove the channel's history ended.
- The collector allows up to **45 seconds for each history load** and **20 seconds for captured message IDs to identify a rendered chat scroller**. If they cannot identify a scroller, it imports only the already captured snapshot with **browser stalled** status. It never guesses a sidebar or page scroller. Browser updates, virtualized rows, short channels, background-tab throttling, or future Discord layout changes can prevent scrolling.
- Capture is limited to **60 minutes**, 10,000 unique posts, 100 messages per observed response, 4 MiB per decoded response, and a bounded sanitized payload. Large captures take longer with the fixed pauses; available history and page sizes can prevent reaching the requested count within that limit. Existing app connections that expire earlier still stop normally. Rate-limit/access errors (401, 403, 429), receiver loss, and invalid responses stop rather than retrying Discord API calls.
- Only message content and selected trade-relevant metadata are sent to the temporary nonce-protected local `127.0.0.1` receiver. The extension never calls Discord API endpoints itself, replays requests, reads request headers, or extracts account credentials. It ignores other network responses and does not download attachments.
- The debugger permission is powerful and Chrome warns about it. This implementation scopes its work to the one selected matching channel tab. Disable or remove the unpacked extension when you finish using it.
- KathanaBot's separate AI-analysis character limit still applies. A large imported history may need to be narrowed before analysis.

## Offline verification

Run `node --test tests/*.test.mjs` from this folder using Node 22.14 or later. Tests exercise URL/channel boundaries, nonce/count limits, sanitization, UTF-8/base64 bounds, deduplication and newest-message ordering, 10,000-message retention, proven-scroller selection, and mock debugger event/stop behavior. A controlled clock tests fixed pacing, delayed rendering, overlapping requests, network quiet, and Stop during a wait without spending real minutes. Node 22 labels its documented MockTimers API experimental. No live Discord account, browser, or game input is used by these tests. Actual Discord scrolling remains a user-run compatibility check.

The extension uses current documented [debugger](https://developer.chrome.com/docs/extensions/reference/api/debugger), [scripting](https://developer.chrome.com/docs/extensions/reference/api/scripting), [cross-origin extension requests](https://developer.chrome.com/docs/extensions/develop/concepts/network-requests), and [service-worker lifecycle](https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle) APIs. It uses no external JavaScript dependencies or remotely loaded code.
