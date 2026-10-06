import test from "node:test";
import assert from "node:assert/strict";
import { CaptureHistory, parseConnection, parseSourceUrl, matchesChannelTab, isMessageRequest, isSnowflake, sanitizeMessage, decodeNetworkBody, MAX_RESPONSE_CHARACTERS } from "../core.mjs";

const guild = "1363839027114934315";
const channel = "1486808135883555037";
const rawConnection = {
  format: "KathanaCaptureConnection", version: 1,
  receiverUrl: `http://127.0.0.1:43123/kathana-capture/${"AB".repeat(32)}/`,
  sourceUrl: `https://discord.com/channels/${guild}/${channel}`, messageLimit: 10000
};
const connect = (changes = {}) => parseConnection(JSON.stringify({ ...rawConnection, ...changes }));
const post = (id, changes = {}) => ({ id: String(id), channel_id: channel, type: 0, content: "S> Tikoy PM me", timestamp: "2026-10-05T23:00:00Z", edited_timestamp: null, author: { id: "1486810317966282916", username: "YONEX", bot: true }, ...changes });

test("exact channel links and selected tabs cannot escape host, guild or channel", () => {
  assert.equal(parseSourceUrl(rawConnection.sourceUrl + "/1556808682858086542").channelId, channel);
  for (const bad of [
    rawConnection.sourceUrl.replace("discord.com", "discord.com.evil.test"),
    rawConnection.sourceUrl.replace("https:", "http:"),
    rawConnection.sourceUrl.replace("discord.com", "user:secret@discord.com"),
    rawConnection.sourceUrl + "?token=secret", rawConnection.sourceUrl + "#other", "https://discord.gg/kathana",
    rawConnection.sourceUrl.replace(channel, "18446744073709551616")
  ]) assert.throws(() => parseSourceUrl(bad));
  assert.equal(matchesChannelTab(rawConnection.sourceUrl, connect()), true);
  assert.equal(matchesChannelTab(rawConnection.sourceUrl.replace(guild, "1363839027114934316"), connect()), false);
  assert.equal(matchesChannelTab(rawConnection.sourceUrl + "8", connect()), false);
});

test("receiver connections enforce literal loopback, nonce, schema and selected bounds", () => {
  assert.equal(connect().captureId.length, 64);
  assert.equal(connect({ messageLimit: 1 }).messageLimit, 1);
  for (const messageLimit of [0, 10001, 1.5, "100", null]) assert.throws(() => connect({ messageLimit }));
  for (const receiverUrl of [
    rawConnection.receiverUrl.replace("127.0.0.1", "localhost"),
    rawConnection.receiverUrl.replace("127.0.0.1", "10.0.0.1"),
    rawConnection.receiverUrl.replace("http:", "https:"),
    rawConnection.receiverUrl.replace("127.0.0.1", "user:secret@127.0.0.1"),
    rawConnection.receiverUrl + "capture", rawConnection.receiverUrl + "?extra=1",
    rawConnection.receiverUrl.replace("AB".repeat(32), "short")
  ]) assert.throws(() => connect({ receiverUrl }));
  assert.throws(() => connect({ format: "other" }));
  assert.throws(() => parseConnection("x".repeat(8193)));
  assert.equal(isSnowflake("18446744073709551615"), true);
  assert.equal(isSnowflake("18446744073709551616"), false);
  assert.equal(isSnowflake("0001"), false);
});

test("only normal GET XHR/Fetch messages endpoints for the exact channel qualify", () => {
  const url = `https://discord.com/api/v10/channels/${channel}/messages?before=1556808682858086542&limit=100`;
  assert.equal(isMessageRequest(url, "GET", channel, "Fetch"), true);
  assert.equal(isMessageRequest(url, "GET", channel, "XHR"), true);
  for (const [bad, method, type] of [
    [url, "POST", "Fetch"], [url, "GET", "Document"],
    [url.replace(channel, "1486808135883555038"), "GET", "Fetch"],
    [url.replace("/messages?", "/messages/123?"), "GET", "Fetch"],
    [url.replace("discord.com", "discord.com.evil.test"), "GET", "Fetch"],
    [url.replace("discord.com", "user:secret@discord.com"), "GET", "Fetch"],
    [url.replace("https:", "http:"), "GET", "Fetch"], [url + "#fragment", "GET", "Fetch"]
  ]) assert.equal(isMessageRequest(bad, method, channel, type), false);
});

test("sanitization preserves trade text and drops all unrelated data", () => {
  const input = post("1556808682858086542", {
    content: "", flags: 0, webhook_id: "1486810317966282916",
    headers: { Authorization: "never-forward" }, token: "never-forward", mentions: [{ username: "unused" }],
    author: { id: "1486810317966282916", username: "YONEX", bot: true, avatar: "unused", token: "never-forward" },
    embeds: [{ title: "BUY DARK XTRAL PM YOUR PRICE", description: "trade", url: "https://unused.test", author: { name: "IGN", icon_url: "unused" }, fields: [{ name: "Item", value: "Tikoy", inline: true }], footer: { text: "contact", icon_url: "unused" }, image: { url: "unused" } }],
    attachments: [{ filename: "trade.png", url: "https://unused.test/file", proxy_url: "unused", size: 5 }]
  });
  const result = sanitizeMessage(input, channel);
  assert.equal(result.author.username, "YONEX");
  assert.equal(result.embeds[0].title, "BUY DARK XTRAL PM YOUR PRICE");
  assert.deepEqual(result.embeds[0].fields, [{ name: "Item", value: "Tikoy" }]);
  assert.deepEqual(result.attachments, [{ filename: "trade.png" }]);
  const encoded = JSON.stringify(result);
  assert.equal(encoded.includes("never-forward"), false);
  assert.equal(encoded.includes("unused"), false);
  assert.throws(() => sanitizeMessage({ ...input, channel_id: "1486808135883555038" }, channel));
  assert.throws(() => sanitizeMessage(post("1556808682858086542", { content: "x".repeat(65537) }), channel));
  assert.throws(() => sanitizeMessage(post("1556808682858086542", { embeds: Array(11).fill({}) }), channel));
});

test("normal and base64 UTF-8 bodies are decoded within byte bounds", () => {
  const batch = [post("1556808682858086542", { content: "買い Tikoy 🦊" })];
  const text = JSON.stringify(batch);
  assert.deepEqual(decodeNetworkBody({ body: text, base64Encoded: false }), batch);
  assert.deepEqual(decodeNetworkBody({ body: Buffer.from(text).toString("base64"), base64Encoded: true }), batch);
  assert.throws(() => decodeNetworkBody({ body: "not json", base64Encoded: false }));
  assert.throws(() => decodeNetworkBody({ body: "!", base64Encoded: true }));
  assert.throws(() => decodeNetworkBody({ body: "/w==", base64Encoded: true }));
  assert.throws(() => decodeNetworkBody({ body: "x".repeat(MAX_RESPONSE_CHARACTERS + 1), base64Encoded: false }));
  assert.throws(() => decodeNetworkBody({ body: "買".repeat(Math.ceil(MAX_RESPONSE_CHARACTERS / 3)), base64Encoded: false }));
  assert.throws(() => decodeNetworkBody({ body: Buffer.alloc(MAX_RESPONSE_CHARACTERS + 1).toString("base64"), base64Encoded: true }));
  assert.throws(() => decodeNetworkBody({ body: JSON.stringify(Array(101).fill(batch[0])), base64Encoded: false }));
});

test("overlaps deduplicate numeric snowflakes and keep the latest edit", () => {
  const history = new CaptureHistory(connect({ messageLimit: 3 }));
  history.addBatch([post("1556808682858086542"), post("1556808388766212207")]);
  history.addBatch([post("1556808682858086542", { content: "edited", edited_timestamp: "2026-10-05T23:10:00Z" }), post("1556808601228681320")]);
  history.addBatch([post("1556808682858086542", { content: "stale", edited_timestamp: "2026-10-05T23:05:00Z" }), post("1556808388766212206")]);
  const envelope = history.envelope(false, "stalled");
  assert.equal(envelope.messages.length, 3);
  assert.deepEqual(envelope.messages.map(x => x.id), ["1556808682858086542", "1556808601228681320", "1556808388766212207"]);
  assert.equal(envelope.messages[0].content, "edited");
  assert.equal(envelope.historyExhausted, false);
  assert.equal(envelope.captureStatus, "stalled");
  const before = JSON.stringify(envelope.messages);
  assert.throws(() => history.addBatch([post("1556808388766212205"), post("1556808388766212204", { channel_id: "1486808135883555038" })]));
  assert.equal(JSON.stringify(history.envelope(false).messages), before, "failed batch cannot mutate the retained snapshot");
});

test("10,000 distinct posts are retained across 100 batches without truncating history", () => {
  const history = new CaptureHistory(connect());
  const newest = 1556808682858086542n;
  for (let page = 0; page < 100; page++) history.addBatch(Array.from({ length: 100 }, (_, index) => post(newest - BigInt(page * 100 + index))));
  assert.equal(history.messages.size, 10000);
  assert.equal(history.responses, 100);
  assert.equal(history.envelope(false).messages.at(-1).id, String(newest - 9999n));
  history.addBatch([post(newest + 1n)]);
  assert.equal(history.messages.size, 10000);
  assert.equal(history.envelope(false).messages[0].id, String(newest + 1n));
  assert.equal(history.envelope(true).historyExhausted, true);
});

test("aggregate payload bounds reject an oversized batch atomically and fit the 64MiB bridge", () => {
  const history = new CaptureHistory(connect());
  const newest = 1556808682858086542n;
  const batch = page => Array.from({ length: 100 }, (_, index) => post(newest - BigInt(page * 100 + index), { content: "買".repeat(65000) }));
  for (let page = 0; page < 3; page++) history.addBatch(batch(page));
  assert.equal(history.messages.size, 300);
  assert.ok(new TextEncoder().encode(JSON.stringify(history.envelope(false, "stalled"))).byteLength < 64 * 1024 * 1024);
  assert.throws(() => history.addBatch(batch(3)), /too large/);
  assert.equal(history.messages.size, 300);
});
