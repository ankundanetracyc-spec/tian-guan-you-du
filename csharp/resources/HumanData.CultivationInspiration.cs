using Godot;
using Godot.Collections;

public partial class HumanData
{
    /// <summary>New inspiration inventory. Legacy qi_inspirations remains compatibility-only.</summary>
    [Export] public Array<CultivationInspirationRecord> cultivation_inspiration_records = [];
    /// <summary>Stable event IDs prevent repeated callbacks and save reloads from awarding twice.</summary>
    [Export] public Array<CultivationInspirationSourceEventRecord> cultivation_inspiration_source_events = [];

    /// <summary>Only ordinary Yunxi cultivation hours accumulate here; travel or other activity never subtracts it.</summary>
    [Export] public double cultivation_bottleneck_accumulated_hours;
    /// <summary>Cached tier/realm values keep the current relieved penalty stable across save/load.</summary>
    [Export] public int cultivation_bottleneck_level = -1;
    [Export] public int cultivation_bottleneck_realm_tier = -1;
    /// <summary>Current penalty magnitude P (0.02 means 2% less gain), after any inspiration relief.</summary>
    [Export] public double cultivation_bottleneck_current_penalty = -1d;

    /// <summary>Contribution bonus from an inspiration consumed when the current ordinary Yuxu duty began.</summary>
    [Export] public int sect_active_yuxu_inspiration_bonus;
}
