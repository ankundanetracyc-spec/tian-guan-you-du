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

    [ExportGroup("基础信息")]
    /// <summary>下架只关闭游戏入口与效果，原资源和存档研读记录保留。</summary>
    [Export] public bool gameplay_enabled = true;
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
    [Export] public Array<string> school_tags = [];
    [Export] public string main_realm_start = "";
    [Export] public string main_realm_end = "";
    [Export] public string sub_realm_start = "";
    [Export] public string sub_realm_end = "";
    [ExportGroup("永久加成·学会即得")]
    [Export] public Dictionary learn_permanent_bonuses = [];
    [Export] public Dictionary learn_permanent_multipliers = [];
    [ExportGroup("修命·养形谱")]
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

    public string get_heart_method_description(int chapterOrder, bool isMain)
    {
        var sorted = get_sorted_chapters();
        var chapter = sorted
            .Where(item => item.order <= chapterOrder && !string.IsNullOrWhiteSpace(item.heart_method_description))
            .LastOrDefault();
        var description = "";
        var visited = new HashSet<int>();
        while (chapter is not null && visited.Add(chapter.order))
        {
            var candidate = chapter.heart_method_description.Trim();
            var inheritedOrder = get_inherited_heart_method_order(candidate);
            if (inheritedOrder <= 0)
            {
                description = candidate;
                break;
            }

            // 秘籍图稿用“沿用第X卷”避免重复正文；运行时详情必须展开到被引用的完整版本。
            chapter = sorted.LastOrDefault(item => item.order == inheritedOrder && !string.IsNullOrWhiteSpace(item.heart_method_description));
        }
        if (string.IsNullOrWhiteSpace(description)) description = get_heart_method_skill(isMain)?.description ?? "";
        const string marker = "主修额外效果：";
        description = description.Replace("主修额外追加：", marker);
        var mainIndex = description.IndexOf(marker, StringComparison.Ordinal);
        if (mainIndex >= 0)
            description = isMain ? description[..mainIndex].Trim() + "\n" + description[mainIndex..].Trim()
                : description[..mainIndex].Trim();
        return description.Trim();
    }

    private static int get_inherited_heart_method_order(string description)
    {
        const string marker = "沿用第";
        var markerIndex = description.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return 0;
        var orderStart = markerIndex + marker.Length;
        var orderEnd = description.IndexOf('卷', orderStart);
        if (orderEnd <= orderStart) return 0;
        var token = description[orderStart..orderEnd].Trim();
        if (int.TryParse(token, out var arabicOrder)) return arabicOrder;

        var total = 0;
        var current = 0;
        foreach (var character in token)
        {
            var digit = character switch
            {
                '零' or '〇' => 0,
                '一' => 1,
                '二' or '两' => 2,
                '三' => 3,
                '四' => 4,
                '五' => 5,
                '六' => 6,
                '七' => 7,
                '八' => 8,
                '九' => 9,
                _ => -1
            };
            if (digit >= 0)
            {
                current = digit;
                continue;
            }
            if (character == '十')
            {
                total += (current == 0 ? 1 : current) * 10;
                current = 0;
                continue;
            }
            return 0;
        }
        return total + current;
    }

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
