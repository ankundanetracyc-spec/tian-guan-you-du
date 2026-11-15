using Godot;

public partial class StartMenu
{
    private Control? _roguelikeSetupOverlay;

    private void OpenRoguelikeSetup()
    {
        if (_roguelikeSetupOverlay is { } existing && GodotObject.IsInstanceValid(existing))
        {
            existing.Show();
            existing.MoveToFront();
            return;
        }

        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        if (profile is null) return;
        profile.player_name = "行客";
        profile.roll_fated_aptitude();
        profile.randomize_player_appearance();

        var overlay = new Control { Name = "RoguelikeSetupOverlay", MouseFilter = MouseFilterEnum.Stop, ZIndex = 260 };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        _roguelikeSetupOverlay = overlay;
        var veil = new ColorRect { Color = new Color(.02f, .025f, .02f, .72f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(veil);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(780, 590), MouseFilter = MouseFilterEnum.Stop };
        paper.SetAnchorsPreset(LayoutPreset.Center);
        paper.OffsetLeft = -390; paper.OffsetTop = -295; paper.OffsetRight = 390; paper.OffsetBottom = 295;
        paper.AddThemeStyleboxOverride("panel", PaperStyle());
        overlay.AddChild(paper);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); paper.AddChild(root);
        var title = new Label { Text = "独立修行副本", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30); title.AddThemeColorOverride("font_color", Ink()); root.AddChild(title);
        var intro = new Label { Text = "新角色踏入三十层秘境，收集材料、修炼成长，击败首领后通关。", HorizontalAlignment = HorizontalAlignment.Center };
        intro.AddThemeColorOverride("font_color", new Color(.32f, .27f, .19f)); root.AddChild(intro);
        var name = new LineEdit { Text = profile.player_name, PlaceholderText = "角色名", CustomMinimumSize = new Vector2(0, 42) };
        root.AddChild(name);

        var choices = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        choices.AddThemeConstantOverride("separation", 10); root.AddChild(choices);
        Button make_choice(string label, string id)
        {
            var button = PlainButton(label);
            button.CustomMinimumSize = new Vector2(0, 82);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            choices.AddChild(button);
            button.Pressed += async () =>
            {
                foreach (var child in choices.GetChildren())
                    if (child is Button other) other.Disabled = true;
                profile.player_name = string.IsNullOrWhiteSpace(name.Text) ? "行客" : name.Text.Trim();
                var result = await JourneyCreationLoadingView.EnterAsync(GetTree(), profile.player_name, id);
                if (result != Error.Ok && GodotObject.IsInstanceValid(overlay))
                    foreach (var child in choices.GetChildren())
                        if (child is Button other) other.Disabled = false;
            };
            return button;
        }
        make_choice("剑修　·　剑道专长\n剑道造诣 10 · 专属秘籍《舒卷天章》", "sword");
        make_choice("丹师　·　丹道专长\n丹道造诣 10 · 专属秘籍《斫基损契玄经》", "alchemy");
        make_choice("器师　·　器道专长\n器道造诣 10 · 专属秘籍《百骸衍枝化生宝册》", "forging");
        var close = PlainButton("返回"); close.Pressed += overlay.QueueFree; root.AddChild(close);
    }
}
