using Godot;

/// <summary>单一势力对玩家的七阶态度；与区域声望及 NPC 个人关系分开保存。</summary>
[GlobalClass]
public partial class FactionAttitudeRecord : Resource
{
    public static readonly string[] StageNames = ["仇视", "敌视", "不满", "冷淡", "友善", "亲近", "尊敬"];

    [Export] public string faction_id = "";
    [Export(PropertyHint.Range, "0,6,1")] public int attitude_stage = 3;
    [Export(PropertyHint.MultilineText)] public string note = "";

    public string get_stage_name()
        => attitude_stage >= 0 && attitude_stage < StageNames.Length ? StageNames[attitude_stage] : "态度未定";
}
