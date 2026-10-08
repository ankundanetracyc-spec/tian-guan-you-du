window.PROJECT_CATALOG = {
  "generatedAt": "2026-09-18T14:09:42.567Z",
  "project": "青溪问道",
  "cards": [
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_3ff62822",
      "projectSource": true,
      "skill_name": "抱一",
      "resource_path": "res://resources/activeskills/黄庭养形篇/抱一.tres",
      "icon_path": "",
      "description": "每回合限一次；仅当本回合已主动使气血下降时可打出；获得气血1层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "黄庭养形篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_qi_blood",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_494adb03",
      "projectSource": true,
      "skill_name": "抱元观微",
      "resource_path": "res://resources/activeskills/明微抱元经/抱元观微.tres",
      "icon_path": "",
      "description": "凝神抱元，以镜意观照敌我。进入抱元观微状态，持续整场战斗。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "明微抱元经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "抱元观微",
          "stacks": 1,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_3d2dafbd",
      "projectSource": true,
      "skill_name": "爆炎符",
      "resource_path": "res://resources/activeskills/符箓/爆炎符.tres",
      "icon_path": "",
      "description": "符箓·火焰爆裂，灼烧敌人",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "符箓",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 16,
      "damage_type": "spiritual",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "intelligence",
          "multiplier": 0.5
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_a6be0254",
      "projectSource": true,
      "skill_name": "避锋",
      "resource_path": "res://resources/activeskills/避锋.tres",
      "icon_path": "res://textures/cards/expressive_ink/bifeng_ink_v4.png",
      "description": "应对：受到物理攻击时自动打出。本次受击的闪避判定中，身法 +20%。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_physical_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "evasion",
          "power": 0.2
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_0655aad2",
      "projectSource": true,
      "skill_name": "补虚行血",
      "resource_path": "res://resources/activeskills/补虚行血诀/补虚行血.tres",
      "icon_path": "",
      "description": "获得【补虚行血】（唯一Buff，持续至本场战斗结束）。【补虚行血】：每回合首次气血由1层降至0层时，本回合下一张由自身主动打出的催血牌少消耗1点精力，且额外获得气血1层。\n主修额外追加：该催血牌结算后抽牌1张。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "补虚行血诀",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_abc6623f",
      "projectSource": true,
      "skill_name": "藏锋待发",
      "resource_path": "res://resources/activeskills/藏锋卸力诀/藏锋待发.tres",
      "icon_path": "",
      "description": "藏锋守中，蓄力待发。获得护盾；若这些护盾保留到自身下一回合开始，获得1层剑意。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "藏锋卸力诀",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "3",
      "condition_value": "藏锋卸力诀",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "shield",
          "formula": "base:5;sword_proficiency:0.18;physique:0.18"
        },
        {
          "effect": "gain_intent_next_turn_if_shield_remains",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_bfcb5440",
      "projectSource": true,
      "skill_name": "沉桥压身",
      "resource_path": "res://resources/activeskills/太祖长拳/沉桥压身.tres",
      "icon_path": "",
      "description": "沉桥压身，重击相邻敌人；消耗拳架时额外造成伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "太祖长拳",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "重击"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 16,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 1.8
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 1.6
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1bd5bd63",
      "projectSource": true,
      "skill_name": "承机",
      "resource_path": "res://resources/activeskills/阴符应物经/承机.tres",
      "icon_path": "",
      "description": "敌方攻击自身时使用；获得化解。化解层数 = max(1, floor(体魄/2) + floor(体道造诣/3))。该次攻击结算后，若自身生命未因该次攻击减少，获得借力1层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "阴符应物经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "化解",
          "stacks": 3,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [
        {
          "effect": "counter_block",
          "formula": "base:3"
        }
      ],
      "stacks_formula": "max(1, floor(physique/2) + floor(body_proficiency/3))",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_767be102",
      "projectSource": true,
      "skill_name": "乘物游骸",
      "resource_path": "res://resources/activeskills/乘物游骸真经/乘物游骸.tres",
      "icon_path": "",
      "description": "获得【乘物游骸】（唯一Buff，持续至本场战斗结束）。【乘物游骸】：每回合首次由自身主动打出的卡牌使气血下降时，获得游骸1层。\n主修额外追加：游骸成功闪避一张敌方卡牌的物理伤害后，对该攻击方额外结算1段物理伤害；该段伤害 = round(4 + 体魄×0.20 + 敏捷×0.15 + 体道造诣×0.15)。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "乘物游骸真经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "游骸",
          "stacks": 1,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c48c7524",
      "projectSource": true,
      "skill_name": "赤霄斩",
      "resource_path": "res://resources/activeskills/武器技能/赤霄斩.tres",
      "icon_path": "",
      "description": "以赤霄剑斩击相邻敌人，造成物理伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "武器技能",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 6,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.6
        },
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.8
        },
        {
          "attribute": "soul",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_9b2dd33f",
      "projectSource": true,
      "skill_name": "赤阳参同",
      "resource_path": "res://resources/activeskills/赤阳参同经/赤阳参同.tres",
      "icon_path": "",
      "description": "获得【赤阳参同】（唯一Buff，持续至本场战斗结束）与【血炉】。\n【赤阳参同】：自身主动使气血下降时，本次不回复生命，改为获得血炉值；血炉值 = 气血下降层数×当前气血回落回复率。其余“气血回落时”效果仍可触发。\n主修额外追加：每回合首次主动消耗血炉值时，若实际消耗不少于20点，对该牌原目标额外结算1段物理伤害；伤害 = round(4 + 体魄×0.25 + 体道造诣×0.15 + 实际消耗血炉值×0.20)。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "赤阳参同经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_10f367ee",
      "projectSource": true,
      "skill_name": "催血",
      "resource_path": "res://resources/activeskills/催血.tres",
      "icon_path": "res://textures/cards/expressive_ink/cuixue_ink_v4.png",
      "description": "获得气血：1层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_qi_blood",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_6a42ae26",
      "projectSource": true,
      "skill_name": "摧枝一剑",
      "resource_path": "res://resources/activeskills/折枝剑谱/摧枝一剑.tres",
      "icon_path": "",
      "description": "枝裂势尽，一剑绝命。命中后最多消耗5层折损，每层使本次伤害提高15%；结算后根据目标已有的外伤重新施加剑痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 2,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "1",
      "condition_value": "折枝剑谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 6,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.68
        },
        {
          "attribute": "agility",
          "multiplier": 0.2
        },
        {
          "attribute": "physique",
          "multiplier": 0.16
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "require_target_status",
          "status_id": "折损",
          "stacks": 1
        },
        {
          "effect": "consume_fracture_for_damage",
          "max_stacks": 5,
          "damage_per_stack": 0.15,
          "consume_after_hit": true
        },
        {
          "effect": "critical_if_target_has_wound_any_zone",
          "amount": 12
        },
        {
          "effect": "consume_marks_add_wound",
          "marks": 3,
          "wound": "外伤",
          "destination": "discard"
        },
        {
          "effect": "restore_marks_from_wounds",
          "wounds_per_mark": 2,
          "max_marks": 2
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_0988dfa5",
      "projectSource": true,
      "skill_name": "淬厉",
      "resource_path": "res://resources/activeskills/内景损益篇/淬厉.tres",
      "icon_path": "",
      "description": "向弃牌堆加入2张【伤势】；抽牌1张。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "内景损益篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "draw",
          "count": 1
        },
        {
          "effect": "add_wound",
          "wound": "内伤",
          "count": 2,
          "destination": "discard"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_64cd5f64",
      "projectSource": true,
      "skill_name": "调息",
      "resource_path": "res://resources/activeskills/调息.tres",
      "icon_path": "",
      "description": "调整呼吸，回转气力，按体魄、敏捷与体术造诣恢复精力。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:14;physique:0.2;agility:0.25;body_proficiency:0.15"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_079aa926",
      "projectSource": true,
      "skill_name": "顶心肘",
      "resource_path": "res://resources/activeskills/市井游身功/顶心肘.tres",
      "icon_path": "",
      "description": "借对方攻势贴身肘击。若上一张打出的牌是攻击牌，本次攻击会心提高。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "市井游身功",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "乘势"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "after_attack",
      "momentum_effect_params": "agility:0.02;body_proficiency:0.03",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 6,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "agility",
          "multiplier": 1.5
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 1
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_f638bd5a",
      "projectSource": true,
      "skill_name": "短打",
      "resource_path": "res://resources/activeskills/短打.tres",
      "icon_path": "",
      "description": "最基础的近身击打，不依赖任何流派传承。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 8,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.35
        },
        {
          "attribute": "agility",
          "multiplier": 0.35
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_35c09191",
      "projectSource": true,
      "skill_name": "发劲",
      "resource_path": "res://resources/activeskills/拳脚.tres",
      "icon_path": "res://textures/cards/expressive_ink/fajin_ink_v4.png",
      "description": "对指定1名敌方造成物理伤害。伤害 = round(8 + 体魄×0.4 + 体道造诣×0.2 + 敏捷×0.25)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 8,
      "damage_type": "physical",
      "damage_formula": "round(8 + 体魄×0.4 + 体道造诣×0.2 + 敏捷×0.25)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.4
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.2
        },
        {
          "attribute": "agility",
          "multiplier": 0.25
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8417d566",
      "projectSource": true,
      "skill_name": "返照",
      "resource_path": "res://resources/activeskills/黄庭养形篇/返照.tres",
      "icon_path": "",
      "description": "气血-1；抽牌2张。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "黄庭养形篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "spend_qi_blood",
          "count": 1
        },
        {
          "effect": "draw",
          "count": 2
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_d2da8cbc",
      "projectSource": true,
      "skill_name": "冯虚",
      "resource_path": "res://resources/activeskills/乘物游骸真经/冯虚.tres",
      "icon_path": "",
      "description": "仅当本场已消耗游骸层数不少于2时可打出；不消耗当前游骸；对指定1名敌方结算物理伤害：伤害段数 = 1 + floor(本场实际消耗游骸层数÷2)；每段伤害 = round(8 + 体魄×0.45 + 敏捷×0.30 + 体道造诣×0.30)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "乘物游骸真经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 10,
      "damage_type": "physical",
      "damage_formula": "round(8 + 体魄×0.45 + 敏捷×0.30 + 体道造诣×0.30)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.55
        },
        {
          "attribute": "agility",
          "multiplier": 0.4
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.35
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c55c3448",
      "projectSource": true,
      "skill_name": "伏火",
      "resource_path": "res://resources/activeskills/赤阳参同经/伏火.tres",
      "icon_path": "",
      "description": "每回合限一次。仅当自身气血至少为1层时可打出；气血-1；本次气血回落回复率R+3个百分点。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "赤阳参同经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "spend_qi_blood",
          "count": 1
        },
        {
          "effect": "gain_blood_furnace",
          "count": 6
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_f27f9522",
      "projectSource": true,
      "skill_name": "拂枝点痕",
      "resource_path": "res://resources/activeskills/折枝剑谱/拂枝点痕.tres",
      "icon_path": "",
      "description": "剑锋轻拂，点隙留痕。命中后获得剑意并施加剑痕；攻击未留痕目标时额外施加一层剑痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 3,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.48
        },
        {
          "attribute": "agility",
          "multiplier": 0.16
        },
        {
          "attribute": "physique",
          "multiplier": 0.12
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_sword_intent",
          "count": 1
        },
        {
          "effect": "apply_sword_mark",
          "count": 1
        },
        {
          "effect": "apply_sword_mark_if_absent",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_fbc86340",
      "projectSource": true,
      "skill_name": "拱身架招",
      "resource_path": "res://resources/activeskills/妖兽/灰背野猪/拱身戒备.tres",
      "icon_path": "",
      "description": "敌方攻击自身时自动打出。化解层数 = 3 + floor(体魄/4) + floor(敏捷/5)。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_physical_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "化解",
          "stacks": 5,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "3 + floor(physique/4) + floor(agility/5)",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8c673b80",
      "projectSource": true,
      "skill_name": "拱撞",
      "resource_path": "res://resources/activeskills/妖兽/灰背野猪/拱撞.tres",
      "icon_path": "",
      "description": "以獠牙猛力拱击。伤害 = round(15 + 体魄×1.2)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "round(15 + physique*1.2)",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_3effb869",
      "projectSource": true,
      "skill_name": "观痕养意",
      "resource_path": "res://resources/activeskills/听锋寻隙篇/观痕养意.tres",
      "icon_path": "",
      "description": "观痕养意，借敌之隙调匀气息。场上存在带有剑痕的敌人时获得1层剑意；每名带有剑痕的敌人恢复2点精力，最多计算3名。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "听锋寻隙篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "3",
      "condition_value": "听锋寻隙篇",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_intent_if_marked_enemy_exists",
          "count": 1
        },
        {
          "effect": "restore_stamina_per_marked_enemy",
          "amount": 2,
          "max_targets": 3
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8f04569c",
      "projectSource": true,
      "skill_name": "归藏",
      "resource_path": "res://resources/activeskills/黄庭养形篇/归藏.tres",
      "icon_path": "",
      "description": "仅当本回合已主动使气血下降至少2次时可打出；抽牌3张；本回合随后两张由自身主动打出的锻躯牌各少消耗1点精力，最低为0。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "黄庭养形篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "draw",
          "count": 3
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1be1d378",
      "projectSource": true,
      "skill_name": "滚身卸劲",
      "resource_path": "res://resources/activeskills/市井游身功/滚地翻.tres",
      "icon_path": "",
      "description": "低身卸劲，不再进行位移；获得3层身法，应对下一次来攻。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "市井游身功",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "身法",
          "stacks": 3,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c53f067c",
      "projectSource": true,
      "skill_name": "还阳",
      "resource_path": "res://resources/activeskills/补虚行血诀/还阳.tres",
      "icon_path": "",
      "description": "若自身气血为0，获得气血2层；否则获得气血1层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "补虚行血诀",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_qi_blood",
          "count": 2
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_889193bf",
      "projectSource": true,
      "skill_name": "撼山",
      "resource_path": "res://resources/activeskills/撼山.tres",
      "icon_path": "res://textures/cards/expressive_ink/hanshan_ink_v4.png",
      "description": "对指定1名敌方造成物理伤害。伤害 = round(15 + 体魄×0.85 + 体道造诣×0.55)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 15,
      "damage_type": "physical",
      "damage_formula": "round(15 + 体魄×0.85 + 体道造诣×0.55)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.85
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.55
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_beb75426",
      "projectSource": true,
      "skill_name": "合劲",
      "resource_path": "res://resources/activeskills/阴符应物经/合劲.tres",
      "icon_path": "",
      "description": "对指定1名敌方造成物理伤害。伤害 = round((10 + 体魄×0.55 + 体道造诣×0.35)×(1 + 借力层数×0.08))。借力每层总增伤8%（公共效果5% + 【合劲】额外3%）；本牌结算后移除全部借力。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "阴符应物经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 10,
      "damage_type": "physical",
      "damage_formula": "round((10 + 体魄×0.55 + 体道造诣×0.35)×(1 + 借力层数×0.08))",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.55
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.35
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c30bfd76",
      "projectSource": true,
      "skill_name": "横剑卸劲",
      "resource_path": "res://resources/activeskills/藏锋卸力诀/横剑卸劲.tres",
      "icon_path": "",
      "description": "横剑卸劲，借来势还其身。敌方攻击自身时自动打出并获得化解；至少化解1点伤害后获得借力，使下一张由玩家主动打出的剑术攻击牌精力消耗减少3点。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "藏锋卸力诀",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "3",
      "condition_value": "藏锋卸力诀",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "counter_block"
        },
        {
          "effect": "gain_borrowed_force_if_blocked",
          "stamina_reduction": 3,
          "expires_next_turn_end": true
        }
      ],
      "stacks_formula": "max(1, floor(sword_proficiency/8) + floor(physique/12))",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_3145e511",
      "projectSource": true,
      "skill_name": "护元",
      "resource_path": "res://resources/activeskills/护元.tres",
      "icon_path": "",
      "description": "调动灵息护住周身，获得可累积的护盾。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "shield",
          "formula": "base:10;intelligence:0.15;soul:0.15;magic_proficiency:0.25"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_aaf1b93f",
      "projectSource": true,
      "skill_name": "黄庭养形",
      "resource_path": "res://resources/activeskills/黄庭养形篇/黄庭养形.tres",
      "icon_path": "",
      "description": "获得【黄庭养形】（唯一Buff，持续至本场战斗结束）。【黄庭养形】：每回合首次由自身主动打出的卡牌使气血下降后，抽牌1张。\n主修额外追加：本回合第二次主动使气血下降后，获得精力1点。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "黄庭养形篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_ccecbfbd",
      "projectSource": true,
      "skill_name": "挥砍",
      "resource_path": "res://resources/activeskills/武器技能/挥砍.tres",
      "icon_path": "",
      "description": "以剑刃横斩相邻敌人，伤害随体魄和剑道造诣提高。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "武器技能",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.5
        },
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.6
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1567bf73",
      "projectSource": true,
      "skill_name": "回春丹",
      "resource_path": "res://resources/activeskills/符箓/回春丹.tres",
      "icon_path": "",
      "description": "丹药·回复生命，活血化瘀",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "符箓",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "shield",
          "power": 10
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_fd7a25de",
      "projectSource": true,
      "skill_name": "回锋添痕",
      "resource_path": "res://resources/activeskills/折枝剑谱/回锋添痕.tres",
      "icon_path": "",
      "description": "回锋添痕，借势再折。敌方攻击自身时自动打出，获得化解并向攻击者施加剑痕。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "1",
      "condition_value": "折枝剑谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "counter_block"
        },
        {
          "effect": "apply_sword_mark_to_attacker",
          "count": 1
        },
        {
          "effect": "bonus_block_if_attacker_fractured",
          "count": 1
        },
        {
          "effect": "apply_fracture_if_stance_and_attacker_wounded",
          "count": 1
        }
      ],
      "stacks_formula": "max(1, floor(sword_proficiency/6) + floor(physique/10))",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_14c7d6e8",
      "projectSource": true,
      "skill_name": "回气",
      "resource_path": "res://resources/activeskills/回气.tres",
      "icon_path": "",
      "description": "平复呼吸，恢复精力。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:14;physique:0.15;agility:0.15;intelligence:0.15;soul:0.15"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_538c3e00",
      "projectSource": true,
      "skill_name": "回身点锋",
      "resource_path": "res://resources/activeskills/惊鸿掠影谱/回身点锋.tres",
      "icon_path": "",
      "description": "回身一点，剑锋随步而至。命中后获得1层剑意；上一张打出的牌是遁身牌时，本次伤害提高20%；目标带有剑痕时，会心+4。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "惊鸿掠影谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "3",
      "condition_value": "惊鸿掠影谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 3,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.38
        },
        {
          "attribute": "agility",
          "multiplier": 0.22
        },
        {
          "attribute": "physique",
          "multiplier": 0.08
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_sword_intent",
          "count": 1
        },
        {
          "effect": "bonus_damage_if_previous_movement",
          "multiplier": 0.2
        },
        {
          "effect": "critical_if_target_marked",
          "amount": 4
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_fd544236",
      "projectSource": true,
      "skill_name": "魂刺",
      "resource_path": "res://resources/activeskills/魂刺.tres",
      "icon_path": "",
      "description": "以一缕灵力凝作神魂尖刺，直指敌手识海。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "soul",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "soul",
          "multiplier": 0.65
        },
        {
          "attribute": "intelligence",
          "multiplier": 0.2
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_b0ff602a",
      "projectSource": true,
      "skill_name": "疾拱",
      "resource_path": "res://resources/activeskills/妖兽/灰背野猪/疾拱.tres",
      "icon_path": "",
      "description": "迅蹄踏地，借速度斜冲。伤害 = round(10 + 体魄×0.8 + 敏捷×1.6)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "round(10 + physique*0.8 + agility*1.6)",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_552bf38f",
      "projectSource": true,
      "skill_name": "架梁",
      "resource_path": "res://resources/activeskills/太祖长拳/架梁.tres",
      "icon_path": "",
      "description": "架肘沉肩，如梁承重。敌方攻击自身时自动打出，获得化解状态。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "太祖长拳",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_punch_frame",
          "count": 1
        }
      ],
      "stacks_formula": "floor(physique/4) + floor(body_proficiency/3) + 1",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_ea35b7b3",
      "projectSource": true,
      "skill_name": "架招",
      "resource_path": "res://resources/activeskills/架招.tres",
      "icon_path": "res://textures/cards/expressive_ink/jiazhao_ink_v4.png",
      "description": "敌方攻击自身时使用；获得化解。化解层数 = 3 + floor(体魄/4) + floor(敏捷/5)。化解仅抵消本次攻击的物理伤害：每层抵消1点；该次攻击结算后全部消散。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "化解",
          "stacks": 5,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "3 + floor(physique/4) + floor(agility/5)",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_998de6f8",
      "projectSource": true,
      "skill_name": "解形",
      "resource_path": "res://resources/activeskills/乘物游骸真经/解形.tres",
      "icon_path": "",
      "description": "仅当自身拥有游骸时可打出；消耗1层游骸；对指定1名敌方造成物理伤害：伤害 = round(7 + 体魄×0.35 + 敏捷×0.25 + 体道造诣×0.25)。该层游骸计入本场已消耗游骸层数。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "乘物游骸真经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 7,
      "damage_type": "physical",
      "damage_formula": "round(7 + 体魄×0.35 + 敏捷×0.25 + 体道造诣×0.25)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.35
        },
        {
          "attribute": "agility",
          "multiplier": 0.25
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.25
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_58f739be",
      "projectSource": true,
      "skill_name": "借力还锋",
      "resource_path": "res://resources/activeskills/藏锋卸力诀/借力还锋.tres",
      "icon_path": "",
      "description": "借力还锋，以守势送出一剑。自身拥有护盾、化解或借力时精准+6；若本牌消耗了借力，命中后向目标施加1层剑痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "藏锋卸力诀",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "3",
      "condition_value": "藏锋卸力诀",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.42
        },
        {
          "attribute": "physique",
          "multiplier": 0.2
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "precision_if_guarding",
          "amount": 6
        },
        {
          "effect": "consume_borrowed_force_and_mark",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_5731f5cc",
      "projectSource": true,
      "skill_name": "进步崩拳",
      "resource_path": "res://resources/activeskills/太祖长拳/进步崩拳.tres",
      "icon_path": "",
      "description": "上步进身，以崩拳重击相邻敌人；消耗拳架时额外造成伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "太祖长拳",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "重击"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 12,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 1.5
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 2
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_41fcbabd",
      "projectSource": true,
      "skill_name": "惊鸿蓄锋",
      "resource_path": "res://resources/activeskills/惊鸿掠影谱/惊鸿掠步.tres",
      "icon_path": "",
      "description": "振衣蓄锋，不再进行位移；获得2层身法，为下一次交锋留出余地。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "惊鸿掠影谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "3",
      "condition_value": "惊鸿掠影谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "身法",
          "stacks": 2,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_93d1e84f",
      "projectSource": true,
      "skill_name": "静观",
      "resource_path": "res://resources/activeskills/静观.tres",
      "icon_path": "res://textures/cards/expressive_ink/jingguan_ink_v4.png",
      "description": "静下心神，抽取2张卡牌。本场消耗。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "观势",
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "draw",
          "count": 2
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_b19705d3",
      "projectSource": true,
      "skill_name": "聚息",
      "resource_path": "res://resources/activeskills/聚息.tres",
      "icon_path": "",
      "description": "短暂敛神聚息，恢复精力，为后续运气蓄势。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:18;intelligence:0.2;soul:0.2;magic_proficiency:0.35"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_77b6ad5f",
      "projectSource": true,
      "skill_name": "雷击符",
      "resource_path": "res://resources/activeskills/符箓/雷击符.tres",
      "icon_path": "",
      "description": "符箓·引雷一击，麻痹敌人",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "符箓",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 2,
      "range_max": 5,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 12,
      "damage_type": "spiritual",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "intelligence",
          "multiplier": 0.4
        },
        {
          "attribute": "soul",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_4a5cdcba",
      "projectSource": true,
      "skill_name": "烈焰突刺",
      "resource_path": "res://resources/activeskills/武器技能/烈焰突刺.tres",
      "icon_path": "",
      "description": "向1至3格内释放烈焰剑气，攻击目标及其周围敌人；打出后从本场战斗中移除。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "武器技能",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 5,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 1,
      "can_target_empty": true,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 5,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "agility",
          "multiplier": 0.6
        },
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.6
        },
        {
          "attribute": "soul",
          "multiplier": 0.4
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_01b68487",
      "projectSource": true,
      "skill_name": "裂喉",
      "resource_path": "res://resources/activeskills/妖兽/林地凶狼/裂喉.tres",
      "icon_path": "",
      "description": "血牙撕开要害。伤害 = round(8 + 体魄×0.9 + 敏捷×0.9)；命中后向目标弃牌堆加入1张外伤。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "round(8 + physique*0.9 + agility*0.9)",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "add_wound",
          "wound": "外伤",
          "count": 1,
          "destination": "discard"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_25a77fc6",
      "projectSource": true,
      "skill_name": "裂柯",
      "resource_path": "res://resources/activeskills/折枝剑谱/裂柯.tres",
      "icon_path": "",
      "description": "裂柯，伤从痕开。只能攻击带有剑痕的敌人；命中后先施加1层剑痕，再消耗2层剑痕，并向目标弃牌堆加入1张外伤。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "1",
      "condition_value": "折枝剑谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.5
        },
        {
          "attribute": "agility",
          "multiplier": 0.18
        },
        {
          "attribute": "physique",
          "multiplier": 0.14
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "require_target_status",
          "status_id": "剑痕",
          "stacks": 1
        },
        {
          "effect": "gain_sword_intent",
          "count": 1
        },
        {
          "effect": "apply_sword_mark",
          "count": 1
        },
        {
          "effect": "consume_marks_add_wound",
          "marks": 2,
          "wound": "外伤",
          "destination": "discard"
        },
        {
          "effect": "apply_fracture_if_target_has_wound_any_zone",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_3ffa0b1f",
      "projectSource": true,
      "skill_name": "灵爆",
      "resource_path": "res://resources/activeskills/灵爆.tres",
      "icon_path": "",
      "description": "压缩灵息后骤然引爆，对远处目标造成强力灵力冲击。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 13,
      "damage_type": "spiritual",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "intelligence",
          "multiplier": 0.9
        },
        {
          "attribute": "magic_proficiency",
          "multiplier": 0.6
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_cdaedd0a",
      "projectSource": true,
      "skill_name": "灵击",
      "resource_path": "res://resources/activeskills/灵击.tres",
      "icon_path": "",
      "description": "牵引一缕灵息凝成短促冲击，对目标造成法术伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "spiritual",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "intelligence",
          "multiplier": 0.5
        },
        {
          "attribute": "soul",
          "multiplier": 0.35
        },
        {
          "attribute": "magic_proficiency",
          "multiplier": 0.2
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_ce39696b",
      "projectSource": true,
      "skill_name": "灵枢经筋",
      "resource_path": "res://resources/activeskills/灵枢经筋篇/灵枢经筋.tres",
      "icon_path": "",
      "description": "获得【灵枢经筋】（唯一Buff，持续至本场战斗结束）。【灵枢经筋】：每回合首次主动消耗手中的【伤势】牌时，获得借力1层。\n主修额外追加：若该次主动消耗累计不少于3张【伤势】，恢复气血1层。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "灵枢经筋篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8dae61bd",
      "projectSource": true,
      "skill_name": "留痕",
      "resource_path": "res://resources/activeskills/明微抱元经/留痕.tres",
      "icon_path": "",
      "description": "以神魂映照，令痕迹深入目标。造成神魂伤害，并留下照痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "明微抱元经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 4,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 8,
      "damage_type": "soul",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "照痕",
          "stacks": 1,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_e75be4ee",
      "projectSource": true,
      "skill_name": "掠影扑杀",
      "resource_path": "res://resources/activeskills/妖兽/林地凶狼/掠影扑杀.tres",
      "icon_path": "",
      "description": "借影疾扑。伤害 = round(5 + 体魄×0.5 + 敏捷×1.2)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "round(5 + physique*0.5 + agility*1.2)",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_667ebf43",
      "projectSource": true,
      "skill_name": "内景损益",
      "resource_path": "res://resources/activeskills/内景损益篇/内景损益.tres",
      "icon_path": "",
      "description": "获得【内景损益】（唯一Buff，持续至本场战斗结束）。【内景损益】：战斗牌堆每有1张【伤势】，自身主动打出的锻躯物理伤害提高3%。\n主修额外追加：每张伤势额外提高2%，即合计提高5%。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "内景损益篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_65c281ff",
      "projectSource": true,
      "skill_name": "内伤",
      "resource_path": "res://resources/activeskills/业障/内伤.tres",
      "icon_path": "",
      "description": "伤入经脉，气机不畅。在手牌中时持续降低行动速度、提高法力消耗并降低护心。",
      "skill_type": "CURSE",
      "element": "NONE",
      "source_type": "业障",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "伤势"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c51a0e10",
      "projectSource": true,
      "skill_name": "泥鳅身",
      "resource_path": "res://resources/activeskills/市井游身功/泥鳅身.tres",
      "icon_path": "",
      "description": "扭身卸力，如泥鳅般滑溜。敌方攻击自身时自动打出，获得3层身法。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "市井游身功",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_ece6d344",
      "projectSource": true,
      "skill_name": "平息",
      "resource_path": "res://resources/activeskills/吐纳归元.tres",
      "icon_path": "",
      "description": "气血-1；本次气血下降触发公共规则，回复最大生命×3%。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "spend_qi_blood",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_b33adf40",
      "projectSource": true,
      "skill_name": "去故",
      "resource_path": "res://resources/activeskills/灵枢经筋篇/去故.tres",
      "icon_path": "",
      "description": "仅当手中至少有2张【伤势】牌时可打出；消耗手中至多3张【伤势】牌；每消耗1张，获得借力1层。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "灵枢经筋篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_adf301ca",
      "projectSource": true,
      "skill_name": "撒石灰",
      "resource_path": "res://resources/activeskills/市井游身功/撒石灰.tres",
      "icon_path": "",
      "description": "向相邻敌人撒出石灰并造成物理伤害；打出后从本场战斗中移除。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "市井游身功",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 2,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "agility",
          "multiplier": 1
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_295c2057",
      "projectSource": true,
      "skill_name": "伤势",
      "resource_path": "res://resources/activeskills/业障/伤势.tres",
      "icon_path": "",
      "description": "无法主动打出。每张使本场最大生命降低1%。",
      "skill_type": "CURSE",
      "element": "NONE",
      "source_type": "业障",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "伤势"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c7ab0710",
      "projectSource": true,
      "skill_name": "摄魂",
      "resource_path": "res://resources/activeskills/摄魂.tres",
      "icon_path": "",
      "description": "直摄敌手神魂，造成重创并撕开魂隙。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 11,
      "damage_type": "soul",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "soul",
          "multiplier": 0.9
        },
        {
          "attribute": "intelligence",
          "multiplier": 0.45
        }
      ],
      "status_effects": [
        {
          "status_id": "魂隙",
          "stacks": 1,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_6be2cbbc",
      "projectSource": true,
      "skill_name": "身随剑转",
      "resource_path": "res://resources/activeskills/惊鸿掠影谱/身随剑转.tres",
      "icon_path": "",
      "description": "身随剑转，剑落步生。收束呼吸与剑势，立刻回复少量精力，不再依赖遁身与位移。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "惊鸿掠影谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "3",
      "condition_value": "惊鸿掠影谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:4;sword_proficiency:0.2"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_6dabcd33",
      "projectSource": true,
      "skill_name": "撕咬",
      "resource_path": "res://resources/activeskills/妖兽/林地凶狼/撕咬.tres",
      "icon_path": "",
      "description": "扑近撕咬。伤害 = round(7 + 体魄×0.7 + 敏捷×0.8)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "round(7 + physique*0.7 + agility*0.8)",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8cb43e62",
      "projectSource": true,
      "skill_name": "太祖起手",
      "resource_path": "res://resources/activeskills/太祖长拳/太祖长拳.tres",
      "icon_path": "",
      "description": "以刚猛起手式攻击相邻敌人，并获得拳架。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "太祖长拳",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 5,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 1.2
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 1.3
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_punch_frame",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_05b8e011",
      "projectSource": true,
      "skill_name": "挑枝断筋",
      "resource_path": "res://resources/activeskills/折枝剑谱/挑枝断筋.tres",
      "icon_path": "",
      "description": "挑枝断筋，新旧痕迹一并牵裂。命中后施加2层剑痕；目标随后至少有3层剑痕时，消耗3层并向目标弃牌堆加入1张外伤。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 1,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "1",
      "condition_value": "折枝剑谱",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 4,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.54
        },
        {
          "attribute": "agility",
          "multiplier": 0.2
        },
        {
          "attribute": "physique",
          "multiplier": 0.14
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_sword_intent",
          "count": 1
        },
        {
          "effect": "apply_sword_mark",
          "count": 2
        },
        {
          "effect": "consume_marks_add_wound",
          "marks": 3,
          "wound": "外伤",
          "destination": "discard"
        },
        {
          "effect": "apply_fracture_on_wound",
          "count": 1
        },
        {
          "effect": "critical_if_target_has_wound_any_zone",
          "amount": 10
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_4bea376a",
      "projectSource": true,
      "skill_name": "听锋辨隙",
      "resource_path": "res://resources/activeskills/听锋寻隙篇/听锋辨隙.tres",
      "icon_path": "",
      "description": "听锋辨隙，先定其破绽。选择1至3格内的一名敌人，施加1层剑痕；本回合下一张由玩家主动打出且以该敌人为目标的剑术攻击牌精准+6。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "听锋寻隙篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "3",
      "condition_value": "听锋寻隙篇",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "apply_sword_mark",
          "count": 1
        },
        {
          "effect": "prime_next_sword_attack_against_target",
          "precision": 6
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_dc558518",
      "projectSource": true,
      "skill_name": "痛击",
      "resource_path": "res://resources/activeskills/痛击.tres",
      "icon_path": "res://textures/cards/expressive_ink/tongji_ink_v4.png",
      "description": "看准破绽的一记重击；比两次应敌更省手牌，但需预留精力。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "重击"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 11,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.45
        },
        {
          "attribute": "agility",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_45e1df1a",
      "projectSource": true,
      "skill_name": "突刺",
      "resource_path": "res://resources/activeskills/武器技能/突刺.tres",
      "icon_path": "",
      "description": "以剑尖直刺1至2格内的敌人；若上一张打出的牌是攻击牌，本次攻击会心提高。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "武器技能",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "乘势"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "after_attack",
      "momentum_effect_params": "agility:0.03",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 3,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "agility",
          "multiplier": 0.5
        },
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.5
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_107cb1e6",
      "projectSource": true,
      "skill_name": "蜕形",
      "resource_path": "res://resources/activeskills/乘物游骸真经/蜕形.tres",
      "icon_path": "",
      "description": "气血-1；获得身法2层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "乘物游骸真经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "身法",
          "stacks": 2,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [
        {
          "effect": "spend_qi_blood",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_726dcabc",
      "projectSource": true,
      "skill_name": "托身",
      "resource_path": "res://resources/activeskills/乘物游骸真经/托身.tres",
      "icon_path": "",
      "description": "仅当自身当前拥有游骸时可打出；获得身法2层。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "乘物游骸真经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "保留"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "身法",
          "stacks": 2,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_be8805a2",
      "projectSource": true,
      "skill_name": "外伤",
      "resource_path": "res://resources/activeskills/业障/外伤.tres",
      "icon_path": "",
      "description": "创口未合，动则牵痛。在手牌中时持续降低行动速度，并削弱遁身牌移动、遁身牌精力消耗与身法。",
      "skill_type": "CURSE",
      "element": "NONE",
      "source_type": "业障",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "伤势"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_ec701c0e",
      "projectSource": true,
      "skill_name": "挟势",
      "resource_path": "res://resources/activeskills/阴符应物经/挟势.tres",
      "icon_path": "",
      "description": "对指定1名敌方造成物理伤害。伤害（未计借力） = round(6 + 体魄×0.45 + 体道造诣×0.30)。本牌享受【借力】对物理伤害的加成；本牌结算后不移除借力。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "阴符应物经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 6,
      "damage_type": "physical",
      "damage_formula": "round(6 + 体魄×0.45 + 体道造诣×0.30)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.45
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c5577035",
      "projectSource": true,
      "skill_name": "卸力",
      "resource_path": "res://resources/activeskills/卸力.tres",
      "icon_path": "res://textures/cards/expressive_ink/xieli_ink_v4.png",
      "description": "敌方攻击自身时使用；获得化解。化解层数 = max(1, floor(体魄/2) + floor(体道造诣/3))。化解仅抵消本次攻击的物理伤害：每层抵消1点；该次攻击结算后全部消散。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "max(1, floor(physique/2) + floor(body_proficiency/3))",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_69f808cd",
      "projectSource": true,
      "skill_name": "蓄势重击",
      "resource_path": "res://resources/activeskills/蓄势重击.tres",
      "icon_path": "res://textures/cards/expressive_ink/xushi_ink_v4.png",
      "description": "蓄足力道后出手的强力近身一击。若持有拳架，消耗拳架并强化本次伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "重击",
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 14,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.6
        },
        {
          "attribute": "agility",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1d8bddcd",
      "projectSource": true,
      "skill_name": "循隙一刺",
      "resource_path": "res://resources/activeskills/听锋寻隙篇/循隙一刺.tres",
      "icon_path": "",
      "description": "循隙递锋，一刺中的。目标没有剑痕时，命中后施加1层剑痕；目标已有剑痕时，本次伤害提高15%，且不消耗剑痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "听锋寻隙篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "3",
      "condition_value": "听锋寻隙篇",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 3,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.44
        },
        {
          "attribute": "soul",
          "multiplier": 0.14
        },
        {
          "attribute": "agility",
          "multiplier": 0.1
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "apply_sword_mark_if_absent",
          "count": 1
        },
        {
          "effect": "bonus_damage_if_target_marked",
          "multiplier": 0.15
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1815309b",
      "projectSource": true,
      "skill_name": "压枝",
      "resource_path": "res://resources/activeskills/折枝剑谱/压枝.tres",
      "icon_path": "",
      "description": "压枝，旧痕作伤。命中后施加1层剑痕；目标随后至少有2层剑痕时，消耗2层并向目标弃牌堆加入1张外伤。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 2,
      "range_min": 1,
      "range_max": 2,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 3,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "sword_proficiency",
          "multiplier": 0.42
        },
        {
          "attribute": "agility",
          "multiplier": 0.2
        },
        {
          "attribute": "physique",
          "multiplier": 0.12
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_sword_intent",
          "count": 1
        },
        {
          "effect": "apply_sword_mark",
          "count": 1
        },
        {
          "effect": "bonus_damage_if_pre_mark",
          "minimum": 2,
          "multiplier": 0.2
        },
        {
          "effect": "consume_marks_add_wound",
          "marks": 2,
          "wound": "外伤",
          "destination": "discard"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_fde1a9b3",
      "projectSource": true,
      "skill_name": "偃月",
      "resource_path": "res://resources/activeskills/赤阳参同经/偃月.tres",
      "icon_path": "",
      "description": "消耗至多20点血炉值；对指定1名敌方造成物理伤害：伤害 = round(8 + 体魄×0.45 + 体道造诣×0.30 + 实际消耗血炉值×0.70)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "赤阳参同经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 8,
      "damage_type": "physical",
      "damage_formula": "round(8 + 体魄×0.45 + 体道造诣×0.30 + min(20, blood_furnace_value)×0.70)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.45
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.3
        }
      ],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "spend_blood_furnace",
          "count": 20
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_c1144afc",
      "projectSource": true,
      "skill_name": "养息",
      "resource_path": "res://resources/activeskills/养息.tres",
      "icon_path": "res://textures/cards/expressive_ink/yangxi_ink_v4.png",
      "description": "调匀呼吸，获得1点精力。本场消耗。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "调息",
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_energy",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_e533e083",
      "projectSource": true,
      "skill_name": "阴符应物",
      "resource_path": "res://resources/activeskills/阴符应物经/阴符应物.tres",
      "icon_path": "",
      "description": "获得【阴符应物】（唯一Buff，持续至本场战斗结束）。【阴符应物】：自身以【化解】应对敌方攻击时，每完整抵消4点该次攻击伤害，获得借力1层。\n主修额外追加：改为每完整抵消3点该次攻击伤害，获得借力1层。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "阴符应物经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "固有"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_8a8177ba",
      "projectSource": true,
      "skill_name": "引气调息",
      "resource_path": "res://resources/activeskills/引气步.tres",
      "icon_path": "",
      "description": "以吐纳牵引灵息，回复少量精力，不再进行位移。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:8;agility:0.15"
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_6f0fb9a2",
      "projectSource": true,
      "skill_name": "应敌",
      "resource_path": "res://resources/activeskills/应敌.tres",
      "icon_path": "res://textures/cards/expressive_ink/yingdi_ink_v4.png",
      "description": "以最朴素的步势迎敌，造成基础近身伤害。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 1,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 7,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.25
        },
        {
          "attribute": "agility",
          "multiplier": 0.2
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_9a96f0bb",
      "projectSource": true,
      "skill_name": "硬鬃架招",
      "resource_path": "res://resources/activeskills/妖兽/灰背野猪/硬鬃架招.tres",
      "icon_path": "",
      "description": "敌方物理攻击自身时自动打出。化解层数 = 5 + floor(体魄/3)。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_physical_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "化解",
          "stacks": 5,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "5 + floor(physique/3)",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_1f73235e",
      "projectSource": true,
      "skill_name": "游身架招",
      "resource_path": "res://resources/activeskills/妖兽/林地凶狼/游身架招.tres",
      "icon_path": "",
      "description": "敌方物理攻击自身时自动打出。化解层数 = 2 + floor(敏捷/4)。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "妖兽",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "应对"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "on_physical_attacked",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "化解",
          "stacks": 4,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "2 + floor(agility/4)",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_d9b63b70",
      "projectSource": true,
      "skill_name": "站桩",
      "resource_path": "res://resources/activeskills/站桩.tres",
      "icon_path": "res://textures/cards/expressive_ink/zhuangzhuang_ink_v4.png",
      "description": "沉肩坠肘，稳住拳架。获得1层拳架；本牌结算后移出本场战斗。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "基础通用",
      "source_gongfa": "",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 0,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_punch_frame",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_b51cdfd5",
      "projectSource": true,
      "skill_name": "照痕养锋",
      "resource_path": "res://resources/activeskills/折枝剑谱/照痕养锋.tres",
      "icon_path": "",
      "description": "照痕养锋，静候再发。场上存在带有剑痕的敌人时获得1层剑意，并使本回合下一张折枝攻击额外施加1层剑痕；场上没有剑痕时额外恢复4点精力。",
      "skill_type": "SPECIAL",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "restore_stamina",
          "formula": "base:10;sword_proficiency:0.28;physique:0.10"
        },
        {
          "effect": "gain_intent_and_prime_mark_if_marked_enemy",
          "intent": 1,
          "mark": 1
        },
        {
          "effect": "restore_stamina_if_no_marked_enemy",
          "amount": 4
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_7c83c918",
      "projectSource": true,
      "skill_name": "照形",
      "resource_path": "res://resources/activeskills/明微抱元经/照形.tres",
      "icon_path": "",
      "description": "照见其形，直探其魂。造成神魂伤害，并在目标身上留下自身来源的照痕。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "明微抱元经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 3,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 1,
      "range_max": 3,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 6,
      "damage_type": "soul",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "照痕",
          "stacks": 1,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_9d77b70d",
      "projectSource": true,
      "skill_name": "折枝·断势",
      "resource_path": "res://resources/activeskills/折枝剑谱/折枝·断势.tres",
      "icon_path": "",
      "description": "折枝断势，先夺其形。进入折枝剑势；下一回合起每回合消耗1层剑意维持。处于剑势时，每次成功消耗剑痕并加入外伤，额外施加1层折损。",
      "skill_type": "POWER",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 1,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "enter_sword_stance",
          "stance_id": "折枝剑势",
          "sub_intent": 1,
          "main_intent": 2,
          "upkeep_intent": 1,
          "upkeep_starts_next_turn": true
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_034b59c2",
      "projectSource": true,
      "skill_name": "折枝寻隙",
      "resource_path": "res://resources/activeskills/折枝剑谱/折步寻隙.tres",
      "icon_path": "",
      "description": "折枝寻隙，压身递剑。不再进行位移；获得2层身法，借空隙避开下一次来攻。",
      "skill_type": "BUFF",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "折枝剑谱",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": 0,
      "required_weapon_type": 1,
      "target_type": 0,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [
        {
          "status_id": "身法",
          "stacks": 2,
          "hit_formula": "1.0"
        }
      ],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_957ec97d",
      "projectSource": true,
      "skill_name": "周行",
      "resource_path": "res://resources/activeskills/黄庭养形篇/周行.tres",
      "icon_path": "",
      "description": "获得气血1层；若本回合已主动使气血下降，抽牌1张。",
      "skill_type": "DRAW",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "黄庭养形篇",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 2,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 1,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 0,
      "damage_type": "physical",
      "damage_formula": "",
      "damage_bonuses": [],
      "status_effects": [],
      "special_effects": [
        {
          "effect": "gain_qi_blood",
          "count": 1
        },
        {
          "effect": "draw",
          "count": 1
        }
      ],
      "stacks_formula": "",
      "move_formula": ""
    },
    {
      "schemaVersion": 1,
      "kind": "card",
      "id": "project_card_5641de47",
      "projectSource": true,
      "skill_name": "朱明",
      "resource_path": "res://resources/activeskills/赤阳参同经/朱明.tres",
      "icon_path": "",
      "description": "消耗全部血炉值；对指定1名敌方造成物理伤害：伤害 = round(12 + 体魄×0.60 + 体道造诣×0.40 + 实际消耗血炉值×1.00)。",
      "skill_type": "ATTACK",
      "element": "NONE",
      "source_type": "秘籍习得",
      "source_gongfa": "赤阳参同经",
      "required_realm": "",
      "required_attributes": {},
      "energy_cost": 3,
      "mana_cost": 0,
      "spirit_occupy": 0,
      "required_equip_slot": -1,
      "required_weapon_type": 0,
      "target_type": 2,
      "range_min": 0,
      "range_max": 0,
      "aoe_range": 0,
      "can_target_empty": false,
      "move_distance": 0,
      "keywords": [
        "消耗"
      ],
      "condition_type": "NONE",
      "condition_value": "",
      "momentum_condition_type": "",
      "momentum_effect_params": "",
      "flow_condition_type": "",
      "flow_condition_params": [],
      "counter_condition": "",
      "base_damage": 12,
      "damage_type": "physical",
      "damage_formula": "round(12 + 体魄×0.60 + 体道造诣×0.40 + blood_furnace_value×1.00)",
      "damage_bonuses": [
        {
          "attribute": "physique",
          "multiplier": 0.6
        },
        {
          "attribute": "body_proficiency",
          "multiplier": 0.4
        }
      ],
      "status_effects": [],
      "special_effects": [],
      "stacks_formula": "",
      "move_formula": ""
    }
  ],
  "manuals": [
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_c8f30d91",
      "projectSource": true,
      "gongfa_name": "补虚行血诀",
      "resource_path": "res://resources/gongfa/补虚行血诀.tres",
      "description": "以气血归零为发动点的锻躯行血法。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.1,
        "sinew": 0.05,
        "bone": 0.1,
        "blood": 0.4,
        "viscera": 0.2,
        "heart": 0.15
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "buxu",
          "chapter_name": "补虚",
          "description": "以气血见底为转机，建立零层回填循环。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/补虚行血诀/补虚行血.tres",
            "res://resources/activeskills/补虚行血诀/还阳.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "shouque",
          "chapter_name": "守缺",
          "description": "守虚以待，令空血成为可调度的窗口。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "buxu"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_qi_blood": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "yuwen",
          "chapter_name": "余温",
          "description": "将回落余温转为持续的再起余地。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "shouque"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "guiyuan",
          "chapter_name": "归元",
          "description": "空血复起，一次回满整个气血循环。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "yuwen"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_d5f0f205",
      "projectSource": true,
      "gongfa_name": "藏锋卸力诀",
      "resource_path": "res://resources/gongfa/藏锋卸力诀.tres",
      "description": "仅可辅修的守御剑诀。通过化解、护盾和借力降低高消耗剑招的使用压力。",
      "wuxing": "ALL",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 64,
      "required_attributes": {
        "physique": 8,
        "sword_proficiency": 8
      },
      "can_be_main": false,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "sword"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.3,
        "sinew": 0.2,
        "bone": 0.2,
        "blood": 0.1,
        "viscera": 0.1,
        "heart": 0.1
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "xie_feng",
          "chapter_name": "卸锋卷",
          "description": "受到攻击时化解伤害，并把防守转为下一次剑术攻击的减耗和精准。",
          "order": 1,
          "required_study_progress": 64,
          "required_realm": "",
          "required_attributes": {
            "physique": 8,
            "sword_proficiency": 8
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "crit_resist": 4,
            "physical_defense": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/藏锋卸力诀/横剑卸劲.tres",
            "res://resources/activeskills/藏锋卸力诀/借力还锋.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "REPLACE",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "格剑",
              "result_skill_path": "res://resources/activeskills/藏锋卸力诀/横剑卸劲.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "shou_zhong",
          "chapter_name": "守中卷",
          "description": "以护盾守住敌方攻势，并在下一回合获得剑意。",
          "order": 2,
          "required_study_progress": 96,
          "required_realm": "",
          "required_attributes": {
            "physique": 12,
            "sword_proficiency": 12
          },
          "required_chapter_ids": [
            "xie_feng",
            "4",
            "7"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "crit_resist": 4,
            "precision": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/藏锋卸力诀/藏锋待发.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_e1a447d9",
      "projectSource": true,
      "gongfa_name": "乘物游骸真经",
      "resource_path": "res://resources/gongfa/乘物游骸真经.tres",
      "description": "气血、游骸与身法并行的锻躯真经：以肉身根基压住敌方，游骸整张避伤，再把已耗游骸化为反击。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.25,
        "sinew": 0.4,
        "bone": 0.1,
        "blood": 0.1,
        "viscera": 0.05,
        "heart": 0.1
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "you_hai",
          "chapter_name": "游骸",
          "description": "以主动回落寄形于骸，建立确定闪避的起点。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "agility": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 30,
            "dodge": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/乘物游骸真经/乘物游骸.tres",
            "res://resources/activeskills/乘物游骸真经/蜕形.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "tuo_shen",
          "chapter_name": "托身",
          "description": "以身法延长游骸后的受击优势。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "agility": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "you_hai"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "dodge": 2,
            "max_qi_blood": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/乘物游骸真经/托身.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "jie_xing",
          "chapter_name": "解形",
          "description": "将游骸由守势转为可主动支取的攻伐资源。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "agility": 9,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "tuo_shen"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "agility": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "dodge": 2
          },
          "naqi_multipliers": {
            "physical_damage": 0.05
          },
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/乘物游骸真经/解形.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "feng_xu",
          "chapter_name": "冯虚",
          "description": "以本场已耗游骸为尺，将防守资源反转成重型反击。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "agility": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "jie_xing"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_qi_blood": 1,
            "dodge": 3
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/乘物游骸真经/冯虚.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_c1b33366",
      "projectSource": true,
      "gongfa_name": "赤阳参同经",
      "resource_path": "res://resources/gongfa/赤阳参同经.tres",
      "description": "锻躯主修法。主动烧气血，将公共回血转为血炉，再以炉火维持输出或清炉爆发。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.1,
        "sinew": 0.1,
        "bone": 0.1,
        "blood": 0.35,
        "viscera": 0.15,
        "heart": 0.2
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "fu_huo",
          "chapter_name": "伏火",
          "description": "立参同之炉，将主动气血回落炼为血炉。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 30,
            "max_qi_blood": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/赤阳参同经/赤阳参同.tres",
            "res://resources/activeskills/赤阳参同经/伏火.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "da_ye",
          "chapter_name": "大冶",
          "description": "一举焚血，快速筑高炉火。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "fu_huo"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "blood_furnace_value_cap": 30
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "yan_yue",
          "chapter_name": "偃月",
          "description": "分炉炼形，在持续输出与局部爆发间取舍。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "da_ye"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "blood_furnace_value_cap": 20
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/赤阳参同经/偃月.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "zhu_ming",
          "chapter_name": "朱明",
          "description": "满炉一击，完成云息期的清炉收束。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "yan_yue"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "blood_furnace_value_cap": 20
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/赤阳参同经/朱明.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_df5fea13",
      "projectSource": true,
      "gongfa_name": "黄庭养形篇",
      "resource_path": "res://resources/gongfa/黄庭养形篇.tres",
      "description": "锻躯主修法。以主动气血回落换取手牌与精力，再催血续接循环。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.2,
        "sinew": 0.15,
        "bone": 0.15,
        "blood": 0.2,
        "viscera": 0.15,
        "heart": 0.15
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "shou_zhong",
          "chapter_name": "守中",
          "description": "立黄庭之基，建立主动回落气血的手牌循环。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 30
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/黄庭养形篇/黄庭养形.tres",
            "res://resources/activeskills/黄庭养形篇/返照.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "bao_yi",
          "chapter_name": "抱一",
          "description": "回落后抱元复起，使气血循环不断。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "shou_zhong"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_qi_blood": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/黄庭养形篇/抱一.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "zhou_xing",
          "chapter_name": "周行",
          "description": "让回落与再催血形成更高密度的周身循环。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "bao_yi"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/黄庭养形篇/周行.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "gui_cang",
          "chapter_name": "归藏",
          "description": "将基础催血纳入黄庭循环，收束云息期的气血构筑。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "zhou_xing"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 20
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/黄庭养形篇/归藏.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_7244d3cf",
      "projectSource": true,
      "gongfa_name": "惊鸿掠影谱",
      "resource_path": "res://resources/gongfa/惊鸿掠影谱.tres",
      "description": "仅可辅修的身法剑谱。以剑后换位、步后再剑为主要打法。",
      "wuxing": "ALL",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 48,
      "required_attributes": {
        "agility": 5,
        "sword_proficiency": 6
      },
      "can_be_main": false,
      "can_be_sub": true,
      "bearing_compatibility": [
        2
      ],
      "school_tags": [
        "sword",
        "movement"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "jing_hong",
          "chapter_name": "惊鸿卷",
          "description": "剑后换位，步后再剑。",
          "order": 1,
          "required_study_progress": 48,
          "required_realm": "",
          "required_attributes": {
            "agility": 5,
            "sword_proficiency": 6
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 3,
            "initiative_speed": 3
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/惊鸿掠影谱/惊鸿掠步.tres",
            "res://resources/activeskills/惊鸿掠影谱/回身点锋.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "REPLACE",
              "count": 1,
              "source_skill_path": "res://resources/activeskills/避锋.tres",
              "source_skill_name": "避锋",
              "result_skill_path": "res://resources/activeskills/惊鸿掠影谱/惊鸿掠步.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "fan_ying",
          "chapter_name": "返影卷",
          "description": "出剑命中后，为下一次遁身降低精力消耗。",
          "order": 2,
          "required_study_progress": 72,
          "required_realm": "",
          "required_attributes": {
            "agility": 8,
            "sword_proficiency": 10
          },
          "required_chapter_ids": [
            "jing_hong",
            "4",
            "8"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "initiative_speed": 3,
            "critical": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/惊鸿掠影谱/身随剑转.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_fade1228",
      "projectSource": true,
      "gongfa_name": "灵枢经筋篇",
      "resource_path": "res://resources/gongfa/灵枢经筋篇.tres",
      "description": "伤势业障牌的消耗与借力构筑。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.15,
        "sinew": 0.35,
        "bone": 0.15,
        "blood": 0.15,
        "viscera": 0.1,
        "heart": 0.1
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "ling_shu",
          "chapter_name": "灵枢",
          "description": "以伤势入经筋，以消耗伤势换取借力。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/灵枢经筋篇/灵枢经筋.tres",
            "res://resources/activeskills/灵枢经筋篇/去故.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "tiao_shu",
          "chapter_name": "调枢",
          "description": "消伤养形，以伤势换取回血窗口。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "ling_shu"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "tong_tian",
          "chapter_name": "通天",
          "description": "令基础平息也能调度伤势构筑。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "tiao_shu"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "yin_jin",
          "chapter_name": "引筋",
          "description": "以伤势为价，收束一记筋骨重击。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "tong_tian"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_07a9397d",
      "projectSource": true,
      "gongfa_name": "明微抱元经",
      "resource_path": "res://resources/gongfa/明微抱元经.tres",
      "description": "通用主修根本法。以自身来源的照痕定标、定影锁魂、见隙破抗、烛微收束为主的神魂攻杀法门。",
      "wuxing": "ALL",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": false,
      "bearing_compatibility": [
        3
      ],
      "school_tags": [
        "soul"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "zhao_xing",
          "chapter_name": "照形篇",
          "description": "先照其形，再察其魂。以神魂攻势在目标身上留下自身来源的照痕，为后续返照、定影与烛微收束奠基。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "soul": 5,
            "intelligence": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            3
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {
            "cultivation_rate": 0.15,
            "mind_regen_rate": 0.15
          },
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/明微抱元经/抱元观微.tres",
            "res://resources/activeskills/明微抱元经/照形.tres",
            "res://resources/activeskills/明微抱元经/留痕.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "fan_zhao",
          "chapter_name": "返照篇",
          "description": "返其来势，反震其魂。趁自身施加的照痕未散，转守为攻。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "soul": 7,
            "intelligence": 5
          },
          "required_chapter_ids": [
            "zhao_xing"
          ],
          "allowed_bearings": [
            3
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {
            "cultivation_rate": 0.15,
            "mana_regen_rate": 0.15
          },
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "ding_ying",
          "chapter_name": "定影篇",
          "description": "三痕既成，形影可定。以锁魂窗口放大神魂攻杀。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "soul": 9,
            "intelligence": 7
          },
          "required_chapter_ids": [
            "fan_zhao"
          ],
          "allowed_bearings": [
            3
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {
            "cultivation_rate": 0.18,
            "mind_regen_rate": 0.18
          },
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "zhu_wei",
          "chapter_name": "烛微篇",
          "description": "烛照幽微，直指神魂。定影之后破其魂隙，以高额神魂伤害收束。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "soul": 10,
            "intelligence": 9
          },
          "required_chapter_ids": [
            "ding_ying"
          ],
          "allowed_bearings": [
            3
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {
            "cultivation_rate": 0.22
          },
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_dfba1bac",
      "projectSource": true,
      "gongfa_name": "内景损益篇",
      "resource_path": "res://resources/gongfa/内景损益篇.tres",
      "description": "以伤势业障为代价并从中取得持续物理增伤的锻躯法门。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.1,
        "sinew": 0.05,
        "bone": 0.1,
        "blood": 0.15,
        "viscera": 0.35,
        "heart": 0.25
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "neijing",
          "chapter_name": "内景",
          "description": "读取牌堆伤势，以生命上限代价换取锻躯物理增伤。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/内景损益篇/内景损益.tres",
            "res://resources/activeskills/内景损益篇/淬厉.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "lubo",
          "chapter_name": "履薄",
          "description": "在受创中持续向弃牌堆积累伤势。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "neijing"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "sunyi",
          "chapter_name": "损益",
          "description": "统一读取伤势数量，不拆分新的伤势资源。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "lubo"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "baihai",
          "chapter_name": "百骸",
          "description": "按牌堆伤势数追加段数的多段攻伐。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "sunyi"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_defab905",
      "projectSource": true,
      "gongfa_name": "市井游身功",
      "resource_path": "res://resources/gongfa/市井游身功.tres",
      "description": "只能辅修的凡人身法。以翻滚换位、贴身追打、阴手干扰和受击脱身组成完整循环。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 32,
      "required_attributes": {
        "agility": 3,
        "body_proficiency": 2
      },
      "can_be_main": false,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "movement",
        "fist"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {
        "agility": 0.5,
        "body_proficiency": 0.3,
        "precision": "2.0.0"
      },
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "hua_bu",
          "chapter_name": "滑步卷",
          "description": "练习低身翻滚与快速换位，先让脚下活起来。",
          "order": 1,
          "required_study_progress": 32,
          "required_realm": "",
          "required_attributes": {
            "agility": "3.0.0",
            "body_proficiency": "2.0.0",
            "initiative_speed": 2
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "initiative_speed": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/市井游身功/滚地翻.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "jie_li",
          "chapter_name": "借力卷",
          "description": "趁换位贴身时顶肘追打，把身法转成攻势。",
          "order": 2,
          "required_study_progress": 40,
          "required_realm": "",
          "required_attributes": {
            "agility": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [
            "hua_bu"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.25,
            "body_proficiency": 0.15
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 2,
            "critical": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/市井游身功/顶心肘.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/市井游身功/顶心肘.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "yin_shou",
          "chapter_name": "阴手卷",
          "description": "不与敌人正面硬拼，用石灰干扰视线与出手节奏。",
          "order": 3,
          "required_study_progress": 48,
          "required_realm": "",
          "required_attributes": {
            "agility": 7,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "jie_li"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.25,
            "body_proficiency": 0.15
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 2,
            "dodge": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/市井游身功/撒石灰.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/市井游身功/撒石灰.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "tuo_shen",
          "chapter_name": "脱身卷",
          "description": "受到攻击时扭身卸力，保住站位后再寻找进身机会。",
          "order": 4,
          "required_study_progress": 56,
          "required_realm": "",
          "required_attributes": {
            "agility": 9,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "yin_shou"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.25,
            "body_proficiency": 0.15
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "dodge": 2,
            "initiative_speed": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/市井游身功/泥鳅身.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/市井游身功/泥鳅身.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_02b0bfa4",
      "projectSource": true,
      "gongfa_name": "太祖长拳",
      "resource_path": "res://resources/gongfa/太祖长拳.tres",
      "description": "只能辅修的凡人拳法。从起手、进身、桩功到架势，逐卷建立近身攻守。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 24,
      "required_attributes": {
        "physique": 2,
        "body_proficiency": 1
      },
      "can_be_main": false,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "fist"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {
        "body_proficiency": 0.5,
        "physique": 0.5
      },
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "qi_shou",
          "chapter_name": "起手卷",
          "description": "从起手与站桩学起，建立拳架与近身攻击。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "body_proficiency": 1,
            "physique": 2
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "physical_defense": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/太祖长拳/太祖长拳.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/太祖长拳/太祖长拳.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "jin_shen",
          "chapter_name": "进身卷",
          "description": "以步带拳，消耗拳架后打出更重的崩劲。",
          "order": 2,
          "required_study_progress": 32,
          "required_realm": "",
          "required_attributes": {
            "physique": 4,
            "body_proficiency": 3
          },
          "required_chapter_ids": [
            "qi_shou"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.25,
            "body_proficiency": 0.25
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "body_proficiency": 0.5,
            "precision": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/太祖长拳/进步崩拳.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/太祖长拳/进步崩拳.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "zhuang_gong",
          "chapter_name": "桩功卷",
          "description": "沉肩稳架，将成功化解的攻势转为下一击的拳架。",
          "order": 3,
          "required_study_progress": 40,
          "required_realm": "",
          "required_attributes": {
            "physique": 6,
            "body_proficiency": 5
          },
          "required_chapter_ids": [
            "jin_shen"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.25,
            "body_proficiency": 0.25
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "physical_defense": 1,
            "crit_resist": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/太祖长拳/架梁.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/太祖长拳/架梁.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "jia_shi",
          "chapter_name": "架势卷",
          "description": "沉桥压身，将基础重击转为消耗拳架的终结招式。",
          "order": 4,
          "required_study_progress": 48,
          "required_realm": "",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "zhuang_gong"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.25,
            "body_proficiency": 0.25
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "body_proficiency": 0.5
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/太祖长拳/沉桥压身.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "REPLACE",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "撼山",
              "result_skill_path": "res://resources/activeskills/太祖长拳/沉桥压身.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_22bfcc84",
      "projectSource": true,
      "gongfa_name": "听锋寻隙篇",
      "resource_path": "res://resources/gongfa/听锋寻隙篇.tres",
      "description": "仅可辅修的识破剑诀。负责施加第一层剑痕，并围绕带痕目标提高命中和恢复精力。",
      "wuxing": "ALL",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 56,
      "required_attributes": {
        "soul": 5,
        "sword_proficiency": 8
      },
      "can_be_main": false,
      "can_be_sub": true,
      "bearing_compatibility": [
        3
      ],
      "school_tags": [
        "sword"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "ting_feng",
          "chapter_name": "听锋卷",
          "description": "为指定敌人施加第一层剑痕，并提高下一次指定剑术攻击的精准。",
          "order": 1,
          "required_study_progress": 56,
          "required_realm": "",
          "required_attributes": {
            "soul": 5,
            "sword_proficiency": 8
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 4
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/听锋寻隙篇/听锋辨隙.tres",
            "res://resources/activeskills/听锋寻隙篇/循隙一刺.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/听锋寻隙篇/听锋辨隙.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "guan_xi",
          "chapter_name": "观隙卷",
          "description": "根据场上带有剑痕的敌人补充剑意和精力。",
          "order": 2,
          "required_study_progress": 84,
          "required_realm": "",
          "required_attributes": {
            "soul": 8,
            "sword_proficiency": 12
          },
          "required_chapter_ids": [
            "ting_feng",
            "4",
            "7"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 3,
            "critical": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [
            {
              "enabled": true,
              "mode": "ADD",
              "count": 1,
              "source_skill_path": "",
              "source_skill_name": "",
              "result_skill_path": "res://resources/activeskills/听锋寻隙篇/观痕养意.tres",
              "main_result_skill_path": ""
            }
          ],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_e513b04c",
      "projectSource": true,
      "gongfa_name": "阴符应物经",
      "resource_path": "res://resources/gongfa/阴符应物经.tres",
      "description": "锻躯主修法。以应对牌抵消当前物理伤害，将敌势转为借力后收束反制。",
      "wuxing": "NONE",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 8,
      "required_attributes": {},
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1
      ],
      "school_tags": [
        "body"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "凡人",
      "main_realm_end": "云息十三层",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {},
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {
        "skin": 0.3,
        "sinew": 0.15,
        "bone": 0.25,
        "blood": 0.05,
        "viscera": 0.15,
        "heart": 0.1
      },
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "cheng_ji",
          "chapter_name": "承机",
          "description": "承敌之机，令化解成为借力入口。",
          "order": 1,
          "required_study_progress": 24,
          "required_realm": "",
          "required_attributes": {
            "physique": 5,
            "body_proficiency": 4
          },
          "required_chapter_ids": [],
          "allowed_bearings": [
            0,
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 30
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/阴符应物经/阴符应物.tres",
            "res://resources/activeskills/阴符应物经/承机.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "xie_shi",
          "chapter_name": "挟势",
          "description": "借势而不竭势，保留层数给后续收束。",
          "order": 2,
          "required_study_progress": 48,
          "required_realm": "云息三层",
          "required_attributes": {
            "physique": 6,
            "body_proficiency": 6
          },
          "required_chapter_ids": [
            "cheng_ji"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.5,
            "body_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "max_health": 20
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/阴符应物经/挟势.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "ying_wu",
          "chapter_name": "应物",
          "description": "使基础卸力也承接敌势，稳定积蓄借力。",
          "order": 3,
          "required_study_progress": 84,
          "required_realm": "云息六层",
          "required_attributes": {
            "physique": 8,
            "body_proficiency": 7
          },
          "required_chapter_ids": [
            "xie_shi"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "physique": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "he_jin",
          "chapter_name": "合劲",
          "description": "合众势为一击，完成借力体系的终结反制。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "云息九层",
          "required_attributes": {
            "physique": 10,
            "body_proficiency": 9
          },
          "required_chapter_ids": [
            "ying_wu"
          ],
          "allowed_bearings": [
            1
          ],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "body_proficiency": 1
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {},
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://resources/activeskills/阴符应物经/合劲.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    },
    {
      "schemaVersion": 1,
      "kind": "manual",
      "id": "project_manual_991cba61",
      "projectSource": true,
      "gongfa_name": "折枝剑谱",
      "resource_path": "res://resources/gongfa/折枝剑谱.tres",
      "description": "可主修也可辅修的伤势剑谱。以剑痕起手，折痕成伤，再将外伤与折损转化为斩杀。",
      "wuxing": "ALL",
      "wuxing_requirement_mode": "OR",
      "required_realm": "",
      "required_study_progress": 64,
      "required_attributes": {
        "agility": 8,
        "sword_proficiency": 10
      },
      "can_be_main": true,
      "can_be_sub": true,
      "bearing_compatibility": [
        1,
        2
      ],
      "school_tags": [
        "sword"
      ],
      "cross_bearing_policy": "NONE",
      "main_realm_start": "",
      "main_realm_end": "",
      "sub_realm_start": "",
      "sub_realm_end": "",
      "learn_permanent_bonuses": {},
      "learn_permanent_multipliers": {},
      "passive_bonuses": {
        "agility": 0.5,
        "sword_proficiency": 0.5
      },
      "passive_multipliers": {},
      "body_nourish_rate_per_day": 0.8,
      "body_nourish_weights": {},
      "default_unlocked_chapter_count": 1,
      "granted_active_skills": [],
      "heart_method_skill": "",
      "sub_heart_method_skill": "",
      "main_heart_method_skill": "",
      "general_breakthrough_multipliers": {},
      "general_breakthrough_fixed": {},
      "chapters": [
        {
          "chapter_id": "ya_zhi",
          "chapter_name": "压枝卷",
          "description": "从点痕、压枝与起势开始，建立折枝剑谱的第一层伤势循环。",
          "order": 1,
          "required_study_progress": 64,
          "required_realm": "",
          "required_attributes": {
            "agility": "8.0.0",
            "sword_proficiency": "10.0.0",
            "precision": 2,
            "critical": 1
          },
          "required_chapter_ids": [],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {},
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 2,
            "critical": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "fu_zhi",
          "chapter_name": "拂枝卷",
          "description": "学会借剑痕养锋，并让轻剑招式稳定制造剑痕与压制。",
          "order": 2,
          "required_study_progress": 84,
          "required_realm": "",
          "required_attributes": {
            "agility": 10,
            "sword_proficiency": 12
          },
          "required_chapter_ids": [
            "ya_zhi"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.5,
            "sword_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "critical": 2,
            "initiative_speed": 2
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/折枝剑谱/拂枝点痕.tres",
            "res://resources/activeskills/折枝剑谱/照痕养锋.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "lie_ke",
          "chapter_name": "裂柯卷",
          "description": "将应对与斩击串联起来，进一步提高折痕后的爆发与衔接能力。",
          "order": 3,
          "required_study_progress": 104,
          "required_realm": "",
          "required_attributes": {
            "agility": 12,
            "physique": 8,
            "sword_proficiency": 14
          },
          "required_chapter_ids": [
            "fu_zhi"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.5,
            "sword_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "precision": 2,
            "physical_defense": 1
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/折枝剑谱/裂柯.tres",
            "res://resources/activeskills/折枝剑谱/回锋添痕.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        },
        {
          "chapter_id": "cui_zhi",
          "chapter_name": "摧枝卷",
          "description": "掌握折枝终卷，将积累的剑痕、外伤与折损转化为真正的斩杀。",
          "order": 4,
          "required_study_progress": 128,
          "required_realm": "",
          "required_attributes": {
            "agility": 14,
            "physique": 10,
            "sword_proficiency": 16
          },
          "required_chapter_ids": [
            "lie_ke"
          ],
          "allowed_bearings": [],
          "requires_main_slot": false,
          "left_branch_name": "淬炼",
          "right_branch_name": "化法",
          "learn_permanent_bonuses": {
            "agility": 0.5,
            "physique": 0.5,
            "sword_proficiency": 0.5
          },
          "learn_permanent_multipliers": {},
          "naqi_bonuses": {
            "critical": 3,
            "crit_damage_mult": 0.04
          },
          "naqi_multipliers": {},
          "main_naqi_bonuses": {},
          "main_naqi_multipliers": {},
          "learned_active_skills": [
            "res://csharp/gongfa/ActiveSkillData.cs",
            "res://resources/activeskills/折枝剑谱/挑枝断筋.tres",
            "res://resources/activeskills/折枝剑谱/摧枝一剑.tres"
          ],
          "main_learned_active_skills": [],
          "sub_deck_rewrites": [],
          "main_deck_rewrites": []
        }
      ]
    }
  ],
  "realms": [
    {
      "name": "凡人",
      "required_attributes": {},
      "battle_base_attributes": {
        "cards_per_turn": 2,
        "initial_hand_size": 4
      },
      "realm_multiplier_bonuses": {},
      "realm_fixed_bonuses": {},
      "requires_main_gongfa": false,
      "is_mortal": true
    },
    {
      "name": "云息一层",
      "required_attributes": {
        "spirit_strands": 12
      },
      "battle_base_attributes": {
        "cards_per_turn": 2,
        "initial_hand_size": 4
      },
      "realm_multiplier_bonuses": {
        "agility": 0.05,
        "intelligence": 0.05,
        "physique": 0.05,
        "soul": 0.05
      },
      "realm_fixed_bonuses": {
        "max_age": 10,
        "physique": 1
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息二层",
      "required_attributes": {
        "spirit_strands": 24
      },
      "battle_base_attributes": {
        "cards_per_turn": 2,
        "initial_hand_size": 4
      },
      "realm_multiplier_bonuses": {
        "agility": 0.05,
        "intelligence": 0.05,
        "physique": 0.05,
        "soul": 0.05
      },
      "realm_fixed_bonuses": {
        "max_age": 5,
        "physique": 2
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息三层",
      "required_attributes": {
        "spirit_strands": 36
      },
      "battle_base_attributes": {
        "cards_per_turn": 3,
        "initial_hand_size": 5
      },
      "realm_multiplier_bonuses": {
        "agility": 0.08,
        "comprehension": 0.05,
        "cultivation_rate": 0.1,
        "intelligence": 0.08,
        "physique": 0.08,
        "soul": 0.08
      },
      "realm_fixed_bonuses": {
        "max_age": 5,
        "physique": 2
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息四层",
      "required_attributes": {
        "spirit_strands": 72
      },
      "battle_base_attributes": {
        "cards_per_turn": 3,
        "initial_hand_size": 5
      },
      "realm_multiplier_bonuses": {
        "agility": 0.1,
        "comprehension": 0.05,
        "intelligence": 0.1,
        "physique": 0.1,
        "soul": 0.1
      },
      "realm_fixed_bonuses": {
        "agility": 1,
        "intelligence": 1,
        "max_age": 10,
        "physique": 3,
        "soul": 1
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息五层",
      "required_attributes": {
        "spirit_strands": 108
      },
      "battle_base_attributes": {
        "cards_per_turn": 3,
        "initial_hand_size": 5
      },
      "realm_multiplier_bonuses": {
        "agility": 0.12,
        "comprehension": 0.08,
        "cultivation_rate": 0.1,
        "intelligence": 0.12,
        "physique": 0.12,
        "soul": 0.12
      },
      "realm_fixed_bonuses": {
        "agility": 1,
        "intelligence": 2,
        "max_age": 10,
        "physique": 3,
        "soul": 2
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息六层",
      "required_attributes": {
        "spirit_strands": 144
      },
      "battle_base_attributes": {
        "cards_per_turn": 3,
        "initial_hand_size": 5
      },
      "realm_multiplier_bonuses": {
        "agility": 0.15,
        "comprehension": 0.1,
        "fortune": 0.05,
        "intelligence": 0.15,
        "physique": 0.15,
        "soul": 0.15
      },
      "realm_fixed_bonuses": {
        "agility": 2,
        "comprehension": 1,
        "intelligence": 2,
        "max_age": 15,
        "physique": 4,
        "soul": 2,
        "sub_slot_increase": 1
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    },
    {
      "name": "云息七层",
      "required_attributes": {
        "spirit_strands": 159
      },
      "battle_base_attributes": {
        "cards_per_turn": 3,
        "initial_hand_size": 5
      },
      "realm_multiplier_bonuses": {
        "agility": 0.08,
        "intelligence": 0.1,
        "physique": 0.1,
        "soul": 0.1
      },
      "realm_fixed_bonuses": {
        "comprehension": 1,
        "max_age": 20,
        "physique": 2,
        "soul": 2
      },
      "requires_main_gongfa": false,
      "is_mortal": false
    }
  ]
};
