using Godot;

/// <summary>复用正式人物页和最近存档副本，供美术预览；不载入或保存正式旅程。</summary>
public partial class CharacterOverviewPreviewScene : Control
{
    private InfoPanel? _panel;
    private PersistentGameSave? _snapshot;

    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        HumanData? data=null;
        using(var dir=DirAccess.Open("user://game_saves"))
        {
            if(dir is not null)
            {
                var latest=dir.GetFiles().Where(p=>p.EndsWith(".tres",StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p=>Godot.FileAccess.GetModifiedTime("user://game_saves/"+p)).FirstOrDefault();
                if(latest is not null) _snapshot=ResourceLoader.Load<PersistentGameSave>("user://game_saves/"+latest);
                data=_snapshot?.player_data?.Duplicate(true) as HumanData;
            }
        }
        if(data is null)
        {
            data=new HumanData { current_age=16, cultivation_realm="凡人" };
            GetNode<GameStartProfile>("/root/GameStartProfile").apply_to_youngest(data);
        }
        var character=new Character { ProcessMode=ProcessModeEnum.Disabled, entity_data=data };
        AddChild(character); character.load_from_data(data); character.Hide();
        _panel=GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>();
        AddChild(_panel); _panel.show_entity(character);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        _panel.preview_mood(Vector2.Zero);
        var args=OS.GetCmdlineUserArgs();
        if(args.Contains("--audit-overview")) await AuditOverview();
        if(!args.Contains("--capture-overview")) return;
        await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        string revision=args.Contains("--capture-v4") ? "v4" : args.Contains("--capture-v3") ? "v3" : "v2";
        string target=$"res://docs/美术规范/人物概览与心境圆盘_20261010/实际界面_{revision}.png";
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(target));
        var root=_panel.GetNode<Label>("Content/Page/Body/OverviewLayout/AppearanceEntry/OverviewRow/OverviewDetails/OverviewFacts/spirit_root");
        Input.WarpMouse(root.GetGlobalRect().GetCenter());
        root.EmitSignal(Control.SignalName.MouseEntered);
        await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://docs/美术规范/人物概览与心境圆盘_20261010/灵根悬停_{revision}.png"));
        GD.Print("OVERVIEW_CAPTURE_OK "+root.GetMeta("info_tooltip").AsString());
        root.EmitSignal(Control.SignalName.MouseExited);
        if(args.Contains("--audit-overview"))
        {
            var details=_panel.GetNode<Button>("Content/Page/Body/OverviewLayout/AppearanceEntry/OverviewRow/OverviewMoodSection/MoodDetails");
            details.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://docs/美术规范/人物概览与心境圆盘_20261010/心境详情_v4.png"));
        }
        GetTree().Quit();
    }

    public string get_profile_location_for(Character character)
    {
        if(_snapshot is null) return "未详";
        var map=GD.Load<WorldNodeMapData>("res://resources/world/yunqu_state_nodes.tres");
        string place=map?.get_node(_snapshot.current_node_id)?.node_name ?? "未详地点";
        return $"{place}（{_snapshot.world_cell.X}，{_snapshot.world_cell.Y}）";
    }

    private async System.Threading.Tasks.Task AuditOverview()
    {
        if(_panel is null) return;
        void Check(bool condition,string label)
        {
            if(!condition) throw new InvalidOperationException("概览验收失败："+label);
            GD.Print("OVERVIEW_CHECK_OK "+label);
        }
        const string path="Content/Page/Body/OverviewLayout/AppearanceEntry/OverviewRow/";
        var disc=_panel.GetNode<MoodDiscControl>(path+"OverviewMoodSection/MoodDisc");
        Check(disc.CurrentName=="无心绪","圆心状态正确");
        string hover=""; disc.HoverTextChanged+=s=>hover=s;
        disc._GuiInput(new InputEventMouseMotion { Position=disc.Size*.5f });
        Check(hover.Contains("当前心境") && hover.Contains("外观预览"),"节点悬停显示状态及来源");
        Check(!MoodDiscControl.MoodNames.Contains("安宁"),"圆周无安宁扇区");
        _panel.preview_mood(new Vector2(.72f,.05f));
        await ToSignal(GetTree().CreateTimer(.9),SceneTreeTimer.SignalName.Timeout);
        Check(disc.CurrentName=="焦虑","非圆心落点与情绪方向对应");
        disc.ShowState(new Vector2(float.NaN,0));
        Check(disc.CurrentName=="未详","非法坐标不产生虚假状态");
        _panel.preview_mood(Vector2.Zero);
        var details=_panel.GetNode<Button>(path+"OverviewMoodSection/MoodDetails");
        details.EmitSignal(Button.SignalName.Pressed);
        Check(_panel.GetNode<Control>("MoodDetailsOverlay").Visible,"心境详情打开");
        Check(_panel.dismiss_top_overlay() && !_panel.GetNode<Control>("MoodDetailsOverlay").Visible,"返回键先关闭详情");
        details.EmitSignal(Button.SignalName.Pressed);
        _panel._UnhandledKeyInput(new InputEventKey { Keycode=Key.Escape,Pressed=true });
        Check(!_panel.GetNode<Control>("MoodDetailsOverlay").Visible,"预览场景独立支持退出详情");
        if(_snapshot?.world_npcs.FirstOrDefault() is { } npc && _panel.current_card is WorldEntity human)
        {
            _panel.show_npc(npc);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(_panel.GetNode<Control>("Content/Page/Body/OverviewLayout").Visible,"主角与人物共用概览布局");
            Check(_panel.GetNode<Label>(path+"OverviewDetails/OverviewFacts/spirit_root").Text==npc.spirit_root_name,"人物灵根读取实际数据");
            Check(_panel.GetNode<Label>(path+"OverviewDetails/OverviewFacts/gender").Text is "男" or "女","人物性别单独成行");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://docs/美术规范/人物概览与心境圆盘_20261010/人物共用概览_v4.png"));
            _panel.show_entity(human);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(disc.CurrentName=="未详","切回主角不保留演示状态");
            _panel.preview_mood(Vector2.Zero);
        }
        _panel.GetNode<ScrollContainer>("Content/Page").ScrollVertical=0;
    }
}
