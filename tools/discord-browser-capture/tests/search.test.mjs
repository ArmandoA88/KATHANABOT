import test from "node:test";
import assert from "node:assert/strict";
import vm from "node:vm";
import { focusSearchBox, searchBoxFocused, clickNextSearchPage } from "../search.mjs";

const guild = "1363839027114934315";
const channel = "1486808135883555037";

function element(attributes = {}, { visible = true, text = "", disabled = false } = {}) {
  return {
    clicks: 0, focused: 0, disabled, textContent: text,
    getAttribute(name) { return Object.hasOwn(attributes, name) ? attributes[name] : null; },
    getClientRects() { return visible ? [{}] : []; },
    click() { this.clicks++; }, focus() { this.focused++; }
  };
}

function page({ pathname = `/channels/${guild}/${channel}`, origin = "https://discord.com", lists = {}, active = null } = {}) {
  return {
    location: { origin, pathname },
    document: {
      activeElement: active,
      querySelectorAll(selector) { return lists[selector] ?? []; }
    }
  };
}

// chrome.scripting.executeScript serializes the function and runs it in the page, so it must not use module-level helpers.
function inPage(func, environment) {
  // The result is round-tripped through JSON, as executeScript does, so it can be compared across realms.
  return JSON.parse(JSON.stringify(vm.runInNewContext(`(${func.toString()})`, environment)(guild, channel)));
}

test("page functions are self-contained when serialized into the tab", () => {
  for (const func of [focusSearchBox, searchBoxFocused, clickNextSearchPage]) {
    const environment = page();
    assert.doesNotThrow(() => inPage(func, { ...environment, RegExp, Boolean }), func.name);
  }
});

test("focusSearchBox clicks and focuses Discord's visible search box on the source channel only", () => {
  const hidden = element({ "aria-label": "Search Kathana", contenteditable: "true" }, { visible: false });
  const box = element({ "aria-label": "Search Kathana", contenteditable: "true" });
  const environment = page({ lists: { '[contenteditable="true"][aria-label^="Search"]': [hidden, box] } });
  assert.deepEqual(inPage(focusSearchBox, environment), { found: true });
  assert.equal(box.clicks, 1);
  assert.equal(box.focused, 1);
  assert.equal(hidden.clicks, 0);
  assert.deepEqual(inPage(focusSearchBox, page()), { found: false, reason: "no-search-box" });
  assert.deepEqual(inPage(focusSearchBox, page({ pathname: `/channels/${guild}/${channel}8` })), { found: false, reason: "navigation" });
  assert.deepEqual(inPage(focusSearchBox, page({ origin: "https://example.com" })), { found: false, reason: "navigation" });
});

test("searchBoxFocused only accepts a focused editable Search control", () => {
  const search = element({ contenteditable: "true", "aria-label": "Search Kathana" });
  const composer = element({ contenteditable: "true", "aria-label": "Message #board" });
  const plain = element({ "aria-label": "Search" });
  assert.deepEqual(inPage(searchBoxFocused, page({ active: search })), { focused: true });
  assert.deepEqual(inPage(searchBoxFocused, page({ active: composer })), { focused: false });
  assert.deepEqual(inPage(searchBoxFocused, page({ active: plain })), { focused: false });
  assert.deepEqual(inPage(searchBoxFocused, page({ active: null })), { focused: false });
  assert.deepEqual(inPage(searchBoxFocused, page({ pathname: "/channels/@me", active: search })), { focused: false, reason: "navigation" });
});

test("clickNextSearchPage presses only an enabled Next control inside a pagination/results scope", () => {
  const next = element({ "aria-label": "Next" });
  const previous = element({ "aria-label": "Previous" });
  const scope = { querySelectorAll: () => [previous, next] };
  const environment = page({ lists: { '[class*="pagination"]': [scope] } });
  assert.deepEqual(inPage(clickNextSearchPage, environment), { found: true });
  assert.equal(next.clicks, 1);
  assert.equal(previous.clicks, 0);

  const arrow = element({}, { text: "›" });
  assert.deepEqual(inPage(clickNextSearchPage, page({ lists: { '[class*="searchResults"]': [{ querySelectorAll: () => [arrow] }] } })), { found: true });
  assert.equal(arrow.clicks, 1);

  const last = element({ "aria-label": "Next page", "aria-disabled": "true" });
  assert.deepEqual(inPage(clickNextSearchPage, page({ lists: { '[class*="pagination"]': [{ querySelectorAll: () => [last] }] } })), { found: false, reason: "last-page" });
  assert.equal(last.clicks, 0);
  const disabled = element({ "aria-label": "Next" }, { disabled: true });
  assert.deepEqual(inPage(clickNextSearchPage, page({ lists: { '[class*="pagination"]': [{ querySelectorAll: () => [disabled] }] } })), { found: false, reason: "last-page" });

  assert.deepEqual(inPage(clickNextSearchPage, page({ lists: { '[class*="pagination"]': [{ querySelectorAll: () => [previous] }] } })), { found: false, reason: "no-next-button" });
  assert.deepEqual(inPage(clickNextSearchPage, page()), { found: false, reason: "no-results-panel" });
  assert.deepEqual(inPage(clickNextSearchPage, page({ pathname: `/channels/${guild}/${channel}8` })), { found: false, reason: "navigation" });
});
