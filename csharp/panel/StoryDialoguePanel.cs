using Godot;

/// <summary>承载叙事与人物台词的底部对话框，不使用系统居中弹窗。</summary>
public partial class StoryDialoguePanel : Control
{
    private Label? _speaker;
    private Label? _body;
    private Action? _on_closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 10000;
        GuiInput += on_background_input;

        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        frame.AnchorLeft = 0;
        frame.AnchorRight = 1;
        frame.AnchorTop = 1;
        frame.AnchorBottom = 1;
        frame.OffsetLeft = 0;
        frame.OffsetTop = -250;
        frame.OffsetRight = 0;
        frame.OffsetBottom = 0;
        frame.AddThemeStyleboxOverride("panel", make_frame_style());
        frame.GuiInput += on_background_input;
        AddChild(frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        frame.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        _speaker = new Label { Text = "旁白" };
        _speaker.AddThemeFontSizeOverride("font_size", 26);
        _speaker.AddThemeColorOverride("font_color", new Color("e6c77e"));
        column.AddChild(_speaker);

        _body = new Label { AutowrapMode = TextServer.AutowrapMode.Off, HorizontalAlignment = HorizontalAlignment.Left, SizeFlagsVertical = SizeFlags.ExpandFill };
        _body.AddThemeFontSizeOverride("font_size", 23);
        _body.AddThemeColorOverride("font_color", new Color("f2eee2"));
        column.AddChild(_body);

        var continueHint = new Label
        {
            Text = "点击任意处继续　›",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore
        };
        continueHint.AddThemeFontSizeOverride("font_size", 15);
        continueHint.AddThemeColorOverride("font_color", new Color("c9b98d"));
        column.AddChild(continueHint);

        Hide();
    }

    public void show_narration(string body, Action? on_closed = null) => present("旁白", body, false, on_closed);
    public void show_player_speech(string player_name, string body, Action? on_closed = null) => present(player_name, body, false, on_closed);
    public void show_player_thought(string player_name, string body, Action? on_closed = null) => present(player_name, $"（{body}）", false, on_closed);
    public void show_npc_speech(string speaker, string body, Action? on_closed = null) => present(speaker, body, true, on_closed);

    private void present(string speaker, string body, bool speaker_on_right, Action? on_closed)
    {
        _speaker!.Text = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker;
        _speaker.HorizontalAlignment = speaker_on_right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _body!.Text = body;
        _on_closed = on_closed;
        Show();
        MoveToFront();
    }

    public void close()
    {
        if (!Visible) return;
        hide_speaker_portrait();
        Hide();
        var callback = _on_closed;
        _on_closed = null;
        callback?.Invoke();
    }

    private void on_background_input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            close();
            GetViewport().SetInputAsHandled();
        }
    }

    private static StyleBoxFlat make_frame_style()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.032f, 0.027f, 0.95f),
            BorderColor = new Color("9a7a3d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ShadowColor = new Color(0, 0, 0, 0.55f),
            ShadowSize = 10
        };
    }
}
