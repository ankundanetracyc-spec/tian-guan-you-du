using Godot;
using Godot.Collections;

public partial class HumanData
{
    /// <summary>停用灵感系统的历史库存，仅兼容存档；旧 qi_inspirations 同样不参与玩法。</summary>
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

    /// <summary>One-shot automatic refinement preferences for the next qualifying cultivation session.</summary>
    [Export] public bool cultivation_auto_refinement_enabled;
    [Export] public string cultivation_auto_refinement_record_id = "";
    /// <summary>Trigger when current bottleneck penalty reaches this fraction. -1 migrates the legacy tier setting.</summary>
    [Export] public double cultivation_auto_refinement_threshold_penalty = -1d;
    /// <summary>Legacy save field; converted to a penalty threshold on first use.</summary>
    [Export] public int cultivation_auto_refinement_threshold_level = 1;

    /// <summary>Contribution bonus from an inspiration consumed when the current ordinary Yuxu duty began.</summary>
    [Export] public int sect_active_yuxu_inspiration_bonus;
}
