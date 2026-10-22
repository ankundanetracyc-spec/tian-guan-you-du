using Godot;
using Godot.Collections;

/// <summary>公共战斗技能节点；解锁只写入稳定节点 ID，技能数据仍由 ActiveSkillData 提供。</summary>
[GlobalClass]
public partial class SkillNodeConfig : Resource
{
    [Export] public string node_id = "";
    [Export] public string node_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public string skill_path = "";
    /// <summary>节点所属脉面；跨面关系由本节点与直接前置节点的脉面共同决定。</summary>
    [Export] public string face_id = "";
    [Export] public Array<string> prerequisite_node_ids = [];
    [Export] public Dictionary required_attributes = [];
    /// <summary>造诣图谱主体区内的节点中心位置；节点大小由前置数量决定。</summary>
    [Export] public Vector2 graph_position;
}
