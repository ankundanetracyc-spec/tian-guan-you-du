using Godot;
using System;
using System.Collections.Generic;

/// <summary>Live editor for the player's modular portrait and current ink battle preview.</summary>
public partial class AppearanceEditor : Control
{
    public event Action? AppearanceApplied;

    private sealed class ChoiceRow
    {
        public string Key = "";
        public string Title = "";
        public string Field = "";
        public string Part = "";
        public List<int> Values = [];
        public Label? ValueLabel;
        public Button? Previous;
        public Button? Next;
    }

    private Character? _character;
    private EntityData? _sourceData;
    private EntityData? _draftData;
    private string _gender = "male";
    private int _age;
    private PanelContainer? _frame;
    private VBoxContainer? _choiceBox;
    private PortraitRenderer? _portraitPreview;
    private BattlePaperDoll? _battlePreview;
    private readonly Dictionary<string, ChoiceRow> _choiceRows = [];

    private const int PaletteCount = 5;
    private static readonly string[] EarNames = { "标准耳", "小巧耳", "长耳", "圆耳垂", "贴耳" };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZAsRelative = false;
        ZIndex = 500;
        build_ui();
        Hide();
    }

    public void open_for(Character character)
    {
        if (!GodotObject.IsInstanceValid(character) || character.entity_data is null) return;
        _character = character;
        _sourceData = character.entity_data;
        _draftData = _sourceData.Duplicate(true) as EntityData ?? _sourceData;
        _gender = _sourceData is HumanData humanData && humanData.gender == 1 ? "female" : "male";
        _age = character.Get("current_age").AsInt32();
        normalize_choices(_draftData);
        build_choice_rows();
        refresh_controls();
        refresh_previews();
        if (_battlePreview is not null)
        {
            _battlePreview.Configure(new BattleEntityData
            {
                uid = character.unique_id,
                entity_name = character.entity_name,
                entity_type = (int)WorldEntity.EntityType.CHARACTER,
                faction = character.faction,
                gender = _gender == "female" ? 1 : 0,
                portrait_data = _draftData,
                battle_portrait_path = BattlePaperDoll.DefaultPortraitPath,
            });
            _battlePreview.Show();
        }
        Show();
        _frame?.MoveToFront();
    }

    public void open_for_data(EntityData data, int age = 18)
    {
        if (data is null) return;
        _character = null;
        _sourceData = data;
        _draftData = _sourceData.Duplicate(true) as EntityData ?? _sourceData;
        _gender = _sourceData is HumanData humanData && humanData.gender == 1 ? "female" : "male";
        _age = age;
        normalize_choices(_draftData);
        build_choice_rows();
        refresh_controls();
        refresh_previews();
        _battlePreview?.Hide();
        Show();
        _frame?.MoveToFront();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        cancel_edit();
        GetViewport().SetInputAsHandled();
    }

    private void build_ui()
    {
        var dim = new ColorRect { Name = "Dimmer", Color = new Color(0.025f, 0.035f, 0.032f, 0.76f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _frame = new PanelContainer { Name = "AppearanceEditorFrame", CustomMinimumSize = new Vector2(1420, 850), MouseFilter = MouseFilterEnum.Stop };
        _frame.AddThemeStyleboxOverride("panel", panel_style(new Color(.82f, .77f, .65f, .99f)));
        center.AddChild(_frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22); margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18); margin.AddThemeConstantOverride("margin_bottom", 18);
        _frame.AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);

        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 16); root.AddChild(heading);
        var title = text("角色仪容", 30); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(title);
        var close = new Button { Text = "×", CustomMinimumSize = new Vector2(48, 42) };
        close.Pressed += cancel_edit; heading.AddChild(close);
        root.AddChild(text("可调整局外立绘的五官与外观；局内形象采用留白面部与水墨衣装。", 16));

        var work = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        work.AddThemeConstantOverride("separation", 16); root.AddChild(work);

        var portraitCard = card(); portraitCard.CustomMinimumSize = new Vector2(430, 680); portraitCard.SizeFlagsVertical = SizeFlags.ExpandFill; work.AddChild(portraitCard);
        var portraitBox = new VBoxContainer(); portraitBox.AddThemeConstantOverride("separation", 8); portraitCard.AddChild(portraitBox);
        var portraitTitle = text("局外 · 半身立绘", 20); portraitTitle.HorizontalAlignment = HorizontalAlignment.Center; portraitBox.AddChild(portraitTitle);
        _portraitPreview = new PortraitRenderer { CustomMinimumSize = new Vector2(390, 610), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        portraitBox.AddChild(_portraitPreview);

        var battleCard = card(); battleCard.CustomMinimumSize = new Vector2(270, 680); battleCard.SizeFlagsVertical = SizeFlags.ExpandFill; work.AddChild(battleCard);
        var battleBox = new VBoxContainer(); battleBox.AddThemeConstantOverride("separation", 8); battleCard.AddChild(battleBox);
        var battleTitle = text("局内 · 水墨", 20); battleTitle.HorizontalAlignment = HorizontalAlignment.Center; battleBox.AddChild(battleTitle);
        var stage = new Control { Name = "InkBattleStage", CustomMinimumSize = new Vector2(250, 620), SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        var stageBackground = new ColorRect { Color = new Color("d4c8aa"), MouseFilter = MouseFilterEnum.Ignore };
        stageBackground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(stageBackground);
        _battlePreview = new BattlePaperDoll { Position = new Vector2(125, 385) };
        stage.AddChild(_battlePreview); battleBox.AddChild(stage);
        battleBox.AddChild(text("五官不进入战斗画面", 15));

        var settingsCard = card(); settingsCard.SizeFlagsHorizontal = SizeFlags.ExpandFill; settingsCard.SizeFlagsVertical = SizeFlags.ExpandFill; work.AddChild(settingsCard);
        var settingsMargin = new MarginContainer();
        settingsMargin.AddThemeConstantOverride("margin_left", 14); settingsMargin.AddThemeConstantOverride("margin_right", 14);
        settingsMargin.AddThemeConstantOverride("margin_top", 12); settingsMargin.AddThemeConstantOverride("margin_bottom", 12);
        settingsCard.AddChild(settingsMargin);
        var settings = new VBoxContainer(); settings.AddThemeConstantOverride("separation", 9); settingsMargin.AddChild(settings);
        settings.AddChild(text("五官与外观", 23));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        settings.AddChild(scroll);
        _choiceBox = new VBoxContainer(); _choiceBox.AddThemeConstantOverride("separation", 6); _choiceBox.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(_choiceBox);

        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 10); root.AddChild(footer);
        var random = new Button { Text = "随机一组", CustomMinimumSize = new Vector2(120, 44) }; random.Pressed += randomize_draft; footer.AddChild(random);
        var hint = text("选择会实时预览；点“应用”写入角色档案。", 14); hint.SizeFlagsHorizontal = SizeFlags.ExpandFill; hint.VerticalAlignment = VerticalAlignment.Center; footer.AddChild(hint);
        var cancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(120, 44) }; cancel.Pressed += cancel_edit; footer.AddChild(cancel);
        var apply = new Button { Text = "应用", CustomMinimumSize = new Vector2(140, 44) };
        apply.AddThemeColorOverride("font_color", new Color("f5edda"));
        apply.AddThemeStyleboxOverride("normal", panel_style(new Color(.25f, .30f, .25f)));
        apply.Pressed += apply_edit; footer.AddChild(apply);
    }

    private void build_choice_rows()
    {
        if (_choiceBox is null) return;
        foreach (var child in _choiceBox.GetChildren()) child.QueueFree();
        _choiceRows.Clear();
        add_choice("face", "脸型", "appearance_face_style", "face");
        add_choice("ear", "耳朵", "appearance_ear_style", "ear");
        add_choice("brow", "眉型", "appearance_brow_style", "brow");
        add_choice("eye", _gender == "male" ? "眉眼组合" : "眼型", "appearance_eye_style", "eye");
        add_choice("nose", "鼻型", "appearance_nose_style", "nose");
        add_choice("mouth", "口型", "appearance_mouth_style", "mouth");
        add_choice("makeup", "妆容", "appearance_makeup_style", "makeup");
        add_choice("hair_back", "后发", "appearance_hair_back_style", "hair_back");
        add_choice("hair_front", "前发", "appearance_hair_style", "hair_front");
        add_choice("clothes", "衣着", "appearance_clothes_style", "clothes");
        add_choice("beard", "胡须", "appearance_beard_style", "beard");
        add_choice("skin", "肤色", "appearance_skin_tone", "skin");
        add_choice("hair_color", "发色", "appearance_hair_color", "hair_color");
    }

    private void add_choice(string key, string title, string field, string part)
    {
        if (_choiceBox is null) return;
        var catalog = GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        var values = part switch
        {
            "skin" when catalog?.allows_color_tint(_gender, "skin") == true => new List<int> { 0, 1, 2, 3, 4 },
            "hair_color" when catalog?.allows_color_tint(_gender, "hair") == true => new List<int> { 0, 1, 2, 3, 4 },
            "skin" or "hair_color" => new List<int>(),
            _ => available_variants(part),
        };
        if (values.Count == 0) return;
        var rowData = new ChoiceRow { Key = key, Title = title, Field = field, Part = part, Values = values };
        var row = new HBoxContainer { Name = "Choice_" + key, CustomMinimumSize = new Vector2(0, 40) }; row.AddThemeConstantOverride("separation", 6); _choiceBox.AddChild(row);
        var label = text(title, 16); label.CustomMinimumSize = new Vector2(72, 0); row.AddChild(label);
        rowData.Previous = new Button { Name = "Previous", Text = "‹", CustomMinimumSize = new Vector2(42, 34), Disabled = values.Count <= 1 };
        rowData.Previous.Pressed += () => change_choice(key, -1); row.AddChild(rowData.Previous);
        rowData.ValueLabel = text("—", 15); rowData.ValueLabel.AutowrapMode = TextServer.AutowrapMode.Off; rowData.ValueLabel.HorizontalAlignment = HorizontalAlignment.Center; rowData.ValueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(rowData.ValueLabel);
        rowData.Next = new Button { Name = "Next", Text = "›", CustomMinimumSize = new Vector2(42, 34), Disabled = values.Count <= 1 };
        rowData.Next.Pressed += () => change_choice(key, 1); row.AddChild(rowData.Next);
        _choiceRows[key] = rowData;
    }

    private List<int> available_variants(string part)
    {
        var result = new List<int>();
        if (GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog") is not { } catalog) return result;
        foreach (Godot.Collections.Dictionary entry in catalog.get_parts(_gender, part))
        {
            int index = entry.GetValueOrDefault("variant_index", -1).AsInt32();
            if (index >= 0 && !result.Contains(index)) result.Add(index);
        }
        result.Sort();
        if (part == "beard" && _age < 35) result.RemoveAll(value => value != 0);
        return result;
    }

    private void normalize_choices(EntityData data)
    {
        var seed = data.appearance_seed == 0 ? 1 : data.appearance_seed;
        data.appearance_face_style = normalize(data.appearance_face_style, "face", seed);
        data.appearance_ear_style = normalize(data.appearance_ear_style, "ear", seed);
        int eyeStyle = data.appearance_eye_style >= 0 ? data.appearance_eye_style : data.appearance_eyebrow_style;
        data.appearance_eye_style = normalize(eyeStyle, "eye", seed);
        data.appearance_eyebrow_style = data.appearance_eye_style;
        data.appearance_brow_style = normalize(data.appearance_brow_style, "brow", seed);
        data.appearance_nose_style = normalize(data.appearance_nose_style, "nose", seed);
        data.appearance_mouth_style = normalize(data.appearance_mouth_style, "mouth", seed);
        data.appearance_makeup_style = normalize(data.appearance_makeup_style, "makeup", seed);
        data.appearance_hair_back_style = normalize(data.appearance_hair_back_style, "hair_back", seed);
        data.appearance_hair_style = normalize(data.appearance_hair_style, "hair_front", seed);
        data.appearance_clothes_style = normalize(data.appearance_clothes_style, "clothes", seed);
        data.appearance_beard_style = normalize(data.appearance_beard_style, "beard", seed);
        var catalog = GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        data.appearance_skin_tone = catalog?.allows_color_tint(_gender, "skin") == true ? normalize_palette(data.appearance_skin_tone, seed) : -1;
        data.appearance_hair_color = catalog?.allows_color_tint(_gender, "hair") == true ? normalize_palette(data.appearance_hair_color, seed + 17) : -1;
    }

    private int normalize(int current, string part, int seed)
    {
        var values = available_variants(part);
        if (values.Count == 0) return Math.Max(0, current);
        if (values.Contains(current)) return current;
        return values[stable_pick(seed, part, values.Count)];
    }

    private static int normalize_palette(int current, int seed)
    {
        if (current >= 0 && current < PaletteCount) return current;
        return stable_pick(seed, "palette", PaletteCount);
    }

    private static int stable_pick(int seed, string key, int count)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Math.Max(1, seed) ^ unchecked((ulong)GD.Hash(key)) };
        return rng.RandiRange(0, Math.Max(0, count - 1));
    }

    private void refresh_controls()
    {
        if (_draftData is null) return;
        foreach (var row in _choiceRows.Values)
        {
            int current = get_field(_draftData, row.Field);
            int index = row.Values.IndexOf(current);
            if (row.ValueLabel is not null)
                row.ValueLabel.Text = row.Values.Count == 0 ? "暂无素材" :
                    row.Key == "ear" && current >= 0 && current < EarNames.Length
                        ? $"{EarNames[current]}  {Math.Max(0, index) + 1} / {row.Values.Count}"
                        : $"{GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog")?.get_variant_name(_gender, row.Part, current) ?? "默认"}  {Math.Max(0, index) + 1} / {row.Values.Count}";
            if (row.Previous is not null) row.Previous.Disabled = row.Values.Count <= 1;
            if (row.Next is not null) row.Next.Disabled = row.Values.Count <= 1;
        }
    }

    private void change_choice(string key, int delta)
    {
        if (_draftData is null || !_choiceRows.TryGetValue(key, out var row) || row.Values.Count <= 1) return;
        int current = row.Values.IndexOf(get_field(_draftData, row.Field));
        if (current < 0) current = 0;
        int next = row.Values[(current + delta + row.Values.Count) % row.Values.Count];
        set_field(_draftData, row.Field, next);
        if (key == "eye") _draftData.appearance_eyebrow_style = next;
        refresh_controls(); refresh_previews();
    }

    private void randomize_draft()
    {
        if (_draftData is null) return;
        foreach (var row in _choiceRows.Values)
        {
            if (row.Values.Count == 0) continue;
            var rng = new RandomNumberGenerator { Seed = GD.Randi() };
            set_field(_draftData, row.Field, row.Values[rng.RandiRange(0, row.Values.Count - 1)]);
            if (row.Key == "eye") _draftData.appearance_eyebrow_style = _draftData.appearance_eye_style;
        }
        refresh_controls(); refresh_previews();
    }

    private void refresh_previews()
    {
        if (_draftData is null) return;
        var catalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var context = catalog.get_context(_draftData, _gender, _age, _draftData.faction_name, _character?.cultivation_realm ?? "凡人");
        _portraitPreview?.configure(_draftData, context, catalog.get_requested_appearance(_draftData));
    }

    private void apply_edit()
    {
        if (_sourceData is null || _draftData is null) return;
        _sourceData.appearance_seed = _draftData.appearance_seed;
        _sourceData.appearance_face_style = _draftData.appearance_face_style;
        _sourceData.appearance_ear_style = _draftData.appearance_ear_style;
        _sourceData.appearance_eyebrow_style = _draftData.appearance_eyebrow_style;
        _sourceData.appearance_eye_style = _draftData.appearance_eye_style;
        _sourceData.appearance_brow_style = _draftData.appearance_brow_style;
        _sourceData.appearance_nose_style = _draftData.appearance_nose_style;
        _sourceData.appearance_mouth_style = _draftData.appearance_mouth_style;
        _sourceData.appearance_makeup_style = _draftData.appearance_makeup_style;
        _sourceData.appearance_skin_tone = _draftData.appearance_skin_tone;
        _sourceData.appearance_hair_color = _draftData.appearance_hair_color;
        _sourceData.appearance_hair_style = _draftData.appearance_hair_style;
        _sourceData.appearance_hair_back_style = _draftData.appearance_hair_back_style;
        _sourceData.appearance_clothes_style = _draftData.appearance_clothes_style;
        _sourceData.appearance_beard_style = _draftData.appearance_beard_style;
        _sourceData.EmitChanged();
        AppearanceApplied?.Invoke();
        Hide();
    }

    private void cancel_edit() { _character = null; _sourceData = null; _draftData = null; Hide(); }

    private static int get_field(EntityData data, string field) => field switch
    {
        "appearance_face_style" => data.appearance_face_style,
        "appearance_ear_style" => data.appearance_ear_style,
        "appearance_brow_style" => data.appearance_brow_style,
        "appearance_eye_style" => data.appearance_eye_style,
        "appearance_nose_style" => data.appearance_nose_style,
        "appearance_mouth_style" => data.appearance_mouth_style,
        "appearance_makeup_style" => data.appearance_makeup_style,
        "appearance_hair_style" => data.appearance_hair_style,
        "appearance_hair_back_style" => data.appearance_hair_back_style,
        "appearance_clothes_style" => data.appearance_clothes_style,
        "appearance_beard_style" => data.appearance_beard_style,
        "appearance_skin_tone" => data.appearance_skin_tone,
        "appearance_hair_color" => data.appearance_hair_color,
        _ => 0,
    };

    private static void set_field(EntityData data, string field, int value)
    {
        switch (field)
        {
            case "appearance_face_style": data.appearance_face_style = value; break;
            case "appearance_ear_style": data.appearance_ear_style = value; break;
            case "appearance_brow_style": data.appearance_brow_style = value; break;
            case "appearance_eye_style": data.appearance_eye_style = value; break;
            case "appearance_nose_style": data.appearance_nose_style = value; break;
            case "appearance_mouth_style": data.appearance_mouth_style = value; break;
            case "appearance_makeup_style": data.appearance_makeup_style = value; break;
            case "appearance_hair_style": data.appearance_hair_style = value; break;
            case "appearance_hair_back_style": data.appearance_hair_back_style = value; break;
            case "appearance_clothes_style": data.appearance_clothes_style = value; break;
            case "appearance_beard_style": data.appearance_beard_style = value; break;
            case "appearance_skin_tone": data.appearance_skin_tone = value; break;
            case "appearance_hair_color": data.appearance_hair_color = value; break;
        }
    }

    private static Label text(string value, int size)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(.13f, .11f, .08f));
        return label;
    }

    private static PanelContainer card()
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.91f, .87f, .77f, .96f))); return panel;
    }

    private static StyleBoxFlat panel_style(Color background) => new()
    {
        BgColor = background, BorderColor = new Color(.37f, .31f, .21f, .62f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10, ContentMarginBottom = 10,
    };
}
