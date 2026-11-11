using Godot;

/// <summary>One lecture's assigned speaker and the opening's durable milestones.</summary>
[GlobalClass]
public partial class BaishiLectureProgress : Resource
{
    [Export] public string event_id = Guid.NewGuid().ToString("N");
    [Export] public string host_actor_id = "";
    [Export] public bool host_is_player;
    [Export] public WorldNpcData? host_snapshot;
    [Export] public Godot.Collections.Array<WorldNpcData> representatives = [];
    [Export] public bool met_fang;
    [Export] public bool saw_awakening;
    [Export] public bool departed_qingxi;
    // Old saves already past the opening default to false and retain their position.
    [Export] public bool traveling_to_baishi;
    [Export] public bool heard_lecture;
    [Export] public bool roots_checked;
    [Export] public string selected_sect_id = "";
    [Export] public bool roots_session_finished;
    [Export] public bool fang_returned;
    [Export] public bool reached_yunxi;
    [Export] public bool chose_yuxu;
    [Export] public bool spirit_boat_in_transit;
    [Export] public string spirit_boat_driver_id = "";
    [Export] public bool finished;
    [Export] public bool assignments_released;
    [Export] public WorldNpcData companion = new()
    {
        npc_id = "prologue_fang_yuan", npc_name = "方远", spirit_root_name = "无灵根",
        cultivation_realm = "凡人", formal_identity = "求道者", current_node_id = "home_village", is_alive = true
    };

    // Actor assignment is independent of dialogue and participant progress. Future
    // duties can assign a player actor without replacing the lecture's content.
    public void AssignHost(string actorId, bool isPlayer, WorldNpcData? snapshot = null)
    {
        if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Missing lecture actor", nameof(actorId));
        host_actor_id = actorId;
        host_is_player = isPlayer;
        host_snapshot = snapshot?.Duplicate(true) as WorldNpcData;
    }
}
