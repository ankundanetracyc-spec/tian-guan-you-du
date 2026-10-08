import fs from "node:fs";
import path from "node:path";

const root = path.resolve(process.argv[2] || path.join(import.meta.dirname, "..", ".."));
const output = path.resolve(process.argv[3] || path.join(import.meta.dirname, "project-catalog.js"));

const skillTypes = ["ATTACK", "MOVEMENT", "DRAW", "BUFF", "WAIT", "SPECIAL", "POWER", "CURSE"];
const wuxing = [
  "NONE", "METAL", "WOOD", "WATER", "FIRE", "EARTH", "METAL_WOOD", "METAL_WATER", "METAL_FIRE", "METAL_EARTH",
  "WOOD_WATER", "WOOD_FIRE", "WOOD_EARTH", "WATER_FIRE", "WATER_EARTH", "FIRE_EARTH", "METAL_WOOD_WATER",
  "METAL_WOOD_FIRE", "METAL_WOOD_EARTH", "METAL_WATER_FIRE", "METAL_WATER_EARTH", "METAL_FIRE_EARTH",
  "WOOD_WATER_FIRE", "WOOD_WATER_EARTH", "WOOD_FIRE_EARTH", "WATER_FIRE_EARTH", "METAL_WOOD_WATER_FIRE",
  "METAL_WOOD_WATER_EARTH", "METAL_WOOD_FIRE_EARTH", "METAL_WATER_FIRE_EARTH", "WOOD_WATER_FIRE_EARTH",
  "METAL_WOOD_WATER_FIRE_EARTH", "ALL"
];

function read(file) { return fs.readFileSync(file, "utf8"); }
function filesUnder(dir, extension) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? filesUnder(full, extension) : (entry.name.endsWith(extension) ? [full] : []);
  });
}
function godotString(value) {
  const match = value.match(/=\s*"((?:\\.|[^"\\])*)"/);
  return match ? match[1].replaceAll('\\"', '"').replaceAll('\\n', "\n") : "";
}
function lineValue(block, key) {
  const match = block.match(new RegExp(`^${key}\\s*=\\s*(.*)$`, "m"));
  return match ? match[1].trim() : "";
}
function stringField(block, key, fallback = "") {
  const value = lineValue(block, key);
  if (!value) return fallback;
  if (value.startsWith('"')) return value.slice(1, value.lastIndexOf('"')).replaceAll('\\"', '"').replaceAll('\\n', "\n");
  return value;
}
function numberField(block, key, fallback = 0) {
  const value = lineValue(block, key).replace(/,$/, "");
  if (!value) return fallback;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function boolField(block, key, fallback = false) {
  const value = lineValue(block, key).replace(/,$/, "");
  if (value === "true") return true;
  if (value === "false" || value === "null") return false;
  return fallback;
}
function dictField(block, key) {
  const start = block.search(new RegExp(`^${key}\\s*=\\s*\\{`, "m"));
  if (start < 0) return {};
  const brace = block.indexOf("{", start);
  const endLf = block.indexOf("}\n", brace);
  const endCrLf = block.indexOf("}\r\n", brace);
  const end = endCrLf >= 0 && (endLf < 0 || endCrLf < endLf) ? endCrLf : endLf;
  const text = block.slice(brace + 1, end < 0 ? block.length : end);
  const result = {};
  for (const match of text.matchAll(/"([^"]+)"\s*:\s*([^,\n}]+)/g)) {
    const raw = match[2].trim();
    result[match[1]] = raw === "true" ? true : raw === "false" || raw === "null" ? false : (Number.isFinite(Number(raw)) ? Number(raw) : raw.replace(/^"|"$/g, ""));
  }
  return result;
}
function arrayStrings(block, key) {
  const raw = lineValue(block, key);
  return [...raw.matchAll(/"((?:\\.|[^"\\])*)"/g)].map(match => match[1].replaceAll('\\"', '"'));
}
function arrayNumbers(block, key) {
  const raw = lineValue(block, key);
  return [...raw.matchAll(/(?:^|,|\[)\s*(-?\d+(?:\.\d+)?)\s*(?=,|\]|$)/g)].map(match => Number(match[1]));
}
function extResources(text) {
  const result = {};
  for (const match of text.matchAll(/\[ext_resource[^\n]*?path="([^"]+)"[^\n]*?id="([^"]+)"[^\n]*?\]/g)) result[match[2]] = match[1];
  return result;
}
function refPaths(raw, ext) {
  return [...raw.matchAll(/ExtResource\("([^"]+)"\)/g)].map(match => ext[match[1]]).filter(Boolean);
}
function arrayRefIds(block, key) {
  return lineValue(block, key).match(/ExtResource\("[^"]+"\)|SubResource\("[^"]+"\)/g) || [];
}
function parseDictionaryArray(raw) {
  const result = [];
  for (const object of raw.matchAll(/\{([^{}]*)\}/g)) {
    const item = {};
    for (const pair of object[1].matchAll(/"([^"]+)"\s*:\s*("(?:\\.|[^"\\])*"|-?\d+(?:\.\d+)?|true|false)/g)) {
      const value = pair[2];
      item[pair[1]] = value.startsWith('"') ? value.slice(1, -1).replaceAll('\\"', '"') : value === "true" ? true : value === "false" ? false : Number(value);
    }
    result.push(item);
  }
  return result;
}
function dictionaryArray(block, key) {
  const raw = lineValue(block, key);
  return parseDictionaryArray(raw);
}
function subresourceBlocks(text) {
  const matches = [...text.matchAll(/\[sub_resource[^\n]*?id="([^"]+)"\][\s\S]*?(?=\n\[sub_resource|\n\[resource|$)/g)];
  return matches.map(match => ({ id: match[1], text: match[0] }));
}
function normalizedPath(file) {
  return "res://" + path.relative(root, file).replaceAll(path.sep, "/");
}
function safeId(value, fallback) {
  const ascii = String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, "");
  return ascii || fallback;
}
function stableKey(value) {
  let hash = 2166136261;
  for (const char of String(value)) {
    hash ^= char.codePointAt(0);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(16).padStart(8, "0");
}
function sourceInfo(file) {
  const relative = path.relative(path.join(root, "resources", "activeskills"), file).split(path.sep);
  const folder = relative.length > 1 ? relative[0] : "基础通用";
  const known = new Set(["武器技能", "符箓", "妖兽", "业障"]);
  return { sourceType: known.has(folder) || folder === "基础通用" ? folder : "秘籍习得", sourceGongfa: known.has(folder) || folder === "基础通用" ? "" : folder };
}
function refDescription(name, reference) {
  const item = reference?.cards?.[name];
  return item?.text || "";
}

function parseSkill(file, reference) {
  const text = read(file);
  const resource = text.slice(text.lastIndexOf("[resource]"));
  const ext = extResources(text);
  const sub = Object.fromEntries(subresourceBlocks(text).map(item => [item.id, item.text]));
  const damageBonusRefs = lineValue(resource, "damage_bonuses");
  const damageBonuses = [];
  for (const id of damageBonusRefs.matchAll(/SubResource\("([^"]+)"\)/g)) {
    const block = sub[id[1]] || "";
    if (block) damageBonuses.push({ attribute: stringField(block, "attribute"), multiplier: numberField(block, "multiplier") });
  }
  const source = sourceInfo(file);
  const name = stringField(resource, "skill_name", path.basename(file, ".tres"));
  const keywords = arrayStrings(resource, "keywords");
  return {
    schemaVersion: 1, kind: "card", id: `project_${safeId(name, "card")}_${stableKey(path.relative(root, file))}`,
    projectSource: true, skill_name: name, resource_path: normalizedPath(file), icon_path: stringField(resource, "icon_path"),
    description: stringField(resource, "description") || refDescription(name, reference), skill_type: skillTypes[numberField(resource, "skill_type")] || "ATTACK",
    element: stringField(resource, "element", "NONE"), source_type: source.sourceType, source_gongfa: source.sourceGongfa,
    required_realm: stringField(resource, "required_realm"), required_attributes: dictField(resource, "required_attributes"),
    energy_cost: numberField(resource, "energy_cost"), mana_cost: numberField(resource, "mana_cost"), spirit_occupy: numberField(resource, "spirit_occupy"),
    required_equip_slot: numberField(resource, "required_equip_slot", -1), required_weapon_type: numberField(resource, "required_weapon_type"),
    target_type: numberField(resource, "target_type"), range_min: numberField(resource, "range_min"), range_max: numberField(resource, "range_max"),
    aoe_range: numberField(resource, "aoe_range"), can_target_empty: boolField(resource, "can_target_empty"), move_distance: numberField(resource, "move_distance"),
    keywords, condition_type: stringField(resource, "condition_type", "NONE"), condition_value: stringField(resource, "condition_value"),
    momentum_condition_type: stringField(resource, "momentum_condition_type"), momentum_effect_params: stringField(resource, "momentum_effect_params"),
    flow_condition_type: stringField(resource, "flow_condition_type"), flow_condition_params: arrayStrings(resource, "flow_condition_params"), counter_condition: stringField(resource, "counter_condition"),
    base_damage: numberField(resource, "base_damage"), damage_type: stringField(resource, "damage_type", "physical"), damage_formula: stringField(resource, "damage_formula"), damage_bonuses: damageBonuses,
    status_effects: dictionaryArray(resource, "status_effects").map(item => ({ status_id: item.status_id || "", stacks: item.stacks || 1, hit_formula: item.hit_formula || item.formula || "1.0" })),
    special_effects: dictionaryArray(resource, "special_effects"), stacks_formula: stringField(resource, "stacks_formula"), move_formula: stringField(resource, "move_formula")
  };
}

function rewriteFromBlock(block, ext) {
  const sourcePath = refPaths(lineValue(block, "source_skill"), ext)[0] || "";
  const resultPath = refPaths(lineValue(block, "result_skill"), ext)[0] || "";
  const mainPath = refPaths(lineValue(block, "main_result_skill"), ext)[0] || "";
  return { enabled: boolField(block, "enabled", true), mode: numberField(block, "mode") === 1 ? "REPLACE" : "ADD", count: numberField(block, "count", 1), source_skill_path: sourcePath, source_skill_name: stringField(block, "source_skill_name"), result_skill_path: resultPath, main_result_skill_path: mainPath };
}
function parseChapter(block, ext, rewrites) {
  const rewriteIds = arrayRefIds(block, "sub_deck_rewrites").concat(arrayRefIds(block, "main_deck_rewrites")).map(value => value.match(/SubResource\("([^"]+)"\)/)?.[1]).filter(Boolean);
  const rewrite = rewriteIds.map(id => rewrites[id]).filter(Boolean);
  return {
    chapter_id: stringField(block, "chapter_id"), chapter_name: stringField(block, "chapter_name"), description: stringField(block, "description"), order: numberField(block, "order", 1),
    required_study_progress: numberField(block, "required_study_progress", 8), required_realm: stringField(block, "required_realm"), required_attributes: dictField(block, "required_attributes"),
    required_chapter_ids: arrayStrings(block, "required_chapter_ids"), allowed_bearings: arrayNumbers(block, "allowed_bearings"), requires_main_slot: boolField(block, "requires_main_slot"),
    left_branch_name: stringField(block, "left_branch_name", "淬炼"), right_branch_name: stringField(block, "right_branch_name", "化法"),
    learn_permanent_bonuses: dictField(block, "learn_permanent_bonuses"), learn_permanent_multipliers: dictField(block, "learn_permanent_multipliers"),
    naqi_bonuses: dictField(block, "naqi_bonuses"), naqi_multipliers: dictField(block, "naqi_multipliers"), main_naqi_bonuses: dictField(block, "main_naqi_bonuses"), main_naqi_multipliers: dictField(block, "main_naqi_multipliers"),
    learned_active_skills: refPaths(lineValue(block, "learned_active_skills"), ext), main_learned_active_skills: refPaths(lineValue(block, "main_learned_active_skills"), ext),
    sub_deck_rewrites: rewrite.filter((_, index) => index < arrayRefIds(block, "sub_deck_rewrites").length), main_deck_rewrites: rewrite.slice(arrayRefIds(block, "sub_deck_rewrites").length)
  };
}
function parseGongfa(file) {
  const text = read(file);
  const resource = text.slice(text.lastIndexOf("[resource]"));
  const ext = extResources(text);
  const sub = Object.fromEntries(subresourceBlocks(text).map(item => [item.id, item.text]));
  const rewriteBlocks = Object.fromEntries(Object.values(sub).filter(block => /\bmode\s*=/.test(block)).map(block => [block.match(/id="([^"]+)"/)?.[1] || "", rewriteFromBlock(block, ext)]));
  const chapterIds = arrayRefIds(resource, "chapters").map(value => value.match(/SubResource\("([^"]+)"\)/)?.[1]).filter(Boolean);
  const name = stringField(resource, "gongfa_name", path.basename(file, ".tres"));
  return {
    schemaVersion: 1, kind: "manual", id: `project_${safeId(name, "manual")}_${stableKey(path.relative(root, file))}`, projectSource: true, gongfa_name: name, resource_path: normalizedPath(file), description: stringField(resource, "description"),
    wuxing: wuxing[numberField(resource, "wuxing")] || "NONE", wuxing_requirement_mode: numberField(resource, "wuxing_requirement_mode") === 1 ? "AND" : "OR", required_realm: stringField(resource, "required_realm"), required_study_progress: numberField(resource, "required_study_progress", 8), required_attributes: dictField(resource, "required_attributes"),
    can_be_main: boolField(resource, "can_be_main", true), can_be_sub: boolField(resource, "can_be_sub", true), bearing_compatibility: arrayNumbers(resource, "bearing_compatibility"), school_tags: arrayStrings(resource, "school_tags"), cross_bearing_policy: stringField(resource, "cross_bearing_policy", "NONE"),
    main_realm_start: stringField(resource, "main_realm_start"), main_realm_end: stringField(resource, "main_realm_end"), sub_realm_start: stringField(resource, "sub_realm_start"), sub_realm_end: stringField(resource, "sub_realm_end"),
    learn_permanent_bonuses: dictField(resource, "learn_permanent_bonuses"), learn_permanent_multipliers: dictField(resource, "learn_permanent_multipliers"), passive_bonuses: dictField(resource, "passive_bonuses"), passive_multipliers: dictField(resource, "passive_multipliers"),
    body_nourish_rate_per_day: numberField(resource, "body_nourish_rate_per_day", 0.8), body_nourish_weights: dictField(resource, "body_nourish_weights"), default_unlocked_chapter_count: numberField(resource, "default_unlocked_chapter_count", 1),
    granted_active_skills: refPaths(lineValue(resource, "granted_active_skills"), ext), heart_method_skill: refPaths(lineValue(resource, "heart_method_skill"), ext)[0] || "", sub_heart_method_skill: refPaths(lineValue(resource, "sub_heart_method_skill"), ext)[0] || "", main_heart_method_skill: refPaths(lineValue(resource, "main_heart_method_skill"), ext)[0] || "",
    general_breakthrough_multipliers: dictField(resource, "general_breakthrough_multipliers"), general_breakthrough_fixed: dictField(resource, "general_breakthrough_fixed"),
    chapters: chapterIds.map(id => parseChapter(sub[id] || "", ext, rewriteBlocks)).filter(chapter => chapter.chapter_id).sort((a, b) => a.order - b.order)
  };
}
function parseRealm(file) {
  const text = read(file);
  const resource = text.slice(text.lastIndexOf("[resource]"));
  return { name: stringField(resource, "realm_name", path.basename(file, ".tres")), required_attributes: dictField(resource, "required_attributes"), battle_base_attributes: dictField(resource, "battle_base_attributes"), realm_multiplier_bonuses: dictField(resource, "realm_multiplier_bonuses"), realm_fixed_bonuses: dictField(resource, "realm_fixed_bonuses"), requires_main_gongfa: boolField(resource, "requires_main_gongfa"), is_mortal: boolField(resource, "is_mortal") };
}

const referenceFile = path.join(root, "resources", "card_reference.json");
const reference = fs.existsSync(referenceFile) ? JSON.parse(read(referenceFile)) : {};
const cards = filesUnder(path.join(root, "resources", "activeskills"), ".tres").map(file => parseSkill(file, reference)).sort((a, b) => a.skill_name.localeCompare(b.skill_name, "zh-CN"));
const manuals = filesUnder(path.join(root, "resources", "gongfa"), ".tres").filter(file => !file.endsWith("功法配置.tres")).map(parseGongfa).sort((a, b) => a.gongfa_name.localeCompare(b.gongfa_name, "zh-CN"));
const realms = filesUnder(path.join(root, "resources", "realm"), ".tres").sort((a, b) => path.basename(a).localeCompare(path.basename(b), "en")).map(parseRealm);
const catalog = { generatedAt: new Date().toISOString(), project: "青溪问道", cards, manuals, realms };
fs.writeFileSync(output, `window.PROJECT_CATALOG = ${JSON.stringify(catalog, null, 2)};\n`, "utf8");
console.log(`generated ${cards.length} cards, ${manuals.length} manuals, ${realms.length} realms -> ${output}`);
