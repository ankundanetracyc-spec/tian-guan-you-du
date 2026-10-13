using Godot;

/// <summary>A renewable insight token. The source and its permitted action domain are recorded separately.</summary>
[GlobalClass]
public partial class CultivationInspirationRecord : Resource
{
    public const string StateAvailable = "available";
    public const string StateConsumed = "consumed";
    public const string StateExpired = "expired";

    [Export] public string record_id = "";
    [Export] public string source_event_id = "";
    [Export] public string source_id = "";
    [Export] public string source_label = "";
    [Export] public string use_domain_id = "";
    /// <summary>Game rank: 1 见几, 2 明辨, 3 通变.</summary>
    [Export] public int quality_rank = 1;
    [Export] public long acquired_at_game_hour = -1;
    [Export] public long expires_at_game_hour = -1;
    [Export] public string state_id = StateAvailable;
    /// <summary>Player's explicit choice to spend this record on its matching action when that action begins.</summary>
    [Export] public bool armed_for_action;
    [Export] public string consumed_for = "";
    [Export] public long consumed_at_game_hour = -1;

    public string get_quality_name() => quality_rank switch
    {
        1 => "见几",
        2 => "明辨",
        3 => "通变",
        _ => "未定品级"
    };

    public bool is_available_at(long gameHour)
        => state_id == StateAvailable && expires_at_game_hour > gameHour;

    public bool mark_expired_if_due(long gameHour)
    {
        if (state_id != StateAvailable || expires_at_game_hour < 0 || expires_at_game_hour > gameHour)
            return false;
        state_id = StateExpired;
        armed_for_action = false;
        return true;
    }

    public bool mark_consumed(string use, long gameHour)
    {
        if (state_id != StateAvailable || string.IsNullOrWhiteSpace(use) || expires_at_game_hour <= gameHour)
            return false;
        state_id = StateConsumed;
        consumed_for = use;
        consumed_at_game_hour = gameHour;
        return true;
    }
}
