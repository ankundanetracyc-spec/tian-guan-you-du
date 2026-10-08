using Godot;

/// <summary>Persistent state for the currently accepted Yuxu Sect duty.</summary>
public partial class HumanData
{
    /// <summary>Current Yuxu duty ID; empty means the player has no active duty.</summary>
    [Export] public string sect_active_yuxu_duty_id = "";
    /// <summary>Unique accepted instance for idempotent official-duty settlement.</summary>
    [Export] public string sect_active_yuxu_duty_instance_id = "";

    /// <summary>Prototype progress for time-based Yuxu duties; -1 means not started.</summary>
    [Export] public int sect_active_yuxu_duty_started_hour = -1;
    /// <summary>Verified task-specific milestone count for the active Yuxu duty.</summary>
    [Export] public int sect_active_yuxu_duty_stage;
}
