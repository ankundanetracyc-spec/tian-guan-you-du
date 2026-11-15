/* Selection checks and qualitative hints for the isolated UI preview.
 * This module intentionally has no output-grade, quantity, cost or effect formula.
 */
(function () {
  "use strict";

  const catalog = window.TalismanCatalog;
  const utilityOrder = ["固", "聚", "通", "化", "引", "破", "护", "镇", "生", "明", "藏", "行"];
  const gradeNames = ["", "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品"];
  const feeds = { "木": "火", "火": "土", "土": "金", "金": "水", "水": "木" };
  const restrains = { "木": "土", "土": "水", "水": "火", "火": "金", "金": "木" };
  const byId = (items, id) => items.find(item => item.id === id);
  const elementsOf = item => Array.from(new Set(item.elements || (item.element ? [item.element] : [])));

  function eligible(material, craft) {
    if (!material) return false;
    if (craft === "paper") return ["纤维", "液体", "黏胶"].includes(material.texture);
    if (craft === "ink") return ["致密", "颗粒", "液体", "黏胶"].includes(material.texture);
    return false;
  }

  function normalizeQty(raw, stock) {
    const parsed = Number(raw);
    const maximum = Number.isFinite(Number(stock)) ? Math.max(0, Math.floor(Number(stock))) : 0;
    if (maximum < 1) return 0;
    return Math.min(maximum, Math.max(1, Number.isFinite(parsed) ? Math.floor(parsed) : 1));
  }

  function freshAnalysis() {
    return {
      isValid: false, blockers: [], notes: [], relations: [], total: 0, kinds: 0,
      availableUtilities: [], scope: "selection-and-qualitative-hints", outputCalculated: false
    };
  }

  function collectUtilities(items, callableOnly) {
    const sources = new Map();
    items.forEach(item => {
      const utilities = callableOnly ? (item.callableUtilities || {}) : (item.utilities || {});
      Object.entries(utilities).forEach(([name, grade]) => {
        if (!utilityOrder.includes(name) || !Number.isInteger(grade) || grade < 1 || grade > 9) return;
        if (!sources.has(name)) sources.set(name, []);
        sources.get(name).push({ id: item.id, grade });
      });
    });
    return utilityOrder.filter(name => sources.has(name)).map(name => {
      const sourceList = sources.get(name);
      const grades = Array.from(new Set(sourceList.map(source => source.grade))).sort((a, b) => b - a);
      return {
        name,
        // Highest present source only: this is candidate information, never an output grade.
        grade: grades[0], grades,
        gradeDisplay: grades.map(grade => gradeNames[grade]).join("／"),
        sources: sourceList
      };
    });
  }

  function findRelations(items) {
    const result = [];
    const seen = new Set();
    for (let a = 0; a < items.length; a += 1) {
      for (let b = a + 1; b < items.length; b += 1) {
        [[items[a], items[b]], [items[b], items[a]]].forEach(([fromItem, toItem]) => {
          elementsOf(fromItem).forEach(from => elementsOf(toItem).forEach(to => {
            const kind = feeds[from] === to ? "生" : (restrains[from] === to ? "克" : "");
            if (!kind) return;
            const key = from + kind + to;
            if (seen.has(key)) return;
            seen.add(key);
            result.push({ from, to, kind, fromName: fromItem.name, toName: toItem.name });
          }));
        });
      }
    }
    // These are possible contact relationships, not a sum of pairwise bonuses.
    return result;
  }

  function addRelationNotes(result) {
    const harmful = result.relations.find(relation => relation.kind === "克");
    const helpful = result.relations.find(relation => relation.kind === "生");
    if (harmful) result.notes.push({ tone: "warn", text: harmful.from + "克" + harmful.to + "，留意受克部分的保留。" });
    if (helpful) result.notes.push({ tone: "good", text: helpful.from + "生" + helpful.to + "，有助调和与留存。" });
  }

  function analyzeBatch(craft, selected, retained) {
    const result = freshAnalysis();
    if (!["paper", "ink"].includes(craft)) {
      result.blockers.push("请先选择制符纸或研墨。");
      return result;
    }
    const merged = new Map();
    (Array.isArray(selected) ? selected : []).forEach(entry => {
      const material = byId(catalog.materials, entry && entry.id);
      if (!material) {
        result.blockers.push("有未识别的材料，请重新选择。");
        return;
      }
      const qty = Number(entry.qty);
      if (!Number.isInteger(qty) || qty < 1) {
        result.blockers.push(material.name + "的份数需要正整数。");
        return;
      }
      merged.set(material.id, { material, qty: qty + (merged.get(material.id)?.qty || 0) });
    });
    const entries = Array.from(merged.values());
    const items = entries.map(entry => entry.material);
    result.kinds = items.length;
    result.total = entries.reduce((total, entry) => total + entry.qty, 0);
    entries.forEach(({ material, qty }) => {
      if (qty > material.stock) result.blockers.push(material.name + "超过演示可用份数。");
      if (!eligible(material, craft)) {
        result.blockers.push(material.name + "的质地不适用于" + (craft === "paper" ? "制符纸。" : "研墨。"));
      }
    });
    if (!items.length) {
      result.blockers.push("先选一些用料。");
      return result;
    }
    const legal = items.filter(item => eligible(item, craft));
    const has = texture => legal.some(item => item.texture === texture);
    if (craft === "paper") {
      if (!has("纤维")) result.blockers.push("还缺纤维纸基。");
      if (!has("液体")) result.blockers.push("还缺液料调和。");
      if (has("黏胶")) result.notes.push({ tone: "neutral", text: "胶料参与结合，也会改变纸的渗墨特点。" });
      if (legal.some(item => Object.hasOwn(item.utilities, "藏"))) {
        result.notes.push({ tone: "good", text: "用料有藏，可留作收口。" });
      }
    } else {
      if (!legal.some(item => ["致密", "颗粒", "液体"].includes(item.texture))) {
        result.blockers.push("还缺实际墨源，胶料不能单独成墨。");
      }
      if (legal.some(item => ["致密", "颗粒"].includes(item.texture)) && !has("液体")) {
        result.blockers.push("固体墨料还需液料调和。");
      }
      if (legal.every(item => item.texture === "液体")) {
        result.notes.push({ tone: "good", text: "液料可直接调墨，不必另加固体墨料。" });
      }
      if (has("黏胶")) result.notes.push({ tone: "neutral", text: "胶料可帮助附着，也可能使接续更难。" });
    }
    result.availableUtilities = collectUtilities(legal, false);
    const availableNames = new Set(result.availableUtilities.map(utility => utility.name));
    const retainedNames = Array.from(new Set((Array.isArray(retained) ? retained : []).map(value => typeof value === "string" ? value : value.name)));
    if (retainedNames.length > 4) result.blockers.push("保留功用最多四项。");
    retainedNames.forEach(name => {
      if (!availableNames.has(name)) result.blockers.push("当前用料没有" + name + "来源。");
    });
    result.relations = findRelations(legal);
    addRelationNotes(result);
    result.blockers = Array.from(new Set(result.blockers));
    result.isValid = result.blockers.length === 0;
    if (result.isValid && !result.notes.length) {
      result.notes.push({ tone: "good", text: "基础用料齐备，可按所选工法开始。" });
    }
    return result;
  }

  function analyzeDrawing(config) {
    const result = freshAnalysis();
    const options = config || {};
    const paper = byId(catalog.papers, options.paperId);
    const brush = byId(catalog.brushes, options.brushId);
    const recipe = byId(catalog.recipes, options.recipeId);
    const ids = Array.from(new Set(Array.isArray(options.inkIds) ? options.inkIds : []));
    const inks = ids.map(id => byId(catalog.inks, id));
    if (!paper) result.blockers.push("先选一批符纸。");
    if (!brush) result.blockers.push("先选一支符笔。");
    if (!recipe) result.blockers.push("先选已学符谱。");
    if (!inks.length) result.blockers.push("先选至少一份符墨。");
    if (inks.some(ink => !ink)) result.blockers.push("有未识别的符墨，请重新选择。");
    const actualInks = inks.filter(Boolean);
    const quantity = options.quantity === undefined ? 1 : Number(options.quantity);
    if (!Number.isInteger(quantity) || quantity < 1) result.blockers.push("准备张数需要正整数。");
    else if (paper && quantity > paper.stock) result.blockers.push("准备张数超过这批纸的库存。");
    result.total = Number.isInteger(quantity) && quantity > 0 ? quantity : 0;
    result.kinds = actualInks.length;
    const sources = (paper ? [paper] : []).concat(actualInks);
    result.availableUtilities = collectUtilities(sources, true);
    const availableNames = new Set(result.availableUtilities.map(utility => utility.name));
    if (recipe) recipe.requiredUtilities.forEach(name => {
      if (!availableNames.has(name)) result.blockers.push("所选符谱还缺" + name + "来源。");
    });
    const writing = options.writingId;
    if (["gather", "凝毫聚势"].includes(writing) && !availableNames.has("聚")) {
      result.blockers.push("凝毫聚势还缺实际聚来源。");
    }
    if (["penetrate", "透纸行墨"].includes(writing)) {
      if (!paper?.hasInnerPaths) result.blockers.push("所选纸没有可利用的内层通路。");
      if (!availableNames.has("通") || !availableNames.has("行")) result.blockers.push("透纸行墨还缺实际通、行支持。");
    }
    if (["continuous", "匀铺长行"].includes(writing) && brush && !brush.capabilities.continuous) {
      result.notes.push({ tone: "warn", text: "这支笔不擅长长段匀送，接续需要留意。" });
    }
    if (paper?.fields.渗墨性 === "偏高" && actualInks.some(ink => ink.fields.流动性 === "偏高")) {
      result.notes.push({ tone: "warn", text: "纸易渗、墨易铺开，留意无效洇散。" });
    }
    if (paper?.fields.承墨量 === "较少" && actualInks.some(ink => ink.fields.浓淡 === "偏浓")) {
      result.notes.push({ tone: "warn", text: "墨较浓，需留意这张纸的承接。" });
    }
    if (actualInks.length > 1) result.notes.push({ tone: "neutral", text: "兼用多份墨，留意墨性与收口。" });
    result.relations = findRelations(sources);
    addRelationNotes(result);
    if (paper && actualInks.length && availableNames.has("藏")) {
      result.notes.push({ tone: "good", text: "有实际留持来源，落笔前先为收口预留。" });
    }
    result.blockers = Array.from(new Set(result.blockers));
    result.isValid = result.blockers.length === 0;
    return result;
  }

  window.TalismanRules = Object.freeze({ eligible, normalizeQty, analyzeBatch, analyzeDrawing });
})();
