using Godot;

/// <summary>黑屏开场背景：文本阶段由透明全屏按钮推进，选卷阶段才交出点击权。</summary>
public partial class OpeningIntro : Control
{
    public event Action? IntroCompleted;
    private Character? _child;
    private int _line;
    private bool _choosing, _selecting;
    private Button? _advanceCapture;
    private VBoxContainer? _lines;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Stop; ZIndex = 200; Build(); Hide(); }
    public void begin(Character child)
    {
        _child = child; _line = 0; _choosing = false; _selecting = false;
        if (_lines is not null) foreach (var item in _lines.GetChildren()) item.QueueFree();
        Show(); MoveToFront(); _advanceCapture?.Show(); AddNextLine();
    }

    private void Build()
    {
        var shade = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _advanceCapture = new Button { Name = "AdvanceCapture", Flat = true, FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop, Modulate = new Color(1, 1, 1, 0) };
        _advanceCapture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _advanceCapture.Pressed += AddNextLine; AddChild(_advanceCapture);
        var content = new VBoxContainer { Name = "Lines", MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); content.OffsetLeft = 150; content.OffsetRight = -150; content.OffsetTop = 230; content.OffsetBottom = -180; content.AddThemeConstantOverride("separation", 14); AddChild(content); _lines = content;
    }

    private void AddNextLine()
    {
        if (_choosing) return;
        if (_line >= PrologueText.OpeningLines.Length) { ShowChoices(); return; }
        var label = new Label { Text = PrologueText.OpeningLines[_line++], HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 22); label.AddThemeColorOverride("font_color", new Color(.9f, .88f, .8f)); _lines?.AddChild(label); CreateTween().TweenProperty(label, "modulate:a", 1f, .24f);
    }

    private void ShowChoices()
    {
        _choosing = true; _advanceCapture?.Hide();
        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.selected_initial_gongfa_path))
        {
            _lines?.AddChild(new Label { Text = PrologueText.SavedChoiceHint, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            var confirm = new Button { Text = PrologueText.SavedChoiceButton, CustomMinimumSize = new Vector2(220, 56) };
            confirm.Pressed += () => Select(profile.selected_initial_gongfa_path); _lines?.AddChild(confirm); return;
        }
        _lines?.AddChild(new Label { Text = PrologueText.ChoiceHint, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
        var row = new HBoxContainer { Name = "Choices" }; row.AddThemeConstantOverride("separation", 18); _lines?.AddChild(row);
        foreach (var (path, name, direction) in PrologueText.InitialChoices)
        {
            var button = new Button { Text = $"《{name}》\n{direction}", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 78) };
            button.Pressed += () => Select(path); row.AddChild(button);
        }
    }

    private void Select(string path)
    {
        if (_selecting) return;
        _selecting = true;
        var gongfa = ResourceLoader.Load<GongfaResource>(path);
        if (_child is null || gongfa is null) { ShowSelectionError("首卷载入失败，请重新开始本局。"); return; }
        if (GetNodeOrNull<GameStartProfile>("/root/GameStartProfile") is { } profile) profile.apply_to_youngest(_child.entity_data as HumanData ?? new HumanData());
        if (!_child.grant_intro_gongfa(gongfa)) { ShowSelectionError("此卷暂不能授予幼子，请重新选择。"); return; }
        Hide(); IntroCompleted?.Invoke();
    }

    private void ShowSelectionError(string message)
    {
        _selecting = false;
        _lines?.AddChild(new Label { Text = message, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1f, .65f, .55f) });
    }
}
