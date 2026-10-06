# Discord Trade import

As of 1.0.242, Trade uses **Browser capture** or manually pasted character names
and messages. Configure Discord, Import latest, Auto and the reader post count
have been removed. Previously saved Auto settings cannot start reader polling;
existing posts, reviewed queues and legacy connection metadata are preserved.

This setup uses:

- Source: [Kathana electronic-board](https://discord.com/channels/1363839027114934315/1486808135883555037).
- Receiving channel: [your receiving channel](https://discord.com/channels/1483326612538654813/1556796091855278112).

The user has set up Follow; no source-published post has arrived yet. Three original
electronic-board posts inspected on October 5, 2026 had message-level `flags: 0`,
so those posts were unpublished when inspected. This does not establish the status
of every message. End-to-end Follow delivery remains unverified.

## Browser capture

As of version 1.0.236, the Trade source box displays each imported post as the
exact author name followed by the message, with a blank line between posts.
Generated timestamps and Discord message/announcement links are omitted. Links
written within a message remain part of its text. Structured message IDs, dates
and source metadata still support ordering and edit handling. This applies to
older reader-bot imports too; saved text uses the new format after another import.
Followed announcements keep the delivery-identity qualifier described below.

Browser capture automates collecting a batch from a channel you can open in your
personal Discord account. It requires Discord's web app in Chrome/Edge 118 or later
and a local extension. **Discord prohibits personal-account automation and may
terminate an account for it.** This is an unsupported option, not a Discord bot
integration or a guarantee of account safety.
[Discord's account automation policy](https://support.discord.com/hc/en-us/articles/115002192352-Automated-User-Accounts-Self-Bots).

1. Open `chrome://extensions` (Chrome) or `edge://extensions` (Edge). Enable
   **Developer mode**, click **Load unpacked**, and select
   `tools/discord-browser-capture` inside this KathanaBot workspace. Pin
   **Kathana Discord Capture** to the browser toolbar.
2. Open [electronic-board](https://discord.com/channels/1363839027114934315/1486808135883555037)
   in that browser and sign in normally. Close Developer Tools before capture;
   an open debugger can conflict with the extension's connection.
3. In KathanaBot, open **Trade > Browser capture**. Check the original channel link
   and choose **1–10,000** messages, then click **Start capture** and **Copy setup**.
4. Click the browser extension, paste the setup, and click its **Start** button.
   The collector reloads this channel and scrolls automatically while collecting
   message bodies Discord loads. Keep the channel tab open and avoid navigating
   or scrolling it manually until capture completes.
5. The completed batch arrives automatically in the Trade source box. Review the
   reported count. A stalled browser or fewer available posts can produce a
   clearly labelled partial batch; the collector cannot promise 10,000 messages
   when Discord does not load them. **Analyze posts with AI**, review game names
   and checked recipients, then start whispers separately when ready.

**Stop/F12** in KathanaBot invalidates the local connection. The extension stops on
its next connection check; its own **Stop** button cancels immediately. Closing
the setup dialog leaves a session you explicitly started active. Changing the
source, loading a profile, or closing KathanaBot ends it. Each completed batch
requires a new capture session/setup; this does not continuously archive the feed.

Version 1.0.235 waits at least **10 seconds between scrolls**, leaves time for the
initial page to render, and waits for message requests to finish and remain quiet
before moving again. A slow history page gets up to 45 seconds to load. Update the
unpacked extension files and click its reload button in the browser's Extensions
page before starting a new connection.

The local connection expires after 90 minutes, and each browser collection is
bounded to 60 minutes. Import pauses while analysis/sending is busy or checked
whispers await review. Existing posts are kept on invalid, empty or cancelled
captures. Reader-bot imports and automatic polling are retired.

The collector captures only bodies of ordinary message-history responses for the
selected channel. It does not replay Discord API calls, access login credentials,
read cookies, or copy request headers. It sends sanitized message data to the local
KathanaBot connection, not to an external server. Attachment files are not
downloaded. Discord usernames/webhook display names still require review against
actual game character names.

The local bridge and collector are verified offline. The user reported that
collection started in their browser, but scrolling was too fast for page loading.
Version 1.0.235 adds slower pacing; **the revised live pacing remains unverified**.
Discord's UI, loading behavior, debugger compatibility and browser
background throttling can affect collection. Use the displayed results to verify
what actually loaded rather than assuming a complete history.

See [extension setup and troubleshooting](../tools/discord-browser-capture/README.md).

## Manual paste and analysis

Paste each exact character name on one line and its message on the next, with a
blank line between posts. Browser captures already use this format. Then click
**Analyze posts with AI**, review exact game names and checked whispers, and
start sending separately when ready.

AI analysis uses **GPT-5 nano** with minimal reasoning, independently of the model
selected in Quiz. It shares the encrypted API key. The model's standard token
rates were the lowest listed text-model rates on October 5, 2026: $0.05 per million
input tokens and $0.40 per million output tokens. See
[OpenAI pricing](https://developers.openai.com/api/docs/pricing).

Analysis processes the entire source in batches of up to **50 posts or 8,000
characters**, up to 10,000 posts, with at most four requests in flight. It keeps
each complete name/message block together and reports batch, post and listing
progress. Completed verified batches are reused from a bounded in-memory cache
during this app session when the model/request and exact posts are unchanged;
cached batches make no API request. Changing the source may change batch boundaries
and require new requests. There is no automatic fallback to an expensive model.
Sorting, item counts, filtering and queue creation run locally and cost no API
tokens. Larger imports still take more time and API requests. An individual
oversized post produces an error before any requests;
posts are never silently truncated or skipped. All batches must succeed before
the completed results replace the previous reviewed queue. Stop/F12, failed
requests or incomplete responses preserve that queue. Imported and saved posts
use Windows line breaks so names, bodies and blank separators display correctly.

Version 1.0.243 checks AI evidence against the original named post while allowing
capitalization and whitespace differences. It restores the original item spelling
and evidence before building results. A copied author header can be part of
evidence, but the item must still belong to that post's body and original buy/sell
clause. Character names stay case-sensitive; punctuation, word order and upgrade
digits are not guessed or changed. Prefix variants and evidence crossing authors
are rejected. If every listing in a batch remains unverified, its batch number is
shown and the previous queue is kept. This adds no paid retries or model fallback.

Captured posts and whisper recipients have different counts: Buy from sellers
keeps sell listings, Sell to buyers keeps buy listings, and repeated listings
from the same exact character combine into one whisper. Only the top three
items start selected. To include all detected items in the current mode, clear
the item search and click **Select all matches**, then review the queue.
**Unselect all** clears every checked item, including hidden filtered selections,
and clears the review queue. Chatter
and posts without clear buy/sell intent or an exact character are omitted.

**Start whispers (foreground)** stops combat input and uses a separate foreground
text sender. Keep the selected Full game window focused and close its chat before
starting. The sender checks the exact selected window and process without needing
access to the executable's installation path. Start allows up to 10 seconds for
focus to settle; click the selected game during that wait if Windows does not
activate it automatically. No keys are sent before focus is confirmed.
Switching apps after typing begins, Stop/F12 or a failed send stops the queue; close any
unfinished chat draft before retrying unsent rows. Completed submissions are
unchecked. Successful completion resumes the previous combat mode only when its
original game window is still valid and focused; its background keyboard setting
is preserved.

Follow can attribute delivery to a server identity instead of the original seller.
Treat delivery names as metadata: review the actual game character/IGN in each post
before sending a whisper. Posts without an unambiguous character should not become
recipients.

## Earlier Follow setup

The earlier reader route depended on Discord Following delivering source-published
announcements into the receiving channel. It did not copy existing history or
unpublished posts. The receiving channel remained empty during the user's setup,
and this reader route was removed from Trade in 1.0.242. Use Browser capture or
manual paste for the current workflow.
