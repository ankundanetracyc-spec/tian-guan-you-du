using Godot;

public partial class StartMenu
{
    private static readonly string[] BackdropNames = ["山村晓色", "山海云阔", "一指开天"];
    private readonly Button[] _backdropSelectors = new Button[3];
    private Control? _demoOverlay;

    private const string MenuButtonAtlasPath = "res://textures/start_menu/ui_paper_ink_v3/button_atlas.png";

    private static StyleBoxTexture MenuPaperButtonStyle(bool primary, bool hover = false)
    {
        // Atlas regions keep the generated alpha and paper edges intact.
        var top = primary ? (hover ? 538 : 70) : (hover ? 778 : 305);
        return new StyleBoxTexture
        {
            Texture = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>(MenuButtonAtlasPath),
                Region = new Rect2(60, top, 1420, 195),
                FilterClip = true
            },
            ContentMarginLeft = 26, ContentMarginRight = 26,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
    }

    private static void BuildPaperInkMenu(VBoxContainer menu)
    {
        menu.AddThemeConstantOverride("separation", 14);
        foreach (var name in new[] { "NewSave", "LoadSave", "Encyclopedia", "Settings", "Quit" })
        {
            var button = menu.GetNode<Button>(name);
            var primary = name == "NewSave";
            button.CustomMinimumSize = new Vector2(430, 66);
            StyleSettingsText(button, 23, primary ? new Color(.97f, .95f, .87f) : new Color(.13f, .23f, .20f));
            foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_focus_color" })
                button.AddThemeColorOverride(state, primary ? new Color(1f, .98f, .92f) : new Color(.10f, .19f, .16f));
            button.AddThemeStyleboxOverride("normal", MenuPaperButtonStyle(primary));
            button.AddThemeStyleboxOverride("hover", MenuPaperButtonStyle(primary, true));
            button.AddThemeStyleboxOverride("pressed", MenuPaperButtonStyle(primary, true));
            button.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
        }
        var preview = menu.GetNode<Button>("DemoToggle");
        preview.CustomMinimumSize = new Vector2(430, 36);
        preview.Text = "试演与预览";
        foreach (var state in new[] { "normal", "hover", "pressed" })
            preview.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        preview.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
    }

    private void BuildBackdropSelectors()
    {
        var row = new HBoxContainer { Name = "BackdropSelectors" };
        row.SetAnchorsPreset(LayoutPreset.BottomLeft);
        row.OffsetLeft = 60;
        row.OffsetTop = -86;
        row.OffsetRight = 560;
        row.OffsetBottom = -42;
        row.AddThemeConstantOverride("separation", 10);
        var paper = new TextureRect
        {
            Name = "BackdropSelectorPaper",
            Texture = MenuPaperButtonStyle(false).Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, .85f)
        };
        paper.SetAnchorsPreset(LayoutPreset.BottomLeft);
        paper.OffsetLeft = 48; paper.OffsetTop = -92;
        paper.OffsetRight = 570; paper.OffsetBottom = -36;
        AddChild(paper);
        AddChild(row);

        for (var index = 0; index < BackdropNames.Length; index++)
        {
            var target = index;
            var button = new Button
            {
                Text = BackdropNames[index],
                CustomMinimumSize = new Vector2(156, 40),
            };
            StyleSettingsText(button, 16, new Color(.17f, .26f, .22f));
            button.AddThemeColorOverride("font_hover_color", new Color(.12f, .22f, .18f));
            button.AddThemeColorOverride("font_pressed_color", new Color(.12f, .22f, .18f));
            button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
            button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
            button.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
            button.Pressed += () => SelectBackdrop(target);
            _backdropSelectors[index] = button;
            row.AddChild(button);
        }
        RefreshBackdropSelectors();
    }

    private void SelectBackdrop(int index)
    {
        if (_backdrop is null || _nextBackdrop is null) return;
        _backdropIndex = index;
        _backdrop.Texture = GD.Load<Texture2D>(BackdropPaths[index]);
        _backdrop.Modulate = Colors.White;
        _backdrop.MoveToFront();
        _nextBackdrop.Modulate = new Color(1f, 1f, 1f, 0f);
        _backdropHoldElapsed = 0;
        _backdropFadeElapsed = 0;
        _isBackdropFading = false;
        RefreshBackdropSelectors();
    }

    private void RefreshBackdropSelectors()
    {
        for (var index = 0; index < _backdropSelectors.Length; index++)
        {
            var button = _backdropSelectors[index];
            if (button is null) continue;
            var active = index == _backdropIndex;
            button.Text = (active ? "●  " : "○  ") + BackdropNames[index];
            button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        }
    }

    private void ToggleDemoEntries()
    {
        if (_demoOverlay is not null) { CloseDemoEntries(); return; }
        _demoOverlay = new Control { Name = "StartMenuPreviewOverlay", ZIndex = 300 };
        _demoOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_demoOverlay);
        var veil = new ColorRect { Color = new Color(.025f, .045f, .04f, .50f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _demoOverlay.AddChild(veil);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _demoOverlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(640, 700) };
        panel.AddThemeStyleboxOverride("panel", PaperStyle());
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        panel.AddChild(box);
        var heading = new Label { Text = "试演与预览", HorizontalAlignment = HorizontalAlignment.Center };
        StyleSettingsText(heading, 32, new Color(.12f, .20f, .17f));
        box.AddChild(heading);
        box.AddChild(SettingsRule());
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);
        var menu = GetNode<VBoxContainer>("StartMenuMenu");
        foreach (var name in new[] { "AppearanceTest", "AppearanceCreatorTest", "InkVfxTest", "BattleTest", "CondensationTest", "YuanxiPreview", "SocialInteractionTest" })
        {
            var source = menu.GetNode<Button>(name);
            var button = SettingsButton(source.Text);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 58);
            button.Pressed += () => { CloseDemoEntries(); source.EmitSignal(BaseButton.SignalName.Pressed); };
            list.AddChild(button);
        }
        var alchemy = SettingsButton("炼丹配伍");
        alchemy.SizeFlagsHorizontal = SizeFlags.ExpandFill; alchemy.CustomMinimumSize = new Vector2(0, 58);
        alchemy.Pressed += () => { CloseDemoEntries(); GetTree().ChangeSceneToFile("res://tscn/scene/AlchemyPreviewScene.tscn"); };
        list.AddChild(alchemy);
        var forging = SettingsButton("炼器工坊 · 剑类试炼");
        forging.SizeFlagsHorizontal = SizeFlags.ExpandFill; forging.CustomMinimumSize = new Vector2(0, 58);
        forging.Pressed += () => { CloseDemoEntries(); GetTree().ChangeSceneToFile("res://tscn/scene/SwordCraftingScene.tscn"); };
        list.AddChild(forging);
        var letters = SettingsButton("信件界面预览");
        letters.SizeFlagsHorizontal = SizeFlags.ExpandFill; letters.CustomMinimumSize = new Vector2(0, 58);
        letters.Pressed += () => { CloseDemoEntries(); GetTree().ChangeSceneToFile("res://tscn/scene/LettersPreviewScene.tscn"); };
        list.AddChild(letters);
        var close = SettingsButton("返回首页");
        close.Pressed += CloseDemoEntries;
        box.AddChild(close);
    }

    private void CloseDemoEntries()
    {
        _demoOverlay?.QueueFree();
        _demoOverlay = null;
        GetNode<Button>("StartMenuMenu/DemoToggle").GrabFocus();
    }

    public static StyleBoxFlat SettingsFocusStyle() => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = new Color(.32f, .43f, .37f, .75f),
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    public static void StyleSettingsOption(OptionButton selector)
    {
        StyleSettingsText(selector, 17, new Color(.16f, .25f, .20f));
        selector.AddThemeColorOverride("font_hover_color", new Color(.12f, .21f, .17f));
        selector.AddThemeColorOverride("font_pressed_color", new Color(.12f, .21f, .17f));
        selector.AddThemeColorOverride("font_disabled_color", new Color(.45f, .48f, .41f));
        selector.AddThemeStyleboxOverride("normal", SettingsButtonStyle(new Color(.96f, .94f, .87f), new Color(.42f, .48f, .37f, .6f)));
        selector.AddThemeStyleboxOverride("hover", SettingsButtonStyle(new Color(.99f, .96f, .86f), new Color(.65f, .53f, .31f)));
        selector.AddThemeStyleboxOverride("pressed", SettingsButtonStyle(new Color(.87f, .90f, .82f), new Color(.38f, .48f, .37f)));
        selector.AddThemeStyleboxOverride("disabled", SettingsButtonStyle(new Color(.86f, .86f, .79f), new Color(.53f, .55f, .47f, .4f)));
        selector.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
        selector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selector.AddThemeConstantOverride("modulate_arrow", 1);
        var popup = selector.GetPopup();
        popup.AddThemeStyleboxOverride("panel", PaperStyle());
        popup.AddThemeStyleboxOverride("hover", SettingsButtonStyle(new Color(.19f, .30f, .24f), new Color(.42f, .45f, .31f)));
        popup.AddThemeColorOverride("font_color", new Color(.16f, .25f, .20f));
        popup.AddThemeColorOverride("font_hover_color", new Color(.96f, .93f, .84f));
        popup.AddThemeFontSizeOverride("font_size", 17);
        popup.AddThemeConstantOverride("v_separation", 16);
        if (GD.Load<FontFile>(StartMenuFontPath) is { } font) popup.AddThemeFontOverride("font", font);
    }

    public static void StyleSettingsSlider(HSlider slider)
    {
        slider.AddThemeStyleboxOverride("slider", new StyleBoxFlat
        {
            BgColor = new Color(.60f, .64f, .54f), ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        slider.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat
        {
            BgColor = new Color(.24f, .38f, .30f), ContentMarginTop = 3, ContentMarginBottom = 3
        });
        slider.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat
        {
            BgColor = new Color(.34f, .48f, .37f), ContentMarginTop = 3, ContentMarginBottom = 3
        });
        using var thumb = new Image();
        thumb.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><circle cx='10' cy='10' r='8' fill='#345841' stroke='#eee8d5' stroke-width='3'/></svg>");
        using var texture = ImageTexture.CreateFromImage(thumb);
        slider.AddThemeIconOverride("grabber", texture);
        slider.AddThemeIconOverride("grabber_highlight", texture);
        slider.AddThemeStyleboxOverride("focus", SettingsFocusStyle());
    }
}
