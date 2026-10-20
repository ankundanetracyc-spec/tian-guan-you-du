using Godot;

/// <summary>项目启动入口：以水墨山村与玄镜为意象的正式首页。</summary>
public partial class StartMenu : Control
{
    private static readonly string[] BackdropPaths =
    [
        "res://textures/start_menu/title_menu_dawn_v2.png",
        "res://textures/start_menu/title_menu_world_v2.png",
        "res://textures/start_menu/title_menu_power_v4.png"
    ];
    private const string NewGameBackdropPath = "res://textures/start_menu/new_game_ink_study_bg.png";
    private const string BattleTestCharacterScenePath = "res://tscn/battle/BattleTestCharacter.tscn";
    private TextureRect? _backdrop;
    private TextureRect? _nextBackdrop;
    private int _backdropIndex;
    private double _backdropHoldElapsed;
    private double _backdropFadeElapsed;
    private bool _isBackdropFading;
    private Control? _creationOverlay;
    private Control? _battleTestOverlay;
    private EncyclopediaPanel? _encyclopediaPanel;
    private sealed record TestSkillOption(string Name, string Path, bool IsResponse);
    private sealed record TestLoadoutControls(ScrollContainer Scroll, VBoxContainer Content, OptionButton Deck, OptionButton MainBook, OptionButton SubBook, OptionButton[] Skills, CheckBox CustomResources, SpinBox StaminaCap, SpinBox ManaCap);
    private static readonly (string Path, string Name, string Summary)[] InitialGongfas =
    [
        ("res://resources/gongfa/黄庭养形篇.tres", "胎息经", "品阶：凡阶上品\n适配：杂灵根 / 平和灵根\n气血未尽时回落可积身法，归零时得借力；主修可消耗身法，以零气血攻击换回气血。"),
        ("res://resources/gongfa/阴符应物经.tres", "阴符应物经", "品阶：凡阶上品\n适配：金、水灵根\n借势化力，以守转攻。适合稳中求进的修行路数。"),
        ("res://resources/gongfa/赤阳参同经.tres", "赤阳参同经", "品阶：凡阶上品\n适配：火、土灵根\n以气血为炉，淬炼真息。进境刚猛，需谨慎驾驭。 ")
    ];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ApplySavedStartMenuSettings();
        BuildBackdrop();
        BuildMenu();
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--encyclopedia-beasts") >= 0)
            CallDeferred(nameof(OpenBeastEncyclopedia));
    }

    public override void _Process(double delta)
    {
        if (_backdrop is null || _nextBackdrop is null) return;
        // 仅由鼠标驱动视差；鼠标静止时背景完全静止。
        var size = GetViewportRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        var cursorOffset = (GetViewport().GetMousePosition() - size * 0.5f) / (size * 0.5f);
        cursorOffset = cursorOffset.Clamp(-Vector2.One, Vector2.One);
        _backdrop.PivotOffset = size * .5f;
        _nextBackdrop.PivotOffset = size * .5f;
        var target = new Vector2(-cursorOffset.X * 14f, -cursorOffset.Y * 8f);
        _backdrop.Position = _backdrop.Position.Lerp(target, (float)(1.0 - Math.Exp(-7.0 * delta)));
        _nextBackdrop.Position = _nextBackdrop.Position.Lerp(target, (float)(1.0 - Math.Exp(-7.0 * delta)));
        if (_settingsOverlay is null && _demoOverlay is null)
            UpdateBackdropRotation(delta);
    }

    private void BuildBackdrop()
    {
        var imageLayer = new Control { Name = "StartMenuBackdropLayer", MouseFilter = MouseFilterEnum.Ignore };
        imageLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(imageLayer);
        _backdrop = CreateBackdropTexture(BackdropPaths[0], 1f);
        _nextBackdrop = CreateBackdropTexture(BackdropPaths[1], 0f);
        imageLayer.AddChild(_backdrop);
        imageLayer.AddChild(_nextBackdrop);
        var shade = new ColorRect { Color = new Color(0.025f, 0.055f, 0.052f, 0.055f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
    }

    private static TextureRect CreateBackdropTexture(string path, float alpha)
    {
        var texture = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, alpha)
        };
        texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        texture.Scale = new Vector2(1.025f, 1.025f);
        texture.PivotOffset = new Vector2(960, 540);
        return texture;
    }

    private void UpdateBackdropRotation(double delta)
    {
        if (!_isBackdropFading)
        {
            _backdropHoldElapsed += delta;
            if (_backdropHoldElapsed < 16.0)
                return;

            _backdropHoldElapsed = 0;
            _backdropFadeElapsed = 0;
            _nextBackdrop!.Texture = GD.Load<Texture2D>(BackdropPaths[(_backdropIndex + 1) % BackdropPaths.Length]);
            _nextBackdrop.Modulate = new Color(1f, 1f, 1f, 0f);
            _nextBackdrop.MoveToFront();
            _isBackdropFading = true;
        }

        _backdropFadeElapsed += delta;
        var amount = Mathf.Clamp((float)(_backdropFadeElapsed / 1.8), 0f, 1f);
        // 保持底图不透明，仅淡入上层，避免两张半透明画面叠加时出现暗闪。
        _backdrop!.Modulate = Colors.White;
        _nextBackdrop!.Modulate = new Color(1f, 1f, 1f, amount);
        if (amount < 1f)
            return;

        (_backdrop, _nextBackdrop) = (_nextBackdrop, _backdrop);
        _backdropIndex = (_backdropIndex + 1) % BackdropPaths.Length;
        _backdrop.Modulate = Colors.White;
        _nextBackdrop.Modulate = new Color(1f, 1f, 1f, 0f);
        _backdropHoldElapsed = 0;
        _backdropFadeElapsed = 0;
        _isBackdropFading = false;
        RefreshBackdropSelectors();
    }

    private void BuildMenu()
    {
        var menu = GetNode<VBoxContainer>("StartMenuMenu");
        BuildPaperInkMenu(menu);
        menu.MoveToFront();
        AddMenuButton(menu.GetNode<Button>("NewSave"), OpenNewGameSetup);
        AddMenuButton(menu.GetNode<Button>("LoadSave"), OpenLoadSaves);
        AddMenuButton(menu.GetNode<Button>("Encyclopedia"), OpenEncyclopedia);
        AddMenuButton(menu.GetNode<Button>("Settings"), OpenStartMenuSettings);
        AddMenuButton(menu.GetNode<Button>("Quit"), () => GetTree().Quit());
        AddMenuButton(menu.GetNode<Button>("DemoToggle"), ToggleDemoEntries);
        AddMenuButton(menu.GetNode<Button>("AppearanceTest"), () => GetTree().ChangeSceneToFile("res://tscn/scene/BattlePoseLibraryShowcase.tscn"));
        AddMenuButton(menu.GetNode<Button>("AppearanceCreatorTest"), () => GetTree().ChangeSceneToFile("res://tscn/scene/CharacterAppearanceTest.tscn"));
        AddMenuButton(menu.GetNode<Button>("InkVfxTest"), () => GetTree().ChangeSceneToFile("res://tscn/scene/InkVfxShowcase.tscn"));
        AddMenuButton(menu.GetNode<Button>("BattleTest"), OpenBattleTestSetup);
        AddMenuButton(menu.GetNode<Button>("CondensationTest"), OpenCondensationTest);
        AddMenuButton(menu.GetNode<Button>("YuanxiPreview"), () => GetTree().ChangeSceneToFile("res://tscn/scene/YuanxiPreviewScene.tscn"));
        AddMenuButton(menu.GetNode<Button>("SocialInteractionTest"), () => GetTree().ChangeSceneToFile("res://tscn/scene/NpcSocialPreviewScene.tscn"));
        BuildBackdropSelectors();
    }

    private void OpenEncyclopedia()
    {
        _encyclopediaPanel ??= new EncyclopediaPanel { Name = "EncyclopediaPanel" };
        if (_encyclopediaPanel.GetParent() is null) AddChild(_encyclopediaPanel);
        _encyclopediaPanel.show_panel();
    }

    private void OpenBeastEncyclopedia()
    {
        _encyclopediaPanel ??= new EncyclopediaPanel { Name = "EncyclopediaPanel" };
        if (_encyclopediaPanel.GetParent() is null) AddChild(_encyclopediaPanel);
        _encyclopediaPanel.show_beasts();
    }

    private void OpenLoadSaves()
    {
        var manager = GetNodeOrNull<GameSaveManager>("/root/GameSaveManager");
        var slots = manager?.get_save_slots() ?? [];
        if (slots.Count == 0)
        {
            ShowNotice("载入存档", "还没有修行进度。开始新旅程后，游戏会自动记录时间、角色与世界状态。");
            return;
        }

        var overlay = new Control { Name = "LoadSaveOverlay", MouseFilter = MouseFilterEnum.Stop, ZIndex = 250 };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        var veil = new ColorRect { Color = new Color(.02f, .025f, .02f, .72f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(veil);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(760, 590), MouseFilter = MouseFilterEnum.Stop };
        paper.SetAnchorsPreset(LayoutPreset.Center);
        paper.OffsetLeft = -380; paper.OffsetTop = -295; paper.OffsetRight = 380; paper.OffsetBottom = 295;
        paper.AddThemeStyleboxOverride("panel", PaperStyle());
        overlay.AddChild(paper);

        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); paper.AddChild(root);
        var title = new Label { Text = "续接修行", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30); title.AddThemeColorOverride("font_color", Ink()); root.AddChild(title);
        var hint = new Label { Text = "选择一段已保存的旅程。读档将恢复时间、角色、地图与同行人物。", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeColorOverride("font_color", new Color(.32f, .27f, .19f)); root.AddChild(hint);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        var list = new VBoxContainer(); list.SizeFlagsHorizontal = SizeFlags.ExpandFill; list.AddThemeConstantOverride("separation", 8); scroll.AddChild(list);
        foreach (var slot in slots)
        {
            var slotId = slot["id"].AsString();
            var button = PlainButton($"{slot["name"].AsString()}　·　{slot["realm"].AsString()}\n{slot["time"].AsString()}");
            button.CustomMinimumSize = new Vector2(0, 76);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Pressed += async () =>
            {
                var error = await SceneLoadingScreen.ChangeSceneAsync(GetTree(), "res://tscn/scene/MainScene.tscn", "续接修行", () => manager?.load_slot(slotId) == true);
                if (error != Error.Ok && error != Error.Busy && GodotObject.IsInstanceValid(this))
                    ShowNotice("载入失败", "这段存档无法读取，请检查文件后重试。");
            };
            list.AddChild(button);
        }

        var close = PlainButton("返回"); close.Pressed += overlay.QueueFree; root.AddChild(close);
    }

    private void OpenCondensationTest()
    {
        GetTree().ChangeSceneToFile("res://tscn/scene/CondensationTestScene.tscn");
    }

    /// <summary>测试入口可独立配置双方主修、辅修秘籍及十个固定技能槽。</summary>
    private void OpenBattleTestSetup()
    {
        if(_battleTestOverlay is not null){_battleTestOverlay.Show();_battleTestOverlay.MoveToFront();return;}
        var overlay=new Control{Name="BattleTestSetup",MouseFilter=MouseFilterEnum.Stop,ZIndex=300};overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(overlay);_battleTestOverlay=overlay;
        var veil=new ColorRect{Color=new Color(.02f,.03f,.025f,.72f),MouseFilter=MouseFilterEnum.Stop};veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);overlay.AddChild(veil);
        var paper=new PanelContainer{CustomMinimumSize=new(940,620),MouseFilter=MouseFilterEnum.Stop};paper.SetAnchorsPreset(LayoutPreset.Center);paper.OffsetLeft=-470;paper.OffsetTop=-310;paper.OffsetRight=470;paper.OffsetBottom=310;paper.AddThemeStyleboxOverride("panel",PaperStyle());overlay.AddChild(paper);
        var root=new VBoxContainer();root.AddThemeConstantOverride("separation",12);paper.AddChild(root);
        var title=new Label{Text="战斗测试配置",HorizontalAlignment=HorizontalAlignment.Center};title.AddThemeFontSizeOverride("font_size",30);title.AddThemeColorOverride("font_color",Ink());root.AddChild(title);
        root.AddChild(new Label{Text="主修、辅修秘籍与十个技能槽可分别配置；技能可跨秘籍选择。",HorizontalAlignment=HorizontalAlignment.Center,Modulate=new Color(.28f,.22f,.14f)});
        var columns=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddThemeConstantOverride("separation",18);root.AddChild(columns);
        var decks=TestDeckOptions();var books=TestBookOptions();var skillOptions=TestSkillOptions();
        var player=BuildTestLoadoutColumn("我方",decks,books,skillOptions);columns.AddChild(player.Scroll);
        var enemyControls=BuildTestLoadoutColumn("敌方",decks,books,skillOptions);columns.AddChild(enemyControls.Scroll);
        enemyControls.Content.AddChild(new Label{Text="敌方单位"});var enemyKind=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill};foreach(var name in new[]{"试炼盗匪","灰背野猪","精英·灰背野猪","迅蹄野猪","精英·迅蹄野猪","铁鬃野猪","精英·铁鬃野猪","林地凶狼","精英·林地凶狼","血牙凶狼","精英·血牙凶狼","影足凶狼","精英·影足凶狼"})enemyKind.AddItem(name);enemyControls.Content.AddChild(enemyKind);
        enemyControls.Content.AddChild(new Label{Text="敌方数量"});var enemyCount=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill};enemyCount.AddItem("1");enemyCount.AddItem("2");enemyCount.AddItem("3");enemyControls.Content.AddChild(enemyCount);
        enemyKind.ItemSelected+=selected=>SetTestLoadoutEnabled(enemyControls,selected==0);
        SetTestLoadoutEnabled(enemyControls,true);
        AddBattleTestPresets(root,player,enemyControls,enemyKind,enemyCount,books,skillOptions);
        var footer=new HBoxContainer();footer.AddThemeConstantOverride("separation",12);root.AddChild(footer);
        var close=PlainButton("返回");close.Pressed+=()=>overlay.Hide();footer.AddChild(close);
        var start=PlainButton("开始战斗测试");start.SizeFlagsHorizontal=SizeFlags.ExpandFill;footer.AddChild(start);
        start.Pressed+=()=>StartBattleTest(player,enemyControls,enemyKind.Selected,enemyCount.Selected+1);
    }

    private TestLoadoutControls BuildTestLoadoutColumn(string heading,(string name,string path)[] decks,(string name,string path)[] books,TestSkillOption[] skillOptions)
    {
        var section=MakeSection(heading);var box=section.box;section.panel.SizeFlagsHorizontal=SizeFlags.ExpandFill;
        var scroll=new ScrollContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};scroll.AddChild(section.panel);
        var customResources=new CheckBox{Text="自定义体力、法力上限"};box.AddChild(customResources);
        var staminaCap=BuildTestResourceInput(box,"体力上限",6);
        var manaCap=BuildTestResourceInput(box,"法力上限",0);
        staminaCap.Editable=false;manaCap.Editable=false;
        customResources.Toggled+=enabled=>{staminaCap.Editable=enabled;manaCap.Editable=enabled;};
        box.AddChild(new Label{Text="未勾选沿用原上限；勾选后对本方每个单位生效。法力开局填满，体力仍按原轮次规则补给。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=Ink()});
        box.AddChild(new Label{Text="基础技能路线"});var deck=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill};foreach(var item in decks)deck.AddItem(item.name);box.AddChild(deck);
        var mainBook=BuildBookSelector(box,"主修秘籍",books);
        var subBook=BuildBookSelector(box,"辅修秘籍",books);
        box.AddChild(new Label{Text="技能槽配置"});
        var slots=new OptionButton[10];
        var defaultSkills=new[]{"res://resources/activeskills/应敌.tres","res://resources/activeskills/架招.tres","res://resources/activeskills/痛击.tres"};
        for(var i=0;i<slots.Length;i++)
        {
            var row=new HBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddThemeConstantOverride("separation",8);box.AddChild(row);
            var label=new Label{Text=$"{i+1,2}",CustomMinimumSize=new Vector2(28,0),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};row.AddChild(label);
            var select=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill,ClipText=true};select.AddItem("空槽");
            foreach(var item in skillOptions){select.AddItem(item.Name);select.SetItemMetadata(select.ItemCount-1,item.Path);}
            if(i<defaultSkills.Length)
            {
                var defaultIndex=System.Array.FindIndex(skillOptions,item=>item.Path==defaultSkills[i]);
                if(defaultIndex>=0)select.Select(defaultIndex+1);
            }
            select.ItemSelected+=_=>RefreshTestLoadoutChoices(mainBook,subBook,slots,books,skillOptions);
            slots[i]=select;row.AddChild(select);
        }
        mainBook.ItemSelected+=_=>RefreshTestLoadoutChoices(mainBook,subBook,slots,books,skillOptions);
        subBook.ItemSelected+=_=>RefreshTestLoadoutChoices(mainBook,subBook,slots,books,skillOptions);
        RefreshTestLoadoutChoices(mainBook,subBook,slots,books,skillOptions);
        box.AddChild(new Label{Text="技能槽10装配《玄枢含章经》时仅限非应对技能。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color(.30f,.24f,.15f)});
        return new TestLoadoutControls(scroll,box,deck,mainBook,subBook,slots,customResources,staminaCap,manaCap);
    }

    private static SpinBox BuildTestResourceInput(VBoxContainer parent,string caption,int initial)
    {
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",8);parent.AddChild(row);
        row.AddChild(new Label{Text=caption,CustomMinimumSize=new Vector2(90,0),VerticalAlignment=VerticalAlignment.Center,Modulate=Ink()});
        var value=new SpinBox{MinValue=0,MaxValue=int.MaxValue,Step=1,Value=initial,Rounded=true,SizeFlagsHorizontal=SizeFlags.ExpandFill};
        value.GetLineEdit().AddThemeColorOverride("font_color",Ink());row.AddChild(value);return value;
    }

    private static void ApplyTestResourceCaps(BattleEntityData unit,TestLoadoutControls controls)
    {
        if(!controls.CustomResources.ButtonPressed)return;
        controls.StaminaCap.Apply();controls.ManaCap.Apply();
        unit.test_stamina_cap_override=(int)controls.StaminaCap.Value;
        unit.test_mana_cap_override=(int)controls.ManaCap.Value;
        unit.max_stamina=unit.test_stamina_cap_override;
        unit.current_stamina=Math.Min(unit.current_stamina,unit.max_stamina);
        unit.max_mana=unit.test_mana_cap_override;
        unit.current_mana=unit.max_mana;
    }

    private static OptionButton BuildBookSelector(VBoxContainer parent,string label,(string name,string path)[] books)
    {
        parent.AddChild(new Label{Text=label});var select=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill};
        foreach(var item in books)select.AddItem(item.name);
        parent.AddChild(select);return select;
    }

    private void StartBattleTest(TestLoadoutControls playerControls,TestLoadoutControls enemyControls,int enemyKind,int enemyCount)
    {
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        if (transition is null) { GD.PushError("StartMenu: 找不到战斗切换服务"); return; }
        var useBeast=enemyKind>0;var isWolf=enemyKind>=7;var elite=enemyKind is 2 or 4 or 6 or 8 or 10 or 12;
        BeastModifier? modifier=enemyKind is 3 or 4?ResourceLoader.Load<BeastModifier>("res://resources/beast_modifiers/迅蹄野猪.tres"):enemyKind is 5 or 6?ResourceLoader.Load<BeastModifier>("res://resources/beast_modifiers/铁鬃野猪.tres"):enemyKind is 9 or 10?ResourceLoader.Load<BeastModifier>("res://resources/beast_modifiers/血牙凶狼.tres"):enemyKind is 11 or 12?ResourceLoader.Load<BeastModifier>("res://resources/beast_modifiers/影足凶狼.tres"):null;
        var player = CreateTestUnit("试炼修士", (int)WorldEntity.Faction.PLAYER, 0);
        ApplyTestLoadout(player,SelectedPath(playerControls.Deck,TestDeckOptions()),SelectedPath(playerControls.MainBook,TestBookOptions()),SelectedPath(playerControls.SubBook,TestBookOptions()),SelectedSkillPaths(playerControls.Skills));
        ApplyTestResourceCaps(player,playerControls);
        var enemyScenes=new Godot.Collections.Array<PackedScene>();var enemies=new Godot.Collections.Array();
        for(var index=0;index<enemyCount;index++)
        {
            var enemy=useBeast?(isWolf?CreateForestWolfTestUnit(modifier,elite,index):CreateGreybackBoarTestUnit(modifier,elite,index)):CreateTestUnit("试炼盗匪",(int)WorldEntity.Faction.ENEMY,index+1);
            if(!useBeast)ApplyTestLoadout(enemy,SelectedPath(enemyControls.Deck,TestDeckOptions()),SelectedPath(enemyControls.MainBook,TestBookOptions()),SelectedPath(enemyControls.SubBook,TestBookOptions()),SelectedSkillPaths(enemyControls.Skills));
            ApplyTestResourceCaps(enemy,enemyControls);
            enemies.Add(enemy);enemyScenes.Add(useBeast?(isWolf?CreateForestWolfScene(modifier,elite):CreateGreybackBoarScene(modifier,elite)):ResourceLoader.Load<PackedScene>(BattleTestCharacterScenePath)!);
        }
        var characterScene = ResourceLoader.Load<PackedScene>(BattleTestCharacterScenePath);
        if (characterScene is null) { GD.PushError("StartMenu: 找不到战斗测试角色场景"); return; }
        transition.set_battle_return_context(new Godot.Collections.Dictionary{{"is_training",true},{"allow_prebattle_cancel",true},{"title","试炼遭遇"}});
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { characterScene },
            enemyScenes,
            new Godot.Collections.Array { player }, enemies,
            "res://tscn/scene/StartMenu.tscn");
    }

    private static void SetTestLoadoutEnabled(TestLoadoutControls controls,bool enabled)
    {
        controls.Deck.Disabled=!enabled;controls.MainBook.Disabled=!enabled;controls.SubBook.Disabled=!enabled;
        foreach(var slot in controls.Skills)slot.Disabled=!enabled;
    }

    private static string SelectedPath(OptionButton button,(string name,string path)[] options)=>(uint)button.Selected<(uint)options.Length?options[button.Selected].path:"";

    private static string[] SelectedSkillPaths(OptionButton[] slots)=>
        slots.Select(slot=>slot.Selected>0&&slot.Selected<slot.ItemCount?slot.GetItemMetadata(slot.Selected).AsString():"").ToArray();

    private static void RefreshTestLoadoutChoices(OptionButton mainBook,OptionButton subBook,OptionButton[] slots,(string name,string path)[] books,TestSkillOption[] skillOptions)
    {
        var mainPath=SelectedPath(mainBook,books);var subPath=SelectedPath(subBook,books);
        for(var i=1;i<books.Length;i++)
        {
            mainBook.SetItemDisabled(i,books[i].path.Length>0&&books[i].path==subPath);
            subBook.SetItemDisabled(i,books[i].path.Length>0&&books[i].path==mainPath);
        }
        if(mainBook.Selected>0&&mainBook.IsItemDisabled(mainBook.Selected))mainBook.Select(0);
        if(subBook.Selected>0&&subBook.IsItemDisabled(subBook.Selected))subBook.Select(0);
        mainPath=SelectedPath(mainBook,books);subPath=SelectedPath(subBook,books);
        var hasXuanShu=books.Any(book=>book.path.Length>0&&(book.path==mainPath||book.path==subPath)&&System.IO.Path.GetFileNameWithoutExtension(book.path)=="玄枢含章经");
        if(hasXuanShu&&slots[9].Selected>0&&skillOptions[slots[9].Selected-1].IsResponse)slots[9].Select(0);
        var selectedSkillPaths=slots.Select(slot=>slot.Selected>0?slot.GetItemMetadata(slot.Selected).AsString():"").ToArray();
        for(var slotIndex=0;slotIndex<slots.Length;slotIndex++)
        {
            for(var optionIndex=1;optionIndex<skillOptions.Length+1;optionIndex++)
            {
                var option=skillOptions[optionIndex-1];
                var usedElsewhere=selectedSkillPaths.Where((_,index)=>index!=slotIndex).Contains(option.Path,StringComparer.Ordinal);
                slots[slotIndex].SetItemDisabled(optionIndex,usedElsewhere||(slotIndex==9&&hasXuanShu&&option.IsResponse));
            }
            if(slots[slotIndex].Selected>0&&slots[slotIndex].IsItemDisabled(slots[slotIndex].Selected))slots[slotIndex].Select(0);
        }
    }

    private static void ApplyTestLoadout(BattleEntityData unit,string deckPath,string mainBookPath,string subBookPath,string[] selectedSkillPaths)
    {
        unit.basic_deck_path=deckPath;unit.main_gongfa_path=mainBookPath;
        unit.sub_gongfa_paths=[];
        if(!string.IsNullOrWhiteSpace(subBookPath)&&subBookPath!=mainBookPath)unit.sub_gongfa_paths.Add(subBookPath);
        unit.gongfa_paths=[];
        if(!string.IsNullOrWhiteSpace(mainBookPath))unit.gongfa_paths.Add(mainBookPath);
        foreach(var path in unit.sub_gongfa_paths)unit.gongfa_paths.Add(path);

        var books=new List<(GongfaResource Book,bool IsMain)>();
        if(!string.IsNullOrWhiteSpace(mainBookPath)&&ResourceLoader.Load<GongfaResource>(mainBookPath) is { } main)books.Add((main,true));
        foreach(var path in unit.sub_gongfa_paths)if(ResourceLoader.Load<GongfaResource>(path) is { } sub)books.Add((sub,false));
        unit.combat_passive_paths=[];
        var heartUpgrades=new Godot.Collections.Dictionary();
        var temperingByBook=new Godot.Collections.Dictionary();
        foreach(var (book,isMain) in books)
        {
            var heart=book.get_heart_method_skill(isMain);
            if(!string.IsNullOrWhiteSpace(heart?.ResourcePath)&&!unit.combat_passive_paths.Contains(heart.ResourcePath))unit.combat_passive_paths.Add(heart.ResourcePath);
            var tempering=new Godot.Collections.Dictionary();
            var learnedOrder=0;
            foreach(var chapter in book.get_sorted_chapters())
            {
                learnedOrder=Math.Max(learnedOrder,chapter.order);
                if(!string.IsNullOrWhiteSpace(chapter.heart_method_upgrade_id)&&!string.IsNullOrWhiteSpace(chapter.heart_method_upgrade_description))
                    heartUpgrades[$"{book.gongfa_name}|{chapter.heart_method_upgrade_id}"]=chapter.heart_method_upgrade_description;
                foreach(Variant key in chapter.combat_tempering_bonuses.Keys)
                    tempering[key]=tempering.GetValueOrDefault(key,0f).AsSingle()+chapter.combat_tempering_bonuses[key].AsSingle();
            }
            if(learnedOrder>0)heartUpgrades[$"{book.gongfa_name}|current_description"]=book.get_heart_method_description(learnedOrder,isMain);
            if(tempering.Count>0)temperingByBook[book.gongfa_name]=tempering;
        }
        unit.combat_heart_method_upgrades=heartUpgrades;
        unit.combat_tempering_by_gongfa=temperingByBook;

        var uniquePaths=new HashSet<string>(StringComparer.Ordinal);
        var configuredPaths=selectedSkillPaths.Take(10).Select((path,index)=>
        {
            if(string.IsNullOrWhiteSpace(path)||!uniquePaths.Add(path))return "";
            var skill=ResourceLoader.Load<ActiveSkillData>(path);
            var hasXuanShu=books.Any(book=>book.Book.gongfa_name=="玄枢含章经");
            return Character.is_combat_skill_slot_eligible(skill)&&!(index==9&&hasXuanShu&&CardRules.IsResponse(skill))?path:"";
        }).ToArray();
        var paths=new Godot.Collections.Array<string>(configuredPaths);
        unit.active_skill_paths=paths.Duplicate();unit.loaded_skill_paths=paths.Duplicate();
        unit.combat_skill_slot_paths=paths.Duplicate();unit.combat_skill_slots_configured=true;
        unit.combat_skill_references=new Godot.Collections.Array<CombatSkillReference>(configuredPaths.Select(CombatSkillReference.FromKey));
        unit.combat_resolved_skills=new Godot.Collections.Array<ActiveSkillData>(configuredPaths.Select(path=>
            string.IsNullOrWhiteSpace(path)?ArtifactSkillFactory.EmptySlot():ResourceLoader.Load<ActiveSkillData>(path)?.Duplicate() as ActiveSkillData??ArtifactSkillFactory.EmptySlot()));
        unit.battle_deck_configured=true;
    }
    private static (string name,string path)[] TestDeckOptions()=>[("公共基础技能池","res://resources/basic_decks/公共基础技能池.tres")];
    private static (string name,string path)[] TestBookOptions()
    {
        var list=new List<(string,string)>{("无","")};
        foreach(var path in System.IO.Directory.EnumerateFiles(ProjectSettings.GlobalizePath("res://resources/gongfa"),"*.tres").OrderBy(path=>path,StringComparer.Ordinal))list.Add((System.IO.Path.GetFileNameWithoutExtension(path),ProjectSettings.LocalizePath(path)));
        return list.ToArray();
    }
    private static TestSkillOption[] TestSkillOptions()
    {
        var result=new List<TestSkillOption>();
        foreach(var file in System.IO.Directory.EnumerateFiles(ProjectSettings.GlobalizePath("res://resources/activeskills"),"*.tres",System.IO.SearchOption.AllDirectories).OrderBy(path=>path,StringComparer.Ordinal))
        {
            var path=ProjectSettings.LocalizePath(file);var skill=ResourceLoader.Load<ActiveSkillData>(path);
            if(!Character.is_combat_skill_slot_eligible(skill))continue;
            var variant=path.Contains("/化法/",StringComparison.Ordinal)?" · 化法":"";
            var source=string.IsNullOrWhiteSpace(skill!.source_gongfa_name)?"公共技能":skill.source_gongfa_name;
            result.Add(new TestSkillOption($"{source} · {skill.skill_name}{variant}",path,CardRules.IsResponse(skill)));
        }
        return result.ToArray();
    }

    /// <summary>测试双方先生成完整角色数据，再从角色快照进入战斗。</summary>
    private static BattleEntityData CreateTestUnit(string name, int faction, int seed)
    {
        var data = new CharacterGenerator().generate("云息", faction);
        data.entity_name = name; data.appearance_seed = 1200 + seed; data.faction = faction;
        var character = new Character(); character.load_from_data(data);
        character.entity_type = WorldEntity.EntityType.CHARACTER; character.faction = faction; character.unique_id = $"temp_enemy_start_menu_{seed}";
        var snapshot = character.get_snapshot_data(); character.QueueFree();
        return snapshot;
    }

    private static PackedScene CreateGreybackBoarScene(BeastModifier? modifier,bool elite)
    {
        var beast=new Beast{species_id="wild_boar",cultivation_realm="通智",entity_data=new LivingData{cultivation_realm="通智",battle_portrait_path="res://textures/bestiary/static/gray_back_boar.png"},racial_talent=ResourceLoader.Load<BeastRacialTalent>("res://resources/beast_talents/灰背野猪·厚皮.tres"),beast_deck=ResourceLoader.Load<BasicDeckResource>("res://resources/beast_decks/灰背野猪.tres"),beast_modifier=modifier,is_elite=elite};
        var scene=new PackedScene();if(scene.Pack(beast)!=Error.Ok)GD.PushError("StartMenu: 无法创建灰背野猪测试场景");beast.QueueFree();return scene;
    }

    private static float RoundOne(float value)=>Mathf.Round(value*10f)/10f;
    private static float CeilOne(float value)=>Mathf.Ceil(value*10f)/10f;
    private static BattleEntityData CreateGreybackBoarTestUnit(BeastModifier? modifier,bool elite,int index=0)
    {
        var physique=13.5f;var intelligence=2f;var soul=4.5f;
        if(modifier is not null){physique=RoundOne(physique*modifier.physique_multiplier);intelligence=RoundOne(intelligence*modifier.intelligence_multiplier);soul=RoundOne(soul*modifier.soul_multiplier);}
        if(elite){physique=CeilOne(physique*1.25f);intelligence=CeilOne(intelligence*1.25f);soul=CeilOne(soul*1.25f);}
        var name=modifier?.display_name??"灰背野猪";if(elite)name="精英·"+name;
        var coordination=Mathf.Min(physique,soul);var maxHealth=Mathf.RoundToInt(60+physique*11+soul*3+intelligence*1.5f);var maxMind=Mathf.RoundToInt(30+soul*4+intelligence*1.5f+physique*.5f);
        var iron=modifier?.modifier_id=="greyback_boar_iron_bristle";var swift=modifier?.modifier_id=="greyback_boar_swift_trot";
        return new BattleEntityData{uid=$"battle_test_greyback_boar_{index}",entity_name=name,entity_type=(int)WorldEntity.EntityType.BEAST,faction=(int)WorldEntity.Faction.ENEMY,cultivation_realm="通智",physique=physique,intelligence=intelligence,soul=soul,max_health=maxHealth,current_health=maxHealth,max_mind=maxMind,current_mind=maxMind,max_mana=0,current_mana=0,physical_defense=Mathf.RoundToInt(physique*(iron?.5f:.3f)),precision=Mathf.RoundToInt(intelligence*7+soul+physique*.5f),dodge=Mathf.RoundToInt(coordination*4+soul*2+intelligence+physique*.5f),critical=Mathf.RoundToInt(physique*.5f+soul*2+intelligence*.3f),crit_resist=Mathf.RoundToInt(physique*3+intelligence*2+coordination*2+soul*1.5f),crit_damage_mult=1.5f,initiative_speed=Creature.calculate_base_initiative_speed(physique,intelligence,soul)+(swift?coordination*.5f:0),initial_hand_size=5,cards_per_turn=3,battle_portrait_path="res://textures/bestiary/static/gray_back_boar.png"};
    }

    private static PackedScene CreateForestWolfScene(BeastModifier? modifier,bool elite)
    {
        var wolf=new Beast{beast_template=ResourceLoader.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres"),beast_modifier=modifier,is_elite=elite};
        var scene=new PackedScene();if(scene.Pack(wolf)!=Error.Ok)GD.PushError("StartMenu: 无法创建林地凶狼测试场景");wolf.QueueFree();return scene;
    }

    private static BattleEntityData CreateForestWolfTestUnit(BeastModifier? modifier,bool elite,int index)
    {
        var physique=9.75f;var intelligence=2.2f;var soul=7.75f;
        if(modifier is not null){physique=RoundOne(physique*modifier.physique_multiplier);intelligence=RoundOne(intelligence*modifier.intelligence_multiplier);soul=RoundOne(soul*modifier.soul_multiplier);}
        if(elite){physique=CeilOne(physique*1.25f);intelligence=CeilOne(intelligence*1.25f);soul=CeilOne(soul*1.25f);}
        var name=modifier?.display_name??"林地凶狼";if(elite)name="精英·"+name;
        var coordination=Mathf.Min(physique,soul);var maxHealth=Mathf.RoundToInt(60+physique*11+soul*3+intelligence*1.5f);var maxMind=Mathf.RoundToInt(30+soul*4+intelligence*1.5f+physique*.5f);var shadow=modifier?.modifier_id=="forest_wolf_shadowfoot";
        return new BattleEntityData{uid=$"battle_test_forest_wolf_{index}",entity_name=name,entity_type=(int)WorldEntity.EntityType.BEAST,faction=(int)WorldEntity.Faction.ENEMY,cultivation_realm="通智",physique=physique,intelligence=intelligence,soul=soul,max_health=maxHealth,current_health=maxHealth,max_mind=maxMind,current_mind=maxMind,max_mana=0,current_mana=0,physical_defense=0,precision=Mathf.RoundToInt(intelligence*7+soul+physique*.5f),dodge=Mathf.RoundToInt(coordination*4+soul*2+intelligence+physique*.5f)+(shadow?Mathf.RoundToInt(coordination*.25f):0),critical=Mathf.RoundToInt(physique*.5f+soul*2+intelligence*.3f),crit_resist=Mathf.RoundToInt(physique*3+intelligence*2+coordination*2+soul*1.5f),crit_damage_mult=1.5f,initiative_speed=Creature.calculate_base_initiative_speed(physique,intelligence,soul),initial_hand_size=5,cards_per_turn=3,battle_portrait_path=BattlePaperDoll.DefaultPortraitPath};
    }

    private void OpenNewGameSetup()
    {
        build_new_game_creation();
    }
    private static (PanelContainer panel, VBoxContainer box) MakeSection(string heading)
    {
        var panel=new PanelContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(.82f,.77f,.64f,.46f),BorderColor=new Color(.29f,.22f,.12f,.72f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=12,ContentMarginBottom=12});var box=new VBoxContainer();box.AddThemeConstantOverride("separation",8);panel.AddChild(box);var title=new Label{Text=heading};title.AddThemeFontSizeOverride("font_size",20);title.AddThemeColorOverride("font_color",Ink());box.AddChild(title);return(panel,box);
    }
    private static Button PlainButton(string text){var button=new Button{Text=text,CustomMinimumSize=new Vector2(0,38)};button.AddThemeColorOverride("font_color",Ink());button.AddThemeColorOverride("font_hover_color",Ink());button.AddThemeColorOverride("font_pressed_color",Ink());button.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color(.72f,.58f,.28f,.42f),BorderColor=new Color(.31f,.22f,.10f,.72f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5});return button;}
    private static StyleBoxFlat PaperStyle()=>new(){BgColor=new Color(.91f,.86f,.74f,.98f),BorderColor=new Color(.22f,.16f,.09f,.95f),BorderWidthLeft=2,BorderWidthTop=2,BorderWidthRight=2,BorderWidthBottom=2,CornerRadiusTopLeft=9,CornerRadiusTopRight=9,CornerRadiusBottomLeft=9,CornerRadiusBottomRight=9,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=20,ContentMarginBottom=20};
    private static Color Ink()=>new(.12f,.095f,.065f);

    private static void AddMenuButton(Button button, Action action)
    {
        button.Pressed += action;
    }

    private void ShowNotice(string heading, string body)
    {
        var dialog = new AcceptDialog { Title = heading, DialogText = body, OkButtonText = "知晓" };
        dialog.Exclusive = true;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(430, 190));
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
    }

}
