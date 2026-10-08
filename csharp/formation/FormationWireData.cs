using Godot;

/// <summary>细脉纹只传递控制信号；阵源至阵器的灵络由阵器的供能许可单独表示。</summary>
public partial class FormationWireData : Resource
{
    [Export] public StringName from_node_id = new();
    [Export] public StringName to_node_id = new();
}
