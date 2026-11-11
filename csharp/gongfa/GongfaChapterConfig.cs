using Godot;
using Godot.Collections;

/// <summary>功法卷章的学习门槛、属性分支、已学技能/大世界术法和化法技能改写。</summary>
public partial class GongfaChapterConfig : Resource
{
    [Export] public string chapter_id = "";
    [Export] public string chapter_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public int order = 1;
    [Export] public float required_study_progress = 8.0f;
    [ExportGroup("Learning Requirements")]
    [Export] public string required_realm = "";
    [Export] public Dictionary required_attributes = [];
    [Export] public Array<string> required_chapter_ids = [];
    [Export] public bool requires_main_slot;
    [ExportGroup("卷章效果栏目")]
    /// <summary>旧字段名保留；仅控制栏目标题，不再表示互斥的分支选择。</summary>
    [Export] public string left_branch_name = "修持";
    /// <summary>旧字段名保留；仅控制栏目标题，不再表示互斥的分支选择。</summary>
    [Export] public string right_branch_name = "化法";
    [ExportGroup("Permanent Learn Bonuses")]
    [Export] public Dictionary learn_permanent_bonuses = [];
    /// <summary>永久奖励修订标记与修订前基准，用于已研读存档的一次性差额补发。</summary>
    [Export] public string learn_bonus_revision = "";
    [Export] public Dictionary previous_learn_permanent_bonuses = [];
    [Export] public Dictionary learn_permanent_multipliers = [];
    [ExportGroup("Learned Skills")]
    [Export] public Array<Resource> learned_active_skills = [];
    /// <summary>旧字段名保留存档兼容；当前和普通习得技能一样主辅修共用。</summary>
    [Export] public Array<Resource> main_learned_active_skills = [];
    [ExportGroup("Heart Method Progression")]
    /// <summary>本卷研读后解锁的机制标记；与左右分支选择无关。</summary>
    [Export] public string heart_method_upgrade_id = "";
    [Export(PropertyHint.MultilineText)] public string heart_method_upgrade_description = "";
    /// <summary>学至本卷时的完整心法描述，含主修额外效果；仅展示，不重复应用机制。</summary>
    [Export(PropertyHint.MultilineText)] public string heart_method_description = "";
    [ExportGroup("大世界术法")]
    /// <summary>研读本卷后永久解锁的大世界术法；与战斗技能分开存放。</summary>
    [Export] public Array<Resource> learned_world_spells = [];
    [ExportGroup("Naqi Bonuses")]
    [Export] public Dictionary naqi_bonuses = [];
    [Export] public Dictionary naqi_multipliers = [];
    /// <summary>旧字段名保留；当前纳气效果由主辅修共用。</summary>
    [Export] public Dictionary main_naqi_bonuses = [];
    /// <summary>旧字段名保留；当前纳气效果由主辅修共用。</summary>
    [Export] public Dictionary main_naqi_multipliers = [];
    [ExportGroup("Combat Tempering Bonuses")]
    /// <summary>修持的战斗内属性；通过战斗快照传递，不写入角色常驻属性。</summary>
    [Export] public Dictionary combat_tempering_bonuses = [];
    [ExportGroup("Huafa Rewrites")]
    [Export] public Array<GongfaDeckRewriteConfig> sub_deck_rewrites = [];
    /// <summary>旧主修字段名保留；当前与普通化法配置合并并由主辅修共用。</summary>
    [Export] public Array<GongfaDeckRewriteConfig> main_deck_rewrites = [];

    public string get_id() => chapter_id.Length > 0 ? chapter_id : chapter_name.Length > 0 ? chapter_name : order.ToString();
    public Array<GongfaDeckRewriteConfig> get_deck_rewrites(bool is_main)
    {
        // is_main 保留作调用兼容；旧主修／辅修配置现在一并生效。
        Array<GongfaDeckRewriteConfig> result = [];
        foreach (var item in sub_deck_rewrites) if (item is not null) result.Add(item);
        foreach (var item in main_deck_rewrites) if (item is not null) result.Add(item);
        return result;
    }
    public Array<Resource> get_learned_skills(bool is_main)
    {
        // is_main 保留作调用兼容；旧主修限定技能现在由主辅修共用。
        Array<Resource> result = [];
        foreach (var item in learned_active_skills) if (item is not null) result.Add(item);
        foreach (var item in main_learned_active_skills) if (item is not null) result.Add(item);
        return result;
    }
    public Dictionary get_naqi_bonuses(bool is_main) => merge(naqi_bonuses, main_naqi_bonuses);
    public Dictionary get_naqi_multipliers(bool is_main) => merge(naqi_multipliers, main_naqi_multipliers);
    public Dictionary get_combat_tempering_bonuses() => combat_tempering_bonuses.Duplicate();
    private static Dictionary merge(Dictionary baseValues, Dictionary? extra)
    {
        Dictionary result = baseValues.Duplicate();
        if (extra is not null) foreach (Variant key in extra.Keys) result[key] = result.GetValueOrDefault(key, 0.0f).AsSingle() + extra[key].AsSingle();
        return result;
    }
}
