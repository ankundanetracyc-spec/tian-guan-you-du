(() => {
  "use strict";

  const CATALOG = window.PROJECT_CATALOG || { cards: [], manuals: [], realms: [] };
  const STORAGE_KEY = "qingxi-card-studio-v1";
  const SYNC_META_KEY = "qingxi-card-studio-sync-v1";
  const SKILL_TYPES = {
    ATTACK: "攻伐牌", MOVEMENT: "遁身牌", DRAW: "抽牌牌", BUFF: "守御牌",
    WAIT: "等待牌", SPECIAL: "术诀牌", POWER: "心法牌", CURSE: "业障牌"
  };
  const SOURCE_TYPES = ["基础通用", "秘籍习得", "武器技能", "符箓", "妖兽", "业障", "自定义"];
  const TARGET_TYPES = [[0, "自身"], [1, "友方单体"], [2, "单体敌人"], [3, "指定目标"], [4, "范围目标"]];
  const KEYWORDS = ["固有", "应对", "消耗", "乘势", "保留"];
  const DAMAGE_TYPES = [["physical", "物理"], ["magic", "法术"], ["spiritual", "法术/灵力"], ["soul", "神魂"]];
  const CONDITIONS = [["NONE", "无"], ["MAIN_GONGFA", "主修秘籍"], ["SUB_GONGFA", "辅修秘籍"], ["ANY_GONGFA", "任意秘籍"]];
  const COUNTER_CONDITIONS = [["", "无"], ["on_attacked", "敌方攻击自身时"], ["on_damaged", "自身受伤时"], ["on_spell_hit", "术法命中时"]];
  const WUXING = ["NONE", "METAL", "WOOD", "WATER", "FIRE", "EARTH", "METAL_WOOD", "METAL_WATER", "METAL_FIRE", "METAL_EARTH", "WOOD_WATER", "WOOD_FIRE", "WOOD_EARTH", "WATER_FIRE", "WATER_EARTH", "FIRE_EARTH", "METAL_WOOD_WATER", "METAL_WOOD_FIRE", "METAL_WOOD_EARTH", "METAL_WATER_FIRE", "METAL_WATER_EARTH", "METAL_FIRE_EARTH", "WOOD_WATER_FIRE", "WOOD_WATER_EARTH", "WOOD_FIRE_EARTH", "WATER_FIRE_EARTH", "METAL_WOOD_WATER_FIRE", "METAL_WOOD_WATER_EARTH", "METAL_WOOD_FIRE_EARTH", "METAL_WATER_FIRE_EARTH", "WOOD_WATER_FIRE_EARTH", "METAL_WOOD_WATER_FIRE_EARTH", "ALL"];
  const WUXING_LABELS = { NONE: "无要求", METAL: "金", WOOD: "木", WATER: "水", FIRE: "火", EARTH: "土", ALL: "通用" };
  const WUXING_DISPLAY = value => WUXING_LABELS[value] || value.replaceAll("_", "·");
  const BEARINGS = { "1": "BODY", "2": "QI", "3": "SOUL", BODY: 1, QI: 2, SOUL: 3 };
  const ATTRIBUTE_OPTIONS = [
    ["physique", "体魄"], ["intelligence", "灵慧"], ["agility", "敏捷"], ["soul", "神魂"],
    ["body_proficiency", "体道造诣"], ["sword_proficiency", "剑道造诣"], ["blade_proficiency", "刀道造诣"],
    ["spear_proficiency", "枪道造诣"], ["magic_proficiency", "术法造诣"], ["max_health", "生命上限"],
    ["max_stamina", "精力上限"], ["max_mana", "法力上限"], ["max_spirit_perception", "灵识容量"],
    ["physical_defense", "物理防御"], ["precision", "精准"], ["dodge", "闪避"], ["critical", "会心"],
    ["crit_resist", "抗暴"], ["initiative_speed", "行动速度"], ["learning_rate", "学习速率"], ["cultivation_rate", "修炼速率"]
  ];
  const ATTRIBUTE_LABELS = Object.fromEntries(ATTRIBUTE_OPTIONS);
  const REQUIRED_ATTRIBUTE_KEYS = new Set(["physique", "intelligence", "agility", "soul", "sword_proficiency", "blade_proficiency", "spear_proficiency", "body_proficiency", "magic_proficiency"]);
  const FORBIDDEN_REQUIRED_KEYS = new Set(["fortune", "comprehension", "charisma", "spirit_strands", "core_count", "max_health", "max_stamina", "max_mind", "max_mana", "max_spirit_perception", "physical_defense", "precision", "dodge", "critical", "crit_resist", "initiative_speed", "health_regen_rate", "stamina_regen_rate", "mana_regen_rate"]);
  const EFFECT_OPTIONS = [["gain_energy", "获得精力"], ["restore_stamina", "恢复精力"], ["restore_mana", "恢复法力"], ["draw", "抽牌"], ["shield", "获得护持"], ["heal", "恢复生命"], ["damage", "追加伤害"], ["gain_qi_blood", "获得气血"], ["gain_sword_intent", "获得剑意"], ["gain_punch_frame", "获得拳架"], ["add_status", "施加状态"], ["remove_status", "移除状态"]];
  const EFFECT_LABELS = Object.fromEntries(EFFECT_OPTIONS);
  const STATUS_LABELS = { physique: "体魄", intelligence: "灵慧", agility: "敏捷", soul: "神魂", body_proficiency: "体道造诣", sword_proficiency: "剑道造诣" };
  const REALMS = Array.isArray(CATALOG.realms) && CATALOG.realms.length ? CATALOG.realms : [{ name: "凡人", battle_base_attributes: { initial_hand_size: 4, cards_per_turn: 2 } }];
  const REALM_NAMES = REALMS.map(item => item.name);
  const realmIndex = value => REALM_NAMES.indexOf(value);
  const byId = id => document.getElementById(id);
  const valueOf = id => byId(id)?.value ?? "";
  const numberOf = (id, fallback = 0) => { const value = Number(valueOf(id)); return Number.isFinite(value) ? value : fallback; };
  const boolOf = id => Boolean(byId(id)?.checked);
  const clone = value => JSON.parse(JSON.stringify(value));
  const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" }[char]));
  const uid = prefix => `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
  const slug = value => String(value || "未命名").trim().toLowerCase().replace(/[^a-z0-9\u4e00-\u9fff]+/g, "_").replace(/^_|_$/g, "") || "未命名";
  const splitList = value => String(value || "").split(/[，,\n]/).map(item => item.trim()).filter(Boolean);
  const listText = list => (Array.isArray(list) ? list : []).join(", ");
  const formatNumber = value => Number.isInteger(Number(value)) ? String(Number(value)) : Number(value || 0).toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
  const nowIso = () => new Date().toISOString();

  function loadSyncMeta() {
    try {
      const value = JSON.parse(localStorage.getItem(SYNC_META_KEY) || "null");
      return { endpoint: value?.endpoint || "", lastRevision: Number(value?.lastRevision) || 0, lastSyncAt: value?.lastSyncAt || "", deleted: Array.isArray(value?.deleted) ? value.deleted : [], testConfigUpdatedAt: value?.testConfigUpdatedAt || "" };
    } catch (_) {
      return { endpoint: "", lastRevision: 0, lastSyncAt: "", deleted: [], testConfigUpdatedAt: "" };
    }
  }
  let syncMeta = loadSyncMeta();
  function saveSyncMeta() { try { localStorage.setItem(SYNC_META_KEY, JSON.stringify(syncMeta)); } catch (_) { /* storage may be blocked in private mode */ } }

  function defaultCard(overrides = {}) {
    return normalizeCard({
      schemaVersion: 1, kind: "card", id: uid("card"), projectSource: false,
      skill_name: "未命名卡牌", resource_path: "res://resources/activeskills/未命名卡牌.tres", icon_path: "",
      description: "", skill_type: "ATTACK", element: "NONE", source_type: "自定义", source_gongfa: "", required_realm: "", required_attributes: {},
      energy_cost: 1, mana_cost: 0, spirit_occupy: 0, required_equip_slot: -1, required_weapon_type: 0,
      target_type: 2, range_min: 1, range_max: 1, aoe_range: 0, can_target_empty: false, move_distance: 0,
      keywords: [], condition_type: "NONE", condition_value: "", momentum_condition_type: "", momentum_effect_params: "", flow_condition_type: "", flow_condition_params: [], counter_condition: "",
      base_damage: 0, damage_type: "physical", damage_formula: "", damage_bonuses: [], status_effects: [], special_effects: [], stacks_formula: "", move_formula: "",
      ...overrides
    });
  }
  function normalizeCard(raw = {}) {
    const card = { ...raw };
    card.kind = "card"; card.schemaVersion = 1; card.id ||= uid("card");
    card.skill_name = card.skill_name ?? card.name ?? "未命名卡牌"; card.resource_path = card.resource_path ?? card.resourcePath ?? "";
    card.skill_type = SKILL_TYPES[card.skill_type] ? card.skill_type : "ATTACK";
    card.keywords = Array.isArray(card.keywords) ? card.keywords : splitList(card.keywords);
    card.damage_bonuses = Array.isArray(card.damage_bonuses) ? card.damage_bonuses : [];
    card.status_effects = Array.isArray(card.status_effects) ? card.status_effects : [];
    card.special_effects = Array.isArray(card.special_effects) ? card.special_effects : [];
    card.required_attributes = card.required_attributes && typeof card.required_attributes === "object" && !Array.isArray(card.required_attributes) ? card.required_attributes : {};
    card.flow_condition_params = Array.isArray(card.flow_condition_params) ? card.flow_condition_params : splitList(card.flow_condition_params);
    for (const field of ["energy_cost", "mana_cost", "spirit_occupy", "required_equip_slot", "required_weapon_type", "target_type", "range_min", "range_max", "aoe_range", "move_distance", "base_damage"]) card[field] = Number.isFinite(Number(card[field])) ? Number(card[field]) : 0;
    card.required_equip_slot = raw.required_equip_slot === undefined ? -1 : card.required_equip_slot;
    return card;
  }
  function defaultChapter(order = 1) {
    return { chapter_id: `chapter_${order}`, chapter_name: `第${order}卷`, description: "", order, required_study_progress: 8, required_realm: "", required_attributes: {}, required_chapter_ids: [], allowed_bearings: [], requires_main_slot: false, left_branch_name: "淬炼", right_branch_name: "化法", learn_permanent_bonuses: {}, learn_permanent_multipliers: {}, naqi_bonuses: {}, naqi_multipliers: {}, main_naqi_bonuses: {}, main_naqi_multipliers: {}, learned_active_skills: [], main_learned_active_skills: [], sub_deck_rewrites: [], main_deck_rewrites: [] };
  }
  function defaultManual(overrides = {}) {
    return normalizeManual({
      schemaVersion: 1, kind: "manual", id: uid("manual"), projectSource: false, gongfa_name: "未命名秘籍", resource_path: "res://resources/gongfa/未命名秘籍.tres", description: "", wuxing: "NONE", wuxing_requirement_mode: "OR", required_realm: "", required_study_progress: 8, required_attributes: {}, can_be_main: true, can_be_sub: true, bearing_compatibility: [], school_tags: [], cross_bearing_policy: "NONE", main_realm_start: "", main_realm_end: "", sub_realm_start: "", sub_realm_end: "", learn_permanent_bonuses: {}, learn_permanent_multipliers: {}, passive_bonuses: {}, passive_multipliers: {}, body_nourish_rate_per_day: 0.8, body_nourish_weights: {}, default_unlocked_chapter_count: 1, granted_active_skills: [], heart_method_skill: "", sub_heart_method_skill: "", main_heart_method_skill: "", general_breakthrough_multipliers: {}, general_breakthrough_fixed: {}, chapters: [defaultChapter(1)], ...overrides
    });
  }
  function normalizeManual(raw = {}) {
    const manual = { ...raw };
    manual.kind = "manual"; manual.schemaVersion = 1; manual.id ||= uid("manual"); manual.gongfa_name = manual.gongfa_name ?? manual.name ?? "未命名秘籍"; manual.resource_path = manual.resource_path ?? manual.resourcePath ?? "";
    for (const key of ["required_attributes", "learn_permanent_bonuses", "learn_permanent_multipliers", "passive_bonuses", "passive_multipliers", "body_nourish_weights", "general_breakthrough_multipliers", "general_breakthrough_fixed"]) if (!manual[key] || typeof manual[key] !== "object" || Array.isArray(manual[key])) manual[key] = {};
    manual.school_tags = Array.isArray(manual.school_tags) ? manual.school_tags : splitList(manual.school_tags); manual.bearing_compatibility = Array.isArray(manual.bearing_compatibility) ? manual.bearing_compatibility : splitList(manual.bearing_compatibility).map(item => BEARINGS[item] ?? Number(item)).filter(Number.isFinite);
    manual.granted_active_skills = Array.isArray(manual.granted_active_skills) ? manual.granted_active_skills : splitList(manual.granted_active_skills);
    manual.chapters = Array.isArray(manual.chapters) && manual.chapters.length ? manual.chapters.map((chapter, index) => ({ ...defaultChapter(index + 1), ...chapter, required_attributes: chapter.required_attributes || {}, required_chapter_ids: Array.isArray(chapter.required_chapter_ids) ? chapter.required_chapter_ids : splitList(chapter.required_chapter_ids), learned_active_skills: Array.isArray(chapter.learned_active_skills) ? chapter.learned_active_skills : splitList(chapter.learned_active_skills), main_learned_active_skills: Array.isArray(chapter.main_learned_active_skills) ? chapter.main_learned_active_skills : splitList(chapter.main_learned_active_skills), sub_deck_rewrites: Array.isArray(chapter.sub_deck_rewrites) ? chapter.sub_deck_rewrites : [], main_deck_rewrites: Array.isArray(chapter.main_deck_rewrites) ? chapter.main_deck_rewrites : [] })) : [defaultChapter(1)];
    return manual;
  }

  function loadStore() {
    let local = null;
    try { local = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null"); } catch (_) { local = null; }
    const cards = Array.isArray(local?.cards) ? local.cards.map(normalizeCard) : [];
    const manuals = Array.isArray(local?.manuals) ? local.manuals.map(normalizeManual) : [];
    for (const card of (CATALOG.cards || []).map(normalizeCard)) if (!cards.some(item => item.id === card.id || (item.projectSource && item.resource_path === card.resource_path))) cards.push(card);
    for (const manual of (CATALOG.manuals || []).map(normalizeManual)) if (!manuals.some(item => item.id === manual.id || (item.projectSource && item.resource_path === manual.resource_path))) manuals.push(manual);
    if (!cards.some(card => !card.projectSource)) cards.push(defaultCard({ skill_name: "测试拳" , description: "对指定1名敌方造成物理伤害。伤害 = round(6 + 体魄×0.4)。", base_damage: 6, damage_formula: "round(6 + 体魄×0.4)", damage_bonuses: [{ attribute: "physique", multiplier: 0.4 }] }));
    if (!manuals.some(manual => !manual.projectSource)) manuals.push(defaultManual());
    for (const item of [...cards, ...manuals]) if (!item.projectSource && !item.updatedAt) item.updatedAt = nowIso();
    return { cards, manuals };
  }
  const store = loadStore();
  let activeView = "cards";
  let activeCardId = store.cards[0]?.id || "";
  let activeManualId = store.manuals[0]?.id || "";
  let activeChapterIndex = 0;
  let testDeckIds = [];
  let testState = null;

  function currentCard() { return store.cards.find(card => card.id === activeCardId) || store.cards[0]; }
  function currentManual() { return store.manuals.find(manual => manual.id === activeManualId) || store.manuals[0]; }
  function cardById(id) { return store.cards.find(card => card.id === id); }
  function cardByPath(path) { return store.cards.find(card => card.resource_path === path); }
  function ensureDraftTimestamps() { for (const item of [...store.cards, ...store.manuals]) if (!item.projectSource && !item.updatedAt) item.updatedAt = nowIso(); }
  function saveStore(silent = false) {
    ensureDraftTimestamps();
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(store)); } catch (_) { /* storage may be blocked in private mode */ }
    byId("saveIndicator").textContent = silent ? "已自动保存" : "已保存";
    byId("footerStatus").textContent = silent ? "本地草稿已保存" : "保存完成";
  }
  function toast(message, tone = "normal") {
    const node = byId("toast"); node.textContent = message; node.dataset.tone = tone; node.classList.add("show");
    clearTimeout(toast.timer); toast.timer = setTimeout(() => node.classList.remove("show"), 2600);
  }
  function setSelectOptions(id, options, selected, labels = null) {
    const select = byId(id); if (!select) return;
    const normalized = options.map(option => Array.isArray(option) ? option : [option, labels?.[option] || option]);
    const entries = [...normalized]; if (selected !== undefined && selected !== "" && !entries.some(item => String(item[0]) === String(selected))) entries.push([selected, selected]);
    select.innerHTML = entries.map(([value, label]) => `<option value="${escapeHtml(value)}">${escapeHtml(label)}</option>`).join("");
    select.value = selected ?? entries[0]?.[0] ?? "";
  }
  function setupStaticOptions() {
    setSelectOptions("cardSourceType", SOURCE_TYPES);
    setSelectOptions("cardSkillType", Object.keys(SKILL_TYPES), undefined, SKILL_TYPES);
    setSelectOptions("cardElement", WUXING, undefined, Object.fromEntries(WUXING.map(item => [item, WUXING_DISPLAY(item)])));
    setSelectOptions("cardTargetType", TARGET_TYPES);
    setSelectOptions("cardConditionType", CONDITIONS);
    setSelectOptions("cardCounterCondition", COUNTER_CONDITIONS);
    setSelectOptions("cardDamageType", DAMAGE_TYPES);
    setSelectOptions("manualWuxing", WUXING, undefined, Object.fromEntries(WUXING.map(item => [item, WUXING_DISPLAY(item)])));
    setSelectOptions("manualWuxingMode", [["OR", "满足任一属性"], ["AND", "满足全部属性"]]);
    setSelectOptions("manualCrossBearingPolicy", [["NONE", "无"], ["LOW_TIER_ONLY", "仅低阶"], ["PERMANENT_LEARNED_ONLY", "仅永久习得"], ["FULL", "完整兼容"]]);
    setSelectOptions("testRealmSelect", REALM_NAMES);
    setSelectOptions("testManualSelect", store.manuals.map(manual => [manual.id, manual.gongfa_name]));
  }
  function setField(id, value) { const node = byId(id); if (node) node.value = value ?? ""; }
  function setCheck(id, value) { const node = byId(id); if (node) node.checked = Boolean(value); }
  function typeLabel(type) { return SKILL_TYPES[type] || type || "未定"; }
  function targetLabel(type) { return TARGET_TYPES.find(item => Number(item[0]) === Number(type))?.[1] || `目标 ${type}`; }
  function sourceBadge(card) { return card.projectSource ? "项目现有" : "本地草稿"; }

  function renderCardList() {
    const query = valueOf("cardSearch").trim().toLowerCase();
    const list = store.cards.filter(card => !query || [card.skill_name, card.source_gongfa, card.source_type, ...(card.keywords || [])].join(" ").toLowerCase().includes(query));
    byId("cardCount").textContent = `${store.cards.length} 张`;
    byId("cardList").innerHTML = list.map(card => `<button class="item-row ${card.id === activeCardId ? "active" : ""}" data-card-id="${escapeHtml(card.id)}" type="button"><span class="item-main"><strong>${escapeHtml(card.skill_name)}</strong><small>${escapeHtml(typeLabel(card.skill_type))} · 精力 ${formatNumber(card.energy_cost)} · ${escapeHtml(card.source_gongfa || card.source_type || "自定义")}</small></span><span class="item-badge ${card.projectSource ? "project" : "draft"}">${sourceBadge(card)}</span></button>`).join("") || `<div class="empty-state">没有匹配的卡牌</div>`;
  }
  function renderManualList() {
    const query = valueOf("manualSearch").trim().toLowerCase();
    const list = store.manuals.filter(manual => !query || [manual.gongfa_name, manual.description, ...(manual.school_tags || []), ...(manual.chapters || []).map(chapter => chapter.chapter_name)].join(" ").toLowerCase().includes(query));
    byId("manualCount").textContent = `${store.manuals.length} 本`;
    byId("manualList").innerHTML = list.map(manual => `<button class="item-row ${manual.id === activeManualId ? "active" : ""}" data-manual-id="${escapeHtml(manual.id)}" type="button"><span class="item-main"><strong>${escapeHtml(manual.gongfa_name)}</strong><small>${escapeHtml(manual.school_tags?.join(" · ") || "未标流派")} · ${manual.chapters?.length || 0} 卷</small></span><span class="item-badge ${manual.projectSource ? "project" : "draft"}">${manual.projectSource ? "项目现有" : "本地草稿"}</span></button>`).join("") || `<div class="empty-state">没有匹配的秘籍</div>`;
  }

  function mapRows(map, owner, placeholder = "属性键") {
    const entries = Object.entries(map || {});
    if (!entries.length) return `<div class="empty-row">暂无条目，点击右侧添加</div>`;
    return entries.map(([key, value]) => `<div class="map-row" data-owner="${escapeHtml(owner)}"><input class="map-key" data-map-key value="${escapeHtml(key)}" placeholder="${escapeHtml(placeholder)}"><input class="map-value" data-map-value type="number" step="0.01" value="${escapeHtml(value)}"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`).join("");
  }
  function repeatRows(list, kind, renderer, empty = "暂无条目，点击右侧添加") {
    if (!Array.isArray(list) || !list.length) return `<div class="empty-row">${empty}</div>`;
    return list.map((item, index) => renderer(item, index, kind)).join("");
  }
  function renderKeywordChecks(keywords) {
    byId("cardKeywords").innerHTML = KEYWORDS.map(keyword => `<label class="keyword-toggle"><input type="checkbox" value="${escapeHtml(keyword)}" ${keywords.includes(keyword) ? "checked" : ""}><span>${escapeHtml(keyword)}</span></label>`).join("");
  }
  function renderDamageRows(list) {
    byId("cardDamageBonuses").innerHTML = repeatRows(list, "damage-bonus", item => `<div class="repeat-row" data-row-kind="damage-bonus"><select data-bonus-attribute>${ATTRIBUTE_OPTIONS.map(([value, label]) => `<option value="${value}" ${item.attribute === value ? "selected" : ""}>${label}</option>`).join("")}</select><input data-bonus-multiplier type="number" step="0.05" value="${escapeHtml(item.multiplier ?? 0)}" placeholder="倍率"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`);
  }
  function renderStatusRows(list) {
    byId("cardStatusEffects").innerHTML = repeatRows(list, "status", item => `<div class="repeat-row" data-row-kind="status"><input data-status-id value="${escapeHtml(item.status_id || "")}" placeholder="状态 ID，例如：化解"><input data-status-stacks type="number" min="1" step="1" value="${escapeHtml(item.stacks ?? 1)}" placeholder="层数"><input data-status-formula value="${escapeHtml(item.hit_formula || item.formula || "1.0")}" placeholder="命中公式"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`);
  }
  function renderSpecialRows(list) {
    byId("cardSpecialEffects").innerHTML = repeatRows(list, "special", item => `<div class="repeat-row special-row" data-row-kind="special"><select data-special-effect>${EFFECT_OPTIONS.map(([value, label]) => `<option value="${value}" ${item.effect === value ? "selected" : ""}>${label}</option>`).join("")}</select><input data-special-count type="number" min="0" step="1" value="${escapeHtml(item.count ?? 1)}" placeholder="数量"><input data-special-formula value="${escapeHtml(item.formula || "")}" placeholder="公式/状态 ID"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`);
  }
  function cardDescription(card) {
    if (card.description) return card.description;
    const lines = [];
    if (card.base_damage || card.damage_formula) lines.push(`造成${card.damage_type === "soul" ? "神魂" : card.damage_type === "magic" || card.damage_type === "spiritual" ? "法术" : "物理"}伤害${card.damage_formula ? `：${card.damage_formula}` : `：${formatNumber(card.base_damage)}点`}`);
    for (const effect of card.special_effects || []) lines.push(`${EFFECT_LABELS[effect.effect] || effect.effect || "效果"}${effect.count ? ` ${effect.count}` : ""}${effect.formula ? `（${effect.formula}）` : ""}`);
    for (const status of card.status_effects || []) if (status.status_id) lines.push(`获得${status.status_id}${status.stacks ? ` ${status.stacks}层` : ""}`);
    return lines.join("；") || "施展本牌效果。";
  }
  function renderCardPreview(card) {
    const keywords = card.keywords?.length ? card.keywords.join(" · ") : "无关键词";
    byId("cardPreview").innerHTML = `<div class="preview-card-inner"><div class="preview-topline"><span>${escapeHtml(typeLabel(card.skill_type))}</span><span>${escapeHtml(WUXING_DISPLAY(card.element || "NONE"))}</span></div><div class="preview-title">${escapeHtml(card.skill_name || "未命名卡牌")}</div><div class="preview-costs"><span class="cost energy">精 ${formatNumber(card.energy_cost)}</span><span class="cost mana">法 ${formatNumber(card.mana_cost)}</span><span class="cost spirit">识 ${formatNumber(card.spirit_occupy)}</span></div><div class="preview-target">${escapeHtml(targetLabel(card.target_type))} · ${formatNumber(card.range_min)}—${formatNumber(card.range_max)}格</div><div class="preview-effect">${escapeHtml(cardDescription(card)).replaceAll("\n", "<br>")}</div><div class="preview-keywords">${escapeHtml(keywords)}</div></div>`;
  }
  function renderCardEditor(card = currentCard()) {
    if (!card) return;
    byId("cardFormTitle").textContent = card.skill_name || "未命名卡牌";
    setField("cardName", card.skill_name); setField("cardResourcePath", card.resource_path); setField("cardSourceType", card.source_type || "自定义"); setField("cardSourceGongfa", card.source_gongfa); setField("cardSkillType", card.skill_type); setField("cardElement", card.element || "NONE"); setField("cardRequiredRealm", card.required_realm); byId("cardRequiredAttributes").innerHTML = mapRows(card.required_attributes, "cardRequiredAttributes"); setField("cardDescription", card.description);
    setField("cardEnergyCost", card.energy_cost); setField("cardManaCost", card.mana_cost); setField("cardSpiritOccupy", card.spirit_occupy); setField("cardTargetType", card.target_type); setField("cardRangeMin", card.range_min); setField("cardRangeMax", card.range_max); setField("cardAoeRange", card.aoe_range); setField("cardMoveDistance", card.move_distance); setCheck("cardCanTargetEmpty", card.can_target_empty);
    renderKeywordChecks(card.keywords || []); setField("cardConditionType", card.condition_type || "NONE"); setField("cardConditionValue", card.condition_value); setField("cardCounterCondition", card.counter_condition); setField("cardFlowConditionType", card.flow_condition_type); setField("cardFlowConditionParams", listText(card.flow_condition_params)); setField("cardMomentumConditionType", card.momentum_condition_type); setField("cardMomentumEffectParams", card.momentum_effect_params);
    setField("cardBaseDamage", card.base_damage); setField("cardDamageType", card.damage_type || "physical"); setField("cardDamageFormula", card.damage_formula); renderDamageRows(card.damage_bonuses); renderStatusRows(card.status_effects); renderSpecialRows(card.special_effects);
    renderCardPreview(card); renderCardValidation(card);
  }
  function readMap(containerId) {
    const result = {}; document.querySelectorAll(`#${containerId} .map-row`).forEach(row => { const key = row.querySelector("[data-map-key]")?.value.trim(); const value = Number(row.querySelector("[data-map-value]")?.value); if (key && Number.isFinite(value)) result[key] = value; }); return result;
  }
  function readCardRows() {
    const damage_bonuses = [...document.querySelectorAll("#cardDamageBonuses .repeat-row")].map(row => ({ attribute: row.querySelector("[data-bonus-attribute]")?.value || "", multiplier: Number(row.querySelector("[data-bonus-multiplier]")?.value) || 0 }));
    const status_effects = [...document.querySelectorAll("#cardStatusEffects .repeat-row")].map(row => ({ status_id: row.querySelector("[data-status-id]")?.value.trim() || "", stacks: Number(row.querySelector("[data-status-stacks]")?.value) || 1, hit_formula: row.querySelector("[data-status-formula]")?.value.trim() || "1.0" }));
    const special_effects = [...document.querySelectorAll("#cardSpecialEffects .repeat-row")].map(row => ({ effect: row.querySelector("[data-special-effect]")?.value || "", count: Number(row.querySelector("[data-special-count]")?.value) || 0, formula: row.querySelector("[data-special-formula]")?.value.trim() || "" }));
    return { damage_bonuses, status_effects, special_effects };
  }
  function syncCardFromForm() {
    const card = currentCard(); if (!card) return;
    Object.assign(card, { skill_name: valueOf("cardName").trim() || "未命名卡牌", resource_path: valueOf("cardResourcePath").trim(), source_type: valueOf("cardSourceType"), source_gongfa: valueOf("cardSourceGongfa").trim(), skill_type: valueOf("cardSkillType"), element: valueOf("cardElement"), required_realm: valueOf("cardRequiredRealm").trim(), required_attributes: readMap("cardRequiredAttributes"), description: valueOf("cardDescription"), energy_cost: numberOf("cardEnergyCost"), mana_cost: numberOf("cardManaCost"), spirit_occupy: numberOf("cardSpiritOccupy"), target_type: numberOf("cardTargetType"), range_min: numberOf("cardRangeMin"), range_max: numberOf("cardRangeMax"), aoe_range: numberOf("cardAoeRange"), move_distance: numberOf("cardMoveDistance"), can_target_empty: boolOf("cardCanTargetEmpty"), keywords: [...document.querySelectorAll("#cardKeywords input:checked")].map(input => input.value), condition_type: valueOf("cardConditionType"), condition_value: valueOf("cardConditionValue").trim(), counter_condition: valueOf("cardCounterCondition"), flow_condition_type: valueOf("cardFlowConditionType").trim(), flow_condition_params: splitList(valueOf("cardFlowConditionParams")), momentum_condition_type: valueOf("cardMomentumConditionType").trim(), momentum_effect_params: valueOf("cardMomentumEffectParams").trim(), base_damage: numberOf("cardBaseDamage"), damage_type: valueOf("cardDamageType"), damage_formula: valueOf("cardDamageFormula").trim(), ...readCardRows() });
    if (!card.projectSource) card.updatedAt = nowIso();
    byId("cardFormTitle").textContent = card.skill_name; renderCardPreview(card); renderCardValidation(card); renderCardList(); saveStore(true);
  }

  function cardValidation(card) {
    const messages = [];
    if (!card.skill_name?.trim()) messages.push(["error", "卡牌名不能为空"]);
    if (card.energy_cost < 0 || card.energy_cost > 3) messages.push(["error", "精力费用必须在 0—3"]);
    if (card.mana_cost < 0 || card.mana_cost > 3) messages.push(["error", "法力费用常态范围为 0—3"]);
    if (card.spirit_occupy < 0 || card.spirit_occupy > 3) messages.push(["error", "灵识占用必须在 0—3"]);
    if (card.energy_cost === 0 && !card.keywords.some(keyword => ["应对", "消耗", "固有", "保留"].includes(keyword))) messages.push(["warning", "0 费牌需要次数、条件、消耗或其他严格限制"]);
    if (card.keywords.includes("应对") && !card.counter_condition) messages.push(["error", "应对牌需要填写触发条件"]);
    if (card.keywords.includes("固有") && card.skill_type !== "POWER") messages.push(["warning", "固有牌通常应按心法牌或明确的持续效果处理"]);
    if (card.keywords.includes("消耗") && card.skill_type === "POWER") messages.push(["error", "心法牌不能使用消耗关键词"]);
    if (card.spirit_occupy > 0 && !["POWER", "BUFF", "SPECIAL"].includes(card.skill_type)) messages.push(["warning", "灵识占用只适合祭出后持续留场的实体"]);
    if (card.skill_type !== "ATTACK" && (card.base_damage > 0 || card.damage_formula || card.damage_bonuses.some(item => item.multiplier))) messages.push(["error", "直接造成伤害的牌应归为攻伐牌"]);
    if (card.damage_type === "soul" && !card.damage_formula) messages.push(["warning", "神魂伤害应在公式中写明神魂差值、权重和上下限"]);
    if (/[“”‘’]/.test(card.description || "")) messages.push(["warning", "展示文案建议不使用中文引号"]);
    for (const status of card.status_effects) if (!status.status_id) messages.push(["error", "状态效果存在空的状态 ID"]);
    for (const effect of card.special_effects) if (!effect.effect) messages.push(["error", "即时效果存在空的效果 ID"]);
    if (!card.resource_path?.startsWith("res://")) messages.push(["warning", "资源路径建议使用 res:// 开头"]);
    return messages;
  }
  function renderCardValidation(card) {
    const messages = cardValidation(card); const errors = messages.filter(item => item[0] === "error").length; const warnings = messages.filter(item => item[0] === "warning").length;
    const badge = byId("cardValidationBadge"); badge.className = `validation-badge ${errors ? "error" : warnings ? "warning" : "success"}`; badge.textContent = errors ? `${errors} 项错误` : warnings ? `${warnings} 项提醒` : "规则通过";
    byId("cardValidationList").innerHTML = messages.length ? messages.map(([kind, message]) => `<div class="validation-line ${kind}"><span>${kind === "error" ? "!" : "·"}</span>${escapeHtml(message)}</div>`).join("") : `<div class="validation-line success"><span>✓</span>基础字段和当前规则检查通过</div>`;
  }

  function renderManualEditor(manual = currentManual()) {
    if (!manual) return;
    byId("manualFormTitle").textContent = manual.gongfa_name || "未命名秘籍";
    setField("manualName", manual.gongfa_name); setField("manualResourcePath", manual.resource_path); setField("manualWuxing", manual.wuxing || "NONE"); setField("manualWuxingMode", manual.wuxing_requirement_mode || "OR"); setField("manualRequiredRealm", manual.required_realm); setField("manualRequiredStudyProgress", manual.required_study_progress); setField("manualMainRealmStart", manual.main_realm_start); setField("manualMainRealmEnd", manual.main_realm_end); setField("manualSubRealmStart", manual.sub_realm_start); setField("manualSubRealmEnd", manual.sub_realm_end); setCheck("manualCanBeMain", manual.can_be_main); setCheck("manualCanBeSub", manual.can_be_sub); setField("manualDescription", manual.description); setField("manualSchoolTags", listText(manual.school_tags)); setField("manualBearingCompatibility", manual.bearing_compatibility.map(item => BEARINGS[item] || item).join(", ")); setField("manualCrossBearingPolicy", manual.cross_bearing_policy || "NONE"); setField("manualDefaultChapterCount", manual.default_unlocked_chapter_count); setField("manualBodyNourishRate", manual.body_nourish_rate_per_day);
    byId("manualLearnPermanentBonuses").innerHTML = mapRows(manual.learn_permanent_bonuses, "manualLearnPermanentBonuses"); byId("manualLearnPermanentMultipliers").innerHTML = mapRows(manual.learn_permanent_multipliers, "manualLearnPermanentMultipliers"); byId("manualPassiveBonuses").innerHTML = mapRows(manual.passive_bonuses, "manualPassiveBonuses"); byId("manualPassiveMultipliers").innerHTML = mapRows(manual.passive_multipliers, "manualPassiveMultipliers");
    renderChapterList(); renderChapterEditor(); renderManualValidation(manual);
  }
  function renderChapterList() {
    const manual = currentManual(); if (!manual) return;
    if (activeChapterIndex >= manual.chapters.length) activeChapterIndex = Math.max(0, manual.chapters.length - 1);
    byId("chapterList").innerHTML = manual.chapters.map((chapter, index) => `<button class="chapter-row ${index === activeChapterIndex ? "active" : ""}" data-chapter-index="${index}" type="button"><span class="chapter-order">${chapter.order || index + 1}</span><span><strong>${escapeHtml(chapter.chapter_name || "未命名卷章")}</strong><small>${escapeHtml(chapter.chapter_id || "无 ID")} · ${chapter.learned_active_skills?.length || 0} 张牌</small></span></button>`).join("");
  }
  function rewriteRows(list, group) {
    return repeatRows(list, "rewrite", item => `<div class="rewrite-row" data-rewrite-group="${group}"><select data-rewrite-mode><option value="ADD" ${item.mode === "ADD" ? "selected" : ""}>加入</option><option value="REPLACE" ${item.mode === "REPLACE" ? "selected" : ""}>替换</option></select><input data-rewrite-count type="number" min="1" step="1" value="${escapeHtml(item.count ?? 1)}" placeholder="数量"><input data-rewrite-source-name value="${escapeHtml(item.source_skill_name || "")}" placeholder="原基础牌（替换必填）"><input data-rewrite-result-path value="${escapeHtml(item.result_skill_path || "")}" placeholder="结果牌资源路径"><input data-rewrite-main-result-path value="${escapeHtml(item.main_result_skill_path || "")}" placeholder="主修结果牌路径"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`);
  }
  function renderChapterEditor() {
    const manual = currentManual(); const chapter = manual?.chapters?.[activeChapterIndex]; const node = byId("chapterEditor");
    if (!chapter) { node.innerHTML = `<div class="empty-state">请先新建卷章</div>`; return; }
    node.innerHTML = `<div class="chapter-editor-heading"><div><span class="section-kicker">第 ${chapter.order || activeChapterIndex + 1} 卷</span><h3>${escapeHtml(chapter.chapter_name || "未命名卷章")}</h3></div><button class="danger-button small" data-delete-chapter type="button">删除卷章</button></div><div class="form-grid two-columns"><label>卷章 ID<input id="chapterId" value="${escapeHtml(chapter.chapter_id)}" placeholder="稳定英文小写下划线 ID"></label><label>卷章名<input id="chapterName" value="${escapeHtml(chapter.chapter_name)}"></label><label>顺序<input id="chapterOrder" type="number" min="1" step="1" value="${escapeHtml(chapter.order || 1)}"></label><label>研读值门槛<input id="chapterStudy" type="number" min="0" step="1" value="${escapeHtml(chapter.required_study_progress ?? 8)}"></label><label>需求境界<input id="chapterRealm" value="${escapeHtml(chapter.required_realm || "")}"></label><label>前置卷章 ID<input id="chapterRequiredIds" value="${escapeHtml(listText(chapter.required_chapter_ids))}" placeholder="用逗号分隔"></label><label>允许道途<input id="chapterBearings" value="${escapeHtml((chapter.allowed_bearings || []).map(item => BEARINGS[item] || item).join(", "))}" placeholder="BODY, QI, SOUL"></label><label class="checkbox-label"><input id="chapterMainOnly" type="checkbox" ${chapter.requires_main_slot ? "checked" : ""}><span>仅主修可学</span></label></div><label>卷章描述<textarea id="chapterDescription" rows="2">${escapeHtml(chapter.description || "")}</textarea><div class="form-grid two-columns"><label>左侧分支名<input id="chapterLeftBranch" value="${escapeHtml(chapter.left_branch_name || "淬炼")}"></label><label>右侧分支名<input id="chapterRightBranch" value="${escapeHtml(chapter.right_branch_name || "化法")}"></label><label>习得卡牌资源路径<input id="chapterLearnedSkills" value="${escapeHtml(listText(chapter.learned_active_skills))}" placeholder="用逗号分隔 res://..."></label><label>主修额外习得<input id="chapterMainLearnedSkills" value="${escapeHtml(listText(chapter.main_learned_active_skills))}" placeholder="用逗号分隔"></label></div><div class="dictionary-grid"><div><div class="array-heading"><span>研读属性门槛</span><button class="tiny-button" data-map-add="chapterRequiredAttributes" type="button">＋ 添加</button></div><div id="chapterRequiredAttributes" class="repeat-list map-list">${mapRows(chapter.required_attributes, "chapterRequiredAttributes")}</div></div><div><div class="array-heading"><span>学会固定加成</span><button class="tiny-button" data-map-add="chapterLearnBonuses" type="button">＋ 添加</button></div><div id="chapterLearnBonuses" class="repeat-list map-list">${mapRows(chapter.learn_permanent_bonuses, "chapterLearnBonuses")}</div></div><div><div class="array-heading"><span>学会倍率加成</span><button class="tiny-button" data-map-add="chapterLearnMultipliers" type="button">＋ 添加</button></div><div id="chapterLearnMultipliers" class="repeat-list map-list">${mapRows(chapter.learn_permanent_multipliers, "chapterLearnMultipliers")}</div></div><div><div class="array-heading"><span>纳气固定加成</span><button class="tiny-button" data-map-add="chapterNaqiBonuses" type="button">＋ 添加</button></div><div id="chapterNaqiBonuses" class="repeat-list map-list">${mapRows(chapter.naqi_bonuses, "chapterNaqiBonuses")}</div></div><div><div class="array-heading"><span>纳气倍率加成</span><button class="tiny-button" data-map-add="chapterNaqiMultipliers" type="button">＋ 添加</button></div><div id="chapterNaqiMultipliers" class="repeat-list map-list">${mapRows(chapter.naqi_multipliers, "chapterNaqiMultipliers")}</div></div></div><div class="array-heading effect-heading"><span>辅修化法</span><button class="tiny-button" data-add-rewrite="sub" type="button">＋ 添加</button></div><div id="chapterSubRewrites" class="rewrite-list">${rewriteRows(chapter.sub_deck_rewrites, "sub")}</div><div class="array-heading effect-heading"><span>主修化法</span><button class="tiny-button" data-add-rewrite="main" type="button">＋ 添加</button></div><div id="chapterMainRewrites" class="rewrite-list">${rewriteRows(chapter.main_deck_rewrites, "main")}</div>`;
  }
  function readRewriteList(containerId) {
    return [...document.querySelectorAll(`#${containerId} .rewrite-row`)].map(row => ({ enabled: true, mode: row.querySelector("[data-rewrite-mode]")?.value || "ADD", count: Number(row.querySelector("[data-rewrite-count]")?.value) || 1, source_skill_path: "", source_skill_name: row.querySelector("[data-rewrite-source-name]")?.value.trim() || "", result_skill_path: row.querySelector("[data-rewrite-result-path]")?.value.trim() || "", main_result_skill_path: row.querySelector("[data-rewrite-main-result-path]")?.value.trim() || "" }));
  }
  function syncChapterFromForm() {
    const manual = currentManual(); const chapter = manual?.chapters?.[activeChapterIndex]; if (!chapter || !byId("chapterId")) return;
    Object.assign(chapter, { chapter_id: valueOf("chapterId").trim(), chapter_name: valueOf("chapterName").trim(), order: numberOf("chapterOrder", 1), required_study_progress: numberOf("chapterStudy", 8), required_realm: valueOf("chapterRealm").trim(), required_chapter_ids: splitList(valueOf("chapterRequiredIds")), allowed_bearings: splitList(valueOf("chapterBearings")).map(item => BEARINGS[item] ?? Number(item)).filter(Number.isFinite), requires_main_slot: boolOf("chapterMainOnly"), description: valueOf("chapterDescription"), left_branch_name: valueOf("chapterLeftBranch").trim() || "淬炼", right_branch_name: valueOf("chapterRightBranch").trim() || "化法", learned_active_skills: splitList(valueOf("chapterLearnedSkills")), main_learned_active_skills: splitList(valueOf("chapterMainLearnedSkills")), required_attributes: readMap("chapterRequiredAttributes"), learn_permanent_bonuses: readMap("chapterLearnBonuses"), learn_permanent_multipliers: readMap("chapterLearnMultipliers"), naqi_bonuses: readMap("chapterNaqiBonuses"), naqi_multipliers: readMap("chapterNaqiMultipliers"), sub_deck_rewrites: readRewriteList("chapterSubRewrites"), main_deck_rewrites: readRewriteList("chapterMainRewrites") });
    if (!manual.projectSource) manual.updatedAt = nowIso();
    const heading = byId("chapterEditor")?.querySelector(".chapter-editor-heading h3"); if (heading) heading.textContent = chapter.chapter_name || "未命名卷章"; renderChapterList(); renderManualValidation(manual); saveStore(true);
  }
  function syncManualFromForm() {
    const manual = currentManual(); if (!manual) return;
    Object.assign(manual, { gongfa_name: valueOf("manualName").trim() || "未命名秘籍", resource_path: valueOf("manualResourcePath").trim(), wuxing: valueOf("manualWuxing"), wuxing_requirement_mode: valueOf("manualWuxingMode"), required_realm: valueOf("manualRequiredRealm").trim(), required_study_progress: numberOf("manualRequiredStudyProgress", 8), main_realm_start: valueOf("manualMainRealmStart").trim(), main_realm_end: valueOf("manualMainRealmEnd").trim(), sub_realm_start: valueOf("manualSubRealmStart").trim(), sub_realm_end: valueOf("manualSubRealmEnd").trim(), can_be_main: boolOf("manualCanBeMain"), can_be_sub: boolOf("manualCanBeSub"), description: valueOf("manualDescription"), school_tags: splitList(valueOf("manualSchoolTags")), bearing_compatibility: splitList(valueOf("manualBearingCompatibility")).map(item => BEARINGS[item] ?? Number(item)).filter(Number.isFinite), cross_bearing_policy: valueOf("manualCrossBearingPolicy"), default_unlocked_chapter_count: numberOf("manualDefaultChapterCount", 1), body_nourish_rate_per_day: numberOf("manualBodyNourishRate", 0.8), learn_permanent_bonuses: readMap("manualLearnPermanentBonuses"), learn_permanent_multipliers: readMap("manualLearnPermanentMultipliers"), passive_bonuses: readMap("manualPassiveBonuses"), passive_multipliers: readMap("manualPassiveMultipliers") });
    if (!manual.projectSource) manual.updatedAt = nowIso();
    byId("manualFormTitle").textContent = manual.gongfa_name; renderManualList(); renderManualValidation(manual); saveStore(true);
  }
  function addMapRow(containerId) { const container = byId(containerId); if (!container) return; container.querySelector(".empty-row")?.remove(); container.insertAdjacentHTML("beforeend", `<div class="map-row" data-owner="${containerId}"><input class="map-key" data-map-key placeholder="属性键"><input class="map-value" data-map-value type="number" step="0.01" value="0"><button class="remove-row" data-remove-row type="button" aria-label="删除">×</button></div>`); container.querySelector(".map-row:last-child .map-key")?.focus(); }
  function addRewrite(group) { const container = byId(group === "main" ? "chapterMainRewrites" : "chapterSubRewrites"); if (!container) return; container.querySelector(".empty-row")?.remove(); container.insertAdjacentHTML("beforeend", rewriteRows([{ mode: "ADD", count: 1 }], group)); syncChapterFromForm(); }
  function manualValidation(manual) {
    const messages = [];
    if (!manual.gongfa_name?.trim()) messages.push(["error", "秘籍名不能为空"]);
    if (!manual.resource_path?.startsWith("res://")) messages.push(["warning", "资源路径建议使用 res:// 开头"]);
    if (!manual.can_be_main && !manual.can_be_sub) messages.push(["error", "秘籍至少应允许主修或辅修之一"]);
    if (manual.default_unlocked_chapter_count < 0 || manual.default_unlocked_chapter_count > manual.chapters.length) messages.push(["error", "默认解锁卷章数不能超过卷章总数"]);
    const ids = new Set();
    for (const chapter of manual.chapters) {
      if (!/^[a-z][a-z0-9_]*$/.test(chapter.chapter_id || "")) messages.push(["error", `卷章 ${chapter.chapter_name || "未命名"} 的 ID 必须是英文小写下划线`]);
      if (ids.has(chapter.chapter_id)) messages.push(["error", `卷章 ID 重复：${chapter.chapter_id}`]); ids.add(chapter.chapter_id);
      for (const key of Object.keys(chapter.required_attributes || {})) if (FORBIDDEN_REQUIRED_KEYS.has(key) || !REQUIRED_ATTRIBUTE_KEYS.has(key)) messages.push(["error", `卷章 ${chapter.chapter_name || chapter.chapter_id} 使用了不允许的学习属性：${key}`]);
      for (const id of chapter.required_chapter_ids || []) if (id === chapter.chapter_id || (id && !manual.chapters.some(item => item.chapter_id === id))) messages.push(["error", `卷章 ${chapter.chapter_id || "未命名"} 的前置卷章不存在：${id}`]);
      for (const rewrite of [...(chapter.sub_deck_rewrites || []), ...(chapter.main_deck_rewrites || [])]) {
        if (!rewrite.result_skill_path) messages.push(["error", `卷章 ${chapter.chapter_id || "未命名"} 的化法缺少结果牌路径`]);
        if (rewrite.mode === "REPLACE" && !rewrite.source_skill_name && !rewrite.source_skill_path) messages.push(["error", `卷章 ${chapter.chapter_id || "未命名"} 的替换规则缺少原基础牌`]);
        if ((rewrite.count || 0) < 1) messages.push(["error", `卷章 ${chapter.chapter_id || "未命名"} 的化法数量必须大于 0`]);
      }
    }
    for (const key of Object.keys(manual.required_attributes || {})) if (FORBIDDEN_REQUIRED_KEYS.has(key) || !REQUIRED_ATTRIBUTE_KEYS.has(key)) messages.push(["error", `秘籍使用了不允许的学习属性：${key}`]);
    if (manual.required_realm && !REALM_NAMES.includes(manual.required_realm)) messages.push(["warning", `需求境界不在当前资源目录：${manual.required_realm}`]);
    if (manual.can_be_main && !manual.main_realm_start && manual.main_realm_end) messages.push(["warning", "填写主修终止境界时建议同时填写起始境界"]);
    return messages;
  }
  function renderManualValidation(manual) {
    const messages = manualValidation(manual); const errors = messages.filter(item => item[0] === "error").length; const warnings = messages.filter(item => item[0] === "warning").length;
    const badge = byId("manualValidationBadge"); badge.className = `validation-badge ${errors ? "error" : warnings ? "warning" : "success"}`; badge.textContent = errors ? `${errors} 项错误` : warnings ? `${warnings} 项提醒` : "规则通过";
    byId("manualValidationList").innerHTML = messages.length ? messages.map(([kind, message]) => `<div class="validation-line ${kind}"><span>${kind === "error" ? "!" : "·"}</span>${escapeHtml(message)}</div>`).join("") : `<div class="validation-line success"><span>✓</span>总览、卷章和化法检查通过</div>`;
  }

  function formulaValue(formula, attrs, base = 0) {
    const source = String(formula || "").trim(); if (!source) return base;
    if (source.includes(":")) return source.split(";").reduce((total, part) => { const [key, factor] = part.split(":").map(item => item.trim()); const multiplier = Number(factor); if (!Number.isFinite(multiplier)) return total; return total + (key === "base" ? 1 : Number(attrs[key] || attrs[STATUS_LABELS[key]] || 0)) * multiplier; }, 0);
    const aliases = { "体魄": "physique", "灵慧": "intelligence", "敏捷": "agility", "神魂": "soul", "体道造诣": "body_proficiency", "剑道造诣": "sword_proficiency", "术法造诣": "magic_proficiency", "最大生命": "max_health", "借力层数": "borrowed_force" };
    let expression = source.replaceAll("×", "*").replaceAll("÷", "/"); for (const [key, value] of Object.entries(aliases)) expression = expression.replaceAll(key, value); expression = expression.replace(/\bround\s*\(/g, "Math.round(").replace(/\bfloor\s*\(/g, "Math.floor(").replace(/\bceil\s*\(/g, "Math.ceil(").replace(/\bmax\s*\(/g, "Math.max(").replace(/\bmin\s*\(/g, "Math.min(");
    if (!/^[0-9a-zA-Z_+\-*/%().,\s]+$/.test(expression)) return base;
    const names = ["physique", "intelligence", "agility", "soul", "body_proficiency", "sword_proficiency", "magic_proficiency", "max_health", "borrowed_force", "base"];
    try { const fn = new Function(...names, `return (${expression});`); const result = fn(...names.map(name => name === "base" ? base : Number(attrs[name] || 0))); return Number.isFinite(Number(result)) ? Number(result) : base; } catch (_) { return base; }
  }
  function damageValue(card, attrs) { if (card.damage_formula) return Math.max(0, Math.round(formulaValue(card.damage_formula, attrs, Number(card.base_damage || 0)))); return Math.max(0, Math.round(Number(card.base_damage || 0) + (card.damage_bonuses || []).reduce((sum, item) => sum + Number(attrs[item.attribute] || 0) * Number(item.multiplier || 0), 0))); }

  function godotQuote(value) { return `"${String(value ?? "").replaceAll("\\", "\\\\").replaceAll("\"", "\\\"").replaceAll("\n", "\\n")}"`; }
  function godotDict(value) { const entries = Object.entries(value || {}); return `{${entries.map(([key, item]) => `${godotQuote(key)}: ${typeof item === "string" ? godotQuote(item) : formatNumber(item)}`).join(", ")}}`; }
  function godotStringArray(value) { return `Array[String]([${(value || []).map(godotQuote).join(", ")}])`; }
  function godotDictionaryArray(value) { return `Array[Dictionary]([${(value || []).map(item => JSON.stringify(item).replaceAll("\\/", "/")).join(", ")}])`; }
  function cardToTres(card) {
    const lines = [`[gd_resource type="Resource" script_class="ActiveSkillData" format=3]`, ``, `[ext_resource type="Script" path="res://csharp/gongfa/ActiveSkillData.cs" id="1"]`];
    if (card.damage_bonuses?.length) lines.push(`[ext_resource type="Script" path="res://csharp/gongfa/DamageBonus.cs" id="2"]`);
    (card.damage_bonuses || []).forEach((bonus, index) => lines.push(``, `[sub_resource type="Resource" id="DamageBonus${index + 1}"]`, `script = ExtResource("2")`, `attribute = ${godotQuote(bonus.attribute)}`, `multiplier = ${formatNumber(bonus.multiplier)}`));
    const conditionType = { NONE: 0, MAIN_GONGFA: 1, SUB_GONGFA: 2, ANY_GONGFA: 3 }[card.condition_type] ?? 0;
    lines.push(``, `[resource]`, `script = ExtResource("1")`, `skill_name = ${godotQuote(card.skill_name)}`, `icon_path = ${godotQuote(card.icon_path)}`, `description = ${godotQuote(card.description)}`, `skill_type = ${Object.keys(SKILL_TYPES).indexOf(card.skill_type)}`, `element = ${Math.max(0, WUXING.indexOf(card.element || "NONE"))}`, `source_gongfa_name = ${godotQuote(card.source_gongfa)}`, `required_realm = ${godotQuote(card.required_realm)}`, `required_attributes = ${godotDict(card.required_attributes)}`, `energy_cost = ${formatNumber(card.energy_cost)}`, `mana_cost = ${formatNumber(card.mana_cost)}`, `spirit_occupy = ${formatNumber(card.spirit_occupy)}`, `required_equip_slot = ${formatNumber(card.required_equip_slot)}`, `required_weapon_type = ${formatNumber(card.required_weapon_type)}`, `target_type = ${formatNumber(card.target_type)}`, `range_min = ${formatNumber(card.range_min)}`, `range_max = ${formatNumber(card.range_max)}`, `aoe_range = ${formatNumber(card.aoe_range)}`, `can_target_empty = ${card.can_target_empty ? "true" : "false"}`, `move_distance = ${formatNumber(card.move_distance)}`, `keywords = ${godotStringArray(card.keywords)}`, `condition_type = ${conditionType}`, `condition_value = ${godotQuote(card.condition_value)}`, `momentum_condition_type = ${godotQuote(card.momentum_condition_type)}`, `momentum_effect_params = ${godotQuote(card.momentum_effect_params)}`, `flow_condition_type = ${godotQuote(card.flow_condition_type)}`, `flow_condition_params = ${godotStringArray(card.flow_condition_params)}`, `counter_condition = ${godotQuote(card.counter_condition)}`, `base_damage = ${formatNumber(card.base_damage)}`, `damage_type = ${godotQuote(card.damage_type)}`, `damage_formula = ${godotQuote(card.damage_formula)}`, `status_effects = ${godotDictionaryArray(card.status_effects)}`, `special_effects = ${godotDictionaryArray(card.special_effects)}`, `stacks_formula = ${godotQuote(card.stacks_formula)}`, `move_formula = ${godotQuote(card.move_formula)}`);
    if (card.damage_bonuses?.length) lines.push(`damage_bonuses = Array[ExtResource("2")]([${card.damage_bonuses.map((_, index) => `SubResource("DamageBonus${index + 1}")`).join(", ")}])`);
    return lines.join("\n") + "\n";
  }
  function manualToMarkdown(manual) {
    const lines = [`# ${manual.gongfa_name}`, ``, `资源路径：${manual.resource_path}`, `描述：${manual.description || ""}`, `五行：${WUXING_DISPLAY(manual.wuxing)}（${manual.wuxing_requirement_mode}）`, `需求境界：${manual.required_realm || "无"}`, `可作主修：${manual.can_be_main ? "是" : "否"}；可作辅修：${manual.can_be_sub ? "是" : "否"}`, `流派标签：${listText(manual.school_tags) || "无"}`, ``, `## 总览加成`, `学会永久固定加成：${JSON.stringify(manual.learn_permanent_bonuses)}`, `学会永久倍率加成：${JSON.stringify(manual.learn_permanent_multipliers)}`, `辅修固定加成：${JSON.stringify(manual.passive_bonuses)}`, `辅修倍率加成：${JSON.stringify(manual.passive_multipliers)}`, ``];
    for (const chapter of manual.chapters) { lines.push(`## ${chapter.chapter_name}（${chapter.chapter_id}）`, ``, `顺序：${chapter.order}`, `研读值：${chapter.required_study_progress}`, `需求境界：${chapter.required_realm || "无"}`, `前置卷章：${listText(chapter.required_chapter_ids) || "无"}`, `学习属性：${JSON.stringify(chapter.required_attributes)}`, `描述：${chapter.description || ""}`, `习得：${listText(chapter.learned_active_skills) || "无"}`, `主修额外习得：${listText(chapter.main_learned_active_skills) || "无"}`, `纳气固定加成：${JSON.stringify(chapter.naqi_bonuses)}`, `纳气倍率加成：${JSON.stringify(chapter.naqi_multipliers)}`, `辅修化法：${JSON.stringify(chapter.sub_deck_rewrites)}`, `主修化法：${JSON.stringify(chapter.main_deck_rewrites)}`, ``); }
    return lines.join("\n");
  }
  function manualToTres(manual) {
    const cardPaths = new Set(); for (const path of manual.granted_active_skills || []) cardPaths.add(path); for (const chapter of manual.chapters || []) for (const path of [...(chapter.learned_active_skills || []), ...(chapter.main_learned_active_skills || [])]) cardPaths.add(path); for (const chapter of manual.chapters || []) for (const rewrite of [...(chapter.sub_deck_rewrites || []), ...(chapter.main_deck_rewrites || [])]) for (const path of [rewrite.source_skill_path, rewrite.result_skill_path, rewrite.main_result_skill_path]) if (path) cardPaths.add(path);
    const ext = [`[gd_resource type="Resource" script_class="GongfaResource" format=3]`, ``, `[ext_resource type="Script" path="res://csharp/gongfa/GongfaResource.cs" id="1"]`, `[ext_resource type="Script" path="res://csharp/gongfa/GongfaChapterConfig.cs" id="2"]`, `[ext_resource type="Script" path="res://csharp/gongfa/GongfaDeckRewriteConfig.cs" id="3"]`, `[ext_resource type="Script" path="res://csharp/gongfa/ActiveSkillData.cs" id="4"]`]; const pathIds = {}; [...cardPaths].forEach((path, index) => { pathIds[path] = String(5 + index); ext.push(`[ext_resource type="Resource" path="${path}" id="${pathIds[path]}"]`); });
    const sub = []; const rewriteResource = (rewrite, id) => { const lines = [`[sub_resource type="Resource" id="${id}"]`, `script = ExtResource("3")`, `mode = ${rewrite.mode === "REPLACE" ? 1 : 0}`, `count = ${formatNumber(rewrite.count || 1)}`]; if (rewrite.source_skill_path && pathIds[rewrite.source_skill_path]) lines.push(`source_skill = ExtResource("${pathIds[rewrite.source_skill_path]}")`); if (rewrite.source_skill_name) lines.push(`source_skill_name = ${godotQuote(rewrite.source_skill_name)}`); if (rewrite.result_skill_path && pathIds[rewrite.result_skill_path]) lines.push(`result_skill = ExtResource("${pathIds[rewrite.result_skill_path]}")`); if (rewrite.main_result_skill_path && pathIds[rewrite.main_result_skill_path]) lines.push(`main_result_skill = ExtResource("${pathIds[rewrite.main_result_skill_path]}")`); sub.push(lines.join("\n")); return id; };
    const chapterRefs = []; for (const [index, chapter] of manual.chapters.entries()) { const rewriteRefs = []; for (const [rewriteIndex, rewrite] of (chapter.sub_deck_rewrites || []).entries()) rewriteRefs.push(`SubResource("Rewrite${index + 1}Sub${rewriteIndex + 1}")`), rewriteResource(rewrite, `Rewrite${index + 1}Sub${rewriteIndex + 1}`); for (const [rewriteIndex, rewrite] of (chapter.main_deck_rewrites || []).entries()) rewriteRefs.push(`SubResource("Rewrite${index + 1}Main${rewriteIndex + 1}")`), rewriteResource(rewrite, `Rewrite${index + 1}Main${rewriteIndex + 1}`); const lines = [`[sub_resource type="Resource" id="Chapter${index + 1}"]`, `script = ExtResource("2")`, `chapter_id = ${godotQuote(chapter.chapter_id)}`, `chapter_name = ${godotQuote(chapter.chapter_name)}`, `description = ${godotQuote(chapter.description)}`, `order = ${formatNumber(chapter.order)}`, `required_study_progress = ${formatNumber(chapter.required_study_progress)}`, `required_realm = ${godotQuote(chapter.required_realm)}`, `required_attributes = ${godotDict(chapter.required_attributes)}`, `required_chapter_ids = ${godotStringArray(chapter.required_chapter_ids)}`, `allowed_bearings = Array[int]([${(chapter.allowed_bearings || []).join(", ")}])`, `requires_main_slot = ${chapter.requires_main_slot ? "true" : "false"}`, `left_branch_name = ${godotQuote(chapter.left_branch_name)}`, `right_branch_name = ${godotQuote(chapter.right_branch_name)}`, `learn_permanent_bonuses = ${godotDict(chapter.learn_permanent_bonuses)}`, `learn_permanent_multipliers = ${godotDict(chapter.learn_permanent_multipliers)}`, `naqi_bonuses = ${godotDict(chapter.naqi_bonuses)}`, `naqi_multipliers = ${godotDict(chapter.naqi_multipliers)}`, `learned_active_skills = Array[ExtResource("4")]([${(chapter.learned_active_skills || []).map(path => pathIds[path] ? `ExtResource("${pathIds[path]}")` : "").filter(Boolean).join(", ")}])`, `main_learned_active_skills = Array[ExtResource("4")]([${(chapter.main_learned_active_skills || []).map(path => pathIds[path] ? `ExtResource("${pathIds[path]}")` : "").filter(Boolean).join(", ")}])`, `sub_deck_rewrites = Array[ExtResource("3")]([${rewriteRefs.filter((_, i) => i < (chapter.sub_deck_rewrites || []).length).join(", ")}])`, `main_deck_rewrites = Array[ExtResource("3")]([${rewriteRefs.slice((chapter.sub_deck_rewrites || []).length).join(", ")}])`]; sub.push(lines.join("\n")); chapterRefs.push(`SubResource("Chapter${index + 1}")`); }
    const resource = [``, `[resource]`, `script = ExtResource("1")`, `gongfa_name = ${godotQuote(manual.gongfa_name)}`, `description = ${godotQuote(manual.description)}`, `wuxing = ${Math.max(0, WUXING.indexOf(manual.wuxing || "NONE"))}`, `wuxing_requirement_mode = ${manual.wuxing_requirement_mode === "AND" ? 1 : 0}`, `required_realm = ${godotQuote(manual.required_realm)}`, `required_study_progress = ${formatNumber(manual.required_study_progress)}`, `required_attributes = ${godotDict(manual.required_attributes)}`, `can_be_main = ${manual.can_be_main ? "true" : "false"}`, `can_be_sub = ${manual.can_be_sub ? "true" : "false"}`, `bearing_compatibility = Array[int]([${(manual.bearing_compatibility || []).join(", ")}])`, `school_tags = ${godotStringArray(manual.school_tags)}`, `main_realm_start = ${godotQuote(manual.main_realm_start)}`, `main_realm_end = ${godotQuote(manual.main_realm_end)}`, `sub_realm_start = ${godotQuote(manual.sub_realm_start)}`, `sub_realm_end = ${godotQuote(manual.sub_realm_end)}`, `learn_permanent_bonuses = ${godotDict(manual.learn_permanent_bonuses)}`, `learn_permanent_multipliers = ${godotDict(manual.learn_permanent_multipliers)}`, `passive_bonuses = ${godotDict(manual.passive_bonuses)}`, `passive_multipliers = ${godotDict(manual.passive_multipliers)}`, `body_nourish_rate_per_day = ${formatNumber(manual.body_nourish_rate_per_day)}`, `default_unlocked_chapter_count = ${formatNumber(manual.default_unlocked_chapter_count)}`, `chapters = Array[ExtResource("2")]([${chapterRefs.join(", ")}])`]; return [...ext, ``, ...sub, resource.join("\n")].join("\n") + "\n";
  }
  function downloadFile(name, content, mime = "text/plain;charset=utf-8") { const blob = new Blob([content], { type: mime }); const link = document.createElement("a"); link.href = URL.createObjectURL(blob); link.download = name; link.click(); setTimeout(() => URL.revokeObjectURL(link.href), 1000); }
  function exportCurrentCard(format) { const card = currentCard(); if (!card) return; syncCardFromForm(); const safe = slug(card.skill_name); if (format === "json") downloadFile(`${safe}.card.json`, JSON.stringify(card, null, 2), "application/json;charset=utf-8"); else downloadFile(`${safe}.tres`, cardToTres(card)); toast(`已导出 ${card.skill_name}`); }
  function exportCurrentManual(format) { const manual = currentManual(); if (!manual) return; syncManualFromForm(); syncChapterFromForm(); const safe = slug(manual.gongfa_name); if (format === "json") downloadFile(`${safe}.manual.json`, JSON.stringify(manual, null, 2), "application/json;charset=utf-8"); if (format === "markdown") downloadFile(`${safe}.md`, manualToMarkdown(manual)); if (format === "tres") downloadFile(`${safe}.tres`, manualToTres(manual)); toast(`已导出 ${manual.gongfa_name}`); }
  function exportBundle() { syncCardFromForm(); syncManualFromForm(); syncChapterFromForm(); downloadFile("qingxi-card-studio-workspace.json", JSON.stringify({ schemaVersion: 1, project: "青溪问道", exportedAt: new Date().toISOString(), cards: store.cards.filter(item => !item.projectSource), manuals: store.manuals.filter(item => !item.projectSource) }, null, 2), "application/json;charset=utf-8"); toast("已导出本地草稿工作包"); }
  function mergeImported(data) { const cards = data?.cards || (data?.kind === "card" ? [data] : []); const manuals = data?.manuals || (data?.kind === "manual" ? [data] : []); for (const item of cards) { const normalized = normalizeCard({ ...item, projectSource: false, id: item.id && !String(item.id).startsWith("project_") ? item.id : uid("card") }); const index = store.cards.findIndex(existing => existing.id === normalized.id); if (index >= 0) store.cards[index] = normalized; else store.cards.unshift(normalized); activeCardId = normalized.id; } for (const item of manuals) { const normalized = normalizeManual({ ...item, projectSource: false, id: item.id && !String(item.id).startsWith("project_") ? item.id : uid("manual") }); const index = store.manuals.findIndex(existing => existing.id === normalized.id); if (index >= 0) store.manuals[index] = normalized; else store.manuals.unshift(normalized); activeManualId = normalized.id; } saveStore(); renderAll(); toast(`已导入 ${cards.length} 张卡牌、${manuals.length} 本秘籍`); }

  let syncBusy = false;
  function itemStamp(item) { return Date.parse(item?.updatedAt || "") || 0; }
  function readTestConfig() {
    if (!syncMeta.testConfigUpdatedAt) { syncMeta.testConfigUpdatedAt = nowIso(); saveSyncMeta(); }
    return { updatedAt: syncMeta.testConfigUpdatedAt, realm: valueOf("testRealmSelect"), deckIds: [...testDeckIds], attributes: readTestAttributes(), energy: numberOf("testEnergy", 3), maxEnergy: numberOf("testMaxEnergy", 6), mana: numberOf("testMana", 0), maxMana: numberOf("testMaxMana", 0), spirit: numberOf("testSpirit", 0), maxSpirit: numberOf("testMaxSpirit", 0), enemyHp: numberOf("testEnemyHp", 60), shield: numberOf("testShield", 0) };
  }
  function touchTestConfig() { syncMeta.testConfigUpdatedAt = nowIso(); saveSyncMeta(); updateSyncIndicator(); }
  function localWorkspace() {
    ensureDraftTimestamps();
    return { schemaVersion: 1, project: "青溪问道", cards: clone(store.cards.filter(item => !item.projectSource)), manuals: clone(store.manuals.filter(item => !item.projectSource)), testConfig: readTestConfig(), deleted: clone(syncMeta.deleted || []) };
  }
  function mergeCollection(localItems, remoteItems) {
    const merged = new Map();
    for (const item of [...(localItems || []), ...(remoteItems || [])]) {
      if (!item?.id) continue;
      const previous = merged.get(item.id);
      if (!previous || itemStamp(item) >= itemStamp(previous)) merged.set(item.id, clone(item));
    }
    return [...merged.values()];
  }
  function mergeDeleted(localItems, remoteItems) {
    const merged = new Map();
    for (const item of [...(localItems || []), ...(remoteItems || [])]) {
      if (!item?.kind || !item?.id) continue;
      const key = `${item.kind}:${item.id}`;
      if (!merged.has(key) || itemStamp(item) >= itemStamp(merged.get(key))) merged.set(key, { kind: item.kind, id: item.id, updatedAt: item.updatedAt || nowIso() });
    }
    return [...merged.values()];
  }
  function filterDeleted(items, kind, deleted) {
    const removed = new Map(deleted.filter(item => item.kind === kind).map(item => [item.id, itemStamp(item)]));
    return items.filter(item => !removed.has(item.id) || itemStamp(item) > removed.get(item.id));
  }
  function mergeWorkspaces(local, remote) {
    const left = local || {}; const right = remote || {};
    const deleted = mergeDeleted(left.deleted, right.deleted);
    const localCards = mergeCollection(left.cards, right.cards); const localManuals = mergeCollection(left.manuals, right.manuals);
    const localConfig = left.testConfig; const remoteConfig = right.testConfig;
    const testConfig = !remoteConfig || itemStamp(localConfig) >= itemStamp(remoteConfig) ? localConfig : remoteConfig;
    return { schemaVersion: 1, project: "青溪问道", cards: filterDeleted(localCards, "card", deleted), manuals: filterDeleted(localManuals, "manual", deleted), testConfig: testConfig ? clone(testConfig) : null, deleted };
  }
  function recordTombstone(kind, id) { syncMeta.deleted = (syncMeta.deleted || []).filter(item => !(item.kind === kind && item.id === id)); syncMeta.deleted.push({ kind, id, updatedAt: nowIso() }); saveSyncMeta(); }
  function syncEndpoint() {
    const typed = valueOf("syncEndpoint").trim(); const saved = syncMeta.endpoint || ""; const current = location.protocol === "http:" || location.protocol === "https:" ? location.origin : "";
    return (typed || saved || current).replace(/\/+$/, "");
  }
  function syncUrl() { const endpoint = syncEndpoint(); if (!endpoint) throw new Error("请填写电脑同步地址"); return `${endpoint}/api/sync`; }
  function syncLog(message) { const node = byId("syncLog"); if (!node) return; const stamp = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }); node.textContent = `${stamp}  ${message}\n${node.textContent}`.trim(); }
  function workspaceSummary(workspace) { const value = workspace || {}; return `电脑副本：${value.cards?.length || 0} 张本地卡牌、${value.manuals?.length || 0} 本本地秘籍；测试设置${value.testConfig ? "已保存" : "未保存"}`; }
  function updateSyncIndicator() {
    const node = byId("syncIndicator"); if (!node) return;
    if (!navigator.onLine) { node.className = "sync-indicator offline"; node.textContent = "离线可用"; return; }
    if (syncMeta.lastSyncAt) { node.className = "sync-indicator online"; node.textContent = "已同步"; return; }
    node.className = "sync-indicator"; node.textContent = "仅本机";
  }
  function updateSyncSummary(state = null) {
    const node = byId("syncSummary"); if (!node) return;
    const endpoint = syncEndpoint(); const revision = state?.revision ?? syncMeta.lastRevision;
    node.textContent = state ? `${endpoint} · 第 ${revision} 版 · ${workspaceSummary(state.workspace)}` : `${endpoint || "尚未填写地址"} · ${syncMeta.lastSyncAt ? `上次同步 ${new Date(syncMeta.lastSyncAt).toLocaleString()}` : "尚未同步"}`;
  }
  async function requestSync(method = "GET", body = null) {
    const response = await fetch(syncUrl(), { method, cache: "no-store", headers: body ? { "Content-Type": "application/json" } : undefined, body: body ? JSON.stringify(body) : undefined });
    let data = {};
    try { data = await response.json(); } catch (_) { /* server returned no JSON */ }
    return { response, data };
  }
  function applyTestConfig(config) {
    if (!config) return;
    if (REALM_NAMES.includes(config.realm)) { setField("testRealmSelect", config.realm); setField("manualTestRealm", config.realm); }
    if (config.attributes && typeof config.attributes === "object") renderTestAttributes(config.attributes);
    for (const [id, fallback] of [["testEnergy", 3], ["testMaxEnergy", 6], ["testMana", 0], ["testMaxMana", 0], ["testSpirit", 0], ["testMaxSpirit", 0], ["testEnemyHp", 60], ["testShield", 0]]) if (config[id] !== undefined) setField(id, Number(config[id]) || fallback);
    testDeckIds = Array.isArray(config.deckIds) ? config.deckIds.filter(id => cardById(id)) : testDeckIds;
    byId("testDeckCount").textContent = `${testDeckIds.length} 张已选`;
  }
  function applyWorkspace(workspace) {
    const projectCards = store.cards.filter(item => item.projectSource); const projectManuals = store.manuals.filter(item => item.projectSource);
    store.cards = [...projectCards, ...(workspace.cards || []).map(item => normalizeCard({ ...item, projectSource: false }))];
    store.manuals = [...projectManuals, ...(workspace.manuals || []).map(item => normalizeManual({ ...item, projectSource: false }))];
    syncMeta.deleted = Array.isArray(workspace.deleted) ? workspace.deleted : [];
    if (workspace.testConfig) { applyTestConfig(workspace.testConfig); syncMeta.testConfigUpdatedAt = workspace.testConfig.updatedAt || syncMeta.testConfigUpdatedAt; }
    syncMeta.lastSyncAt = nowIso(); testState = null;
    activeCardId = store.cards.some(item => item.id === activeCardId) ? activeCardId : store.cards[0]?.id || "";
    activeManualId = store.manuals.some(item => item.id === activeManualId) ? activeManualId : store.manuals[0]?.id || "";
    saveSyncMeta(); saveStore(true); renderAll();
  }
  async function checkSyncConnection() {
    if (syncBusy) return;
    syncBusy = true; updateSyncIndicator();
    try {
      const endpoint = syncEndpoint(); if (!endpoint) throw new Error("请填写电脑同步地址");
      syncMeta.endpoint = endpoint; saveSyncMeta();
      const result = await requestSync(); if (!result.response.ok) throw new Error(result.data.error || `连接失败（${result.response.status}）`);
      syncMeta.lastRevision = Number(result.data.revision) || 0; saveSyncMeta(); updateSyncSummary(result.data); syncLog(`连接成功。${workspaceSummary(result.data.workspace)}`); toast("同步服务连接成功", "success");
    } catch (error) { updateSyncIndicator(); syncLog(`连接失败：${error.message || "电脑暂时不可达"}`); toast("电脑暂时不可达，本机仍可继续使用", "warning"); }
    finally { syncBusy = false; }
  }
  async function mergeSync() {
    if (syncBusy) return;
    syncBusy = true; syncLog("正在合并本机与电脑数据…");
    try {
      syncCardFromForm(); syncManualFromForm(); syncChapterFromForm();
      const endpoint = syncEndpoint(); if (!endpoint) throw new Error("请填写电脑同步地址");
      syncMeta.endpoint = endpoint; saveSyncMeta();
      const local = localWorkspace(); let remote = await requestSync();
      if (!remote.response.ok) throw new Error(remote.data.error || `读取失败（${remote.response.status}）`);
      let merged = mergeWorkspaces(local, remote.data.workspace);
      let result = null;
      for (let attempt = 0; attempt < 3; attempt += 1) {
        result = await requestSync("PUT", { baseRevision: Number(remote.data.revision) || 0, workspace: merged });
        if (result.response.status !== 409) break;
        remote = { response: result.response, data: result.data }; merged = mergeWorkspaces(local, remote.data.workspace);
      }
      if (!result?.response.ok) throw new Error(result?.data?.error || `写入失败（${result?.response?.status || "未知"}）`);
      applyWorkspace(result.data.workspace); syncMeta.lastRevision = Number(result.data.revision) || 0; syncMeta.lastSyncAt = nowIso(); saveSyncMeta(); updateSyncSummary(result.data); updateSyncIndicator(); syncLog(`同步完成。${workspaceSummary(result.data.workspace)}`); toast("局域网数据已合并", "success");
    } catch (error) { updateSyncIndicator(); syncLog(`同步未完成：${error.message || "电脑暂时不可达"}`); toast("同步未完成，本机数据没有被清除", "warning"); }
    finally { syncBusy = false; }
  }
  function openSyncDialog() { setField("syncEndpoint", syncMeta.endpoint || (location.protocol === "http:" || location.protocol === "https:" ? location.origin : "")); byId("syncDialog").hidden = false; updateSyncSummary(); updateSyncIndicator(); }
  function closeSyncDialog() { byId("syncDialog").hidden = true; }

  function chineseNumeral(value) { const digits = { 一: 1, 二: 2, 三: 3, 四: 4, 五: 5, 六: 6, 七: 7, 八: 8, 九: 9, 零: 0 }; if (digits[value]) return digits[value]; if (value === "十") return 10; if (value.startsWith("十")) return 10 + (digits[value[1]] || 0); if (value.endsWith("十")) return (digits[value[0]] || 0) * 10; if (value.length === 3 && value[1] === "十") return (digits[value[0]] || 0) * 10 + (digits[value[2]] || 0); return Number(value) || 0; }
  function realmProfile(realmName, baseAttrs = null) {
    const realm = REALMS.find(item => item.name === realmName) || REALMS[0]; const name = String(realmName || ""); const tierMatch = name.match(/云息(\d+)层/) || name.match(/云息([一二三四五六七八九十百]+)层/); const tier = tierMatch ? (Number.isFinite(Number(tierMatch[1])) ? Number(tierMatch[1]) : chineseNumeral(tierMatch[1])) : 0; let multiplier = 1; for (let layer = 1; layer <= Math.min(13, tier); layer++) multiplier += layer === 4 || layer === 7 ? 0.15 : 0.10; const defaults = baseAttrs || { physique: 12, intelligence: 8, agility: 10, soul: 6, body_proficiency: 3, sword_proficiency: 1, magic_proficiency: 1, max_health: 120, borrowed_force: 0 }; const adjusted = { ...defaults }; for (const key of ["physique", "intelligence", "agility", "soul"]) adjusted[key] = Number(defaults[key] || 0) * multiplier; adjusted.max_health = Math.round(60 + adjusted.physique * 6 + Number(defaults.physique || 0) * 2 + adjusted.agility * 3 + adjusted.soul * 3 + adjusted.intelligence * 1.5); const battle = realm.battle_base_attributes || {}; return { tier, multiplier, adjusted, initialHandSize: Number(battle.initial_hand_size || (tier >= 3 ? 5 : 4)), cardsPerTurn: Number(battle.cards_per_turn || (tier >= 3 ? 3 : 2)), maxMana: tier <= 0 ? 0 : tier <= 2 ? 1 : 2, maxSpirit: tier <= 0 ? 0 : tier <= 2 ? 1 : 2, realm }; }
  function renderRealmSummary() { const profile = realmProfile(valueOf("testRealmSelect")); const percent = Math.round((profile.multiplier - 1) * 100); byId("realmSummary").innerHTML = `<strong>${escapeHtml(valueOf("testRealmSelect"))}</strong><span>四维累计 +${percent}%</span><span>初始手牌 ${profile.initialHandSize}</span><span>每次抽 ${profile.cardsPerTurn}</span><span>默认法力 ${profile.maxMana}</span><span>默认灵识 ${profile.maxSpirit}</span>`; }
  function defaultTestAttributes() { return { physique: 12, intelligence: 8, agility: 10, soul: 6, body_proficiency: 3, sword_proficiency: 1, magic_proficiency: 1 }; }
  function renderTestAttributes(attrs = defaultTestAttributes()) { byId("testAttributes").innerHTML = Object.entries(attrs).map(([key, item]) => `<div class="map-row" data-test-attr="${escapeHtml(key)}"><span class="attr-name">${escapeHtml(ATTRIBUTE_LABELS[key] || key)}</span><input data-test-value type="number" step="0.1" value="${escapeHtml(item)}"><button class="remove-row" data-remove-test-attr type="button" aria-label="删除">×</button></div>`).join(""); }
  function readTestAttributes() { const attrs = {}; document.querySelectorAll("#testAttributes [data-test-attr]").forEach(row => { attrs[row.dataset.testAttr] = Number(row.querySelector("[data-test-value]")?.value) || 0; }); return attrs; }
  function selectedTestCards() { return testDeckIds.map(cardById).filter(Boolean); }
  function renderTestDeckList() { const selected = new Set(testDeckIds); byId("testDeckList").innerHTML = store.cards.map(card => `<label class="check-row"><input type="checkbox" data-test-card value="${escapeHtml(card.id)}" ${selected.has(card.id) ? "checked" : ""}><span><strong>${escapeHtml(card.skill_name)}</strong><small>${escapeHtml(typeLabel(card.skill_type))} · 精 ${formatNumber(card.energy_cost)} · ${escapeHtml(card.source_gongfa || card.source_type || "")}</small></span></label>`).join(""); byId("testDeckCount").textContent = `${testDeckIds.length} 张已选`;
  }
  function addTestLog(message, tone = "normal") { if (!testState) return; testState.log.push({ message, tone }); if (testState.log.length > 120) testState.log.shift(); }
  function shuffle(list) { const result = [...list]; for (let i = result.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [result[i], result[j]] = [result[j], result[i]]; } return result; }
  function refillDrawPile() { if (!testState.drawPile.length && testState.discard.length) { testState.drawPile = shuffle(testState.discard); testState.discard = []; addTestLog("弃牌堆洗回牌堆。", "muted"); } }
  function drawTestCards(count) { for (let index = 0; index < count; index++) { refillDrawPile(); const id = testState.drawPile.shift(); if (!id) { addTestLog("牌堆已空，无法继续抽牌。", "warning"); break; } testState.hand.push(id); } }
  function startTest() { const realm = valueOf("testRealmSelect") || REALM_NAMES[0]; const profile = realmProfile(realm, readTestAttributes()); testState = { turn: 1, realm, baseAttrs: readTestAttributes(), attrs: profile.adjusted, energy: Math.min(numberOf("testEnergy", 3), numberOf("testMaxEnergy", 6)), maxEnergy: numberOf("testMaxEnergy", 6), mana: numberOf("testMana", profile.maxMana), maxMana: numberOf("testMaxMana", profile.maxMana), spirit: numberOf("testSpirit", 0), maxSpirit: numberOf("testMaxSpirit", profile.maxSpirit), enemyHp: numberOf("testEnemyHp", 60), maxEnemyHp: numberOf("testEnemyHp", 60), shield: numberOf("testShield", 0), playerHp: 100, hand: [], drawPile: shuffle(testDeckIds), discard: [], exhausted: [], statuses: {}, log: [] }; addTestLog(`开始测试：${realm}。初始手牌 ${profile.initialHandSize} 张，每回合抽 ${profile.cardsPerTurn} 张。`, "success"); drawTestCards(profile.initialHandSize); renderTest(); }
  function testStateOrStart() { if (!testState) startTest(); return testState; }
  function executeSpecial(effect, card) { const count = Number(effect.count || 1); const formula = effect.formula || ""; switch (effect.effect) { case "gain_energy": case "restore_stamina": testState.energy = Math.min(testState.maxEnergy, testState.energy + (formula ? Math.round(formulaValue(formula, testState.attrs, count)) : count)); addTestLog(`${card.skill_name}：获得精力 ${count}。`, "success"); break; case "restore_mana": testState.mana = Math.min(testState.maxMana, testState.mana + (formula ? Math.round(formulaValue(formula, testState.attrs, count)) : count)); addTestLog(`${card.skill_name}：恢复法力 ${count}。`, "success"); break; case "draw": drawTestCards(count); addTestLog(`${card.skill_name}：抽牌 ${count}。`, "success"); break; case "shield": testState.shield += Math.round(formula ? formulaValue(formula, testState.attrs, count) : count); addTestLog(`${card.skill_name}：获得护持 ${count}。`, "success"); break; case "heal": testState.playerHp = Math.min(100, testState.playerHp + Math.round(formula ? formulaValue(formula, testState.attrs, count) : count)); addTestLog(`${card.skill_name}：恢复生命 ${count}。`, "success"); break; case "damage": { const damage = Math.max(0, Math.round(formula ? formulaValue(formula, testState.attrs, count) : count)); testState.enemyHp = Math.max(0, testState.enemyHp - damage); addTestLog(`${card.skill_name}：追加 ${damage} 点伤害。`, "success"); break; } case "gain_qi_blood": case "gain_sword_intent": case "gain_punch_frame": case "add_status": { const status = effect.effect === "add_status" ? (formula || "状态") : effect.effect.replace("gain_", ""); testState.statuses[status] = (testState.statuses[status] || 0) + count; addTestLog(`${card.skill_name}：${status} +${count}。`, "success"); break; } case "remove_status": { const status = formula || "状态"; delete testState.statuses[status]; addTestLog(`${card.skill_name}：移除 ${status}。`, "success"); break; } default: addTestLog(`${card.skill_name}：效果 ${effect.effect || "未配置"} 仅记录，尚未接入沙盘。`, "warning"); } }
  function playTestCard(index) { testStateOrStart(); const card = cardById(testState.hand[index]); if (!card) return; if (testState.energy < card.energy_cost) return addTestLog(`${card.skill_name} 无法打出：精力不足。`, "warning"), renderTest(); if (testState.mana < card.mana_cost) return addTestLog(`${card.skill_name} 无法打出：法力不足。`, "warning"), renderTest(); if (testState.spirit + card.spirit_occupy > testState.maxSpirit) return addTestLog(`${card.skill_name} 无法打出：灵识容量不足。`, "warning"), renderTest(); testState.energy -= card.energy_cost; testState.mana -= card.mana_cost; testState.spirit += card.spirit_occupy; testState.hand.splice(index, 1); const damage = damageValue(card, testState.attrs); if (damage > 0) { testState.enemyHp = Math.max(0, testState.enemyHp - damage); addTestLog(`${card.skill_name}：造成 ${damage} 点${card.damage_type === "soul" ? "神魂" : card.damage_type === "physical" ? "物理" : "法术"}伤害。`, "success"); } for (const effect of card.special_effects || []) executeSpecial(effect, card); for (const status of card.status_effects || []) if (status.status_id) { const stacks = card.stacks_formula ? Math.max(1, Math.round(formulaValue(card.stacks_formula, testState.attrs, Number(status.stacks || 1)))) : Number(status.stacks || 1); testState.statuses[status.status_id] = (testState.statuses[status.status_id] || 0) + stacks; addTestLog(`${card.skill_name}：${status.status_id} +${stacks} 层。`, "success"); } if (card.keywords?.includes("消耗") || card.skill_type === "POWER") testState.exhausted.push(card.id); else testState.discard.push(card.id); if (card.keywords?.includes("应对")) addTestLog("这是一张应对牌，当前沙盘按主动点击测试，不自动触发。", "muted"); renderTest(); }
  function endTestTurn() { testStateOrStart(); const retained = []; for (const id of testState.hand) { const card = cardById(id); if (card?.keywords?.includes("保留")) retained.push(id); else testState.discard.push(id); } testState.hand = retained; testState.mana = 0; testState.energy = Math.min(testState.maxEnergy, testState.energy + 3); testState.turn += 1; const profile = realmProfile(testState.realm, testState.baseAttrs); drawTestCards(profile.cardsPerTurn); addTestLog(`结束回合，精力恢复至 ${testState.energy}，法力清零，抽取 ${profile.cardsPerTurn} 张。`, "success"); renderTest(); }
  function renderTest() { if (!testState) return; byId("testEnergy").value = testState.energy; byId("testMaxEnergy").value = testState.maxEnergy; byId("testMana").value = testState.mana; byId("testMaxMana").value = testState.maxMana; byId("testSpirit").value = testState.spirit; byId("testMaxSpirit").value = testState.maxSpirit; byId("testEnemyHp").value = testState.enemyHp; byId("testShield").value = testState.shield; byId("testEnergyReadout").textContent = `${testState.energy}/${testState.maxEnergy}`; byId("testManaReadout").textContent = `${testState.mana}/${testState.maxMana}`; byId("testSpiritReadout").textContent = `${testState.spirit}/${testState.maxSpirit}`; byId("testEnemyReadout").textContent = `${testState.enemyHp}/${testState.maxEnemyHp}`; byId("testTurnReadout").textContent = `第 ${testState.turn} 回合 · ${testState.realm}`; byId("testHandCount").textContent = `${testState.hand.length} 张`; byId("testHand").innerHTML = testState.hand.map((id, index) => { const card = cardById(id); return card ? `<button class="test-card ${card.energy_cost > testState.energy || card.mana_cost > testState.mana ? "unplayable" : ""}" data-play-card="${index}" type="button"><strong>${escapeHtml(card.skill_name)}</strong><span>精 ${formatNumber(card.energy_cost)} · 法 ${formatNumber(card.mana_cost)}</span><small>${escapeHtml(cardDescription(card).slice(0, 64))}</small></button>` : ""; }).join("") || `<div class="empty-state">手牌为空，抽牌或结束回合</div>`; byId("testLog").innerHTML = testState.log.slice().reverse().map(item => `<div class="log-line ${item.tone}"><span>›</span>${escapeHtml(item.message)}</div>`).join("") || `<div class="empty-state">尚无结算记录</div>`; }

  function renderManualTest() { const manual = store.manuals.find(item => item.id === valueOf("testManualSelect")) || store.manuals[0]; if (!manual) return; setSelectOptions("testChapterSelect", manual.chapters.map((chapter, index) => [index, `${chapter.order}. ${chapter.chapter_name}`]), 0); renderManualRequirements(); }
  function renderManualRequirements() { const manual = store.manuals.find(item => item.id === valueOf("testManualSelect")) || store.manuals[0]; const chapter = manual?.chapters?.[Number(valueOf("testChapterSelect")) || 0]; if (!manual || !chapter) return; const realm = valueOf("manualTestRealm") || valueOf("testRealmSelect") || "凡人"; const requirement = Object.entries(chapter.required_attributes || {}).map(([key, value]) => `${ATTRIBUTE_LABELS[key] || key} ≥ ${value}`).join("、") || "无属性门槛"; byId("manualTestRequirements").innerHTML = `<div><strong>${escapeHtml(manual.gongfa_name)} · ${escapeHtml(chapter.chapter_name)}</strong></div><div>需求境界：${escapeHtml(chapter.required_realm || manual.required_realm || "无")}</div><div>研读值：${formatNumber(chapter.required_study_progress)}</div><div>属性：${escapeHtml(requirement)}</div><div>前置卷章：${escapeHtml(listText(chapter.required_chapter_ids) || "无")}</div><div class="small-note">当前角色境界：${escapeHtml(realm)}；研读值本身不在此处扣除。</div>`; }
  function runManualTest() { const manual = store.manuals.find(item => item.id === valueOf("testManualSelect")) || store.manuals[0]; const chapter = manual?.chapters?.[Number(valueOf("testChapterSelect")) || 0]; if (!manual || !chapter) return; const realm = valueOf("manualTestRealm") || "凡人"; const learned = new Set(splitList(valueOf("manualTestLearned"))); const messages = []; const requiredRealm = chapter.required_realm || manual.required_realm; if (requiredRealm && realmIndex(realm) < realmIndex(requiredRealm)) messages.push(`境界不足：需要 ${requiredRealm}`); for (const id of chapter.required_chapter_ids || []) if (!learned.has(id)) messages.push(`缺少前置卷章：${id}`); const attrs = realmProfile(realm, readTestAttributes()).adjusted; for (const [key, need] of Object.entries(chapter.required_attributes || {})) if (Number(attrs[key] || 0) < Number(need)) messages.push(`属性不足：${ATTRIBUTE_LABELS[key] || key} 需要 ${need}，当前 ${formatNumber(attrs[key] || 0)}`); const node = byId("manualTestResult"); node.className = `test-result ${messages.length ? "fail" : "pass"}`; node.innerHTML = messages.length ? `<strong>未通过</strong>${messages.map(message => `<div>· ${escapeHtml(message)}</div>`).join("")}` : `<strong>可以学习</strong><div>前置卷章、境界和属性门槛均通过。</div><div>本卷研读值需求：${formatNumber(chapter.required_study_progress)}；习得 ${chapter.learned_active_skills?.length || 0} 张牌。</div>`; }

  function importFile() { byId("importFile").click(); }
  function renderAll() { renderCardList(); renderManualList(); renderCardEditor(); renderManualEditor(); renderTestDeckList(); renderManualTest(); renderRealmSummary(); if (activeView) switchView(activeView); }
  function switchView(view) { activeView = view; document.querySelectorAll(".view").forEach(node => node.classList.toggle("active", node.id === `${view}View`)); document.querySelectorAll(".nav-button").forEach(node => node.classList.toggle("active", node.dataset.view === view)); if (view === "test") { renderTestDeckList(); renderManualTest(); renderTest(); } }
  function newCard() { const card = defaultCard(); store.cards.unshift(card); activeCardId = card.id; saveStore(); renderCardList(); renderCardEditor(card); toast("已新建卡牌"); }
  function duplicateCard() { const card = currentCard(); if (!card) return; const copy = defaultCard({ ...clone(card), id: uid("card"), projectSource: false, skill_name: `${card.skill_name}（副本）`, resource_path: `res://resources/activeskills/${slug(card.skill_name)}_副本.tres` }); store.cards.unshift(copy); activeCardId = copy.id; saveStore(); renderCardList(); renderCardEditor(copy); toast("已复制当前卡牌"); }
  function deleteCard() { const card = currentCard(); if (!card) return; if (!window.confirm(`从工具中移除“${card.skill_name}”？项目原文件不会被删除。`)) return; const index = store.cards.findIndex(item => item.id === card.id); if (!card.projectSource) recordTombstone("card", card.id); store.cards.splice(index, 1); activeCardId = store.cards[Math.max(0, index - 1)]?.id || ""; saveStore(); renderCardList(); renderCardEditor(currentCard()); toast("已从工具中移除"); }
  function newManual() { const manual = defaultManual(); store.manuals.unshift(manual); activeManualId = manual.id; activeChapterIndex = 0; saveStore(); renderManualList(); renderManualEditor(manual); toast("已新建秘籍"); }
  function duplicateManual() { const manual = currentManual(); if (!manual) return; const copy = normalizeManual({ ...clone(manual), id: uid("manual"), projectSource: false, gongfa_name: `${manual.gongfa_name}（副本）`, resource_path: `res://resources/gongfa/${slug(manual.gongfa_name)}_副本.tres` }); store.manuals.unshift(copy); activeManualId = copy.id; activeChapterIndex = 0; saveStore(); renderManualList(); renderManualEditor(copy); toast("已复制当前秘籍"); }
  function deleteManual() { const manual = currentManual(); if (!manual) return; if (!window.confirm(`从工具中移除“${manual.gongfa_name}”？项目原文件不会被删除。`)) return; const index = store.manuals.findIndex(item => item.id === manual.id); if (!manual.projectSource) recordTombstone("manual", manual.id); store.manuals.splice(index, 1); activeManualId = store.manuals[Math.max(0, index - 1)]?.id || ""; activeChapterIndex = 0; saveStore(); renderManualList(); renderManualEditor(currentManual()); toast("已从工具中移除"); }
  function initTestDeck() { const preferred = ["发劲", "架招", "养息", "撼山", "灵击"].map(name => store.cards.find(card => card.skill_name === name)?.id).filter(Boolean); testDeckIds = preferred.length ? preferred : store.cards.slice(0, 5).map(card => card.id); }

  function bindEvents() {
    document.querySelectorAll(".nav-button").forEach(button => button.addEventListener("click", () => switchView(button.dataset.view)));
    byId("syncButton").addEventListener("click", openSyncDialog); byId("closeSyncButton").addEventListener("click", closeSyncDialog); byId("syncDialog").addEventListener("click", event => { if (event.target === byId("syncDialog")) closeSyncDialog(); }); byId("checkSyncButton").addEventListener("click", checkSyncConnection); byId("mergeSyncButton").addEventListener("click", mergeSync); window.addEventListener("online", updateSyncIndicator); window.addEventListener("offline", updateSyncIndicator);
    byId("cardList").addEventListener("click", event => { const button = event.target.closest("[data-card-id]"); if (!button) return; syncCardFromForm(); activeCardId = button.dataset.cardId; renderCardList(); renderCardEditor(); });
    byId("manualList").addEventListener("click", event => { const button = event.target.closest("[data-manual-id]"); if (!button) return; syncManualFromForm(); syncChapterFromForm(); activeManualId = button.dataset.manualId; activeChapterIndex = 0; renderManualList(); renderManualEditor(); });
    byId("cardSearch").addEventListener("input", renderCardList); byId("manualSearch").addEventListener("input", renderManualList); byId("clearCardSearchButton").addEventListener("click", () => { setField("cardSearch", ""); renderCardList(); }); byId("clearManualSearchButton").addEventListener("click", () => { setField("manualSearch", ""); renderManualList(); });
    byId("cardForm").addEventListener("input", event => { if (event.target.closest("#cardForm")) syncCardFromForm(); }); byId("cardForm").addEventListener("change", event => { if (!event.target.closest("#cardForm")) return; syncCardFromForm(); });
    byId("manualForm").addEventListener("input", event => { if (event.target.closest("#chapterEditor")) syncChapterFromForm(); else syncManualFromForm(); }); byId("manualForm").addEventListener("change", event => { if (event.target.closest("#chapterEditor")) syncChapterFromForm(); else syncManualFromForm(); });
    byId("cardForm").addEventListener("submit", event => { event.preventDefault(); syncCardFromForm(); const messages = cardValidation(currentCard()); saveStore(); toast(messages.some(item => item[0] === "error") ? "卡牌已保存，但还有规则错误" : "卡牌已保存", messages.some(item => item[0] === "error") ? "warning" : "success"); });
    byId("manualForm").addEventListener("submit", event => { event.preventDefault(); syncManualFromForm(); syncChapterFromForm(); const messages = manualValidation(currentManual()); saveStore(); toast(messages.some(item => item[0] === "error") ? "秘籍已保存，但还有规则错误" : "秘籍已保存", messages.some(item => item[0] === "error") ? "warning" : "success"); });
    byId("newCardButton").addEventListener("click", newCard); byId("duplicateCardButton").addEventListener("click", duplicateCard); byId("deleteCardButton").addEventListener("click", deleteCard); byId("newManualButton").addEventListener("click", newManual); byId("duplicateManualButton").addEventListener("click", duplicateManual); byId("deleteManualButton").addEventListener("click", deleteManual);
    byId("addDamageBonusButton").addEventListener("click", () => { currentCard().damage_bonuses.push({ attribute: "physique", multiplier: 0.1 }); renderDamageRows(currentCard().damage_bonuses); syncCardFromForm(); }); byId("addStatusButton").addEventListener("click", () => { currentCard().status_effects.push({ status_id: "", stacks: 1, hit_formula: "1.0" }); renderStatusRows(currentCard().status_effects); syncCardFromForm(); }); byId("addSpecialButton").addEventListener("click", () => { currentCard().special_effects.push({ effect: "draw", count: 1, formula: "" }); renderSpecialRows(currentCard().special_effects); syncCardFromForm(); });
    byId("addChapterButton").addEventListener("click", () => { const manual = currentManual(); manual.chapters.push(defaultChapter(manual.chapters.length + 1)); activeChapterIndex = manual.chapters.length - 1; renderChapterList(); renderChapterEditor(); renderManualValidation(manual); saveStore(); });
    byId("chapterList").addEventListener("click", event => { const button = event.target.closest("[data-chapter-index]"); if (!button) return; syncChapterFromForm(); activeChapterIndex = Number(button.dataset.chapterIndex); renderChapterList(); renderChapterEditor(); });
    byId("importButton").addEventListener("click", importFile); byId("importFile").addEventListener("change", async event => { const file = event.target.files?.[0]; if (!file) return; try { mergeImported(JSON.parse(await file.text())); } catch (_) { toast("JSON 文件无法读取", "warning"); } event.target.value = ""; }); byId("exportBundleButton").addEventListener("click", exportBundle); byId("exportCardJsonButton").addEventListener("click", () => exportCurrentCard("json")); byId("exportCardTresButton").addEventListener("click", () => exportCurrentCard("tres")); byId("exportManualJsonButton").addEventListener("click", () => exportCurrentManual("json")); byId("exportManualMarkdownButton").addEventListener("click", () => exportCurrentManual("markdown")); byId("exportManualTresButton").addEventListener("click", () => exportCurrentManual("tres"));
    byId("manualLearnPermanentBonuses").closest("fieldset")?.addEventListener("click", handleDynamicClick);
    document.addEventListener("click", handleDynamicClick);
    byId("startTestButton").addEventListener("click", startTest); byId("drawTestCardButton").addEventListener("click", () => { testStateOrStart(); drawTestCards(1); addTestLog("手动抽取 1 张。", "success"); renderTest(); }); byId("endTestTurnButton").addEventListener("click", endTestTurn); byId("clearTestLogButton").addEventListener("click", () => { if (testState) { testState.log = []; renderTest(); } }); byId("testRealmSelect").addEventListener("change", () => { touchTestConfig(); renderRealmSummary(); const profile = realmProfile(valueOf("testRealmSelect")); setField("testMaxMana", profile.maxMana); setField("testMana", profile.maxMana); setField("testMaxSpirit", profile.maxSpirit); setField("manualTestRealm", valueOf("testRealmSelect")); if (testState) addTestLog(`境界切换为 ${valueOf("testRealmSelect")}；点击开始新测试后应用。`, "muted"); }); byId("testDeckList").addEventListener("change", event => { if (!event.target.matches("[data-test-card]")) return; testDeckIds = [...document.querySelectorAll("[data-test-card]:checked")].map(input => input.value); byId("testDeckCount").textContent = `${testDeckIds.length} 张已选`; touchTestConfig(); }); byId("testView").addEventListener("input", event => { if (event.target.matches("input, select") && !event.target.closest("#syncDialog")) touchTestConfig(); }); byId("testHand").addEventListener("click", event => { const button = event.target.closest("[data-play-card]"); if (button) playTestCard(Number(button.dataset.playCard)); }); byId("testManualSelect").addEventListener("change", renderManualTest); byId("testChapterSelect").addEventListener("change", renderManualRequirements); byId("manualTestRealm").addEventListener("input", renderManualRequirements); byId("runManualTestButton").addEventListener("click", runManualTest); byId("resetTestAttributesButton").addEventListener("click", () => { renderTestAttributes(defaultTestAttributes()); touchTestConfig(); if (testState) testState.baseAttrs = readTestAttributes(); });
  }
  function handleDynamicClick(event) { const target = event.target; const map = target.closest("[data-map-add]"); if (map) { addMapRow(map.dataset.mapAdd); return; } const rewrite = target.closest("[data-add-rewrite]"); if (rewrite) { addRewrite(rewrite.dataset.addRewrite); return; } const remove = target.closest("[data-remove-row]"); if (remove) { const row = remove.closest(".repeat-row, .rewrite-row, .map-row"); row?.remove(); if (row?.closest("#cardForm")) syncCardFromForm(); else if (row?.closest("#chapterEditor")) syncChapterFromForm(); else if (row?.closest("#manualForm")) syncManualFromForm(); return; } if (target.closest("[data-delete-chapter]")) { const manual = currentManual(); if (manual.chapters.length <= 1) return toast("秘籍至少保留一卷", "warning"); manual.chapters.splice(activeChapterIndex, 1); activeChapterIndex = Math.max(0, activeChapterIndex - 1); renderChapterList(); renderChapterEditor(); renderManualValidation(manual); saveStore(); return; } const removeAttr = target.closest("[data-remove-test-attr]"); if (removeAttr) { removeAttr.closest(".map-row")?.remove(); return; } }

  function init() { setupStaticOptions(); initTestDeck(); setField("testRealmSelect", "云息三层"); renderTestAttributes(); const profile = realmProfile(valueOf("testRealmSelect")); setField("testEnergy", 3); setField("testMaxEnergy", 6); setField("testMana", profile.maxMana); setField("testMaxMana", profile.maxMana); setField("testSpirit", 0); setField("testMaxSpirit", profile.maxSpirit); setField("testEnemyHp", 60); setField("testShield", 0); setField("manualTestRealm", valueOf("testRealmSelect")); bindEvents(); renderAll(); saveStore(true); updateSyncIndicator(); if ("serviceWorker" in navigator && location.protocol !== "file:") navigator.serviceWorker.register("service-worker.js").catch(() => {}); }
  window.QingxiCardStudio = { store, cardValidation, manualValidation, formulaValue, realmProfile, cardToTres, manualToTres };
  init();
})();
