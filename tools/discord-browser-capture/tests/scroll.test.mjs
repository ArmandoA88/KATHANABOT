import test from "node:test";
import assert from "node:assert/strict";
import { scrollObservedChat } from "../scroll.mjs";

const guild = "1363839027114934315";
const channel = "1486808135883555037";
const id = "1556808682858086542";

function fixture({ matching = true, hidden = false, overflow = "auto", atTop = false, wrongChannel = false } = {}) {
  const body = {};
  const documentElement = {};
  const chat = { parentElement: body, clientHeight: 400, scrollHeight: 2400, scrollTop: atTop ? 0 : 1200, isConnected: true, events: 0, dispatchEvent() { this.events++; }, overflow };
  const message = { parentElement: chat, getAttribute(name) { return name === "data-list-item-id" && matching ? `chat-messages___${id}` : "sidebar-other"; }, getClientRects() { return hidden ? [] : [{}]; } };
  const sidebar = { parentElement: body, clientHeight: 400, scrollHeight: 4000, scrollTop: 500, overflow: "auto", isConnected: true, events: 0, dispatchEvent() { this.events++; } };
  const unrelated = { parentElement: sidebar, getAttribute() { return "guild-sidebar-123"; }, getClientRects() { return [{}]; } };
  globalThis.location = { origin: "https://discord.com", pathname: `/channels/${guild}/${wrongChannel ? channel + "8" : channel}` };
  globalThis.document = { body, documentElement, querySelectorAll(selector) { assert.equal(selector, "[id],[data-list-item-id]"); return [unrelated, message]; } };
  globalThis.getComputedStyle = element => ({ overflowY: element.overflow });
  globalThis.requestAnimationFrame = callback => { callback(); };
  return { chat, sidebar };
}

test("only ancestor proved by an observed rendered message ID scrolls", async () => {
  const { chat, sidebar } = fixture();
  assert.deepEqual(await scrollObservedChat(guild, channel, [id]), { found: true, before: 1200, after: 0 });
  assert.equal(chat.events, 1);
  assert.equal(sidebar.scrollTop, 500);
  assert.equal(sidebar.events, 0);
});

test("at-top reload trigger gives the proved chat container 750ms before returning to top", async t => {
  t.mock.timers.enable({ apis: ["setTimeout", "Date"], now: 1000 });
  const { chat, sidebar } = fixture({ atTop: true });
  const pending = scrollObservedChat(guild, channel, [id]);
  assert.equal(chat.scrollTop, 24);
  assert.equal(chat.events, 1);
  t.mock.timers.tick(749);
  await Promise.resolve();
  assert.equal(chat.events, 1);
  t.mock.timers.tick(1);
  const result = await pending;
  assert.equal(result.found, true);
  assert.equal(chat.scrollTop, 0);
  assert.equal(chat.events, 2);
  assert.equal(sidebar.events, 0);
});

test("unknown/hidden IDs and non-scroll ancestors cannot cause a sidebar/page guess", async () => {
  for (const options of [{ matching: false }, { hidden: true }, { overflow: "hidden" }]) {
    const { chat, sidebar } = fixture(options);
    assert.equal((await scrollObservedChat(guild, channel, [id])).found, false);
    assert.equal(chat.events, 0);
    assert.equal(sidebar.events, 0);
  }
});

test("navigation to a channel with a common numeric prefix aborts before touching DOM", async () => {
  const { chat } = fixture({ wrongChannel: true });
  assert.deepEqual(await scrollObservedChat(guild, channel, [id]), { found: false, reason: "navigation" });
  assert.equal(chat.events, 0);
});
