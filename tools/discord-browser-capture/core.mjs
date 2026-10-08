// Pure validation and history handling. No browser/session credentials are used.
export const MAX_MESSAGES = 10000;
export const MAX_RESPONSE_CHARACTERS = 4 * 1024 * 1024;
// UTF-8 is at most three bytes per JavaScript code unit. Leave room inside the
// receiver's 64 MiB body bound even for non-ASCII trade text.
export const MAX_CAPTURE_CHARACTERS = 20 * 1024 * 1024;
const MAX_SNOWFLAKE = 18446744073709551615n;

export function isSnowflake(value) {
  return typeof value === "string" && /^[1-9][0-9]{0,19}$/.test(value) && BigInt(value) <= MAX_SNOWFLAKE;
}

export function parseSourceUrl(value) {
  let url;
  try { url = new URL(value); } catch { throw new Error("Open a Discord channel link."); }
  const parts = /^\/channels\/([0-9]+)\/([0-9]+)(?:\/([0-9]+))?\/?$/.exec(url.pathname);
  if (url.protocol !== "https:" || url.hostname !== "discord.com" || url.port || url.username || url.password || url.search || url.hash ||
      !parts || !parts.slice(1).filter(Boolean).every(isSnowflake)) {
    throw new Error("Use an exact https://discord.com/channels/server/channel link.");
  }
  return { guildId: parts[1], channelId: parts[2], sourceUrl: `https://discord.com/channels/${parts[1]}/${parts[2]}` };
}

export const MAX_SEARCH_TERMS = 10;
export const MAX_SEARCH_TERM_LENGTH = 100;

// Optional list of items KathanaBot wants Discord's own search to filter on. Each is typed into Discord's search box.
export function parseSearchTerms(value) {
  const invalid = () => new Error("The search items in the connection are invalid. Copy it again from KathanaBot.");
  if (value === undefined) return [];
  if (!Array.isArray(value) || value.length > MAX_SEARCH_TERMS) throw invalid();
  const terms = [];
  for (const entry of value) {
    if (typeof entry !== "string") throw invalid();
    const term = entry.trim().replace(/\s+/g, " ");
    if (term.length < 1 || term.length > MAX_SEARCH_TERM_LENGTH || /[\u0000-\u001f\u007f]/.test(term)) throw invalid();
    if (!terms.includes(term)) terms.push(term);
  }
  return terms;
}

export function parseConnection(text) {
  if (typeof text !== "string" || text.length > 8192) throw new Error("Paste the connection JSON copied from KathanaBot.");
  let input;
  try { input = JSON.parse(text); } catch { throw new Error("The connection JSON is invalid. Copy it again from KathanaBot."); }
  if (input?.format !== "KathanaCaptureConnection" || input.version !== 1 ||
      !Number.isInteger(input.messageLimit) || input.messageLimit < 1 || input.messageLimit > MAX_MESSAGES) {
    throw new Error("The connection must select between 1 and 10,000 posts.");
  }
  let receiver;
  try { receiver = new URL(input.receiverUrl); } catch { throw new Error("The local receiver address is invalid."); }
  const match = /^\/kathana-capture\/([a-fA-F0-9]{32,128})\/$/.exec(receiver.pathname);
  if (receiver.protocol !== "http:" || receiver.hostname !== "127.0.0.1" || !receiver.port || receiver.username || receiver.password ||
      receiver.search || receiver.hash || !match || Number(receiver.port) < 1 || Number(receiver.port) > 65535) {
    throw new Error("Use only the temporary 127.0.0.1 connection copied from KathanaBot.");
  }
  const source = parseSourceUrl(input.sourceUrl);
  return { ...source, receiverUrl: receiver.href, captureId: match[1], messageLimit: input.messageLimit, searchTerms: parseSearchTerms(input.searchTerms) };
}

export function matchesChannelTab(value, connection) {
  try {
    const source = parseSourceUrl(value);
    return source.guildId === connection.guildId && source.channelId === connection.channelId;
  } catch { return false; }
}

export function isMessageRequest(value, method, channelId, resourceType) {
  if (method !== "GET" || !isSnowflake(channelId) || !["XHR", "Fetch"].includes(resourceType)) return false;
  let url;
  try { url = new URL(value); } catch { return false; }
  return url.protocol === "https:" && url.hostname === "discord.com" && !url.port && !url.username && !url.password && !url.hash &&
    new RegExp(`^/api/v[0-9]+/channels/${channelId}/messages$`).test(url.pathname);
}

// Discord's own search box queries GET /api/vN/guilds/<server>/messages/search. Only that exact path of the source server qualifies.
export function isSearchRequest(value, method, guildId, resourceType) {
  if (method !== "GET" || !isSnowflake(guildId) || !["XHR", "Fetch"].includes(resourceType)) return false;
  let url;
  try { url = new URL(value); } catch { return false; }
  return url.protocol === "https:" && url.hostname === "discord.com" && !url.port && !url.username && !url.password && !url.hash &&
    new RegExp(`^/api/v[0-9]+/guilds/${guildId}/messages/search$`).test(url.pathname);
}

export function searchOffset(value) {
  try {
    const offset = Number(new URL(value).searchParams.get("offset") ?? 0);
    return Number.isInteger(offset) && offset >= 0 && offset <= 1000000 ? offset : 0;
  } catch { return 0; }
}

export const SEARCH_PAGE_SIZE = 25;

function decodeJsonBody(result) {
  if (!result || typeof result.body !== "string" || typeof result.base64Encoded !== "boolean") {
    throw new Error("The browser did not return a readable Discord history response.");
  }
  let body = result.body;
  if (result.base64Encoded) {
    if (body.length > Math.ceil(MAX_RESPONSE_CHARACTERS / 3) * 4 + 4 || !/^[A-Za-z0-9+/]*={0,2}$/.test(body)) {
      throw new Error("The Discord history response is too large or invalid.");
    }
    let binary;
    try { binary = atob(body); } catch { throw new Error("The Discord history response has invalid encoding."); }
    if (binary.length > MAX_RESPONSE_CHARACTERS) throw new Error("The Discord history response is too large.");
    try { body = new TextDecoder("utf-8", { fatal: true }).decode(Uint8Array.from(binary, character => character.charCodeAt(0))); }
    catch { throw new Error("The Discord history response has invalid text encoding."); }
  }
  if (body.length > MAX_RESPONSE_CHARACTERS || new TextEncoder().encode(body).byteLength > MAX_RESPONSE_CHARACTERS) {
    throw new Error("The Discord history response is too large.");
  }
  try { return JSON.parse(body); } catch { throw new Error("The browser returned invalid Discord history JSON."); }
}

export function decodeNetworkBody(result) {
  const parsed = decodeJsonBody(result);
  if (!Array.isArray(parsed) || parsed.length > 100) throw new Error("The Discord history response must contain at most 100 messages.");
  return parsed;
}

// A search response is { messages: [[hit, ...context], ...], total_results }. Only the flagged hits from the source channel are kept;
// surrounding context messages and results from other channels are ignored.
export function decodeSearchBody(result, channelId) {
  const parsed = decodeJsonBody(result);
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.messages) || parsed.messages.length > 100) {
    throw new Error("The Discord search response must contain at most 100 result groups.");
  }
  const hits = [];
  for (const group of parsed.messages) {
    if (!Array.isArray(group) || group.length > 20) throw new Error("The Discord search response has an invalid result group.");
    const flagged = group.filter(entry => entry && typeof entry === "object" && entry.hit === true);
    for (const entry of flagged.length ? flagged : group.length === 1 ? group : []) {
      if (entry && typeof entry === "object" && entry.channel_id === channelId) hits.push(entry);
    }
  }
  const total = Number.isSafeInteger(parsed.total_results) && parsed.total_results >= 0 ? parsed.total_results : null;
  return { hits, groups: parsed.messages.length, total };
}

function textField(value, maximum = 16384) {
  if (value === undefined || value === null) return undefined;
  if (typeof value !== "string" || value.length > maximum) throw new Error("A Discord post contains an unsupported or oversized text field.");
  return value;
}

function setText(target, name, value, maximum) {
  const text = textField(value, maximum);
  if (text !== undefined) target[name] = text;
}

function sanitizeEmbed(embed) {
  if (!embed || typeof embed !== "object" || Array.isArray(embed)) throw new Error("Invalid Discord embed.");
  const result = {};
  setText(result, "title", embed.title);
  setText(result, "description", embed.description);
  if (embed.author?.name !== undefined) result.author = { name: textField(embed.author.name, 1024) };
  if (embed.footer?.text !== undefined) result.footer = { text: textField(embed.footer.text, 4096) };
  if (embed.fields !== undefined) {
    if (!Array.isArray(embed.fields) || embed.fields.length > 25) throw new Error("Invalid Discord embed fields.");
    result.fields = embed.fields.map(field => {
      if (!field || typeof field !== "object") throw new Error("Invalid Discord embed field.");
      const clean = {};
      setText(clean, "name", field.name, 1024);
      setText(clean, "value", field.value);
      return clean;
    });
  }
  return result;
}

export function sanitizeMessage(message, channelId) {
  if (!message || typeof message !== "object" || Array.isArray(message) || !isSnowflake(message.id) || message.channel_id !== channelId) {
    throw new Error("A message does not belong to the selected Discord channel.");
  }
  if (!Number.isInteger(message.type) || message.type < 0 || message.type > 255) throw new Error("Invalid Discord message type.");
  const result = { id: message.id, channel_id: channelId, type: message.type };
  setText(result, "content", message.content, 65536);
  setText(result, "timestamp", message.timestamp, 128);
  if (message.edited_timestamp === null) result.edited_timestamp = null;
  else setText(result, "edited_timestamp", message.edited_timestamp, 128);
  if (message.flags !== undefined) {
    if (!Number.isSafeInteger(message.flags) || message.flags < 0) throw new Error("Invalid Discord message flags.");
    result.flags = message.flags;
  }
  if (message.webhook_id !== undefined) {
    if (!isSnowflake(message.webhook_id)) throw new Error("Invalid Discord webhook ID.");
    result.webhook_id = message.webhook_id;
  }
  if (message.author !== undefined) {
    if (!message.author || !isSnowflake(message.author.id)) throw new Error("Invalid Discord message author.");
    result.author = { id: message.author.id };
    setText(result.author, "username", message.author.username, 1024);
    if (typeof message.author.bot === "boolean") result.author.bot = message.author.bot;
  }
  if (message.embeds !== undefined) {
    if (!Array.isArray(message.embeds) || message.embeds.length > 10) throw new Error("Invalid Discord embeds.");
    result.embeds = message.embeds.map(sanitizeEmbed);
  }
  if (message.attachments !== undefined) {
    if (!Array.isArray(message.attachments) || message.attachments.length > 10) throw new Error("Invalid Discord attachments.");
    // Filenames can supply useful trade text. No CDN downloads or URL forwarding.
    result.attachments = message.attachments.map(attachment => {
      if (!attachment || typeof attachment !== "object") throw new Error("Invalid Discord attachment.");
      const clean = {};
      setText(clean, "filename", attachment.filename, 1024);
      return clean;
    });
  }
  if (JSON.stringify(result).length > 100000) throw new Error("A Discord post is too large for this capture.");
  return result;
}

export class CaptureHistory {
  constructor(connection) {
    this.connection = connection;
    this.messages = new Map();
    this.characters = 0;
    this.revision = 0;
    this.responses = 0;
  }

  addBatch(batch) {
    if (!Array.isArray(batch) || batch.length > 100) throw new Error("The Discord history response must contain at most 100 messages.");
    const cleaned = batch.map(message => sanitizeMessage(message, this.connection.channelId));
    const next = new Map(this.messages);
    let added = 0;
    for (const message of cleaned) {
      const old = next.get(message.id);
      if (old) {
        const newer = Date.parse(message.edited_timestamp ?? message.timestamp ?? "") || 0;
        const older = Date.parse(old.edited_timestamp ?? old.timestamp ?? "") || 0;
        if (newer >= older) next.set(message.id, message);
      } else {
        next.set(message.id, message);
        added++;
      }
    }
    const ordered = [...next.values()].sort((a, b) => BigInt(a.id) > BigInt(b.id) ? -1 : BigInt(a.id) < BigInt(b.id) ? 1 : 0);
    const kept = ordered.slice(0, this.connection.messageLimit);
    const characters = kept.reduce((sum, message) => sum + JSON.stringify(message).length, 0);
    if (characters > MAX_CAPTURE_CHARACTERS) throw new Error("The capture is too large. Select fewer posts in KathanaBot.");
    this.messages = new Map(kept.map(message => [message.id, message]));
    this.characters = characters;
    this.responses++;
    if (added) this.revision++;
    return { added, count: this.messages.size, empty: batch.length === 0 };
  }

  envelope(historyExhausted, captureStatus = historyExhausted ? "history-ended" : "limit-reached") {
    return {
      format: "KathanaTradeCapture", version: 1, captureId: this.connection.captureId,
      sourceGuildId: this.connection.guildId, sourceChannelId: this.connection.channelId,
      requestedMessageCount: this.connection.messageLimit, historyExhausted: Boolean(historyExhausted),
      captureStatus,
      capturedAt: new Date().toISOString(), messages: [...this.messages.values()]
    };
  }
}
