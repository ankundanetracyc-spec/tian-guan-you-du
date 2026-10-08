import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));
const syncDirectory = path.join(root, ".data");
const syncFile = path.join(syncDirectory, "sync-workspace.json");
const args = new Map();
for (let index = 2; index < process.argv.length; index += 1) {
  const key = process.argv[index];
  if (key.startsWith("--") && process.argv[index + 1] && !process.argv[index + 1].startsWith("--")) {
    args.set(key.slice(2), process.argv[index + 1]);
    index += 1;
  }
}

const host = args.get("host") || args.get("bind") || "127.0.0.1";
const port = Number(args.get("port") || 8765);
fs.mkdirSync(syncDirectory, { recursive: true });
const mimeTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

function emptyWorkspace() {
  return { schemaVersion: 1, project: "青溪问道", cards: [], manuals: [], testConfig: null, deleted: [] };
}

function normalizeWorkspace(value) {
  const workspace = value && typeof value === "object" ? value : {};
  return {
    schemaVersion: 1,
    project: "青溪问道",
    cards: Array.isArray(workspace.cards) ? workspace.cards.filter(item => item && !item.projectSource) : [],
    manuals: Array.isArray(workspace.manuals) ? workspace.manuals.filter(item => item && !item.projectSource) : [],
    testConfig: workspace.testConfig && typeof workspace.testConfig === "object" ? workspace.testConfig : null,
    deleted: Array.isArray(workspace.deleted) ? workspace.deleted.filter(item => item && item.kind && item.id) : [],
  };
}

function loadSyncState() {
  try {
    const value = JSON.parse(fs.readFileSync(syncFile, "utf8"));
    return { revision: Number(value.revision) || 0, updatedAt: value.updatedAt || "", workspace: normalizeWorkspace(value.workspace) };
  } catch (_) {
    return { revision: 0, updatedAt: "", workspace: emptyWorkspace() };
  }
}

let syncState = loadSyncState();

function persistSyncState() {
  const temporary = `${syncFile}.tmp`;
  fs.writeFileSync(temporary, JSON.stringify(syncState, null, 2), "utf8");
  fs.renameSync(temporary, syncFile);
}

function responseHeaders(contentType = "application/json; charset=utf-8") {
  return { "Content-Type": contentType, "Access-Control-Allow-Origin": "*", "Access-Control-Allow-Methods": "GET, PUT, OPTIONS", "Access-Control-Allow-Headers": "Content-Type", "Cache-Control": "no-store" };
}

function sendJson(response, status, value) {
  response.writeHead(status, responseHeaders());
  response.end(JSON.stringify(value));
}

function readJsonBody(request) {
  return new Promise((resolve, reject) => {
    let body = "";
    request.setEncoding("utf8");
    request.on("data", chunk => {
      body += chunk;
      if (body.length > 8 * 1024 * 1024) {
        request.destroy();
        reject(new Error("请求内容过大"));
      }
    });
    request.on("end", () => {
      try { resolve(body ? JSON.parse(body) : {}); } catch (_) { reject(new Error("请求不是有效 JSON")); }
    });
    request.on("error", reject);
  });
}

async function handleSync(request, response) {
  if (request.method === "OPTIONS") {
    response.writeHead(204, responseHeaders());
    response.end();
    return true;
  }
  if (request.method === "GET") {
    sendJson(response, 200, syncState);
    return true;
  }
  if (request.method !== "PUT") {
    sendJson(response, 405, { error: "只支持 GET 或 PUT" });
    return true;
  }
  try {
    const payload = await readJsonBody(request);
    const baseRevision = Number(payload.baseRevision);
    if (!Number.isInteger(baseRevision) || baseRevision !== syncState.revision) {
      sendJson(response, 409, { error: "服务器数据已更新", ...syncState });
      return true;
    }
    syncState = { revision: syncState.revision + 1, updatedAt: new Date().toISOString(), workspace: normalizeWorkspace(payload.workspace) };
    persistSyncState();
    sendJson(response, 200, syncState);
  } catch (error) {
    sendJson(response, 400, { error: error.message || "无法处理同步数据" });
  }
  return true;
}

const server = http.createServer(async (request, response) => {
  let requestPath;
  try { requestPath = new URL(request.url || "/", `http://${request.headers.host || "localhost"}`).pathname; } catch (_) { sendJson(response, 400, { error: "无效请求地址" }); return; }
  if (requestPath === "/api/sync") {
    await handleSync(request, response);
    return;
  }
  if (request.method !== "GET" && request.method !== "HEAD") {
    response.writeHead(405, responseHeaders("text/plain; charset=utf-8"));
    response.end("Method Not Allowed");
    return;
  }
  const relativePath = requestPath === "/" ? "index.html" : requestPath.replace(/^\/+/, "");
  if (relativePath === ".data" || relativePath.startsWith(`.data${path.sep}`) || relativePath.startsWith(".data/")) {
    response.writeHead(403, responseHeaders("text/plain; charset=utf-8"));
    response.end("Forbidden");
    return;
  }
  const filePath = path.resolve(root, relativePath);
  if (!filePath.startsWith(`${root}${path.sep}`) && filePath !== root) {
    response.writeHead(403, responseHeaders("text/plain; charset=utf-8"));
    response.end("Forbidden");
    return;
  }
  fs.stat(filePath, (error, stats) => {
    if (error || !stats.isFile()) {
      response.writeHead(404, responseHeaders("text/plain; charset=utf-8"));
      response.end("Not found");
      return;
    }
    response.writeHead(200, responseHeaders(mimeTypes[path.extname(filePath)] || "application/octet-stream"));
    if (request.method === "HEAD") { response.end(); return; }
    fs.createReadStream(filePath).pipe(response);
  });
});

server.listen(port, host, () => {
  console.log(`卡牌工坊已启动：http://${host}:${port}/`);
});
