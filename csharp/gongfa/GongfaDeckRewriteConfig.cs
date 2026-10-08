using Godot;

/// <summary>功法化法阶段对基础牌组的单条改写规则。</summary>
public partial class GongfaDeckRewriteConfig : Resource
{
    public enum RewriteMode { ADD, REPLACE }

    [Export] public bool enabled = true;
    [Export] public RewriteMode mode = RewriteMode.ADD;
    [Export] public int count = 1;
    [ExportGroup("Match")]
    [Export] public Resource? source_skill;
    [Export] public string source_skill_name = "";
    [ExportGroup("Result")]
    [Export] public Resource? result_skill;
    [Export] public Resource? main_result_skill;

    public Resource? get_result_skill(bool is_main) => is_main && main_result_skill is not null ? main_result_skill : result_skill;

    public bool matches(Resource? skill)
    {
        if (skill is null) return false;
        if (source_skill is not null && ((source_skill.ResourcePath.Length > 0 && skill.ResourcePath == source_skill.ResourcePath) || skill == source_skill)) return true;
        return source_skill_name.Length > 0 && skill.Get("skill_name").AsString() == source_skill_name;
    }
}
