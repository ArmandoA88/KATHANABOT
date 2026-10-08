import test from "node:test";
import assert from "node:assert/strict";

const guild = "1363839027114934315";
const channel = "1486808135883555037";
const url = `https://discord.com/channels/${guild}/${channel}`;
const api = `https://discord.com/api/v10/channels/${channel}/messages?limit=100`;
const connection = count => JSON.stringify({ format: "KathanaCaptureConnection", version: 1, receiverUrl: `http://127.0.0.1:43123/kathana-capture/${"AB".repeat(32)}/`, sourceUrl: url, messageLimit: count });
const message = (id = "1556808682858086542") => ({ id, channel_id: channel, type: 0, content: "S> Tikoy", flags: 0, timestamp: "2026-10-05T23:00:00Z", author: { id: "1486810317966282916", username: "YONEX", bot: true, credential: "must-not-forward" }, headers: { Authorization: "must-not-forward" } });
const listeners = {};
function event(name) { return { addListener(callback) { listeners[name] = callback; } }; }
let fixture;
let serial = 0;
let timerClock;
async function flush() { for (let index = 0; index < 30; index++) await Promise.resolve(); }
async function sleep(milliseconds) {
  await flush();
  for (let elapsed = 0; elapsed < milliseconds; elapsed += 250) {
    timerClock.tick(Math.min(250, milliseconds - elapsed));
    await flush();
  }
}
function reset(count = 2) {
  fixture = { count, url, requests: [], commands: [], bodies: new Map(), posts: [], attach: 0, detach: 0, scripts: 0, scriptTimes: [], scriptNames: [], scriptByName: null, onCommand: null, stateTimes: [], headersRead: 0, paused: false, loseReceiver: false, stateReads: 0, initialBatch: null, base64: false, conflictCount: 0, scriptResult: { found: false, reason: "no-observed-chat-scroller" } };
}
function emitRequest({ requestUrl = api, method = "GET", status = 200, body = [message()], type = "Fetch", tabId = 11, before = false, base64 = false, requestOnly = false } = {}) {
  const requestId = `mock-${++serial}`;
  const actualUrl = requestUrl + (before ? "&before=1556808682858086542" : "");
  const request = { url: actualUrl, method };
  Object.defineProperty(request, "headers", { get() { fixture.headersRead++; throw new Error("Request headers must never be accessed."); } });
  const response = { url: actualUrl, status };
  Object.defineProperty(response, "headers", { get() { fixture.headersRead++; throw new Error("Response headers must never be accessed."); } });
  const bodyText = JSON.stringify(body);
  fixture.bodies.set(requestId, { body: base64 ? Buffer.from(bodyText).toString("base64") : bodyText, base64Encoded: base64 });
  listeners.debuggerEvent({ tabId }, "Network.requestWillBeSent", { requestId, request, type });
  if (requestOnly) return { requestId, response, type, tabId };
  listeners.debuggerEvent({ tabId }, "Network.responseReceived", { requestId, response, type });
  listeners.debuggerEvent({ tabId }, "Network.loadingFinished", { requestId });
  return { requestId, response, type, tabId };
}

const searchUrl = (offset = 0) => `https://discord.com/api/v9/guilds/${guild}/messages/search?content=ror%20asura&offset=${offset}`;
const hitId = index => String(1556808682858086542n - BigInt(index));
const hit = (id, changes = {}) => ({ ...message(id), hit: true, ...changes });
const searchConnection = (count, terms) => JSON.stringify({ ...JSON.parse(connection(count)), searchTerms: terms });
function emitSearch({ offset = 0, groups = [], total = groups.length, status = 200, tabId } = {}) {
  return emitRequest({ requestUrl: searchUrl(offset), status, body: { messages: groups, total_results: total }, tabId });
}
const pressedEnter = (method, args) => method === "Input.dispatchKeyEvent" && args.type === "keyDown" && args.key === "Enter";

function finishRequest(request) {
  listeners.debuggerEvent({ tabId: request.tabId }, "Network.responseReceived", request);
  listeners.debuggerEvent({ tabId: request.tabId }, "Network.loadingFinished", { requestId: request.requestId });
}

globalThis.chrome = {
  runtime: { id: "a".repeat(32), getURL: file => `chrome-extension://${"a".repeat(32)}/${file}`, onMessage: event("message") },
  debugger: {
    onEvent: event("debuggerEvent"), onDetach: event("detach"),
    async attach() { fixture.attach++; }, async detach() { fixture.detach++; },
    async sendCommand(target, method, args) {
      fixture.commands.push({ target, method, args });
      if (method === "Network.getResponseBody") return fixture.bodies.get(args.requestId);
      if (fixture.onCommand) fixture.onCommand(method, args);
      return {};
    }
  },
  tabs: {
    onUpdated: event("updated"), onRemoved: event("removed"),
    async query() { return [{ id: 11, url: fixture.url }]; },
    async get() { return { id: 11, url: fixture.url }; },
    async reload() { if (fixture.initialBatch !== null) emitRequest({ body: fixture.initialBatch, base64: fixture.base64 }); },
    async update(tabId, change) { fixture.url = change.url; if (fixture.initialBatch !== null) emitRequest({ body: fixture.initialBatch }); }
  },
  scripting: {
    async executeScript(injection) {
      fixture.scripts++;
      fixture.scriptTimes.push(Date.now());
      fixture.scriptNames.push(injection.func.name);
      if (fixture.onScript) fixture.onScript(injection.func.name);
      return [{ result: fixture.scriptByName?.[injection.func.name] ?? fixture.scriptResult }];
    }
  }
};
globalThis.fetch = async (requestUrl, options) => {
  assert.match(requestUrl, /^http:\/\/127\.0\.0\.1:43123\/kathana-capture\/[A-F0-9]{64}\/(state|capture)$/);
  assert.equal(options.credentials, "omit");
  assert.equal(options.redirect, "error");
  fixture.requests.push(requestUrl);
  if (requestUrl.endsWith("state")) {
    fixture.stateReads++;
    fixture.stateTimes.push(Date.now());
    if (fixture.loseReceiver && fixture.stateReads > 1) throw new Error("private server details");
    return { status: 200, async text() { return JSON.stringify({ active: true, paused: fixture.paused, messageLimit: fixture.count, sourceChannelId: channel }); } };
  }
  fixture.posts.push(options.body);
  if (fixture.conflictCount-- > 0) return { status: 409, async text() { return "{}"; } };
  return { status: 200, async text() { return JSON.stringify({ imported: true, messageCount: JSON.parse(options.body).messages.length }); } };
};

await import("../worker.mjs");
const sender = { id: chrome.runtime.id, url: chrome.runtime.getURL("popup.html") };
function command(action, extra = {}) { return new Promise(resolve => listeners.message({ action, ...extra }, sender, resolve)); }
async function state() { return (await command("status")).status; }
async function until(predicate, timeout = 5000) {
  const end = Date.now() + timeout;
  while (!predicate() && Date.now() < end) await sleep(250);
  assert.equal(predicate(), true, "expected mocked worker condition before timeout");
}
async function stopped() { await until(() => fixture.detach > 0); await sleep(20); }
async function stoppedLater() { await until(() => fixture.detach > 0, 60000); await sleep(20); }

test("mock worker enforces selected tab, event privacy, stop and receiver flow", async t => {
  // Node's documented Date/setTimeout mocks advance the production scheduler
  // without waiting real seconds or adding production-only test switches.
  t.mock.timers.enable({ apis: ["setTimeout", "Date"], now: Date.parse("2026-10-05T23:00:00Z") });
  timerClock = t.mock.timers;
  t.beforeEach(async () => { await command("stop"); await flush(); });
  await t.test("wrong channel and non-popup messages cannot start a debugger", async () => {
    reset();
    fixture.url = url + "8";
    assert.equal((await command("start", { connection: connection(2) })).ok, false);
    assert.equal(fixture.attach, 0);
    assert.equal(fixture.requests.length, 0);
    assert.equal(listeners.message({ action: "start", connection: connection(2) }, { ...sender, tab: { id: 11 } }, () => assert.fail()), false);
  });

  await t.test("unrelated bodies are ignored, header access is absent, Stop sends no partial import", async () => {
    reset(5);
    await command("start", { connection: connection(5) });
    emitRequest({ requestUrl: api.replace(channel, "1486808135883555038") });
    emitRequest({ method: "POST" });
    emitRequest({ type: "Document" });
    emitRequest({ tabId: 12 });
    assert.equal(fixture.commands.filter(x => x.method === "Network.getResponseBody").length, 0);
    emitRequest({ body: [message()], base64: true });
    await sleep(30);
    assert.equal((await state()).count, 1);
    assert.equal(fixture.headersRead, 0);
    await command("stop");
    assert.equal(fixture.posts.length, 0);
    assert.equal(fixture.detach, 1);
  });

  await t.test("navigation aborts before any captured snapshot is posted", async () => {
    reset();
    await command("start", { connection: connection(2) });
    listeners.updated(11, { url: url + "8" });
    await until(() => fixture.detach > 0, 30000);
    assert.equal((await state()).phase, "error");
    assert.equal(fixture.posts.length, 0);
  });

  for (const status of [401, 403, 429]) {
    await t.test(`${status} stops without API replay, retry, or partial import`, async () => {
      reset();
      await command("start", { connection: connection(2) });
      emitRequest({ status });
      await stopped();
      assert.equal(fixture.posts.length, 0);
      assert.equal(fixture.commands.filter(x => x.method === "Network.getResponseBody").length, 0);
      assert.equal((await state()).phase, "error");
    });
  }

  await t.test("receiver loss detaches without sending a snapshot", async () => {
    reset();
    fixture.loseReceiver = true;
    await command("start", { connection: connection(2) });
    await stopped();
    assert.equal(fixture.posts.length, 0);
    assert.match((await state()).message, /receiver is unavailable/);
  });

  await t.test("reaching the count posts only a sanitized, exact-channel envelope", async () => {
    reset(1);
    fixture.initialBatch = [message()];
    fixture.base64 = true;
    await command("start", { connection: connection(1) });
    await stopped();
    assert.equal(fixture.posts.length, 1);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(envelope.captureStatus, "limit-reached");
    assert.equal(envelope.historyExhausted, false);
    assert.equal(envelope.sourceChannelId, channel);
    assert.equal(envelope.requestedMessageCount, 1);
    assert.equal(envelope.messages.length, 1);
    assert.equal(fixture.posts[0].includes("must-not-forward"), false);
    assert.equal(fixture.headersRead, 0);
    assert.equal((await state()).phase, "complete");
  });

  await t.test("a reviewed queue pause keeps the snapshot and prevents posting until resumed", async () => {
    reset(1);
    fixture.initialBatch = [message()];
    fixture.onScript = () => assert.fail("selected count reached: scrolling is unnecessary");
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async (...args) => {
      if (fixture.stateReads === 1) fixture.paused = true;
      return originalFetch(...args);
    };
    await command("start", { connection: connection(1) });
    await sleep(500);
    assert.equal(fixture.posts.length, 0);
    assert.equal((await state()).phase, "paused");
    globalThis.fetch = originalFetch;
    fixture.paused = false;
    await stopped();
    assert.equal(fixture.posts.length, 1);
  });

  await t.test("a local 409 defers the same frozen snapshot rather than overwriting", async () => {
    reset(1);
    fixture.initialBatch = [message()];
    fixture.conflictCount = 1;
    await command("start", { connection: connection(1) });
    await stopped();
    assert.equal(fixture.posts.length, 2);
    assert.equal(fixture.posts[0], fixture.posts[1]);
    assert.equal((await state()).phase, "complete");
  });

  await t.test("rendering race retries the proven scroller and an older empty response establishes history end", async () => {
    reset(100);
    fixture.initialBatch = [message()];
    fixture.onScript = () => {
      if (fixture.scripts >= 2) {
        fixture.scriptResult = { found: true };
        emitRequest({ body: [], before: true });
      }
    };
    await command("start", { connection: connection(100) });
    await until(() => fixture.detach > 0, 30000);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(fixture.scripts, 2);
    assert.equal(envelope.messages.length, 1);
    assert.equal(envelope.historyExhausted, true);
    assert.equal(envelope.captureStatus, "history-ended");
  });

  await t.test("no identifiable scroller imports a partial stalled snapshot after a bounded render wait", async () => {
    reset(100);
    fixture.initialBatch = [message()];
    await command("start", { connection: connection(100) });
    await until(() => fixture.detach > 0, 35000);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(envelope.messages.length, 1);
    assert.equal(envelope.historyExhausted, false);
    assert.equal(envelope.captureStatus, "stalled");
    assert.match((await state()).message, /does not prove history ended/);
    assert.ok(fixture.scripts >= 2 && fixture.scripts < 100);
  });

  await t.test("immediate responses still leave five seconds initially and ten seconds between scrolls", async () => {
    reset(3);
    fixture.initialBatch = [message()];
    fixture.scriptResult = { found: true };
    const began = Date.now();
    fixture.onScript = () => emitRequest({ body: [message(String(1556808682858086542n - BigInt(fixture.scripts)))] });
    await command("start", { connection: connection(3) });
    await until(() => fixture.detach > 0, 30000);
    assert.equal(fixture.scripts, 2);
    assert.ok(fixture.scriptTimes[0] - began >= 5000);
    assert.ok(fixture.scriptTimes[1] - fixture.scriptTimes[0] >= 10000);
    const gaps = fixture.stateTimes.slice(1).map((time, index) => time - fixture.stateTimes[index]);
    assert.ok(gaps.every(gap => gap <= 2000), "receiver remains polled during pacing waits");
    assert.equal(JSON.parse(fixture.posts[0]).messages.length, 3);
  });

  await t.test("a revision change cannot skip another pending request or its three-second quiet period", async () => {
    reset(4);
    fixture.initialBatch = [message()];
    fixture.scriptResult = { found: true };
    let held;
    fixture.onScript = () => {
      if (fixture.scripts === 1) {
        emitRequest({ body: [message("1556808682858086541")] });
        held = emitRequest({ body: [message("1556808682858086540")], requestOnly: true });
      } else emitRequest({ body: [message("1556808682858086539")] });
    };
    await command("start", { connection: connection(4) });
    await until(() => fixture.scripts === 1, 10000);
    await sleep(12000);
    assert.equal(fixture.scripts, 1);
    assert.equal(fixture.posts.length, 0);
    assert.equal((await state()).running, true);
    const finished = Date.now();
    finishRequest(held);
    await sleep(2750);
    assert.equal(fixture.scripts, 1);
    await until(() => fixture.detach > 0, 5000);
    assert.equal(fixture.scripts, 2);
    assert.ok(fixture.scriptTimes[1] - finished >= 3000);
    assert.ok(fixture.scriptTimes[1] - fixture.scriptTimes[0] >= 10000);
    assert.equal(JSON.parse(fixture.posts[0]).messages.length, 4);
    assert.equal((await state()).phase, "complete");
  });

  await t.test("rendering and history loads beyond the former seven-second ceiling succeed", async () => {
    reset(2);
    fixture.initialBatch = [message()];
    const began = Date.now();
    let loadScheduled = false;
    let actualScrollAt;
    fixture.onScript = () => {
      if (Date.now() - began < 13000) return;
      fixture.scriptResult = { found: true };
      if (!loadScheduled) {
        actualScrollAt = Date.now();
        loadScheduled = true;
        const held = emitRequest({ body: [message("1556808682858086541")], requestOnly: true });
        setTimeout(() => finishRequest(held), 12000);
      }
    };
    await command("start", { connection: connection(2) });
    await until(() => fixture.detach > 0, 40000);
    assert.ok(actualScrollAt - began >= 13000);
    assert.ok(Date.now() - actualScrollAt >= 12000);
    assert.equal(JSON.parse(fixture.posts[0]).messages.length, 2);
    assert.equal((await state()).phase, "complete");
  });

  await t.test("Stop during the new pacing wait prevents further scrolls and snapshot delivery", async () => {
    reset(3);
    fixture.initialBatch = [message()];
    fixture.scriptResult = { found: true };
    fixture.onScript = () => emitRequest({ body: [message("1556808682858086541")] });
    await command("start", { connection: connection(3) });
    await until(() => fixture.scripts === 1, 10000);
    await sleep(1000);
    await command("stop");
    await sleep(20000);
    assert.equal(fixture.scripts, 1);
    assert.equal(fixture.posts.length, 0);
    assert.equal(fixture.detach, 1);
    assert.equal((await state()).phase, "stopped");
  });

  await t.test("history starting during the final receiver check must finish and become quiet before scrolling", async () => {
    reset(2);
    fixture.initialBatch = [message()];
    fixture.scriptResult = { found: true };
    fixture.onScript = () => emitRequest({ body: [message("1556808682858086540")] });
    const began = Date.now();
    const previousFetch = globalThis.fetch;
    let held;
    globalThis.fetch = async (...args) => {
      if (args[0].endsWith("state") && !held && fixture.scripts === 0 && Date.now() - began >= 5000) {
        held = emitRequest({ body: [message()], requestOnly: true });
      }
      return previousFetch(...args);
    };
    try {
      await command("start", { connection: connection(2) });
      await until(() => Boolean(held), 10000);
      await sleep(6000);
      assert.equal(fixture.scripts, 0, "the receiver HTTP await cannot bypass new pending history");
      const finished = Date.now();
      finishRequest(held);
      await sleep(2750);
      assert.equal(fixture.scripts, 0);
      await until(() => fixture.detach > 0, 5000);
      assert.equal(fixture.scripts, 1);
      assert.ok(fixture.scriptTimes[0] - finished >= 3000);
      assert.equal((await state()).phase, "complete");
    } finally { globalThis.fetch = previousFetch; }
  });

  // ---- Search mode: Discord's own search box filters the channel and its result pages are captured. ----
  await t.test("search mode types the item into Discord's search box and imports only source-channel hits", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    fixture.onCommand = (method, args) => {
      if (pressedEnter(method, args)) emitSearch({ groups: [
        [hit(hitId(0))],
        [hit(hitId(1), { channel_id: "1486808135883555038" })],
        [message(hitId(2)), hit(hitId(3))]
      ] });
    };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    // Ordinary channel history is ignored in search mode, whatever Discord loads while the page settles.
    emitRequest({ body: [message(hitId(50))] });
    await until(() => fixture.detach > 0, 30000);
    assert.deepEqual(fixture.commands.filter(x => x.method === "Input.insertText").map(x => x.args.text), ["ror asura"]);
    const methods = fixture.commands.map(x => x.method).filter(x => x.startsWith("Input."));
    assert.ok(methods.indexOf("Input.insertText") > 0 && methods.lastIndexOf("Input.dispatchKeyEvent") > methods.indexOf("Input.insertText"), "text is typed before Enter");
    assert.equal(fixture.commands.some(x => x.method === "Input.dispatchKeyEvent" && x.args.commands?.includes("selectAll")), true, "previous search text is cleared first");
    const envelope = JSON.parse(fixture.posts[0]);
    assert.deepEqual(envelope.messages.map(x => x.id), [hitId(0), hitId(3)]);
    assert.equal(envelope.captureStatus, "history-ended");
    assert.equal(envelope.historyExhausted, true);
    assert.equal(fixture.posts[0].includes("must-not-forward"), false);
    assert.equal(fixture.headersRead, 0);
    assert.equal(fixture.scriptNames.includes("scrollObservedChat"), false, "search mode never scrolls the chat");
    const finalState = await state();
    assert.equal(finalState.phase, "complete");
    assert.match(finalState.message, /found by Discord search for "ror asura"/);
  });

  await t.test("search result pages are read with the next-page control at least one second apart", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true }, clickNextSearchPage: { found: true } };
    let enterAt = 0;
    fixture.onCommand = (method, args) => {
      if (pressedEnter(method, args)) {
        enterAt = Date.now();
        emitSearch({ offset: 0, total: 30, groups: Array.from({ length: 25 }, (_, index) => [hit(hitId(index))]) });
      }
    };
    fixture.onScript = name => {
      if (name === "clickNextSearchPage") emitSearch({ offset: 25, total: 30, groups: Array.from({ length: 5 }, (_, index) => [hit(hitId(25 + index))]) });
    };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await until(() => fixture.detach > 0, 60000);
    const clicks = fixture.scriptNames.filter(name => name === "clickNextSearchPage").length;
    assert.equal(clicks, 1);
    assert.ok(fixture.scriptTimes[fixture.scriptNames.indexOf("clickNextSearchPage")] - enterAt >= 1000);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(envelope.messages.length, 30);
    assert.equal(envelope.captureStatus, "history-ended");
  });

  await t.test("search mode stops paging once the selected count is reached", async () => {
    reset(5);
    fixture.scriptByName = { focusSearchBox: { found: true }, clickNextSearchPage: { found: true } };
    fixture.onCommand = (method, args) => {
      if (pressedEnter(method, args)) emitSearch({ total: 500, groups: Array.from({ length: 25 }, (_, index) => [hit(hitId(index))]) });
    };
    await command("start", { connection: searchConnection(5, ["ror asura"]) });
    await until(() => fixture.detach > 0, 30000);
    assert.equal(fixture.scriptNames.includes("clickNextSearchPage"), false);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(envelope.messages.length, 5);
    assert.equal(envelope.captureStatus, "limit-reached");
  });

  await t.test("several items are searched one after another, spaced one second apart, and merged without duplicates", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    const enters = [];
    fixture.onCommand = (method, args) => {
      if (!pressedEnter(method, args)) return;
      enters.push(Date.now());
      emitSearch({ groups: enters.length === 1 ? [[hit(hitId(0))], [hit(hitId(1))]] : [[hit(hitId(1))], [hit(hitId(2))]] });
    };
    await command("start", { connection: searchConnection(100, ["ror asura", "d.potra"]) });
    await until(() => fixture.detach > 0, 60000);
    assert.deepEqual(fixture.commands.filter(x => x.method === "Input.insertText").map(x => x.args.text), ["ror asura", "d.potra"]);
    assert.ok(enters[1] - enters[0] >= 1000);
    assert.deepEqual(JSON.parse(fixture.posts[0]).messages.map(x => x.id), [hitId(0), hitId(1), hitId(2)]);
  });

  await t.test("a search that finds nothing imports nothing and says so", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    fixture.onCommand = (method, args) => { if (pressedEnter(method, args)) emitSearch({ groups: [], total: 0 }); };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await stoppedLater();
    assert.equal(fixture.posts.length, 0);
    const finalState = await state();
    assert.equal(finalState.phase, "error");
    assert.match(finalState.message, /^No readable channel posts matched Discord's search for "ror asura"/);
  });

  await t.test("an indexing (202) answer is waited out rather than treated as a result or an error", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    fixture.onCommand = (method, args) => {
      if (!pressedEnter(method, args)) return;
      emitSearch({ status: 202, groups: [] });
      setTimeout(() => emitSearch({ groups: [[hit(hitId(0))]] }), 4000);
    };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await until(() => fixture.detach > 0, 60000);
    assert.equal(JSON.parse(fixture.posts[0]).messages.length, 1);
    assert.equal((await state()).phase, "complete");
  });

  for (const status of [401, 403, 429]) {
    await t.test(`${status} on Discord's search stops without retry or partial import`, async () => {
      reset(100);
      fixture.scriptByName = { focusSearchBox: { found: true } };
      fixture.onCommand = (method, args) => { if (pressedEnter(method, args)) emitSearch({ status, groups: [] }); };
      await command("start", { connection: searchConnection(100, ["ror asura"]) });
      await stoppedLater();
      assert.equal(fixture.posts.length, 0);
      assert.equal((await state()).phase, "error");
    });
  }

  await t.test("search requests for another server or tab are ignored", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    fixture.onCommand = (method, args) => {
      if (!pressedEnter(method, args)) return;
      emitRequest({ requestUrl: searchUrl(0).replace(guild, "1363839027114934316"), body: { messages: [[hit(hitId(9))]], total_results: 1 } });
      emitSearch({ tabId: 12, groups: [[hit(hitId(8))]] });
      emitSearch({ groups: [[hit(hitId(0))]] });
    };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await until(() => fixture.detach > 0, 30000);
    assert.deepEqual(JSON.parse(fixture.posts[0]).messages.map(x => x.id), [hitId(0)]);
  });

  await t.test("a missing search box falls back to manual searching and Import now sends what Discord loaded", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: false, reason: "no-search-box" }, searchBoxFocused: { focused: false } };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await sleep(15000);
    assert.equal(fixture.commands.some(x => x.method === "Input.insertText"), false, "nothing is typed when the box cannot be focused");
    let current = await state();
    assert.equal(current.phase, "search-manual");
    assert.equal(current.canFinish, true);
    assert.match(current.message, /Search in Discord yourself/);
    // The user searches by hand; capture keeps listening.
    emitSearch({ groups: [[hit(hitId(0))], [hit(hitId(1))]] });
    await sleep(500);
    assert.equal((await state()).count, 2);
    assert.equal((await command("finish")).ok, true);
    await stoppedLater();
    const envelope = JSON.parse(fixture.posts[0]);
    assert.deepEqual(envelope.messages.map(x => x.id), [hitId(0), hitId(1)]);
    assert.equal(envelope.captureStatus, "stalled");
    assert.equal(envelope.historyExhausted, false);
    current = await state();
    assert.equal(current.phase, "complete");
    assert.match(current.message, /when you pressed Import now/);
  });

  await t.test("a missing next-page control also falls back to manual paging", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true }, clickNextSearchPage: { found: false, reason: "no-next-button" } };
    fixture.onCommand = (method, args) => {
      if (pressedEnter(method, args)) emitSearch({ total: 60, groups: Array.from({ length: 25 }, (_, index) => [hit(hitId(index))]) });
    };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await sleep(40000);
    assert.equal((await state()).phase, "search-manual");
    // No new results for three minutes: the loaded page is imported as a partial, clearly marked snapshot.
    await until(() => fixture.detach > 0, 200000);
    const envelope = JSON.parse(fixture.posts[0]);
    assert.equal(envelope.messages.length, 25);
    assert.equal(envelope.captureStatus, "stalled");
    assert.match((await state()).message, /result pages stopped loading/);
  });

  await t.test("Import now is refused outside search mode and Stop still discards a search capture", async () => {
    reset(5);
    await command("start", { connection: connection(5) });
    assert.equal((await command("finish")).ok, false);
    await command("stop");
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: true } };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    assert.equal((await state()).canFinish, true);
    await sleep(9000);
    await command("stop");
    await sleep(2000);
    assert.equal(fixture.posts.length, 0);
    assert.equal((await state()).phase, "stopped");
  });

  await t.test("navigating away during a search aborts without importing", async () => {
    reset(100);
    fixture.scriptByName = { focusSearchBox: { found: false, reason: "navigation" } };
    await command("start", { connection: searchConnection(100, ["ror asura"]) });
    await stoppedLater();
    assert.equal(fixture.posts.length, 0);
    assert.equal((await state()).phase, "error");
  });
});
