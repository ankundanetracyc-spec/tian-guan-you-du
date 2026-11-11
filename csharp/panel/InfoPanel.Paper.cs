using Godot;

/// <summary>角色七页的纸墨布局；数据及业务回调仍由InfoPanel既有部分提供。</summary>
public partial class InfoPanel
{
    private readonly List<Control> _paperNavHosts = [];
    private readonly List<Panel> _paperNavBindings = [];
    private readonly Dictionary<int, Tween> _paperNavTweens = [];
    private Button? _paperInventory;
    private static readonly string[] CharacterPageNames = ["概览", "属性", "战斗", "功法", "造诣", "装备", "关系"];
    private static readonly Dictionary<bool, Texture2D> _paperFrameTextures = [];

    private static ProgressBar CharacterProgress(Color tint)
    {
        var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 17), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bar.AddThemeStyleboxOverride("background", new StyleBoxTexture { Texture = CraftingPaperStyle.Sprite("progressTrack") });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxTexture { Texture = CraftingPaperStyle.Sprite("progressFill"), ModulateColor = tint });
        return bar;
    }

    // The approved role panel uses 25px edges; alchemy's shared panel uses 10px.
    // Only source rectangles are resampled in memory, never written as replacement PNGs.
    private static StyleBoxTexture CharacterPaper(bool shell = false, int padding = 25, bool dark = false)
    {
        if (!_paperFrameTextures.TryGetValue(shell, out var texture))
        {
            using var atlas = GD.Load<Texture2D>(CraftingPaperStyle.Assets + "frame_atlas.png").GetImage();
            if (atlas.IsCompressed()) atlas.Decompress();
            atlas.Convert(Image.Format.Rgba8);
            using var result = Image.CreateEmpty(511, 718, false, Image.Format.Rgba8);
            int[] sourceX = [1105, 1143, 1604, 1642], sourceY = [160, 198, 866, 904];
            int[] targetX = [0, 25, 486, 511], targetY = [0, 25, 693, 718];
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
            {
                var bounds = shell && row == 1 && col == 1 ? new Rect2I(110, 285, 850, 140)
                    : new Rect2I(sourceX[col], sourceY[row], sourceX[col + 1] - sourceX[col], sourceY[row + 1] - sourceY[row]);
                using var piece = atlas.GetRegion(bounds);
                int width = targetX[col + 1] - targetX[col], height = targetY[row + 1] - targetY[row];
                if (piece.GetWidth() != width || piece.GetHeight() != height) piece.Resize(width, height, Image.Interpolation.Lanczos);
                result.BlitRect(piece, new Rect2I(0, 0, width, height), new Vector2I(targetX[col], targetY[row]));
            }
            texture = ImageTexture.CreateFromImage(result); texture.ResourceName = shell ? "CharacterPaper/Shell" : "CharacterPaper/Panel";
            _paperFrameTextures[shell] = texture;
        }
        return new StyleBoxTexture
        {
            Texture = texture, TextureMarginLeft = 25, TextureMarginRight = 25, TextureMarginTop = 25, TextureMarginBottom = 25,
            ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding,
            ModulateColor = dark ? new Color(.19f, .19f, .19f) : Colors.White
        };
    }

    private void setup_character_paper()
    {
        foreach (var name in new[] { "Backdrop", "Header", "LeftRail", "RightRail", "Content" }) GetNodeOrNull<CanvasItem>(name)?.Hide();
        _characterCanvas = new Control { Name = "CharacterDesignCanvas", MouseFilter = MouseFilterEnum.Stop, Theme = CraftingPaperStyle.MakeTheme() };
        AddChild(_characterCanvas); _characterCanvas.Size = CharacterDesignSize;
        var shell = new Panel { Name = "CharacterPaperShell", MouseFilter = MouseFilterEnum.Ignore };
        shell.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>(CraftingPaperStyle.Assets + "frame_atlas.png"), Region = new Rect2(110, 285, 850, 140), FilterClip = true }
        });
        _characterCanvas.AddChild(shell); CharacterRect(shell, 0, 0, 1672, 941);
        var plaque = new PanelContainer { Name = "CharacterPagePlaque", MouseFilter = MouseFilterEnum.Ignore };
        var titlePaper = CraftingPaperStyle.Title(); titlePaper.ContentMarginTop = titlePaper.ContentMarginBottom = 1;
        titlePaper.TextureMarginTop = titlePaper.TextureMarginBottom = 12;
        plaque.AddThemeStyleboxOverride("panel", titlePaper); _characterCanvas.AddChild(plaque); CharacterRect(plaque, 16, 12, 234, 46);
        _characterTitle = CharacterText("人物 · 概览", 22); _characterTitle.HorizontalAlignment = HorizontalAlignment.Center; _characterTitle.VerticalAlignment = VerticalAlignment.Center; plaque.AddChild(_characterTitle);
        _characterName = CharacterClippedText("", 22); _characterRealm = CharacterClippedText("", 22); _characterRoot = CharacterClippedText("", 22);
        _characterCanvas.AddChild(_characterName); CharacterRect(_characterName, 268, 12, 240, 46);
        _characterCanvas.AddChild(_characterRealm); CharacterRect(_characterRealm, 522, 12, 175, 46);
        _characterCanvas.AddChild(_characterRoot); CharacterRect(_characterRoot, 711, 12, 200, 46);
        foreach (var label in new[] { _characterName, _characterRealm, _characterRoot })
        { label.AddThemeColorOverride("font_color", new Color("e8e4d6")); label.VerticalAlignment = VerticalAlignment.Center; }

        _paperInventory = CharacterButton("行囊", 22); _paperInventory.Name = "CharacterInventory";
        _characterCanvas.AddChild(_paperInventory); CharacterRect(_paperInventory, 1414, 18, 114, 46);
        _paperInventory.Pressed += () =>
        {
            if (current_card is Character human && GetTree().GetFirstNodeInGroup("storage_panel") is StoragePanel storage)
            { storage.bind_player(human); hide_panel(); storage.show_panel(); storage.MoveToFront(); }
        };
        var back = CharacterButton("返回", 22); back.Name = "CharacterReturn"; back.Alignment = HorizontalAlignment.Left;
        _characterCanvas.AddChild(back); CharacterRect(back, 1534, 18, 114, 46); back.Pressed += hide_panel;
        var returnTriangle = new CharacterReturnGlyph { Name = "ReturnTriangle", MouseFilter = MouseFilterEnum.Ignore };
        back.AddChild(returnTriangle); CharacterRect(returnTriangle, 80, 19, 6, 8);

        var rail = new Panel { Name = "CharacterPaperRail", MouseFilter = MouseFilterEnum.Ignore };
        rail.AddThemeStyleboxOverride("panel", CharacterPaper(padding: 0)); _characterCanvas.AddChild(rail); CharacterRect(rail, 16, 76, 200, 845);
        var names = CharacterPageNames;
        var icons = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_icons_ink_v1.png"); int[] regions = [5, 0, 1, 2, 0, 3, 5];
        for (int i = 0; i < names.Length; i++)
        {
            int page = i;
            var host = new Control { Name = "CharacterNavHost" + i, MouseFilter = MouseFilterEnum.Ignore };
            _characterCanvas.AddChild(host); CharacterRect(host, 25, 101 + i * 83, 185, 69); _paperNavHosts.Add(host);
            var binding = new Panel { Name = "NavigationBinding", MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
            binding.AddThemeStyleboxOverride("panel", CraftingPaperStyle.BindingStyle()); host.AddChild(binding); CharacterRect(binding, 0, 0, 18, 69); _paperNavBindings.Add(binding);
            var button = new Button { Name = "CharacterTab" + i, Text = names[i], ExpandIcon = true, IconAlignment = HorizontalAlignment.Left, Alignment = HorizontalAlignment.Center };
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Navigation, 26);
            button.AddThemeConstantOverride("icon_max_width", 31); button.AddThemeConstantOverride("h_separation", 9);
            button.Icon = new AtlasTexture { Atlas = icons, Region = new Rect2(regions[i] % 3 * 512, regions[i] / 3 * 512, 512, 512), FilterClip = true };
            host.AddChild(button); CharacterRect(button, 0, 0, 167, 69); _characterTabs.Add(button);
            button.Pressed += () => { if (_npc_read_only) render_npc_tab(page); else render_new_page(page); };
        }
        if (_staticOverviewPage is not null)
        {
            var overviewScroll = new ScrollContainer { Name = "CharacterOverviewScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, ClipContents = true };
            _characterCanvas.AddChild(overviewScroll); CharacterRect(overviewScroll, 232, 76, 1424, 845);
            _staticOverviewPage.Reparent(overviewScroll, false); _staticOverviewPage.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _staticOverviewPage.Theme = _characterCanvas.Theme;
            _characterPages[0] = overviewScroll; build_character_paper_overview();
        }
        for (int i = 1; i < names.Length; i++)
        {
            var page = new Control { Name = "CharacterPage" + i, ClipContents = true };
            _characterCanvas.AddChild(page); CharacterRect(page, 232, 76, 1424, 845); CharacterResponsive(page, new Vector2(1382, 795)); _characterPages[i] = page;
        }
        if (_gongfaModal is not null) _gongfaModal.Theme = _characterCanvas.Theme;
        if (_readerPanel is null) build_character_reader();
        Resized += resize_character_design; resize_character_design();
    }

    private void update_character_paper_navigation(int selected, bool npc = false)
    {
        for (int i = 0; i < _characterTabs.Count; i++)
        {
            var button = _characterTabs[i]; var binding = _paperNavBindings[i];
            bool visible = !npc || i == 0 || i == 1 && _npc_card is { } target && NpcProbeKnowledge.HasSnapshot(target);
            _paperNavHosts[i].Visible = visible; button.Visible = visible;
            bool active = i == selected;
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Navigation, 26, active);
            button.SetMeta("paper_selected", active);
            if (_paperNavTweens.Remove(i, out var previous)) previous.Kill();
            var tween = CreateTween().SetParallel(); _paperNavTweens[i] = tween;
            tween.TweenProperty(button, "position:x", active ? 18f : 0f, .22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(binding, "modulate:a", active && visible ? 1f : 0f, .14).SetTrans(Tween.TransitionType.Linear);
        }
    }

    private void update_character_paper_header(int page, Creature unit)
    {
        _characterTitle!.Text = "人物 · " + CharacterPageNames[page];
        _characterName!.Text = unit.entity_data?.entity_name ?? unit.entity_name;
        _characterRealm!.Text = unit.cultivation_realm; _characterRoot!.Text = (unit as Character)?.spirit_root_name ?? "未详";
        _characterName.Visible = _characterRealm.Visible = _characterRoot.Visible = page != 0;
        if (_paperInventory is not null) _paperInventory.Visible = unit is Character;
        update_character_paper_navigation(page);
    }

    private void resize_character_paper()
    {
        if (_characterCanvas is null) return;
        var width = _characterCanvas.Size.X; var height = _characterCanvas.Size.Y;
        CharacterRect(_characterCanvas.GetNode<Panel>("CharacterPaperShell"), 0, 0, width, height);
        CharacterRect(_characterCanvas.GetNode<Panel>("CharacterPaperRail"), 16, 76, 200, height - 96);
        if (_paperInventory is not null) CharacterRect(_paperInventory, width - 258, 12, 114, 46);
        CharacterRect(_characterCanvas.GetNode<Button>("CharacterReturn"), width - 130, 12, 114, 46);
        foreach (var page in _characterPages.Values) CharacterRect(page, 232, 76, width - 248, height - 96);
        if (_npcProbePage is not null) CharacterRect(_npcProbePage, 232, 76, width - 248, height - 96);
        if (_staticOverviewPage is not null)
        {
            _staticOverviewPage.CustomMinimumSize = new Vector2(width - 248, height - 96);
            if (_staticOverviewPage.GetNodeOrNull<Control>("CharacterPaperOverview") is { } overview)
            { overview.CustomMinimumSize = new Vector2(width - 248, height - 96); overview.Size = overview.CustomMinimumSize; }
        }
    }

    private void build_character_paper_overview()
    {
        if (_staticOverviewPage is null) return;
        CharacterClear(_staticOverviewPage); _overviewValueLabels.Clear();
        var layout = new Control { Name = "CharacterPaperOverview", CustomMinimumSize = new Vector2(1424, 845), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _staticOverviewPage.AddChild(layout); layout.Size = layout.CustomMinimumSize; CharacterResponsive(layout, new Vector2(1382, 795));
        _appearanceEntry = CharacterPanel(layout, 0, 0, 412, 795, 25); _appearanceEntry.Name = "AppearanceEntry";
        var portraitBox = CharacterBox(_appearanceEntry); portraitBox.AddThemeConstantOverride("separation", 5);
        var portraitHeading = new HBoxContainer(); portraitBox.AddChild(portraitHeading);
        var shape = CharacterText("形貌", 28); shape.SizeFlagsHorizontal = SizeFlags.ExpandFill; portraitHeading.AddChild(shape);
        var viewPortrait = CharacterButton("查看立绘", 18, padding: 6); viewPortrait.Name = "ViewPortrait"; portraitHeading.AddChild(viewPortrait); viewPortrait.Pressed += open_character_portrait;
        _appearancePortraitFrame = new PanelContainer { Name = "PortraitFrame", CustomMinimumSize = new Vector2(0, 510), SizeFlagsVertical = SizeFlags.ExpandFill };
        _appearancePortraitFrame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); portraitBox.AddChild(_appearancePortraitFrame);
        _profilePortraitRenderer = new PortraitRenderer { MouseFilter = MouseFilterEnum.Ignore }; _appearancePortraitFrame.AddChild(_profilePortraitRenderer);
        _overviewName = CharacterText("", 30); _overviewName.Name = "OverviewName"; _overviewName.HorizontalAlignment = HorizontalAlignment.Center; portraitBox.AddChild(_overviewName);
        void IdentityRow(int size, params string[] keys)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 7); portraitBox.AddChild(row);
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) row.AddChild(CharacterText("·", size, true));
                var label = CharacterText("未详", size, true); label.Name = "OverviewFact_" + keys[i]; row.AddChild(label); _overviewValueLabels[keys[i]] = label;
            }
        }
        IdentityRow(21, "faction", "identity"); IdentityRow(21, "realm", "spirit_root"); IdentityRow(19, "gender", "age", "species");

        var facts = CharacterBox(CharacterPanel(layout, 434, 0, 574, 390, 30));
        _overviewRenownValue = CharacterText("声望：百川 —", 28); _overviewRenownValue.Name = "OverviewRenown"; facts.AddChild(_overviewRenownValue);
        _overviewRenownBreakdown = CharacterParagraph("", 19, true); _overviewRenownBreakdown.Name = "OverviewRenownBreakdown"; _overviewRenownBreakdown.CustomMinimumSize = new Vector2(0, 100); facts.AddChild(_overviewRenownBreakdown);
        facts.AddChild(new HSeparator()); _overviewKarmaValue = CharacterText("业力：—", 24); _overviewKarmaValue.Name = "OverviewKarma"; facts.AddChild(_overviewKarmaValue);

        _overviewMoodSection = CharacterBox(CharacterPanel(layout, 1030, 0, 352, 390, 24)); _overviewMoodSection.AddThemeConstantOverride("separation", 5);
        var moodHeading = new HBoxContainer(); _overviewMoodSection.AddChild(moodHeading); var moodTitle = CharacterText("心境", 28); moodTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; moodHeading.AddChild(moodTitle);
        var moodHelp = new Button { Icon = CraftingPaperStyle.Sprite("help"), Name = "MoodDetails" }; PaperButtonFeedback.Apply(moodHelp, CraftingPaperStyle.ButtonKind.Icon, 20); moodHeading.AddChild(moodHelp); moodHelp.Pressed += open_mood_details;
        var discHolder = new Control { CustomMinimumSize = new Vector2(0, 250), MouseFilter = MouseFilterEnum.Ignore }; _overviewMoodSection.AddChild(discHolder);
        _overviewMoodScale = new MoodDiscControl(); discHolder.AddChild(_overviewMoodScale); _overviewMoodScale.CustomMinimumSize = Vector2.Zero; _overviewMoodScale.Size = new Vector2(440, 440); _overviewMoodScale.Scale = Vector2.One * .55f; _overviewMoodScale.Position = new Vector2(30, 0);
        foreach (var label in _overviewMoodScale.GetChildren().OfType<Label>()) label.AddThemeFontSizeOverride("font_size", MoodDiscControl.MoodNames.Contains(label.Text) ? 38 : 29);
        bind_mood_disc(_overviewMoodScale);
        _overviewMoodSummary = CharacterText("当前心境　未详", 22); _overviewMoodSummary.Name = "OverviewMoodSummary"; _overviewMoodSummary.HorizontalAlignment = HorizontalAlignment.Center; _overviewMoodSection.AddChild(_overviewMoodSummary);
        _overviewMoodReason = CharacterParagraph("暂无已确认的长期心绪记录。", 18, true); _overviewMoodReason.Hide(); _overviewMoodSection.AddChild(_overviewMoodReason);

        build_overview_world_status_section();
        _overviewWorldStatusSection!.Reparent(layout, false); _overviewWorldStatusSection.CustomMinimumSize = Vector2.Zero;
        _overviewWorldStatusSection.AddThemeStyleboxOverride("panel", CharacterPaper(padding: 27)); CharacterRect(_overviewWorldStatusSection, 434, 412, 948, 383);
        _overviewWorldStatusScroll!.CustomMinimumSize = Vector2.Zero;
    }

    private void open_character_portrait()
    {
        CharacterChoose("形貌", [], _ => { });
        CharacterClear(_gongfaModalChoices!);
        var portrait = new PortraitRenderer { CustomMinimumSize = new Vector2(0, 470), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _gongfaModalChoices!.AddChild(portrait);
        if (current_card is Character human) portrait.show_character(human); else if (_npc_card is { } npc) portrait.show_world_npc(npc);
    }

    private sealed partial class CharacterReturnGlyph : Control
    {
        public override void _Draw() => DrawColoredPolygon([Vector2.Zero, new Vector2(6, 4), new Vector2(0, 8)], CraftingPaperStyle.Ink);
    }
}
