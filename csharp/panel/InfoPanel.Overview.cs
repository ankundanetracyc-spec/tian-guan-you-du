using Godot;

public partial class InfoPanel
{
    private Control? _moodDetailsOverlay;
    private Label? _moodDetailsText;
    private bool _overviewIsPreview;

    private void bind_mood_disc(MoodDiscControl disc)
    {
        disc.HoverTextChanged += show_tooltip;
        disc.HoverEnded += () => HoverInfoManager.HideFor(disc);
    }

    private void open_mood_details()
    {
        hide_tooltip();
        if(_moodDetailsOverlay is null)
        {
            _moodDetailsOverlay=new Control { Name="MoodDetailsOverlay", ZIndex=150, MouseFilter=MouseFilterEnum.Stop };
            _moodDetailsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_moodDetailsOverlay);
            var shade=new ColorRect { Color=new Color(.08f,.09f,.075f,.44f), MouseFilter=MouseFilterEnum.Stop };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _moodDetailsOverlay.AddChild(shade);
            var frame=new PanelContainer { Name="Frame", AnchorLeft=.5f, AnchorRight=.5f, AnchorTop=.5f, AnchorBottom=.5f, OffsetLeft=-370, OffsetRight=370, OffsetTop=-250, OffsetBottom=250 };
            frame.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor=new Color("#eee4c9"), BorderColor=new Color("#918368"), BorderWidthLeft=1, BorderWidthRight=1, BorderWidthTop=1, BorderWidthBottom=1, ContentMarginLeft=32, ContentMarginRight=32, ContentMarginTop=28, ContentMarginBottom=28 });
            _moodDetailsOverlay.AddChild(frame);
            var box=new VBoxContainer(); box.AddThemeConstantOverride("separation",24); frame.AddChild(box);
            var heading=new HBoxContainer(); box.AddChild(heading);
            var title=new Label { Text="心境", SizeFlagsHorizontal=SizeFlags.ExpandFill }; title.AddThemeFontSizeOverride("font_size",30); title.AddThemeColorOverride("font_color",new Color("#4c4436")); heading.AddChild(title);
            var close=new Button { Text="关闭", Flat=true }; close.AddThemeColorOverride("font_color",new Color("#4c4436")); close.AddThemeColorOverride("font_hover_color",new Color("#23544b")); heading.AddChild(close); close.Pressed+=()=>_moodDetailsOverlay.Hide();
            box.AddChild(new HSeparator());
            _moodDetailsText=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart, SizeFlagsVertical=SizeFlags.ExpandFill };
            _moodDetailsText.AddThemeFontSizeOverride("font_size",22); _moodDetailsText.AddThemeColorOverride("font_color",new Color("#4c4436")); box.AddChild(_moodDetailsText);
        }
        if(_moodDetailsText is not null)
            _moodDetailsText.Text=$"{_overviewMoodSummary?.Text ?? "当前心境　未详"}\n\n{_overviewMoodReason?.Text ?? "暂无已确认的长期心绪记录。"}\n\n圆心为无心绪，向圆周情绪逐渐增强。\n\n最近持久变化\n暂无记录。"+(_overviewIsPreview ? "\n\n当前为外观预览，不代表存档中的真实心境。" : "");
        _moodDetailsOverlay.Show(); _moodDetailsOverlay.MoveToFront();
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if(!IsVisibleInTree() || ev is not InputEventKey { Pressed:true, Echo:false, Keycode:Key.Escape }) return;
        if(_moodDetailsOverlay?.Visible==true)
        {
            _moodDetailsOverlay.Hide(); GetViewport().SetInputAsHandled();
        }
    }

    private void render_shared_npc_overview()
    {
        if(_newBody is null || _npc_card is not WorldNpcData npc || _staticOverviewPage is null) return;
        show_redesigned_npc_shell(npc);
        clear_runtime_body(); hide_tooltip(); _moodDetailsOverlay?.Hide();
        _staticOverviewPage.Show();
        _staticAttributePage?.Hide(); _staticBattlePage?.Hide(); _staticGongfaPage?.Hide();
        if(_appearanceEntry is not null) _appearanceEntry.Show();
        _overviewWorldStatusSection?.Hide();
        _appearancePortraitFrame?.Show();
        _profilePortraitRenderer?.Show(); _profilePortraitRenderer?.show_world_npc(npc);
        if(_overviewName is not null) _overviewName.Text=npc.npc_name;
        set_overview_value("gender",npc.gender==1?"女":"男");
        set_overview_value("age",$"{npc.current_age} 岁");
        set_overview_value("species",npc.species);
        set_overview_value("realm",npc.cultivation_realm);
        set_overview_value("spirit_root",npc.spirit_root_name);
        set_overview_value("identity",string.IsNullOrWhiteSpace(npc.formal_identity)?"无":npc.formal_identity);
        set_overview_value("faction",string.IsNullOrWhiteSpace(npc.faction_id)?"无":npc_faction_name(npc.faction_id));
        var map=GD.Load<WorldNodeMapData>("res://resources/world/yunqu_state_nodes.tres");
        string Place(string id)=>map?.get_node(id)?.node_name ?? "未详地点";
        string location=npc.is_traveling ? $"途中 · 前往{Place(npc.travel_destination_node_id)}" : Place(npc.current_node_id);
        if(npc.has_world_grid_position) location+=$" · 格位（{npc.current_grid_cell.X}，{npc.current_grid_cell.Y}）";
        set_overview_value("location",location);
        var weights=new Godot.Collections.Array(); foreach(int value in npc.root_weights) weights.Add(value);
        if(_overviewValueLabels.TryGetValue("spirit_root",out var root)) bind_tooltip(root,format_root_weight_tooltip(weights));
        if(find_label(this,"SpiritrootValue") is { } sidebarRoot) bind_tooltip(sidebarRoot,format_root_weight_tooltip(weights));
        if(_overviewRenownValue is not null) _overviewRenownValue.Text=$"声望：百川 {npc.get_regional_renown(RegionalRenownSystem.BaiChuanRegionId)}";
        if(_overviewRenownBreakdown is not null) _overviewRenownBreakdown.Text="人物已记录的百川声望";
        if(_overviewKarmaValue is not null) _overviewKarmaValue.Text=$"业力：{format_signed_value(npc.karma_value)}";
        _overviewIsPreview=false; set_overview_mood(null);

        _npc_page=new VBoxContainer { Name="NpcReadonlyPage", SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _npc_page.AddThemeConstantOverride("separation",18);
        var supplementMargin=new MarginContainer { Name="NpcSupplementMargin" };
        foreach(string side in new[]{"left","right","top","bottom"}) supplementMargin.AddThemeConstantOverride("margin_"+side,30);
        (_characterCanvas is not null ? _staticOverviewPage : _newBody).AddChild(supplementMargin); supplementMargin.AddChild(_npc_page);
        Label Text(string text,int size=20)
        {
            var label=new Label { Text=text, AutowrapMode=TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size",size); label.AddThemeColorOverride("font_color",new Color("#4c4436")); return label;
        }
        _npc_page.AddChild(new HSeparator());
        _npc_page.AddChild(Text("基础属性",24));
        _npc_page.AddChild(Text($"体魄 {npc.physique:0.#}　灵慧 {npc.intelligence:0.#}　敏捷 {npc.agility:0.#}　神魂 {npc.soul:0.#}\n福缘 未详　悟性 未详　魅力 未详　交际 未详　治事 未详"));
        // 五项尚未存储的属性不能由界面临时随机生成。
        _npc_page.AddChild(Text($"当前状态：{npc.current_state}\n生存：{(npc.is_alive?"在世":"已故 · "+npc.death_cause)}"));
        if(npc.faction_reputations.TryGetValue(npc.faction_id,out var attitude))
            _npc_page.AddChild(Text($"所属势力态度：{attitude}"));
        else _npc_page.AddChild(Text("所属势力态度：未详"));
        if(npc.behavior_tags.Count>0) _npc_page.AddChild(Text("已知特征："+string.Join("、",npc.behavior_tags)));
        _npc_page.AddChild(Text("近期行迹",24));
        if(npc.action_history.Count==0) _npc_page.AddChild(Text("尚无可见记录。"));
        else foreach(var entry in npc.action_history.TakeLast(3))
            if(entry is not null) _npc_page.AddChild(Text($"{entry.date_label}　{entry.action_name}\n{entry.outcome}"));
        _npc_page.AddChild(Text("可进行的交往",24));
        var actions=new HBoxContainer(); actions.AddThemeConstantOverride("separation",14); _npc_page.AddChild(actions);
        void AddAction(string label, Action action)
        {
            var button=new Button { Text=label,CustomMinimumSize=new Vector2(170,46),SizeFlagsHorizontal=SizeFlags.ShrinkBegin };
            button.AddThemeFontSizeOverride("font_size",20);
            actions.AddChild(button); button.Pressed+=action;
        }
        AddAction("交谈",()=>NpcTalkRequested?.Invoke(npc));
        AddAction("交易",()=>NpcTradeRequested?.Invoke(npc));
        if(NpcSparAvailable?.Invoke(npc)==true) AddAction("同门切磋",()=>NpcSparRequested?.Invoke(npc));
        if(NpcPurchaseAvailable?.Invoke(npc)==true) AddAction("求购药材",()=>NpcPurchaseRequested?.Invoke(npc));
    }
}
