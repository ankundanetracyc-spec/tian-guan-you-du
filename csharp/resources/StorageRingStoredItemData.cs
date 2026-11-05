using Godot;

[GlobalClass]
public partial class StorageRingStoredItemData : Resource
{
    [Export] public string instance_id = "";
    [Export] public string item_path = "";
    [Export] public ArtifactInstanceData? artifact;
    [Export] public int cell_x;
    [Export] public int cell_y;
    [Export] public int rotation;
}
