using Godot;

/// <summary>一个持久的大世界人物状态实例；其具体效果由已定案的状态规则负责。</summary>
[GlobalClass]
public partial class WorldStatusRecord : Resource
{
    [Export] public string instance_id = "";
    [Export] public string status_id = "";
    [Export] public string display_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public int stack_count = 1;
    [Export] public string source_id = "";
    [Export] public string source_name = "";
    [Export] public long applied_at_total_game_hours = -1;
    /// <summary>-1 表示无时限；其他值为绝对游戏时辰。</summary>
    [Export] public long expires_at_total_game_hours = -1;
    /// <summary>大世界状态通过真实休息行动恢复时所需的休息小时；0 表示不使用休息恢复。</summary>
    [Export] public int recovery_hours_required;
    /// <summary>累计休息行动小时数，不随旅行或普通时间推进增加。</summary>
    [Export] public int recovery_hours_progress;
    /// <summary>施加状态时的境界快照，供说明与存档核对。</summary>
    [Export] public string recovery_realm = "";
}
