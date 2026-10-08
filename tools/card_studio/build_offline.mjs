import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));
const templatePath = path.join(directory, "index.html");
const outputPath = path.join(directory, "qingxi-card-studio-offline.html");
const inlineScript = value => value.replace(/<\/script/gi, "<\\/script");

let html = fs.readFileSync(templatePath, "utf8");
const css = fs.readFileSync(path.join(directory, "app.css"), "utf8");
const catalog = fs.readFileSync(path.join(directory, "project-catalog.js"), "utf8");
const app = fs.readFileSync(path.join(directory, "app.js"), "utf8");

html = html.replace(/\s*<link rel="manifest"[^>]*>/i, "");
html = html.replace('<link rel="stylesheet" href="app.css?v=2">', `<style>\n${css}\n</style>`);
html = html.replace('<script src="project-catalog.js?v=2"></script>', `<script>${inlineScript(catalog)}</script>`);
html = html.replace('<script src="app.js?v=2"></script>', `<script>${inlineScript(app)}</script>`);

if (html.includes('href="app.css?v=2"') || html.includes('src="app.js?v=2"') || html.includes('src="project-catalog.js?v=2"')) throw new Error("离线版内联替换不完整");
fs.writeFileSync(outputPath, html, "utf8");
console.log(`已生成单文件离线版：${outputPath}`);
