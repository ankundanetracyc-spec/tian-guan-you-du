using Godot;

/// <summary>一封邀约自己的时间、地点与应答；活动规则由实际活动提供。</summary>
[GlobalClass]
public partial class LetterInvitationData : Resource
{
    [Export] public string activity_key = "";
    [Export] public string activity_name = "";
    [Export] public string location_id = "";
    [Export] public string subscene_id = "";
    [Export] public string place_name = "";
    [Export] public int reply_by_hour;
    [Export] public int starts_at_hour;
    [Export] public int ends_at_hour;
    [Export] public string response = "pending";

    public bool IsValid(int sentHour) => !string.IsNullOrWhiteSpace(activity_key)
        && !string.IsNullOrWhiteSpace(activity_name) && !string.IsNullOrWhiteSpace(place_name)
        && reply_by_hour >= sentHour && starts_at_hour >= reply_by_hour && ends_at_hour > starts_at_hour;
    public bool CanReply(int now) => response == "pending" && now < reply_by_hour;
    public bool CanAttend(int now) => response == "accepted" && now < ends_at_hour;
    public string State(int now) => response switch
    {
        "completed" => "已赴约",
        "declined" => "已婉拒",
        "accepted" when now >= ends_at_hour => "邀约已结束",
        "accepted" when now >= starts_at_hour => "已应邀 · 活动时段内",
        "accepted" => "已应邀 · 待赴约",
        _ when now >= reply_by_hour => "已错过邀约",
        _ => "待应答"
    };
}
