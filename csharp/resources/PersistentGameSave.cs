using Godot;
using Godot.Collections;

/// <summary>Versioned snapshot for one playable save slot.</summary>
[GlobalClass]
public partial class PersistentGameSave : Resource
{
    public const int CurrentFormatVersion = 1;

    [Export] public int format_version = CurrentFormatVersion;
    [Export] public string slot_id = "";
    [Export] public string save_name = "苏醒者";
    [Export] public long saved_at_unix;

    [Export] public HumanData? player_data;
    [Export] public Dictionary battle_reward_ledger = [];
    [Export] public int year = 1;
    [Export] public int month = 1;
    [Export] public int day = 1;
    [Export] public int hour;
    [Export] public float pending_hours;

    [Export] public Vector2I world_cell = new(38, 166);
    [Export] public string current_node_id = "home_village";
    [Export] public byte[] explored_map_cells = [];
    [Export] public Array<Vector2I> remaining_travel_path = [];

    [Export] public Array<WorldNpcData> world_npcs = [];
    [Export] public bool npc_simulation_enabled;
    [Export] public int npc_last_processed_month = -1;

    /// <summary>Scene-specific milestone flags, journal entries, and quest presentation state.</summary>
    [Export] public Dictionary scene_progress = [];
}
