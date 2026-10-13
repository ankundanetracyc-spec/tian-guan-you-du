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

    public bool is_open => status is StatusAvailable or StatusAssigned or StatusObjectiveComplete;
}
