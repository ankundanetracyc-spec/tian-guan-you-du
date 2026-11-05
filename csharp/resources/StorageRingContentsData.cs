using Godot;

/// <summary>Content and spatial specification belong to one physical ring instance.</summary>
[GlobalClass]
public partial class StorageRingContentsData : Resource
{
    [Export] public string ring_instance_id = "";
    [Export] public string quality = "";
    [Export] public int width;
    [Export] public int height;
    [Export] public Godot.Collections.Array<StorageRingStoredItemData> items = [];
}
