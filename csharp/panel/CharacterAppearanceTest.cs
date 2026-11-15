using Godot;

/// <summary>Start-menu preview of the current male and female nine-part ink portraits.</summary>
public partial class CharacterAppearanceTest : Control
{
    private const string StartMenuPath = "res://tscn/scene/StartMenu.tscn";
    private readonly bool[] _visible = new bool[9];
    private readonly CheckButton[] _partChecks = new CheckButton[9];
    private readonly OptionButton[] _partChoices = new OptionButton[9];
    private readonly int[][] _variants = [new int[9], new int[9]];
    private bool _female = true;
    private bool _showAnchors;
    private TextureRect _portraitView = null!;
    private Label _status = null!;
    private FontFile? _font;
    private ShaderMaterial? _breathingMaterial;
    private double _breathTime;
    private bool _breathingEnabled = true;

    public override void _Process(double delta)
    {
        if (!_breathingEnabled || _showAnchors || !IsVisibleInTree()) return;
        _breathTime = (_breathTime + delta) % 4.8;
        _breathingMaterial?.SetShaderParameter("breath", (float)(0.5 - 0.5 * System.Math.Cos(_breathTime / 4.8 * System.Math.Tau)));
    }

    public void SetBreathingPreview(bool enabled)
    {
        _breathingEnabled = enabled;
        _breathTime = 0;
        _breathingMaterial?.SetShaderParameter("breath", 0f);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        System.Array.Fill(_visible, true);

        build_ui();
        refresh_portrait();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        return_to_start_menu();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        if (_portraitView is not null) _portraitView.Texture = null;
        InkPortraitTestComposer.ReleaseResources();
    }

    private void build_ui()
    {
        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/start_menu/ink_mountain_dawn_v2.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var wash = new ColorRect { Color = new Color(0.035f, 0.055f, 0.052f, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(wash);

        var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var frame = new PanelContainer { Name = "AppearanceTestFrame", CustomMinimumSize = new Vector2(1460, 920) };
        frame.AddThemeStyleboxOverride("panel", panel_style(new Color(0.91f, 0.88f, 0.80f, 0.98f), new Color(0.24f, 0.29f, 0.25f, 0.95f)));
        center.AddChild(frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        frame.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);
        var heading = label("水墨立绘 · 九部位测试", 30, new Color("24312d"));
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(heading);
        var genderTitle = label("性别", 18, new Color("35423d"));
        genderTitle.CustomMinimumSize = new Vector2(50, 0);
        header.AddChild(genderTitle);
        var genderChoice = new OptionButton { Name = "GenderChoice", CustomMinimumSize = new Vector2(120, 42) };
        genderChoice.AddItem("男");
        genderChoice.AddItem("女");
        genderChoice.Select(1);
        apply_font(genderChoice, 18);
        genderChoice.ItemSelected += index =>
        {
            _female = index == 1;
            refresh_choices();
            refresh_portrait();
        };
        header.AddChild(genderChoice);
        var anchorGuide = new CheckButton { Name = "AnchorGuide", Text = "显示锚点", CustomMinimumSize = new Vector2(160, 42) };
        apply_font(anchorGuide, 18);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            anchorGuide.AddThemeColorOverride(state, new Color("35423d"));
        anchorGuide.Toggled += enabled => { _showAnchors = enabled; _breathingMaterial?.SetShaderParameter("breath", 0f); refresh_portrait(); };
        header.AddChild(anchorGuide);
        var breathing = new CheckButton { Name = "BreathingPreview", Text = "呼吸", ButtonPressed = true };
        apply_font(breathing, 18);
        breathing.AddThemeColorOverride("font_color", new Color("35423d"));
        breathing.Toggled += SetBreathingPreview;
        header.AddChild(breathing);
        var backTop = button("返回开始界面", 150, 42);
        backTop.Pressed += return_to_start_menu;
        header.AddChild(backTop);

        root.AddChild(label("相近五官已合并；眉、眼、鼻、嘴与前后发均可独立选择，不绑定整套。", 16, new Color("5b625c")));

        var work = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        work.AddThemeConstantOverride("separation", 18);
        root.AddChild(work);

        var portraitCard = new PanelContainer { CustomMinimumSize = new Vector2(590, 790), SizeFlagsVertical = SizeFlags.ExpandFill };
        portraitCard.AddThemeStyleboxOverride("panel", panel_style(new Color(0.80f, 0.79f, 0.73f, 0.92f), new Color(0.48f, 0.45f, 0.37f, 0.72f)));
        work.AddChild(portraitCard);
        var portraitMargin = new MarginContainer();
        portraitMargin.AddThemeConstantOverride("margin_left", 18);
        portraitMargin.AddThemeConstantOverride("margin_right", 18);
        portraitMargin.AddThemeConstantOverride("margin_top", 16);
        portraitMargin.AddThemeConstantOverride("margin_bottom", 16);
        portraitCard.AddChild(portraitMargin);
        var portraitColumn = new VBoxContainer();
        portraitColumn.AddThemeConstantOverride("separation", 8);
        portraitMargin.AddChild(portraitColumn);
        var portraitTitle = label("正面半身立绘", 20, new Color("303936"));
        portraitTitle.HorizontalAlignment = HorizontalAlignment.Center;
        portraitColumn.AddChild(portraitTitle);
        var stage = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        portraitColumn.AddChild(stage);
        _portraitView = new TextureRect
        {
            Name = "PortraitPreview",
            CustomMinimumSize = new Vector2(500, 750),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        stage.AddChild(_portraitView);
        _breathingMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/portrait_breathing.gdshader") };
        _portraitView.Material = _breathingMaterial;

        var settingsCard = new PanelContainer { CustomMinimumSize = new Vector2(780, 790), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        settingsCard.AddThemeStyleboxOverride("panel", panel_style(new Color(0.95f, 0.93f, 0.87f, 0.97f), new Color(0.48f, 0.45f, 0.37f, 0.72f)));
        work.AddChild(settingsCard);
        var settingsMargin = new MarginContainer();
        settingsMargin.AddThemeConstantOverride("margin_left", 28);
        settingsMargin.AddThemeConstantOverride("margin_right", 28);
        settingsMargin.AddThemeConstantOverride("margin_top", 22);
        settingsMargin.AddThemeConstantOverride("margin_bottom", 22);
        settingsCard.AddChild(settingsMargin);
        var settings = new VBoxContainer();
        settings.AddThemeConstantOverride("separation", 12);
        settingsMargin.AddChild(settings);

        settings.AddChild(label("九部位 · 独立选款", 24, new Color("303936")));
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 18);
        settings.AddChild(columns);
        for (int col = 0; col < 1; col++)
        {
            var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            group.AddThemeConstantOverride("separation", 3);
            columns.AddChild(group);
            for (int row = 0; row < 9; row++)
            {
                int part = row;
                if (part >= _partChecks.Length) break;
                var check = new CheckButton
                {
                    Name = "Part_" + InkPortraitTestComposer.PartKeys[part],
                    Text = InkPortraitTestComposer.PartNames[part],
                    ButtonPressed = true,
                    CustomMinimumSize = new Vector2(190, 40),
                };
                apply_font(check, 19);
                foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
                    check.AddThemeColorOverride(state, new Color("303936"));
                var line = new HBoxContainer();
                group.AddChild(line);
                line.AddChild(check);
                _partChecks[part] = check;
                int capturedPart = part;
                check.Toggled += shown => { _visible[capturedPart] = shown; refresh_portrait(); };
                var choice = new OptionButton
                {
                    Name = "Variant_" + InkPortraitTestComposer.PartKeys[part],
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(220, 40),
                };
                for (int variant = 0; variant < InkPortraitTestComposer.VariantCount(_female, part); variant++)
                    choice.AddItem(InkPortraitTestComposer.VariantName(_female, part, variant));
                apply_font(choice, 18);
                choice.ItemSelected += selected =>
                {
                    _variants[_female ? 1 : 0][capturedPart] = (int)selected;
                    refresh_portrait();
                };
                line.AddChild(choice);
                _partChoices[part] = choice;
            }
        }

        settings.AddChild(new HSeparator());
        _status = label("", 18, new Color("35423d"));
        _status.Name = "AppearanceStatus";
        _status.CustomMinimumSize = new Vector2(0, 62);
        settings.AddChild(_status);
        settings.AddChild(label("拼接顺序：后发 → 后领 → 脸型 → 前衣 → 鼻子与山根 → 眼睛 → 眉毛 → 嘴巴 → 面饰 → 前发。", 15, new Color("656a65")));

        var reset = button("显示全部九件", 160, 48);
        reset.Name = "ShowAllParts";
        reset.Pressed += show_all_parts;
        var actions = new HBoxContainer();
        settings.AddChild(actions);
        actions.AddChild(reset);
        var random = button("随机组合", 140, 48);
        random.Name = "RandomizeParts";
        random.Pressed += () =>
        {
            for (int part = 0; part < 9; part++)
            {
                int selected = (int)(GD.Randi() % (uint)InkPortraitTestComposer.VariantCount(_female, part));
                _variants[_female ? 1 : 0][part] = selected;
                _partChoices[part].Select(selected);
            }
            show_all_parts();
        };
        actions.AddChild(random);
        var baseline = button("恢复当前款", 150, 48);
        baseline.Name = "ResetVariants";
        baseline.Pressed += () =>
        {
            System.Array.Clear(_variants[_female ? 1 : 0]);
            foreach (var choice in _partChoices) choice.Select(0);
            show_all_parts();
        };
        actions.AddChild(baseline);
        var reference = button("查看女修", 150, 48);
        reference.Name = "ReferenceFemaleFace";
        reference.Pressed += () =>
        {
            _female = true;
            genderChoice.Select(1);
            refresh_choices();
            show_all_parts();
        };
        actions.AddChild(reference);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        settings.AddChild(spacer);
        settings.AddChild(label("素材画布：1024 × 1536　｜　用途：开始界面测试入口　｜　保存：仅预览", 14, new Color("77786f")));
    }

    private void refresh_portrait()
    {
        if (_portraitView is null) return;
        _portraitView.Texture = InkPortraitTestComposer.Compose(_female, _visible, _showAnchors, _variants[_female ? 1 : 0]);
        int shown = 0;
        foreach (bool visible in _visible) if (visible) shown++;
        _status.Text = $"当前：{(_female ? "女修" : "男修")}　｜　前发 {InkPortraitTestComposer.VariantCount(_female, 0)} 款／后发 {InkPortraitTestComposer.VariantCount(_female, 1)} 款　｜　已显示 {shown} / 9 件";
    }

    private void refresh_choices()
    {
        for (int part = 0; part < 9; part++)
        {
            var choice = _partChoices[part];
            choice.Clear();
            for (int variant = 0; variant < InkPortraitTestComposer.VariantCount(_female, part); variant++)
                choice.AddItem(InkPortraitTestComposer.VariantName(_female, part, variant));
            choice.Select(_variants[_female ? 1 : 0][part]);
        }
    }

    private void show_all_parts()
    {
        for (int part = 0; part < _visible.Length; part++)
        {
            _visible[part] = true;
            _partChecks[part].SetPressedNoSignal(true);
        }
        refresh_portrait();
    }

    private void return_to_start_menu() => GetTree().ChangeSceneToFile(StartMenuPath);

    private Label label(string text, int size, Color color)
    {
        var item = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        item.AddThemeFontSizeOverride("font_size", size);
        item.AddThemeColorOverride("font_color", color);
        apply_font(item, size);
        return item;
    }

    private Button button(string text, int width, int height)
    {
        var item = new Button { Text = text, CustomMinimumSize = new Vector2(width, height) };
        item.AddThemeColorOverride("font_color", new Color("263630"));
        item.AddThemeStyleboxOverride("normal", panel_style(new Color(0.84f, 0.84f, 0.76f, 1), new Color(0.31f, 0.37f, 0.32f, 0.8f)));
        item.AddThemeStyleboxOverride("hover", panel_style(new Color(0.92f, 0.89f, 0.79f, 1), new Color(0.58f, 0.47f, 0.29f, 0.95f)));
        item.AddThemeStyleboxOverride("pressed", panel_style(new Color(0.72f, 0.78f, 0.70f, 1), new Color(0.31f, 0.37f, 0.32f, 0.9f)));
        apply_font(item, 17);
        return item;
    }

    private void apply_font(Control control, int size)
    {
        if (_font is not null) control.AddThemeFontOverride("font", _font);
        control.AddThemeFontSizeOverride("font_size", size);
    }

    private static StyleBoxFlat panel_style(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
