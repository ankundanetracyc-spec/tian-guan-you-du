"""Artifact rewrite contract reference; pure Python, no Godot or player-save writes."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from dataclasses import dataclass, replace
from decimal import Decimal, ROUND_FLOOR
from pathlib import Path


BASE = "res://resources/activeskills/剑修基础/格剑.tres"
TEMPLATE = "artifact.gejian.defuse_flat.v1"
DEFAULT_CONTRACT = Path(__file__).resolve().parents[2] / "docs/plans/物品与装备/器物化法接口契约候选_20261108.json"


class ContractError(ValueError):
    pass


def number(value) -> Decimal:
    if isinstance(value, bool):
        raise ContractError("布尔值不能作为效力或费用")
    try:
        result = Decimal(str(value))
    except Exception as exc:
        raise ContractError("数值无法读取") from exc
    if not result.is_finite() or result < 0:
        raise ContractError("数值须有限且非负")
    return result


def digest(value) -> str:
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def exact_keys(value: dict, allowed: set[str], required: set[str]) -> None:
    if not isinstance(value, dict) or not required <= value.keys() or value.keys() - allowed:
        raise ContractError("缺少必填项或含未开放字段")


def mechanisms(skill: dict) -> set[str]:
    result = set()
    response = "应对" in skill.get("keywords", [])
    if response and skill.get("counter_condition") in ("", "on_attacked", "on_physical_attacked"):
        result.add("physical_response")
    if skill.get("target_type") == 0 and "化解" in skill.get("status_effect_ids", []):
        result.add("defuse")
    return result


@dataclass(frozen=True)
class FrozenBundle:
    source_instance_id: str
    bundle_id: str
    template_id: str
    template_version: int
    operations: tuple[tuple[str, int], ...]
    allocation_X: Decimal
    budget_debits: tuple[tuple[str, Decimal], ...]
    template_digest: str
    generation_rules_version: str

    def cache_digest(self) -> str:
        return digest({"source": self.source_instance_id, "bundle": self.bundle_id,
                       "template": [self.template_id, self.template_version, self.template_digest],
                       "operations": self.operations, "X": str(self.allocation_X),
                       "debits": [(key, str(value)) for key, value in self.budget_debits],
                       "generation_rules": self.generation_rules_version})


@dataclass(frozen=True)
class Decision:
    allowed: bool
    reason: str


class BudgetLedger:
    """A caller supplies the actual shared-solver allowance; generation debits it once."""
    def __init__(self, available: dict):
        self.remaining = {key: number(value) for key, value in available.items()}
        self.used_tokens: set[tuple[str, str]] = set()

    def consume(self, token: tuple[str, str], debits: dict[str, Decimal]) -> None:
        if token in self.used_tokens:
            raise ContractError("同一物件化法包已冻结，不能重复领取预算")
        if any(key not in self.remaining or value > self.remaining[key] for key, value in debits.items()):
            raise ContractError("化法包超过真实来源或共同预算")
        for key, value in debits.items():
            self.remaining[key] -= value
        self.used_tokens.add(token)


class Registry:
    def __init__(self, document: dict):
        if document.get("game_runtime_integrated") is not False or document.get("game_save_modified") is not False:
            raise ContractError("候选契约不能标记为已接通游戏或改写正式存档")
        self.document = copy.deepcopy(document)
        self.operators = {op["id"]: copy.deepcopy(op) for op in document["registered_operators"]}
        self.skills: dict[str, dict] = {}
        self.templates: dict[tuple[str, int], dict] = {}
        for skill in document["global_skill_interfaces"]:
            self.register_skill_interface(skill)
        for template in document["registered_templates"]:
            self.register_template(template)

    def register_skill_interface(self, skill: dict) -> None:
        if not skill.get("resource_key") or not skill.get("base_skill_key"):
            raise ContractError("技能接口缺少准确资源与基础身份")
        if skill["resource_key"] != skill["base_skill_key"] and skill["base_skill_key"] not in self.skills:
            raise ContractError("兼容变体必须先有已登记的准确基础身份")
        # This is a trusted adapter of the global enabled library, never a player-authored name match.
        self.skills[skill["resource_key"]] = copy.deepcopy(skill)

    def matches(self, template: dict, skill: dict) -> bool:
        target = template["matcher"]
        terms = set(skill.get("tags", [])) | ({"应对"} if "应对" in skill.get("keywords", []) else set())
        caps = mechanisms(skill)
        if not skill.get("enabled") or skill["base_skill_key"] not in target["base_skill_keys"]:
            return False
        if not set(target["required_terms"]) <= terms or not set(target["required_mechanisms"]) <= caps:
            return False
        for operation in template["operations"]:
            operator = self.operators.get(operation["id"])
            if operator is None or operation["slot_id"] not in skill.get("modifier_slots", []):
                return False
            if not (set(operator["required_mechanisms"]) | set(operation["required_mechanisms"])) <= caps:
                return False
        return True

    def register_template(self, template: dict) -> None:
        key = (template.get("template_id"), template.get("template_version"))
        if not key[0] or not isinstance(key[1], int) or key[1] < 1 or key in self.templates:
            raise ContractError("模板身份为空、版本非法或重复")
        if template.get("source_policy") != "held_active_only" or template.get("target_policy") != "one_placed_copy":
            raise ContractError("本轮只开放手持来源和一份目标")
        exact_keys(template["matcher"], {"base_skill_keys", "required_terms", "required_mechanisms"},
                   {"base_skill_keys", "required_terms", "required_mechanisms"})
        if not template["matcher"]["base_skill_keys"] or not template.get("operations"):
            raise ContractError("模板必须声明真实基础目标和完整操作包")
        for operation in template["operations"]:
            exact_keys(operation, {"id", "slot_id", "phase", "value_type", "coefficient", "required_mechanisms"},
                       {"id", "slot_id", "phase", "value_type", "coefficient", "required_mechanisms"})
            operator = self.operators.get(operation["id"])
            if operator is None:
                raise ContractError("操作尚未注册，不能任意改ActiveSkillData字段")
            for field in ("slot_id", "phase", "value_type"):
                if operation[field] != operator[field]:
                    raise ContractError("操作与已审核技能接口不兼容")
            if number(operation["coefficient"]) != number(operator["generation_coefficient"]):
                raise ContractError("操作换算系数不属于已登记版本")
        if not any(self.matches(template, skill) for skill in self.skills.values()):
            raise ContractError("全局启用技能库没有整包适配目标")
        self.templates[key] = copy.deepcopy(template)

    def template(self, template_id: str, version: int) -> dict:
        try:
            return self.templates[(template_id, version)]
        except KeyError as exc:
            raise ContractError("化法模板或版本尚未注册") from exc

    def freeze_existing(self, bundle: dict) -> FrozenBundle:
        template = self.template(bundle["template_id"], bundle["template_version"])
        if not bundle.get("source_instance_id") or not bundle.get("bundle_id"):
            raise ContractError("缺少真实物件或包身份，尚不可绑定")
        if bundle.get("source_policy") != template["source_policy"] or bundle.get("target_policy") != template["target_policy"]:
            raise ContractError("冻结包来源或目标政策被改写")
        if bundle.get("matcher") != template["matcher"]:
            raise ContractError("冻结包不能扩大已登记适配范围")
        generation = bundle["generation"]
        x = number(generation["allocation_X"])
        expected_debits: dict[str, Decimal] = {}
        frozen_ops = []
        if len(bundle["operations"]) != len(template["operations"]):
            raise ContractError("整包操作数量不一致，不能只取可用子项")
        for spec, operation in zip(template["operations"], bundle["operations"]):
            exact_keys(operation, {"id", "parameters"}, {"id", "parameters"})
            exact_keys(operation["parameters"], {"amount"}, {"amount"})
            operator = self.operators[spec["id"]]
            if operation["id"] != spec["id"] or x < number(operator["minimum_allocation_X"]):
                raise ContractError("操作不一致或效力不足，整包不生成")
            amount = int((x * number(spec["coefficient"])).to_integral_value(rounding=ROUND_FLOOR))
            if amount < 1 or type(operation["parameters"]["amount"]) is not int or operation["parameters"]["amount"] != amount:
                raise ContractError("追加值必须由本次整包效力量化，不能硬填")
            frozen_ops.append((operation["id"], amount))
            for resource, cost in operator["budget_costs_per_X"].items():
                expected_debits[resource] = expected_debits.get(resource, Decimal(0)) + x * number(cost)
        supplied = {key: number(value) for key, value in generation["budget_debits"].items()}
        if supplied.keys() != expected_debits.keys() or any(abs(supplied[key] - value) > Decimal("0.00000001") for key, value in expected_debits.items()):
            raise ContractError("整包预算扣除不一致或来源被重复复制")
        if generation.get("source_dimension") != "固" or number(generation.get("coefficient")) != Decimal("0.5"):
            raise ContractError("生成来源或换算参数与本轮模板不一致")
        source_allocations = generation.get("source_allocations", {})
        if set(source_allocations) != {"固"} or abs(number(source_allocations["固"]) - expected_debits["source/固"]) > Decimal("0.00000001"):
            raise ContractError("化法包的真实来源分配不一致")
        if not isinstance(generation.get("rules_version"), str) or not generation["rules_version"]:
            raise ContractError("缺少当次成器规则版本")
        return FrozenBundle(bundle["source_instance_id"], bundle["bundle_id"], bundle["template_id"],
                            bundle["template_version"], tuple(frozen_ops), x,
                            tuple(sorted(expected_debits.items())), digest(template), generation["rules_version"])

    def validate_frozen(self, bundle: FrozenBundle) -> None:
        number(bundle.allocation_X)
        template = self.template(bundle.template_id, bundle.template_version)
        expected = tuple((op["id"], int((bundle.allocation_X * number(op["coefficient"])).to_integral_value(rounding=ROUND_FLOOR)))
                         for op in template["operations"])
        if bundle.template_digest != digest(template) or bundle.operations != expected or any(amount < 1 for _, amount in expected):
            raise ContractError("冻结包的操作或模板已被篡改")


def generate_bundle(registry: Registry, *, source_instance_id: str, bundle_id: str,
                    allocation_X, ledger: BudgetLedger, rules_version: str) -> FrozenBundle:
    template = registry.template(TEMPLATE, 1)
    x = number(allocation_X)
    costs: dict[str, Decimal] = {}
    operations = []
    for spec in template["operations"]:
        operator = registry.operators[spec["id"]]
        operations.append({"id": spec["id"], "parameters": {"amount": int((x * number(spec["coefficient"])).to_integral_value(rounding=ROUND_FLOOR))}})
        for key, cost in operator["budget_costs_per_X"].items():
            costs[key] = costs.get(key, Decimal(0)) + x * number(cost)
    raw = {"source_instance_id": source_instance_id, "bundle_id": bundle_id, "template_id": TEMPLATE,
           "template_version": 1, "source_policy": "held_active_only", "target_policy": "one_placed_copy",
           "matcher": copy.deepcopy(template["matcher"]), "operations": operations,
           "generation": {"allocation_X": str(x), "source_dimension": "固", "source_allocations": {"固": str(costs["source/固"])}, "coefficient": "0.5",
                          "budget_debits": {key: str(value) for key, value in costs.items()}, "rules_version": rules_version}}
    frozen = registry.freeze_existing(raw)
    ledger.consume((source_instance_id, bundle_id), costs)
    return frozen


def can_configure(registry: Registry, actor: dict, source: dict, bundle: FrozenBundle, layout: dict, placement_id: str) -> Decision:
    registry.validate_frozen(bundle)
    template = registry.template(bundle.template_id, bundle.template_version)
    if source.get("instance_id") != bundle.source_instance_id or source["instance_id"] not in actor.get("owned_instance_ids", []):
        return Decision(False, "不是本人持有的同一真实物件")
    if source.get("selected_mode") != "held" or not source.get("usable"):
        return Decision(False, "须选择合法手持用法且器物可用")
    if bundle.bundle_id not in source.get("bundle_ids", []):
        return Decision(False, "本件当前快照不提供这个化法包")
    target = next((item for item in layout["placements"] if item["placement_id"] == placement_id), None)
    if target is None or target.get("kind") != "skill":
        return Decision(False, "所选招式份数不在当前盘面")
    skill = registry.skills.get(target.get("skill_resource_key"))
    if skill is None or skill["base_skill_key"] not in actor.get("learned_base_skill_keys", []):
        return Decision(False, "目标不是本人已学的准确基础招式")
    if target.get("base_skill_key") != skill["base_skill_key"] or not registry.matches(template, skill):
        return Decision(False, "目标不满足整包关键词与真实机制")
    return Decision(True, "可配置；是否已启用在真实执行时另查")


def bind(registry: Registry, actor: dict, source: dict, bundle: FrozenBundle, layout: dict, placement_id: str) -> dict:
    decision = can_configure(registry, actor, source, bundle, layout, placement_id)
    if not decision.allowed:
        raise ContractError(decision.reason)
    target = next(item for item in layout["placements"] if item["placement_id"] == placement_id)
    return {"actor_id": actor["actor_id"], "source_instance_id": bundle.source_instance_id,
            "bundle_id": bundle.bundle_id, "template_id": bundle.template_id, "template_version": bundle.template_version,
            "target_layout_id": layout["layout_id"], "target_placement_id": placement_id,
            "target_base_skill_key": target["base_skill_key"]}


def is_active_now(registry: Registry, actor: dict, source: dict, bundle: FrozenBundle, binding: dict, layout: dict) -> Decision:
    identity = (binding.get("actor_id") == actor["actor_id"] and binding.get("source_instance_id") == bundle.source_instance_id
                and binding.get("bundle_id") == bundle.bundle_id and binding.get("template_id") == bundle.template_id
                and binding.get("template_version") == bundle.template_version and binding.get("target_layout_id") == layout["layout_id"])
    if not identity:
        raise ContractError("来源、版本、人物或目标布局身份串接")
    decision = can_configure(registry, actor, source, bundle, layout, binding["target_placement_id"])
    target = next((item for item in layout["placements"] if item["placement_id"] == binding["target_placement_id"]), None)
    if target is not None and target.get("base_skill_key") != binding["target_base_skill_key"]:
        decision = Decision(False, "原目标份数已改成其他招式，保留选择但暂停")
    if not decision.allowed:
        return decision
    if not source.get("active") or actor.get("held_instance_id") != bundle.source_instance_id:
        return Decision(False, "同一器物尚未实际手持启用，暂不生效")
    return Decision(True, "当前同一手持来源与目标均有效")


def resolve_modifier_plan(registry: Registry, actor: dict, source: dict, bundle: FrozenBundle, binding: dict, layout: dict) -> dict:
    decision = is_active_now(registry, actor, source, bundle, binding, layout)
    target = next((item for item in layout["placements"] if item["placement_id"] == binding["target_placement_id"]), None)
    skill = registry.skills.get(target.get("skill_resource_key")) if target else None
    inherited = copy.deepcopy(skill["combat_metadata"]) if skill else {}
    inherited["base_skill_key"] = binding["target_base_skill_key"]
    active = decision.allowed
    if type(source.get("state_version")) is not int or source["state_version"] < 0:
        raise ContractError("缺少有效来源状态版本，不能缓存修正")
    cache_key = (actor["actor_id"], layout["layout_id"], layout["version"], binding["target_placement_id"],
                 bundle.source_instance_id, source["state_version"], bundle.cache_digest(), bundle.template_version,
                 source.get("selected_mode"), bool(source.get("active")), actor.get("held_instance_id"))
    return {"runtime_status": "python_reference_plan_not_godot_execution", "cache_key": cache_key,
            "active": active, "reason": decision.reason,
            "target_placement_id": binding["target_placement_id"], "inherited_skill": inherited,
            "operations": list(bundle.operations) if active else [], "new_action": False,
            "copy_policy": "not_decided", "binding_preserved": True}


def settle_defuse_event(registry: Registry, actor: dict, source: dict, bundle: FrozenBundle, binding: dict,
                        layout: dict, event: dict, *, base_defuse: int, completed: dict) -> dict:
    if type(base_defuse) is not int or base_defuse < 0:
        raise ContractError("原技能化解结果须为非负整数，本接口不替原技能造值")
    if event.get("cast_origin") in ("copy", "replay") or event.get("copied_from_placement") is not None or event.get("borrowed_opportunity_placement") is not None:
        raise ContractError("器物化法复制政策未定，原型不擅自将它开放给复命")
    token = (event["battle_id"], event["cast_id"], event["event_id"], actor["actor_id"], bundle.source_instance_id,
             bundle.bundle_id, binding["target_placement_id"])
    if token in completed:
        return copy.deepcopy(completed[token])
    if event.get("round_index", 0) < 1 or event.get("actor_id") != actor["actor_id"] or event.get("layout_id") != layout["layout_id"]:
        raise ContractError("缺少真实人物／共同回合事件上下文")
    plan = resolve_modifier_plan(registry, actor, source, bundle, binding, layout)
    valid_event = (event.get("phase") == "response_defuse_calculation" and event.get("is_real_response") is True
                   and event.get("incoming_damage_type") == "physical"
                   and event.get("target_placement_id") == binding["target_placement_id"])
    extra = sum(amount for op, amount in plan["operations"] if op == "add_response_defuse_flat") if plan["active"] and valid_event else 0
    result = {"base_defuse": base_defuse, "added_defuse": extra, "total_defuse": base_defuse + extra,
              "original_skill_ledger_key": binding["target_base_skill_key"], "new_action": False,
              "runtime_status": "pure_python_contract_event_not_game_battle"}
    completed[token] = copy.deepcopy(result)
    return result


def self_check(registry: Registry) -> dict:
    checks = []
    def check(condition, title):
        if not condition:
            raise AssertionError(title)
        checks.append(title)
    def rejects(fn, title):
        try:
            fn()
        except ContractError:
            checks.append(title)
        else:
            raise AssertionError(title)
    actor = {"actor_id": "fixture:owner", "owned_instance_ids": ["fixture:item"], "learned_base_skill_keys": [BASE], "held_instance_id": "fixture:item"}
    source = {"instance_id": "fixture:item", "bundle_ids": ["fixture:bundle"], "selected_mode": "held", "usable": True, "active": False, "state_version": 1}
    layout = {"layout_id": "fixture:layout", "version": 1, "placements": [
        {"placement_id": key, "kind": "skill", "base_skill_key": BASE, "skill_resource_key": BASE} for key in ("A", "B")]}
    ledger = BudgetLedger({"source/固": 10, "root": 10, "structure": 10, "通": 10})
    bundle = generate_bundle(registry, source_instance_id="fixture:item", bundle_id="fixture:bundle",
                             allocation_X="3.8", ledger=ledger, rules_version="fixture:rules")
    check(bundle.operations == (("add_response_defuse_flat", 1),), "value_is_floor_half_X_not_hardcoded")
    check(can_configure(registry, actor, source, bundle, layout, "A").allowed, "inactive_held_item_can_prebind")
    binding = bind(registry, actor, source, bundle, layout, "A")
    check(not resolve_modifier_plan(registry, actor, source, bundle, binding, layout)["active"], "prebound_source_is_inactive_before_activation")
    source = dict(source, active=True, state_version=2)
    plan = resolve_modifier_plan(registry, actor, source, bundle, binding, layout)
    check(plan["active"] and plan["target_placement_id"] == "A", "only_target_A_resolves")
    check(plan["inherited_skill"] == dict(registry.skills[BASE]["combat_metadata"], base_skill_key=BASE), "original_cost_cooldown_and_counts_are_preserved")
    event = {"battle_id": "fixture:battle", "cast_id": "fixture:cast", "event_id": "response:1", "actor_id": actor["actor_id"], "layout_id": layout["layout_id"], "round_index": 1,
             "phase": "response_defuse_calculation", "is_real_response": True, "incoming_damage_type": "physical", "target_placement_id": "A"}
    completed = {}
    first = settle_defuse_event(registry, actor, source, bundle, binding, layout, event, base_defuse=7, completed=completed)
    check(first["total_defuse"] == 8, "real_physical_response_adds_one")
    repeated = settle_defuse_event(registry, actor, source, bundle, binding, layout, event, base_defuse=7, completed=completed)
    check(repeated == first and len(completed) == 1, "same_response_event_does_not_double_apply")
    for label, changes in (("magic_response_not_eligible", {"incoming_damage_type": "magic"}),
                           ("not_a_real_response", {"is_real_response": False}),
                           ("preview_or_scan_does_not_grant_defuse", {"phase": "skill_preview"})):
        ignored = settle_defuse_event(registry, actor, source, bundle, binding, layout, dict(event, event_id=label, **changes), base_defuse=7, completed=completed)
        check(ignored["total_defuse"] == 7, label)
    other = settle_defuse_event(registry, actor, source, bundle, binding, layout, dict(event, event_id="response:B", target_placement_id="B"), base_defuse=7, completed=completed)
    check(other["total_defuse"] == 7, "same_base_skill_B_does_not_receive_A_bundle")
    disabled = dict(source, active=False, state_version=3)
    later = settle_defuse_event(registry, actor, disabled, bundle, binding, layout, dict(event, event_id="response:2"), base_defuse=7, completed=completed)
    check(later["total_defuse"] == 7 and first["total_defuse"] == 8, "source_invalidation_stops_future_effect_without_rollback")
    check(resolve_modifier_plan(registry, actor, source, bundle, binding, layout)["cache_key"] != resolve_modifier_plan(registry, actor, disabled, bundle, binding, layout)["cache_key"], "source_version_changes_cache_identity")
    check(not resolve_modifier_plan(registry, actor, dict(source, selected_mode="controlled"), bundle, binding, layout)["active"], "controlled_item_does_not_activate_handheld_transformation")
    check(not resolve_modifier_plan(registry, dict(actor, held_instance_id="fixture:another_item"), source, bundle, binding, layout)["active"], "another_held_instance_cannot_supply_this_bundle")
    check(not can_configure(registry, actor, dict(source, bundle_ids=[]), bundle, layout, "A").allowed, "same_item_without_that_frozen_bundle_cannot_bind")
    rejects(lambda: can_configure(registry, actor, source, replace(bundle, operations=(("add_response_defuse_flat", 99),)), layout, "A"), "tampered_frozen_amount_rejected")
    moved = copy.deepcopy(layout); moved["version"] = 2; moved["placements"][0].update(cell=[2, 1], quarter_turns=1)
    check(resolve_modifier_plan(registry, actor, source, bundle, binding, moved)["active"], "move_rotation_keep_same_placement_binding")
    removed = dict(layout, version=3, placements=[layout["placements"][1]])
    check(not resolve_modifier_plan(registry, actor, source, bundle, binding, removed)["active"] and binding["target_placement_id"] == "A", "removal_pauses_without_same_name_rebinding")
    rejects(lambda: bind(registry, dict(actor, learned_base_skill_keys=[]), source, bundle, layout, "A"), "unlearned_target_is_not_granted_by_item")
    for label, mutate in (("wrong_base_identity", lambda s: s.update(base_skill_key="fixture:not_gejian")),
                          ("missing_required_term", lambda s: s.update(tags=["化解"])),
                          ("missing_real_defuse_mechanism", lambda s: s.update(status_effect_ids=[]))):
        skill = copy.deepcopy(registry.skills[BASE]); skill["resource_key"] = "fixture:" + label; mutate(skill)
        # A separate registered base fixture demonstrates that name and prose cannot qualify it.
        registry.skills[skill["resource_key"]] = skill
        bad = copy.deepcopy(layout); bad["placements"][0]["skill_resource_key"] = skill["resource_key"]; bad["placements"][0]["base_skill_key"] = skill["base_skill_key"]
        candidate_actor = dict(actor, learned_base_skill_keys=[BASE, skill["base_skill_key"]])
        rejects(lambda bad=bad, candidate_actor=candidate_actor: bind(registry, candidate_actor, source, bundle, bad, "A"), label)
    variant = copy.deepcopy(registry.skills[BASE]); variant["resource_key"] = "fixture:registered_gejian_variant"
    variant["combat_metadata"] = {"energy_cost": 2, "mana_cost": 1, "cooldown_rounds": 3, "uses_per_round": 2, "uses_per_battle": 4, "max_charges": 5}
    registry.register_skill_interface(variant)
    compatible = copy.deepcopy(layout); compatible["placements"][0]["skill_resource_key"] = variant["resource_key"]
    check(can_configure(registry, actor, source, bundle, compatible, "A").allowed, "registered_compatible_variant_keeps_exact_base_and_real_mechanisms")
    check(resolve_modifier_plan(registry, actor, source, bundle, binding, compatible)["inherited_skill"] == dict(variant["combat_metadata"], base_skill_key=BASE), "nonzero_variant_cost_cooldown_and_limits_are_not_reset")
    template = copy.deepcopy(registry.template(TEMPLATE, 1)); template["template_id"] += ".invalid_bundle"
    template["operations"].append(dict(template["operations"][0], required_mechanisms=["shield"]))
    rejects(lambda: registry.register_template(template), "one_incompatible_operation_rejects_entire_bundle")
    template = copy.deepcopy(registry.template(TEMPLATE, 1)); template["template_id"] += ".arbitrary_field"
    template["operations"][0]["id"] = "set_any_active_skill_field"
    rejects(lambda: registry.register_template(template), "unknown_arbitrary_field_operation_rejected")
    template = copy.deepcopy(registry.template(TEMPLATE, 1)); template["template_id"] += ".extra_field"
    template["operations"][0]["soul"] = 999
    rejects(lambda: registry.register_template(template), "undeclared_operation_field_rejected")
    rejects(lambda: generate_bundle(registry, source_instance_id="fixture:item", bundle_id="fixture:bundle", allocation_X="3.8", ledger=ledger, rules_version="fixture:rules"), "frozen_bundle_cannot_claim_same_budget_twice")
    poor = BudgetLedger({"source/固": 1, "root": 10, "structure": 10, "通": 10})
    before = dict(poor.remaining)
    rejects(lambda: generate_bundle(registry, source_instance_id="fixture:item", bundle_id="fixture:poor", allocation_X=3, ledger=poor, rules_version="fixture:rules"), "insufficient_source_rejects_generation_atomically")
    check(poor.remaining == before, "failed_generation_does_not_consume_partial_budget")
    rejects(lambda: settle_defuse_event(registry, actor, source, bundle, binding, layout, dict(event, copied_from_placement="A", borrowed_opportunity_placement="B"), base_defuse=7, completed=completed), "copy_policy_remains_undecided")
    bad_document = copy.deepcopy(registry.document); bad_document["game_runtime_integrated"] = True
    rejects(lambda: Registry(bad_document), "prototype_metadata_cannot_claim_godot_integration")
    unavailable_library = copy.deepcopy(registry.document)
    for skill in unavailable_library["global_skill_interfaces"]:
        skill["enabled"] = False
    rejects(lambda: Registry(unavailable_library), "template_requires_a_global_enabled_whole_package_target")
    check(registry.template(TEMPLATE, 1)["extra_action"] is False, "no_new_action_created")
    return {"status": "passed", "checks": len(checks), "cases": checks, "runtime_status": "pure_python_contract_reference_only", "godot_runtime_integrated": False, "player_save_modified": False}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--contract", type=Path, default=DEFAULT_CONTRACT)
    parser.add_argument("--self-check", action="store_true")
    parser.add_argument("--validate-result", type=Path)
    args = parser.parse_args()
    registry = Registry(json.loads(args.contract.read_text(encoding="utf-8-sig")))
    output = self_check(registry) if args.self_check else {"status": "registry_valid", "templates": len(registry.templates), "godot_runtime_integrated": False}
    if args.validate_result:
        result = json.loads(args.validate_result.read_text(encoding="utf-8-sig"))
        raw = result.get("transformation_bundle")
        if raw is None:
            if any(effect.get("carrier") == "binding" and effect.get("value", 0) > 0 for effect in result.get("normal_effects", [])):
                raise ContractError("新物仍有未封装的裸化法效果，不能跳过模板注册")
            output["generated_bundle"] = "not_generated_no_positive_registered_package"
        else:
            bundle = registry.freeze_existing(raw)
            actor = {"actor_id": "contract-preview", "owned_instance_ids": [bundle.source_instance_id],
                     "learned_base_skill_keys": [BASE], "held_instance_id": bundle.source_instance_id}
            source = {"instance_id": bundle.source_instance_id, "bundle_ids": [bundle.bundle_id],
                      "selected_mode": "held", "usable": True, "active": True, "state_version": 1}
            layout = {"layout_id": "contract-preview", "version": 1, "placements": [
                {"placement_id": key, "kind": "skill", "base_skill_key": BASE, "skill_resource_key": BASE} for key in ("A", "B")]}
            binding = bind(registry, actor, source, bundle, layout, "A")
            additions = {}
            for key in ("A", "B"):
                event = {"battle_id": "contract-preview", "cast_id": key, "event_id": "response:" + key,
                         "actor_id": actor["actor_id"], "layout_id": layout["layout_id"], "round_index": 1,
                         "phase": "response_defuse_calculation", "is_real_response": True,
                         "incoming_damage_type": "physical", "target_placement_id": key}
                additions[key] = settle_defuse_event(registry, actor, source, bundle, binding, layout, event, base_defuse=0, completed={})["added_defuse"]
            assert additions["A"] == sum(amount for _, amount in bundle.operations) and additions["B"] == 0
            output["generated_bundle"] = {"status": "valid_frozen_candidate", "template_id": bundle.template_id,
                                          "operations": bundle.operations, "allocation_X": str(bundle.allocation_X),
                                          "source_identity_checked": True, "synthetic_event_additions": additions,
                                          "event_examples_are_synthetic": True}
    print(json.dumps(output, ensure_ascii=False))


if __name__ == "__main__":
    main()
