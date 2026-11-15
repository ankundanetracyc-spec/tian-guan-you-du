using Godot;

/// <summary>固定技能栏的持久来源；空引用保留位置，法器来源不伪造资源路径。</summary>
public partial class CombatSkillReference : Resource
{
    [Export] public string skill_path = "";
    [Export] public string artifact_instance_id = "";
    public string Key => artifact_instance_id.Length > 0 ? "artifact:" + artifact_instance_id : skill_path;
    public static CombatSkillReference FromKey(string key) => key.StartsWith("artifact:", StringComparison.Ordinal)
        ? new() { artifact_instance_id = key[9..] } : new() { skill_path = key };
}
