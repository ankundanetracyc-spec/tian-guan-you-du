using Godot;

/// <summary>Exercises typed/native containers and Export properties for detached save validation.</summary>
public partial class DetachedSnapshotProbeData : Resource
{
    [Export] public Godot.Collections.Dictionary<string, NpcActionRecord> records = [];
    [Export] public Godot.Collections.Array<int> numbers = [1, 2, 3];
    [Export] public byte[] bytes = [3, 5, 8];
    [Export] public Vector2I position { get; set; } = new(14, 27);
    [Export] public Variant payload { get; set; }
    [Export] public Resource? external;
    [Export] public Godot.Collections.Array mixed = [];
}
