using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;

/// <summary>受控结果驱动真实主场景：探查、对话、存读档、禁斗与反击完整往返。</summary>
public partial class NpcProbeSmoke : Node
{
    private int _checks;
    private MainScene _main = null!;
    private WorldNpcData _npc = null!;
    private Character _player = null!;
    private NpcInteractionPanel _page = null!;
    private InfoPanel _info = null!;
    private StoryDialoguePanel _bottom = null!;
    private NpcWorldManager _world = null!;
    private void Check(bool ok, string why) { _checks++; if (!ok) throw new Exception(why); }
    private static T Read<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
    private static object? Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
    private async Task Frames(int n = 8) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> done, string why)
    { for (var i = 0; i < 6000; i++) { if (done()) return; await Frames(1); } throw new Exception(why); }
    private static Button Button(Node panel, string prefix)
    {
        var buttons = panel.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
        return buttons.FirstOrDefault(b => b.Text.StartsWith(prefix)) ?? throw new Exception("找不到按钮 " + prefix + "；现有：" + string.Join(" / ", buttons.Select(b => b.Text)));
    }
    private static void Press(Node panel, string prefix) => Button(panel, prefix).EmitSignal(BaseButton.SignalName.Pressed);
    private D Pending => Read<D>(_main, "_pendingNpcProbe");
    private string Phase => Pending.GetValueOrDefault("phase", "").AsString();
    private string Dialogue => Read<Label>(_bottom, "_body").Text;
    private string VisibleText(Node node) => string.Join("\n", node.FindChildren("*", "Label", true, false).OfType<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text));
    private void RefreshReferences()
    {
        _main = (MainScene)GetTree().CurrentScene; _world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
        _npc = _world.get_npc("npc_probe_fixture")!; _player = _main.get_player_character()!;
        _page = Read<NpcInteractionPanel>(_main, "npc_interactions"); _info = Read<InfoPanel>(_main, "info");
        _bottom = Read<StoryDialoguePanel>(_main, "story_dialogue");
    }
    private void Personality(string forgiveness = "宽和", string risk = "均衡", string benevolence = "均衡", string order = "均衡")
    {
        foreach (var key in new[] { "sociability", "attachment" }) _npc.social_personality[key] = "均衡";
        _npc.social_personality["forgiveness"] = forgiveness; _npc.social_personality["risk"] = risk;
        _npc.social_personality["benevolence"] = benevolence; _npc.social_personality["order"] = order;
        _npc.social_priorities.Clear(); _npc.social_priorities.Add("修行");
    }
    private void Probe(string id, double a, double b)
    {
        Call(_main, "commit_npc_probe", _npc.npc_id, id, a, b);
        if (_bottom.Visible && (Phase == "notice" || Dialogue.Contains("对方没有察觉"))) _bottom.close();
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await Frames(20); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var root = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..]
            ?? "res://docs/美术规范/NPC身份与探查_20261106/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(root));
        Check(GetViewport().GetTexture().GetImage().SavePng(root + name + ".png") == Error.Ok, "保存实机 " + name);
    }

    private void CheckDialogueLayout()
    {
        if (DisplayServer.GetName() == "headless") return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var dialogue = _bottom.get_dialogue_rect(); var choices = _bottom.get_response_rect();
        Check(dialogue.Position.X > 8 && dialogue.End.X < viewport.X - 8 && dialogue.End.Y < viewport.Y - 8,
            "对话卡两侧和底部必须留空");
        Check(Mathf.Abs(dialogue.GetCenter().X - viewport.X * .5f) < 1, "对话卡应水平居中");
        Check(!dialogue.Intersects(choices) && Mathf.Abs(choices.End.Y - dialogue.Position.Y) < 1,
            "选项的底边必须紧贴底部对话栏的上沿");
        var portrait = Read<Control>(_bottom, "_speakerPortrait");
        var expectedRight = (viewport.X + Mathf.Min(1500f, viewport.X * .76f)) * .5f - 12f;
        Check(Mathf.Abs(choices.End.Y - dialogue.Position.Y) < 1 && Mathf.Abs(choices.End.X - expectedRight) < 2,
            "选项横向保持固定，纵向紧贴对话栏");
        portrait.Hide(); Call(_bottom, "layout_floating_choices");
        var withoutPortrait = _bottom.get_response_rect();
        portrait.Show(); Call(_bottom, "layout_floating_choices");
        Check(withoutPortrait.IsEqualApprox(choices) && _bottom.get_response_rect().IsEqualApprox(choices),
            "立绘出现或隐藏时选项位置保持不变");
        var responseLayer = Read<ScrollContainer>(_bottom, "_responseScroll");
        var highestPortraitLayer = portrait.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>()
            .Where(layer => layer.Visible).Select(layer => layer.ZIndex).DefaultIfEmpty(0).Max();
        Check(!responseLayer.ZAsRelative && responseLayer.ZIndex > portrait.ZIndex + highestPortraitLayer,
            "重叠时选项在所有立绘部件之前，文字可见");
        Check(portrait.Size.Y <= dialogue.Size.Y * 2.1f + 1 && portrait.Size.Y <= dialogue.Size.X * .30f + 1,
            "立绘应按对话卡比例缩放");
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        Check(roster is not null && !choices.Intersects(roster.get_ui_rect()) && !dialogue.Intersects(roster.get_ui_rect()),
            "对话卡及选项不覆盖左侧名单");
        var buttons = _bottom.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
        Check(buttons.Length == 4 && buttons.Zip(buttons.Skip(1)).All(pair =>
            Mathf.Abs(pair.First.GlobalPosition.X - pair.Second.GlobalPosition.X) < 1 && pair.Second.GlobalPosition.Y > pair.First.GlobalPosition.Y),
            "四条回应必须竖排");
    }

    private async Task ChooseResponse(string prefix)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) { Press(_bottom, prefix); return; }
        var button = Button(_bottom, prefix); ClickAt(button.GetGlobalRect().GetCenter());
        await Frames();
        Check(Phase == "reply", "真实鼠标应能点选悬浮回应");
    }
    private void ClickAt(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        foreach (var down in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, ButtonMask = down ? MouseButtonMask.Left : 0, Pressed = down }, true);
    }
    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("npc_probe"), "必须使用隔离存档");
            await Frames(); GetTree().CurrentScene = null;
            var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saveManager.has_active_slot, "禁止覆盖玩家正式存档");
            for (var i = 1; i <= 10; i++)
            {
                var p = NpcProbeRules.Probability(i, 10);
                Check(Math.Abs(p * p + p * (1 - p) + 1 - p - 1) < 1e-12, "三结果概率守恒");
                Check(NpcProbeRules.Resolve(i, 10, 0, 0) is { Success: true, Detected: false }, "隐蔽成功可达");
                Check(NpcProbeRules.Resolve(i, 10, 0, 1) is { Success: true, Detected: true }, "察觉成功可达");
                Check(NpcProbeRules.Resolve(i, 10, p, 0) is { Success: false, Detected: true }, "失败必察觉及成功边界");
                Check(NpcProbeRules.Resolve(i, 10, 0, p).Detected, "隐蔽边界处理");
            }
            Check(NpcProbeRules.Probability(10, 10) == .5 && NpcProbeRules.Probability(40, 10) == .8, "公式标定点");
            Check(NpcProbeRules.Probability(0, 0) == .5 && double.IsFinite(NpcProbeRules.Probability(double.NaN, 10)), "非法或零神魂安全处理");
            var fixture = new PersistentGameSave { year = 1, month = 1, day = 8, hour = 9,
                current_node_id = "white_stone_town", npc_population_version = 1, npc_simulation_enabled = false,
                player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层", current_age = 18,
                    base_physique = 5, base_intelligence = 5, base_soul = 5, current_world_stamina = 73 },
                node_world_state = new D { ["current_node"] = "bc_place_021" },
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true, ["baishi_lecture"] = new BaishiLectureProgress { departed_qingxi = true, fang_returned = true, finished = true } } };
            var npc = new WorldNpcData { npc_id = "npc_probe_fixture", npc_name = "陆衡", faction_id = "baopu_shan",
                formal_identity = "内门弟子", current_node_id = "white_stone_town", current_subscene_id = "inn",
                cultivation_realm = "云息二层", current_age = 27, current_health = 92, max_health = 150,
                physique = 7.25f, intelligence = 8.5f, soul = 10, spirit_root_name = "金木水三灵根" };
            npc.carried_items["res://resources/entities/items/兽骨.tres"] = 2; fixture.world_npcs.Add(npc);
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saveManager, fixture);
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "加载正式场景");
            await Until(() => GetTree().CurrentScene is MainScene, "主场景未载入"); await Frames(24); RefreshReferences();
            var dialogue = Read<StoryDialoguePanel>(_main, "story_dialogue"); for (var i = 0; i < 40 && dialogue.Visible; i++) dialogue.close();
            Call(_main, "on_node_enter_requested", "white_stone_town");
            var nodes = Read<WorldNodeMapData>(_main, "_world_node_data");
            Read<WorldLocationInteriorPanel>(_main, "location_interior").show_baishi_space(nodes.get_node("white_stone_town")!, _world, "inn");
            _player.soul = 10; Personality(); await Frames();
            Check(NpcPublicIdentity.Title(_npc) == "抱朴山内门弟子", "身份显示真实所属和名头");
            _main.open_world_npc_actions(_npc); await Frames();
            Check(Button(_page, "攻击").Disabled && !Button(_page, "探查").Disabled, "禁斗地点应禁攻击而开放探查");
            await Capture("探查入口");
            Call(_main, "request_npc_probe", _npc); await Frames();
            Check(_bottom.Visible && !_page.Visible, "探查确认必须使用底部剧情框");
            Check(Read<Label>(_bottom, "_speaker").Text == "旁白" && Dialogue.StartsWith("你准备以灵识探察"),
                "无事项标题的旁白直接显示正文");
            await Capture("底部探查确认"); Press(_bottom, "作罢"); await Frames();
            Check(_player.current_world_stamina == 73 && !NpcProbeKnowledge.HasSnapshot(_npc), "取消无费用无新认知");
            _npc.soul = 999.25f; _info.show_npc(_npc); await Frames();
            Check(!VisibleText(_info).Contains("999.25") && !Read<bool>(_info, "_npcAttributesSelected"), "双击公开档案绕过探查");
            Check(!Read<System.Collections.Generic.List<Button>>(_info, "_characterTabs")[1].Visible, "未知属性页不应开放");
            await Capture("公开档案未探明"); _info.hide_panel(); _npc.soul = 10;
            _player.current_world_stamina = 0; Probe("no_energy", 0, 0);
            Check(!NpcProbeKnowledge.HasAttempt(_npc, "no_energy") && !NpcProbeKnowledge.HasSnapshot(_npc), "精力不足不得登记探查结果");
            for (var i = 0; i < 40 && dialogue.Visible; i++) dialogue.close(); _player.current_world_stamina = 73;
            var realm = _player.cultivation_realm; _player.cultivation_realm = "凡人";
            Check(((string)Call(_main, "npc_probe_unavailable_reason", _npc)!).Contains("云息"), "凡人不得直接运用灵识");
            _player.cultivation_realm = realm; _player.soul = 10;
            var history = _npc.action_history.Count; var hour = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            Probe("silent", 0, 0); await Frames();
            Check(_player.current_world_stamina == 72 && NpcProbeKnowledge.HasSnapshot(_npc), "隐蔽成功收费和知识");
            Check(_npc.action_history.Count == history && !_npc.player_social_memory.ContainsKey("probe_warned"), "未发现时NPC不应知情");
            Check(_info.Visible && Read<bool>(_info, "_npcAttributesSelected") && VisibleText(_info).Contains("神魂  10"), "成功进入实际属性页");
            await Capture("隐蔽成功属性页");
            Probe("silent", 1, 1); Check(_player.current_world_stamina == 72, "同一事件重放收费");
            _npc.soul = 88; _npc.current_health = 11; _info.show_npc_attributes(_npc); await Frames();
            Check(NpcProbeKnowledge.Snapshot(_npc)["soul"].AsDouble() == 10 && NpcProbeKnowledge.Snapshot(_npc)["health"].AsInt32() == 92,
                "旧快照被免费实时刷新");
            _info.hide_panel(); Probe("failed", 1, 0); await Frames();
            Check(Phase == "result" && !Pending["success"].AsBool() && _bottom.Visible && !_page.Visible, "失败且发现应使用底部对话");
            Check(NpcProbeKnowledge.Snapshot(_npc)["event_id"].AsString() == "silent", "失败覆盖上次成功记录");
            Check(Button(_bottom, "查看上次记录") is not null && _player.current_world_stamina == 71, "失败费用或旧记录入口");
            await Capture("失败被发现_忍下"); Press(_bottom, "告辞");
            _npc.soul = 10; _npc.current_health = 92; _npc.player_social_memory.Remove("probe_warned");
            Probe("noticed_success", 0, 1); await Frames();
            Check(Phase == "result" && Pending["success"].AsBool() && NpcProbeKnowledge.Snapshot(_npc)["event_id"].AsString() == "noticed_success",
                "成功被发现丢失所得");
            await Capture("成功被发现_提醒"); Press(_bottom, "告辞");
            Personality("记怨", "冒进", "冷酷", "不羁"); Probe("city_question", 0, 1); await Frames();
            Check(Phase == "question" && Pending["forbidden"].AsBool() && _bottom.Visible && !_page.Visible, "城中质问必须在底部且压住出手");
            Check(Read<Label>(_bottom, "_speaker").Text == _npc.npc_name, "人物发言仍显示实际人物姓名");
            var dialogueRect = Read<PanelContainer>(_bottom, "_dialogueFrame").GetGlobalRect();
            if (DisplayServer.GetName() != "headless")
                Check(dialogueRect.Position.Y >= GetViewport().GetVisibleRect().Size.Y * .5f &&
                    dialogueRect.End.Y <= GetViewport().GetVisibleRect().Size.Y + 1, "对话框必须位于屏幕下方");
            Check(_world.get_all_east_woods_incidents().Count == 0 && !GetNode<BattleTransitionManager>("/root/BattleTransitionManager").has_open_flow_view(),
                "禁斗地点发难错误启动战斗");
            CheckDialogueLayout();
            await Capture("禁斗地点质问");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                GetWindow().Size = new Vector2I(1280, 720); await Frames(24);
                var smallRect = Read<PanelContainer>(_bottom, "_dialogueFrame").GetGlobalRect();
                Check(smallRect.Position.Y >= 360 && smallRect.End.Y <= 721, "小窗口对话仍位于底部");
                CheckDialogueLayout();
                await Capture("底部质问_1280"); GetWindow().Size = new Vector2I(1920, 1080); await Frames(24);
                CheckDialogueLayout(); await Capture("居中对话_1920");
            }
            var charged = _player.current_world_stamina;
            const string path = "user://npc_probe_pending.tres";
            Check(ResourceSaver.Save(_main.capture_battle_world_snapshot()!, path) == Error.Ok, "待回应存档");
            var loaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(loaded.scene_progress["npc_probe_pending"].AsGodotDictionary()["event_id"].AsString() == "city_question", "存档丢失待回应结果");
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saveManager, loaded);
            GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn");
            await Until(() => GetTree().CurrentScene is MainScene, "读档未回主场景"); await Frames(30); RefreshReferences();
            await Until(() => _bottom.Visible && Phase == "question", "读档未恢复底部质问");
            Check(_player.current_world_stamina == charged && NpcProbeKnowledge.HasAttempt(_npc, "city_question"), "读档重扣或重抽");
            Check(Read<WorldLocationInteriorPanel>(_main, "location_interior").get_baishi_space_id() == "inn", "读档质问未恢复实际当面地点");
            await ChooseResponse("是我唐突"); await Frames();
            Check(Phase == "reply" && Dialogue.Contains("是我唐突") && Read<Label>(_bottom, "_speaker").Text == _player.entity_name &&
                _player.current_world_stamina == charged, "玩家赔礼应单独在底部说话且不重复收费");
            await Capture("赔礼对白");
            if (OS.GetCmdlineUserArgs().Contains("--capture")) ClickAt(Read<Label>(_bottom, "_body").GetGlobalRect().GetCenter());
            else _bottom.close();
            await Frames();
            Check(Phase == "result" && Read<Label>(_bottom, "_speaker").Text == _npc.npc_name, "赔礼后应切回NPC回应");
            await Capture("赔礼后回应"); Press(_bottom, "告辞");
            Probe("city_explain", 1, 0); await Frames(); Press(_bottom, "说明来意"); await Frames();
            Check(Phase == "reply" && Dialogue.Contains("并无别的意思"), "说明来意的对白未接通"); _bottom.close(); await Frames(); Press(_bottom, "告辞");
            Probe("city_defy", 0, 1); await Frames(); Press(_bottom, "何必动气"); await Frames(); _bottom.close(); await Frames();
            Check(Phase == "result" && Dialogue.Contains("禁斗") && !_world.get_all_east_woods_incidents().Any(), "城中强硬回应绕过禁斗");
            await Capture("禁斗地点_强硬回应"); Press(_bottom, "告辞");
            Check(CharacterRelationshipRules.Value(_npc, "player", CharacterRelationshipRules.Resentment) == 0, "冒犯错误套用实际伤害18");
            var city = Read<WorldNodeMapData>(_main, "_world_node_data").get_node("white_stone_town")!;
            Check(WorldConflictRules.Forbidden(city, "inn"), "白石禁斗表");
            city.hostile_combat_policy = WorldConflictRules.Policy.Allowed;
            var room = city.subscenes.First(s => s.subscene_id == "inn"); room.hostile_combat_policy = WorldConflictRules.Policy.Forbidden;
            Check(WorldConflictRules.Forbidden(city, "inn"), "子场所优先级"); room.hostile_combat_policy = WorldConflictRules.Policy.Inherit;
            Check(!WorldConflictRules.Forbidden(city, "inn"), "继承节点明确配置");
            Check(!WorldConflictRules.Forbidden(new WorldNodeData { node_id = "wild_test", node_type = WorldNodeData.NodeType.City }, ""), "不应凭城镇类型猜禁斗");
            _player.soul = 10; _npc.soul = 20;
            Personality("记怨", "冒进", "冷酷", "不羁");
            Check(NpcProbeRules.Respond(_npc, .5, true, false, false) == NpcProbeRules.Reaction.Attack, "野外冒进记怨应可动手");
            Check(NpcProbeRules.Respond(_npc, .5, true, false, true) != NpcProbeRules.Reaction.Attack, "职责保护被探查绕过");
            Check(NpcProbeRules.Respond(_npc, 2, true, false, false) != NpcProbeRules.Reaction.Attack, "明显强来势应有顾忌");
            _npc.player_social_memory["player_realm"] = "金丹圆满"; Personality("记怨", "谨慎", "冷酷", "不羁");
            Check(NpcProbeRules.Respond(_npc, .5, true, false, false) != NpcProbeRules.Reaction.Attack, "谨慎者应考虑已知高境界");
            _npc.player_social_memory.Remove("player_realm"); Personality("记怨", "冒进", "冷酷", "不羁");
            // 进入真实林地，验证允许私斗地点与副本返回；不把城中改规则的夹具画面当作野外实机。
            typeof(MainScene).GetField("_player_world_node_id", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_main, "east_woods");
            Call(_main, "on_node_enter_requested", "east_woods"); await Frames(12);
            var dungeon = Read<EastWoodsDungeonPanel>(_main, "_east_woods_dungeon");
            _npc.current_node_id = "east_woods"; _npc.current_subscene_id = "";
            _npc.dungeon_id = EastWoodsDungeonRules.DungeonId; _npc.dungeon_node_id = dungeon.get_current_route_node_id();
            Call(_main, "refresh_east_woods_route_roster", dungeon.get_current_route_node_id());
            Check(!((bool)Call(_main, "current_location_forbids_fighting")!), "林地不应继承白石城禁斗");
            Probe("retaliation", 0, 1); await Frames();
            Check(Phase == "threat" && Button(_bottom, "应战") is not null && !_page.Visible, "出手前台词应在底部剧情框");
            await Capture("允许交手地点_出手");
            _bottom.close(); // 关闭威胁也不能抹去对方已决定发起的冲突。
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            await Until(() => transition.has_open_flow_view(), "关闭威胁未进入真实战前");
            var context = Read<D>(transition, "return_context").Duplicate(true);
            Check(context["npc_initiated"].AsBool() && context["probe_event_id"].AsString() == "retaliation" && !context["allow_prebattle_cancel"].AsBool(),
                "反击身份、诱因或取消边界错误");
            Check(Phase == "battle", "未持久标记交手阶段");
            var before = Read<System.Collections.Generic.List<BattleEntityData>>(transition, "_beforeBattle");
            var uid = _player.unique_id; var attackers = new A(); var defenders = new A();
            Call(transition, "BeginConfirmedBattle");
            await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "反击没有进入正式战场");
            foreach (var data in before)
            {
                var copy = (BattleEntityData)data.Duplicate(true); copy.current_health = copy.uid == uid ? 5 : 0;
                if (copy.uid == uid) attackers.Add(copy); else defenders.Add(copy);
            }
            transition.end_team_battle(attackers, defenders);
            await Until(() => Read<bool>(transition, "_awaitingSettlement") && !transition.is_flow_transition_active(), "反击战后未结算");
            var summary = Read<D>(transition, "last_summary").Duplicate(true); Call(transition, "ReturnAfterSettlement");
            await Until(() => GetTree().CurrentScene is MainScene, "反击未返回世界"); await Frames(24); RefreshReferences();
            var incident = _world.get_east_woods_incident(context["incident_id"].AsString());
            Check(incident is not null && incident.attacker_npc_id == _npc.npc_id && incident.target_npc_id == "player" && incident.attacker_died && !incident.target_died,
                "反击案卷把玩家误记为实际攻击发起者");
            Check(incident!.attack_trigger_id == "npc_retaliation_to_probe" && incident.evidence_source_refs.Contains("player-probe:retaliation"), "丢失窥探诱因");
            Check(incident.route_node_id.Length > 0 && incident.route_node_id == context["origin_space"].AsString(), "冲突事实丢失实际林地节点");
            Check(!_npc.is_alive && _npc.death_cause.Contains("玩家"), "反击伤亡未落地");
            Check(Read<EastWoodsDungeonPanel>(_main, "_east_woods_dungeon").Visible, "林地反击未返回原历练场景");
            Check(CharacterRelationshipRules.Value((HumanData)_player.entity_data!, _npc.npc_id, CharacterRelationshipRules.Resentment) == 18,
                "NPC出手造成玩家真实受伤未登记单向伤害");
            await Until(() => !transition.is_flow_transition_active() && _main.has_active_item_claim(), "反击胜利无实际携带物领取");
            Check(_main.has_active_item_claim(), "反击实际携带物进入领取");
            Call(Read<ItemClaimPanel>(_main, "_active_item_claim"), "claim_selected_items"); await Frames();
            Check(!GetTree().Paused, "领取结束后仍处于战斗过渡的临时暂停");
            Check(incident.item_transfers.Count == 1 && incident.item_transfers[0].from_npc_id == _npc.npc_id && incident.item_transfers[0].quantity == 2,
                "反击实际领取账方向错误");
            await Until(() => _bottom.Visible && Phase == "result", "反击后没有恢复底部探查结果");
            Check(NpcProbeKnowledge.Snapshot(_npc)["health"].AsInt32() == 92, "战后篡改战前探查记录");
            await Capture("反击结束_记录仍在");
            var transferred = incident.item_transfers.Count; _main.handle_persistent_battle_return(attackers, defenders, summary, context);
            Check(incident.item_transfers.Count == transferred && _world.get_all_east_woods_incidents().Count == 1, "重复反击回调重复结算");
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == hour, "探查对话额外推进游戏时间");
            Check(NpcPersonalityRules.InspectionText(_npc, false).Contains("灵识探查"), "探查知识破坏既有认知结构");
            GD.Print($"NPC_PROBE PASS: {_checks} checks"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
