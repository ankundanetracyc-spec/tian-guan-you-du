using Godot;
using Godot.Collections;

/// <summary>角色造诣图谱节点的可配置内容表。</summary>
[GlobalClass]
public partial class SkillNodeNetworkConfig : Resource
{
    public const string DefaultPath = "res://resources/skill_nodes/公共技能节点网.tres";

    [Export] public Array<Resource> nodes = [];
    [Export] public Array<Resource> faces = [];

    public IEnumerable<SkillNodeConfig> get_nodes() => nodes.OfType<SkillNodeConfig>();
    public IEnumerable<SkillNodeFaceConfig> get_faces() => faces.OfType<SkillNodeFaceConfig>();
    public SkillNodeConfig? get_node(string nodeId) => get_nodes().FirstOrDefault(node => node.node_id == nodeId);
    public SkillNodeFaceConfig? get_face(string faceId) => get_faces().FirstOrDefault(face => face.face_id == faceId);

    public static SkillNodeNetworkConfig? load_default() => ResourceLoader.Load<SkillNodeNetworkConfig>(DefaultPath);
}
