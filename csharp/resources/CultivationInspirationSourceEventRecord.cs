using Godot;

/// <summary>Idempotency and diminishing-return ledger for a completed source event, including misses.</summary>
[GlobalClass]
public partial class CultivationInspirationSourceEventRecord : Resource
{
    [Export] public string event_id = "";
    [Export] public string source_id = "";
    [Export] public long attempted_at_game_hour = -1;
    [Export] public bool granted;
    [Export] public string granted_record_id = "";
    [Export] public int requested_quality_rank;
    [Export] public float evaluated_chance;
    [Export] public float deterministic_roll;
}
