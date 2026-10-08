import { CaptureHistory, parseConnection, matchesChannelTab, isMessageRequest, isSnowflake, decodeNetworkBody, isSearchRequest, searchOffset, decodeSearchBody, SEARCH_PAGE_SIZE } from "./core.mjs";
import { scrollObservedChat } from "./scroll.mjs";
import { focusSearchBox, searchBoxFocused, clickNextSearchPage } from "./search.mjs";

const MAX_DURATION_MS = 60 * 60 * 1000;
const INITIAL_RENDER_SETTLE_MS = 5000;
const MIN_SCROLL_INTERVAL_MS = 10000;
const NETWORK_QUIET_MS = 3000;
const WAIT_FOR_BATCH_MS = 45000;
const RENDER_RETRY_MS = 20000;
const POLL_MS = 250;
const RECEIVER_POLL_MS = 2000;
const MAX_STALLED_ATTEMPTS = 3;
// Search mode (KathanaBot sent search items): Discord's own search box filters the channel and its result pages are captured.
const SEARCH_SETTLE_MS = 8000;
const SEARCH_WAIT_MS = 60000;
const MIN_SEARCH_PAGE_INTERVAL_MS = 1000;
const MAX_EMPTY_SEARCH_PAGES = 10;
const MANUAL_IDLE_MS = 3 * 60 * 1000;
let current = null;
let status = { running: false, phase: "idle", message: "Copy a browser-capture connection from KathanaBot to begin.", count: 0, requested: 0 };

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
const alive = session => current === session && !session.cancelled;
function show(session, phase, message) {
  if (!alive(session)) return;
  status = { running: true, phase, message, count: session.history.messages.size, requested: session.connection.messageLimit, sourceUrl: session.connection.sourceUrl, canFinish: session.search };
}

async function detach(session) {
  if (!session.attached) return;
  session.attached = false;
  try { await chrome.debugger.detach({ tabId: session.tabId }); } catch { /* Already detached/closed. */ }
}

async function stopSession(session, phase, message) {
  if (!alive(session)) return;
  session.cancelled = true;
  const count = session.history.messages.size;
  current = null;
  for (const controller of session.fetchControllers) controller.abort();
  session.pending.clear();
  status = { running: false, phase, message, count, requested: session.connection.messageLimit, sourceUrl: session.connection.sourceUrl };
  await detach(session);
  session.history.messages.clear();
}

function deadline(session) {
  if (!alive(session)) throw new Error("Capture stopped.");
  if (Date.now() >= session.deadline) throw new Error("Capture reached its 60-minute limit. Nothing incomplete was imported; try fewer posts.");
}

async function waitSlice(session, milliseconds = POLL_MS) {
  const end = Math.min(Date.now() + milliseconds, session.deadline);
  while (Date.now() < end) {
    deadline(session);
    await delay(Math.min(POLL_MS, end - Date.now()));
  }
  deadline(session);
}

async function localRequest(session, suffix, options = {}) {
  deadline(session);
  const controller = new AbortController();
  session.fetchControllers.add(controller);
  const timer = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await fetch(session.connection.receiverUrl + suffix, {
      ...options, signal: controller.signal, cache: "no-store", credentials: "omit", redirect: "error", referrerPolicy: "no-referrer"
    });
    const text = await response.text();
    if (text.length > 16384) throw new Error("Invalid local receiver response.");
    let body;
    try { body = JSON.parse(text); } catch { throw new Error("Invalid local receiver response."); }
    return { status: response.status, body };
  } catch {
    throw new Error("KathanaBot's local receiver is unavailable. Start a new browser capture in KathanaBot.");
  } finally {
    clearTimeout(timer);
    session.fetchControllers.delete(controller);
  }
}

async function receiverState(session) {
  const response = await localRequest(session, "state", { headers: { Accept: "application/json" } });
  if (response.status !== 200 || response.body?.active !== true || typeof response.body.paused !== "boolean") {
    throw new Error("This KathanaBot capture connection has closed or expired. Copy a new connection.");
  }
  if (response.body.sourceChannelId !== session.connection.channelId || response.body.messageLimit !== session.connection.messageLimit) {
    throw new Error("KathanaBot's channel or post count changed. Copy a new connection.");
  }
  return response.body;
}

async function readyReceiver(session) {
  while (alive(session)) {
    deadline(session);
    const state = await receiverState(session);
    session.lastReceiverCheckAt = Date.now();
    if (!state.paused) return;
    show(session, "paused", "KathanaBot is reviewing or sending whispers. Capture waits without scrolling.");
    await waitSlice(session, 1500);
  }
  throw new Error("Capture stopped.");
}

async function pollReceiver(session, force = false) {
  if (force || Date.now() - session.lastReceiverCheckAt >= RECEIVER_POLL_MS) await readyReceiver(session);
  deadline(session);
}

const historyDrained = session => session.pending.size === 0 && session.processing === 0;
const historyQuiet = session => Date.now() - session.lastHistoryActivityAt >= NETWORK_QUIET_MS;
const nextScrollAt = session => Math.max(
  (session.firstBodyAt ?? Date.now()) + INITIAL_RENDER_SETTLE_MS,
  session.lastScrollAt === null ? 0 : session.lastScrollAt + MIN_SCROLL_INTERVAL_MS
);

async function waitForInitialHistory(session) {
  const end = Math.min(Date.now() + WAIT_FOR_BATCH_MS, session.deadline);
  while (alive(session)) {
    await pollReceiver(session);
    if (session.history.responses > 0 && historyDrained(session)) return;
    if (Date.now() >= end) throw new Error("Discord history loading did not finish. Capture stopped without importing an incomplete batch.");
    await waitSlice(session);
  }
  throw new Error("Capture stopped.");
}

async function waitForHistoryIdle(session) {
  const end = Math.min(Date.now() + WAIT_FOR_BATCH_MS, session.deadline);
  while (alive(session)) {
    await pollReceiver(session);
    if (historyDrained(session) && historyQuiet(session) && Date.now() >= nextScrollAt(session)) return;
    if (Date.now() >= end) throw new Error("Discord history loading did not settle. Capture stopped without importing an incomplete batch.");
    show(session, "waiting", "Giving Discord time to load and render posts. Scrolls are at least 10 seconds apart.");
    await waitSlice(session);
  }
  throw new Error("Capture stopped.");
}

async function waitForNetwork(session, revision) {
  const end = Math.min(Date.now() + WAIT_FOR_BATCH_MS, session.deadline);
  while (alive(session)) {
    await pollReceiver(session);
    const complete = session.historyEnded || session.history.messages.size >= session.connection.messageLimit;
    const changed = session.history.revision !== revision;
    // A new response must not bypass another in-flight history request. The
    // minimum scroll interval is also enforced separately before the next move.
    if (historyDrained(session) && (complete || changed && historyQuiet(session))) return;
    if (Date.now() >= end) {
      if (!historyDrained(session)) throw new Error("Discord history loading did not finish. Capture stopped without importing an incomplete batch.");
      return;
    }
    await waitSlice(session);
  }
  throw new Error("Capture stopped.");
}

async function scrollWhenRendered(session) {
  let renderDeadline = Math.min(Date.now() + RENDER_RETRY_MS, session.deadline);
  while (alive(session)) {
    await pollReceiver(session);
    deadline(session);
    if (!historyDrained(session) || !historyQuiet(session) || Date.now() < nextScrollAt(session)) {
      await waitForHistoryIdle(session);
      // Loading another batch can delay rendering; allow its bounded render
      // window after that batch settles rather than scrolling past it.
      renderDeadline = Math.min(Date.now() + RENDER_RETRY_MS, session.deadline);
    }
    // Recheck the local pause immediately before mutating the selected chat.
    await pollReceiver(session, true);
    if (session.historyEnded || session.history.messages.size >= session.connection.messageLimit) return true;
    // Discord may start another normal history request while the local receiver
    // check awaits HTTP. Revalidate the load/quiet gates in this same turn before
    // issuing the scroll, rather than using the state from before that await.
    if (!historyDrained(session) || !historyQuiet(session) || Date.now() < nextScrollAt(session)) continue;
    const result = await chrome.scripting.executeScript({
      target: { tabId: session.tabId }, func: scrollObservedChat,
      args: [session.connection.guildId, session.connection.channelId, [...session.history.messages.keys()]]
    });
    deadline(session);
    const observed = result?.[0]?.result;
    if (observed?.found) {
      session.lastScrollAt = Date.now();
      return true;
    }
    if (observed?.reason === "navigation") throw new Error("The selected tab left the source channel. Capture stopped.");
    if (Date.now() >= renderDeadline) return false;
    show(session, "rendering", "Waiting for loaded message IDs to identify the actual chat scroller.");
    await waitSlice(session);
  }
  throw new Error("Capture stopped.");
}

async function captureLoop(session) {
  try {
    await waitForInitialHistory(session);
    deadline(session);
    if (session.history.messages.size === 0) {
      throw new Error("No readable channel posts were loaded. Open the source channel, close DevTools, and start again.");
    }
    let stalled = 0;
    while (alive(session) && session.history.messages.size < session.connection.messageLimit && !session.historyEnded && stalled < MAX_STALLED_ATTEMPTS) {
      await waitForHistoryIdle(session);
      deadline(session);
      const tab = await chrome.tabs.get(session.tabId);
      if (!matchesChannelTab(tab.url, session.connection)) throw new Error("The selected tab left the source channel. Capture stopped.");
      show(session, "collecting", `Collecting channel history: ${session.history.messages.size.toLocaleString()} of ${session.connection.messageLimit.toLocaleString()} posts.`);
      const revision = session.history.revision;
      if (!await scrollWhenRendered(session)) { stalled = MAX_STALLED_ATTEMPTS; break; }
      await waitForNetwork(session, revision);
      stalled = session.history.revision === revision ? stalled + 1 : 0;
    }
    deadline(session);
    const outcome = session.history.messages.size >= session.connection.messageLimit ? "limit-reached" : session.historyEnded ? "history-ended" : "stalled";
    await deliverSnapshot(session, outcome);
  } catch (error) {
    await failCapture(session, error);
  }
}

async function failCapture(session, error) {
  // Chrome errors can include arbitrary site text. Only our controlled messages
  // are exposed; foreign API errors receive a fixed redacted description.
  const message = error?.message;
  const safe = /^(Capture |KathanaBot|This KathanaBot|The selected tab|No readable channel|Could not identify|Discord history|Discord search)/.test(message || "");
  await stopSession(session, "error", safe ? message : "The browser capture could not continue. Close DevTools and start a new connection.");
}

// Posts the collected snapshot to the local receiver. outcome: limit-reached | history-ended | stalled | finished-early.
async function deliverSnapshot(session, outcome) {
  const count = session.history.messages.size;
  const exhausted = session.search ? outcome === "history-ended" : session.historyEnded;
  session.sending = true;
  const body = JSON.stringify(session.history.envelope(exhausted, outcome === "finished-early" ? "stalled" : outcome));
  while (alive(session)) {
    await readyReceiver(session);
    show(session, "sending", session.search
      ? `Importing the ${count.toLocaleString()} posts Discord's search found into KathanaBot.`
      : outcome === "stalled"
        ? `Browser stopped loading older posts. Importing the ${count.toLocaleString()} captured posts; older history is unverified.`
        : `Importing ${count.toLocaleString()} captured posts into KathanaBot.`);
    const response = await localRequest(session, "capture", { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body });
    if (response.status === 409) {
      show(session, "paused", "KathanaBot deferred this snapshot. It is kept in memory while the reviewed queue is busy.");
      await waitSlice(session, 1500);
      continue;
    }
    if (response.status !== 200 || response.body?.imported !== true) throw new Error("KathanaBot did not accept the capture. Start a new connection and retry.");
    const searched = session.search ? ` found by Discord search for ${session.connection.searchTerms.map(term => `"${term}"`).join(", ")}` : "";
    await stopSession(session, "complete", outcome === "finished-early"
      ? `Imported the ${count.toLocaleString()} posts${searched || " captured"} when you pressed Import now; later results were not collected.`
      : outcome === "stalled"
        ? `Imported ${count.toLocaleString()} of ${session.connection.messageLimit.toLocaleString()} requested posts${searched}. ${session.search ? "Discord's result pages stopped loading, so later results may be missing." : "The browser stalled; this does not prove history ended."}`
        : `Imported ${count.toLocaleString()} posts${searched}${session.search ? (outcome === "history-ended" ? "; every result page was read" : "") : session.historyEnded ? "; Discord returned the end of loaded history" : ""}.`);
    return;
  }
}

// ---- Search mode -------------------------------------------------------------------------------------------------
const finishing = session => session.finishRequested;
const limitReached = session => session.history.messages.size >= session.connection.messageLimit;

async function runPageScript(session, func) {
  const result = await chrome.scripting.executeScript({ target: { tabId: session.tabId }, func, args: [session.connection.guildId, session.connection.channelId] });
  deadline(session);
  const value = result?.[0]?.result;
  if (value?.reason === "navigation") throw new Error("The selected tab left the source channel. Capture stopped.");
  return value;
}

async function pressKey(session, key, code, keyCode, modifiers = 0, extra = {}) {
  const target = { tabId: session.tabId };
  const base = { key, code, windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode, modifiers };
  await chrome.debugger.sendCommand(target, "Input.dispatchKeyEvent", { type: "keyDown", ...base, ...extra });
  await chrome.debugger.sendCommand(target, "Input.dispatchKeyEvent", { type: "keyUp", ...base });
  deadline(session);
}

// Types one item into Discord's own search box and submits it. False means the box could not be found or focused.
async function submitSearch(session, term) {
  const focus = await runPageScript(session, focusSearchBox);
  if (!focus?.found) {
    // Discord's keyboard shortcut for search is Ctrl+F.
    await pressKey(session, "f", "KeyF", 70, 2);
    await waitSlice(session, 500);
    if (!(await runPageScript(session, searchBoxFocused))?.focused) return false;
  }
  await pressKey(session, "a", "KeyA", 65, 2, { commands: ["selectAll"] });
  await pressKey(session, "Backspace", "Backspace", 8);
  await chrome.debugger.sendCommand({ tabId: session.tabId }, "Input.insertText", { text: term });
  deadline(session);
  await waitSlice(session, 400);
  await pressKey(session, "Enter", "Enter", 13, 0, { text: "\r" });
  return true;
}

async function waitForSearchResponse(session, previousResponses) {
  const end = Math.min(Date.now() + SEARCH_WAIT_MS, session.deadline);
  while (alive(session) && !finishing(session)) {
    await pollReceiver(session);
    if (session.searchResponses > previousResponses && historyDrained(session)) return true;
    if (Date.now() >= end) return false;
    show(session, "waiting", "Waiting for Discord's search results.");
    await waitSlice(session);
  }
  return false;
}

async function waitForSearchPace(session) {
  while (alive(session) && !finishing(session)) {
    await pollReceiver(session);
    if (historyDrained(session) && Date.now() - session.lastSearchAt >= MIN_SEARCH_PAGE_INTERVAL_MS) return;
    show(session, "waiting", "Giving Discord a moment between search result pages (at least 1 second apart).");
    await waitSlice(session);
  }
}

// Automation could not drive Discord's search UI. Capture keeps listening, so the user can search and page by hand.
async function manualSearchPhase(session, reason) {
  session.manualSearch = true;
  let seen = session.searchResponses;
  let idleSince = Date.now();
  while (alive(session) && !finishing(session) && !limitReached(session)) {
    await pollReceiver(session);
    if (session.searchResponses !== seen) { seen = session.searchResponses; idleSince = Date.now(); }
    if (Date.now() - idleSince >= MANUAL_IDLE_MS) return "stalled";
    show(session, "search-manual", `${reason} Search in Discord yourself and click through the result pages; capture keeps listening. Press Import now in the extension when finished (it also imports after 3 minutes without new results).`);
    await waitSlice(session, 500);
  }
  return "finished";
}

// Searches one item and reads every result page. Returns "done" (last page, count reached or only other-channel results),
// "stalled" (Discord stopped answering), "finished" (user pressed Import now) or the manual phase's result.
async function runSearchTerm(session, term) {
  session.lastSearchPage = null;
  session.searchEmptyPages = 0;
  let responses = session.searchResponses;
  if (!await submitSearch(session, term)) return manualSearchPhase(session, "Could not find Discord's search box.");
  if (!await waitForSearchResponse(session, responses)) {
    return finishing(session) ? "finished" : manualSearchPhase(session, `Discord did not answer the search for "${term}".`);
  }
  while (alive(session) && !finishing(session)) {
    const page = session.lastSearchPage;
    if (page.last || limitReached(session) || session.searchEmptyPages >= MAX_EMPTY_SEARCH_PAGES) return "done";
    await waitForSearchPace(session);
    if (finishing(session)) break;
    // Recheck the local pause immediately before pressing a control in the selected chat.
    await pollReceiver(session, true);
    responses = session.searchResponses;
    const click = await runPageScript(session, clickNextSearchPage);
    if (click?.reason === "last-page") return "done";
    if (!click?.found) return manualSearchPhase(session, "Could not find Discord's next-page button.");
    if (!await waitForSearchResponse(session, responses)) return finishing(session) ? "finished" : "stalled";
  }
  return "finished";
}

async function searchLoop(session) {
  try {
    await waitSlice(session, SEARCH_SETTLE_MS);
    const tab = await chrome.tabs.get(session.tabId);
    if (!matchesChannelTab(tab.url, session.connection)) throw new Error("The selected tab left the source channel. Capture stopped.");
    let incomplete = false;
    for (const term of session.connection.searchTerms) {
      if (!alive(session) || finishing(session) || limitReached(session)) break;
      if (session.lastSearchAt > 0) await waitForSearchPace(session); // same 1-second spacing between searches as between pages
      if (finishing(session)) break;
      show(session, "searching", `Searching Discord for "${term}".`);
      const result = await runSearchTerm(session, term);
      if (result === "stalled") incomplete = true;
      if (result === "finished" || result === "stalled" || session.manualSearch) break;
    }
    deadline(session);
    if (session.history.messages.size === 0) {
      throw new Error(`No readable channel posts matched Discord's search for ${session.connection.searchTerms.map(term => `"${term}"`).join(", ")}. Nothing was imported.`);
    }
    await deliverSnapshot(session, limitReached(session) ? "limit-reached"
      : finishing(session) ? "finished-early" : incomplete || session.manualSearch ? "stalled" : "history-ended");
  } catch (error) {
    await failCapture(session, error);
  }
}

// Search-mode network handling: only the source server's search responses count; ordinary channel history is ignored.
function handleSearchEvent(session, method, params) {
  if (method === "Network.requestWillBeSent") {
    if (!isSearchRequest(params?.request?.url, params?.request?.method, session.connection.guildId, params?.type)) return;
    if (params.redirectResponse || session.pending.size >= 32) {
      void stopSession(session, "error", "Discord search requests changed unexpectedly. Capture stopped.");
      return;
    }
    session.lastHistoryActivityAt = Date.now();
    session.pending.set(params.requestId, { offset: searchOffset(params.request.url), responseReady: false });
  } else if (method === "Network.responseReceived") {
    const request = session.pending.get(params?.requestId);
    if (!request) return;
    session.lastHistoryActivityAt = Date.now();
    const statusCode = params?.response?.status;
    if ([401, 403, 429].includes(statusCode)) {
      void stopSession(session, "error", statusCode === 429 ? "Discord rate-limited search. Capture stopped; no retry or incomplete import was attempted." : "Discord denied search access. Capture stopped.");
      return;
    }
    if (statusCode === 202) {
      // Discord is still indexing the server; its own page retries, so just wait for the real answer.
      session.pending.delete(params.requestId);
      show(session, "waiting", "Discord is still indexing this server for search. Waiting for its results.");
      return;
    }
    if (statusCode !== 200 || !isSearchRequest(params?.response?.url, "GET", session.connection.guildId, params?.type)) {
      void stopSession(session, "error", "Discord search returned an unexpected response. Capture stopped.");
      return;
    }
    request.responseReady = true;
  } else if (method === "Network.loadingFailed" && session.pending.has(params?.requestId)) {
    // Discord cancels an in-flight search when a newer one starts; anything else is a real failure.
    if (params?.canceled === true) { session.pending.delete(params.requestId); return; }
    void stopSession(session, "error", "Discord search loading failed. Capture stopped without importing an incomplete batch.");
  } else if (method === "Network.loadingFinished") {
    const request = session.pending.get(params?.requestId);
    if (!request || !request.responseReady) return;
    session.lastHistoryActivityAt = Date.now();
    session.pending.delete(params.requestId);
    session.processing++;
    session.bodyQueue = session.bodyQueue.then(async () => {
      if (!alive(session) || session.sending) return;
      const result = await chrome.debugger.sendCommand({ tabId: session.tabId }, "Network.getResponseBody", { requestId: params.requestId });
      if (!alive(session) || session.sending) return;
      const page = decodeSearchBody(result, session.connection.channelId);
      session.history.addBatch(page.hits);
      const last = page.groups === 0 || (page.total === null ? page.groups < SEARCH_PAGE_SIZE : request.offset + page.groups >= page.total);
      session.lastSearchPage = { offset: request.offset, groups: page.groups, hits: page.hits.length, total: page.total, last };
      session.searchEmptyPages = page.hits.length === 0 ? session.searchEmptyPages + 1 : 0;
      session.searchResponses++;
      session.lastSearchAt = Date.now();
      show(session, "collecting", `Discord search found ${session.history.messages.size.toLocaleString()} of up to ${session.connection.messageLimit.toLocaleString()} posts so far.`);
    }).catch(() => stopSession(session, "error", "Discord search results could not be read or exceeded the capture bounds. No incomplete snapshot was imported."))
      .finally(() => {
        session.processing--;
        if (alive(session)) session.lastHistoryActivityAt = Date.now();
      });
  }
}

async function startCapture(connectionText) {
  if (current) throw new Error("Stop the current capture before starting another.");
  const connection = parseConnection(connectionText);
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!Number.isInteger(tab?.id) || !matchesChannelTab(tab.url, connection)) throw new Error("Select the configured Discord source channel tab before pressing Start.");
  const session = {
    connection, tabId: tab.id, history: new CaptureHistory(connection), pending: new Map(), processing: 0,
    fetchControllers: new Set(), deadline: Date.now() + MAX_DURATION_MS, attached: false, cancelled: false,
    historyEnded: false, sending: false, bodyQueue: Promise.resolve(), firstBodyAt: null,
    lastHistoryActivityAt: Date.now(), lastScrollAt: null, lastReceiverCheckAt: 0,
    search: connection.searchTerms.length > 0, searchResponses: 0, searchEmptyPages: 0, lastSearchPage: null, lastSearchAt: 0,
    finishRequested: false, manualSearch: false
  };
  current = session;
  show(session, "connecting", "Checking KathanaBot's local receiver.");
  try {
    await readyReceiver(session);
    await chrome.debugger.attach({ tabId: tab.id }, "1.3");
    if (!alive(session)) { session.attached = true; await detach(session); return; }
    session.attached = true;
    await chrome.debugger.sendCommand({ tabId: tab.id }, "Network.enable", { maxTotalBufferSize: 32 * 1024 * 1024, maxResourceBufferSize: 4 * 1024 * 1024 });
    await chrome.debugger.sendCommand({ tabId: tab.id }, "Page.enable");
    show(session, "loading", session.search
      ? "Reloading the selected channel, then using Discord's own search box for your items."
      : "Reloading the selected channel to capture normal Discord history responses.");
    // A focused message link can load a middle slice. Navigate to the channel's
    // normal latest view before reloading; both remain the same permitted channel.
    if (tab.url.replace(/\/$/, "") !== connection.sourceUrl) await chrome.tabs.update(tab.id, { url: connection.sourceUrl });
    else await chrome.tabs.reload(tab.id);
    void (session.search ? searchLoop(session) : captureLoop(session));
  } catch {
    await stopSession(session, "error", "Cannot attach to this channel. Close its DevTools, check the KathanaBot receiver, and start again.");
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (sender.id !== chrome.runtime.id || sender.tab || sender.url !== chrome.runtime.getURL("popup.html")) return false;
  if (message?.action === "status") { sendResponse({ ok: true, status }); return false; }
  if (message?.action === "stop") {
    const pending = current ? stopSession(current, "stopped", "Capture stopped. No incomplete snapshot was imported.") : Promise.resolve();
    pending.then(() => sendResponse({ ok: true, status }));
    return true;
  }
  if (message?.action === "finish") {
    // Import now: only a search capture can end early, since its result pages have no natural end if paging is manual.
    if (current?.search && !current.sending) current.finishRequested = true;
    sendResponse({ ok: Boolean(current?.search), status });
    return false;
  }
  if (message?.action === "start") {
    startCapture(message.connection).then(() => sendResponse({ ok: true, status })).catch(error => sendResponse({ ok: false, error: error.message }));
    return true;
  }
  return false;
});

chrome.debugger.onEvent.addListener((source, method, params) => {
  const session = current;
  if (!session || source.tabId !== session.tabId || source.sessionId || !alive(session)) return;
  if (method === "Page.frameNavigated" && !params?.frame?.parentId && !matchesChannelTab(params?.frame?.url, session.connection) ||
      method === "Page.navigatedWithinDocument" && !matchesChannelTab(params?.url, session.connection)) {
    void stopSession(session, "error", "The selected tab left the source channel. Capture stopped.");
    return;
  }
  if (session.sending) return;
  if (session.search) { handleSearchEvent(session, method, params); return; }
  if (method === "Network.requestWillBeSent") {
    // Deliberately access only URL/method/type, never headers, cookies or postData.
    if (!isMessageRequest(params?.request?.url, params?.request?.method, session.connection.channelId, params?.type)) return;
    if (params.redirectResponse || session.pending.size >= 128) {
      void stopSession(session, "error", "Discord history requests changed unexpectedly. Capture stopped.");
      return;
    }
    const before = new URL(params.request.url).searchParams.get("before");
    session.lastHistoryActivityAt = Date.now();
    session.pending.set(params.requestId, { before: isSnowflake(before) ? before : null, responseReady: false });
  } else if (method === "Network.responseReceived") {
    const request = session.pending.get(params?.requestId);
    if (!request) return;
    session.lastHistoryActivityAt = Date.now();
    const statusCode = params?.response?.status;
    if ([401, 403, 429].includes(statusCode)) {
      void stopSession(session, "error", statusCode === 429 ? "Discord rate-limited history loading. Capture stopped; no retry or incomplete import was attempted." : "Discord denied channel history access. Capture stopped.");
      return;
    }
    if (statusCode !== 200 || !isMessageRequest(params?.response?.url, "GET", session.connection.channelId, params?.type)) {
      void stopSession(session, "error", "Discord history returned an unexpected response. Capture stopped.");
      return;
    }
    request.responseReady = true;
  } else if (method === "Network.loadingFailed" && session.pending.has(params?.requestId)) {
    void stopSession(session, "error", "Discord history loading failed. Capture stopped without importing an incomplete batch.");
  } else if (method === "Network.loadingFinished") {
    const request = session.pending.get(params?.requestId);
    if (!request || !request.responseReady) return;
    session.lastHistoryActivityAt = Date.now();
    session.pending.delete(params.requestId);
    session.processing++;
    session.bodyQueue = session.bodyQueue.then(async () => {
      if (!alive(session) || session.sending) return;
      const result = await chrome.debugger.sendCommand({ tabId: session.tabId }, "Network.getResponseBody", { requestId: params.requestId });
      if (!alive(session) || session.sending) return;
      const batch = decodeNetworkBody(result);
      session.history.addBatch(batch);
      if (session.firstBodyAt === null) session.firstBodyAt = Date.now();
      if (request.before && batch.length === 0) session.historyEnded = true;
      show(session, "collecting", `Captured ${session.history.messages.size.toLocaleString()} of ${session.connection.messageLimit.toLocaleString()} posts.`);
    }).catch(() => stopSession(session, "error", "Discord history could not be read or exceeded the capture bounds. No incomplete snapshot was imported."))
      .finally(() => {
        session.processing--;
        if (alive(session)) session.lastHistoryActivityAt = Date.now();
      });
  }
});

chrome.debugger.onDetach.addListener(source => {
  if (current && source.tabId === current.tabId) void stopSession(current, "stopped", "Browser debugging detached. Capture stopped without importing an incomplete snapshot.");
});
chrome.tabs.onUpdated.addListener((tabId, change) => {
  if (current && tabId === current.tabId && change.url && !matchesChannelTab(change.url, current.connection)) {
    void stopSession(current, "error", "The selected tab left the source channel. Capture stopped.");
  }
});
chrome.tabs.onRemoved.addListener(tabId => {
  if (current && tabId === current.tabId) void stopSession(current, "stopped", "The selected Discord tab closed. Capture stopped.");
});
