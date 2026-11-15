using Godot;

/// <summary>一条待月度归纳或已即时结算的定向交往证据。</summary>
[GlobalClass]
public partial class RelationshipEvidenceRecord : Resource
{
    [Export] public string event_id = "";
    [Export] public string event_kind = "";
    [Export] public string axis = "";
    [Export] public int month_index = -1;
    [Export] public int base_amount;
    [Export] public string note = "";
}
