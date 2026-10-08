using Godot;
using Godot.Collections;

/// <summary>功法配置资源；字段名与既有 .tres 保持一致，确保存量内容无需重录。</summary>
public partial class GongfaResource : Resource
{
    public enum Wuxing
    {
        NONE, METAL, WOOD, WATER, FIRE, EARTH,
        METAL_WOOD, METAL_WATER, METAL_FIRE, METAL_EARTH,
        WOOD_WATER, WOOD_FIRE, WOOD_EARTH, WATER_FIRE, WATER_EARTH, FIRE_EARTH,
        METAL_WOOD_WATER, METAL_WOOD_FIRE, METAL_WOOD_EARTH, METAL_WATER_FIRE, METAL_WATER_EARTH,
        METAL_FIRE_EARTH, WOOD_WATER_FIRE, WOOD_WATER_EARTH, WOOD_FIRE_EARTH, WATER_FIRE_EARTH,
        METAL_WOOD_WATER_FIRE, METAL_WOOD_WATER_EARTH, METAL_WOOD_FIRE_EARTH, METAL_WATER_FIRE_EARTH,
        WOOD_WATER_FIRE_EARTH, METAL_WOOD_WATER_FIRE_EARTH, ALL
    }
    public enum WuxingRequirementMode { OR, AND }
    public enum DaoBearing { UNSET, BODY, QI, SOUL }
    public enum CrossBearingPolicy { NONE, LOW_TIER_ONLY, PERMANENT_LEARNED_ONLY, FULL }

    [ExportGroup("基础信息")]
    [Export] public string gongfa_name = "未命名秘籍";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Wuxing wuxing;
    [Export] public WuxingRequirementMode wuxing_requirement_mode;
    [ExportGroup("学习要求")]
    [Export] public string required_realm = "";
    [Export] public float required_study_progress = 8.0f;
    [Export] public Dictionary required_attributes = [];
    [ExportGroup("槽位配置")]
    [Export] public bool can_be_main = true;
    [Export] public bool can_be_sub = true;
    [Export] public Array<int> bearing_compatibility = [];
    [Export] public Array<string> school_tags = [];
    [Export] public CrossBearingPolicy cross_bearing_policy;
    [Export] public string main_realm_start = "";
    [Export] public string main_realm_end = "";
    [Export] public string sub_realm_start = "";
    [Export] public string sub_realm_end = "";
    [ExportGroup("永久加成·学会即得")]
    [Export] public Dictionary learn_permanent_bonuses = [];
    [Export] public Dictionary learn_permanent_multipliers = [];
    [ExportGroup("养形谱·锻躯")]
    /// <summary>正常吐纳每 24 小时提供的总养形炼度；具体分配由下方谱表决定。</summary>
    [Export] public float body_nourish_rate_per_day = 0.8f;
    /// <summary>部位稳定 ID到养形分配权重；为空时使用均衡默认谱。</summary>
    [Export] public Dictionary body_nourish_weights = [];
    [ExportGroup("装备加成·辅修临时")]
    [Export] public Dictionary passive_bonuses = [];
    [Export] public Dictionary passive_multipliers = [];
    [ExportGroup("授予卡牌")]
    [Export] public Array<ActiveSkillData> granted_active_skills = [];
    [Export] public ActiveSkillData? heart_method_skill;
    [Export] public ActiveSkillData? sub_heart_method_skill;
    [Export] public ActiveSkillData? main_heart_method_skill;
    [Export] public int default_unlocked_chapter_count = 1;
    [Export] public Array<GongfaChapterConfig> chapters = [];
    [ExportGroup("突破加成·主修进阶")]
    [Export] public Dictionary general_breakthrough_multipliers = [];
    [Export] public Dictionary general_breakthrough_fixed = [];
    [Export] public Array<BreakthroughBonusConfig> breakthrough_bonuses = [];

    public void sync_defaults() { }

    public Dictionary get_breakthrough_bonus_for_realm(string realm_name)
    {
        foreach (var config in breakthrough_bonuses)
            if (config is not null && config.realm == realm_name)
                return new Dictionary { ["multipliers"] = config.multipliers, ["fixed"] = config.@fixed };
        return new Dictionary { ["multipliers"] = general_breakthrough_multipliers, ["fixed"] = general_breakthrough_fixed };
    }

    public Array<string> get_default_unlocked_chapter_ids()
    {
        var result = new Array<string>();
        var sorted = get_sorted_chapters();
        for (var i = 0; i < Mathf.Clamp(default_unlocked_chapter_count, 0, sorted.Count); i++) result.Add(sorted[i].get_id());
        return result;
    }

    public Array<GongfaChapterConfig> get_sorted_chapters()
    {
        var sorted = new List<GongfaChapterConfig>();
        foreach (var chapter in chapters) if (chapter is not null) sorted.Add(chapter);
        sorted.Sort((left, right) => left.order.CompareTo(right.order));
        return new Array<GongfaChapterConfig>(sorted);
    }

    public GongfaChapterConfig? get_chapter_by_id(string chapter_id)
    {
        foreach (var chapter in chapters) if (chapter is not null && chapter.get_id() == chapter_id) return chapter;
        return null;
    }

    public ActiveSkillData? get_heart_method_skill(bool is_main) =>
        is_main && main_heart_method_skill is not null ? main_heart_method_skill :
        !is_main && sub_heart_method_skill is not null ? sub_heart_method_skill :
        heart_method_skill ?? (is_main ? sub_heart_method_skill : main_heart_method_skill);

    public bool is_bearing_compatible(DaoBearing bearing) =>
        bearing == DaoBearing.UNSET || (bearing_compatibility.Count == 0 ? !can_be_main : bearing_compatibility.Contains((int)bearing));

    public bool is_wuxing_compatible(int spirit_root_type)
    {
        if (wuxing == Wuxing.NONE) return true;
        if (wuxing == Wuxing.ALL) return spirit_root_type != 0;
        var required = wuxing.ToString().Split('_');
        var roots = ((Wuxing)spirit_root_type).ToString().Split('_');
        if (wuxing_requirement_mode == WuxingRequirementMode.AND)
            return required.All(element => roots.Contains(element));
        return required.Any(element => roots.Contains(element));
    }
}
