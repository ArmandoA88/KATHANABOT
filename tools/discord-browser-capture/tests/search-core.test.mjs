import test from "node:test";
import assert from "node:assert/strict";
import { CaptureHistory, parseConnection, isSearchRequest, searchOffset, decodeSearchBody, MAX_RESPONSE_CHARACTERS } from "../core.mjs";

const guild = "1363839027114934315";
const channel = "1486808135883555037";
const rawConnection = {
  format: "KathanaCaptureConnection", version: 1,
  receiverUrl: `http://127.0.0.1:43123/kathana-capture/${"AB".repeat(32)}/`,
  sourceUrl: `https://discord.com/channels/${guild}/${channel}`, messageLimit: 10000
};
const connect = (changes = {}) => parseConnection(JSON.stringify({ ...rawConnection, ...changes }));
const post = (id, changes = {}) => ({ id: String(id), channel_id: channel, type: 0, content: "S> Tikoy PM me", timestamp: "2026-10-05T23:00:00Z", edited_timestamp: null, author: { id: "1486810317966282916", username: "YONEX", bot: true }, ...changes });

test("search items in the connection are trimmed, de-duplicated and bounded", () => {
  assert.deepEqual(connect().searchTerms, []);
  assert.deepEqual(connect({ searchTerms: ["  ror   asura ", "d.potra", "ror asura"] }).searchTerms, ["ror asura", "d.potra"]);
  assert.deepEqual(connect({ searchTerms: ["bad\nterm"] }).searchTerms, ["bad term"], "whitespace, including new lines, collapses to one space");
  assert.equal(connect({ searchTerms: Array.from({ length: 10 }, (_, index) => `item ${index}`) }).searchTerms.length, 10);
  for (const searchTerms of [
    "ror asura", [1], [null], [""], ["   "], ["x".repeat(101)], ["bad\u0000term"], ["bad\u007fterm"],
    Array.from({ length: 11 }, (_, index) => `item ${index}`)
  ]) assert.throws(() => connect({ searchTerms }), undefined, JSON.stringify(searchTerms));
});

test("only the source server's GET search endpoint qualifies as a search request", () => {
  const url = `https://discord.com/api/v9/guilds/${guild}/messages/search?content=ror%20asura&offset=25`;
  assert.equal(isSearchRequest(url, "GET", guild, "Fetch"), true);
  assert.equal(isSearchRequest(url, "GET", guild, "XHR"), true);
  for (const [bad, method, type] of [
    [url, "POST", "Fetch"], [url, "GET", "Document"],
    [url.replace(guild, "1363839027114934316"), "GET", "Fetch"],
    [url.replace("/messages/search", "/messages/search/tabs"), "GET", "Fetch"],
    [url.replace("/messages/search", "/members/search"), "GET", "Fetch"],
    [url.replace("discord.com", "discord.com.evil.test"), "GET", "Fetch"],
    [url.replace("discord.com", "user:secret@discord.com"), "GET", "Fetch"],
    [url.replace("https:", "http:"), "GET", "Fetch"], [url + "#fragment", "GET", "Fetch"]
  ]) assert.equal(isSearchRequest(bad, method, guild, type), false, bad);
  assert.equal(isSearchRequest(url, "GET", "not-a-snowflake", "Fetch"), false);
  assert.equal(searchOffset(url), 25);
  assert.equal(searchOffset(url.replace("&offset=25", "")), 0);
  assert.equal(searchOffset(url.replace("offset=25", "offset=-5")), 0);
  assert.equal(searchOffset(url.replace("offset=25", "offset=abc")), 0);
});

test("search responses keep only flagged hits from the source channel", () => {
  const hit = (id, changes = {}) => post(id, { hit: true, ...changes });
  const body = {
    total_results: 41,
    messages: [
      [post("1556808682858086500"), hit("1556808682858086501"), post("1556808682858086502")],
      [hit("1556808682858086510", { channel_id: "1486808135883555038" })],
      [post("1556808682858086520")],
      [hit("1556808682858086530")], []
    ]
  };
  const text = JSON.stringify(body);
  for (const result of [{ body: text, base64Encoded: false }, { body: Buffer.from(text).toString("base64"), base64Encoded: true }]) {
    const page = decodeSearchBody(result, channel);
    assert.deepEqual(page.hits.map(entry => entry.id), ["1556808682858086501", "1556808682858086520", "1556808682858086530"]);
    assert.equal(page.groups, 5);
    assert.equal(page.total, 41);
  }
  assert.equal(decodeSearchBody({ body: JSON.stringify({ messages: [] }), base64Encoded: false }, channel).total, null);
  // Hits flow through the normal sanitizer, which strips everything except trade-relevant fields.
  const history = new CaptureHistory(connect());
  const secret = hit("1556808682858086501", { author: { id: "1486810317966282916", username: "YONEX", token: "must-not-forward" } });
  history.addBatch(decodeSearchBody({ body: JSON.stringify({ total_results: 1, messages: [[secret]] }), base64Encoded: false }, channel).hits);
  assert.equal(JSON.stringify(history.envelope(true)).includes("must-not-forward"), false);
  assert.equal(history.messages.size, 1);
});

test("malformed or oversized search responses are rejected", () => {
  const decode = value => decodeSearchBody({ body: typeof value === "string" ? value : JSON.stringify(value), base64Encoded: false }, channel);
  for (const bad of ["not json", [], null, { messages: "x" }, { messages: [1] }, { messages: Array.from({ length: 101 }, () => []) }, { messages: [Array.from({ length: 21 }, () => ({}))] }]) {
    assert.throws(() => decode(bad), undefined, JSON.stringify(bad));
  }
  assert.throws(() => decodeSearchBody({ body: "x".repeat(MAX_RESPONSE_CHARACTERS + 1), base64Encoded: false }, channel));
  assert.throws(() => decodeSearchBody({ body: 5, base64Encoded: false }, channel));
});
