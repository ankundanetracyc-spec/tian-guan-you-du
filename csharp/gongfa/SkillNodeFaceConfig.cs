using Godot;

/// <summary>造诣图谱中的可扩展脉面；面数不由代码固定。</summary>
[GlobalClass]
public partial class SkillNodeFaceConfig : Resource
{
    [Export] public string face_id = "";
    [Export] public string face_name = "";
    [Export] public int sort_order;
}
