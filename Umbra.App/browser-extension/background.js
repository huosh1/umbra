const HOST = "com.umbra.browser_blocker";
let port;
let activeSites = [];
let reconnectTimer;
const lastAttempts = new Map();

function connect() {
  clearTimeout(reconnectTimer);
  try {
    port = chrome.runtime.connectNative(HOST);
    port.onMessage.addListener(handleMessage);
    port.onDisconnect.addListener(() => {
      port = null;
      applyState(false, []);
      reconnectTimer = setTimeout(connect, 3000);
    });
    requestState();
  } catch {
    reconnectTimer = setTimeout(connect, 3000);
  }
}

function requestState() {
  if (!port) return;
  port.postMessage({ action: "getState" });
  setTimeout(requestState, 2000);
}

function handleMessage(message) {
  if (message?.ok && typeof message.blocking === "boolean") {
    applyState(message.blocking, message.sites || []);
  }
}

async function applyState(blocking, sites) {
  activeSites = blocking ? sites : [];
  const oldRules = await chrome.declarativeNetRequest.getDynamicRules();
  const addRules = activeSites.map((site, index) => ({
    id: index + 1,
    priority: 1,
    action: { type: "redirect", redirect: { extensionPath: "/blocked.html" } },
    condition: {
      regexFilter: `^https?://([^/]+\\.)?${escapeRegex(site)}(?::[0-9]+)?(?:/|$)`,
      resourceTypes: ["main_frame"]
    }
  }));
  await chrome.declarativeNetRequest.updateDynamicRules({
    removeRuleIds: oldRules.map(rule => rule.id),
    addRules
  });
  if (blocking) await redirectAlreadyOpenTabs();
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function matchingSite(url) {
  let host;
  try { host = new URL(url).hostname.replace(/^www\./, ""); } catch { return null; }
  return activeSites.find(site => host === site || host.endsWith(`.${site}`)) || null;
}

async function redirectAlreadyOpenTabs() {
  const tabs = await chrome.tabs.query({});
  for (const tab of tabs) {
    if (!tab.id || !tab.url || !matchingSite(tab.url)) continue;
    await chrome.tabs.update(tab.id, { url: chrome.runtime.getURL("blocked.html") });
  }
}

chrome.webNavigation.onBeforeNavigate.addListener(details => {
  if (details.frameId !== 0 || !port) return;
  const matched = matchingSite(details.url);
  if (!matched) return;
  const now = Date.now();
  if (now - (lastAttempts.get(matched) || 0) < 3000) return;
  lastAttempts.set(matched, now);
  port.postMessage({ action: "recordAttempt", target: matched });
});

connect();
