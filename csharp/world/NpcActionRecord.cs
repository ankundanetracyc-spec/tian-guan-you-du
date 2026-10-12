using Godot;

/// <summary>一条可追溯的世界人物月度行动或死亡记录。</summary>
public partial class NpcActionRecord : Resource
{
    [Export] public int month_index;
    [Export] public string date_label = "";
    [Export] public string action_id = "";
    [Export] public string action_name = "";
    [Export] public string node_id = "";
    [Export] public string subscene_id = "";
    [Export] public string from_node_id = "";
    [Export] public string to_node_id = "";
    [Export] public int travel_days;
    [Export] public int travel_days_remaining;
    [Export] public string target_npc_id = "";
    /// <summary>Links battle-history entries to their single authoritative structured incident record.</summary>
    [Export] public string dungeon_incident_id = "";
    [Export] public string reason = "";
    [Export] public string outcome = "";
    [Export] public int resource_delta;
    [Export] public bool is_death_record;
}
