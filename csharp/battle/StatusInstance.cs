using Godot;

/// <summary>角色持有的单个战斗状态及其当前层数、持续时间。</summary>
public partial class StatusInstance : RefCounted
{
    public StatusEffect? effect;
    public int remaining_duration = -1;
    public int remaining_hits = 1;
    public float remaining_frames = -1;
    public float remaining_combat_seconds = -1;
    public int stacks = 1;
    public int applied_round_id;
    public string source_id = "";
    public string source_name = "";
    public int pill_efficacy_tier = -1;
    /// <summary>持续丹的剩余触发次数；普通状态不读取。</summary>
    public int remaining_pill_ticks = -1;

    public int remaining_combat_frames => Mathf.Max(0,Mathf.CeilToInt(remaining_combat_seconds*StatusEffect.CombatFramesPerSecond-0.0001f));
    public void sync_combat_frame_layers()
    {
        if(effect?.frame_counted_layers==true)
        {
            stacks=remaining_combat_frames;
            if(stacks==0)remaining_combat_seconds=0;
        }
    }

    public int get_damage_per_tick() => effect?.damage_per_tick * stacks ?? 0;
    public int get_heal_per_tick() => effect?.heal_per_tick * stacks ?? 0;
    public int get_shield_per_tick() => effect?.shield_per_tick * stacks ?? 0;
    public int get_stamina_per_tick() => effect?.stamina_per_tick * stacks ?? 0;
}
