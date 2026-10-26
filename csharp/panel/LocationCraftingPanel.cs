using Godot;

/// <summary>地点设施的炼制页面宿主；离开页面后仍留在原地点。</summary>
public partial class LocationCraftingPanel : Control
{
    private Control? _room;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; ZIndex = 300; Hide();
    }
    public void ShowFor(Character owner, bool alchemy)
    {
        if (_room is not null && IsInstanceValid(_room) && !_room.IsQueuedForDeletion()) return;
        if (alchemy)
        {
            var room = GD.Load<PackedScene>("res://tscn/scene/AlchemyPreviewScene.tscn").Instantiate<AlchemySlotBrewPreviewScene>();
            room.OwnerCharacter = owner; _room = room;
        }
        else
        {
            var room = GD.Load<PackedScene>("res://tscn/scene/ProcessForgingScene.tscn").Instantiate<ProcessForgingScene>();
            room.BindOwner(owner); _room = room;
        }
        _room.TreeExiting += () => { _room = null; Hide(); };
        AddChild(_room); Show(); MoveToFront();
    }
    public void ReturnToLocation()
    {
        if (_room is AlchemySlotBrewPreviewScene alchemy) alchemy.ReturnToBaigong();
        else if (_room is ProcessForgingScene forging) forging.ReturnToBaigong();
        else Hide();
    }
}
