using Godot;

/// <summary>一封邀约自己的时间、地点与应答；活动规则由实际活动提供。</summary>
[GlobalClass]
public partial class LetterInvitationData : Resource
{
    [Export] public string activity_key = "";
    /// <summary>指向已保存的具体活动实例，不由通用信件框架推断。</summary>
    [Export] public string activity_instance_id = "";
    /// <summary>由 NPC 主持的活动使用，玩家主持时留空。</summary>
    [Export] public string host_npc_id = "";
    [Export] public string activity_name = "";
    [Export] public string location_id = "";
    [Export] public string subscene_id = "";
    [Export] public string place_name = "";
    [Export] public int reply_by_hour;
    [Export] public int starts_at_hour;
    [Export] public int ends_at_hour;
    [Export] public string response = "pending";

    /// <summary>邀约在界面按日期展示；底层仍以绝对游戏小时判断应答与赴约。</summary>
    public bool UsesDateOnlySchedule => activity_key == "banquet";
    public string StartsAtDisplay => LetterMailbox.Date(starts_at_hour);
    public string ReplyByDisplay => LetterMailbox.Date(Math.Max(0, reply_by_hour - 1));
    public string ScheduleDisplay
    {
        get
        {
            var firstDay = LetterMailbox.Date(starts_at_hour);
            var lastDay = LetterMailbox.Date(Math.Max(starts_at_hour, ends_at_hour - 1));
            return firstDay == lastDay ? firstDay : $"{firstDay} 至 {lastDay}";
        }
    }

    public bool IsValid(int sentHour) => !string.IsNullOrWhiteSpace(activity_key)
        && !string.IsNullOrWhiteSpace(activity_name) && !string.IsNullOrWhiteSpace(place_name)
        && reply_by_hour >= sentHour && starts_at_hour >= reply_by_hour && ends_at_hour > starts_at_hour;
    public bool CanReply(int now) => response == "pending" && now < reply_by_hour;
    public bool CanAttend(int now) => response == "accepted" && now >= starts_at_hour && now < ends_at_hour;
    public string State(int now) => response switch
    {
        "completed" => "已赴约",
        "declined" => "已婉拒",
        "accepted" when now >= ends_at_hour => "邀约已结束",
        "accepted" when now >= starts_at_hour => "已应邀 · 举办日内",
        "failed" => "邀约未能成行",
        "accepted" => "已应邀 · 待赴约",
        _ when now >= reply_by_hour => "已错过邀约",
        _ => "待应答"
    };
}
