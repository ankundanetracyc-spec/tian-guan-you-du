using Godot;

/// <summary>Content and spatial specification belong to one physical bag instance.</summary>
[GlobalClass]
public partial class StorageBagContentsData : Resource
{
    [Export] public string bag_instance_id = "";
    [Export] public string quality = "";
    [Export] public int width;
    [Export] public int height;
    [Export] public Godot.Collections.Array<StorageBagStoredItemData> items = [];
}
