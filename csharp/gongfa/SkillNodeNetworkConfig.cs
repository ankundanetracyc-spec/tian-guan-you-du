using Godot;
using Godot.Collections;

/// <summary>角色造诣图谱节点的可配置内容表。</summary>
[GlobalClass]
public partial class SkillNodeNetworkConfig : Resource
{
    public const string DefaultPath = "res://resources/skill_nodes/公共技能节点网.tres";

    [Export] public Array<Resource> nodes = [];

    public IEnumerable<SkillNodeConfig> get_nodes() => nodes.OfType<SkillNodeConfig>();
    public SkillNodeConfig? get_node(string nodeId) => get_nodes().FirstOrDefault(node => node.node_id == nodeId);

    public static SkillNodeNetworkConfig? load_default() => ResourceLoader.Load<SkillNodeNetworkConfig>(DefaultPath);
}
