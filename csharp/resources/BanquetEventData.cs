using Godot;
using Godot.Collections;

/// <summary>可恢复的宗门宴席实例；每次回应都以结构化记录单次写入。</summary>
[GlobalClass]
public partial class BanquetEventData : Resource
{
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string LeftEarly = "left_early";
    public const string Declined = "declined";
    public const string Expired = "expired";

    [Export] public string event_id = "";
    /// <summary>玩家主持时为 player；NPC 主持时为该 NPC 的稳定 ID。</summary>
    [Export] public string host_id = "";
    [Export] public string host_name = "";
    [Export] public bool player_is_host;
    [Export] public string source_letter_id = "";
    [Export] public string venue_id = "";
    [Export] public int starts_at_hour;
    [Export] public int duration_hours = 4;
    [Export] public string status = Scheduled;
    [Export] public Array<string> invited_npc_ids = [];
    [Export] public Array<string> attendee_npc_ids = [];
    [Export] public Array<string> unavailable_npc_ids = [];
    [Export] public Array<Dictionary> beats = [];
    [Export] public Array<Dictionary> responses = [];
    [Export] public int next_beat_index;
    [Export] public string active_beat_id = "";
    [Export] public bool fee_settled;
    [Export] public bool time_settled;
    [Export] public int started_at_hour = -1;
    [Export] public int finished_at_hour = -1;
}
