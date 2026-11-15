using Godot;

/// <summary>功法化法阶段对基础技能或本秘籍授予技能的单条改写规则。</summary>
public partial class GongfaDeckRewriteConfig : Resource
{
    public enum RewriteMode { ADD, REPLACE }
    public enum RewriteTargetScope { BASIC_SKILL, OWN_GONGFA_SKILL }

    [Export] public bool enabled = true;
    [Export] public RewriteMode mode = RewriteMode.ADD;
    // 存量资源未序列化此字段时默认保持原基础技能目标。
    [Export] public RewriteTargetScope target_scope = RewriteTargetScope.BASIC_SKILL;
    /// <summary>旧基础牌库的复制数量；即时战斗技能按技能授予，不复制成多份。</summary>
    [Export] public int count = 1;
    [ExportGroup("Match")]
    [Export] public Resource? source_skill;
    [Export] public string source_skill_name = "";
    [ExportGroup("Result")]
    [Export] public Resource? result_skill;
    /// <summary>旧主修专属结果保留存档兼容；当前招式改写使用同一结果。</summary>
    [Export] public Resource? main_result_skill;

    [ExportGroup("招式盘化法")]
    [Export] public int board_definition_version;
    [Export] public Vector2I board_size = Vector2I.One;
    [Export] public string board_icon_path = "";
    /// <summary>0为未定，不解释为无限；不从旧牌库count生成化法份数。</summary>
    [Export] public int board_max_placements;

    public Resource? get_result_skill(bool is_main) => result_skill;

    public bool matches(Resource? skill)
    {
        if (skill is null) return false;
        if (source_skill is not null && ((source_skill.ResourcePath.Length > 0 && skill.ResourcePath == source_skill.ResourcePath) || skill == source_skill)) return true;
        return source_skill_name.Length > 0 && skill.Get("skill_name").AsString() == source_skill_name;
    }
}
