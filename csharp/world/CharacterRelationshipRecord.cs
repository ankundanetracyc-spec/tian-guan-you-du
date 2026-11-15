using Godot;

/// <summary>一个人物对另一个稳定人物 ID 的单向私交记录。</summary>
[GlobalClass]
public partial class CharacterRelationshipRecord : Resource
{
    [Export] public string target_character_id = "";
    [Export(PropertyHint.Range, "0,100,1")] public int closeness;
    [Export(PropertyHint.Range, "0,100,1")] public int trust;
    [Export(PropertyHint.Range, "0,100,1")] public int respect;
    [Export(PropertyHint.Range, "0,100,1")] public int resentment;

    /// <summary>v6 明确记录的朋友／仇敌事实；独立保留，不折算为四项新数值。</summary>
    [Export] public string legacy_marker = "";
    [Export] public Godot.Collections.Array<RelationshipEvidenceRecord> pending_evidence = [];
    [Export] public Godot.Collections.Array<string> processed_event_ids = [];
    [Export] public Godot.Collections.Array<int> settled_conversation_months = [];
    [Export] public Godot.Collections.Dictionary category_application_counts = [];
    [Export] public Godot.Collections.Array<RelationshipLifeEventRecord> life_events = [];
}
