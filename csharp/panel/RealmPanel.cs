using Godot;

/// <summary>境界总览只读取既有境界配置，不在界面内结算新数值。</summary>
public partial class RealmPanel : Control
{
    private static readonly string[] Stages =
    [
        "云息一层", "云息二层", "云息三层", "云息四层",
        "云息五层", "云息六层", "云息七层", "云息八层",
        "云息九层", "云息十层", "云息十一层", "云息十二层", "云息十三层"
    ];

    private Character? _player;
    private VBoxContainer? _stageRail;
    private HBoxContainer? _majorTabs;
    private Label? _bonuses;
    private TextureRect? _outerMist;
    private TextureRect? _innerMist;
    private TextureRect? _coreLight;
    private double _motionTime;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 650;
        Build();
        Hide();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _outerMist is null || _innerMist is null || _coreLight is null) return;

        _motionTime += delta;
        // 旋转只发生在纸面内部的裁切节点；每帧按实际尺寸设定中心，避免缩放时漂移越界。
        _outerMist.PivotOffset = _outerMist.Size * .5f;
        _innerMist.PivotOffset = _innerMist.Size * .5f;
        _coreLight.PivotOffset = _coreLight.Size * .5f;
        _outerMist.Rotation = (float)(_motionTime * 0.018d);
        _innerMist.Rotation = (float)(-_motionTime * 0.028d);
        var mistBreath = 1f + 0.018f * Mathf.Sin((float)(_motionTime * 0.72d));
        _innerMist.Scale = new Vector2(.74f * mistBreath, .74f * mistBreath);
        var lightBreath = .26f + .15f * (0.5f + 0.5f * Mathf.Sin((float)(_motionTime * 1.05d)));
        _coreLight.Modulate = new Color(.96f, .97f, .92f, lightBreath);
        var lightScale = 0.97f + 0.03f * Mathf.Sin((float)(_motionTime * 1.05d));
        _coreLight.Scale = new Vector2(lightScale, lightScale);
    }

    public bool is_unlocked(Character? target) =>
        GodotObject.IsInstanceValid(target) &&
        GetNodeOrNull<RealmManager>("/root/RealmManager")?.is_realm_after_or_equal(target!.cultivation_realm, "云息一层") == true;

    public void show_for(Character target)
    {
        if (!is_unlocked(target)) return;
        _player = target;
        Rebuild();
        Refresh();
        Show();
        MoveToFront();
    }

    private void Build()
    {
        // 全屏暗宣纸；背景本身保持实体色，不在代码中再次调色或做透明遮罩。
        var paper = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
        paper.SetAnchorsPreset(LayoutPreset.FullRect);
        paper.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, 0));
        AddChild(paper);

        var background = Texture("res://textures/ui/realm_yunxi_paper_v6.png", TextureRect.StretchModeEnum.KeepAspectCovered);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        paper.AddChild(background);

        // 复用粒子库的透明环流与柔光，且在独立裁切框中旋转。
        var cloudEye = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        cloudEye.SetAnchorsPreset(LayoutPreset.FullRect);
        // 主视觉放在纸面正中，为左下加成和右上题字各留一角。
        cloudEye.AnchorLeft = .28f; cloudEye.AnchorTop = .14f; cloudEye.AnchorRight = .72f; cloudEye.AnchorBottom = .86f;
        cloudEye.OffsetLeft = 0; cloudEye.OffsetTop = 0; cloudEye.OffsetRight = 0; cloudEye.OffsetBottom = 0;
        paper.AddChild(cloudEye);

        _outerMist = Texture("res://textures/particle_vfx/1024/alpha/effect_4.png", TextureRect.StretchModeEnum.KeepAspectCentered);
        _outerMist.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _outerMist.Modulate = new Color(.86f, .88f, .84f, .58f);
        cloudEye.AddChild(_outerMist);

        _innerMist = Texture("res://textures/particle_vfx/1024/alpha/effect_4.png", TextureRect.StretchModeEnum.KeepAspectCentered);
        _innerMist.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _innerMist.Modulate = new Color(.98f, .99f, .94f, .44f);
        _innerMist.Scale = new Vector2(.74f, .74f);
        cloudEye.AddChild(_innerMist);

        _coreLight = Texture("res://textures/particle_vfx/1024/alpha/spotlight_1.png", TextureRect.StretchModeEnum.KeepAspectCentered);
        _coreLight.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _coreLight.Modulate = new Color(.96f, .97f, .92f, .26f);
        cloudEye.AddChild(_coreLight);

        var root = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 44; root.OffsetTop = 32; root.OffsetRight = -44; root.OffsetBottom = -38;
        root.AddThemeConstantOverride("separation", 16);
        paper.AddChild(root);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(header);
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var close = new Button { Text = "关闭", CustomMinimumSize = new Vector2(78, 38), MouseFilter = MouseFilterEnum.Stop };
        ButtonStyle(close, false);
        close.Pressed += Hide;
        header.AddChild(close);
        root.AddChild(Separator());

        // 右上题字、左下加成与中部云眼三者互不挤占。
        var content = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(content);
        var poem = Texture("res://textures/ui/realm_yunxi_poem_brush_black_v3.png", TextureRect.StretchModeEnum.KeepAspectCentered);
        poem.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        poem.AnchorLeft = .61f; poem.AnchorTop = .01f; poem.AnchorRight = .99f; poem.AnchorBottom = .58f;
        poem.OffsetLeft = 0; poem.OffsetTop = 0; poem.OffsetRight = 0; poem.OffsetBottom = 0;
        content.AddChild(poem);

        var bonus = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bonus.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bonus.AnchorLeft = .04f; bonus.AnchorTop = .70f; bonus.AnchorRight = .28f; bonus.AnchorBottom = .96f;
        bonus.OffsetLeft = 0; bonus.OffsetTop = 0; bonus.OffsetRight = 0; bonus.OffsetBottom = 0;
        bonus.AddThemeConstantOverride("separation", 10);
        content.AddChild(bonus);
        bonus.AddChild(Separator());
        // 纸面底色复杂且偏暗，使用明亮的暖色文字保证加成清楚可读。
        bonus.AddChild(Label("本层加成", 18, new Color(.95f, .84f, .48f)));
        _bonuses = Label("", 17, new Color(.96f, .94f, .87f));
        _bonuses.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bonus.AddChild(_bonuses);

        // 大境界与小境界都在纸面外；小境界标签水平书写，由下至上堆叠。
        _majorTabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Stop };
        _majorTabs.SetAnchorsPreset(LayoutPreset.TopWide);
        _majorTabs.OffsetTop = 12; _majorTabs.OffsetBottom = 64;
        _majorTabs.ZIndex = 20;
        AddChild(_majorTabs);

        _stageRail = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Stop };
        _stageRail.SetAnchorsPreset(LayoutPreset.FullRect);
        _stageRail.AnchorLeft = .12f; _stageRail.AnchorRight = .12f;
        // 避开左下角加成说明，同时为云息十三层预留完整高度。
        _stageRail.AnchorTop = .10f; _stageRail.AnchorBottom = .68f;
        _stageRail.OffsetLeft = -205; _stageRail.OffsetTop = 0; _stageRail.OffsetRight = -15; _stageRail.OffsetBottom = 0;
        _stageRail.AddThemeConstantOverride("separation", 6);
        _stageRail.ZIndex = 20;
        AddChild(_stageRail);
    }

    private void Rebuild()
    {
        RebuildMajorTabs();
        if (_stageRail is null) return;
        foreach (var child in _stageRail.GetChildren()) child.QueueFree();
        var current = Index(_player?.cultivation_realm ?? "");
        if (current < 0) return;

        // 显示已到达的全部层数，并在末尾保留下一层预览。
        for (var stage = 0; stage <= Math.Min(current + 1, Stages.Length - 1); stage++)
            AddStage(stage, stage == current);
    }

    private void RebuildMajorTabs()
    {
        if (_majorTabs is null) return;
        foreach (var child in _majorTabs.GetChildren()) child.QueueFree();
        // 当前只有云息；新大境界须待实际突破进入后才会追加，绝不预置灰色占位。
        if (_player?.cultivation_realm.StartsWith("云息") != true) return;
        var tab = new Button { Text = "云息", CustomMinimumSize = new Vector2(138, 48), MouseFilter = MouseFilterEnum.Ignore };
        ButtonStyle(tab, true);
        _majorTabs.AddChild(tab);
    }

    private void AddStage(int stage, bool current)
    {
        if (_stageRail is null) return;
        var button = new Button
        {
            Text = Stages[stage],
            CustomMinimumSize = new Vector2(180, 40),
            Alignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ButtonStyle(button, current);
        _stageRail.AddChild(button);
    }

    private void Refresh()
    {
        if (_player is null || _bonuses is null) return;
        var config = GetNodeOrNull<RealmManager>("/root/RealmManager")?.get_realm_config(_player.cultivation_realm);
        _bonuses.Text = Bonuses(config);
    }

    private static int Index(string realm)
    {
        if (!realm.StartsWith("云息") || !realm.EndsWith("层")) return -1;
        return realm[2..^1] switch
        {
            "一" => 0, "二" => 1, "三" => 2, "四" => 3, "五" => 4, "六" => 5, "七" => 6,
            "八" => 7, "九" => 8, "十" => 9, "十一" => 10, "十二" => 11, "十三" => 12,
            _ => -1
        };
    }

    private static string Bonuses(RealmConfig? config)
    {
        if (config is null) return "此层的正式配置尚未接入。";
        var values = new List<string>();
        foreach (Variant key in config.realm_fixed_bonuses.Keys) values.Add($"{AttributeName(key.AsString())} +{config.realm_fixed_bonuses[key]}");
        foreach (Variant key in config.realm_multiplier_bonuses.Keys) values.Add($"{AttributeName(key.AsString())} +{config.realm_multiplier_bonuses[key].AsSingle() * 100:0}%");
        return values.Count == 0 ? "本层尚无可展示的固定加成。" : string.Join("\n", values);
    }

    private static string AttributeName(string key) => key switch
    {
        "current_age" => "当前年龄", "gender" => "性别", "physique" => "体魄", "intelligence" => "灵慧",
        "agility" => "敏捷", "soul" => "神魂", "fortune" => "机缘", "comprehension" => "悟性",
        "charisma" => "魅力", "spirit_strands" => "灵力", "core_count" => "云息灵核",
        "max_health" => "生命上限", "max_mana" => "法力上限", "max_stamina" => "精力上限",
        "max_mind" => "心境上限", "physical_defense" => "物理防御", "precision" => "精准",
        "dodge" => "身法", "crit_resist" => "护心", "initiative_speed" => "行动速度",
        "cards_per_turn" => "每回合出牌数", "initial_hand_size" => "初始手牌数", "sub_slot_increase" => "辅修槽位",
        "health_regen_rate" => "生命恢复", "stamina_regen_rate" => "精力恢复", "mind_regen_rate" => "心境恢复",
        "learning_rate" => "研读效率", "cultivation_rate" => "修炼效率", "critical" => "会心",
        "crit_damage_mult" => "会心伤害", "sword_proficiency" => "剑术造诣", "blade_proficiency" => "刀术造诣",
        "spear_proficiency" => "枪术造诣", "body_proficiency" => "锻躯造诣", "magic_proficiency" => "术法造诣",
        "forging_proficiency" => "炼器造诣", "talisman_proficiency" => "符箓造诣",
        "formation_proficiency" => "阵法造诣", "alchemy_proficiency" => "炼丹造诣",
        "mana_regen_rate" => "法力恢复", "max_spirit_perception" => "灵识上限", "max_age" => "寿元上限",
        _ => "其他加成"
    };

    private static TextureRect Texture(string path, TextureRect.StretchModeEnum stretch) => new()
    {
        Texture = ResourceLoader.Load<Texture2D>(path),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = stretch,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private static Label Label(string text, int size, Color color)
    {
        var result = new Label { Text = text };
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", color);
        return result;
    }

    private static HSeparator Separator()
    {
        var separator = new HSeparator();
        separator.Modulate = new Color(.18f, .16f, .12f, .48f);
        return separator;
    }

    private static StyleBoxFlat Box(Color background, Color border, int width, int radius = 8) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    private static void ButtonStyle(Button button, bool current)
    {
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", current ? new Color(.96f, .93f, .82f) : new Color(.82f, .78f, .65f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, .95f, .76f));
        button.AddThemeStyleboxOverride("normal", OpaqueRealmTag());
        button.AddThemeStyleboxOverride("hover", OpaqueRealmTag());
    }

    private static StyleBoxTexture OpaqueRealmTag() => new()
    {
        Texture = ResourceLoader.Load<Texture2D>("res://textures/ui/realm_tag_opaque_v1.png")
    };
}
