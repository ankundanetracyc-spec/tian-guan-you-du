using Godot;

/// <summary>开发用的局内皮影测试台。局外立绘暂不参与此流程。</summary>
public partial class AppearanceLab : Control
{
    private Sprite2D? _body;
    private PuppetOverlay? _overlay;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var background = new ColorRect { Color = new Color("101711f2") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var title = new Label { Text = "局内皮影 · 模块测试", Position = new Vector2(34, 26) };
        title.AddThemeFontSizeOverride("font_size", 30);
        AddChild(title);

        var note = new Label
        {
            Text = "当前仅保留局内皮影流程；局外立绘已取消。\n新版素材将使用统一透明母版、统一孔径与固定锚点。",
            Position = new Vector2(36, 76)
        };
        note.AddThemeFontSizeOverride("font_size", 18);
        note.AddThemeColorOverride("font_color", new Color("e8d9aa"));
        AddChild(note);

        _body = new Sprite2D
        {
            Position = new Vector2(640, 560),
            Scale = Vector2.One * .28f,
            Texture = GD.Load<Texture2D>("res://mods/appearance/皮影局内男修_v5/body/vermilion_body.png"),
            ZIndex = 4
        };
        AddChild(_body);
        _overlay = new PuppetOverlay { Position = _body.Position, Scale = _body.Scale };
        AddChild(_overlay);

        var close = new Button { Text = "返回开始界面", Position = new Vector2(36, 180), Size = new Vector2(180, 46) };
        close.Pressed += QueueFree;
        AddChild(close);
    }
}
