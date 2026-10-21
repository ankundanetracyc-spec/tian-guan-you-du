"""Cross-language reference cases from the independent, read-only design calculator."""
from itertools import combinations
import json
from simulate_sword_crafting import ROOT, PARAMETERS, material, outcome

p = json.loads(PARAMETERS.read_text(encoding="utf-8"))
mats = {n: material(n) for n in set(p["comparison_costs"]) | set(p["fuels"])}
configs = list(p["samples"])
methods = list(p["methods"])
for size in range(1, len(methods) + 1):
    for selected in combinations(methods, size):
        utilities = sorted({p["methods"][m]["utility"] for m in selected})
        for form, f in p["forms"].items():
            configs.append({"form": form, "extras": {n: spec[2] - spec[1] for n, spec in f["parts"].items()}, "root": "铜髓矿", "root_quantity": 2, "sources": {u: ["裂甲石" if u == "破障" else "玄铁", 3] for u in utilities}, "methods": list(selected), "proficiency": 32})

cases = []
for config in configs:
    expected = outcome(config, p, mats)
    auxiliary = {}
    if expected["valid"]:
        remaining = dict(expected["materials"])
        def subtract(n, q):
            remaining[n] -= q
        for name, spec in p["forms"][config["form"]]["parts"].items():
            subtract(spec[0], spec[1] + config.get("extras", {}).get(name, 0))
        subtract(config["root"], config["root_quantity"])
        for n, q in config["sources"].values():
            subtract(n, q)
        auxiliary = {n: q for n, q in remaining.items() if q}
    cases.append({"config": config, "auxiliary": auxiliary, "expected": expected})

path = ROOT / "tools/fixtures/sword_crafting_ui.json"
path.parent.mkdir(exist_ok=True)
path.write_text(json.dumps(cases, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
print(f"{len(cases)} fixtures; {sum(c['expected']['valid'] for c in cases)} valid")
