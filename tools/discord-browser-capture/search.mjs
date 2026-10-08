// These functions execute in the selected Discord tab through chrome.scripting.executeScript, which serializes each one,
// so every function must be fully self-contained (no module-level helpers). They only locate and focus Discord's own
// search box and press its own "next page" control. Typing and Enter are sent separately through the attached debugger.
// Discord's markup changes over time, so each function reports why it failed and the worker then falls back to letting
// the user finish by hand.

export function focusSearchBox(guildId, channelId) {
  const route = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/[0-9]+)?\/?$/.exec(location.pathname);
  if (location.origin !== "https://discord.com" || !route || route[1] !== guildId || route[2] !== channelId) return { found: false, reason: "navigation" };
  const selectors = ['[contenteditable="true"][aria-label^="Search"]', '[role="combobox"][aria-label^="Search"]', '[class*="search"] [contenteditable="true"]'];
  for (const selector of selectors) {
    for (const element of document.querySelectorAll(selector)) {
      if (element.getClientRects().length === 0) continue;
      element.click();
      element.focus();
      return { found: true };
    }
  }
  return { found: false, reason: "no-search-box" };
}

export function searchBoxFocused(guildId, channelId) {
  const route = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/[0-9]+)?\/?$/.exec(location.pathname);
  if (location.origin !== "https://discord.com" || !route || route[1] !== guildId || route[2] !== channelId) return { focused: false, reason: "navigation" };
  const active = document.activeElement;
  const label = active?.getAttribute?.("aria-label") ?? "";
  return { focused: Boolean(active) && active.getAttribute?.("contenteditable") === "true" && /^search/i.test(label) };
}

export function clickNextSearchPage(guildId, channelId) {
  const route = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/[0-9]+)?\/?$/.exec(location.pathname);
  if (location.origin !== "https://discord.com" || !route || route[1] !== guildId || route[2] !== channelId) return { found: false, reason: "navigation" };
  const scopes = ['nav[aria-label*="agination" i]', '[class*="pagination"]', '[class*="pageControl"]', '[class*="searchResults"]', '[id^="search-results"]'];
  let sawScope = false;
  for (const selector of scopes) {
    for (const scope of document.querySelectorAll(selector)) {
      sawScope = true;
      for (const button of scope.querySelectorAll('button,[role="button"]')) {
        const label = (button.getAttribute("aria-label") ?? "").trim();
        const text = (button.textContent ?? "").trim();
        if (!(/^next( page)?$/i.test(label) || /^next( page)?$/i.test(text) || /^[›>»]$/.test(text))) continue;
        if (button.disabled || button.getAttribute("aria-disabled") === "true") return { found: false, reason: "last-page" };
        button.click();
        return { found: true };
      }
    }
  }
  return { found: false, reason: sawScope ? "no-next-button" : "no-results-panel" };
}
