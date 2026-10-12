using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Fullscreen, two-step new-journey setup.</summary>
public partial class StartMenu
{
    private sealed class CreationPage
    {
        public Control Root = null!;
        public Action Refresh = () => { };
    }

    private sealed class AppearanceChoiceControl
    {
        public string Field = "";
        public OptionButton Selector = null!;
        public List<int> Values = [];
    }

    private sealed class AttributeChoiceControl
    {
        public string Key = "";
        public Label Value = null!;
        public Button Increase = null!;
        public float Increment;
    }

    private void build_new_game_creation()
    {
        if (_creationOverlay is not null)
        {
            _creationOverlay.Show();
            _creationOverlay.MoveToFront();
            return;
        }

        var profile = GetNodeOrNull<GameStartProfile>("/root/GameStartProfile");
        if (profile is null) return;
        if (!profile.has_fated_roll) profile.roll_fated_aptitude();
        if (profile.player_appearance_data.appearance_seed == 0) profile.randomize_player_appearance();

        var overlay = new Control { Name = "NewGameSetup", MouseFilter = MouseFilterEnum.Stop, ZIndex = 200 };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        _creationOverlay = overlay;

        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>(NewGameBackdropPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(backdrop);

        var veil = new ColorRect { Color = new Color(.025f, .04f, .035f, .64f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(veil);

        var layout = new VBoxContainer { Name = "CreationLayout", MouseFilter = MouseFilterEnum.Pass };
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layout.OffsetLeft = 34;
        layout.OffsetRight = -34;
        layout.OffsetTop = 20;
        layout.OffsetBottom = -20;
        layout.AddThemeConstantOverride("separation", 12);
        overlay.AddChild(layout);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 54) };
        header.AddThemeConstantOverride("separation", 10);
        layout.AddChild(header);

        var heading = new Label { Text = "新建修行旅程", VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddThemeFontSizeOverride("font_size", 26);
        heading.AddThemeColorOverride("font_color", new Color("f3e6c7"));
        header.AddChild(heading);

        var appearanceTab = PlainButton("一　立绘与姓名");
        appearanceTab.CustomMinimumSize = new Vector2(190, 46);
        header.AddChild(appearanceTab);
        var aptitudeTab = PlainButton("二　资质分配");
        aptitudeTab.CustomMinimumSize = new Vector2(180, 46);
        header.AddChild(aptitudeTab);
        var close = PlainButton("返回");
        close.CustomMinimumSize = new Vector2(90, 46);
        close.Pressed += overlay.Hide;
        header.AddChild(close);

        var appearancePage = build_appearance_page(profile);
        var aptitudePage = build_aptitude_page(profile, appearancePage.Refresh);
        var content = new Control { Name = "CreationPageHost", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        layout.AddChild(content);
        appearancePage.Root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        aptitudePage.Root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.AddChild(appearancePage.Root);
        content.AddChild(aptitudePage.Root);

        var footer = new HBoxContainer { CustomMinimumSize = new Vector2(0, 54) };
        footer.AddThemeConstantOverride("separation", 12);
        layout.AddChild(footer);
        var pageHint = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        pageHint.AddThemeColorOverride("font_color", new Color("f3e6c7"));
        footer.AddChild(pageHint);
        var next = PlainButton("下一页");
        next.CustomMinimumSize = new Vector2(190, 50);
        footer.AddChild(next);

        var currentPage = 0;
        void show_page(int page)
        {
            currentPage = Mathf.Clamp(page, 0, 1);
            appearancePage.Root.Visible = currentPage == 0;
            aptitudePage.Root.Visible = currentPage == 1;
            pageHint.Text = currentPage == 0
                ? "选择姓名、性别与立绘部件；所有变化都会即时预览。"
                : "分配剩余资质点并选择初始功法；灵根由开局随机判定。";
            next.Text = currentPage == 0 ? "下一页" : "入世";
            style_creation_tab(appearanceTab, currentPage == 0);
            style_creation_tab(aptitudeTab, currentPage == 1);
            if (currentPage == 0) appearancePage.Refresh();
            else aptitudePage.Refresh();
        }

        appearanceTab.Pressed += () => show_page(0);
        aptitudeTab.Pressed += () => show_page(1);
        next.Pressed += () =>
        {
            if (currentPage == 0)
            {
                show_page(1);
                return;
            }

            profile.player_name = string.IsNullOrWhiteSpace(profile.player_name) ? "苏醒者" : profile.player_name.Trim();
            GetNodeOrNull<GameSaveManager>("/root/GameSaveManager")?.begin_new_game(profile.player_name);
            GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn");
        };
        show_page(0);
    }

    private CreationPage build_appearance_page(GameStartProfile profile)
    {
        var page = new HBoxContainer { Name = "AppearancePage", Visible = false, MouseFilter = MouseFilterEnum.Pass };
        page.AddThemeConstantOverride("separation", 16);
        CreationPage result = new() { Root = page };

        var identityCard = creation_card("人物信息", "立绘采用当前启用的正式素材；没有对应素材的部件不会显示。");
        identityCard.Panel.CustomMinimumSize = new Vector2(430, 0);
        identityCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        identityCard.Panel.SizeFlagsStretchRatio = 0.82f;
        page.AddChild(identityCard.Panel);

        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 10);
        var nameLabel = creation_label("姓名", 17);
        nameLabel.CustomMinimumSize = new Vector2(56, 0);
        nameLabel.VerticalAlignment = VerticalAlignment.Center;
        nameRow.AddChild(nameLabel);
        var nameInput = new LineEdit { PlaceholderText = "苏醒者", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 42) };
        style_creation_input(nameInput);
        nameInput.TextChanged += value => profile.player_name = value;
        nameRow.AddChild(nameInput);
        identityCard.Body.AddChild(nameRow);

        var genderRow = new HBoxContainer();
        genderRow.AddThemeConstantOverride("separation", 10);
        var genderLabel = creation_label("性别", 17);
        genderLabel.CustomMinimumSize = new Vector2(56, 0);
        genderLabel.VerticalAlignment = VerticalAlignment.Center;
        genderRow.AddChild(genderLabel);
        var genderChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 42) };
        style_creation_option(genderChoice);
        genderChoice.AddItem("男修");
        genderChoice.AddItem("女修");
        genderChoice.ItemSelected += index =>
        {
            profile.player_gender = (int)index;
            reset_appearance_parts(profile.player_appearance_data, false);
            result.Refresh();
        };
        genderRow.AddChild(genderChoice);
        identityCard.Body.AddChild(genderRow);

        var portraitHeading = creation_label("半身立绘预览", 19);
        portraitHeading.HorizontalAlignment = HorizontalAlignment.Center;
        identityCard.Body.AddChild(portraitHeading);
        var portrait = new PortraitRenderer
        {
            Name = "StartPortraitPreview",
            CustomMinimumSize = new Vector2(300, 480),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        identityCard.Body.AddChild(portrait);
        var portraitNote = creation_label("发型、五官与衣着可在右侧逐项选择。", 14);
        portraitNote.HorizontalAlignment = HorizontalAlignment.Center;
        identityCard.Body.AddChild(portraitNote);

        var appearanceCard = creation_card("外观部件", "点选部件款式即可改变立绘；“随机”会按当前立绘种子稳定呈现。");
        appearanceCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        appearanceCard.Panel.SizeFlagsStretchRatio = 1.18f;
        page.AddChild(appearanceCard.Panel);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        appearanceCard.Body.AddChild(scroll);
        var controls = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        controls.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(controls);
        var componentGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        componentGrid.AddThemeConstantOverride("h_separation", 12);
        componentGrid.AddThemeConstantOverride("v_separation", 8);
        controls.AddChild(componentGrid);
        var componentChoices = new List<AppearanceChoiceControl>();
        var skinOptions = new[] { "浅瓷", "暖白", "麦色", "棕褐", "深肤" };
        var hairOptions = new[] { "本色", "浅棕", "栗色", "灰白", "赤褐" };

        var portraitButtons = new HBoxContainer();
        portraitButtons.AddThemeConstantOverride("separation", 10);
        var randomAppearance = PlainButton("随机组合");
        randomAppearance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        portraitButtons.AddChild(randomAppearance);
        var resetAppearance = PlainButton("恢复基准");
        resetAppearance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        portraitButtons.AddChild(resetAppearance);
        appearanceCard.Body.AddChild(portraitButtons);

        var catalog = GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        var portraitCatalog = GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog");
        var lastGender = profile.player_gender;

        void refresh_portrait()
        {
            if (portraitCatalog is null) return;
            var data = profile.create_player_appearance_preview();
            var genderName = data.gender == 1 ? "female" : "male";
            if (catalog?.allows_color_tint(genderName, "skin") != true) data.appearance_skin_tone = -1;
            if (catalog?.allows_color_tint(genderName, "hair") != true) data.appearance_hair_color = -1;
            var context = portraitCatalog.get_context(data, genderName, 18, data.faction_name, "凡人");
            var requested = portraitCatalog.get_requested_appearance(data);
            if (data.appearance_hair_back_style < 0) requested["hair_back"] = "";
            portrait.configure(data, context, requested);
        }

        List<int> available_variants(string part)
        {
            var values = new List<int>();
            if (catalog is null) return values;
            foreach (Godot.Collections.Dictionary entry in catalog.get_parts(profile.player_gender == 1 ? "female" : "male", part))
            {
                var index = entry.GetValueOrDefault("variant_index", -1).AsInt32();
                if (index >= 0 && !values.Contains(index)) values.Add(index);
            }
            if (part == "beard") values.RemoveAll(index => index != 0);
            values.Sort();
            return values;
        }

        void add_component_choice(string title, string part, string field, List<int> values, string[]? labels = null)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(250, 66), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 3);
            cell.AddChild(creation_label(title, 15));
            var selector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 38) };
            style_creation_option(selector);
            selector.AddItem("随机");
            foreach (var value in values)
                selector.AddItem(labels is not null && value < labels.Length ? labels[value] :
                    catalog?.get_variant_name(profile.player_gender == 1 ? "female" : "male", part, value) ?? $"款式 {value + 1}");
            var row = new AppearanceChoiceControl { Field = field, Selector = selector, Values = values };
            selector.ItemSelected += selected =>
            {
                var chosen = selected == 0 ? -1 : row.Values[(int)selected - 1];
                set_appearance_field(profile.player_appearance_data, row.Field, chosen);
                refresh_portrait();
            };
            componentChoices.Add(row);
            cell.AddChild(selector);
            componentGrid.AddChild(cell);
        }

        void rebuild_component_choices()
        {
            foreach (var child in componentGrid.GetChildren())
            {
                componentGrid.RemoveChild(child);
                child.QueueFree();
            }
            componentChoices.Clear();
            var genderName = profile.player_gender == 1 ? "female" : "male";
            var browVariants = available_variants("brow");
            foreach (var component in new[]
            {
                (Part: "face", Title: "脸型", Field: "appearance_face_style"),
                (Part: "ear", Title: "耳朵", Field: "appearance_ear_style"),
                (Part: "brow", Title: "眉型", Field: "appearance_brow_style"),
                (Part: "eye", Title: genderName == "male" && browVariants.Count == 0 ? "眉眼组合" : "眼型", Field: "appearance_eye_style"),
                (Part: "nose", Title: "鼻型", Field: "appearance_nose_style"),
                (Part: "mouth", Title: "口型", Field: "appearance_mouth_style"),
                (Part: "makeup", Title: "妆容", Field: "appearance_makeup_style"),
                (Part: "beard", Title: "胡须", Field: "appearance_beard_style"),
                (Part: "hair_back", Title: "后发", Field: "appearance_hair_back_style"),
                (Part: "hair_front", Title: "前发", Field: "appearance_hair_style"),
                (Part: "clothes", Title: "衣着", Field: "appearance_clothes_style"),
            })
            {
                var values = available_variants(component.Part);
                if (values.Count > 0) add_component_choice(component.Title, component.Part, component.Field, values);
            }
            if (catalog?.allows_color_tint(genderName, "skin") == true)
                add_component_choice("肤色", "skin", "appearance_skin_tone", Enumerable.Range(0, skinOptions.Length).ToList(), skinOptions);
            if (catalog?.allows_color_tint(genderName, "hair") == true)
                add_component_choice("发色", "hair_color", "appearance_hair_color", Enumerable.Range(0, hairOptions.Length).ToList(), hairOptions);
        }

        void refresh()
        {
            if (lastGender != profile.player_gender)
            {
                lastGender = profile.player_gender;
                rebuild_component_choices();
            }
            genderChoice.Select(Math.Clamp(profile.player_gender, 0, 1));
            if (nameInput.Text != profile.player_name) nameInput.Text = profile.player_name;
            foreach (var choice in componentChoices)
            {
                var selectedValue = get_appearance_field(profile.player_appearance_data, choice.Field);
                var selectedIndex = choice.Values.IndexOf(selectedValue);
                choice.Selector.Select(selectedIndex < 0 ? 0 : selectedIndex + 1);
            }
            refresh_portrait();
        }

        rebuild_component_choices();
        randomAppearance.Pressed += () => { profile.randomize_player_appearance(); refresh(); };
        resetAppearance.Pressed += () => { reset_appearance_parts(profile.player_appearance_data, true); refresh(); };
        result.Refresh = refresh;
        return result;
    }

    private CreationPage build_aptitude_page(GameStartProfile profile, Action refreshAppearance)
    {
        var page = new VBoxContainer { Name = "AptitudePage", Visible = false, MouseFilter = MouseFilterEnum.Pass };
        page.AddThemeConstantOverride("separation", 12);
        Action refresh = () => { };

        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
        status.AddThemeConstantOverride("separation", 12);
        page.AddChild(status);
        var pointsLabel = creation_label("剩余资质点：0", 19);
        pointsLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pointsLabel.VerticalAlignment = VerticalAlignment.Center;
        status.AddChild(pointsLabel);
        var rootType = creation_label("灵根：" + profile.fated_youngest_root_name, 18);
        rootType.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rootType.HorizontalAlignment = HorizontalAlignment.Right;
        rootType.VerticalAlignment = VerticalAlignment.Center;
        status.AddChild(rootType);
        var reroll = PlainButton("重新抽取资质");
        reroll.CustomMinimumSize = new Vector2(150, 42);
        reroll.TooltipText = "重新抽取灵根与九项初始属性，并重置剩余资质点。";
        status.AddChild(reroll);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        page.AddChild(columns);

        var baseCard = creation_card("基础属性", "每次提升消耗一点；每项上限十点。");
        baseCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        baseCard.Panel.SizeFlagsStretchRatio = .82f;
        columns.AddChild(baseCard.Panel);
        var temperamentCard = creation_card("心性与处世", "每次提升增加五点；每项上限一百点。");
        temperamentCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        temperamentCard.Panel.SizeFlagsStretchRatio = 1.04f;
        columns.AddChild(temperamentCard.Panel);
        var rootCard = creation_card("灵根与五行权重", "五行权重仅供查看；可单独重新抽取灵根。");
        rootCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rootCard.Panel.SizeFlagsStretchRatio = 1.18f;
        columns.AddChild(rootCard.Panel);

        var rootWeightRows = new List<(ProgressBar Bar, Label Value)>();
        var rootNames = new[] { "金", "木", "水", "火", "土" };
        for (var index = 0; index < rootNames.Length; index++)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40) };
            row.AddThemeConstantOverride("separation", 8);
            var name = creation_label(rootNames[index], 16);
            name.CustomMinimumSize = new Vector2(28, 0);
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(64, 16) };
            row.AddChild(bar);
            var value = creation_label("0%", 15);
            value.CustomMinimumSize = new Vector2(48, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(value);
            rootCard.Body.AddChild(row);
            rootWeightRows.Add((bar, value));
        }
        var rootWeightTotal = creation_label("权重合计：—", 14);
        rootCard.Body.AddChild(rootWeightTotal);
        var rerollRoot = PlainButton("重新抽取灵根");
        rerollRoot.CustomMinimumSize = new Vector2(0, 38);
        rerollRoot.TooltipText = "只更新灵根类型与五行权重，保留属性分配、剩余点数和初始功法。";
        rootCard.Body.AddChild(rerollRoot);

        var attributeRows = new List<AttributeChoiceControl>();
        void add_attribute(VBoxContainer parent, string key, string title, float increment)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
            row.AddThemeConstantOverride("separation", 6);
            var name = creation_label(title, 16);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);
            var value = creation_label("—", 18);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            value.CustomMinimumSize = new Vector2(56, 0);
            row.AddChild(value);
            var button = PlainButton(increment == 1 ? "+1" : "+5");
            button.CustomMinimumSize = new Vector2(68, 36);
            button.TooltipText = "消耗一点资质点";
            button.Pressed += () =>
            {
                if (profile.increase_base_attribute(key)) refresh();
            };
            row.AddChild(button);
            parent.AddChild(row);
            attributeRows.Add(new AttributeChoiceControl { Key = key, Value = value, Increase = button, Increment = increment });
        }

        foreach (var item in new[]
        {
            (Key: "physique", Title: "体魄"), (Key: "intelligence", Title: "灵慧"),
            (Key: "agility", Title: "敏捷"), (Key: "soul", Title: "神魂")
        }) add_attribute(baseCard.Body, item.Key, item.Title, 1);
        foreach (var item in new[]
        {
            (Key: "fortune", Title: "机缘"), (Key: "comprehension", Title: "悟性"),
            (Key: "charisma", Title: "魅力"), (Key: "social", Title: "交际"),
            (Key: "management", Title: "治事")
        }) add_attribute(temperamentCard.Body, item.Key, item.Title, 5);

        var configuration = new HBoxContainer { CustomMinimumSize = new Vector2(0, 172) };
        configuration.AddThemeConstantOverride("separation", 14);
        page.AddChild(configuration);
        var techniqueCard = creation_card("初始功法", "选择将随开局传入心神的第一卷功法。");
        techniqueCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        configuration.AddChild(techniqueCard.Panel);
        var techniqueChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 40) };
        style_creation_option(techniqueChoice);
        foreach (var technique in InitialGongfas) techniqueChoice.AddItem(technique.Name);
        var techniqueSummary = creation_label("", 14);
        techniqueSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        techniqueCard.Body.AddChild(techniqueChoice);
        techniqueCard.Body.AddChild(techniqueSummary);
        techniqueChoice.ItemSelected += index =>
        {
            profile.selected_initial_gongfa_path = InitialGongfas[index].Path;
            refresh();
        };

        var presetCard = creation_card("建档方案", "方案仅保存开局选择，不是旅程进度存档。");
        presetCard.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        configuration.AddChild(presetCard.Panel);
        var presetControls = new HBoxContainer();
        presetControls.AddThemeConstantOverride("separation", 8);
        presetCard.Body.AddChild(presetControls);
        var presetName = new LineEdit { PlaceholderText = "方案名称", CustomMinimumSize = new Vector2(150, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        style_creation_input(presetName);
        presetControls.AddChild(presetName);
        var savePreset = PlainButton("保存");
        savePreset.CustomMinimumSize = new Vector2(76, 38);
        presetControls.AddChild(savePreset);
        var presetChoice = new OptionButton { CustomMinimumSize = new Vector2(180, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        style_creation_option(presetChoice);
        presetControls.AddChild(presetChoice);
        var loadPreset = PlainButton("载入");
        loadPreset.CustomMinimumSize = new Vector2(76, 38);
        presetControls.AddChild(loadPreset);
        var presetHint = creation_label("保存后可复用姓名、外观、九项属性、灵根与功法。", 14);
        presetHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        presetCard.Body.AddChild(presetHint);

        var result = new CreationPage { Root = page };
        void refresh_presets()
        {
            presetChoice.Clear();
            var names = profile.get_preset_names();
            if (names.Count == 0)
            {
                presetChoice.AddItem("尚无已存方案");
                loadPreset.Disabled = true;
            }
            else
            {
                foreach (var name in names) presetChoice.AddItem(name);
                presetChoice.Select(0);
                loadPreset.Disabled = false;
            }
        }

        refresh = () =>
        {
            pointsLabel.Text = $"剩余资质点：{profile.fated_remaining_points}";
            foreach (var row in attributeRows)
            {
                var value = profile.Get("fated_base_" + row.Key).AsSingle();
                row.Value.Text = value.ToString(value % 1 == 0 ? "0" : "0.0");
                row.Increase.Disabled = profile.fated_remaining_points < 1 || value >= (row.Increment == 1 ? 10 : 100);
            }
            rootType.Text = "灵根：" + profile.fated_youngest_root_name;
            var rootWeightTotalValue = 0;
            for (var index = 0; index < rootWeightRows.Count; index++)
            {
                var weight = index < profile.fated_youngest_root_weights.Count
                    ? profile.fated_youngest_root_weights[index].AsInt32()
                    : 0;
                rootWeightRows[index].Bar.Value = weight;
                rootWeightRows[index].Value.Text = $"{weight}%";
                rootWeightTotalValue += weight;
            }
            rootWeightTotal.Text = $"权重合计：{rootWeightTotalValue}%";
            var selectedTechnique = Array.FindIndex(InitialGongfas, book => book.Path == profile.selected_initial_gongfa_path);
            if (selectedTechnique < 0) selectedTechnique = 0;
            techniqueChoice.Select(selectedTechnique);
            var detail = InitialGongfas[selectedTechnique];
            techniqueSummary.Text = $"《{detail.Name}》\n{detail.Summary}\n玄鉴会在夜里将所选功法第一卷传入心神。";
        };

        reroll.Pressed += () => { profile.roll_fated_aptitude(); refresh(); };
        rerollRoot.Pressed += () => { profile.reroll_fated_root(); refresh(); };
        savePreset.Pressed += () =>
        {
            profile.save_preset(presetName.Text);
            refresh_presets();
        };
        loadPreset.Pressed += () =>
        {
            if (presetChoice.ItemCount == 0 || presetChoice.GetItemText(presetChoice.Selected) == "尚无已存方案") return;
            if (!profile.load_preset(presetChoice.GetItemText(presetChoice.Selected))) return;
            refreshAppearance();
            refresh();
        };
        refresh_presets();
        result.Refresh = refresh;
        refresh();
        return result;
    }

    private sealed class CreationCard
    {
        public PanelContainer Panel = null!;
        public VBoxContainer Body = null!;
    }

    private static CreationCard creation_card(string title, string subtitle)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.88f, .83f, .71f, .96f),
            BorderColor = new Color(.24f, .18f, .10f, .94f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 14, ContentMarginBottom = 14
        });
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        body.AddThemeConstantOverride("separation", 9);
        panel.AddChild(body);
        var heading = creation_label(title, 21);
        body.AddChild(heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var note = creation_label(subtitle, 13);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(note);
        }
        return new CreationCard { Panel = panel, Body = body };
    }

    private static Label creation_label(string text, int size)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Ink());
        return label;
    }

    private static void style_creation_input(LineEdit input)
    {
        input.AddThemeColorOverride("font_color", Ink());
        input.AddThemeColorOverride("font_placeholder_color", new Color(.30f, .26f, .20f, .72f));
        var style = new StyleBoxFlat { BgColor = new Color(.95f, .91f, .81f), BorderColor = new Color(.37f, .29f, .16f, .9f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, ContentMarginLeft = 8, ContentMarginRight = 8 };
        input.AddThemeStyleboxOverride("normal", style);
        input.AddThemeStyleboxOverride("focus", style);
    }

    private static void style_creation_option(OptionButton option)
    {
        option.AddThemeColorOverride("font_color", Ink());
        option.AddThemeColorOverride("font_hover_color", Ink());
        option.AddThemeColorOverride("font_pressed_color", Ink());
        var style = new StyleBoxFlat { BgColor = new Color(.95f, .91f, .81f), BorderColor = new Color(.37f, .29f, .16f, .9f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, ContentMarginLeft = 8, ContentMarginRight = 8 };
        option.AddThemeStyleboxOverride("normal", style);
        option.AddThemeStyleboxOverride("hover", style);
        option.AddThemeStyleboxOverride("pressed", style);
    }

    private static void style_creation_tab(Button button, bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = active ? new Color(.86f, .77f, .55f, .98f) : new Color(.16f, .18f, .15f, .86f),
            BorderColor = new Color(.69f, .56f, .30f, .96f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
        };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeColorOverride("font_color", active ? Ink() : new Color("f1e5c9"));
        button.AddThemeColorOverride("font_hover_color", active ? Ink() : new Color("f1e5c9"));
    }

    private static void reset_appearance_parts(EntityData data, bool resetSeed)
    {
        if (resetSeed) data.appearance_seed = 1;
        data.appearance_face_style = -1;
        data.appearance_ear_style = -1;
        data.appearance_eyebrow_style = -1;
        data.appearance_eye_style = -1;
        data.appearance_brow_style = -1;
        data.appearance_nose_style = -1;
        data.appearance_mouth_style = -1;
        data.appearance_makeup_style = -1;
        data.appearance_hair_style = -1;
        data.appearance_hair_back_style = -1;
        data.appearance_clothes_style = -1;
        data.appearance_beard_style = -1;
        data.appearance_skin_tone = -1;
        data.appearance_hair_color = -1;
    }

    private static int get_appearance_field(EntityData data, string field) => field switch
    {
        "appearance_face_style" => data.appearance_face_style,
        "appearance_ear_style" => data.appearance_ear_style,
        "appearance_brow_style" => data.appearance_brow_style,
        "appearance_eye_style" => data.appearance_eye_style,
        "appearance_nose_style" => data.appearance_nose_style,
        "appearance_mouth_style" => data.appearance_mouth_style,
        "appearance_makeup_style" => data.appearance_makeup_style,
        "appearance_hair_back_style" => data.appearance_hair_back_style,
        "appearance_hair_style" => data.appearance_hair_style,
        "appearance_clothes_style" => data.appearance_clothes_style,
        "appearance_beard_style" => data.appearance_beard_style,
        "appearance_skin_tone" => data.appearance_skin_tone,
        "appearance_hair_color" => data.appearance_hair_color,
        _ => -1
    };

    private static void set_appearance_field(EntityData data, string field, int value)
    {
        switch (field)
        {
            case "appearance_face_style": data.appearance_face_style = value; break;
            case "appearance_ear_style": data.appearance_ear_style = value; break;
            case "appearance_brow_style": data.appearance_brow_style = value; break;
            case "appearance_eye_style": data.appearance_eye_style = value; data.appearance_eyebrow_style = value; break;
            case "appearance_nose_style": data.appearance_nose_style = value; break;
            case "appearance_mouth_style": data.appearance_mouth_style = value; break;
            case "appearance_makeup_style": data.appearance_makeup_style = value; break;
            case "appearance_hair_back_style": data.appearance_hair_back_style = value; break;
            case "appearance_hair_style": data.appearance_hair_style = value; break;
            case "appearance_clothes_style": data.appearance_clothes_style = value; break;
            case "appearance_beard_style": data.appearance_beard_style = value; break;
            case "appearance_skin_tone": data.appearance_skin_tone = value; break;
            case "appearance_hair_color": data.appearance_hair_color = value; break;
        }
    }

}
