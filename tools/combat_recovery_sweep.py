#!/usr/bin/env python3
"""Compare candidate turn recovery for constructed cloud snapshots; no game changes."""
import argparse
import copy
import json
from pathlib import Path

import combat_fee_lab as lab


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--samples", type=int, default=1000, help="seed count; each seed is run at both speed orientations")
    args = parser.parse_args()
    if args.samples < 1:
        parser.error("--samples must be positive")
    costs, hashes = lab.load_resource_costs()
    outputs = {}
    for matchup in ("5_vs_5", "5_vs_7"):
        path = Path(__file__).with_name("combat_fee_lab_cloud_" + matchup + "_two_books.json")
        base = copy.deepcopy(lab.DEFAULTS)
        base.update(json.loads(path.read_text(encoding="utf-8")))
        for recovery in (1, 2, 3):
            cfg = copy.deepcopy(base)
            cfg["stamina_recovery"] = recovery
            mirror = copy.deepcopy(cfg)
            mirror["speeds"] = list(reversed(cfg["speeds"]))
            results = []
            for seed in range(1, args.samples + 1):
                results.append(lab.Battle(cfg, costs, seed, "first_clash_shared").run())
                results.append(lab.Battle(mirror, costs, seed, "first_clash_shared").run())
            summary = lab.summarize(results)
            sides = []
            for metrics in summary["mean_by_side"]:
                rounds = metrics.get("rounds_started_alive", 0)
                sides.append({
                    "mean_stamina_spent": metrics.get("stamina_spent", 0),
                    "mean_mana_spent": metrics.get("mana_spent", 0),
                    "mean_stamina_unaffordable": metrics.get("stamina_unaffordable", 0),
                    "mean_mana_unaffordable": metrics.get("mana_unaffordable", 0),
                    "mean_no_action_rounds": metrics.get("no_action_rounds", 0),
                    "no_action_round_fraction": metrics.get("no_action_rounds", 0) / rounds if rounds else 0,
                    "mean_rounds_started_alive": rounds,
                    "mean_cloud_damage": metrics.get("cloud_damage", 0),
                })
            outputs[matchup + "_recovery_" + str(recovery)] = {
                "games": summary["games"], "wins": summary["wins"],
                "round_median": summary["round_median"], "round_p90": summary["round_p90"],
                "timeouts": summary["timeouts"], "sides": sides,
            }
    print(json.dumps({
        "scope": "isolated first_clash_shared experiment; resources, skill effects, AI and per-round recovery are candidates; no official data changed",
        "limits": [
            "constructed Godot snapshots; no player saves; candidate deck includes skills the constructed character did not learn",
            "heart-method effects, artifacts, armor, critical and real cooldowns are not simulated",
            "no_action_round counts rounds started alive with no clash participation, support activation or attack resolution; includes death before acting",
            "resource skips count skipped actions, not unique cards or full rounds; untriggered unaffordable guards are not counted",
            "mirrored games reuse random streams; side labels and identities are not fully swapped",
        ],
        "samples": args.samples, "resource_sha256": hashes, "comparison": outputs,
    }, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
