using Godot;
using System.Reflection;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>隔离人物验证突破丹从真实开炉、空间领取到对应关口消费的闭环。</summary>
public partial class BreakthroughAlchemySmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Character> _actors = [];
    private readonly List<CSharpScript> _scripts = [];
    private static readonly Dictionary<string, ItemData> Definitions = [];
    private int _checks;
    private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _checks++; }
    private GameTimeMgr Clock => GetNode<GameTimeMgr>("/root/GameTimeManager");
    private static HumanData Data(Character actor) => (HumanData)actor.entity_data!;
    private Character Actor(double attainment = 68)
    {
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData {
            entity_name = "突破炼丹验收", base_alchemy_proficiency = (float)attainment, alchemy_proficiency = (float)attainment,
            cultivation_realm = "云息三层", spirit_strands = 60, has_spirit_core = true, condensation_final_score = 100,
            spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
            spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
            main_gongfa_path = GongfaAvailability.DefaultBookPath, ambient_lingqi_pressure = 180
        } };
        _actors.Add(actor); AddChild(actor); actor.Hide();
        actor.current_health = actor.max_health = 100; actor.current_world_stamina = actor.max_world_stamina = 100;
        actor.max_spirit_perception = 24; actor.used_spirit_perception = 0;
        return actor;
    }
    private static ItemData Item(string path)
    {
        if (!Definitions.TryGetValue(path, out var item)) Definitions[path] = item = GD.Load<ItemData>(path);
        return item;
    }
    private static AlchemySlotSimulation.Input[] Inputs(AlchemyRecipeCatalog.SampleRoute route) => route.Ingredients.Select(i => {
        string path = "res://resources/entities/items/" + i.HerbName + ".tres";
        var role = i.AssistantSlot == -1 ? AlchemySlotSimulation.SlotRole.Jun : i.AssistantSlot == 0
            ? AlchemySlotSimulation.SlotRole.Chen : i.AssistantSlot is 1 or 2 or 4 ? AlchemySlotSimulation.SlotRole.Zuo : AlchemySlotSimulation.SlotRole.Shi;
        return new AlchemySlotSimulation.Input(i.HerbName, path, (MaterialCraftData)Item(path).craft_data!, i.Dose, role);
    }).ToArray();
    private void Stock(Character actor, IEnumerable<AlchemySlotSimulation.Input> inputs)
    {
        foreach (var input in inputs) Check(actor.add_item_to_inventory(input.Path, input.Dose), "投料入包失败：" + input.Name);
    }
    private void DefinitionsAndRoutes()
    {
        Check(AlchemyProductCatalog.Path("dingmai", 0) == Character.YunxiThreeToFourPillPath &&
            AlchemyProductCatalog.Path("guiyuan_dingxi", 0) == Character.YunxiNineToTenPillPath, "薄效未沿用旧资源路径");
        Check(!AlchemyProductCatalog.IsEnabled("tongmai") && !AlchemyProductCatalog.IsEnabled("jingxin"), "下架丹种被重新开放");
        var actor = Actor();
        foreach (string id in new[] { "dingmai", "guiyuan_dingxi" })
        {
            Check(!AlchemyProductCatalog.IsWorldPill(id) && !AlchemyProductCatalog.IsBattlePill(id), "突破丹用途越界");
            for (int tier = 0; tier < 5; tier++)
            {
                string path = AlchemyProductCatalog.Path(id, tier); var item = (PillItemData)Item(path);
                Check(item.validate_definition().Count == 0 && item.pill_id == id && item.efficacy_tier == tier, "五档定义无效");
                Check(actor.add_item_to_inventory(path), "突破丹真实入包失败");
                Check(!actor.CanUseAlchemyPill(item) && !actor.TryUseAlchemyPill(item) &&
                    !actor.set_battle_supply(0, path, 1) && BattleSupplyRules.Load(path) is null, "突破丹可被提前服用或编入战备");
                Check(actor.get_inventory_total_count(path) == 1, "拒绝使用时扣除了突破丹");
                Check(AlchemyPillVisual.Hover(item).Body.Contains(item.EffectText.TrimEnd('。')), "悬停药效与正式定义不符");
            }
            var choices = actor.get_breakthrough_pill_choices(id == "dingmai" ? "云息四层" : "云息十层");
            Check(choices.Count == 5 && choices.Select(c => c["tier"].AsInt32()).SequenceEqual(Enumerable.Range(0, 5)), "突破选丹未列出本人五档库存");
        }
        var starter = Actor();
        Check(starter.grant_new_journey_yunxi_breakthrough_pills() && starter.grant_new_journey_yunxi_breakthrough_pills() &&
            starter.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 1 &&
            starter.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 1, "新旅程保底来源或重复初始化兼容失效");
        foreach (var pill in AlchemyRecipeCatalog.Pills.Where(p => p.Id is "dingmai" or "guiyuan_dingxi"))
        foreach (var route in pill.SampleRoutes)
        foreach (int a in new[] { 8, 36, 67, 68, 203 })
        {
            var inputs = Inputs(route); var result = AlchemySlotSimulation.Simulate(inputs, a, 24);
            Check(AlchemySlotSimulation.IdentifyPill(inputs)?.Id == pill.Id, "配伍识别错丹：" + route.Name);
            if (pill.Rank == 7 && a < 68) { Check(!result.CanSimulate && result.BlockingReason.Contains("68"), "七品准入未生效"); continue; }
            Check(result.CanSimulate && result.ProductId == pill.Id && result.OutputCount > 0, "样方未成丹：" + route.Name);
            var reversed = AlchemySlotSimulation.Simulate(inputs.AsEnumerable().Reverse().ToArray(), a, 24);
            Check(result.Pills.Select(p => p.Quality).SequenceEqual(reversed.Pills.Select(p => p.Quality)), "配伍顺序改变了结算");
            Check(Math.Abs(result.DeliveredMass - result.Pills.Sum(p => p.SourceMasses.Values.Sum())) < 1e-6, "逐颗质量不守恒");
            if (a is 8 or 68) GD.Print($"BREAKTHROUGH_ROUTE id={pill.Id} route={route.Name} A={a} Q={string.Join('/', result.Pills.Select(p => p.Quality))} N={result.OutputCount} L={result.SpiritNeed} E={result.EnergyNeed} H={AlchemySlotSimulation.DurationHours(inputs)}");
        }
    }
    private void Rejections()
    {
        foreach (string id in new[] { "dingmai", "guiyuan_dingxi" })
        foreach (string fault in new[] { "attainment", "spirit", "energy", "stock" })
        {
            var actor = Actor(fault == "attainment" ? id == "dingmai" ? 7 : 67 : 68);
            var inputs = Inputs(AlchemyRecipeCatalog.Pills.Single(p => p.Id == id).SampleRoutes[0]);
            if (fault != "stock") Stock(actor, inputs);
            if (fault == "spirit") actor.used_spirit_perception = 24;
            if (fault == "energy") actor.current_world_stamina = 0;
            var before = inputs.Select(i => actor.get_inventory_total_count(i.Path)).ToArray();
            var time = Clock.get_precise_game_hours(); var energy = actor.current_world_stamina;
            Check(!actor.TryBrewAlchemy(inputs, out _, out var reason) && reason.Length > 0, "不合法开炉未拦截：" + fault);
            Check(before.SequenceEqual(inputs.Select(i => actor.get_inventory_total_count(i.Path))) && energy == actor.current_world_stamina &&
                Clock.get_precise_game_hours() == time && Data(actor).alchemy_records.Count == 0, "开炉拒绝仍有资源或时间副作用");
        }
    }
    private async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task NotebookGate()
    {
        var actor = Actor(67); var facilities = new LocationCraftingPanel(); AddChild(facilities); facilities.ShowFor(actor, true);
        var room = facilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
        typeof(AlchemySlotBrewPreviewScene).GetMethod("OpenRecipeBook", Hidden)!.Invoke(room, null);
        var button = room.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.GetMeta("recipe_name", "").AsString() == "苦苔定息方");
        Check(button.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Contains("造诣68")), "七品丹列表未显示实际68准入");
        button.EmitSignal(BaseButton.SignalName.Pressed);
        Check(((Button)typeof(AlchemySlotBrewPreviewScene).GetField("_recipeLoad", Hidden)!.GetValue(room)!).Disabled, "造诣67可以载入要求68的内置样方");
        facilities.QueueFree(); await Frames();
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--breakthrough-alchemy-capture") || DisplayServer.GetName() == "headless") return;
        await Frames(5); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        const string folder = "res://docs/美术规范/突破丹炼制接入_20261104/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        using var image = GetViewport().GetTexture().GetImage(); Check(image.SavePng(folder + name + ".png") == Error.Ok, "实机截图失败");
    }
    private void Breakthrough(Character actor, string id, int tier, string path)
    {
        string target = id == "dingmai" ? "云息四层" : "云息十层";
        actor.cultivation_realm = id == "dingmai" ? "云息三层" : "云息九层";
        actor.spirit_strands = id == "dingmai" ? 60 : 324;
        var data = Data(actor); data.pill_toxicity = 200; data.pill_toxicity_last_game_hour = Clock.get_precise_game_hours();
        int before = actor.get_inventory_total_count(path), completions = 0;
        bool observed = false, protectedPill = false, savedReservation = false;
        actor.breakthrough_started += (_, _) => {
            observed = actor.get_inventory_total_count(path) == before && actor.PillToxicity == 200 && data.breakthrough_pill_path == path;
            // 尝试移除整份库存，确认最后一颗预选丹受到保护。
            protectedPill = !actor.remove_item_from_inventory(path, before);
            const string save = "user://breakthrough-alchemy-reservation.tres";
            Check(ResourceSaver.Save(data, save) == Error.Ok, "预选丹存档失败");
            var restored = ResourceLoader.Load<HumanData>(save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            savedReservation = restored.is_in_breakthrough_retreat && restored.breakthrough_pill_path == path &&
                restored.breakthrough_pill_tier == tier && restored.breakthrough_pill_id == id;
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        };
        actor.breakthrough_completed += (_, _) => completions++;
        var started = Clock.get_precise_game_hours();
        Check(actor.start_breakthrough_retreat(false, path), "炼得丹药不能开始对应突破");
        double hours = Clock.get_precise_game_hours() - started;
        double toxicityBefore = PillToxicityRules.Decayed(200, hours);
        Check(observed && protectedPill && savedReservation, "预留、开始不消费或存档兼容失败");
        Check(actor.cultivation_realm == target && !data.is_in_breakthrough_retreat && completions == 1 &&
            actor.get_inventory_total_count(path) == before - 1, "成功晋层未恰好消费所选一颗");
        Check(Math.Abs(actor.PillToxicity - toxicityBefore - PillToxicityRules.Load(id, tier)) < 1e-6, "成功消费丹毒档位或自然排解结算错误");
        if (id == "dingmai") Check(actor.spirit_strands == 60 + PillToxicityRules.Absorb(AlchemyProductCatalog.MeridianProgress[tier], toxicityBefore), "定脉未按服用前丹毒补入四层进度");
        else Check(Math.Abs(actor.get_yuanxi_duration_reduction() - AlchemyProductCatalog.YuanxiDurationReductionPercent[tier] / 100d * PillToxicityRules.Efficiency(toxicityBefore)) < 1e-9, "归元定息缩时未按服用前丹毒锁定");
        var toxicityAfter = actor.PillToxicity;
        typeof(Character).GetMethod("finish_breakthrough_retreat", Hidden)!.Invoke(actor, null);
        Check(actor.get_inventory_total_count(path) == before - 1 && actor.PillToxicity == toxicityAfter && completions == 1, "重入重复消费或发放药效");
    }
    private async Task Receipt(string id, AlchemyRecipeCatalog.SampleRoute recipe, bool useUi)
    {
        var actor = Actor(); var inputs = Inputs(recipe); Stock(actor, inputs);
        var preview = AlchemySlotSimulation.Simulate(inputs, actor.AlchemyAttainment, 24);
        int tier = preview.Pills.Where(p => p.Quality >= 20).Min(p => AlchemyQualityRules.Tier(p.Quality));
        string path = AlchemyProductCatalog.Path(id, tier);
        var time = Clock.get_precise_game_hours(); var energy = actor.current_world_stamina;
        ItemClaimPanel claim; LocationCraftingPanel? facilities = null;
        if (useUi)
        {
            facilities = new LocationCraftingPanel(); AddChild(facilities); facilities.ShowFor(actor, true);
            var room = facilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
            ((Control)typeof(AlchemySlotBrewPreviewScene).GetField("_tutorialOverlay", Hidden)!.GetValue(room)!).Hide();
            typeof(AlchemySlotBrewPreviewScene).GetMethod("OpenRecipeBook", Hidden)!.Invoke(room, null);
            var choice = room.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.GetMeta("recipe_name", "").AsString() == recipe.Name);
            choice.EmitSignal(BaseButton.SignalName.Pressed);
            var load = (Button)typeof(AlchemySlotBrewPreviewScene).GetField("_recipeLoad", Hidden)!.GetValue(room)!;
            Check(!load.Disabled, "达到实际门槛仍不能载入突破丹样方");
            await Frames(); ((ScrollContainer)choice.GetParent().GetParent()).EnsureControlVisible(choice);
            await Capture(id + "_丹方");
            load.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!((Control)typeof(AlchemySlotBrewPreviewScene).GetField("_recipeOverlay", Hidden)!.GetValue(room)!).Visible, "载入样方按钮未完成选方");
            await Capture(id + "_配伍");
            typeof(AlchemySlotBrewPreviewScene).GetField("_simpleAlchemyPresentation", Hidden)!.SetValue(room, true);
            var task = (Task)typeof(AlchemySlotBrewPreviewScene).GetMethod("BrewWithPresentation", Hidden)!.Invoke(room, null)!;
            for (int frame = 0; frame < 600 && !task.IsCompleted; frame++)
            {
                room.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "确定 · 按丹效装瓶" && b.IsVisibleInTree())?.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(1);
            }
            Check(task.IsCompleted, "突破丹炼制演出未完成"); await task;
            claim = (ItemClaimPanel)typeof(AlchemySlotBrewPreviewScene).GetField("_alchemyClaim", Hidden)!.GetValue(room)!;
            Check(claim is not null, "突破丹未进入正式领取页");
            await Capture(id + "_领取");
        }
        else
        {
            Check(actor.TryBrewAlchemy(inputs, out var actual, out var reason), "真实炼制失败：" + reason);
            Check(actual!.ProductId == id && actual.Pills.Select(p => p.Quality).SequenceEqual(preview.Pills.Select(p => p.Quality)), "实际开炉与预览结果不符");
            claim = new ItemClaimPanel(); GetTree().Root.AddChild(claim);
            var rewards = new Godot.Collections.Array();
            foreach (var group in actual.Pills.Where(p => p.Quality >= 20).GroupBy(p => AlchemyQualityRules.Tier(p.Quality)))
                rewards.Add(new GDictionary { ["item_path"] = AlchemyProductCatalog.Path(id, group.Key), ["count"] = group.Count(), ["source"] = "炼丹" });
            Check(claim.Present(actor, rewards, "突破丹炼制所得"), "正式领取拒绝突破丹");
        }
        Check(inputs.All(i => actor.get_inventory_total_count(i.Path) == 0) && actor.current_world_stamina == energy - preview.EnergyNeed &&
            Math.Abs(Clock.get_precise_game_hours() - time - 2 * AlchemySlotSimulation.DurationHours(inputs)) < 1e-6, "实际开炉材料、精力或时长错误");
        Check(actor.get_inventory_total_count(path) == 0 && actor.get_breakthrough_pill_choices(id == "dingmai" ? "云息四层" : "云息十层").Count == 0, "未领取就能使用炼得的突破丹");
        claim!.FindChild("ClaimAutoPlace", true, false)!.Call("emit_signal", "pressed");
        Check(actor.get_inventory_total_count(path) == 0, "领取预览提前发丹");
        claim.FindChild("ClaimFooterPrimary", true, false)!.Call("emit_signal", "pressed"); await Frames();
        Check(actor.get_inventory_total_count(path) == preview.Pills.Count(p => p.Quality >= 20 && AlchemyQualityRules.Tier(p.Quality) == tier), "领取后物品路径或档位数量错误");
        if (facilities is not null) { facilities.ReturnToLocation(); await Frames(); Check(!facilities.Visible, "领取后不能返回地点"); facilities.QueueFree(); }
        Breakthrough(actor, id, tier, path);
    }
    private AlchemyRecipeCatalog.SampleRoute ThinRoute(string id)
    {
        foreach (var seed in AlchemyRecipeCatalog.Pills.Single(p => p.Id == id).SampleRoutes)
        for (int a = 1; a <= 4; a++) for (int b = 1; b <= 3; b++) for (int c = 1; c <= 4; c++) for (int d = 1; d <= 4; d++)
        {
            int[] doses = [a, b, c, d]; var route = seed with { Name = seed.Name + "·薄效验证", Ingredients = seed.Ingredients.Select((i, n) => i with { Dose = doses[n] }).ToArray() };
            var inputs = Inputs(route); var result = AlchemySlotSimulation.Simulate(inputs, 68, 24);
            if (!result.CanSimulate || result.ProductId != id || !result.Pills.Any(p => p.Quality >= 20 && AlchemyQualityRules.Tier(p.Quality) == 0)) continue;
            GD.Print($"BREAKTHROUGH_THIN id={id} Q={string.Join('/', result.Pills.Select(p => p.Quality))} materials=" + string.Join('/', route.Ingredients.Select(i => i.HerbName + i.Dose)));
            return route;
        }
        throw new InvalidOperationException("未找到薄效旧路径的真实炼制验证配伍：" + id);
    }
    public override async void _Ready()
    {
        try
        {
            foreach (string path in new[] { "ItemData", "HumanData", "PillItemData" }) _scripts.Add(GD.Load<CSharpScript>("res://csharp/resources/" + path + ".cs"));
            DefinitionsAndRoutes(); Rejections(); await NotebookGate();
            foreach (string id in new[] { "dingmai", "guiyuan_dingxi" })
            {
                var routes = AlchemyRecipeCatalog.Pills.Single(p => p.Id == id).SampleRoutes;
                await Receipt(id, routes[0], true); await Receipt(id, routes[1], false);
                await Receipt(id, ThinRoute(id), false);
            }
            GD.Print($"BREAKTHROUGH_ALCHEMY_OK {_checks}");
            foreach (var actor in _actors) actor.QueueFree(); await Frames(); Definitions.Clear(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
