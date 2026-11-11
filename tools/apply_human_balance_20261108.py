"""落实已授权的首轮六招平衡；独立记录允许的数值差异，保留迁移原始基线。"""
from pathlib import Path
import json
import re
from sync_board_effects_20261108 import ROOT, read, field, write
from validate_board_effects_20261108 import rule_hash

REPORT = "docs/plans/战斗与构筑/人类招式首轮平衡字段变更_20261108.json"


def patches(name):
    return {
        "灵击": {"energy_cost": ("2", "1"), "base_damage": ("4.0", "6.0")},
        "养锋": {"special_effects": ('Array[Dictionary]([{"effect":"gain_sword_intent","count":1}])',
                                     'Array[Dictionary]([{"effect":"gain_sword_intent","count":2}])')},
        "尽锋": {"damage_formula": tuple(json.dumps(x, ensure_ascii=False) for x in (
            "round(7 + intelligence*0.30 + magic_proficiency*0.35 + sword_proficiency*0.50 + sword_intent_stacks*2)",
            "round(7 + intelligence*0.30 + magic_proficiency*0.35 + sword_proficiency*0.50 + sword_intent_stacks*4)"))},
        "血臂撼岳": {"damage_formula": tuple(json.dumps(x, ensure_ascii=False) for x in (
            "round(7 + 体魄×0.55 + 体道造诣×0.25)", "round(10 + 体魄×0.65 + 体道造诣×0.35)"))},
        "乘势再攻": {"energy_cost": ("1", "2")},
        "云岚三叠": {"damage_formula": tuple(json.dumps(x, ensure_ascii=False) for x in (
            "round(5 + 体魄×0.40 + 剑道造诣×0.50)", "round(8 + 体魄×0.50 + 剑道造诣×0.60)"))},
    }.get(name, {})


def main():
    original = json.loads(read("docs/plans/战斗与构筑/人类招式效果字段基线_20261108.json"))["resources"]
    resources = {}
    for path, original_hash in original.items():
        text = read(path)
        changes = patches(field(text, "skill_name"))
        if not changes:
            assert rule_hash(text) == original_hash, f"非本轮资源出现数值差异：{path}"
            continue
        before = ROOT / "output/combat_balance_20261108/before" / path
        if not before.exists():
            assert rule_hash(text) == original_hash, f"首次修改前不符合原始基线：{path}"
            before.parent.mkdir(parents=True, exist_ok=True)
            before.write_text(text, encoding="utf-8")
        changed_fields = {}
        for key, (old, new) in changes.items():
            match = re.search(r"^" + re.escape(key) + r" = (.*)$", text, re.M)
            assert match and match[1] in (old, new), f"字段不是预期前后值：{path}:{key}"
            text = re.sub(r"^" + re.escape(key) + r" = .*", lambda _: key + " = " + new, text, flags=re.M)
            changed_fields[key] = {"before": old, "after": new}
        description = field(text, "description", "")
        for old, new in [
            ("round(4+灵慧×0.50+术法造诣×0.20)", "round(6+灵慧×0.50+术法造诣×0.20)"),
            ("自身获得剑意1层。", "自身获得剑意2层。"),
            ("剑意层数×2)", "剑意层数×4)"),
            ("round(7+体魄×0.55+体道造诣×0.25)", "round(10+体魄×0.65+体道造诣×0.35)"),
            ("round(5 + 体魄×0.40 + 剑道造诣×0.50)", "round(8 + 体魄×0.50 + 剑道造诣×0.60)"),
            ("round(2 + 体魄×0.20 + 剑道造诣×0.30 + max(本次行动开始时剑意层数−6,0))",
             "round(4 + 体魄×0.30 + 剑道造诣×0.50 + max(本次行动开始时剑意层数−6,0)×4)"),
        ]:
            description = description.replace(old, new)
        text = re.sub(r"^description = .*", lambda _: "description = " + json.dumps(description, ensure_ascii=False), text, flags=re.M)
        write(path, text)
        resources[path] = {"before_hash": original_hash, "after_hash": rule_hash(text), "fields": changed_fields}
    result = {"date": "2026-11-08", "authority": "人类招式首轮平衡与名称标注_20261108.md",
              "original_baseline_preserved": "人类招式效果字段基线_20261108.json",
              "scope": "六招及六份实际化法副本，共12份资源；只允许逐项列明字段，描述另行同步",
              "resources": resources}
    write(REPORT, json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(f"HUMAN_BALANCE_APPLIED resources={len(resources)} fields={sum(len(x['fields']) for x in resources.values())}")


if __name__ == "__main__":
    main()
