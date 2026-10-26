using Godot;

public partial class Character
{
    private string crafting_record_date()
    {
        var time = IsInsideTree() ? GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") : null;
        return time is null ? "" : $"昭明{time.get_zhaoming_year()}年{time.month}月{time.day}日　";
    }
}
