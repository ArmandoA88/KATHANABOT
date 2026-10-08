import { parseConnection } from "./core.mjs";

const connection = document.getElementById("connection");
const start = document.getElementById("start");
const stop = document.getElementById("stop");
const finish = document.getElementById("finish");
const status = document.getElementById("status");
const count = document.getElementById("count");
let busy = false;

function render(state) {
  start.disabled = Boolean(state.running) || busy;
  stop.disabled = !state.running || busy;
  finish.disabled = !state.running || !state.canFinish || busy;
  connection.disabled = Boolean(state.running) || busy;
  status.textContent = state.message;
  if (state.requested) count.textContent = `${state.count.toLocaleString()} / ${state.requested.toLocaleString()} posts`;
}

async function refresh() {
  try {
    const response = await chrome.runtime.sendMessage({ action: "status" });
    if (!busy && response?.ok) render(response.status);
  } catch { if (!busy) status.textContent = "Capture worker is unavailable. Reload the extension and try again."; }
}

connection.addEventListener("input", () => {
  try {
    const parsed = parseConnection(connection.value);
    count.textContent = `Posts to capture: ${parsed.messageLimit.toLocaleString()}`;
  } catch { count.textContent = "Posts to capture: use the count selected in KathanaBot."; }
});

start.addEventListener("click", async () => {
  try {
    parseConnection(connection.value);
    busy = true;
    start.disabled = true;
    connection.disabled = true;
    status.textContent = "Connecting…";
    const response = await chrome.runtime.sendMessage({ action: "start", connection: connection.value });
    busy = false;
    if (!response?.ok) throw new Error(response?.error || "Capture could not start.");
    render(response.status);
    // Temporary connection secrets are not stored in browser storage.
    if (response.status.running) connection.value = "";
  } catch (error) {
    busy = false;
    start.disabled = false;
    connection.disabled = false;
    status.textContent = error.message;
  }
});

finish.addEventListener("click", async () => {
  finish.disabled = true;
  try {
    const response = await chrome.runtime.sendMessage({ action: "finish" });
    if (response?.ok) status.textContent = "Finishing: importing the posts found so far.";
  } catch {
    status.textContent = "Could not contact the capture worker.";
  }
});

stop.addEventListener("click", async () => {
  busy = true;
  stop.disabled = true;
  try {
    const response = await chrome.runtime.sendMessage({ action: "stop" });
    busy = false;
    if (response?.ok) render(response.status);
  } catch {
    busy = false;
    status.textContent = "Could not contact the capture worker. Cancel Chrome's debugging banner to stop capture.";
  }
});

void refresh();
const polling = setInterval(refresh, 1000);
window.addEventListener("pagehide", () => clearInterval(polling));
