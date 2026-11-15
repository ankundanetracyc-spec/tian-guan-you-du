using Godot;

/// <summary>Compatibility entry points all mount the current ink pose/sequence showcase.</summary>
public static class BattlePreviewScene
{
    public const string Path = "res://tscn/scene/SideInkChopPreview.tscn";
    public static void Mount(Node host)
    {
        host.AddChild(GD.Load<PackedScene>(Path).Instantiate());
        host.SetProcess(false);
        host.SetProcessInput(false);
        host.SetProcessUnhandledInput(false);
    }
}
