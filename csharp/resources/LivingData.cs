using Godot;
using Godot.Collections;

/// <summary>人类、野兽等有生命实体共享的属性与战斗数据。</summary>
public partial class LivingData : EntityData
{
    [Export] public float base_physique = 5.0f;
    [Export] public float base_intelligence = 5.0f;
    // v11 only: loaded once to migrate existing v10 saves; never used in gameplay.
    [Export] public float base_agility;
    [Export] public float base_soul = 5.0f;
    [Export] public float base_fortune = 50.0f;
    [Export] public float base_comprehension = 50.0f;
    [Export] public float base_charisma = 50.0f;
    [Export] public float base_social = 50.0f;
    [Export] public float base_management = 50.0f;
    [Export] public float physique = 5.0f;
    [Export] public float intelligence = 5.0f;
    // v11 only: loaded once to migrate existing v10 saves; never used in gameplay.
    [Export] public float agility;
    [Export] public float soul = 5.0f;
    [Export] public float fortune = 50.0f;
    [Export] public float comprehension = 50.0f;
    [Export] public float social = 50.0f;
    [Export] public float management = 50.0f;
    [Export] public int spirit_strands;
    [Export] public int current_age = 22;
    [Export] public int max_age = 80;
    /// <summary>Absolute game-hour cursor used to preserve partial years and save/load continuity.</summary>
    [Export] public long age_last_updated_game_hour = -1;
    [Export] public string cultivation_realm = "凡人";
    [Export] public int current_health = 100;
    [Export] public int max_health = 100;
    [Export] public int current_stamina = 100;
    [Export] public int max_stamina = 100;
    [Export] public int used_spirit_perception;
    [Export] public int max_spirit_perception;
    [Export] public int current_mind = 100;
    [Export] public int max_mind = 100;
    [Export] public int current_mana;
    [Export] public int max_mana;
    [Export] public float health_regen_rate = 0.5f;
    [Export] public float stamina_regen_rate = 0.8f;
    [Export] public float mind_regen_rate = 0.3f;
    [Export] public int cards_per_turn = 5;
    [Export] public int initial_hand_size = 5;
    [Export] public int physical_defense;
    /// <summary>肉身节点提供的物理最终减伤，按 0—1 比例保存。</summary>
    [Export] public float physical_damage_reduction;
    /// <summary>心部位节点提供的受疗加成，按 0—1 比例保存。</summary>
    [Export] public float healing_received_bonus;
    /// <summary>血部位节点提供的气血层数上限加成。</summary>
    [Export] public int body_qi_blood_bonus;
    [Export] public int precision;
    [Export] public int dodge;
    [Export] public int critical;
    [Export] public int crit_resist;
    [Export] public float crit_damage_mult = 1.5f;
    [Export] public float learning_speed = 1.0f;
    [Export] public float cultivation_speed = 1.0f;
    // 旧存档兼容字段；即时战斗不再用它计算抽牌或技能槽。
    [Export] public float initiative_speed = 100.0f;
    [Export] public Array<Resource> active_skills = [];
    [Export] public bool show_battle_bars = true;
    [Export] public bool data_initialized;
}
