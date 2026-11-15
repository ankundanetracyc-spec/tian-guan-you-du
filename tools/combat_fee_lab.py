#!/usr/bin/env python3
"""Isolated, reproducible candidate combat experiment; not game rules.

The default data are explicitly illustrative. The six existing resource costs
are read from .tres files; damage formulas are copied from the design document,
not evaluated by the Godot runtime. No save, resource or project file is written.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import random
import re
import statistics
import sys
from collections import Counter, deque
from dataclasses import dataclass, field
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
RESOURCE_PATHS = {
    "应敌": "resources/activeskills/应敌.tres",
    "痛击": "resources/activeskills/痛击.tres",
    "格剑": "resources/activeskills/剑修基础/格剑.tres",
    "聚炁": "resources/activeskills/聚炁.tres",
    "惊蛰": "resources/activeskills/玄枢含章经/惊蛰.tres",
    "云岚三叠": "resources/activeskills/舒卷天章/云岚三叠.tres",
}

# These are candidate values for this experiment, not approved card data.
DEFAULTS = {
    "sample_note": "illustrative attributes, candidate rules; not runtime snapshots",
    "samples": 1000,
    "seed_start": 1,
    "max_rounds": 30,
    "initial_hand_size": 4,
    "draw_per_round": 2,
    "queue_capacity": 5,
    "initial_stamina": 6,
    "max_stamina": 6,
    "stamina_recovery": 3,
    "initial_mana": 2,
    "max_mana": 2,
    "initial_qi": 0,
    "hp": 120,
    "composure": 39,
    "composure_recovery": 4,
    "composure_after_stun": 20,
    "pressure_coefficient": 0.4,
    "physique": 19.8,
    "intelligence": 15.2,
    "soul": 17.8,
    "body_proficiency": 5,
    "sword_proficiency": 5,
    "magic_proficiency": 5,
    "die_sides": 6,
    "score_divisor": 10,
    "score_base": 3,
    "proficiency_weight": 0.5,
    "guard_score_bonus": 1,
    "dodge_score_bonus": 1,
    "skill_score_bonus": {},
    "follow_cost": 1,
    "repeat_cost": 1,
    "dodge_cost": 1,
    "guard_win_multiplier": 2,
    "guard_tie_multiplier": 1,
    "guard_loss_multiplier": 0.5,
    "damage_multiplier": 1.0,
    "damage_multipliers": {},
    "cost_overrides": {},
    "queue_costs": {"应敌": 1, "痛击": 2, "格剑": 1, "聚炁": 1,
                    "惊蛰": 2, "云岚三叠": 3, "闪避（试验）": 1},
    "deck": {"应敌": 1, "痛击": 2, "格剑": 2, "聚炁": 2,
             "惊蛰": 2, "云岚三叠": 2, "闪避（试验）": 1},
    "speeds": [[12, 8], [10, 7]],
    "mirror_speeds": True,
    "profiles": [{}, {}],
}

PROFILE_FIELDS = {"physique", "intelligence", "soul", "body_proficiency",
                  "sword_proficiency", "magic_proficiency", "hp", "composure",
                  "initial_stamina", "max_stamina", "stamina_recovery", "initial_mana",
                  "max_mana", "initial_qi", "composure_recovery", "composure_after_stun"}


def load_resource_costs() -> tuple[dict[str, tuple[int, int]], dict[str, str]]:
    costs, hashes = {}, {}
    for name, relative in RESOURCE_PATHS.items():
        raw = (ROOT / relative).read_bytes()
        text = raw.decode("utf-8")
        # DamageBonus subresources can precede [resource]; only read main fields.
        main = text.split("[resource]", 1)[-1]
        def number(key: str) -> int:
            match = re.search(rf"(?m)^{key}\s*=\s*(\d+)\s*$", main)
            return int(match.group(1)) if match else 0
        costs[name] = (number("energy_cost"), number("mana_cost"))
        hashes[relative] = hashlib.sha256(raw).hexdigest()
    return costs, hashes


def derived_int(seed: int, *parts: object) -> int:
    raw = "|".join(map(str, (seed, *parts))).encode("utf-8")
    return int.from_bytes(hashlib.sha256(raw).digest()[:8], "big")


@dataclass
class Action:
    name: str
    uid: str
    stage: int = 0
    attempt: int = 0
    held: bool = False
    prepared: bool = False
    intent_snapshot: int = 0
    dodge_streak: int = 0
    started: bool = False
    paid: bool = False


@dataclass
class Fighter:
    side: int
    hp: int
    composure: int
    stamina: int
    mana: int
    qi: int
    deck: list[Action]
    discard: list[Action] = field(default_factory=list)
    hand: list[Action] = field(default_factory=list)
    queues: list[deque[Action]] = field(default_factory=lambda: [deque(), deque()])
    guards: list[list[Action]] = field(default_factory=lambda: [[], []])
    sword_intent: int = 0
    stunned: bool = False
    metrics: Counter = field(default_factory=Counter)


class Battle:
    def __init__(self, cfg: dict, costs: dict, seed: int, repeat_policy: str, trace: bool = False):
        self.c = cfg
        self.costs = costs
        self.seed = seed
        self.repeat_policy = repeat_policy
        self.trace_enabled = trace
        self.events = []
        self.round = 0
        self.mini = 0
        self.event_serial = 0
        self.current_event = None
        self.payments = set()
        self.used = set()
        self.round_activity = [False, False]
        self.fighters = []
        for side in range(2):
            cards = [Action(name, f"{side}:{name}:{i}")
                     for name, count in cfg["deck"].items() for i in range(count)]
            random.Random(derived_int(seed, "deck", side, 0)).shuffle(cards)
            self.fighters.append(Fighter(side, self.value(side, "hp"), self.value(side, "composure"),
                                        self.value(side, "initial_stamina"), self.value(side, "initial_mana"),
                                        self.value(side, "initial_qi"), cards))
        self.shuffles = [0, 0]

    def value(self, side: int, key: str):
        return self.c["profiles"][side].get(key, self.c[key])

    def begin_event(self) -> None:
        self.event_serial += 1
        self.current_event = self.event_serial

    def event(self, kind: str, **data: object) -> None:
        if self.trace_enabled:
            self.events.append({"round": self.round, "mini": self.mini, "event": kind, **data})

    def draw(self, f: Fighter, count: int) -> list[Action]:
        result = []
        while len(result) < count:
            if not f.deck:
                if not f.discard:
                    break
                f.deck, f.discard = f.discard, []
                self.shuffles[f.side] += 1
                random.Random(derived_int(self.seed, "deck", f.side, self.shuffles[f.side])).shuffle(f.deck)
            result.append(f.deck.pop())
        return result

    def plan(self, f: Fighter) -> None:
        count = self.c["initial_hand_size"] if self.round == 1 else self.c["draw_per_round"]
        f.hand.extend(self.draw(f, count))
        hand = f.hand
        f.hand = []
        # Fixed visible-information policy; no opposing hand, action or draw order.
        order = {"聚炁": 0 if f.mana < self.value(f.side, "max_mana") else 5,
                 "格剑": 1, "闪避（试验）": 2, "痛击": 3,
                 "惊蛰": 4, "云岚三叠": 5, "应敌": 6}
        hand.sort(key=lambda a: (order[a.name], a.uid))
        cap = [0, 0]
        for a in hand:
            a.stage = a.attempt = 0
            a.dodge_streak = 0
            a.started = False
            a.paid = False
            a.held = a.prepared = False
            size = self.c["queue_costs"][a.name]
            prefer = 1 if a.name in ("聚炁", "格剑", "闪避（试验）") else 0
            for lane in (prefer, 1 - prefer):
                if cap[lane] + size <= self.c["queue_capacity"]:
                    f.queues[lane].append(a)
                    cap[lane] += size
                    break
            else:
                # A card not placed in either queue remains in hand.
                f.hand.append(a)
                f.metrics["unplaced_cards"] += 1
        self.event("plan", side=f.side, queues=[[a.name for a in q] for q in f.queues],
                   retained_hand=[a.name for a in f.hand])

    def fee(self, action: Action) -> tuple[int, int]:
        if self.repeat_policy in ("first_clash_once", "first_clash_shared") and action.paid:
            return 0, 0
        if action.stage:
            return self.c["follow_cost"], 0
        if action.name == "闪避（试验）":
            return self.c["dodge_cost"], 0
        stamina, mana = self.costs[action.name]
        if action.held and self.repeat_policy == "repeat_one":
            # Applies only to retained melee attacks, not dodges or follow-ups.
            stamina = self.c["repeat_cost"]
        return stamina, mana

    def affordable(self, f: Fighter, a: Action) -> bool:
        stamina, mana = self.fee(a)
        return f.stamina >= stamina and f.mana >= mana

    def record_unaffordable(self, f: Fighter, a: Action) -> None:
        stamina, mana = self.fee(a)
        f.metrics["unaffordable"] += 1
        if f.stamina < stamina: f.metrics["stamina_unaffordable"] += 1
        if f.mana < mana: f.metrics["mana_unaffordable"] += 1

    def pay(self, f: Fighter, a: Action, reason: str) -> None:
        stamina, mana = self.fee(a)
        if not self.affordable(f, a):
            raise AssertionError("invalid payment")
        if self.current_event is None:
            raise AssertionError("payment requires an event")
        key = (self.current_event, f.side, a.uid, a.stage)
        if key in self.payments:
            raise AssertionError("duplicate event payment")
        self.payments.add(key)
        if self.repeat_policy in ("first_clash_once", "first_clash_shared"):
            a.paid = True
        before = (f.stamina, f.mana)
        f.stamina -= stamina
        f.mana -= mana
        f.metrics["stamina_spent"] += stamina
        f.metrics["mana_spent"] += mana
        if reason == "clash" and a.held:
            f.metrics["repeat_clashes"] += 1
            f.metrics["repeat_stamina_spent"] += stamina
        if reason == "clash" and a.name == "闪避（试验）":
            f.metrics["dodge_attempts"] += 1
        if a.name == "云岚三叠":
            f.metrics[f"cloud_payment_events_{a.stage}"] += 1
            f.metrics["cloud_stamina_spent"] += stamina
            f.metrics["cloud_mana_spent"] += mana
        self.event("pay", event_id=self.current_event, side=f.side, action=a.name, uid=a.uid, stage=a.stage, reason=reason,
                   before=before, paid=(stamina, mana), after=(f.stamina, f.mana))

    def score(self, f: Fighter, a: Action) -> int:
        if a.name == "惊蛰":
            attribute, skill = self.value(f.side, "intelligence"), self.value(f.side, "magic_proficiency")
        elif a.name in ("格剑", "闪避（试验）"):
            attribute = min(self.value(f.side, "physique"), self.value(f.side, "soul"))
            skill = self.value(f.side, "sword_proficiency") if a.name == "格剑" else self.value(f.side, "body_proficiency")
        elif a.name == "云岚三叠":
            attribute, skill = self.value(f.side, "physique"), self.value(f.side, "sword_proficiency")
        else:
            attribute, skill = self.value(f.side, "physique"), self.value(f.side, "body_proficiency")
        bonus = self.c["guard_score_bonus"] if a.name == "格剑" else (
            self.c["dodge_score_bonus"] if a.name == "闪避（试验）" else 0)
        bonus += self.c["skill_score_bonus"].get(a.name, 0)
        roll = 1 + derived_int(self.seed, "roll", f.side, self.round, a.uid, a.stage, a.attempt) % self.c["die_sides"]
        a.attempt += 1
        return self.c["score_base"] + math.floor((attribute + self.c["proficiency_weight"] * skill) / self.c["score_divisor"]) + bonus + roll

    @staticmethod
    def mark_started(f: Fighter, a: Action) -> None:
        if a.name == "云岚三叠" and not a.started:
            f.metrics[f"cloud_started_{a.stage}"] += 1
            a.started = True

    def damage(self, attacker: Fighter, a: Action) -> int:
        p, i = self.value(attacker.side, "physique"), self.value(attacker.side, "intelligence")
        sword, magic = self.value(attacker.side, "sword_proficiency"), self.value(attacker.side, "magic_proficiency")
        if a.name == "应敌": value = 7 + .45 * p
        elif a.name == "痛击": value = 11 + .75 * p
        elif a.name == "惊蛰": value = 8 + .65 * i + .2 * magic
        elif a.name == "云岚三叠" and not a.stage: value = 8 + .5 * p + .6 * sword
        elif a.name == "云岚三叠": value = 4 + .3 * p + .5 * sword + max(a.intent_snapshot - 6, 0) * 4
        else: raise AssertionError(f"unexpected attack: {a.name}")
        multiplier = self.c["damage_multiplier"] * self.c["damage_multipliers"].get(a.name, 1.0)
        return math.floor(value * multiplier + 0.5)

    def remove(self, f: Fighter, lane: int, a: Action) -> None:
        if not f.queues[lane] or f.queues[lane][0] is not a:
            raise AssertionError("queue head changed")
        f.queues[lane].popleft()
        if a.stage == 0:
            f.discard.append(a)

    def hit(self, attacker: Fighter, lane: int, a: Action, defender: Fighter, block: int, success: bool) -> None:
        self.round_activity[attacker.side] = True
        self.mark_started(attacker, a)
        raw = self.damage(attacker, a)
        damage = max(0, raw - block)
        # For this experiment a winning attack makes contact even if blocked;
        # a losing attack makes contact only if its leftover damage is positive.
        contact = success or damage > 0
        defender.hp = max(0, defender.hp - damage)
        pressure = math.floor(damage * self.c["pressure_coefficient"] + .5)
        defender.composure = max(0, defender.composure - pressure)
        attacker.metrics["damage"] += damage
        if a.name == "云岚三叠": attacker.metrics["cloud_damage"] += damage
        self.event("hit", event_id=self.current_event, side=attacker.side, action=a.name, uid=a.uid, stage=a.stage, raw=raw,
                   block=block, contact=contact, damage=damage, target_hp=defender.hp, target_composure=defender.composure)
        self.remove(attacker, lane, a)
        if a.name == "云岚三叠" and contact:
            attacker.metrics[f"cloud_hit_{a.stage}"] += 1
            if success and contact and a.stage < 3 and defender.hp > 0:
                child = Action(a.name, a.uid.split(":stage:")[0] + ":stage:" + str(a.stage + 1),
                               stage=a.stage + 1, intent_snapshot=a.intent_snapshot,
                               paid=a.paid if self.repeat_policy == "first_clash_shared" else False)
                attacker.queues[lane].appendleft(child)
                attacker.metrics[f"cloud_generated_{child.stage}"] += 1
        if defender.composure == 0 and defender.hp > 0:
            defender.stunned = True
            defender.metrics["stuns"] += 1
            for q in defender.queues:
                while q:
                    item = q.popleft()
                    if item.stage == 0: defender.discard.append(item)
            for guard_list in defender.guards:
                defender.discard.extend(guard_list)
                guard_list.clear()
            self.event("stun", side=defender.side)

    @staticmethod
    def style(a: Action) -> str:
        return "ranged" if a.name == "惊蛰" else "melee"

    def free_tail(self) -> bool:
        return self.repeat_policy in ("free_tail_check", "free_tail_unchecked")

    def free_break_eligible(self, a: Action, b: Action | None) -> bool:
        return (self.repeat_policy == "free_tail_unchecked" and b is not None
                and self.style(a) == "melee" and self.style(b) == "ranged"
                and a.name not in ("格剑", "闪避（试验）", "聚炁"))

    def duel_free_tail(self, f: Fighter, lane: int, a: Action, g: Fighter, b: Action,
                       defense: bool, queued: bool) -> None:
        """Candidate only: free spell-breaking, payment on an effective result.

        Guard losing with partial mitigation pays; each successful dodge pays.
        These defense rules are experimental assumptions, not approved rules.
        """
        self.begin_event()
        sa, sb = self.score(f, a), self.score(g, b)
        self.event("clash", event_id=self.current_event, attacker=f.side, attack=a.name,
                   stage=a.stage, defender=g.side, response=b.name, scores=(sa, sb))
        if defense:
            if b.name == "格剑":
                g.sword_intent = min(10, g.sword_intent + 1)
                base = 2 + math.floor(self.value(g.side, "physique") / 5) + math.floor(self.value(g.side, "soul") / 8) + math.floor(self.value(g.side, "sword_proficiency") / 5)
                multiplier = self.c["guard_win_multiplier"] if sb > sa else (
                    self.c["guard_tie_multiplier"] if sb == sa else self.c["guard_loss_multiplier"])
                block = math.floor(base * multiplier)
                if block > 0:
                    self.pay(g, b, "defense_effect")
                if sa > sb or self.damage(f, a) > block:
                    self.pay(f, a, "hit")
                if queued: self.remove(g, lane, b)
                else: g.guards[lane].remove(b); g.discard.append(b)
                self.hit(f, lane, a, g, block, sa > sb)
            elif sb > sa:
                g.metrics["dodge_attempts"] += 1
                self.pay(g, b, "defense_effect")
                g.metrics["dodge_wins"] += 1
                b.dodge_streak += 1
                g.metrics["dodge_max_streak"] = max(g.metrics["dodge_max_streak"], b.dodge_streak)
                if b.dodge_streak == 3: g.metrics["dodge_three_plus"] += 1
                if queued: b.prepared = True
                self.remove(f, lane, a)
                self.event("dodge", side=g.side, result="win")
            else:
                g.metrics["dodge_attempts"] += 1
                g.metrics["dodge_failures"] += 1
                self.pay(f, a, "hit")
                if queued: self.remove(g, lane, b)
                else: g.guards[lane].remove(b); g.discard.append(b)
                self.hit(f, lane, a, g, 0, sa > sb)
            return
        if sa == sb:
            self.remove(f, lane, a); self.remove(g, lane, b)
        elif self.style(a) != self.style(b):
            winner, wa = (f, a) if sa > sb else (g, b)
            loser, la = (g, b) if sa > sb else (f, a)
            if self.style(wa) == "melee":
                self.remove(loser, lane, la)
                if not winner.queues[lane] or winner.queues[lane][0] is not wa:
                    raise AssertionError("deflecting action must be queue head")
                winner.queues[lane].rotate(-1)
                wa.held = True
                winner.metrics["deflections"] += 1
                winner.metrics["free_deflections"] += 1
                if not self.affordable(winner, wa): winner.metrics["free_deflections_unaffordable"] += 1
                self.event("deflect_to_tail", event_id=self.current_event, side=winner.side,
                           action=wa.name, uid=wa.uid, lane=lane)
            else:
                self.remove(loser, lane, la)
                self.pay(winner, wa, "hit")
                self.hit(winner, lane, wa, loser, 0, True)
        elif sa > sb:
            self.remove(g, lane, b)
            self.pay(f, a, "hit")
            self.hit(f, lane, a, g, 0, True)
        else:
            self.remove(f, lane, a)
            self.pay(g, b, "hit")
            self.hit(g, lane, b, f, 0, True)

    def duel(self, f: Fighter, lane: int, a: Action, g: Fighter, b: Action, defense: bool, queued: bool) -> None:
        self.round_activity[f.side] = self.round_activity[g.side] = True
        self.mark_started(f, a)
        self.mark_started(g, b)
        if self.free_tail():
            self.duel_free_tail(f, lane, a, g, b, defense, queued)
            return
        self.begin_event()
        if self.repeat_policy in ("first_clash_once", "first_clash_shared"):
            if a.paid: f.metrics["paid_action_reclashes" if a.attempt else "prepaid_continuation_clashes"] += 1
            else: self.pay(f, a, "first_clash")
            if b.paid: g.metrics["paid_action_reclashes" if b.attempt else "prepaid_continuation_clashes"] += 1
            else: self.pay(g, b, "first_clash")
            if defense and b.name == "闪避（试验）": g.metrics["dodge_attempts"] += 1
        else:
            self.pay(f, a, "clash")
            self.pay(g, b, "clash")
        sa, sb = self.score(f, a), self.score(g, b)
        self.event("clash", event_id=self.current_event, attacker=f.side, attack=a.name, stage=a.stage,
                   defender=g.side, response=b.name, scores=(sa, sb))
        if defense:
            if b.name == "格剑":
                g.sword_intent = min(10, g.sword_intent + 1)
                base = 2 + math.floor(self.value(g.side, "physique") / 5) + math.floor(self.value(g.side, "soul") / 8) + math.floor(self.value(g.side, "sword_proficiency") / 5)
                multiplier = self.c["guard_win_multiplier"] if sb > sa else (
                    self.c["guard_tie_multiplier"] if sb == sa else self.c["guard_loss_multiplier"])
                block = math.floor(base * multiplier)
                if queued: self.remove(g, lane, b)
                else: g.guards[lane].remove(b); g.discard.append(b)
                self.hit(f, lane, a, g, block, sa > sb)
            elif sb > sa:
                g.metrics["dodge_wins"] += 1
                b.dodge_streak += 1
                g.metrics["dodge_max_streak"] = max(g.metrics["dodge_max_streak"], b.dodge_streak)
                if b.dodge_streak == 3: g.metrics["dodge_three_plus"] += 1
                if queued: b.prepared = True
                self.remove(f, lane, a)
                self.event("dodge", side=g.side, result="win")
            else:
                g.metrics["dodge_failures"] += 1
                if queued: self.remove(g, lane, b)
                else: g.guards[lane].remove(b); g.discard.append(b)
                self.hit(f, lane, a, g, 0, sa > sb)
            return
        # Each melee-vs-ranged victory retains the melee action until later.
        if sa == sb:
            self.remove(f, lane, a); self.remove(g, lane, b)
        elif self.style(a) != self.style(b):
            winner, winner_action = (f, a) if sa > sb else (g, b)
            loser, loser_action = (g, b) if sa > sb else (f, a)
            if self.style(winner_action) == "melee":
                self.remove(loser, lane, loser_action)
                winner_action.held = True
                if self.repeat_policy in ("first_clash_once", "first_clash_shared"):
                    winner.queues[lane].rotate(-1)
                winner.metrics["deflections"] += 1
                self.event("deflect", side=winner.side, action=winner_action.name)
            else:
                self.remove(loser, lane, loser_action)
                self.hit(winner, lane, winner_action, loser, 0, True)
        elif sa > sb:
            self.remove(g, lane, b); self.hit(f, lane, a, g, 0, True)
        else:
            self.remove(f, lane, a); self.hit(g, lane, b, f, 0, True)

    def act(self, side: int, lane: int) -> bool:
        f, g = self.fighters[side], self.fighters[1 - side]
        if not f.hp or f.stunned or not f.queues[lane]: return False
        a = f.queues[lane][0]
        if a.name in ("格剑", "闪避（试验）"):
            if not a.prepared:
                a.prepared = True
                if a.name == "格剑":
                    f.queues[lane].popleft(); f.guards[lane].append(a)
                self.event("wait", side=side, action=a.name)
                return True
            return False
        target = g.queues[lane][0] if g.queues[lane] and (g.side, lane) not in self.used else None
        may_break_without_resources = (self.free_break_eligible(a, target)
                                       and not any(self.affordable(g, guard) for guard in g.guards[lane])
                                       and target is not None and self.affordable(g, target))
        if (self.free_tail() or not a.held) and not self.affordable(f, a) and not may_break_without_resources:
            self.record_unaffordable(f, a)
            if a.held: f.metrics["repeat_unaffordable"] += 1
            self.event("skip_unaffordable", side=side, action=a.name, stage=a.stage)
            self.remove(f, lane, a)
            return True
        if a.name == "聚炁":
            self.begin_event()
            self.pay(f, a, "activation")
            self.round_activity[f.side] = True
            f.qi += 2
            max_mana = self.value(side, "max_mana")
            if max_mana > 0 and f.qi >= 2 * max_mana:
                f.qi = 0; f.mana = max_mana; f.metrics["zhou_tian"] += 1
            self.remove(f, lane, a)
            return True
        if a.name == "云岚三叠" and a.stage == 0 and a.attempt == 0:
            a.intent_snapshot = f.sword_intent
        guard = next((x for x in g.guards[lane] if self.affordable(g, x)), None)
        if guard is not None:
            if not self.affordable(f, a):
                self.record_unaffordable(f, a); f.metrics["repeat_unaffordable"] += int(a.held)
                self.event("skip_unaffordable", side=side, action=a.name, stage=a.stage, retained=a.held)
                self.remove(f, lane, a); return True
            self.duel(f, lane, a, g, guard, True, False)
            return True
        # A successful dodge stays in the same queue and can respond again even
        # if that queue has already had its waiting turn in this mini-round.
        ready_dodge = g.queues[lane][0] if g.queues[lane] else None
        if ready_dodge and ready_dodge.name == "闪避（试验）" and ready_dodge.prepared:
            if self.affordable(g, ready_dodge):
                if not self.affordable(f, a):
                    self.record_unaffordable(f, a); f.metrics["repeat_unaffordable"] += int(a.held)
                    self.event("skip_unaffordable", side=side, action=a.name, stage=a.stage, retained=a.held)
                    self.remove(f, lane, a); return True
                self.duel(f, lane, a, g, ready_dodge, True, True)
                return True
            g.metrics["dodge_unaffordable"] += 1
            self.remove(g, lane, ready_dodge)
        b = g.queues[lane][0] if g.queues[lane] and (g.side, lane) not in self.used else None
        if b is not None and b.name != "聚炁" and (self.affordable(g, b) or self.free_break_eligible(b, a)):
            if not self.affordable(f, a) and not self.free_break_eligible(a, b):
                self.record_unaffordable(f, a); f.metrics["repeat_unaffordable"] += int(a.held)
                self.event("skip_unaffordable", side=side, action=a.name, stage=a.stage, retained=a.held)
                self.remove(f, lane, a); return True
            self.used.add((g.side, lane))
            self.duel(f, lane, a, g, b, b.name in ("格剑", "闪避（试验）"), True)
            return True
        if b is not None and b.name == "闪避（试验）" and b.prepared and not self.affordable(g, b):
            self.remove(g, lane, b)
        self.begin_event()
        already_paid = self.repeat_policy in ("first_clash_once", "first_clash_shared") and a.paid
        if self.free_tail() or (not a.held and not already_paid):
            self.pay(f, a, "activation")
        self.hit(f, lane, a, g, 0, True)
        return True

    def cleanup(self, f: Fighter) -> None:
        for q in f.queues:
            while q:
                a = q.popleft()
                if a.stage == 0: f.discard.append(a)
        for guards in f.guards:
            f.discard.extend(guards); guards.clear()
        expected = sum(self.c["deck"].values())
        if len(f.deck) + len(f.discard) + len(f.hand) != expected:
            raise AssertionError("card conservation violated")

    def run(self) -> dict:
        for turn in range(1, self.c["max_rounds"] + 1):
            self.round = turn
            self.round_activity = [False, False]
            alive_at_start = [f.hp > 0 for f in self.fighters]
            for f in self.fighters:
                f.stamina = min(self.value(f.side, "max_stamina"), f.stamina + self.value(f.side, "stamina_recovery"))
                if f.stunned:
                    f.stunned = False; f.composure = min(self.value(f.side, "composure"), self.value(f.side, "composure_after_stun"))
                elif turn > 1:
                    f.composure = min(self.value(f.side, "composure"), f.composure + self.value(f.side, "composure_recovery"))
                self.plan(f)
            order = sorted(((side, lane) for side in range(2) for lane in range(2)),
                           key=lambda x: (-self.c["speeds"][x[0]][x[1]], (x[0] + turn) % 2, x[1]))
            for mini in range(1, 101):
                self.mini = mini; self.used = set(); progress = False
                for side, lane in order:
                    if (side, lane) in self.used or not all(f.hp for f in self.fighters): continue
                    self.used.add((side, lane))
                    progress = self.act(side, lane) or progress
                if not all(f.hp for f in self.fighters) or not progress: break
            else:
                raise AssertionError("mini-round limit exceeded")
            for f in self.fighters:
                if alive_at_start[f.side]:
                    f.metrics["rounds_started_alive"] += 1
                    if not self.round_activity[f.side]: f.metrics["no_action_rounds"] += 1
                self.cleanup(f)
            if not all(f.hp for f in self.fighters): break
        winner = next((f.side for f in self.fighters if f.hp > 0), None) if not all(f.hp for f in self.fighters) else None
        return {"seed": self.seed, "policy": self.repeat_policy, "rounds": self.round,
                "winner": winner, "timeout": all(f.hp for f in self.fighters),
                "fighters": [{"hp": f.hp, "stamina": f.stamina, "mana": f.mana,
                              "composure": f.composure, "metrics": dict(f.metrics)} for f in self.fighters],
                "events": self.events if self.trace_enabled else []}


def validate(cfg: dict) -> None:
    if not 1 <= cfg["max_rounds"] <= 100 or not 1 <= cfg["samples"] <= 100000:
        raise ValueError("max_rounds or samples outside allowed range")
    if cfg["die_sides"] < 2 or cfg["score_divisor"] <= 0:
        raise ValueError("invalid score parameters")
    if len(cfg["speeds"]) != 2 or any(len(row) != 2 for row in cfg["speeds"]):
        raise ValueError("exactly two speeds per side required")
    if set(cfg["deck"]) - (set(RESOURCE_PATHS) | {"闪避（试验）"}):
        raise ValueError("unsupported deck card")
    if not cfg["deck"] or any(not isinstance(n, int) or n < 0 for n in cfg["deck"].values()):
        raise ValueError("invalid deck counts")
    if cfg["initial_hand_size"] < 0 or cfg["draw_per_round"] < 0:
        raise ValueError("negative draw size")
    if sum(cfg["deck"].values()) < cfg["initial_hand_size"]:
        raise ValueError("deck smaller than initial hand")
    if any(cfg[k] < 0 for k in ("initial_stamina", "max_stamina", "initial_mana", "max_mana")):
        raise ValueError("negative resource parameter")
    if any(cfg[k] <= 0 for k in ("follow_cost", "repeat_cost", "dodge_cost")):
        raise ValueError("candidate repeat, follow and dodge fees must be positive")
    if cfg["damage_multiplier"] < 0 or any(x < 0 for x in cfg["damage_multipliers"].values()):
        raise ValueError("negative damage multiplier")
    if set(cfg["cost_overrides"]) - set(RESOURCE_PATHS):
        raise ValueError("cost overrides must refer to known resources")
    for cost in cfg["cost_overrides"].values():
        if not isinstance(cost, list) or len(cost) != 2 or any(not isinstance(x, int) or x < 0 for x in cost):
            raise ValueError("cost override must be [nonnegative stamina, nonnegative mana]")
        if cost == [0, 0]:
            raise ValueError("zero-resource skill overrides require a separately defined limit")
    if cfg["initial_stamina"] > cfg["max_stamina"] or cfg["initial_mana"] > cfg["max_mana"]:
        raise ValueError("initial resource exceeds cap")
    if any(cfg["queue_costs"].get(name, 0) <= 0 for name in cfg["deck"]):
        raise ValueError("deck card needs a positive queue cost")
    if len(cfg["profiles"]) != 2 or any(set(p) - PROFILE_FIELDS for p in cfg["profiles"]):
        raise ValueError("profiles must contain two objects with supported fields")
    for profile in cfg["profiles"]:
        v = lambda key: profile.get(key, cfg[key])
        if any(v(k) < 0 for k in PROFILE_FIELDS) or v("hp") <= 0 or v("composure") <= 0:
            raise ValueError("invalid profile values")
        if v("initial_stamina") > v("max_stamina") or v("initial_mana") > v("max_mana"):
            raise ValueError("profile initial resource exceeds cap")


def summarize(results: list[dict]) -> dict:
    rounds = sorted(r["rounds"] for r in results)
    metric_names = {k for r in results for f in r["fighters"] for k in f["metrics"]}
    return {"games": len(results), "wins": [sum(r["winner"] == s for r in results) for s in range(2)],
            "timeouts": sum(r["timeout"] for r in results),
            "round_median": statistics.median(rounds), "round_p90": rounds[math.ceil(.9 * len(rounds)) - 1],
            "mean_by_side": [{name: sum(r["fighters"][s]["metrics"].get(name, 0) for r in results) / len(results)
                              for name in sorted(metric_names)} for s in range(2)]}


def self_test(costs: dict, cfg: dict) -> None:
    # Cost accounting: held action repeats at original versus separately-set fee.
    for policy, expected in (("original", 2), ("repeat_one", 1)):
        battle = Battle(cfg, costs, 1, policy, True)
        actor = battle.fighters[0]
        actor.stamina = 3
        action = Action("痛击", "test", held=True)
        assert battle.fee(action) == (expected, 0)
        battle.begin_event()
        battle.pay(actor, action, "clash")
        assert actor.stamina == 3 - expected
    # Two remote attacks: a retained melee strike must afford each new clash,
    # but its eventual uncontested hit is already paid for.
    for policy in ("original", "repeat_one"):
        battle = Battle(cfg, costs, 2, policy, True)
        a, b = battle.fighters
        a.stamina = 3; b.mana = 2
        a.queues[0].append(Action("痛击", "held"))
        b.queues[0].extend((Action("惊蛰", "shot-1"), Action("惊蛰", "shot-2")))
        battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
        assert a.stamina == 1 and a.queues[0][0].held
        battle.act(0, 0)
        if policy == "original":
            assert not a.queues[0] and len(b.queues[0]) == 1 and a.stamina == 1
        else:
            assert a.stamina == 0 and len(b.queues[0]) == 0 and a.queues[0][0].held
            payments = len(battle.payments)
            before_hp = b.hp
            battle.act(0, 0)
            assert b.hp < before_hp and len(battle.payments) == payments
    # One action both activates and clashes; only one payment is made.
    battle = Battle(cfg, costs, 7, "original", True)
    a, b = battle.fighters
    a.queues[0].append(Action("应敌", "a")); b.queues[0].append(Action("应敌", "b"))
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    assert sum(e["event"] == "pay" for e in battle.events) == 2
    # Consecutive dodge pays every time and remains only while it wins.
    battle = Battle(cfg, costs, 7, "original", True)
    a, b = battle.fighters
    a.queues[0].extend(Action("应敌", f"attack-{i}") for i in range(3))
    dodge = Action("闪避（试验）", "dodge")
    b.queues[0].append(dodge)
    rolls = iter((8, 9, 8, 9, 10, 9))
    battle.score = lambda fighter, action: next(rolls)
    for _ in range(2):
        battle.duel(a, 0, a.queues[0][0], b, dodge, True, True)
        assert b.queues[0][0] is dodge
    battle.duel(a, 0, a.queues[0][0], b, dodge, True, True)
    assert not b.queues[0] and b.stamina == cfg["initial_stamina"] - 3 * cfg["dodge_cost"]
    assert sum(e["event"] == "pay" for e in battle.events) == 6
    # A waiting dodge can still respond after its queue's own turn.
    battle = Battle(cfg, costs, 8, "original", True)
    a, b = battle.fighters
    a.queues[0].append(Action("应敌", "attacker"))
    dodge = Action("闪避（试验）", "prepared", prepared=True)
    b.queues[0].append(dodge)
    battle.used.add((1, 0))
    rolls = iter((8, 9))
    battle.score = lambda fighter, action: next(rolls)
    battle.act(0, 0)
    assert not a.queues[0] and b.queues[0][0] is dodge
    # Cloud generates only one next action per successful stage.
    battle = Battle(cfg, costs, 9, "original", True)
    a, b = battle.fighters
    a.queues[0].append(Action("云岚三叠", "cloud"))
    b.queues[0].append(Action("应敌", "enemy"))
    rolls = iter((10, 8))
    battle.score = lambda fighter, action: next(rolls)
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    assert len(a.queues[0]) == 1 and a.queues[0][0].stage == 1
    battle.act(0, 0)
    assert len(a.queues[0]) == 1 and a.queues[0][0].stage == 2
    battle = Battle(cfg, costs, 9, "original", True)
    a, b = battle.fighters
    a.queues[0].append(Action("云岚三叠", "cloud-loss"))
    b.queues[0].append(Action("格剑", "guard"))
    rolls = iter((8, 9))
    battle.score = lambda fighter, action: next(rolls)
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], True, True)
    assert not a.queues[0] and not a.metrics["cloud_generated_1"]
    # Waiting alone has no cost.
    battle = Battle(cfg, costs, 10, "original", True)
    a = battle.fighters[0]
    a.queues[0].append(Action("格剑", "guard"))
    battle.act(0, 0)
    assert a.stamina == cfg["initial_stamina"] and not any(e["event"] == "pay" for e in battle.events)
    # Placed cards are discarded after the round, while unplaced cards remain.
    small = copy.deepcopy(cfg); small["queue_capacity"] = 1
    battle = Battle(small, costs, 11, "original", True); battle.round = 1
    a = battle.fighters[0]; battle.plan(a)
    retained = [card.uid for card in a.hand]
    assert retained
    battle.cleanup(a)
    assert [card.uid for card in a.hand] == retained
    # Repeatable seed, replay and card conservation through complete battles.
    one = Battle(cfg, costs, 14, "original", True).run()
    two = Battle(cfg, costs, 14, "original", True).run()
    assert one == two
    assert all(0 <= f["stamina"] <= cfg["max_stamina"] and 0 <= f["mana"] <= cfg["max_mana"] for f in one["fighters"])
    # Newly requested accounting: move behind own next action; two free breaks;
    # only the eventual damage pays, and the two defeated spells pay nothing.
    for policy in ("free_tail_check", "free_tail_unchecked"):
        battle = Battle(cfg, costs, 15, policy, True)
        a, b = battle.fighters
        a.stamina = 6; b.mana = 2
        a.queues[0].extend((Action("痛击", "strike"), Action("聚炁", "support")))
        b.queues[0].extend((Action("惊蛰", "shot-1"), Action("惊蛰", "shot-2")))
        battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
        assert [x.uid for x in a.queues[0]] == ["support", "strike"]
        assert a.stamina == 6 and b.mana == 2
        battle.act(0, 0)
        assert a.stamina == 5
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
        assert a.stamina == 5 and b.mana == 2 and a.metrics["free_deflections"] == 2
        health = b.hp
        battle.act(0, 0)
        assert b.hp < health and a.stamina == 3
        assert sum(e["event"] == "pay" and e["action"] == "痛击" for e in battle.events) == 1
        assert not any(e["event"] == "pay" and e["action"] == "惊蛰" for e in battle.events)
        # A defeated strike costs nothing; the successful spell pays once.
        battle = Battle(cfg, costs, 16, policy, True)
        a, b = battle.fighters
        a.queues[0].append(Action("痛击", "strike")); b.queues[0].append(Action("惊蛰", "spell"))
        battle.score = lambda fighter, action: 9 if fighter.side == 0 else 10
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
        assert a.stamina == 6 and b.mana == 1
        # Contact and health damage are distinct: a winning attack costs even
        # when guard absorbs its whole hit; partial guard is a candidate fee.
        battle = Battle(cfg, costs, 16, policy, True)
        a, b = battle.fighters
        a.queues[0].append(Action("痛击", "blocked-strike")); b.queues[0].append(Action("格剑", "guard"))
        battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
        battle.damage = lambda fighter, action: 1
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], True, True)
        hit = next(e for e in battle.events if e["event"] == "hit")
        assert hit["contact"] and hit["damage"] == 0 and a.stamina == 4 and b.stamina == 5
        # The chosen dodge sensitivity charges only successful evades; a loss
        # is free and the now-contacting attack is charged once.
        battle = Battle(cfg, costs, 16, policy, True)
        a, b = battle.fighters
        a.queues[0].extend(Action("应敌", f"attempt-{i}") for i in range(3))
        dodge = Action("闪避（试验）", "dodge-new"); b.queues[0].append(dodge)
        rolls = iter((8, 9, 8, 9, 10, 9))
        battle.score = lambda fighter, action: next(rolls)
        for _ in range(3):
            battle.duel(a, 0, a.queues[0][0], b, dodge, True, True)
        assert a.stamina == 5 and b.stamina == 4 and not b.queues[0]
        # Cloud metrics count distinct stages entered, payment events and
        # contacts separately, even when guard absorbs all health damage.
        battle = Battle(cfg, costs, 16, policy, True)
        a, b = battle.fighters
        a.queues[0].append(Action("云岚三叠", "cloud-no-contact"))
        b.queues[0].append(Action("格剑", "guard-no-contact"))
        battle.score = lambda fighter, action: 8 if fighter.side == 0 else 9
        battle.damage = lambda fighter, action: 1
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], True, True)
        assert a.metrics["cloud_started_0"] == 1 and a.metrics["cloud_hit_0"] == 0
        assert a.metrics["cloud_payment_events_0"] == 0 and not a.queues[0]
        battle = Battle(cfg, costs, 16, policy, True)
        a, b = battle.fighters
        a.queues[0].append(Action("云岚三叠", "cloud-contact"))
        b.queues[0].append(Action("格剑", "guard-contact"))
        battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
        battle.damage = lambda fighter, action: 1
        battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], True, True)
        assert a.metrics["cloud_started_0"] == a.metrics["cloud_hit_0"] == a.metrics["cloud_payment_events_0"] == 1
        assert len(a.queues[0]) == 1 and a.queues[0][0].stage == 1
    # Unaffordable free break is explicitly a separate sensitivity candidate.
    for policy, expected_deflections in (("free_tail_check", 0), ("free_tail_unchecked", 1)):
        battle = Battle(cfg, costs, 17, policy, True)
        a, b = battle.fighters; a.stamina = 0
        a.queues[0].append(Action("痛击", "zero")); b.queues[0].append(Action("惊蛰", "spell"))
        battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
        battle.act(0, 0)
        assert a.metrics["free_deflections"] == expected_deflections
        assert b.mana == 2 and a.stamina == 0
    for policy in ("free_tail_check", "free_tail_unchecked"):
        one = Battle(cfg, costs, 18, policy, True).run()
        two = Battle(cfg, costs, 18, policy, True).run()
        assert one == two
    # Current candidate: both pay the first clash, then the retained action
    # rotates behind its own queue and may clash/hit at zero remaining stamina.
    battle = Battle(cfg, costs, 19, "first_clash_once", True)
    a, b = battle.fighters; a.stamina = 2; b.mana = 2
    a.queues[0].extend((Action("痛击", "once"), Action("聚炁", "behind")))
    b.queues[0].extend((Action("惊蛰", "spell-a"), Action("惊蛰", "spell-b")))
    battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    assert a.stamina == 0 and b.mana == 1 and [x.uid for x in a.queues[0]] == ["behind", "once"]
    battle.act(0, 0)  # the unpaid support cannot start with no stamina
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    assert a.stamina == 0 and b.mana == 0
    before = b.hp; battle.act(0, 0)
    assert b.hp < before and a.stamina == 0
    assert sum(e["event"] == "pay" and e["action"] == "痛击" for e in battle.events) == 1
    assert sum(e["event"] == "pay" and e["action"] == "惊蛰" for e in battle.events) == 2
    # Losing the first clash still pays, as does a repeatedly dodging action
    # exactly once; subsequent dodges remain possible at zero stamina.
    battle = Battle(cfg, costs, 20, "first_clash_once", True)
    a, b = battle.fighters; b.stamina = 1
    a.queues[0].extend(Action("应敌", f"once-attack-{i}") for i in range(3))
    dodge = Action("闪避（试验）", "once-dodge"); b.queues[0].append(dodge)
    rolls = iter((8, 9, 8, 9, 10, 9))
    battle.score = lambda fighter, action: next(rolls)
    for _ in range(3): battle.duel(a, 0, a.queues[0][0], b, dodge, True, True)
    assert a.stamina == 3 and b.stamina == 0 and not b.queues[0]
    assert sum(e["event"] == "pay" and e["action"] == "闪避（试验）" for e in battle.events) == 1
    battle = Battle(cfg, costs, 21, "first_clash_shared", True)
    a, b = battle.fighters; a.stamina = 2; b.hp = 10000
    a.queues[0].append(Action("云岚三叠", "shared-cloud"))
    for stage in range(4):
        assert a.queues[0][0].stage == stage
        battle.act(0, 0)
    assert not a.queues[0] and a.stamina == 0
    assert sum(e["event"] == "pay" and e["action"] == "云岚三叠" for e in battle.events) == 1
    inactive_cfg = copy.deepcopy(cfg)
    inactive_cfg.update(max_rounds=3, initial_stamina=0, stamina_recovery=0,
                        initial_mana=0, max_mana=0, deck={"痛击": 4}, profiles=[{}, {}])
    inactive = Battle(inactive_cfg, costs, 23, "first_clash_shared").run()
    assert all(f["metrics"]["rounds_started_alive"] == 3 and f["metrics"]["no_action_rounds"] == 3 for f in inactive["fighters"])
    assert all(a.metrics[f"cloud_hit_{stage}"] == 1 for stage in range(4))
    battle = Battle(cfg, costs, 22, "first_clash_shared", True)
    a, b = battle.fighters; a.stamina = 2; b.hp = 10000
    a.queues[0].append(Action("云岚三叠", "shared-clashes"))
    b.queues[0].extend((Action("应敌", "foe-1"), Action("应敌", "foe-2")))
    battle.score = lambda fighter, action: 10 if fighter.side == 0 else 9
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    battle.duel(a, 0, a.queues[0][0], b, b.queues[0][0], False, True)
    assert a.stamina == 0 and b.stamina == 4 and a.queues[0][0].stage == 2
    assert sum(e["event"] == "pay" and e["action"] == "云岚三叠" for e in battle.events) == 1
    print(json.dumps({"self_test": "passed", "checks": ["historical fee checks", "free-tail historical checks", "contact versus zero health damage", "cloud stage entry/payment/contact distinction", "zero-resource sensitivity variants", "first clash both pay even on defeat", "retained action moves to tail and repeats free", "paid dodge repeats at zero resources", "cloud four stages share one fee", "no-action round accounting", "hand conservation", "deterministic replay"]}, ensure_ascii=False))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, help="JSON overrides, or - to read stdin; output is always stdout")
    parser.add_argument("--samples", type=int)
    parser.add_argument("--seed-start", type=int)
    parser.add_argument("--trace", type=int, help="also print detailed event log for this seed")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    cfg = copy.deepcopy(DEFAULTS)
    if args.config:
        raw = sys.stdin.read() if str(args.config) == "-" else args.config.read_text(encoding="utf-8")
        overrides = json.loads(raw)
        unknown = set(overrides) - set(cfg)
        if unknown: raise ValueError(f"unknown config fields: {sorted(unknown)}")
        cfg.update(overrides)
    if args.samples is not None: cfg["samples"] = args.samples
    if args.seed_start is not None: cfg["seed_start"] = args.seed_start
    validate(cfg)
    source_costs, hashes = load_resource_costs()
    costs = dict(source_costs)
    for name, cost in cfg["cost_overrides"].items():
        costs[name] = tuple(cost)
    if args.self_test:
        self_test(costs, cfg); return
    outputs = {}
    all_results = {}
    for policy in ("original", "repeat_one", "free_tail_check", "free_tail_unchecked", "first_clash_once", "first_clash_shared"):
        results = []
        for seed in range(cfg["seed_start"], cfg["seed_start"] + cfg["samples"]):
            results.append(Battle(cfg, costs, seed, policy).run())
            if cfg["mirror_speeds"]:
                mirrored = copy.deepcopy(cfg)
                mirrored["speeds"] = list(reversed(mirrored["speeds"]))
                results.append(Battle(mirrored, costs, seed, policy).run())
        outputs[policy] = summarize(results)
        all_results[policy] = results
    pairs = list(zip(all_results["original"], all_results["repeat_one"]))
    paired = {"pairs": len(pairs),
              "changed_winner": sum(a["winner"] != b["winner"] or a["timeout"] != b["timeout"] for a, b in pairs),
              "mean_round_delta_repeat_minus_original": sum(b["rounds"] - a["rounds"] for a, b in pairs) / len(pairs),
              "mean_metric_delta_repeat_minus_original": [
                  {key: sum(b["fighters"][side]["metrics"].get(key, 0) - a["fighters"][side]["metrics"].get(key, 0)
                            for a, b in pairs) / len(pairs)
                   for key in ("stamina_spent", "repeat_stamina_spent", "repeat_clashes", "repeat_unaffordable", "unaffordable", "damage")}
                  for side in range(2)]}
    new_policy_comparisons = {}
    for candidate in ("free_tail_check", "free_tail_unchecked"):
        candidate_pairs = list(zip(all_results["original"], all_results[candidate]))
        new_policy_comparisons[candidate] = {
            "pairs": len(candidate_pairs),
            "changed_winner": sum(a["winner"] != b["winner"] or a["timeout"] != b["timeout"] for a, b in candidate_pairs),
            "mean_round_delta_against_historical_original": sum(b["rounds"] - a["rounds"] for a, b in candidate_pairs) / len(candidate_pairs),
        }
    result = {"scope": "isolated candidate simulation; does not automatically read runtime snapshots; no official data changed",
              "limits": ["illustrative default attributes; custom profiles must identify their own provenance", "copied damage formulas", "blind scripted policy",
                         "no armor, critical, artifacts, heart methods, revival, group targets or full skill migration",
                         "same-lane target links; no arbitrary multi-character targeting", "placed but unused defenses go to discard",
                         "unused unplaced cards stay in hand; no hand-size cap is assumed",
                         "unplaced_cards counts occurrences per round, not distinct cards",
                         "mirrored games share seeds and are not independent", "dice keyed by action; divergent actions cannot be perfectly paired",
                         "first_clash_shared is the current candidate; all other variants are superseded historical comparisons",
                         "free_tail_check obeys resource insufficiency invalidation; free_tail_unchecked is a counterfactual stress test",
                         "guard and dodge pay on first clash in first_clash_shared; dodge re-clashes do not repay; guard mitigation still uses candidate values",
                         "first_clash_shared shares one payment across cloud stages; first_clash_once is the superseded per-stage-fee comparison",
                         "successful contact pays even if zero health damage; losing against guard counts as contact only when residual damage is positive (candidate)",
                         "free-tail moves to the same linked queue tail; no arbitrary queue retargeting modeled"],
              "config": cfg, "source_costs": source_costs, "effective_costs": costs,
              "metric_definitions": {
                  "cloud_started_N": "distinct cloud stage instances entering a clash or direct hit, counted once per instance",
                  "cloud_payment_events_N": "payments for that stage, counting every payment event rather than unique actions",
                  "cloud_hit_N": "stage instances making contact, including fully absorbed or zero-health-damage contacts",
                  "cloud_generated_N": "immediately generated successor stages; only one is generated per successful preceding stage",
                  "no_action_rounds": "rounds started alive with no clash participation, support activation or attack resolution; includes dying before acting",
                  "unaffordable": "actions skipped for insufficient resources, not unique cards or turns",
                  "stamina_unaffordable": "skipped actions lacking stamina; may overlap mana_unaffordable",
                  "mana_unaffordable": "skipped actions lacking mana; may overlap stamina_unaffordable",
              },
              "resource_sha256": hashes, "comparison": outputs, "paired_difference_historical_only": paired,
              "historical_free_tail_comparisons": new_policy_comparisons}
    if args.trace is not None:
        result["trace"] = {policy: Battle(cfg, costs, args.trace, policy, True).run()
                           for policy in ("original", "repeat_one", "free_tail_check", "free_tail_unchecked", "first_clash_once", "first_clash_shared")}
    # ASCII-escaped JSON is portable through Windows PowerShell pipelines.
    print(json.dumps(result, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
