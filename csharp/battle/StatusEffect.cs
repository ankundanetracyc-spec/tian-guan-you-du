using Godot;
using Godot.Collections;

/// <summary>可序列化的战斗状态定义。</summary>
public partial class StatusEffect : Resource
{
    public const float CombatFramesPerSecond = 60f;
    // Preserve serialized enum values 0=Turns and 1=Hits; append new duration modes.
    public enum DurationType { Turns, Hits, Frames, Trigger, CombatTime }

    [Export] public string effect_id = "";
    [Export] public string name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public bool is_debuff;
    [Export] public int max_stacks = 1;
    [Export] public bool stack_by_source;
    // 时间层数与有效战斗帧一一对应；再次施加刷新，计时递减同步层数。
    [Export] public bool frame_counted_layers;
    [Export] public int duration_default = 3;

    [Export] public int damage_per_tick;
    [Export] public int heal_per_tick;
    [Export] public int shield_per_tick;
    [Export] public int stamina_per_tick;
    [Export] public DurationType duration_type = DurationType.Turns;
    [Export] public Dictionary stat_modifiers = [];
    [Export] public Array<Dictionary> hook_effects = [];
}
