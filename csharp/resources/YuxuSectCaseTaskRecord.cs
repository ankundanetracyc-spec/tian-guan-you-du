using Godot;

/// <summary>A case-derived field task published on the Heguang duty board.</summary>
[GlobalClass]
public partial class YuxuSectCaseTaskRecord : Resource
{
    public const string StatusAvailable = "available";
    public const string StatusAssigned = "assigned";
    public const string StatusObjectiveComplete = "objective_complete";
    public const string StatusCompleted = "completed";
    public const string StatusCanceled = "canceled";

    public const string JourneyNotStarted = "not_started";
    public const string JourneyExitingDungeon = "exiting_dungeon";
    public const string JourneyInTransit = "in_transit";
    public const string JourneyIntercepted = "intercepted";
    public const string JourneyArrived = "arrived";
    public const string JourneyHandedOff = "handed_off";

    [Export] public string task_id = "";
    [Export] public string task_type = "";
    [Export] public string title = "";
    [Export] public string target_npc_id = "";
    [Export] public string target_npc_name = "";
    [Export] public int source_transfer_index = -1;
    [Export] public string goal = "";
    [Export] public string completion_conditions = "";
    [Export] public string return_instruction = "";
    [Export] public int contribution_reward;
    [Export] public bool kill_authorized;
    [Export] public string status = StatusAvailable;
    [Export] public string duty_instance_id = "";
    [Export] public string updated_at = "";

    /// <summary>Temporary case party state; the appellant is a real saved NPC and joins the player's travel/battle team.</summary>
    [Export] public string journey_status = JourneyNotStarted;
    [Export] public bool temporary_party_member_active;
    [Export] public string journey_party_npc_id = "";
    [Export] public string journey_destination_node_id = "";
    [Export] public Godot.Collections.Array<Vector2I> journey_route = [];
    [Export] public int journey_next_index;
    [Export] public Vector2I journey_current_cell = new(-1, -1);
    [Export] public float journey_remaining_hours;
    [Export] public string journey_phase_after_interception = "";
    [Export] public string journey_interception_npc_id = "";
    [Export] public string journey_interception_source = "";
    [Export] public Vector2I journey_interception_cell = new(-1, -1);
    [Export] public int journey_interception_count;
    [Export] public string journey_last_interception_result = "";
    [Export] public bool journey_inspiration_consumed;
    [Export] public string handoff_node_id = "";
    [Export] public string handoff_at = "";
    [Export] public string handoff_result = "";

    public bool is_open => status is StatusAvailable or StatusAssigned or StatusObjectiveComplete;
}
