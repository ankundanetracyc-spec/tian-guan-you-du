using Godot;
using Godot.Collections;

/// <summary>Versioned snapshot for one playable save slot.</summary>
[GlobalClass]
public partial class PersistentGameSave : Resource
{
    public const int CurrentFormatVersion = 5;

    [Export] public int format_version = CurrentFormatVersion;
    [Export] public string slot_id = "";
    [Export] public string save_name = "苏醒者";
    [Export] public long saved_at_unix;

    [Export] public HumanData? player_data;
    [Export] public YuxuSectTreasuryLedger yuxu_sect_treasury = new();
    [Export] public Dictionary battle_reward_ledger = [];
    [Export] public int year = 1;
    [Export] public int month = 1;
    [Export] public int day = 1;
    [Export] public int hour;
    [Export] public float pending_hours;
    [Export] public string weather_type = "";
    [Export] public float weather_intensity;
    [Export] public float weather_wind_speed;
    [Export] public float weather_duration_hours;

    [Export] public Vector2I world_cell = new(38, 166);
    [Export] public string current_node_id = "home_village";
    [Export] public byte[] explored_map_cells = [];
    [Export] public Array<Vector2I> remaining_travel_path = [];

    [Export] public Array<WorldNpcData> world_npcs = [];
    [Export] public Array<EastWoodsNpcIncidentRecord> east_woods_npc_incidents = [];
    [Export] public bool npc_simulation_enabled;
    [Export] public int npc_last_processed_month = -1;
    [Export] public int npc_population_version;

    /// <summary>Scene-specific milestone flags, journal entries, and quest presentation state.</summary>
    [Export] public Dictionary scene_progress = [];

    public bool migrate_to_current_format()
    {
        if (format_version < 1 || format_version > CurrentFormatVersion) return false;
        yuxu_sect_treasury ??= new YuxuSectTreasuryLedger();

        while (format_version < CurrentFormatVersion)
        {
            if (format_version == 1)
            {
                // v1 to v2 retained the same Resource envelope; newly added fields use their defaults.
                format_version = 2;
                continue;
            }
            if (format_version == 2)
            {
                yuxu_sect_treasury.backfill_v2_delivery_records(player_data);
                format_version = 3;
                continue;
            }
            if (format_version == 3)
            {
                // v3 saves have no incident ledger; older NPC action text is not parsed into evidence.
                east_woods_npc_incidents ??= [];
                format_version = 4;
                continue;
            }
            if (format_version == 4)
            {
                // v4 saves have no weather snapshot; an empty weather_type asks WeatherMgr to recalculate it.
                weather_type ??= "";
                format_version = 5;
                continue;
            }
            return false;
        }
        return true;
    }
}
