"""Read-only design calculator. Never creates game items or edits resource files."""
from __future__ import annotations

import argparse
from collections import Counter
from copy import deepcopy
from itertools import combinations, product
import json
import math
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PARAMETERS = ROOT / "docs/plans/剑类炼器首轮试算参数_20261021.json"
REPORT = ROOT / "docs/plans/剑类炼器首轮试算结果_20261021.md"
CYCLE = ["木", "火", "土", "金", "水"]
POLARITIES = ["至阴", "阴", "偏阴", "和", "偏阳", "阳", "至阳"]


def material(name):
    item_path = ROOT / "resources/entities/items" / f"{name}.tres"
    craft_path = ROOT / "resources/material_craft" / f"{name}材性.tres"
    item = item_path.read_text(encoding="utf-8")
    craft = craft_path.read_text(encoding="utf-8")
    utilities = {k: int(v) for k, v in re.findall(r'utility_id = "([^"]+)"\s+grade = (\d+)', craft)}
    grades = sorted(utilities.values(), reverse=True)
    score = 2 * grades[0] + (0.5 * grades[1] if len(grades) > 1 else 0) + 0.25 * sum(grades[2:]) if grades else 0
    element = re.search(r'^element = "([^"]*)"', craft, re.M)
    polarity = re.search(r'相性\[([^\]]+)\]', item)
    icon = re.search(r'icon_path = "res://([^"]+)"', item)
    if not polarity or not icon or not (ROOT / icon.group(1)).exists():
        raise ValueError(f"{name}: incomplete material or icon")
    return {"utilities": utilities, "element": element.group(1) if element else "", "polarity": polarity.group(1), "major": max(1, math.ceil(min(27, math.ceil(score)) / 3))}


def allocate(total, minima, caps, weights):
    """Reserve minima, then weighted water-fill without exceeding any source cap."""
    if any(minima[k] > caps[k] + 1e-8 for k in minima) or sum(minima.values()) > total + 1e-8:
        raise ValueError("功用效力不足")
    values = dict(minima)
    remaining = total - sum(values.values())
    while remaining > 1e-8:
        available = [k for k in values if caps[k] - values[k] > 1e-8]
        if not available:
            break
        weight_sum = sum(weights[k] for k in available)
        grants = {k: min(caps[k] - values[k], remaining * weights[k] / weight_sum) for k in available}
        spent = sum(grants.values())
        if spent < 1e-10:
            break
        for k, value in grants.items():
            values[k] += value
        remaining -= spent
    return values


def relation(root, target):
    if not root or not target:
        raise ValueError("首轮功用来源与根材均须单一五行")
    if root == target:
        return "同行", []
    ri, ti = CYCLE.index(root), CYCLE.index(target)
    if (ri + 1) % 5 == ti:
        return "根生性", []
    if (ti + 1) % 5 == ri:
        return "性生根", []
    bridges = []
    i = (ri + 1) % 5
    while i != ti:
        bridges.append(CYCLE[i])
        i = (i + 1) % 5
    return "通关", bridges


def auxiliary_plan(demands, p, materials, slots):
    """One unit pays one explicit quota; identical material units share a slot."""
    if not demands:
        return Counter()
    choices = []
    for kind, value in demands:
        choices.append([n for n in p["auxiliary_materials"] if (value in materials[n]["utilities"] if kind == "utility" else materials[n]["element"] == value)])
    if any(not c for c in choices):
        raise ValueError("没有匹配的辅助材料")
    best = None
    for assignment in product(*choices):
        counts = Counter(assignment)
        if len(counts) > slots or max(counts.values()) > p["auxiliary_max_per_slot"]:
            continue
        key = (sum(p["comparison_costs"][n] * q for n, q in counts.items()), len(counts), tuple(sorted(counts.items())))
        if best is None or key < best[0]:
            best = key, counts
    if best is None:
        raise ValueError("辅助槽位或份数不足")
    return best[1]


def bond_rewards(equipped, p):
    layers, seen = Counter(), {}
    for instance, bond in equipped:
        if instance in seen:
            if seen[instance] != bond:
                raise ValueError("同一法宝不能形成两个主要器蕴")
            continue
        seen[instance] = bond
        layers[bond] += p["artifact_bonds"][bond]["layers_per_item"]
    rewards = Counter()
    for bond, count in layers.items():
        for threshold, reward in p["artifact_bonds"][bond]["thresholds"].items():
            if count >= int(threshold):
                rewards.update(reward)
    return dict(rewards)


def calculate(config, p, materials):
    f = p["forms"][config["form"]]
    ids = config["methods"]
    if not ids or len(ids) != len(set(ids)) or any(x not in p["methods"] for x in ids):
        raise ValueError("方法为空、重复或未定义")
    methods = {x: p["methods"][x] for x in ids}
    utilities = sorted({x["utility"] for x in methods.values()})
    if not 1 <= len(utilities) <= 2 or set(config["sources"]) != set(utilities):
        raise ValueError("功用来源和方法不一致")
    inventory = Counter()
    polarity_inputs = []
    extra_units = 0
    unknown_extras = set(config.get("extras", {})) - set(f["parts"])
    if unknown_extras:
        raise ValueError("额外部件不存在")
    for part, (name, low, high) in f["parts"].items():
        extra = config.get("extras", {}).get(part, 0)
        if not isinstance(extra, int) or not 0 <= extra <= high - low:
            raise ValueError("身材投料越界")
        q = low + extra
        extra_units += extra
        inventory[name] += q
        polarity_inputs.append((name, q, "body"))
    root_name, root_q = config["root"], config["root_quantity"]
    if root_name not in p["roots"] or not isinstance(root_q, int) or not 1 <= root_q <= p["root_quantity_max"]:
        raise ValueError("根材投料越界")
    inventory[root_name] += root_q
    polarity_inputs.append((root_name, root_q, "root"))
    demands = []
    for method in methods.values():
        demands += [("utility", utility) for utility, q in method["auxiliary"].items() for _ in range(q)]
    efficiencies, supply, bridge_count = {}, {}, 0
    for utility in utilities:
        source_name, q = config["sources"][utility]
        key = p["utilities"][utility]
        if not isinstance(q, int) or not 1 <= q <= p["source_quantity_max"] or key not in materials[source_name]["utilities"]:
            raise ValueError("性材功用或份数不符")
        inventory[source_name] += q
        polarity_inputs.append((source_name, q, "source"))
        grade = materials[source_name]["utilities"][key]
        supply[utility] = p["source_supply_per_grade"] * grade * p["quantity_factors"][str(q)]
        relation_name, bridges = relation(materials[root_name]["element"], materials[source_name]["element"])
        efficiencies[utility] = p["relation_efficiency"][relation_name]
        demands += [("element", element) for element in bridges]
        bridge_count += len(bridges)
    drives = sum(x["kind"].startswith("drive_") for x in methods.values())
    load = sum(x["load"] for x in methods.values()) + (p["summon_common_load"] if drives else 0) + (p["dual_utility_load"] if len(utilities) == 2 else 0)
    capacity = f["capacity"] + p["capacity_per_extra_body_unit"] * extra_units
    if load > capacity:
        raise ValueError("器身承载不足")
    auxiliary = auxiliary_plan(demands, p, materials, f["auxiliary_slots"])
    inventory.update(auxiliary)
    polarity_inputs += [(name, q, "auxiliary") for name, q in auxiliary.items()]
    crafting = p["crafting"]
    difficulty = max(f["proficiency"], crafting["proficiency_per_extra_method"] * (len(ids) - 1), crafting["dual_utility_proficiency"] if len(utilities) == 2 else 0, crafting["proficiency_per_bridge_material"] * bridge_count)
    if config["proficiency"] < difficulty:
        raise ValueError("炼器造诣不足")
    retention = p["retention"]["entry"] + p["retention"]["per_proficiency"] * min(p["retention"]["extra_proficiency_cap"], config["proficiency"] - difficulty)
    throughput = min(p["roots"][root_name] * p["quantity_factors"][str(root_q)], f["throughput"] + p["support_per_extra_body_unit"] * extra_units)
    minima = {u: sum(m["minimum"] for m in methods.values() if m["utility"] == u) / (efficiencies[u] * retention) for u in utilities}
    weights = {u: p["priority_weights"][config.get("utility_priorities", {}).get(u, "并重")] for u in utilities}
    allocated = allocate(throughput, minima, supply, weights)
    effective = {u: allocated[u] * efficiencies[u] * retention for u in utilities}
    shares = {}
    for utility in utilities:
        selected = {name: m for name, m in methods.items() if m["utility"] == utility}
        shares.update(allocate(effective[utility], {k: m["minimum"] for k, m in selected.items()}, {k: effective[utility] for k in selected}, {k: p["priority_weights"][config.get("method_priorities", {}).get(k, "并重")] for k in selected}))
    outputs = {}
    e = p["effects"]
    for name, m in methods.items():
        factor = f["single_method_factor"] if len(ids) == 1 else 1.0
        if m["kind"] in {"direct_attack", "drive_attack"}:
            factor *= f["physical_active_factor"]
        value = shares[name] * factor
        if m["utility"] == "破障":
            outputs[name] = {"penetration_percent": min(e["penetration_percent_cap"], math.floor(value * e["penetration_percent_per_effect"] + 1e-8))}
            if m["kind"] != "rewrite":
                outputs[name]["physical_base"] = math.floor(e["physical_base"] + value * e["physical_per_effect"] + 1e-8)
        elif m["kind"] == "passive":
            outputs[name] = {"physical_defense": math.floor(value / e["defense_divisor"] + 1e-8)}
        elif m["kind"] == "rewrite":
            outputs[name] = {"resolve": math.floor(value / e["resolve_divisor"] + 1e-8)}
        else:
            outputs[name] = {"shield": math.floor(value / e["shield_divisor"] + 1e-8)}
        if any(v <= 0 for v in outputs[name].values()):
            raise ValueError("方法强度不足以形成有效结果")
    nonfuel_quantity = sum(inventory.values())
    heat = nonfuel_quantity + crafting["heat_per_load"] * load
    fuel_options = []
    for base in range(p["fuels"]["松心炭"]["minimum"], p["fuels"]["松心炭"]["maximum"] + 1):
        for advanced in range(p["fuels"]["地脉焰炭"]["minimum"], p["fuels"]["地脉焰炭"]["maximum"] + 1):
            counts = {"松心炭": base, "地脉焰炭": advanced}
            if sum(p["fuels"][n]["heat"] * q for n, q in counts.items()) >= heat:
                cost = sum(p["fuels"][n]["cost"] * q for n, q in counts.items())
                fuel_options.append((cost, sum(counts.values()), base, advanced))
    if not fuel_options:
        raise ValueError("燃料组供热不足")
    fuel_cost, _, base, advanced = min(fuel_options)
    fuel = {n: q for n, q in {"松心炭": base, "地脉焰炭": advanced}.items() if q}
    highest_major = max(materials[n]["major"] for n in inventory)
    baseline = sum(x[1] for x in f["parts"].values()) + 1 + len(utilities)
    extra_material = max(0, nonfuel_quantity - baseline)
    hours = math.ceil(4 * 2 ** (highest_major - 1) * f["time_factor"] * (1 + crafting["time_per_extra_method"] * (len(ids) - 1) + crafting["time_per_extra_utility"] * (len(utilities) - 1) + crafting["time_per_extra_material"] * extra_material) - 1e-8) / 4
    energy = math.ceil(hours * crafting["stamina_per_hour"])
    weighted_polarity = total_weight = 0.0
    for name, q, role in polarity_inputs:
        factor = p["quantity_factors"][str(q)]
        weight = p["polarity_weights"][role] * factor
        total_weight += weight
        weighted_polarity += p["polarity_values"][materials[name]["polarity"]] * weight
    raw = weighted_polarity / total_weight
    rank = math.floor(abs(raw) + 0.5) * (1 if raw >= 0 else -1)
    comparison_cost = sum(p["comparison_costs"][n] * q for n, q in inventory.items()) + fuel_cost
    margin = config["proficiency"] - difficulty
    gain = crafting["growth_full"] if margin < crafting["growth_full_margin"] else crafting["growth_reduced"] if margin < crafting["growth_stop_margin"] else 0
    gain = min(gain, max(0, difficulty + crafting["growth_stop_margin"] - config["proficiency"]))
    dominant = next((u for u in config["sources"] if config.get("utility_priorities", {}).get(u) == "主用"), next(iter(config["sources"])))
    bond = next(name for name, value in p["artifact_bonds"].items() if value["required_utility"] == dominant)
    return {"name": config.get("name", ""), "form": config["form"], "valid": True, "load": load, "capacity": capacity, "difficulty": difficulty, "retention": round(retention, 4), "supply": supply, "throughput": throughput, "effective": effective, "shares": shares, "effects": outputs, "materials": dict(inventory), "fuel": fuel, "heat": heat, "hours": hours, "energy": energy, "comparison_cost": comparison_cost, "polarity": POLARITIES[max(-3, min(3, rank)) + 3], "proficiency_gain": round(gain, 6), "bond": bond, "body_hp": f["body_hp"] + p["body_hp_per_extra_body_unit"] * extra_units + p["body_hp_per_extra_root_unit"] * (root_q - 1) if drives else None, "spirit": p["battle_costs"]["spirit_base"] + p["battle_costs"]["spirit_per_extra_drive_branch"] * (drives - 1) if drives else 0}


def outcome(config, p, materials):
    try:
        return calculate(config, p, materials)
    except ValueError as exc:
        return {"name": config.get("name", ""), "form": config["form"], "valid": False, "reason": str(exc)}


def checks(p, materials):
    passed = []
    def verify(condition, label):
        if not condition:
            raise AssertionError(label)
        passed.append(label)
    sample = p["samples"][0]
    baseline = calculate(sample, p, materials)
    verify(all(v >= 0 for v in baseline["shares"].values()), "效力非负")
    verify(sum(baseline["effective"].values()) <= baseline["throughput"] * max(p["relation_efficiency"].values()) + 1e-8, "共用根材不重复计入")
    verify(outcome(p["samples"][3], p, materials)["reason"] == "器身承载不足", "细身基础结构拒绝超载")
    reinforced = calculate(p["samples"][4], p, materials)
    verify(reinforced["effects"]["破障化法"]["penetration_percent"] < baseline["effects"]["破障化法"]["penetration_percent"], "同功多用分散单项效力")
    single = calculate(p["samples"][5], p, materials)
    triple = calculate(p["samples"][6], p, materials)
    verify(triple["effects"]["固结常驻"]["physical_defense"] < single["effects"]["固结常驻"]["physical_defense"], "三用常驻弱于单用")
    verify(triple["comparison_cost"] > single["comparison_cost"], "复合配置材料成本增加")
    duplicate = deepcopy(sample)
    duplicate["methods"] *= 2
    verify(not outcome(duplicate, p, materials)["valid"], "重复方法不可复制能力")
    excess = deepcopy(sample)
    excess["sources"]["破障"][1] = 4
    verify(not outcome(excess, p, materials)["valid"], "性材份数上限有效")
    low = deepcopy(p["samples"][2])
    low["proficiency"] = 0
    verify(outcome(low, p, materials)["reason"] == "炼器造诣不足", "造诣门槛独立检查")
    empty = allocate(100, {"a": 8, "b": 12}, {"a": 9, "b": 13}, {"a": 2, "b": 1})
    verify(abs(sum(empty.values()) - 22) < 1e-8, "材料不足不能吃掉未利用支撑")
    verify(relation("金", "木") == ("通关", ["水"]), "单桥通关方向正确")
    verify(relation("木", "金") == ("通关", ["火", "土"]), "逆向相克需要两桥")
    impossible = False
    try:
        auxiliary_plan([("utility", "通")] * 4, p, materials, 1)
    except ValueError:
        impossible = True
    verify(impossible, "一辅助槽不能无限投料")
    verify(auxiliary_plan([("utility", "聚"), ("utility", "藏")], p, materials, 1) == Counter({"灵石": 2}), "多功用材料按独立份数支付辅助要求")
    counterfactual = deepcopy(p)
    counterfactual["fuels"]["松心炭"]["heat"] *= 2
    hotter = calculate(sample, counterfactual, materials)
    verify(hotter["effects"] == baseline["effects"] and hotter["polarity"] == baseline["polarity"], "燃料不增加效果或阴阳")
    for key in POLARITIES:
        variant = deepcopy(materials)
        for entry in variant.values():
            entry["polarity"] = key
        verify(calculate(sample, p, variant)["polarity"] == key, f"七档保留：{key}")
    for config in p["samples"]:
        result = outcome(config, p, materials)
        if not result["valid"]:
            continue
        source_grade_count = sum(result["materials"].values())
        verify(source_grade_count >= sum(spec[1] for spec in p["forms"][config["form"]]["parts"].values()) + config["root_quantity"] + sum(x[1] for x in config["sources"].values()), f"独立投料记账：{config['name']}")
        verify(sum(p["fuels"][n]["heat"] * q for n, q in result["fuel"].items()) >= result["heat"], f"足量供热：{config['name']}")
    rookie = deepcopy(sample)
    rookie["proficiency"] = 0
    verify(calculate(rookie, p, materials)["proficiency_gain"] == 0.25, "造诣零可完成入门工艺并成长")
    veteran = deepcopy(sample)
    veteran["proficiency"] = 24
    verify(calculate(veteran, p, materials)["proficiency_gain"] == 0, "熟练简单工艺不无限提升造诣")
    bridge = {"form": "厚脊剑", "root": "青木芯", "root_quantity": 1, "sources": {"固结": ["玄铁", 1]}, "methods": ["固结常驻"], "proficiency": 16}
    verify(calculate(bridge, p, materials)["materials"].get("赤纹燧石") == 1 and calculate(bridge, p, materials)["materials"].get("紫砂灵土") == 1, "两桥实际占用两种辅助材料")
    double_drive = {"form": "厚脊剑", "root": "铜髓矿", "root_quantity": 1, "sources": {"破障": ["裂甲石", 1], "固结": ["玄铁", 1]}, "methods": ["破障御使", "固结御使"], "proficiency": 16}
    dd = calculate(double_drive, p, materials)
    verify(dd["load"] == 9 and dd["body_hp"] == 20 and dd["spirit"] == 3, "双御使分支共用器体和构型负担")
    for count in range(9):
        equipped = [(f"sword-{i}", "锋锐") for i in range(count)]
        expected = (3 if count >= 2 else 0) + (3 if count >= 4 else 0)
        verify(bond_rewards(equipped, p).get("physical_damage_percent_add", 0) == expected, f"器蕴阈值不重复：{count}层")
    verify(bond_rewards([("same", "凝固")] * 4, p) == {}, "同实例重复引用不增加器蕴层数")
    four = [(f"armor-{i}", "凝固") for i in range(4)]
    verify(bond_rewards(four, p)["physical_defense_add"] == 2 and bond_rewards(four[:3], p)["physical_defense_add"] == 1, "撤装后按实际层数重算奖励")
    return passed


def equal_cost_search(p, materials):
    """Find direct-attack optima under identical internal comparison budgets."""
    candidates = []
    for form, f in p["forms"].items():
        names = list(f["parts"])
        ranges = [range(spec[2] - spec[1] + 1) for spec in f["parts"].values()]
        for extras, root_q, source_q in product(product(*ranges), range(1, p["root_quantity_max"] + 1), range(1, p["source_quantity_max"] + 1)):
            config = {"form": form, "extras": dict(zip(names, extras)), "root": "铜髓矿", "root_quantity": root_q, "sources": {"破障": ["裂甲石", source_q]}, "methods": ["破障直施"], "proficiency": 24}
            r = outcome(config, p, materials)
            if r["valid"]:
                candidates.append(r)
    rows = []
    for budget in (14, 18, 22, 26):
        for form in p["forms"]:
            eligible = [x for x in candidates if x["form"] == form and x["comparison_cost"] <= budget]
            if not eligible:
                rows.append((budget, form, None))
            else:
                best = max(eligible, key=lambda x: (x["effects"]["破障直施"]["physical_base"], x["effects"]["破障直施"]["penetration_percent"], -x["comparison_cost"]))
                rows.append((budget, form, best))
    return rows


def combination_scan(p, materials):
    total = valid = 0
    rejected = Counter()
    ids = list(p["methods"])
    for count in range(1, len(ids) + 1):
        for selected in combinations(ids, count):
            utilities = {p["methods"][name]["utility"] for name in selected}
            sources = {u: ["裂甲石" if u == "破障" else "玄铁", 3] for u in sorted(utilities)}
            for form, f in p["forms"].items():
                config = {"form": form, "extras": {name: spec[2] - spec[1] for name, spec in f["parts"].items()}, "root": "铜髓矿", "root_quantity": 2, "sources": sources, "methods": list(selected), "proficiency": 32}
                total += 1
                result = outcome(config, p, materials)
                if not result["valid"]:
                    rejected[result["reason"]] += 1
                    continue
                valid += 1
                if result["load"] > result["capacity"] or sum(result["effective"].values()) > result["throughput"] + 1e-7:
                    raise AssertionError("组合遍历发现承载或同调效力越界")
                if abs(sum(result["shares"].values()) - sum(result["effective"].values())) > 1e-7:
                    raise AssertionError("组合遍历发现方法效力重复或遗失")
                if any(result["shares"][name] < p["methods"][name]["minimum"] - 1e-7 for name in selected):
                    raise AssertionError("组合遍历发现最低投入被绕过")
                for removed in selected:
                    reduced = [name for name in selected if name != removed]
                    if not reduced or {p["methods"][name]["utility"] for name in reduced} != utilities:
                        continue
                    smaller = deepcopy(config)
                    smaller["methods"] = reduced
                    previous = calculate(smaller, p, materials)
                    for name in reduced:
                        if result["shares"][name] > previous["shares"][name] + 1e-7:
                            raise AssertionError("增加方法反而免费提高原方法效力")
    return {"total": total, "valid": valid, "rejected": dict(rejected)}


def display_effects(result):
    return "；".join(f"{name}：" + "、".join(f"{key}={value}" for key, value in effect.items()) for name, effect in result["effects"].items())


def report(p, materials, passed, results, comparisons, scan):
    lines = ["# 剑类炼器首轮试算结果", "", "> 2026-10-21；候选模型离线结果，未经战斗平衡验收。内部比较成本不是灵石价格。未改游戏资源、正式库存或历法。", "", "参数：[首轮参数候选](剑类炼器首轮试算参数_20261021.json)；解释：[首轮试算方案](剑类炼器首轮试算方案_20261021.md)。", "", "## 一、配置样方", "", "| 样方 | 合法性 | 承载 | 内部比较成本 | 耗时／精力 | 候选结果 |", "| --- | --- | --- | --- | --- | --- |"]
    for r in results:
        if not r["valid"]:
            lines.append(f"| {r['name']} | {r['reason']} | — | — | — | 无成品 |")
        else:
            lines.append(f"| {r['name']} | 满足候选条件 | {r['load']}/{r['capacity']} | {r['comparison_cost']} | {r['hours']}时辰／{r['energy']} | {display_effects(r)} |")
    lines += ["", "效果字段：physical_base为成器技能的基础物理伤害，非目标实际受伤；penetration_percent为本法宝物防穿透百分比；physical_defense为常驻物防；resolve为实际应对追加化解；shield为新增护盾。", "", "## 二、逐方实际投料", ""]
    for r in results:
        if not r["valid"]:
            continue
        lines += [f"### {r['name']}", "", "非燃料：" + "、".join(f"{name}×{q}" for name, q in sorted(r["materials"].items())) + "。", "", "燃料：" + "、".join(f"{name}×{q}" for name, q in r["fuel"].items()) + f"；加工热量需求{r['heat']}。", "", f"造诣门槛{r['difficulty']}，本方加工保留率{r['retention']:.0%}；成功后造诣增长{r['proficiency_gain']}；材料阴阳倾向：{r['polarity']}；候选器蕴：{r['bond']}1层。", "", "各方法效力：" + "、".join(f"{name}={value:.3f}" for name, value in r["shares"].items()) + "。", ""]
        if r["body_hp"] is not None:
            lines += [f"御器共用器体{r['body_hp']}、在场灵识{r['spirit']}；不因分支重复召出器体。", ""]
    lines += ["## 三、同内部材料成本的直接攻伐比较", "", "同为铜髓矿根材、裂甲石破障、单直接方法，造诣24；穷举部件有效份数、根材1—2份、性材1—3份。比较预算包含辅助与燃料，不包括工时、精力或真实市场价格。", "", "| 预算 | 剑形 | 最优基础物伤 | 本法宝穿防 | 实际成本 |", "| --- | --- | --- | --- | --- |"]
    for budget, form, r in comparisons:
        if r is None:
            lines.append(f"| {budget} | {form} | 无合法配置 | — | — |")
        else:
            effect = r["effects"]["破障直施"]
            lines.append(f"| {budget} | {form} | {effect['physical_base']} | {effect['penetration_percent']}% | {r['comparison_cost']} |")
    lines += ["", "本表只比较一个攻击模板，不能证明全局平衡；还需把真实物品价值、目标防御、化解、护盾、回合资源和御器受击风险纳入实战验收。", "", "## 四、模型检查", ""]
    lines += [f"- 通过：{label}。" for label in passed]
    lines += ["", f"另遍历三种图谱和七类方法的全部非空子集，共{scan['total']}组（部件／根材／性材采用候选上限，造诣32）。其中{scan['valid']}组合法；拒绝原因：" + "、".join(f"{reason}{count}组" for reason, count in scan["rejected"].items()) + "。", "", "所有合法组合另查承载、同调供给、最低投入和方法分配守恒；在相同来源条件下增加方法，不会免费提高原方法的分配。遍历不代表每组都能在正式世界取得材料，也不是市场或战斗平衡验收。"]
    lines += ["", "## 五、边界", "", "本脚本验证候选参数的算术、材料记账与约束，不是Godot编译、旧27项底座检查或实际战斗测试。参数和器蕴奖励仍需评审后才能用于正式制作；材料来源点是否已接入见燃料材料任务记录。", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, default=REPORT)
    args = parser.parse_args()
    p = json.loads(PARAMETERS.read_text(encoding="utf-8"))
    names = set(p["comparison_costs"]) | set(p["fuels"])
    materials = {name: material(name) for name in names}
    passed = checks(p, materials)
    results = [outcome(config, p, materials) for config in p["samples"]]
    comparisons = equal_cost_search(p, materials)
    scan = combination_scan(p, materials)
    args.report.write_text(report(p, materials, passed, results, comparisons, scan), encoding="utf-8")
    print(json.dumps({"checks_passed": len(passed), "samples_valid": sum(r["valid"] for r in results), "samples_total": len(results), "combination_scan": scan, "report": str(args.report)}, ensure_ascii=True))


if __name__ == "__main__":
    main()
