using Godot;

/// <summary>以源像素切片、显示尺寸保留边角的无字底板。</summary>
public partial class BattleBoardSkinPanel : Control
{
    public string Part = "source_panel";
    public override void _Ready() { Resized += QueueRedraw; MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw() => BattleBoardSkin.DrawPanel(this, Part, new Rect2(Vector2.Zero,Size));
}
