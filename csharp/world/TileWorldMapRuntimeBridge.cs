using Godot;

/// <summary>Defers map wiring until MainScene has created its runtime map and player.</summary>
public partial class TileWorldMapRuntimeBridge : Node
{
    public override void _Ready() => CallDeferred(nameof(connect_to_main_scene));

    private void connect_to_main_scene()
    {
        if (GetTree().GetFirstNodeInGroup("main_scene") is MainScene mainScene)
            mainScene.configure_tile_world_map_bridge();
    }
}
