using Godot;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>隔离真实物品、扫盘与正式场景回写；测试产物只存在隔离 user://。</summary>
public partial class TalismanCombatSmoke : Node
{
    private int _checks;
    private Node2D _local = null!;
    private BattleManager _manager = null!;
    private PackedScene _character = null!;
    private string _guard = "", _arrow = "";
    private void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        _checks++; GD.Print("TALISMAN_COMBAT_PASS " + message);
    }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate, string message, int seconds = 40)
    {
        var deadline = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!predicate() && Time.GetTicksMsec() < deadline) await Frames();
        Check(predicate(), message);
    }
    private BattleBoardHud Hud => _local.GetNode<CanvasLayer>("UILayer").GetChildren().OfType<BattleBoardHud>().Single();
    private static CombatBoardPlacement Put(string path, string id = "symbol") => new()
        { placement_id = id, kind = CombatBoardPlacement.ObjectKind.Talisman, source_key = path, cell = Vector2I.Zero };
    private BattleEntityData Unit(string id, int faction, CombatBoardLayout layout, string path = "", int count = 0) => new()
    {
        uid = id, entity_name = id, faction = faction, cultivation_realm = "云息三层",
        max_health = 1000, current_health = 1000, max_stamina = 6, current_stamina = 0,
        max_mana = 3, current_mana = 3, test_mana_cap_override = 3,
        physique = 10, intelligence = 10, soul = 10, precision = 100000, dodge = 0,
        critical = 0, crit_resist = 100000, physical_defense = 0, initiative_speed = faction == 1 ? 10000 : 5000,
        combat_skill_slots_configured = true, combat_resolved_skills = [], combat_board_layout = layout,
        pack_state_included = true, pack_item_paths = path.Length > 0 ? [path] : [],
        pack_item_counts = path.Length > 0 ? [count] : [], pack_item_positions = path.Length > 0 ? [0] : []
    };
    private async Task Setup(string path, int count)
    {
        _manager.setup_team_battle([_character], [_character],
            [Unit("符箓使用者", 1, new() { placements = [Put(path)] }, path, count)],
            [Unit("对练者", 2, new())]);
        await Frames(3);
        Check(_manager.is_board_battle() && _manager.is_board_editing(), "真实人类新盘打开");
    }
    private void Begin()
    {
        Check(_manager.commit_player_board(Hud.Draft, out var reason), "提交真实预留 " + reason);
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        try
        {
            var project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            var user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            Check(new[] { "output", "tmp", ".godot/test-results" }.Any(part => user.StartsWith(
                System.IO.Path.GetFullPath(System.IO.Path.Combine(project, part)) + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)), "只在项目内隔离用户目录运行");
            Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "没有打开正式旅程");
            CheckDefinitions();
            Check(TalismanCraftingService.TrySaveProduct(new() { Kind = "talisman", RecipeId = "hushen", Tier = 0 }, out _guard, out _), "隔离保存真实护身符定义");
            Check(TalismanCraftingService.TrySaveProduct(new() { Kind = "talisman", RecipeId = "yanshi", Tier = 0 }, out _arrow, out _), "隔离保存真实炎矢符定义");
            _local = new Node2D { Name = "TalismanBattleIsolated" }; AddChild(_local);
            _local.AddChild(new Node2D { Name = "DeployLayer" });
            var ui = new CanvasLayer { Name = "UILayer" }; _local.AddChild(ui);
            ui.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn").Instantiate());
            _manager = new BattleManager { Name = "BattleMgr", deploy_layer = "../DeployLayer", isolated_trial_battle = true }; _local.AddChild(_manager);
            _character = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            await CheckGuardAndRefill();
            await CheckArrow();
            _local.QueueFree(); await Frames(4);
            await CheckFormalReturn();
            GD.Print($"TALISMAN_COMBAT_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("TALISMAN_COMBAT_SMOKE_FAILED " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private void CheckDefinitions()
    {
        foreach (var recipe in new[] { "hushen", "yanshi" })
        {
            var name = TalismanCombatCatalog.Name(recipe);
            var markdown = Godot.FileAccess.GetFileAsString($"res://docs/卡牌词典/符箓/{name}.md");
            var diagram = XDocument.Parse(Godot.FileAccess.GetFileAsString($"res://docs/卡牌词典/符箓/{name}.drawio"));
            Check(diagram.Root!.Elements("diagram").Any(page => (string?)page.Attribute("name") == "策划说明"), name + "内部说明独立成页");
            string values = string.Join(" | ", Enumerable.Range(0, 5).Select(tier => TalismanCombatCatalog.Amount(recipe, tier)));
            Check(markdown.Contains("| " + values + " |", StringComparison.Ordinal), name + "词典五档与候选目录一致");
            for (int tier = 0; tier < 5; tier++)
            {
                var skill = GD.Load<ActiveSkillData>(TalismanCombatCatalog.SkillPath(recipe, tier));
                Check(skill is not null && skill.energy_cost == 1 && skill.mana_cost == 0 && skill.spirit_occupy == 0
                    && TalismanCombatCatalog.IsSkill(skill) && !skill.has_board_definition(), $"{name}{tier}固定资源费用/实物来源/不入技能池");
                Check(skill!.damage_bonuses.Count == 0 && skill.damage_formula.Length == 0 && skill.status_effects.Count == 0
                    && skill.gongfa_rewrite_effect_ids.Count == 0 && skill.description == TalismanCombatCatalog.EffectText(recipe, tier), $"{name}{tier}无隐藏属性倍率或额外状态");
                Check(recipe == "yanshi"
                    ? skill.target_type == 2 && skill.attack_style == "ranged" && skill.damage_type == "physical" && skill.base_damage == TalismanCombatCatalog.Amount(recipe, tier) && skill.special_effects.Count == 0
                    : skill.target_type == 0 && skill.base_damage == 0 && skill.special_effects.Count == 1 && CardRules.Evaluate(skill.special_effects[0]["formula"].AsString(), null) == TalismanCombatCatalog.Amount(recipe, tier),
                    $"{name}{tier}目标与固定效果一致");
            }
        }
        var malformed = new TalismanProductItemData { snapshot_json = JsonSerializer.Serialize(new TalismanCraftingService.ProductSnapshot { Kind = "paper", RecipeId = "hushen" }) };
        Check(!TalismanCombatCatalog.IsBattleProduct(malformed), "纸墨笔不会被当成战斗符");
        malformed.snapshot_json = "{invalid}";
        Check(!TalismanCombatCatalog.IsBattleProduct(malformed), "损坏符快照不可用");
        malformed.Dispose();
    }

    private async Task CheckGuardAndRefill()
    {
        await Setup(_guard, 2);
        var actor = (Character)_manager.get_main_player_unit()!;
        var catalog = _manager.get_board_catalog(actor)!;
        var overbooked = Hud.Draft.CopyConfiguration();
        overbooked.size = new(3, 3);
        overbooked.placements.Add(new() { placement_id = "extra1", kind = CombatBoardPlacement.ObjectKind.Talisman, source_key = _guard, cell = new(1, 0) });
        overbooked.placements.Add(new() { placement_id = "extra2", kind = CombatBoardPlacement.ObjectKind.Talisman, source_key = _guard, cell = new(2, 0) });
        Check(CombatBoardGeometry.Validate(overbooked, catalog.FindSkill, pillCount: actor.get_inventory_total_count).Count > 0, "三块不能复用两张实符");
        Check(CombatBoardGeometry.Footprint(Put(_guard), catalog.FindSkill) == new Vector2I(1, 2), "符箓真实占格1×2");
        var rotated = Put(_guard); rotated.quarter_turns = 1;
        Check(CombatBoardGeometry.Footprint(rotated, catalog.FindSkill) == new Vector2I(2, 1), "符箓转向真实占格2×1");
        Hud.SelectCategory("符箓"); await Frames();
        Check(Hud.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("护身符")), "真实符进入符箓来源列表");
        Begin(); actor.current_stamina = 0;
        await Until(() => _manager.is_board_editing(), "费用不足的首轮扫盘完成");
        var state = _manager.get_board_state(actor)!;
        Check(actor.get_inventory_total_count(_guard) == 2 && !state.Layout.placements.Single().expended && actor.shield == 0, "费用不足不扣符、不发护盾");
        Check(actor.current_stamina > 0 && state.HasBeenChecked("symbol") && !state.CanUseTalisman(state.Layout.placements.Single()), "后续空格恢复体力也不在第二占格重试");
        int chargedStamina = -1;
        actor.InventoryChanged += _ => chargedStamina = actor.current_stamina;
        actor.current_stamina = 0;
        Begin();
        await Until(() => _manager.is_board_editing(), "第二轮实际施符完成");
        Check(chargedStamina == 2 && actor.get_inventory_total_count(_guard) == 1, "轮初3体力只扣1并真实消耗一张");
        Check(actor.shield == TalismanCombatCatalog.Amount("hushen", 0), "护身符进入公共护盾");
        Check(state.Layout.placements.Single().expended && CombatBoardGeometry.At(state.Layout, Vector2I.Down, catalog.FindSkill) is { expended: true }, "符痕保留两个真实占格");
        Begin(); await Until(() => _manager.is_board_editing(), "不补符的第三轮完成");
        Check(actor.get_inventory_total_count(_guard) == 1, "符痕跨轮不再次施放或消耗");
        Check(Hud.MovePlacement("symbol", Vector2I.Zero), "轮末选中已有符痕");
        Hud.RefillSelected();
        Check(!Hud.Draft.placements.Single().expended && state.Layout.placements.Single().expended && actor.get_inventory_total_count(_guard) == 1, "补符草稿只预留、不改运行符痕或扣实物");
        Begin(); await Until(() => _manager.is_board_editing(), "补入实符后再次完成施符");
        Check(actor.get_inventory_total_count(_guard) == 0 && state.Layout.placements.Single().expended, "补入的一张只消费一次");
        Hud.MovePlacement("symbol", Vector2I.Zero); Hud.RefillSelected();
        Check(Hud.Draft.placements.Single().expended && !Hud.TryPlaceSource(_guard, new(1, 0)), "库存耗尽不能补符或伪造新块");
        var snapshot = actor.get_snapshot_data();
        Check(snapshot.pack_state_included && !snapshot.pack_item_paths.Contains(_guard) && snapshot.combat_board_layout!.placements.Single().expended, "战斗结果同时携带已消费行囊与符痕");
        Check(BattleSupplyRules.Load(_guard) is null, "旧三战备位继续不接受符箓");
    }

    private async Task CheckArrow()
    {
        await Setup(_arrow, 1);
        var actor = (Character)_manager.get_main_player_unit()!;
        var foe = (Character)_manager.enemy_units[0];
        int health = foe.current_health;
        var original = GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
        Begin(); _manager.gongfa_combat.Before(actor, original);
        int mana = actor.current_mana;
        await Until(() => _manager.is_board_editing(), "炎矢符真实攻击结算完成");
        Check(health - foe.current_health == TalismanCombatCatalog.Amount("yanshi", 0) && actor.get_inventory_total_count(_arrow) == 0, "炎矢一次固定基础物伤与真实扣符");
        Check(actor.current_mana == mana && _manager.get_battle_qi(actor) == 0, "施符不花法力、不自行产炁");
        Check(ReferenceEquals(_manager.gongfa_combat.GetLastNonResponseSkill(actor), original), "施符不替换复命的上次普通招式");
        var state = _manager.get_board_state(actor)!;
        Check(!state.CanUsePlacement(state.Layout.placements.Single(), zhouTian: true), "周天不能再次施放消耗符");
    }

    private async Task CheckFormalReturn()
    {
        var world = GD.Load<PackedScene>("res://tscn/debug/HumanCombatBoardReturnWorld.tscn").Instantiate<HumanCombatBoardReturnWorld>();
        GetTree().Root.AddChild(world); GetTree().CurrentScene = world; await Frames(4);
        Check(world.Player.add_item_to_inventory(_arrow, 2), "在隔离世界角色放入真实成符");
        var original = world.Player.get_snapshot_data(); original.initiative_speed = 10000; original.precision = 100000;
        original.combat_board_layout = new() { placements = [Put(_arrow)] };
        var enemy = Unit("temp_enemy_talisman_return", 2, new()); enemy.current_health = enemy.max_health = 1; enemy.initiative_speed = 50;
        var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        transition.set_battle_return_context(new Godot.Collections.Dictionary { ["is_training"] = false, ["title"] = "符箓真实消费验收", ["allow_prebattle_cancel"] = true });
        transition.start_team_battle([_character], [_character], [original], [enemy], "res://tscn/debug/HumanCombatBoardReturnWorld.tscn");
        await Until(() => transition.has_open_flow_view(), "正式战前页打开");
        transition.GetChildren().OfType<BattleFlowView>().Single().FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.Text == "开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "进入正式符箓战斗场景");
        var manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
        var hud = GetTree().CurrentScene.GetNode<CanvasLayer>("UILayer").GetChildren().OfType<BattleBoardHud>().Single();
        var battleActor = (Character)manager.player_units[0];
        Check(manager.commit_player_board(hud.Draft, out _), "正式场景提交成符预留");
        await Until(() => !manager.battle_running && transition.has_open_flow_view() && !transition.is_flow_transition_active(), "实际施符击败目标进入正式结算");
        Check(((Character)manager.player_units[0]).get_inventory_total_count(_arrow) == 1, "战斗副本只扣一张");
        var playerState = manager.get_board_state(battleActor)!;
        var firstSceneVisits = playerState.VisitFeedbackEvents.Select(visit => visit.Cell).ToArray();
        Check(firstSceneVisits.SequenceEqual(new[] { Vector2I.Zero, Vector2I.Right, new Vector2I(2, 0) })
            && battleActor.current_stamina == 4, "先按列序预扫本幕空格并回复体力，再施符击杀");
        transition.GetChildren().OfType<BattleFlowView>().Single().FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.Text is "返回" or "返回并领取").EmitSignal(BaseButton.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene is HumanCombatBoardReturnWorld result
            && ((HumanData)result.Player.entity_data!).combat_board_layout?.placements.Any(p => p.kind == CombatBoardPlacement.ObjectKind.Talisman && p.expended) == true
            && !transition.is_flow_transition_active(), "正式返回世界并恢复符痕");
        world = (HumanCombatBoardReturnWorld)GetTree().CurrentScene;
        Check(world.Player.get_inventory_total_count(_arrow) == 1, "消费回写世界本人行囊");
        world.GetNode<CombatTrigger>("CombatTrigger").check_and_restore_battle_results();
        Check(world.Player.get_inventory_total_count(_arrow) == 1, "重复恢复不重复扣符");
        var save = new PersistentGameSave { player_data = (HumanData)world.Player.entity_data! };
        const string path = "user://talisman_combat_smoke_return.tres";
        Check(ResourceSaver.Save(save, path) == Error.Ok, "隔离保存已消费世界角色");
        var reloaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        var actor = new Character(); actor.load_from_data(reloaded.player_data!);
        Check(actor.get_inventory_total_count(_arrow) == 1 && reloaded.player_data!.combat_board_layout!.placements.Any(p => p.expended), "重载后真实库存与符痕均保持");
        actor.Free();
        Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "正式往返未打开用户旅程");
    }
}
