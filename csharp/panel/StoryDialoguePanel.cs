using Godot;

/// <summary>承载叙事与人物台词的底部对话框，不使用系统居中弹窗。</summary>
public partial class StoryDialoguePanel : Control
{
    private Label? _speaker;
    private Label? _body;
    private Action? _on_closed;
    private PanelContainer? _dialogueFrame;
    private VBoxContainer? _dialogueColumn;
    private Label? _continueHint;
    private float _dialogueHeight = 250;
    private bool _speakerOnRight;
    private ScrollContainer? _bodyScroll;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = QuietRoomStyle.MakeTheme();
        Theme.SetStylebox("panel", "TooltipPanel", WorldHudStyle.Paper());
        Theme.SetColor("font_color", "TooltipLabel", QuietRoomStyle.Ink);
        Theme.SetColor("font_shadow_color", "TooltipLabel", Colors.Transparent);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 240;
        GuiInput += on_background_input;

        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        _dialogueFrame = frame;
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color("ece9de"), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        frame.GuiInput += on_background_input;
        AddChild(frame);
        var paper = new WorldHudFrame { Name = "DialogueInkPaper" };
        frame.AddChild(paper); paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        frame.AddChild(margin);

        var column = new VBoxContainer();
        _dialogueColumn = column;
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        _speaker = new Label { Text = "旁白" };
        _speaker.AddThemeFontSizeOverride("font_size", 26);
        _speaker.AddThemeColorOverride("font_color", QuietRoomStyle.Teal);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 16);
        column.AddChild(heading);
        _speaker.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        heading.AddChild(_speaker);
        var rule = new HSeparator { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color("566e61", .28f), Thickness = 1 });
        heading.AddChild(rule);

        _body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Left, SizeFlagsVertical = SizeFlags.ExpandFill };
        _body.AddThemeFontSizeOverride("font_size", 23);
        _body.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        _body.AddThemeConstantOverride("line_spacing", 5);
        _body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _bodyScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        column.AddChild(_bodyScroll); _bodyScroll.AddChild(_body);

        var continueHint = new Label
        {
            Text = "点击任意处继续　›",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore
        };
        continueHint.AddThemeFontSizeOverride("font_size", 15);
        _continueHint = continueHint;
        continueHint.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        column.AddChild(continueHint);
        Resized += layout_bottom_dialogue;
        layout_bottom_dialogue();
        Hide();
    }

    public void show_narration(string body, Action? on_closed = null) => present("旁白", body, false, on_closed);
    public void show_player_speech(string player_name, string body, Action? on_closed = null) => present(player_name, body, false, on_closed);
    public void show_player_thought(string player_name, string body, Action? on_closed = null) => present(player_name, $"（{body}）", false, on_closed);
    public void show_npc_speech(string speaker, string body, Action? on_closed = null) => present(speaker, body, true, on_closed);

    private void present(string speaker, string body, bool speaker_on_right, Action? on_closed)
    {
        reset_social_presentation();
        _speaker!.Text = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker;
        _speakerOnRight = speaker_on_right;
        _speaker.HorizontalAlignment = HorizontalAlignment.Left;
        _body!.Text = body;
        if (_bodyScroll is not null) _bodyScroll.ScrollVertical = 0;
        _on_closed = on_closed;
        Show();
        MoveToFront();
        layout_bottom_dialogue(); CallDeferred(nameof(layout_bottom_dialogue));
    }

    public void close()
    {
        if (!Visible) return;
        if (close_bottom_choices()) return;
        if (handle_social_close()) return;
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
            if (!_socialChoosing && !_bottomChoosing) close();
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
