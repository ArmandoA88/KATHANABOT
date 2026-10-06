// This entire function executes in the selected tab's isolated world. It only
// inspects message-ID attributes and changes the proven chat ancestor's scroll.
export async function scrollObservedChat(guildId, channelId, observedIds) {
  const route = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/[0-9]+)?\/?$/.exec(location.pathname);
  if (location.origin !== "https://discord.com" || !route || route[1] !== guildId || route[2] !== channelId) {
    return { found: false, reason: "navigation" };
  }
  const known = new Set(observedIds);
  let scroller = null;
  for (const element of document.querySelectorAll("[id],[data-list-item-id]")) {
    const values = [element.getAttribute("id"), element.getAttribute("data-list-item-id")];
    const matches = values.some(value => value && (value.match(/[0-9]+/g) || []).some(id => known.has(id)));
    if (!matches || element.getClientRects().length === 0) continue;
    for (let ancestor = element.parentElement; ancestor && ancestor !== document.body && ancestor !== document.documentElement; ancestor = ancestor.parentElement) {
      const overflow = getComputedStyle(ancestor).overflowY;
      if ((overflow === "auto" || overflow === "scroll") && ancestor.clientHeight >= 100 && ancestor.scrollHeight > ancestor.clientHeight + 1) {
        scroller = ancestor;
        break;
      }
    }
    if (scroller) break;
  }
  if (!scroller) return { found: false, reason: "no-observed-chat-scroller" };
  const before = scroller.scrollTop;
  if (before <= 1) {
    scroller.scrollTop = Math.min(24, scroller.scrollHeight - scroller.clientHeight);
    scroller.dispatchEvent(new Event("scroll", { bubbles: true }));
    // requestAnimationFrame can stop entirely in a background tab. This fixed
    // pause gives the ordinary scroll handler time to observe the motion.
    await new Promise(resolve => setTimeout(resolve, 750));
  }
  // Navigation can happen while waiting for the ordinary scroll handler.
  const currentRoute = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/[0-9]+)?\/?$/.exec(location.pathname);
  if (location.origin !== "https://discord.com" || !currentRoute || currentRoute[1] !== guildId || currentRoute[2] !== channelId || !scroller.isConnected) {
    return { found: false, reason: "navigation" };
  }
  scroller.scrollTop = 0;
  scroller.dispatchEvent(new Event("scroll", { bubbles: true }));
  return { found: true, before, after: scroller.scrollTop };
}
