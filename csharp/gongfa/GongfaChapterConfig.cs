using Godot;
using Godot.Collections;

/// <summary>功法卷章的学习门槛、常驻加成、纳气加成和牌组改写。</summary>
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
    /// <summary>旧资源兼容字段；用于展示修行倾向，不限制研读或其他养成线。</summary>
    [Export] public Array<int> allowed_bearings = [];
    [Export] public bool requires_main_slot;
    [ExportGroup("传承分支")]
    // 本作锻躯秘籍的左侧为淬炼；旧资源未序列化名称时也应正确显示。
    [Export] public string left_branch_name = "淬炼";
    [Export] public string right_branch_name = "化法";
    [ExportGroup("Permanent Learn Bonuses")]
    [Export] public Dictionary learn_permanent_bonuses = [];
    [Export] public Dictionary learn_permanent_multipliers = [];
    [ExportGroup("Learned Skills")]
    [Export] public Array<Resource> learned_active_skills = [];
    [Export] public Array<Resource> main_learned_active_skills = [];
    [ExportGroup("Naqi Bonuses")]
    [Export] public Dictionary naqi_bonuses = [];
    [Export] public Dictionary naqi_multipliers = [];
    [Export] public Dictionary main_naqi_bonuses = [];
    [Export] public Dictionary main_naqi_multipliers = [];
    [ExportGroup("Huafa Rewrites")]
    [Export] public Array<GongfaDeckRewriteConfig> sub_deck_rewrites = [];
    [Export] public Array<GongfaDeckRewriteConfig> main_deck_rewrites = [];

    public string get_id() => chapter_id.Length > 0 ? chapter_id : chapter_name.Length > 0 ? chapter_name : order.ToString();
    public Array<GongfaDeckRewriteConfig> get_deck_rewrites(bool is_main)
    {
        Array<GongfaDeckRewriteConfig> result = [];
        foreach (var item in sub_deck_rewrites) if (item is not null) result.Add(item);
        if (is_main) foreach (var item in main_deck_rewrites) if (item is not null) result.Add(item);
        return result;
    }
    public Array<Resource> get_learned_skills(bool is_main)
    {
        Array<Resource> result = [];
        foreach (var item in learned_active_skills) if (item is not null) result.Add(item);
        if (is_main) foreach (var item in main_learned_active_skills) if (item is not null) result.Add(item);
        return result;
    }
    public Dictionary get_naqi_bonuses(bool is_main) => merge(naqi_bonuses, is_main ? main_naqi_bonuses : null);
    public Dictionary get_naqi_multipliers(bool is_main) => merge(naqi_multipliers, is_main ? main_naqi_multipliers : null);
    private static Dictionary merge(Dictionary baseValues, Dictionary? extra)
    {
        Dictionary result = baseValues.Duplicate();
        if (extra is not null) foreach (Variant key in extra.Keys) result[key] = result.GetValueOrDefault(key, 0.0f).AsSingle() + extra[key].AsSingle();
        return result;
    }
}
