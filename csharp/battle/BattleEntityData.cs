using Godot;
using Godot.Collections;

/// <summary>
/// Serializable battle snapshot.  It is deliberately independent from a scene
/// node so the same data can cross the world/battle scene boundary safely.
/// </summary>
public partial class BattleEntityData : Resource
{
    [Export] public Godot.Collections.Array<CombatSkillReference> combat_skill_references = [];
    [Export] public Godot.Collections.Array<ArtifactInstanceData> equipped_artifacts = [];
    [Export] public Godot.Collections.Array<ActiveSkillData> combat_resolved_skills = [];
    [Export] public string uid = "";
    [Export] public EntityData? portrait_data;
    [Export] public int battle_external_wounds, battle_internal_wounds;
    [Export] public float dun_speed;
    [Export] public float social, management;
    [Export] public Array<Dictionary> battle_loot = [];
    [Export] public string entity_name = "未命名";
    [Export] public int entity_type;
    [Export] public int faction;
    [Export] public string source_scene_path = "";
    [Export] public int stack_count = 1;
    [Export] public int max_stack = 999;
    [Export] public bool face_camera;
    [Export] public string battle_portrait_path = "";
    [Export] public int appearance_seed;
    [Export] public int appearance_face_style = -1;
    [Export] public int appearance_eyebrow_style = -1;
    [Export] public int appearance_eye_style = -1;
    [Export] public int appearance_nose_style = -1;
    [Export] public int appearance_mouth_style = -1;
    [Export] public int appearance_skin_tone = -1;
    [Export] public int appearance_hair_color = -1;
    [Export] public int appearance_hair_style = -1;
    [Export] public int appearance_clothes_style = -1;
    [Export] public int appearance_accessory_style = -1;
    [Export] public int current_age;
    [Export] public int max_age;
    [Export] public float pill_toxicity;
    [Export] public string cultivation_realm = "";
    [Export] public int spirit_root_type;
    [Export] public string spirit_root_name = "";
    // 四维保留一位小数，确保妖兽的随机、变种与精英修正不会在战斗快照中丢失。
    [Export] public float physique;
    [Export] public float intelligence;
    [Export] public float agility;
    [Export] public float soul;
    [Export] public int fortune;
    [Export] public int comprehension;
    [Export] public int spirit_strands;
    [Export] public int core_count;
    /// <summary>旧快照兼容字段；新战斗不读取或写入。</summary>
    [Export] public int dao_bearing;
    [Export] public float body_proficiency;
    [Export] public float sword_proficiency;
    [Export] public float magic_proficiency;
    [Export] public float formation_proficiency;
    /// <summary>历史分类快照兼容字段；新战斗不读取它。</summary>
    [Export] public bool body_path_awakened;
    /// <summary>历史修炼分类快照兼容字段；不参与战斗行为。</summary>
    [Export] public int energy_lineage;
    [Export] public float mana_regen_rate = 0.04f;
    [Export] public int lifespan_bonus;
    [Export] public int auxiliary_slots = 2;
    [Export] public float charisma = 50.0f;
    [Export] public int gender;
    [Export] public int current_health;
    [Export] public int max_health;
    [Export] public int current_stamina;
    [Export] public int max_stamina;
    [Export] public int used_spirit_perception;
    [Export] public int max_spirit_perception;
    [Export] public int current_mind;
    [Export] public int max_mind;
    [Export] public int current_mana;
    [Export] public int max_mana;
    [Export] public float health_regen_rate = 0.5f;
    [Export] public float stamina_regen_rate = 0.8f;
    [Export] public float mind_regen_rate = 0.3f;
    [Export] public int cards_per_turn = 5;
    [Export] public int initial_hand_size = 5;
    [Export] public int physical_defense;
    [Export] public float physical_damage_reduction;
    [Export] public float healing_received_bonus;
    [Export] public int body_qi_blood_bonus;
    [Export] public int precision;
    [Export] public int dodge;
    [Export] public int critical;
    [Export] public int crit_resist;
    /// <summary>Temporary opening shield from a player-reserved cultivation inspiration.</summary>
    [Export] public int cultivation_inspiration_starting_shield;
    [Export] public float crit_damage_mult = 1.5f;
    [Export] public float initiative_speed = 100.0f;
    [Export] public float learning_speed = 1.0f;
    [Export] public float cultivation_speed = 1.0f;
    [Export] public Array<string> gongfa_paths = [];
    [Export] public string main_gongfa_path = "";
    [Export] public Array<string> sub_gongfa_paths = [];
    [Export] public string basic_deck_path = "";
    [Export] public Array<string> basic_deck_upgrade_ids = [];
    [Export] public Dictionary equipment_paths = [];
    [Export] public Array<string> active_skill_paths = [];
    [Export] public Array<string> loaded_skill_paths = [];
    /// <summary>即时战斗固定技能槽；凝核牌局仍使用独立的卡牌循环。</summary>
    [Export] public Array<string> combat_skill_slot_paths = [];
    [Export] public bool combat_skill_slots_configured;
    [Export] public Array<string> combat_passive_paths = [];
    /// <summary>已研读并随当前主修/辅修生效的心法卷章追加效果，键为秘籍名|效果ID。</summary>
    [Export] public Dictionary combat_heart_method_upgrades = [];
    /// <summary>按心法归属汇总的修持值，仅在战斗快照中使用。</summary>
    [Export] public Dictionary combat_tempering_by_gongfa = [];
    [Export] public bool battle_deck_configured;
    [Export] public Array<string> pack_item_paths = [];
    [Export] public Array<int> pack_item_counts = [];
    /// <summary>角色在局外已布置并通过校验的个人战斗阵图；进入战斗时复制为运行时状态。</summary>
    [Export] public Array<FormationCircuitData> formation_circuits = [];
    [Export] public Array<string> formation_manual_ids = [];

    public void copy_from(BattleEntityData source)
    {
        foreach (var property in GetPropertyList())
        {
            var name = property["name"].AsStringName();
            if (name != "resource_path" && name != "resource_name") Set(name, source.Get(name));
        }
        portrait_data = source.portrait_data?.Duplicate(true) as EntityData;
        battle_loot = source.battle_loot.Duplicate(true);
        equipment_paths = source.equipment_paths.Duplicate();
        gongfa_paths = source.gongfa_paths.Duplicate();
        sub_gongfa_paths = source.sub_gongfa_paths.Duplicate();
        basic_deck_upgrade_ids = source.basic_deck_upgrade_ids.Duplicate();
        active_skill_paths = source.active_skill_paths.Duplicate();
        loaded_skill_paths = source.loaded_skill_paths.Duplicate();
        combat_skill_slot_paths = source.combat_skill_slot_paths.Duplicate();
        combat_skill_references = source.combat_skill_references.Duplicate(true);
        equipped_artifacts = source.equipped_artifacts.Duplicate(true);
        combat_resolved_skills = source.combat_resolved_skills.Duplicate(true);
        combat_passive_paths = source.combat_passive_paths.Duplicate();
        combat_heart_method_upgrades = source.combat_heart_method_upgrades.Duplicate(true);
        combat_tempering_by_gongfa = source.combat_tempering_by_gongfa.Duplicate(true);
        pack_item_paths = source.pack_item_paths.Duplicate();
        pack_item_counts = source.pack_item_counts.Duplicate();
        formation_circuits = source.formation_circuits.Duplicate();
        formation_manual_ids = source.formation_manual_ids.Duplicate();
    }
}
