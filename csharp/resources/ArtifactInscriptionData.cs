using Godot;
using Godot.Collections;

/// <summary>法宝实例上的单条铭文；这里只保存数据，不在行囊层解释战斗效果。</summary>
public partial class ArtifactInscriptionData : Resource
{
    [Export] public string inscription_id = "";
    [Export] public string display_name = "";
    [Export] public Dictionary modifiers = [];
}
