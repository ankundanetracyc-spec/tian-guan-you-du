using Godot;

/// <summary>Persistent state for the currently accepted Yuxu Sect duty.</summary>
public partial class HumanData
{
    /// <summary>Current Yuxu duty ID; empty means the player has no active duty.</summary>
    [Export] public string sect_active_yuxu_duty_id = "";
    /// <summary>Unique accepted instance for idempotent official-duty settlement.</summary>
    [Export] public string sect_active_yuxu_duty_instance_id = "";
    /// <summary>Stable East Woods incident linked to the active verification duty, when applicable.</summary>
    [Export] public string sect_active_yuxu_duty_incident_id = "";
    /// <summary>Stable case-task ID linked to an active field assignment, when applicable.</summary>
    [Export] public string sect_active_yuxu_duty_task_id = "";

    /// <summary>Prototype progress for time-based Yuxu duties; -1 means not started.</summary>
    [Export] public int sect_active_yuxu_duty_started_hour = -1;
    /// <summary>Verified task-specific milestone count for the active Yuxu duty.</summary>
    [Export] public int sect_active_yuxu_duty_stage;
    /// <summary>Patrol verified in the field; contribution awaits reporting at Heguang.</summary>
    [Export] public bool sect_active_yuxu_duty_ready;
}
