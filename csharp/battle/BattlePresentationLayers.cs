/// <summary>Absolute world-canvas depths: include deployed actor ancestors and response focus.</summary>
public static class BattlePresentationLayers
{
    public const int UnitBarsCanvas = 5;
    public const int FeedbackCanvas = 6;
    public const int HudCanvas = 10;
    public const int CharacterDetailsCanvas = 90;
    public const int ContactShade = 3100;
    public const int Effects = 3300;
    public const int Numbers = 3600;

    /// <summary>反馈使用屏幕画布；ZIndex 无法越过另一张 CanvasLayer。</summary>
    public static Godot.CanvasLayer FeedbackLayer(Godot.Node context)
    {
        var tree=context.GetTree();
        var owner=tree.GetFirstNodeInGroup("battle_mgr")??tree.CurrentScene??context.GetViewport();
        if(owner.GetNodeOrNull<Godot.CanvasLayer>("BattleFeedbackCanvas") is { } existing)return existing;
        var canvas=new Godot.CanvasLayer{Name="BattleFeedbackCanvas",Layer=FeedbackCanvas,ProcessMode=Godot.Node.ProcessModeEnum.Pausable};
        owner.AddChild(canvas);
        return canvas;
    }
}
