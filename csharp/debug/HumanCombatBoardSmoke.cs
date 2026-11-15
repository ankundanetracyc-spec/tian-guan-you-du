using Godot;
using System.Text.Json;

/// <summary>真实人类扫盘、应对、周天、复制、化法、视觉及隔离旧档验收。</summary>
public partial class HumanCombatBoardSmoke : Node
{
    private int _checks;
    private readonly Dictionary<string, ActiveSkillData> _skills = [];
    private Node2D _scene = null!;
    private BattleManager _manager = null!;
    private PackedScene _character = null!;
    private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _checks++; GD.Print("HUMAN_BOARD_PASS " + message); }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate, string message, int seconds = 35)
    {
        var until = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!predicate() && Time.GetTicksMsec() < until) await Frames();
        Check(predicate(), message);
    }
    private ActiveSkillData Skill(string name) => _skills[name];
    private CombatBoardPlacement Put(string name, int x, int y, string id, int turns = 0) => new()
        { placement_id = id, source_key = Skill(name).get_board_skill_key(), cell = new(x, y), quarter_turns = turns };
    private CombatBoardCatalog Catalog(string main = "")
    {
        var books = GongfaAvailability.EnabledBooks().Where(book => new[] { "玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章" }.Contains(book.gongfa_name)).ToArray();
        var data = NpcCombatBuildPlanner.TestLearning(books);
        data.main_gongfa_path = main;
        var actor = new Character(); actor.load_from_data(data);
        var catalog = CombatBoardCatalog.FromCharacter(actor); actor.Free(); return catalog;
    }
    private BattleEntityData Unit(string name, int faction, CombatBoardLayout layout, string heart = "") => new()
    {
        uid = name, entity_name = name, faction = faction, cultivation_realm = "云息三层",
        max_health = 10000, current_health = 10000, max_stamina = 6, max_mana = 3, test_mana_cap_override = 3,
        physique = 10, intelligence = 10, soul = 10, body_proficiency = 6, magic_proficiency = 6, sword_proficiency = 6,
        precision = 10000, dodge = 0, critical = 0, crit_resist = 10000, physical_defense = 0,
        initiative_speed = faction == 1 ? 10000 : 5000, combat_skill_slots_configured = true,
        combat_board_layout = layout, combat_board_catalog = Catalog(),
        combat_resolved_skills = [Skill("应敌"), Skill("架招"), Skill("痛击")],
        combat_passive_paths = heart.Length > 0 ? [heart] : []
    };
    private async Task Setup(BattleEntityData player, BattleEntityData foe)
    {
        _manager.setup_team_battle([_character], [_character], [player], [foe]); await Frames(3);
        Check(_manager.is_board_battle() && _manager.is_board_editing(), "人类对战进入真实配招");
    }
    private BattleBoardHud Hud => _scene.GetNode<CanvasLayer>("UILayer").GetChildren().OfType<BattleBoardHud>().Single();
    private async Task Drag(Vector2 from, Vector2 to)
    {
        var previous = from;
        void Motion(Vector2 position)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position,
                Relative = position - previous, ButtonMask = MouseButtonMask.Left });
            previous = position;
        }
        Motion(from);
        Input.ParseInputEvent(new InputEventMouseButton { Position = from, GlobalPosition = from, ButtonIndex = MouseButton.Left, Pressed = true }); await Frames(2);
        Motion(from + new Vector2(18, 0)); await Frames(2); Motion(to); await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = to, GlobalPosition = to, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames(2);
    }
    private void Begin()
    {
        Check(_manager.commit_player_board(Hud.Draft, out var reason), "合法草稿提交 " + reason);
        _manager.request_board_edit();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var capture = GetViewport().GetTexture().GetImage();
        Check(capture.SavePng("res://docs/美术规范/人类招式图标新版_20261107/" + name + ".png") == Error.Ok, "实机截图 " + name);
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        try
        {
            GetWindow().ContentScaleSize = new(1672, 941); GetWindow().Size = new(1672, 941);
            using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://docs/美术规范/人类招式占格审阅_20261107/几何检查结果.json"));
            foreach (var row in json.RootElement.GetProperty("skills").EnumerateArray())
            {
                var skill = GD.Load<ActiveSkillData>(row.GetProperty("resource").GetString()!); _skills[skill.skill_name] = skill;
                using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(skill.icon_path));
                Check(image.GetSize() == skill.board_size * 128 && image.HasMipmaps() == false, skill.skill_name + "正式图标尺寸");
                Check(image.GetPixel(0, 0).A == 0 && image.GetFormat() == Image.Format.Rgba8, skill.skill_name + "真实透明");
                var imported = ResourceLoader.Load<Texture2D>(skill.icon_path);
                Check(imported is not null && imported.GetSize() == skill.board_size * 128, skill.skill_name + "正式纹理可加载");
            }
            Check(Catalog().skills.Count == 20, "已学池完整20招并保留云帔");
            CheckCloudVeil();
            _scene = new Node2D { Name = "HumanBoardIsolated" }; AddChild(_scene);
            _scene.AddChild(new Node2D { Name = "DeployLayer" });
            var ui = new CanvasLayer { Name = "UILayer" }; _scene.AddChild(ui);
            ui.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn").Instantiate());
            _manager = new BattleManager { Name = "BattleMgr", deploy_layer = "../DeployLayer", isolated_trial_battle = true }; _scene.AddChild(_manager);
            _character = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            if (System.Environment.GetEnvironmentVariable("STACKLANDS_BALANCE_AUDIT") is { Length: > 0 } auditPath)
            {
                await RunBalanceAudit(auditPath);
                GetTree().Quit();
                return;
            }
            var empty = new CombatBoardLayout();
            var layout = new CombatBoardLayout { placements = [Put("应敌", 0, 0, "a"), Put("痛击", 1, 0, "pain"), Put("架招", 0, 1, "guard")] };
            var foeLayout = new CombatBoardLayout { placements = [Put("应敌", 0, 2, "foe")] };
            await Setup(Unit("扫盘修士", 1, layout), Unit("对练修士", 2, foeLayout));
            var hud = Hud;
            Check(!hud.MovePlacement("pain", new(2, 0)), "编辑拒绝越界并保留位置");
            Check(hud.MovePlacement("pain", new(1, 1)), "拖动可移动真实多格对象");
            hud.CloseEditor(); Check(!hud.EditorVisible && _manager.is_board_editing(), "点击暗幕只隐藏配招，战斗仍未开始");
            hud.OpenEditor(); Check(hud.Draft.placements.Single(item => item.placement_id == "pain").cell == new Vector2I(1, 1), "返回保留原草稿");
            Check(hud.MovePlacement("pain", new(1, 0)), "草稿恢复合法位置");
            var canvas = hud.GetNode<BattleBoardView>("Editor/Board");
            Vector2 OnBoard(float x, float y) => GetViewport().GetFinalTransform() * (canvas.GetGlobalTransformWithCanvas() * new Vector2(x * canvas.CellSize, y * canvas.CellSize));
            await Drag(OnBoard(2.5f, .5f), OnBoard(2.5f, 1.5f));
            Check(hud.Draft.placements.Single(item => item.placement_id == "pain").cell == new Vector2I(1, 1), "真实拖拽保留抓取的第二格，不把它错当左上角");
            Check(hud.MovePlacement("pain", new(1, 0)), "拖拽后可继续编辑");
            await Capture("实机_配招_1672");
            GetWindow().Size = new(1280, 720); await Capture("实机_配招_1280"); GetWindow().Size = new(1672, 941);
            Begin(); await Capture("实机_自动扫盘");
            await Until(() => _manager.is_board_editing(), "所有参与者完成后才开放整理");
            var player = (Character)_manager.get_main_player_unit()!; var foe = (Character)_manager.enemy_units[0];
            var state = _manager.get_board_state(player)!;
            Check(state.Round == 1 && state.HasSucceeded("a") && state.HasSucceeded("pain"), "同一幕两招均真实施放，未按逐行推进多个回合");
            Check(foe.current_health < foe.max_health, "真实伤害进入对方生命");
            Check(!state.HasSucceeded("guard") && !state.ArmedInOrder().Any() && player.current_health < player.max_health,
                "本幕没有来袭时应对幕末解除，不延续到下一幕");
            Check(player.current_stamina <= 6, "空格回复受体力上限限制");
            await CheckResponseExpiry();
            await CheckReplay(empty);
            await CheckZhouTian(empty);
            await CheckPills();
            CheckTaiheAndRewrites();
            CheckMigratedEffects();
            await CheckVictory();
            await CheckUnconfiguredBeastGate();
            GD.Print($"HUMAN_COMBAT_BOARD_SMOKE_OK checks={_checks}"); CallDeferred(nameof(Shutdown));
        }
        catch (Exception error) { GD.PushError("HUMAN_COMBAT_BOARD_SMOKE_FAILED " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }

    // 让各场景先退出并取消等待，再回收C#包装器；不在仍有绘制对象时直接终止引擎。
    private async void Shutdown()
    {
        _manager.battle_running=false;_scene.QueueFree();await Frames(5);
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Frames(2);GetTree().Quit();
    }

    private void CheckCloudVeil()
    {
        var network = SkillNodeNetworkConfig.load_default()!; var node = network.get_node("cloud_veil_root")!;
        Check(node.face_id == "gather_qi" && node.prerequisite_node_ids.Count == 0 && node.required_attributes["intelligence"].AsSingle() == 8 && node.required_attributes["magic_proficiency"].AsSingle() == 6, "云帔独立节点门槛");
        var old = new HumanData { learned_gongfa_chapters = new() { [HumanData.XuanShuBookPath] = new Godot.Collections.Array<string>(new[] { "yun_pei_2" }) } };
        var save = new PersistentGameSave { format_version = 12, player_data = old };
        Check(save.migrate_to_current_format() && old.legacy_learned_combat_skill_paths.Contains(HumanData.CloudVeilSkillPath), "旧档云帔已学事实保留");
        Check(old.unlocked_skill_node_ids.Count == 0, "旧已学不额外授予节点或属性");
        var fresh = new HumanData(); fresh.preserve_legacy_cloud_veil_learning(); Check(fresh.legacy_learned_combat_skill_paths.Count == 0, "无旧事实不授予云帔");
    }

    private async Task CheckReplay(CombatBoardLayout empty)
    {
        var layout = new CombatBoardLayout { placements = [Put("应敌", 0, 0, "a"), Put("复命", 1, 0, "copy"), Put("应敌", 0, 2, "b")] };
        await Setup(Unit("复命修士", 1, layout), Unit("复命对手", 2, empty)); Begin();
        await Until(() => _manager.is_board_editing(), "复命首轮完成");
        var player = _manager.get_main_player_unit()!; var state = _manager.get_board_state(player)!;
        Check(state.HasSucceeded("a") && state.HasSucceeded("copy") && state.HasSucceeded("b"), "复命占用尚未扫到的原招份数机会");
        Check(state.CooldownRoundsRemaining(Skill("复命")) == 3, "复命保持两个完整冷却回合");
        var noCopy = new CombatBoardLayout { placements = [Put("应敌", 0, 0, "a"), Put("复命", 1, 0, "copy")] };
        await Setup(Unit("无余份修士", 1, noCopy), Unit("无余份对手", 2, empty)); Begin();
        await Until(() => _manager.is_board_editing(), "无余份首轮完成");
        state = _manager.get_board_state(_manager.get_main_player_unit())!;
        Check(!state.HasSucceeded("copy") && ((Creature)_manager.get_main_player_unit()!).current_mana == 3, "无剩余原招机会时复命不发动、不付费");
    }

    private async Task CheckZhouTian(CombatBoardLayout empty)
    {
        var layout = new CombatBoardLayout { placements = [Put("应敌", 1, 0, "a"), Put("聚炁", 0, 1, "qi")] };
        var playerData = Unit("周天修士", 1, layout, "res://resources/activeskills/玄枢含章经/玄枢含章.tres"); playerData.test_mana_cap_override = 1;
        await Setup(playerData, Unit("周天对手", 2, empty));
        Check(_manager.get_board_state(_manager.get_main_player_unit())!.Layout.placements.Any(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator && item.locked), "新装备玄枢心法自动提供默认单格指示"); Begin();
        await Until(() => _manager.is_board_editing(), "周天首轮完成");
        var state = _manager.get_board_state(_manager.get_main_player_unit())!;
        Check(state.HasSucceeded("a") && state.HasUsedZhouTian, "指示招式保留普通机会并获得周天额外一次");
        Check(_manager.get_battle_qi(_manager.get_main_player_unit()!) == 0, "周天清炁");
        playerData.combat_board_layout = state.Layout.CopyConfiguration(); playerData.combat_passive_paths = [];
        await Setup(playerData, Unit("卸心法对手", 2, empty));
        Check(!_manager.get_board_state(_manager.get_main_player_unit())!.Layout.placements.Any(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator), "卸下玄枢心法后撤去其指示，避免锁住旧盘");
    }

    private async Task CheckResponseExpiry()
    {
        var response = new CombatBoardLayout
        {
            size = Vector2I.One,
            placements = [Put("架招", 0, 0, "response")]
        };
        var noIncomingAction = new CombatBoardLayout { size = Vector2I.One };
        await Setup(Unit("无来袭待命修士", 1, response), Unit("无招对手", 2, noIncomingAction));
        Begin();
        await Until(() => _manager.is_board_editing(), "无来袭的一幕结束");
        var owner = _manager.get_main_player_unit()!;
        var state = _manager.get_board_state(owner)!;
        Check(!state.HasSucceeded("response") && !state.ArmedInOrder().Any(), "没有合格来袭时应对在幕末解除");
        Check(owner.Get("current_stamina").AsInt32() == 3, "未触发的应对不支付体力");
    }

    private async Task CheckPills()
    {
        const string path = "res://resources/entities/items/alchemy/huichun_1.tres";
        var layout = new CombatBoardLayout { placements = [new() { placement_id = "pill", source_key = path, kind = CombatBoardPlacement.ObjectKind.Pill, cell = Vector2I.Zero }] };
        var playerData = Unit("服丹修士", 1, layout);
        playerData.current_health -= 20;
        playerData.pack_state_included = true; playerData.pack_item_paths = [path]; playerData.pack_item_counts = [2]; playerData.pack_item_positions = [0];
        var foeData = Unit("服丹对手", 2, new());
        await Setup(playerData, foeData);
        Check(CombatBoardGeometry.Footprint(Hud.Draft.placements[0], _ => null) == Vector2I.One, "丹药在战斗盘只占一格");
        Check(_manager.commit_player_board(Hud.Draft, out _), "真实库存允许单颗丹药入盘");
        await Until(() => _manager.is_board_editing(), "丹药第一轮结束自动开配招");
        var actor = (Character)_manager.get_main_player_unit()!;
        var state = _manager.get_board_state(actor)!;
        Check(state.Layout.placements.Single().expended && actor.get_inventory_total_count(path) == 1, "受伤时单格丹药实际服用且只扣一颗");
        Check(actor.current_health == actor.max_health && actor.PillToxicity > 0, "药效与丹毒沿真实服丹流程结算");
        Check(CombatBoardGeometry.Footprint(state.Layout.placements.Single(), _ => null) == Vector2I.One, "服用后空瓶仍只占一格");
        Check(Hud.EditorVisible, "每个共同回合结束自动显示配招页");
        Check(_manager.commit_player_board(Hud.Draft, out _), "空瓶无需剩余库存也可保留布局");
        actor.current_health = actor.max_health - 10;
        await Until(() => _manager.is_board_editing(), "空瓶第二轮结束");
        Check(actor.get_inventory_total_count(path) == 1 && actor.current_health < actor.max_health, "空瓶下轮不会自动服用第二颗丹药");
        Check(Hud.MovePlacement("pill", Vector2I.Zero), "选择空瓶"); Hud.RefillSelected();
        Check(!Hud.Draft.placements.Single().expended && actor.get_inventory_total_count(path) == 1, "整理补药只预留，不提前扣库存");
        Check(!Hud.TryPlaceSource(path, new(1, 0)), "同一颗库存不能重复摆放");
        var copy = actor.get_snapshot_data();
        var original = new Character(); AddChild(original); original.load_from_data(new HumanData()); original.apply_battle_data(copy);
        Check(original.get_inventory_total_count(path) == 1 && ((HumanData)original.entity_data!).combat_board_layout?.placements.Single().expended == true,
            "战后快照回传实际库存及空瓶布局"); original.Free();
    }

    private void CheckTaiheAndRewrites()
    {
        var owner = (Character)_manager.get_main_player_unit()!; var runtime = _manager.gongfa_combat;
        runtime.Begin(owner); runtime.Utility(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/太和感物养元.tres"));
        owner.qi_blood = 0;
        var attack = GD.Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/化法/乘势再攻第一卷.tres");
        Check(runtime.TaiheFollowUpCount(owner, attack, true, true) == 2 && owner.qi_blood == 2, "两追加条件产生两次自身应对气血收益");
        var first = runtime.CreateTaiheFollowUpSegment(owner, attack, 0); var second = runtime.CreateTaiheFollowUpSegment(owner, attack, 1);
        Check(first.damage_formula == second.damage_formula && GongfaSkillRewriteRuntime.HasEffect(attack, "taihe_c1_koushan"), "太和一卷仅改变应对追加段的化解结算，不改伤害");
        Check(runtime.TaiheFollowUpCount(owner, attack, false, false) == 0, "没有触发条件时不生成太和追加段");
        var shield = GD.Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/涵虚.tres");
        Check(!CardRules.IsResponse(shield) && shield.slot_category == "active" && shield.energy_cost == 1 && shield.mana_cost == 0
            && shield.target_type == 0 && shield.board_size == new Vector2I(2, 1) && shield.board_max_placements == 2,
            "虚怀纳劲作为主动招式并保留原费用、目标、占格和份数");
        Check(shield.tags.Contains("护盾") && !shield.keywords.Contains("应对")
            && shield.description.Contains("施展时")
            && shield.description.Contains("round(4+体魄×0.20+min(体魄×0.20,神魂×0.20))"),
            "虚怀纳劲施展时按原公式预置护盾");
        var catalog = Catalog("res://resources/gongfa/百骸衍枝化生宝册.tres");
        Check(catalog.rewrites.Count == 2 && catalog.rewrites.All(source => source.max_placements == 1), "百骸仅提供保留的两项单份化法");
        var source = catalog.rewrites.Single(source => source.effect_ids.Contains("baihai_c1_cuixue"));
        var layout = new CombatBoardLayout { placements = [Put("催血", 0, 0, "a"), Put("催血", 0, 1, "b"), new() { placement_id = "rewrite", source_key = source.source_key, kind = CombatBoardPlacement.ObjectKind.Rewrite, cell = new(2, 0), rewrite_direction = 2 }] };
        Check(catalog.ResolvePlacement(layout, layout.placements[0])!.gongfa_rewrite_effect_ids.Contains("baihai_c1_cuixue")
            && !catalog.ResolvePlacement(layout, layout.placements[1])!.gongfa_rewrite_effect_ids.Contains("baihai_c1_cuixue"), "化法只强化被指向的一份催血");
    }

    private async Task CheckVictory()
    {
        var player = Unit("胜负修士", 1, new() { placements = [Put("应敌", 0, 0, "a")] });
        var foe = Unit("胜负对手", 2, new()); foe.current_health = foe.max_health = 1;
        await Setup(player, foe);
        Check(_manager.commit_player_board(Hud.Draft, out _), "胜负场提交");
        await Until(() => !_manager.battle_running && _manager.HasMeta("isolated_trial_finished"), "真实最后一击结算至胜负，未额外扫空格");
    }

    private async Task CheckUnconfiguredBeastGate()
    {
        var template = new Beast { species_id = "unconfigured_beast", entity_data = new LivingData { data_initialized = true } };
        var packed = new PackedScene(); Check(packed.Pack(template) == Error.Ok, "构造缺少固定盘的隔离兽战场景"); template.Free();
        var foe = new BattleEntityData { uid = "unconfigured_beast", entity_name = "未配置生灵", faction = 2,
            entity_type = (int)WorldEntity.EntityType.BEAST, current_health = 1000, max_health = 1000,
            cultivation_realm = "凡兽", initiative_speed = 50,
            active_skill_paths = ["res://resources/activeskills/妖兽/林地凶狼/撕咬.tres"] };
        _manager.setup_team_battle([_character], [packed], [Unit("缺盘检查修士", 1, new())], [foe]);
        await Frames(4);
        Check(!_manager.is_board_battle() && !_manager.battle_running && _manager.get_board_entry_rejection_reason().Contains("没有完整的固定招式盘", StringComparison.Ordinal),
            "缺少固定盘的生灵不进入战斗，也不回退旧流程");
    }


    private void CheckMigratedEffects()
    {
        var owner = (Character)_manager.get_main_player_unit()!;
        var foe = (Character)_manager.enemy_units[0];
        var runtime = _manager.gongfa_combat;
        var books = GongfaAvailability.EnabledBooks().ToArray();
        Check(books.Length == 5, "全效果验收不开放候选秘籍");
        foreach (var book in books)
        {
            foreach (var chapter in book.get_sorted_chapters())
            {
                var main = book.get_heart_method_description(chapter.order, true);
                var sub = book.get_heart_method_description(chapter.order, false);
                Check(main.Contains("效果：") && !main.Contains("沿用第"), book.gongfa_name + chapter.chapter_name + "运行时完整心法");
                Check(!sub.Contains("主修额外效果") && sub.Contains("效果："), book.gongfa_name + chapter.chapter_name + "辅修完整且不混入主修收益");
                Check(!main.Contains("技能槽10") && !main.Contains("自动应对") && !main.Contains("攻伐"), book.gongfa_name + chapter.chapter_name + "心法无旧模式文案");
                if (book.gongfa_name == "百骸衍枝化生宝册" || book.gongfa_name == "舒卷天章")
                    Check(sub.Contains("A=") && !sub.Contains("计算公式："), book.gongfa_name + chapter.chapter_name + "辅修公式紧随效果、不会被主修截断");
            }
            var catalog = Catalog(book.ResourcePath);
            var expectedRewriteCount = book.gongfa_name switch
            {
                "玄枢含章经" => 1,
                "斫基损契玄经" => 1,
                "百骸衍枝化生宝册" => 2,
                "太和感物养元宝册" => 1,
                "舒卷天章" => 1,
                _ => throw new InvalidOperationException("未审查的开放秘籍：" + book.gongfa_name)
            };
            Check(catalog.rewrites.Count == expectedRewriteCount, book.gongfa_name + "卷章只提供当前保留的化法数量");
            var expectedEffectIds = book.gongfa_name switch
            {
                "玄枢含章经" => new[] { "xuan_shu_c1_jingzhe" },
                "斫基损契玄经" => new[] { "zhuo_ji_c2_jianzhu" },
                "百骸衍枝化生宝册" => new[] { "baihai_c1_cuixue", "baihai_c4_jiazhao" },
                "太和感物养元宝册" => new[] { "taihe_c1_koushan" },
                "舒卷天章" => new[] { "shujuan_c3" },
                _ => throw new InvalidOperationException("未审查的开放秘籍：" + book.gongfa_name)
            };
            var configuredEffectIds = catalog.rewrites.SelectMany(source => source.effect_ids).ToArray();
            Check(configuredEffectIds.Length == expectedEffectIds.Length
                && configuredEffectIds.ToHashSet(StringComparer.Ordinal).SetEquals(expectedEffectIds),
                book.gongfa_name + "只挂载经逐项审查保留的化法 ID");
            if (book.gongfa_name == "玄枢含章经")
            {
                var jingzhe = GD.Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/化法/惊蛰.tres");
                Check(runtime.BypassesShield(jingzhe, true) && !runtime.BypassesShield(jingzhe, false),
                    "玄枢惊蛰只在周天额外施放时绕过护盾");
                var baseJingzhe = GD.Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/惊蛰.tres");
                var rewrittenJingzhe = GongfaSkillRewriteRuntime.CopyWithMergedEffects(jingzhe, baseJingzhe);
                Check(rewrittenJingzhe.tags.Contains("散血") && rewrittenJingzhe.tags.Contains("穿盾"),
                    "玄枢惊蛰化法保留散血并增加穿盾检索词");
            }
            foreach (var source in catalog.rewrites)
            {
                var basis = catalog.FindSkill(source.target_keys.Single())!;
                var item = new CombatBoardPlacement { placement_id = "basis", source_key = basis.get_board_skill_key() };
                var rewrite = new CombatBoardPlacement { placement_id = "rewrite", source_key = source.source_key,
                    kind = CombatBoardPlacement.ObjectKind.Rewrite, cell = new(basis.board_size.X, 0), rewrite_direction = 2 };
                var layout = new CombatBoardLayout { size = new(8, 8), placements = [item, rewrite] };
                var resolved = catalog.ResolvePlacement(layout, item)!;
                Check(resolved.gongfa_rewrite_effect_ids.Count > 0 && basis.gongfa_rewrite_effect_ids.Count == 0, source.source_name + "仅改写本份副本");
                Check(resolved.energy_cost == basis.energy_cost && resolved.mana_cost == basis.mana_cost
                    && resolved.damage_formula == basis.damage_formula && resolved.target_type == basis.target_type, source.source_name + "保留原费、目标与基础伤害");
                var face = CardPresentation.Face(resolved, owner);
                Check(face.Contains("化法追加") && !face.Contains("技能槽10") && !face.Contains("行动队列"), source.source_name + "追加效果进入当前详情");
            }
        }
        foreach (var skill in _skills.Values)
        {
            var face = CardPresentation.Face(skill, owner);
            Check(!face.Contains("行动队列") && !face.Contains("当前槽") && !face.Contains("技能槽10"), skill.skill_name + "所有基础详情适配新盘");
            Check(skill.get_brief_description(owner) == face, skill.skill_name + "简述入口与完整效果同源");
            var text = CombatBoardPresentation.Skill(skill, owner);
            Check(text.StartsWith(skill.get_board_display_name()) && !text.Contains("最多摆") && !text.Contains("首次扫到")
                && !text.Contains("失败不再重试") && !text.Contains("扫到后变暗"), skill.skill_name + "名称表示份数且效果不重复底层流程");
            Check(text.Contains("关键词：") && text.Contains("目标："), skill.skill_name + "展示真实关键词和目标");
            if (skill.skill_type == ActiveSkillData.SkillType.ATTACK)
                Check(text.Contains("出手方式："), skill.skill_name + "明确近身或远程");
        }
        var displayProbe = (ActiveSkillData)Skill("痛击").Duplicate(true);
        displayProbe.uses_per_battle = 2;
        Check(displayProbe.get_board_display_name() == "痛击*2 限2", "名称份数与整场限次分开表达");
        displayProbe.uses_per_battle = 0; displayProbe.uses_per_round = 1;
        Check(!displayProbe.get_board_display_name().Contains("限") && CombatBoardPresentation.Skill(displayProbe, owner).Contains("每回合1次"), "回合限次不误写成整场限次");

        void Upgrades(string book, params string[] ids)
        {
            var values = new Godot.Collections.Dictionary();
            foreach (var id in ids) values[book + "|" + id] = true;
            owner.SetMeta("combat_heart_method_upgrades", values);
        }
        ActiveSkillData Load(string book, string file) => GD.Load<ActiveSkillData>($"res://resources/activeskills/{book}/{file}.tres");
        void Heart(string book, string file = "心法")
        {
            runtime.Begin(owner);
            owner.main_gongfa = GD.Load<GongfaResource>($"res://resources/gongfa/{book}.tres");
            runtime.Utility(owner, Load(book, file));
        }

        const string cloud = "舒卷天章";
        Heart(cloud); Upgrades(cloud);
        var intent = GD.Load<StatusEffect>("res://resources/battle/statuses/剑意.tres");
        void Intent(int amount)
        {
            owner.consume_status_stacks("剑意", 100);
            if (amount > 0) owner.add_status(intent, -1, amount);
        }
        Intent(10); runtime.Before(owner, Skill("斩剑"));
        var first = runtime.CreateCloudFollowUp(owner, Skill("斩剑")).calculate_damage(owner);
        Check(runtime.CloudFollowUpLimit(owner, Skill("斩剑"), true) == 1, "舒卷一卷满剑意仍只追加一段");
        Upgrades(cloud, "shujuan_c2"); runtime.Before(owner, Skill("斩剑"));
        Check(runtime.CreateCloudFollowUp(owner, Skill("斩剑")).calculate_damage(owner) == first + 4, "舒卷二卷增加超出六层的四点伤害");
        Upgrades(cloud, "shujuan_c4"); runtime.Before(owner, Skill("尽锋")); Intent(0);
        Check(runtime.CloudFollowUpLimit(owner, Skill("尽锋"), true) == 2, "舒卷四卷读取开始十层，尽锋清空后仍追加两段");
        Check(runtime.CloudFollowUpLimit(owner, Skill("尽锋"), false) == 0, "尽锋首段未命中不触发心法追加");
        Intent(5); runtime.Before(owner, Skill("斩剑"));
        Check(runtime.CloudFollowUpLimit(owner, Skill("斩剑"), true) == 1, "舒卷主修五层门槛");
        owner.main_gongfa = null; runtime.Before(owner, Skill("斩剑"));
        Check(runtime.CloudFollowUpLimit(owner, Skill("斩剑"), true) == 0, "舒卷辅修不获得降低门槛");
        Intent(10); runtime.Before(owner, Skill("云岚三叠"));
        Check(runtime.CloudFollowUpLimit(owner, Skill("云岚三叠"), true) == 3, "云岚自身三段不叠加四卷固定两段");
        Check(Mathf.IsEqualApprox(runtime.CloudFollowUpChance(owner, Skill("云岚三叠"), 0), .8f)
            && Mathf.IsEqualApprox(runtime.CloudFollowUpChance(owner, Skill("云岚三叠"), 1), .6f)
            && Mathf.IsEqualApprox(runtime.CloudFollowUpChance(owner, Skill("云岚三叠"), 2), .4f), "云岚递减概率与上限");
        const string arms = "百骸衍枝化生宝册";
        Heart(arms); Upgrades(arms);
        Check(runtime.PreviewBloodArmCount(owner, 4) == 0 && runtime.PreviewBloodArmCount(owner, 5) == 1, "百骸一卷五层显臂");
        Upgrades(arms, "baihai_c2"); Check(runtime.PreviewBloodArmCount(owner, 4) == 1, "百骸二卷四层显臂");
        Upgrades(arms, "baihai_c3"); Check(runtime.PreviewBloodArmCount(owner, 7) == 1 && runtime.PreviewBloodArmCount(owner, 8) == 2, "百骸三卷四八门槛");
        Upgrades(arms, "baihai_c4"); owner.qi_blood = 7;
        Check(runtime.BloodArmCount(owner) == 2 && runtime.PreviewBloodArmCount(owner, 3) == 1, "百骸四卷三七门槛");
        Check(runtime.RollBloodArmAutoResponse(owner, foe, Skill("应敌")) == 0 && owner.qi_blood == 7, "新盘心法不自行应对或回落气血");
        Check(runtime.Segments(owner, Skill("应敌")) == 3 && runtime.Segments(owner, Skill("灵击")) == 1, "双臂仅追加近身物理攻击");
        var mountain = Load(arms, "化法/摧山");
        Check(runtime.Segments(owner, mountain) == 3, "撤下段序化法后，血臂撼岳基础段与双臂段照常结算");
        var guard = Skill("架招"); Heart("玄枢含章经", "玄枢含章"); Upgrades("玄枢含章经"); owner.shield = 0;
        runtime.Before(owner, guard); runtime.OnResponseTriggered(owner, guard);
        runtime.ResponseResolved(owner, foe, 2, 0, false); runtime.ResponseResolved(owner, foe, 3, 0, false);
        Check(owner.shield == 0, "玄枢化解返盾不提前混入本次来袭"); runtime.EndIncomingAttack(owner);
        Check(owner.shield == 5, "玄枢整次来袭后仅按实际抵消五点返盾");

        Heart("斫基损契玄经", "斫基损契"); Upgrades("斫基损契玄经");
        runtime.AfterSkillResolved(owner, Load("斫基损契玄经", "化法/焚元"));
        var payments = runtime.PreviewBoardPayments(owner, Skill("复命"), Skill("云帔"));
        Check(payments[0] == (1, 0, 1) && payments[1] == (0, 1, 0), "焚元代付连续预览只免复命一笔法力");
        runtime.BeginRound(owner); payments = runtime.PreviewBoardPayments(owner, Skill("复命"), Skill("云帔"));
        Check(payments[0] == (0, 1, 0) && payments[1] == (0, 1, 0), "焚元未用代付轮末失效");

        var actualState = _manager.get_board_state(owner)!;
        // 使用真实管理器账本，验证公共查询不再落回旧十槽的实例引用。
        var fumingItem = new CombatBoardPlacement { placement_id = "cooldown", source_key = Skill("复命").get_board_skill_key() };
        var bigger = new CombatBoardLayout { size = actualState.Layout.size, placements = [fumingItem] };
        actualState.ReplaceLayoutAtRoundBoundary(bigger); actualState.BeginRound(actualState.Round + 1);
        actualState.TryReadNextCell(out _); Check(actualState.RecordSuccessfulCast(fumingItem), "真实账本记录复命一次");
        var clone = (ActiveSkillData)Skill("复命").Duplicate(true);
        Check(_manager.get_skill_cooldown_rounds(owner, clone) == 3 && !_manager.is_skill_ready(owner, clone), "克隆招式通用冷却及可用查询读取稳定账本");

        // 轮次状态在获得当轮不递减，随后一个完整共同回合结束才移除。
        owner.battle_round_id = 7;
        owner.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/身法.tres"), 1, 1);
        owner.process_status_event("turn_end", new()); Check(owner.get_status_stacks("身法") > 0, "轮次状态获得当轮保持");
        owner.battle_round_id = 8; owner.process_status_event("turn_end", new());
        Check(owner.get_status_stacks("身法") == 0, "轮次状态按下一完整共同回合结束移除");
    }

    private async Task RunBalanceAudit(string auditPath)
    {
        await Setup(Unit("数值审计", 1, new()), Unit("审计目标", 2, new()));
        var owner = (Character)_manager.get_main_player_unit()!;
        var target = (Character)_manager.enemy_units[0];
        var profiles = new[]
        {
            (Name: "基础", P: 5f, I: 5f, S: 5f, Body: 5f, Magic: 5f, Sword: 5f),
            (Name: "均衡10", P: 10f, I: 10f, S: 10f, Body: 10f, Magic: 10f, Sword: 10f),
            (Name: "均衡20", P: 20f, I: 20f, S: 20f, Body: 20f, Magic: 20f, Sword: 20f),
            (Name: "体道", P: 20f, I: 5f, S: 5f, Body: 20f, Magic: 5f, Sword: 5f),
            (Name: "术法", P: 5f, I: 20f, S: 10f, Body: 5f, Magic: 20f, Sword: 5f),
            (Name: "剑道", P: 10f, I: 5f, S: 5f, Body: 5f, Magic: 5f, Sword: 20f)
        };
        var results = new List<object>();
        var heartResults = new List<object>();
        var swordResults = new List<object>();
        foreach (var profile in profiles)
        {
            owner.physique = profile.P; owner.intelligence = profile.I; owner.soul = profile.S;
            if (owner.entity_data is HumanData human)
            { human.body_proficiency = profile.Body; human.magic_proficiency = profile.Magic; human.sword_proficiency = profile.Sword; }
            owner.main_gongfa = null; _manager.gongfa_combat.Begin(owner);
            owner.SetMeta("combat_heart_method_upgrades", new Godot.Collections.Dictionary());
            owner.consume_status_stacks("剑意", 100); owner.qi_blood = 0;
            foreach (var skill in _skills.Values)
            {
                var raw = skill.calculate_damage(owner);
                var blocks = skill.stacks_formula.Length > 0 ? CardRules.Block(skill, owner) : 0;
                var shield = skill.special_effects.Where(effect => effect.GetValueOrDefault("effect", "").AsString() == "shield")
                    .Sum(effect => Mathf.RoundToInt(CardRules.Evaluate(effect.GetValueOrDefault("formula", "0").AsString(), owner)));
                var defenses = new Dictionary<int, int>();
                foreach (var defense in new[] { 0, 4, 10 })
                {
                    target.physical_defense = defense;
                    defenses[defense] = raw <= 0 ? 0 : BattleActionResolver.apply_defense(Mathf.RoundToInt(raw), skill.damage_type, target);
                }
                results.Add(new { profile = profile.Name, skill = skill.skill_name, energy = skill.energy_cost, mana = skill.mana_cost,
                    cells = skill.board_size.X * skill.board_size.Y, max_placements = skill.board_max_placements,
                    battle_limit = skill.uses_per_battle, raw_damage = raw, blocks, shield, defenses });
            }
            foreach (var name in new[] { "百骸衍枝化生宝册", "舒卷天章" })
            {
                var book = GD.Load<GongfaResource>($"res://resources/gongfa/{name}.tres");
                owner.main_gongfa = book;
                foreach (var order in new[] { 1, 2, 4 })
                {
                    _manager.gongfa_combat.Begin(owner);
                    var upgrades = new Godot.Collections.Dictionary();
                    foreach (var chapter in book.get_sorted_chapters().Where(chapter => chapter.order <= order && chapter.heart_method_upgrade_id.Length > 0))
                        upgrades[name + "|" + chapter.heart_method_upgrade_id] = true;
                    owner.SetMeta("combat_heart_method_upgrades", upgrades);
                    _manager.gongfa_combat.Utility(owner, book.heart_method_skill!);
                    foreach (var stacks in new[] { 0, 3, 5, 6, 7, 8, 10 })
                    {
                        owner.qi_blood = Math.Min(owner.max_qi_blood, stacks);
                        owner.consume_status_stacks("剑意", 100);
                        if (stacks > 0) owner.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/剑意.tres"), -1, stacks);
                        _manager.gongfa_combat.Before(owner, Skill("斩剑"));
                        heartResults.Add(new { profile = profile.Name, book = name, chapter = order, stacks,
                            blood_arms = _manager.gongfa_combat.BloodArmCount(owner),
                            arm_damage = _manager.gongfa_combat.PreviewBloodArmDamage(owner, owner.qi_blood),
                            cloud_segments = _manager.gongfa_combat.CloudFollowUpLimit(owner, Skill("斩剑"), true),
                            cloud_damage = _manager.gongfa_combat.CreateCloudFollowUp(owner, Skill("斩剑")).calculate_damage(owner) });
                        if (name == "舒卷天章" && order == 4 && stacks is 0 or 5 or 10)
                        {
                            foreach (var swordName in new[] { "斩剑", "尽锋", "云岚三叠" })
                            {
                                var sword = Skill(swordName);
                                _manager.gongfa_combat.Before(owner, sword);
                                var raw = Mathf.RoundToInt(sword.calculate_damage(owner));
                                var extra = Mathf.RoundToInt(_manager.gongfa_combat.CreateCloudFollowUp(owner, sword).calculate_damage(owner));
                                var limit = _manager.gongfa_combat.CloudFollowUpLimit(owner, sword, true);
                                var chances = Enumerable.Range(0, limit).Select(index => _manager.gongfa_combat.CloudFollowUpChance(owner, sword, index)).ToArray();
                                foreach (var defense in new[] { 0, 4, 10 })
                                {
                                    target.physical_defense = defense;
                                    var baseDamage = BattleActionResolver.apply_defense(raw, "physical", target);
                                    var extraDamage = BattleActionResolver.apply_defense(extra, "physical", target);
                                    foreach (var hit in new[] { 1f, .75f })
                                    {
                                        var probability = hit;
                                        var expected = baseDamage * hit;
                                        foreach (var chance in chances)
                                        {
                                            probability *= chance * hit;
                                            expected += probability * extraDamage;
                                        }
                                        swordResults.Add(new { profile = profile.Name, sword = swordName, stacks, defense, hit,
                                            energy = sword.energy_cost, mana = sword.mana_cost, base_damage = raw, extra_damage = extra,
                                            chances, expected_damage = expected, expected_per_energy = expected / sword.energy_cost });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        var output = new { source = "真实ActiveSkillData、CardRules、BattleActionResolver及心法运行时计算；六档属性、三档物防；不伪称完整胜率模拟",
            expectation_scope = "四卷舒卷主修；分别假设每段100%或75%命中，无会心、应对、护盾与其他加成；追加须前段命中且概率成功，不计算击杀中止；完整付费另列",
            skills = results, hearts = heartResults, sword_comparisons = swordResults };
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(auditPath)!);
        System.IO.File.WriteAllText(auditPath, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        GD.Print($"HUMAN_BALANCE_AUDIT_OK skills={results.Count} hearts={heartResults.Count} swords={swordResults.Count} path={auditPath}");
    }

}
