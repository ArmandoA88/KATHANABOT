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
  fixture = { count, url, requests: [], commands: [], bodies: new Map(), posts: [], attach: 0, detach: 0, scripts: 0, scriptTimes: [], stateTimes: [], headersRead: 0, paused: false, loseReceiver: false, stateReads: 0, initialBatch: null, base64: false, conflictCount: 0, scriptResult: { found: false, reason: "no-observed-chat-scroller" } };
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
  scripting: { async executeScript() { fixture.scripts++; fixture.scriptTimes.push(Date.now()); if (fixture.onScript) fixture.onScript(); return [{ result: fixture.scriptResult }]; } }
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
});
