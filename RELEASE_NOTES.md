## 1.0.244
- Replaced Quiz-only choices with GPT-6 Luna (Fast / Cheap) and GPT-6 Astra (Ultrafast / Expensive). Saves model IDs separately from display labels; older selections migrate to Luna. Luna uses Fast processing with no reasoning; Astra uses Ultrafast with low reasoning. Trade analysis remains GPT-5 nano.
- Removed silent processing-tier fallback for Quiz. Unsupported tiers or an explicit lower-tier response stop the solve without clicking; mandatory-search retry retains the selected model/tier and shares the existing 30-second deadline.
- Fixed Trade's misleading foreground error caused by background validation requiring a hardcoded Steam executable path and process-image access. Its dedicated foreground sender now binds the explicitly selected live/restored HWND and PID; background/hook target checks remain unchanged.
- The first whisper waits up to 10 seconds for asynchronous activation or a user click, with Stop/F12 cancellation and no key events before focus. Later rows retain strict focus-loss stops and cannot reclaim focus. Owned releases and submitted-row handling remain unchanged.
- Validation: all 13 managed test programs passed, including 37 Quiz policy/request tests, Quiz model/profile/layout fixtures, 1,566 foreground Trade assertions and 29 native target assertions using an owned external process. Both rendered tabs were inspected. No live API calls or game input were made; live model availability, latency and in-game whisper acceptance remain unverified.

## 1.0.243
- Fixed Trade verification rejecting valid AI listings solely because item/evidence capitalization or whitespace changed, or evidence included the same post's author header. Matching maps back to original source offsets and returns source item spelling and evidence.
- Preserved exact character names, post boundaries and per-item buy/sell intent; punctuation, word order and upgrade numbers remain exact. Whole-token checks reject prefix variants such as 7D versus 7DAYS and +9 versus +90.
- Recognizes explicit S-/B-/T-, S/ and B/ shorthand, plus prefix for sale. Buy/sell words require lexical boundaries so SELLER and BUYBACK cannot create listings.
- Clarified the extraction prompt with an exact-copy example and empty results for non-trade posts. Failed validation identifies the batch and preserves the previous queue. No paid retry or model switch was added; Trade remains GPT-5 nano and Quiz keeps its configured model.
- Validation: all 13 managed test programs passed, including 291 new source-validation assertions and the existing 26,055 batching assertions. Offline responses cover original source reconstruction, wrapped whitespace, own-header evidence, exact variants/names, mixed intent, cross-author and Follow guards, marker boundaries, invalid output and cache replay. The Trade layout was inspected visually. No live API calls or game input were made; live extraction remains unverified.

## 1.0.242
- Removed Configure Discord, Import latest, Auto and the reader post count from the Trade source toolbar. Browser capture remains available.
- Retired reader requests and polling, including previously saved Auto settings. Loading profiles preserves existing posts, reviewed queues and legacy connection metadata without decrypting the old reader token.
- Browser capture uses its own selected post count. Updated the source placeholder and Help for Browser capture and manual paste.
- Validation: all 13 managed test programs passed. Offline checks cover the browser-only toolbar, absent reader controls/timers/fetch, inert legacy entry points, saved Auto retirement, native 1,000-post source/profile preservation, browser capture and AI commit guards. The Trade layout was inspected visually; no live Discord requests or game input were sent.

## 1.0.241
- Trade analysis now uses GPT-5 nano with minimal reasoning and explicit Standard processing independently of the Quiz model. Sorting, item ranking, filters and queue creation remain local with no API charge; no automatic expensive-model fallback is used.
- Increased batches from 20 to 50 complete posts within the existing 8,000-character bound and allows up to four requests at once. Results are merged in source order after complete success; cancellation and failures stop pending work and retain the previous reviewed queue.
- Reuses completed, verified batch results in a bounded in-memory session cache keyed by the effective model/request and exact source/validation context. Cached results are copied and checked against the current posts; credentials are excluded, and unsuccessful batches are never reused.
- Runs analysis off the UI thread so large fully cached histories keep the controls and Stop/F12 responsive.
- Validation: all 13 managed test programs passed, including 26,055 batching assertions and 65 economy assertions. Mock requests verify complete 1,000/10,000-post coverage, four overlapping requests, deterministic out-of-order results, failure/cancellation draining, Standard nano/minimal payload, cache copies/context/empty successes and bounded eviction. The Trade layout was inspected visually. No live API calls were made; actual latency, extraction quality and billed usage remain unverified.

## 1.0.240
- Fixed Start whispers silently returning through the keyboard-only background guard. Trade now offers an explicitly labelled foreground start action with visible busy/locked/workflow reasons; refreshing background combat settings no longer cancels this explicit action.
- Added a dedicated foreground Enter/text/Enter sender with captured window/PID checks, exact Unicode text, cancellation/F12, focus-loss stops and owned-key cleanup. It uses its own input instance and preserves the background combat backend and keyboard method.
- Reserves Trade mode, stops combat, retains all input-worker references and waits for them to finish before opening chat. Focus is acquired for the first whisper only; switching apps during typing or between rows stops the queue.
- Submitted rows stay unchecked on cancellation; late completion after a source/profile change cannot mark rows on the restored profile. Error/cancellation status survives capture resume. Combat resumes after full completion only while the original selected window is valid and focused.
- Validation: all 13 managed test programs passed, including 1,303 dedicated foreground sender and 49 Start assertions. Owned local windows and fake input covered exact text, activation/focus, Enter emission receipts, cancellation, release failures, worker waits and stale submission ownership. The Trade layout was inspected visually. No live game input was sent; game acceptance remains unverified.

## 1.0.239
- Added Unselect all beside the Trade item filter and Select all matches. Clears all checked items, including hidden filtered selections, and rebuilds the review queue once. Disabled when nothing is checked; repeated clearing preserves manually reviewed rows.
- Validation: all 13 managed test programs passed. Focused checks cover visible/hidden and no-match clearing, persistence, busy guards, manual checkbox enabled state, repeated clearing and a real button click rebuilding the queue once. The rendered layout passed caption/search width, non-overlap, ten-row height and no-scroll checks and was inspected visually.

## 1.0.238
- Fixed Windows display and persistence of imported name/message lines, including saved LF text. Unchanged refreshes preserve analyzed items and edited whispers.
- Replaced the single large AI request with complete-post batches of up to 20 posts or 8,000 characters, processing up to 10,000 posts with batch/post/listing progress. The former 200,000-character total analysis restriction is removed.
- Results are applied only after all requests finish successfully. Cancellation, failed/incomplete responses, stale source changes and profile changes cannot replace the reviewed queue with partial results. Evidence must belong to the same named post; repeated listings are deduplicated across batches.
- Clarified multi-item sell and sell-or-trade extraction and the difference between captured posts, extracted buy/sell listings, selected items and unique whisper recipients. Top three items still start checked; Select all matches includes additional items for the current mode.
- Validation: all 13 managed test programs passed, including 28,323 offline AI batching assertions and 2,108 reader UI assertions. Mocked requests covered every post in 1,000/10,000-post captures, large plaintext sources, same-post evidence, variant collisions, preflight limits and atomic failure/cancellation. Native Windows controls verified 2,999 hard lines across a 1,000-post import, profile restore and unchanged refreshes; the rendered Trade layout was inspected. Live AI extraction quality remains unverified.

## 1.0.237
- Added Select all matches beside Trade's detected-item filter. It checks all visible results for the current Buy/Sell mode, preserves other checked items, and rebuilds the whisper review queue once. With no filter, it selects all detected items; with no matches, the button is disabled.
- Validation: all 13 managed test programs passed, with focused checks for filtered/unfiltered selection, hidden selections, Buy/Sell isolation, queue/persistence updates, busy states, no matches and preserving edited messages on repeated clicks. The rendered Trade tab passed the existing list-height and no-scroll checks and was inspected visually.

## 1.0.236
- Imported Discord posts now show the exact character/author name followed by the message, separated from the next post by a blank line. Removed generated timestamps and Discord message/announcement reference links from the Trade source box; authored message text remains intact.
- Applies to both browser captures and reader-bot imports. Followed announcements keep their delivery-identity qualifier, and legacy parsing cannot attribute those ads to the preceding character.
- Validation: all 13 managed test programs passed, including exact name/message formatting, embedded listing text, Follow boundaries and 10,000-post history imports.

## 1.0.235
- Slowed browser collection: at least 10 seconds between scrolls, initial rendering time, and a quiet period after message requests finish. New messages arriving no longer allow immediate repeated scrolling.
- Allows 45 seconds for slow history loads and longer bounded rendering retries; Stop, receiver loss and source changes remain responsive while waiting.
- Increased browser capture duration to 60 minutes and the local connection lifetime to 90 minutes so large selected batches can finish at the slower pace. The browser extension must be updated/reloaded along with the app.
- Validation: all 13 managed test programs, all 30 extension checks, and extension JavaScript syntax checks passed. Controlled-clock tests cover immediate responses, overlapping requests, a request during receiver polling, delayed rendering/loading, and Stop during the longer wait.
- User feedback confirmed that the previous collector started in their browser but scrolling outran page loading. Revised live pacing remains unverified; verification uses controlled timers and browser fixtures.

## 1.0.234
- Added Trade > Browser capture and a local Chrome/Edge collector for a selected signed-in Discord channel, with 1–10,000 messages per batch. The collector scrolls the channel and captures only message bodies loaded by Discord's normal web UI, then imports the completed snapshot into KathanaBot through a session-specific loopback connection.
- Added exact channel/count/session checks, duplicate/edit handling, bounded collection and response sizes, pause while checked whispers await review, and cancellation on Stop/F12, source changes, profiles and closing. Analysis and whisper sending remain separate explicit actions.
- Browser capture requires a separately loaded extension and an open signed-in Discord tab. Discord prohibits personal-account automation; this route is unsupported and carries an account risk. No live Discord capture was verified: the browser-control connection failed before reaching the tab.
- Validation: all 13 managed test programs and 25 extension checks passed. New coverage includes 64 capture/parser/loopback and 48 UI assertions, a complete offline 10,000-post import, and rendered inspection of the Trade tab and capture dialog. Browser scrolling was tested against controlled DOM/debugger fixtures only.

## 1.0.233
- Added **Posts to import**, selectable from 1 to 10,000 with a default of 10,000, saved with Trade settings and available in the Discord connection dialog.
- Discord history is read in pages of up to 100 messages, with visible progress, cancellation, and waits for Discord's rate-limit delays. The full import is applied only after it completes.
- Raised the imported-history text capacity beyond the previous 200,000-character limit. AI analysis retains its separate 200,000-character limit and explains how to reduce the analysis input without silently trimming imported history.
- Automatic refresh retains loaded history and reads recent messages; a history gap triggers a full refresh. Manual Import latest re-reads the selected history count.
- Validation: all 13 managed test programs passed, including 330 history/pagination, 64 import-service and 82 UI assertions. A 10,000-message fixture retained all posts across 100 pages, beyond the former text limit; both updated layouts were rendered and inspected. No live Discord requests were used for verification.

## 1.0.232
- Added a Discord receiving-channel connection to Trade: import the latest 100 posts and optionally refresh every 30 seconds, including Follow/webhook messages and embedded listing text.
- Added a masked bot-token dialog with encryption tied to the current Windows account. The reader uses a server-installed bot with View Channel, Read Message History and Message Content access.
- Imports preserve exact text/case and source references. They do not start AI analysis or send whispers; changed posts invalidate the previous extracted queue for review. Automatic refresh pauses during analysis, whisper sending and a checked queue awaiting review.
- Added cancellation on Stop/F12, reconfiguration, profile changes and closing; rate-limit backoff; bounded responses; and clear authorization/content-access errors. Empty channels and failed imports preserve existing posts.
- Follow delivers only future source-published announcements. Live Follow delivery is awaiting a published post and reader-bot configuration.
- Validation: all 13 managed test programs passed, including 65 new offline service assertions and 59 new UI assertions. Both Trade layouts were rendered and inspected; no live Discord requests were used for verification.

## 1.0.231
- Restored background operation with keyboard-only input. Another app can remain active; the bot validates the exact game target and does not take foreground focus or move the system cursor.
- Added four selectable background key methods: posted normal scan codes (default), synchronous normal scan codes, posted virtual keys and synchronous virtual keys. Stop automation and use the Background keys button; the choice is saved, or override it with --background-key-mode.
- Retained randomized short holds, explicit modifiers, target-owned cleanup and original key-release format/method. Method changes fail while owned releases remain pending.
- Mouse/text workflows and automatic game relaunch are skipped in this keyboard build. Runtime filtering preserves saved profile preferences and retains keyboard movement, Ctrl/Alt chords, F pickup and keyboard support actions.
- The methods remain software-generated window input; acceptance and enforcement conclusions must follow the live evidence, not API return values.
- Validation: all 13 managed test programs passed, including 2,401 background assertions. The packaged EXE visibly toggled inventory with each method and twice through the default Lite engine while another window retained focus. These bounded checks do not establish sustained combat or detection compatibility.
- Subsequent user use was reported terminated for suspicious activity; the running panel selected posted scan codes. This build has not solved the continued-use requirement. See S03 in BACKGROUND_INPUT_EXPERIMENTS.md.

## 1.0.230
- Switched production keyboard, mouse and text delivery to the retained foreground Windows SendInput backend. The game must stay visible and focused; mouse actions use the system cursor. Earlier background standalone files are retained.
- Preserved randomized 150-250 ms short key holds and exact configured holds of 250 ms or longer. The watchdog releases owned input after focus loss and retries failed releases.
- Applied exact installed-game validation before foreground activation/input, updated UI/API focus requirements, and reject production background diagnostics in this foreground build.
- API status reports inputMode=foreground. No live input or enforcement testing was performed; this method does not guarantee avoidance of detection.

## 1.0.229
- Short background key holds now sample a fresh duration from 150-250 ms for attack/role taps, Ctrl/Alt shortcuts and manual API keys. Requests above 150 ms set the lower bound; configured holds of 250 ms or longer remain exact.
- Kept target-only delivery, modifier cleanup and immediate cancellation releases. Input diagnostics now describe the randomized range instead of reporting a separate sample as the actual hold.
- Timing variation has not been verified against the game's enforcement and does not guarantee that automation will avoid detection. No live input tests were performed for this change.

## 1.0.228
- All attack and role keys use the same target-only zero-scan-code keyboard path as the verified inventory input, including numbers, function keys and Ctrl/Alt skill shortcuts.
- Background taps now last at least 150 ms, matching the successful live key trial so short presses can be observed by client state polling. Longer configured holds remain longer; stopping/cancellation releases keys promptly. Key taps take longer, while configured cooldowns and role conditions are retained.
- Fixed explicit right/left Alt, Ctrl and Shift being collapsed to generic modifiers; owned releases and mouse modifier masks preserve the selected sides.
- Added role-dispatch, key-matrix, modifier ownership and cleanup regressions, plus a bounded background-key diagnostic. Background mouse/text acceptance remains unresolved as recorded in BACKGROUND_INPUT_EXPERIMENTS.md.

## 1.0.227
- Added target-only background client input through the installed game's SDL3 message path. Keyboard commands omit scan codes so SDL accepts virtual keys while raw keyboard handling is enabled.
- Routed mouse and text requests through the selected game window with a virtual game cursor. The backend keeps the foreground application and physical cursor independent of bot commands.
- Added target/PID validation, owned key/button cleanup, timeout handling and backend regression coverage. API status reports sdl-background; the control panel displays Background client input.
- Live background tests opened and closed inventory using posted and synchronous zero-scan-code key messages with unchanged foreground ownership. Production-backend checks are recorded in docs/INTERNAL_INPUT_HOOK.md.
- The packaged EXE's isolated Lite engine also toggled inventory in the background with normal action timing. Background mouse messages produced no verified UI acceptance; live text and full combat workflows remain unverified.

## 1.0.226
- Restored foreground SendInput for all control-panel keyboard, mouse and chat output. Bot startup no longer depends on the blocked DLL hook; native hook build and payload requirements were removed.
- Improved target activation and held-input cleanup. Manual API input activates the selected game and holds taps for 120 ms instead of sending an immediate down/up pair.
- Live testing confirmed Escape dismisses the game notice and I opens/closes the inventory. Added a controlled --input-diagnostics capture/inventory/escape mode; the game must remain foreground.
- Verified the standalone executable starts the bot against the live game and reports running with no input error.

## 1.0.225
- Block bot startup when the internal input hook cannot connect, show the Windows error code/description, and report the correct input mode and connection error through the local API.
- Live testing found access-denied failures in the current Kathana client. This diagnostics update does not make DLL injection work with that client.

## 1.0.224
- Routed control-panel keyboard, mouse, cursor and chat output through an embedded x64 process-local input hook for the confirmed Steam Kathana client. The client imports buffered Raw Input; the hook targets that path and its keyboard, cursor and message reads.
- Added heartbeat cleanup, target validation, foreground-independent workflows, native hook tests and managed routing tests. No OS input fallback is used.
- Live gameplay acceptance is unverified. See docs/INTERNAL_INPUT_HOOK.md for requirements and limits.

## 1.0.223
- Added background monitoring with configurable alerts for smoother long-running sessions, plus internal reliability fixes and expanded regression coverage.

## 1.0.217
- Added a fresh 5-15 ms delay before each generated key-down (including Unicode text and modifier presses) and mouse-button-down, across all features using the foreground input backend.
- Skill cooldowns remain unchanged. Key/button releases and cursor moves have no added delay; focus and click coverage are checked again after the wait.
- Timing variation is not a guarantee against server disconnections or automation detection.

## 1.0.216
- All generated game keyboard, mouse, cursor and Unicode text input now goes through a shared foreground-only SendInput backend; no PostMessage, keybd_event, mouse_event or SetCursorPos input fallbacks remain.
- Background Only is removed as an operating mode. Old profile flags are ignored; the UI reports Foreground SendInput only.
- Every new input checks the target window/process and foreground state. Clicks also reject covered cursor positions. A watchdog releases bot-owned keys/buttons on focus loss and retries failed releases.
- Combat/Lite/Direct KP, navigation, loot, buffs, party/support, Trade, Quiz, RESU, disconnect and relaunch paths use the shared input boundary. Relaunch no longer clicks unverified desktop coordinates.
- Chat preserves Unicode case/punctuation and does not submit partial text after input failure or focus loss. Modifier cleanup is unconditional.
- Added input-backend and native-import regression checks. No claim is made that foreground input is undetectable.

## 1.0.215
- Enabled skill rows are green; editable key cells are purple. Add skill creates additional saved Ctrl+number or Alt+number rows.
- Added a persistent Background Only button that disables foreground-dependent features, reports the changes, and blocks focus, mouse, and physical key presses while enabled.
- Loot After Kill now holds F for one second by default, with a configurable 0.05-5 second duration beside its toggle. Stopping releases the held key promptly.
- Added regression coverage for background input protection, shortcut key order, cancellable loot holds, and saved custom skill rows.

## 1.0.214
- Fixed Full mode continuing to attack a stale target indefinitely: Auto Retarget If Stuck now overrides the combat lock after the configured no-progress delay, with Loot After Kill either on or off.
- Successful retargets clear old target sightings and damage history so the next mob gets a fresh recovery window. Oscillating stale HP readings no longer count as repeated damage.
- Stuck recovery respects capture validity, death/recovery priority, mode exclusions, and retarget cooldowns; added a recovery log with the elapsed no-progress time.
- Added regressions for ten minutes of combat followed by a stale target, external kills, continued real damage, stale HP jitter, and optional post-kill pickup.

## 1.0.213
- Reset All Settings disables features and overlays, clears calibration and lists, and resets cooldowns to supported minimums while preserving saved character profiles.
- Home pulses red at zero character HP; stopped bots have a red surface tint.
- Hidden tabs remain unlocked across restarts of the same app version and require the password again after an update.
- Daily rupiah projection now uses wallet-sized gray text.
- Added a switch for the in-game bot overlay so Reset All Settings can turn it off too.

## 1.0.212
- Fixed Trade selections clearing the whisper queue when a custom message omitted {items}. Plain messages now work unchanged; optional item substitution and exact-case recipient names are preserved.

## 1.0.211
- Added Reset All Settings beside Profiles, with explicit confirmation; resets current settings and keeps saved character profiles.
- Suppressed persistence and live config pushes during profile application; committed overlay drags save immediately.
- Preserved saved calibration coordinates, including older factory coordinates, instead of migrating them on reload.
- Remembered Full/LITE Direct KP selection across bot stop/start and in saved profiles.
- Clarified target calibration labels: Mob HP - Red Bar and Mob HP - Numbers (OCR).
- Added a colorful monitor icon to Remote Desktop Mosaic.

# KathanaBot 1.0.210

## What changed

- **Restored direct F pickup for Loot After Kill** when an attacked mob reaches zero HP or disappears - it no longer requires the loot scanner or a centered item detection. Long fights stay armed using the latest living-mob observation instead of expiring 3 seconds after the last attack key, failed sends retry briefly, and a successful pickup is never repeated for the same death. Centered Loot Pickup and timed-arrow behavior are unchanged.
- **Fixed Home not refreshing reliably.** It now reads a live engine snapshot immediately on Start/Stop and on every UI tick instead of relying on cached status callbacks or a tab switch, keeping the play/pause icon, status, and session readings in sync.
- **Fixed stale/duplicated Home panels and layout gaps after startup or Start**, and switching tabs no longer resets your window size.
- **Added AUTO-ASSIST ONLY next to LITE Direct KP**: blocks every bot-generated E key press (configured E rows and normal/manual/forced retargeting) while leaving all other keys - including Direct KP's own R - working normally. Enabling it requires a warning plus a separate "Are you sure?" confirmation; canceling either leaves it off, and the setting always resets when the app closes.

# KathanaBot 1.0.207

## What changed

- **Added LITE Direct KP to Combat Full**: a gold-bordered red/green toggle with a configurable E-to-E cycle (200-60,000 ms; default 1,000 ms). Each pair presses E, then R 100 ms later, with at least 100 ms before the next E; delayed cycles never catch up in a burst. It bypasses normal/forced/configured/manual retargeting entirely - configured attacks, heals, buffs, and other enabled features keep running as normal.
- Because target recognition no longer gates attacks in this mode, a warning and a cancellable five-second notice appear before its first activation each launch: monster filters, target exclusions, kill/loot tracking, navigation, and target-based conditions may not work correctly with unconditional E/R. Main Stop and toggling it off immediately cancel any pending input; chat pauses, death, a missing/minimized window, or stale telemetry all suspend it the same as other features.
- While enabled, Combat Skills switches to a compact cream-colored card layout for the same underlying rows (role, cooldown, priority, trigger); turning it back off restores the normal grid without losing any edits. The separate Lite tab/start path has been retired - this is now the Lite experience, built into Combat Full.
- The Combat Full running button and the floating in-game status panel now read "FULL RUNNING" or "LITE RUNNING" depending on whether Direct KP is active; both still control the same running engine, only the displayed label changes.

# KathanaBot 1.0.204

## What changed

- Fixed the delayed Windows ding regression introduced by the 1.0.200 colored debug log. Above 160,000 characters, trimming tried to clear a selection in a read-only RichTextBox; the native edit was rejected and retried every five seconds. The onset depended on log volume, not an alarm timer or attack key.
- Log trimming now permits only the synchronous programmatic deletion, restores read-only mode in a Finally block, preserves category colors, and clears undo history.
- Trim boundaries now use RichTextBox's normalized LF newlines so retained messages remain complete.
- Added a regression test that crosses the production threshold repeatedly and checks bounded size, retained colors/messages, read-only restoration, and absence of read-only WM_CLEAR requests.

# KathanaBot 1.0.203

## What changed

- **Renamed "Hold on Place" to "Max Range"** throughout the app (tab, controls, log/status messages), and added a Help button explaining anchor, tolerance, restrictiveness, and leash settings. It also now shows a clear "inactive" readout while disabled instead of repeating disabled-state log entries.
- **Removed the "Hold to Show Game Window" hotkey controls entirely**, including its background hotkey worker; old saved settings can no longer reactivate it.
- **Removed the "General" category and its icons from the buff icon selector.**

## Recent change history - last 5

1. **Pop-up dialogs (warnings, errors, confirmations) no longer play the Windows alert sound.** They keep the same text, icon, and OK/Cancel/Yes/No buttons; only the system sound that Windows normally triggers for a dialog with an icon has been silenced.
2. **Removed the local audible alarm sound on HP-zero death and game-freeze alerts.** Phone notifications (Discord webhook / ntfy) continue to fire exactly as before; only the local system-sound pulse and its temporary volume override were removed.
3. **Added an "Activity & Setup" tab** with a Timeline (recent actions with HP/MP/target context, skip reasons, and mode changes), an OCR readings view showing reading age and confidence when available, and a Route preview that plots the selected Leveling route with waypoint numbers, direction arrows, and long jumps highlighted in red.
4. **The Diagnostics tab now always shows current status** at the top - what the bot is waiting on or blocked by, plus whether settings are saved/applied, with error details on save or apply failures instead of failing silently.
5. **Log lines are now color-coded by category** (errors, warnings, waiting, success, combat, loot, OCR, navigation) with a legend, and the Combat Full tab's panels are now resizable by dragging their splitters.
