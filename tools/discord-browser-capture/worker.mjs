import { CaptureHistory, parseConnection, matchesChannelTab, isMessageRequest, isSnowflake, decodeNetworkBody } from "./core.mjs";
import { scrollObservedChat } from "./scroll.mjs";

const MAX_DURATION_MS = 60 * 60 * 1000;
const INITIAL_RENDER_SETTLE_MS = 5000;
const MIN_SCROLL_INTERVAL_MS = 10000;
const NETWORK_QUIET_MS = 3000;
const WAIT_FOR_BATCH_MS = 45000;
const RENDER_RETRY_MS = 20000;
const POLL_MS = 250;
const RECEIVER_POLL_MS = 2000;
const MAX_STALLED_ATTEMPTS = 3;
let current = null;
let status = { running: false, phase: "idle", message: "Copy a browser-capture connection from KathanaBot to begin.", count: 0, requested: 0 };

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
const alive = session => current === session && !session.cancelled;
function show(session, phase, message) {
  if (!alive(session)) return;
  status = { running: true, phase, message, count: session.history.messages.size, requested: session.connection.messageLimit, sourceUrl: session.connection.sourceUrl };
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
    session.sending = true;
    const body = JSON.stringify(session.history.envelope(session.historyEnded, outcome));
    while (alive(session)) {
      await readyReceiver(session);
      show(session, "sending", outcome === "stalled"
        ? `Browser stopped loading older posts. Importing the ${session.history.messages.size.toLocaleString()} captured posts; older history is unverified.`
        : `Importing ${session.history.messages.size.toLocaleString()} captured posts into KathanaBot.`);
      const response = await localRequest(session, "capture", { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body });
      if (response.status === 409) {
        show(session, "paused", "KathanaBot deferred this snapshot. It is kept in memory while the reviewed queue is busy.");
        await waitSlice(session, 1500);
        continue;
      }
      if (response.status !== 200 || response.body?.imported !== true) throw new Error("KathanaBot did not accept the capture. Start a new connection and retry.");
      await stopSession(session, "complete", outcome === "stalled"
        ? `Imported ${session.history.messages.size.toLocaleString()} of ${session.connection.messageLimit.toLocaleString()} requested posts. The browser stalled; this does not prove history ended.`
        : `Imported ${session.history.messages.size.toLocaleString()} posts${session.historyEnded ? "; Discord returned the end of loaded history" : ""}.`);
      return;
    }
  } catch (error) {
    // Chrome errors can include arbitrary site text. Only our controlled messages
    // are exposed; foreign API errors receive a fixed redacted description.
    const message = error?.message;
    const safe = /^(Capture |KathanaBot|This KathanaBot|The selected tab|No readable channel|Could not identify|Discord history)/.test(message || "");
    await stopSession(session, "error", safe ? message : "The browser capture could not continue. Close DevTools and start a new connection.");
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
    lastHistoryActivityAt: Date.now(), lastScrollAt: null, lastReceiverCheckAt: 0
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
    show(session, "loading", "Reloading the selected channel to capture normal Discord history responses.");
    // A focused message link can load a middle slice. Navigate to the channel's
    // normal latest view before reloading; both remain the same permitted channel.
    if (tab.url.replace(/\/$/, "") !== connection.sourceUrl) await chrome.tabs.update(tab.id, { url: connection.sourceUrl });
    else await chrome.tabs.reload(tab.id);
    void captureLoop(session);
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
