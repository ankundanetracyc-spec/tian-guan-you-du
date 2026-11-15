using Godot;
using System.Reflection;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>隔离人物验证首批丹药的识别、真实消费、状态生命周期和正式领取。</summary>
public partial class AlchemyExpansionSmoke : Node
{
    private int _checks;
    private readonly List<Character> _actors = [];
    private static readonly Dictionary<string, ItemData> MaterialDefinitions = [];
    // 批量构造临时资源时保持类型脚本存活，避免自定义引擎的弱绑定随GC失效。
    private readonly List<CSharpScript> _resourceScripts = [];
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); _checks++; }
    private static string Path(string id, int tier = 2) => AlchemyProductCatalog.Path(id, tier);
    private Character Actor()
    {
        var actor = new Character { entity_data = new HumanData { base_alchemy_proficiency = 36, alchemy_proficiency = 36 } };
        _actors.Add(actor); AddChild(actor);
        actor.max_health = 1000; actor.current_health = 100; actor.max_mana = 20; actor.current_mana = 0;
        actor.max_world_stamina = 100; actor.current_world_stamina = 100;
        actor.max_spirit_perception = 10000; actor.battle_round_id = 1;
        return actor;
    }
    private void Prepare(Character actor, string id, int tier = 2, int count = 3)
    {
        Check(actor.add_item_to_inventory(Path(id, tier), count), id + "真实入包");
        Check(actor.set_battle_supply(0, Path(id, tier), count), id + "真实战备");
    }
    private static AlchemySlotSimulation.Input[] Inputs(AlchemyRecipeCatalog.SampleRoute route) => route.Ingredients.Select(i =>
    {
        string path = "res://resources/entities/items/" + i.HerbName + ".tres";
        if (!MaterialDefinitions.TryGetValue(path, out var item)) MaterialDefinitions[path] = item = GD.Load<ItemData>(path);
        var role = i.AssistantSlot == -1 ? AlchemySlotSimulation.SlotRole.Jun : i.AssistantSlot == 0
            ? AlchemySlotSimulation.SlotRole.Chen : i.AssistantSlot is 1 or 2 or 4 ? AlchemySlotSimulation.SlotRole.Zuo : AlchemySlotSimulation.SlotRole.Shi;
        return new AlchemySlotSimulation.Input(i.HerbName, path, (MaterialCraftData)item.craft_data!, i.Dose, role);
    }).ToArray();
    private void Routes(bool diagnostics)
    {
        foreach (var pill in AlchemyRecipeCatalog.Pills)
        foreach (var route in pill.SampleRoutes)
        foreach (int a in new[] { 8, 36, 68 })
        {
            var inputs = Inputs(route); var result = AlchemySlotSimulation.Simulate(inputs, a, 10000);
            GD.Print($"EXPANSION_ROUTE expected={pill.Id} route={route.Name} A={a} actual={result.ProductId} Q={result.Quality} N={result.OutputCount} L={result.SpiritNeed} E={result.EnergyNeed} axes={result.CoreQuality:0.#}/{result.PolarityQuality:0.#}/{result.ConnectionQuality:0.#}/{result.StabilityQuality:0.#}/{result.PurityQuality:0.#} blocked={result.BlockingReason}");
            if (!diagnostics && inputs.GroupBy(i => i.Role).All(g => g.Count() <= AlchemySlotSimulation.RoleCapacity(g.Key, a)))
            {
                if (pill.Rank == 7 && a < 68)
                {
                    Check(!result.CanSimulate && result.BlockingReason.Contains("68") &&
                        AlchemySlotSimulation.IdentifyPill(inputs)?.Id == pill.Id, "七品丹未守造诣68准入或退选其他丹种");
                    continue;
                }
                Check(result.CanSimulate && result.ProductId == pill.Id && result.OutputCount > 0, "样方不能炼成标示丹种：" + route.Name);
                var again = AlchemySlotSimulation.Simulate(inputs.AsEnumerable().Reverse().ToArray(), a, 10000);
                Check(result.Pills.Select(p => p.Quality).SequenceEqual(again.Pills.Select(p => p.Quality)), "投料枚举改变结果");
                Check(Math.Abs(result.Pills.Sum(p => p.SourceMasses.Values.Sum()) - result.DeliveredMass) < 1e-6, "逐颗分配不守恒");
                Check(AlchemySlotSimulation.SpiritOccupation(inputs) == result.SpiritNeed, "选中核心灵识显示与正式值不同");
            }
        }
    }
    private void Explore()
    {
        foreach (var id in new[] { "ninggang", "jinji", "qingshen", "xusheng", "jiegu", "hezhong", "naling", "fanyuan" })
        {
            var pill = AlchemyRecipeCatalog.Pills.Single(p => p.Id == id);
            var route = pill.SampleRoutes[0]; var materialSets = new List<AlchemyRecipeCatalog.SampleIngredient[]> { route.Ingredients };
            var alternatives = id switch
            {
                "ninggang" => new[] { (0, "斑甲壳片"), (0, "霜叶枝"), (2, "清音玉片"), (2, "回泉珠"), (2, "灵泉露") },
                "jinji" => [(0, "铜髓矿"), (2, "回泉珠")], "qingshen" => [(0, "乌羽片")],
                "xusheng" => [(0, "砾地菌伞"), (2, "月露菌汁"), (2, "清音玉片")],
                "jiegu" => [(0, "裂甲石")], "hezhong" => [(0, "回泉珠")], "naling" => [(0, "青髓芽"), (0, "回泉珠")],
                _ => new[] { (0, "青髓芽"), (1, "凝露松脂"), (2, "清音玉片"), (2, "回泉珠"), (3, "朱阳花籽") }
            };
            foreach (var (index, material) in alternatives)
            {
                var copy = route.Ingredients.ToArray(); copy[index] = copy[index] with { HerbName = material };
                materialSets.Add(copy);
            }
            var best = new List<(AlchemySlotSimulation.Result Result, AlchemyRecipeCatalog.SampleIngredient[] Ingredients)>();
            foreach (var ingredients in materialSets)
            {
                var seeds = Inputs(new("搜索", "", ingredients));
                for (int a = 1; a <= 4; a++) for (int b = 1; b <= 3; b++) for (int c = 1; c <= 3; c++) for (int d = 1; d <= 3; d++)
                {
                    int[] doses = [a, b, c, d]; var inputs = seeds.Select((i, n) => i with { Dose = doses[n] }).ToArray();
                    if (AlchemySlotSimulation.IdentifyPill(inputs)?.Id != id) continue;
                    var result = AlchemySlotSimulation.Simulate(inputs, 8, 24);
                    if (result.CanSimulate && result.OutputCount > 0)
                        best.Add((result, ingredients.Select((i, n) => i with { Dose = doses[n] }).ToArray()));
                }
            }
            foreach (var candidate in best.OrderByDescending(x => x.Result.Quality).ThenBy(x => x.Result.SpiritNeed)
                .DistinctBy(x => x.Ingredients[0].HerbName + ":" + x.Ingredients[2].HerbName).Take(3))
                GD.Print($"EXPANSION_FOUND id={id} Q={candidate.Result.Quality} N={candidate.Result.OutputCount} L={candidate.Result.SpiritNeed} E={candidate.Result.EnergyNeed} " + string.Join("/", candidate.Ingredients.Select(i => i.HerbName + i.Dose)));
        }
    }
    private void Consumption()
    {
        foreach (var id in new[] { "huichun", "ninggang", "jinji", "qingshen", "xusheng", "jiegu", "hezhong", "naling", "fanyuan" })
        {
            var effects = new HashSet<string>();
            for (int tier = 0; tier < 5; tier++)
            {
                var item = GD.Load<PillItemData>(Path(id, tier)); Check(item.validate_definition().Count == 0, "五档正式定义无效：" + id);
                Check(effects.Add(item.EffectText), "同种药两档基础药效重复：" + id);
                Check(AlchemyPillVisual.Hover(item).Body.Contains(item.EffectText.TrimEnd('。')) || id == "huichun", "展示未读取正式药效");
            }
        }
        Check(AlchemyProductCatalog.Healing.SequenceEqual(new[] { 30, 50, 80, 120, 160 }), "回春未采用确认数值");
        Check(BattleSupplyRules.Load(Path("naling")) is null && BattleSupplyRules.Load(Path("fanyuan")) is null, "世界丹进入战备");
        Check(BattleSupplyRules.Load(Path("tongmai")) is null && !AlchemyProductCatalog.IsEnabled("jingxin"), "下架丹意外开放");
        var shield = Actor(); Prepare(shield, "ninggang"); shield.shield = 10;
        Check(shield.TryUseBattlePill(0, 20, out var gained) && gained == 64 && shield.shield == 74, "凝罡未加真实护盾");
        Check(shield.get_inventory_total_count(Path("ninggang")) == 2 && shield.battle_supplies[0].count == 2 && shield.PillToxicity == 9, "消费与丹毒不一致");
        var defense = Actor(); Prepare(defense, "jinji"); int basis = defense.physical_defense;
        Check(defense.TryUseBattlePill(0, 20, out _) && CardRules.Number(defense, "physical_defense") == basis + 3 && defense.physical_defense == basis, "金肌未作临时物防");
        Check(!defense.TryUseBattlePill(0, 20, out _), "刚服同档无收益重复扣药");
        defense.process_status_event("turn_end", new()); Check(defense.get_status_stacks(Character.DefensePillStatus) == 1, "获得回合提前扣时长");
        defense.battle_round_id = 2; defense.process_status_event("turn_end", new()); Check(defense.status_bonus("physical_defense") == 3, "未满两回合提前消失");
        defense.battle_round_id = 3; defense.process_status_event("turn_end", new()); Check(defense.status_bonus("physical_defense") == 0 && defense.physical_defense == basis, "临时物防未恢复");
        var refresh = Actor(); Prepare(refresh, "jinji", 4); Check(refresh.TryUseBattlePill(0, 20, out _), "高档金肌失败");
        Prepare(refresh, "jinji", 0); Check(!refresh.TryUseBattlePill(0, 20, out _) && refresh.get_inventory_total_count(Path("jinji", 0)) == 3, "低档覆盖高档");
        var agility = Actor(); agility.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/身法.tres"), stacks: 4); Prepare(agility, "qingshen", 4);
        Check(agility.TryUseBattlePill(0, 20, out _) && agility.get_status_stacks("身法") == 20 && !agility.TryUseBattlePill(0, 20, out _), "身法上限或无收益保护失效");
        var regen = Actor(); Prepare(regen, "xusheng"); var data = (HumanData)regen.entity_data!; data.pill_toxicity = 40;
        Check(regen.TryUseBattlePill(0, 20, out _) && regen.current_health == 100 && regen.PillToxicity == 47, "续生提前治疗或重复计毒");
        data.pill_toxicity = 120; regen.battle_round_id = 2; regen.process_status_event("turn_start", new());
        Check(regen.current_health == 145, "持续治疗未锁定服前吸收快照");
        regen.process_status_event("turn_end", new()); regen.battle_round_id = 3; regen.process_status_event("turn_start", new());
        Check(regen.current_health == 190 && regen.get_status_stacks(Character.RegenerationPillStatus) == 0, "续生第二次未完成或未移除");
        regen.process_status_event("turn_start", new()); Check(regen.current_health == 190 && regen.PillToxicity == 120, "续生第三次治疗或再次加毒");
        foreach (var (id, wound, other) in new[] { ("jiegu", "外伤", "内伤"), ("hezhong", "内伤", "外伤") })
        {
            var actor = Actor(); Prepare(actor, id); actor.add_status(GD.Load<StatusEffect>($"res://resources/battle/statuses/{wound}.tres"), stacks: 4);
            actor.add_status(GD.Load<StatusEffect>($"res://resources/battle/statuses/{other}.tres"), stacks: 3);
            Check(actor.TryUseBattlePill(0, 20, out _) && actor.get_status_stacks(wound) == 2 && actor.get_status_stacks(other) == 3 && actor.current_health == 124, "疗伤丹混淆伤势或未回血");
            actor.consume_status_stacks(wound, 4); Check(!actor.TryUseBattlePill(0, 20, out _) && actor.get_inventory_total_count(Path(id)) == 2, "无对应伤势仍然吃药回血");
        }
        var energy = Actor(); Check(energy.add_item_to_inventory(Path("fanyuan"), 2), "返元入包"); energy.current_world_stamina = 1; energy.current_stamina = 2;
        ((HumanData)energy.entity_data!).pill_toxicity = 40;
        Check(energy.TryUseAlchemyPill(GD.Load<PillItemData>(Path("fanyuan"))) && energy.current_world_stamina == 17 && energy.current_stamina == 2, "返元恢复错误资源或未按服前丹毒计算");
        energy.current_world_stamina = 100; Check(!energy.TryUseAlchemyPill(GD.Load<PillItemData>(Path("fanyuan"))) && energy.get_inventory_total_count(Path("fanyuan")) == 1, "满精力仍扣药");
        var spirit = Actor(); spirit.cultivation_realm = "云息一层"; Check(spirit.add_item_to_inventory(Path("naling", 4), 3), "纳灵入包");
        var progress = spirit.get_next_realm_progress(); int need = progress["required"].AsInt32(); Check(need > 0, "境界门槛未加载");
        spirit.spirit_strands = need - 1; ((HumanData)spirit.entity_data!).cultivation_progress = .75f;
        Check(spirit.TryUseAlchemyPill(GD.Load<PillItemData>(Path("naling", 4))) && spirit.spirit_strands == need && spirit.get_cultivation_progress() == 0 && spirit.cultivation_realm == "云息一层", "纳灵越过门槛或自动突破");
        Check(!spirit.TryUseAlchemyPill(GD.Load<PillItemData>(Path("naling", 4))), "满门槛仍扣纳灵");
        spirit.cultivation_realm = "云息十层"; Check(!spirit.CanUseAlchemyPill(GD.Load<PillItemData>(Path("naling", 4))), "纳灵进入归元九息");
        string saved = "user://alchemy_expansion_save.tres";
        Check(ResourceSaver.Save(new PersistentGameSave { player_data = (HumanData)shield.entity_data! }, saved) == Error.Ok, "新丹存档失败");
        var loaded = ResourceLoader.Load<PersistentGameSave>(saved, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(loaded.player_data!.inventory_item_paths.Contains(Path("ninggang")) && loaded.player_data.pill_toxicity == 9, "新丹库存或丹毒未保存");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(saved));
    }
    private async Task Receipt()
    {
        var actor = Actor(); var facilities = new LocationCraftingPanel(); AddChild(facilities); facilities.ShowFor(actor, true);
        var room = facilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
        var recipe = AlchemyRecipeCatalog.Pills.Single(p => p.Id == "ninggang").SampleRoutes[0]; var inputs = Inputs(recipe);
        foreach (var input in inputs) Check(actor.add_item_to_inventory(input.Path, input.Dose), "实际投料入包失败");
        if (OS.GetCmdlineUserArgs().Contains("--alchemy-ui-capture"))
        {
            ((Control)typeof(AlchemySlotBrewPreviewScene).GetField("_tutorialOverlay", Hidden)!.GetValue(room)!).Hide();
            typeof(AlchemySlotBrewPreviewScene).GetMethod("OpenRecipeBook", Hidden)!.Invoke(room, null);
            room.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.GetMeta("recipe_name", "").AsString() == recipe.Name).EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("01_九种丹药样方");
            ((Control)typeof(AlchemySlotBrewPreviewScene).GetField("_recipeOverlay", Hidden)!.GetValue(room)!).Hide();
            ((Control)typeof(AlchemySlotBrewPreviewScene).GetField("_tutorialOverlay", Hidden)!.GetValue(room)!).Hide();
        }
        typeof(AlchemySlotBrewPreviewScene).GetMethod("ApplySampleRoute", Hidden)!.Invoke(room, [recipe, false]);
        typeof(AlchemySlotBrewPreviewScene).GetField("_simpleAlchemyPresentation", Hidden)!.SetValue(room, true);
        var beforeTime = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours();
        var task = (Task)typeof(AlchemySlotBrewPreviewScene).GetMethod("BrewWithPresentation", Hidden)!.Invoke(room, null)!;
        for (int frame = 0; frame < 600 && !task.IsCompleted; frame++)
        {
            room.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "确定 · 按丹效装瓶" && b.IsVisibleInTree())?.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Check(task.IsCompleted, "实际炼丹演出未完成"); await task;
        var claim = (ItemClaimPanel?)typeof(AlchemySlotBrewPreviewScene).GetField("_alchemyClaim", Hidden)!.GetValue(room);
        Check(claim is not null && inputs.All(i => actor.get_inventory_total_count(i.Path) == 0), "开炉没有扣料或进入领取");
        await Capture("02_新丹领取");
        Check(actor.current_world_stamina < 100 && GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours() > beforeTime, "开炉未扣精力或未推进历法");
        Check(actor.get_spatial_inventory().All(e => !e.ItemPath.Contains("alchemy/ninggang")), "领取前提前发丹");
        claim!.FindChild("ClaimAutoPlace", true, false)!.Call("emit_signal", "pressed");
        Check(actor.get_spatial_inventory().All(e => !e.ItemPath.Contains("alchemy/ninggang")), "自动放入提前入包");
        claim.FindChild("ClaimFooterPrimary", true, false)!.Call("emit_signal", "pressed");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(actor.get_spatial_inventory().Any(e => e.ItemPath.Contains("alchemy/ninggang")), "新丹确认领取后未入包");
        facilities.ReturnToLocation(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); Check(!facilities.Visible, "领取后不能返回地点"); facilities.QueueFree();
    }
    private async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> condition, string message)
    {
        for (int frame = 0; frame < 3600; frame++) { if (condition()) return; await Frames(1); }
        throw new InvalidOperationException("队列等待超时：" + message);
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--alchemy-ui-capture") || DisplayServer.GetName() == "headless") return;
        await Frames(5); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        const string folder = "res://docs/美术规范/首批八丹接入_20261104/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        using var image = GetViewport().GetTexture().GetImage(); Check(image.SavePng(folder + name + ".png") == Error.Ok, "截图保存失败");
    }
    private async Task QueueIntegration()
    {
        var original = Actor(); original.cultivation_realm = "云息三层";
        foreach (var id in new[] { "jinji", "xusheng", "hezhong", "naling", "fanyuan" })
            Check(original.add_item_to_inventory(Path(id)), "战前真实备药入包");
        Check(original.set_battle_supply(0, Path("jinji"), 1) && original.set_battle_supply(1, Path("xusheng"), 1) && original.set_battle_supply(2, Path("hezhong"), 1), "三种新丹战备配置失败");
        var snapshot = original.get_snapshot_data(); snapshot.uid = "alchemy-expansion-player"; snapshot.entity_name = "试丹修士";
        snapshot.faction = 1; snapshot.combat_skill_slots_configured = true; snapshot.combat_skill_slot_paths = []; snapshot.active_skill_paths = [];
        snapshot.test_mana_cap_override = 40;
        var enemy = new BattleEntityData { uid = "alchemy-expansion-enemy", entity_name = "试丹陪练", faction = 2,
            current_health = 1000, max_health = 1000, physique = 5, intelligence = 5, soul = 5, initiative_speed = 1,
            cultivation_realm = "云息三层", combat_skill_slots_configured = true, pack_state_included = true };
        var world = new Node2D { Name = "AlchemyQueueSmokeWorld" }; GetTree().Root.AddChild(world); GetTree().CurrentScene = world;
        var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        transition.set_battle_return_context(new GDictionary { ["is_training"] = true, ["title"] = "首批八丹队列验证", ["allow_prebattle_cancel"] = true });
        var scene = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
        transition.start_team_battle(new Godot.Collections.Array<PackedScene> { scene }, new Godot.Collections.Array<PackedScene> { scene },
            new Godot.Collections.Array { snapshot }, new Godot.Collections.Array { enemy }, "res://tscn/scene/StartMenu.tscn");
        await Frames();
        GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "配置战备").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames();
        Check(GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.StartsWith("金肌丹 ·")), "战前选择器不显示新战斗丹");
        Check(!GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.StartsWith("纳灵丹 ·") || b.Text.StartsWith("返元丹 ·")), "世界丹出现在战前选择器");
        await Capture("03_新丹战备");
        GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "完成战备配置").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(); GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
        BattleManager? manager = null;
        await Until(() => { manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager; return manager?.battle_running == true && manager.can_player_plan(); }, "进入规划");
        var battle = manager!; var player = (Character)battle.get_main_player_unit()!;
        await Until(() => !transition.is_flow_transition_active() && battle.SupplyHudVisible, "入场过渡完成");
        player.current_health = 100; player.max_health = 1000; player.initiative_speed = 10000;
        battle.add_wound_card(player, "内伤", 2);
        Check(player.get_status_stacks("内伤") == 2, "队列疗伤样本未施加真实内伤");
        foreach (var unit in battle.enemy_units) unit.Set("initiative_speed", 1);
        Check(battle.queue_pill_action(0) && battle.queue_pill_action(1) && battle.queue_pill_action(2), "新丹不能进入实际队列");
        var actions = battle.get_battle_unit(player).skill_queue.Take(3).ToArray();
        Check(actions.All(a => a.target == player && a.kind == BattleQueuedSkill.ActionKind.Pill), "服丹目标或行动类型错误");
        await Capture("04_新丹入队"); battle.confirm_player_queue();
        await Until(() => actions.All(a => a.completed), "三种新丹结算");
        Check(actions.All(a => !a.skipped) && player.status_bonus("physical_defense") == 3 && player.get_status_stacks(Character.RegenerationPillStatus) == 1 && player.get_status_stacks("内伤") == 0,
            $"实际队列效果不符：skipped={string.Join(',', actions.Select(a => a.skipped))} defense={player.status_bonus("physical_defense")} regen={player.get_status_stacks(Character.RegenerationPillStatus)} wound={player.get_status_stacks("内伤")} toxin={player.PillToxicity} round={player.battle_round_id}");
        Check(player.PillToxicity == 19 && player.get_inventory_total_count(Path("jinji")) == 0 && player.get_inventory_total_count(Path("xusheng")) == 0 && player.get_inventory_total_count(Path("hezhong")) == 0, "队列消费或丹毒错误");
        Check(original.get_inventory_total_count(Path("jinji")) == 1, "战斗克隆提前修改世界库存");
        await Capture("05_新丹结算");
        original.apply_battle_data(player.get_snapshot_data());
        Check(original.get_inventory_total_count(Path("jinji")) == 0 && original.get_inventory_total_count(Path("naling")) == 1 && original.PillToxicity == 19, "战末新丹库存或丹毒未回传");
        Check(original.status_bonus("physical_defense") == 0, "局内临时物防泄漏到世界人物");
        battle.QueueFree(); await Frames();
    }
    public override async void _Ready()
    {
        try
        {
            foreach (var path in new[] { "res://csharp/resources/ItemData.cs",
                "res://csharp/resources/HumanData.cs", "res://csharp/battle/StatusEffect.cs", "res://csharp/resources/PillItemData.cs" })
                _resourceScripts.Add(GD.Load<CSharpScript>(path));
            bool diagnostics = OS.GetCmdlineUserArgs().Contains("--alchemy-route-diagnostics");
            if (OS.GetCmdlineUserArgs().Contains("--alchemy-search-routes")) { Explore(); GetTree().Quit(); return; }
            Routes(diagnostics);
            if (!diagnostics) { Consumption(); await Receipt(); await QueueIntegration(); GD.Print($"ALCHEMY_EXPANSION_OK {_checks}"); }
            foreach (var actor in _actors) if (GodotObject.IsInstanceValid(actor)) actor.QueueFree();
            MaterialDefinitions.Clear(); await Frames();
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
        finally { foreach (var actor in _actors) if (GodotObject.IsInstanceValid(actor)) actor.QueueFree(); }
    }
}
