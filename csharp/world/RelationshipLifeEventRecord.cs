using Godot;

/// <summary>关系人离世等后续心境事件的事实接口；本记录本身不改变心境。</summary>
[GlobalClass]
public partial class RelationshipLifeEventRecord : Resource
{
    [Export] public string event_id = "";
    [Export] public string event_type = "";
    [Export] public string related_character_id = "";
    [Export] public int month_index = -1;
    [Export] public string cause = "";
}
