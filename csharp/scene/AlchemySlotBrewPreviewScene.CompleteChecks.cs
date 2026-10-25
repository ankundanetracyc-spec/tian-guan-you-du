using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class AlchemySlotBrewPreviewScene
{
    private void RunCompleteAlchemyChecks(Action<bool, string> verify)
    {
        var route = AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last();
        var inputs = route.Ingredients.Select(i =>
        {
            var material = _materials.Single(m => m.Name == i.HerbName);
            return new AlchemySlotSimulation.Input(material.Name, material.Path, material.Craft, i.Dose, RoleFor(i.AssistantSlot));
        }).ToArray();
        var many = AlchemySlotSimulation.Simulate(inputs, 36, requestedCount: 4);
        var fewer = AlchemySlotSimulation.Simulate(inputs, 36, requestedCount: 1);
        verify(many.CanSimulate && fewer.CanSimulate && fewer.CandidateCount == 1 && many.CandidateCount == 4,
            "可选择一至四颗并按所选颗数分配");
        verify(fewer.Pills[0].MainMass > many.Pills.Max(p => p.MainMass) && fewer.EnergyNeed < many.EnergyNeed,
            "减产集中真实药性并降低分丹精力");
        foreach (var result in new[] { many, fewer })
        {
            verify(Math.Abs(result.Pills.Sum(p => p.SourceMasses.Values.Sum()) - result.DeliveredMass) < 1e-7,
                "逐颗有效质量合计等于整炉送达质量");
            verify(Math.Abs(result.Pills.Sum(p => p.CarryMass) - result.CarryMass) < 1e-7,
                "逐颗残余夹带质量守恒");
            verify(result.Pills.All(p => p.Quality is >= 0 and <= 100), "每颗丹效均在零至一百内");
        }
        var repeated = AlchemySlotSimulation.Simulate(inputs.AsEnumerable().Reverse().ToArray(), 36, requestedCount: 4);
        verify(repeated.Pills.Select(p => p.Quality).SequenceEqual(many.Pills.Select(p => p.Quality)) &&
            repeated.Pills.Zip(many.Pills).All(pair => Math.Abs(pair.First.MainMass - pair.Second.MainMass) < 1e-8),
            "相同配伍不因输入枚举顺序改变分配或结果");
        verify(many.Pills.Select(p => p.MainMass).Distinct().Count() > 1, "同炉药性分配有可复现的小幅差异");
        bool adjacentExample = false;
        foreach (var candidatePill in AlchemyRecipeCatalog.Pills)
        foreach (var sample in candidatePill.SampleRoutes.TakeLast(2))
        {
            if (adjacentExample) break;
            var sampleInputs = sample.Ingredients.Select(i =>
            {
                var material = _materials.Single(m => m.Name == i.HerbName);
                return new AlchemySlotSimulation.Input(material.Name, material.Path, material.Craft, i.Dose, RoleFor(i.AssistantSlot));
            }).ToArray();
            for (int a = 8; a <= 68 && !adjacentExample; a++)
            for (int k = 2; k <= 4 && !adjacentExample; k++)
            {
                var example = AlchemySlotSimulation.Simulate(sampleInputs, a, requestedCount: k);
                var tiers = example.Pills.Where(p => p.Quality >= 20).Select(p => AlchemyQualityRules.Tier(p.Quality)).Distinct().ToArray();
                if (!example.CanSimulate || tiers.Length != 2 || tiers.Max() - tiers.Min() != 1) continue;
                adjacentExample = true;
                GD.Print($"ADJACENT_RECIPE {sample.Name} A={a} K={k} {example.Outcome} scores={string.Join(",", example.Pills.Select(p => p.Quality))}");
            }
        }
        verify(adjacentExample, "真实样方存在同炉相邻档成品而非整炉同分");
        var fixedCoreSample = AlchemyRecipeCatalog.Pills[0].SampleRoutes.Single(r => r.Name == "青木清露方");
        var fixedCoreInputs = fixedCoreSample.Ingredients.Select(i =>
        {
            var material = _materials.Single(m => m.Name == i.HerbName);
            return new AlchemySlotSimulation.Input(material.Name, material.Path, material.Craft, i.Dose, RoleFor(i.AssistantSlot));
        }).ToArray();
        var overCount = AlchemySlotSimulation.Simulate(fixedCoreInputs, 8, requestedCount: 2);
        verify(!overCount.CanSimulate && overCount.ProductName == "九品回春丹", "改颗数不能借另一弱核心悄悄换丹种绕过上限");
        MaterialCraftData HighCraft(params string[] functions) => new()
        {
            texture = "液体", element = "木", utilities = new Godot.Collections.Array<MaterialUtilityGrade>(functions.Select(f =>
                new MaterialUtilityGrade { utility_id = f, grade = 8 }))
        };
        var threeMaterials = new[]
        {
            new AlchemySlotSimulation.Input("高阶君", "high-main", HighCraft("生"), 3, AlchemySlotSimulation.SlotRole.Jun),
            new AlchemySlotSimulation.Input("高阶佐", "high-support", HighCraft("固", "明", "藏"), 1, AlchemySlotSimulation.SlotRole.Zuo),
            new AlchemySlotSimulation.Input("高阶使", "high-delivery", HighCraft("通", "行"), 1, AlchemySlotSimulation.SlotRole.Shi)
        };
        var threeResult = AlchemySlotSimulation.Simulate(threeMaterials, 1196, 1000, requestedCount: 4);
        verify(threeResult.OutputCount == 4 && threeResult.Pills.All(p => p.Quality == 100),
            "三味高阶真实满足全部轴时不再按味数锁在优效");
        verify(AlchemyQualityRules.FromRaw(44) == 50 && AlchemyQualityRules.FromRaw(51) == 51 &&
            AlchemyQualityRules.FromRaw(65) == 75 && AlchemyQualityRules.FromRaw(76) == 76 &&
            AlchemyQualityRules.FromRaw(84) == 90 && AlchemyQualityRules.FromRaw(91) == 91 &&
            AlchemyQualityRules.FromRaw(97) == 99 && AlchemyQualityRules.FromRaw(100) == 100,
            "跨档锚点与五档整数边界一致");
        verify(AlchemyQualityRules.FromRaw(50.999) < 51 && AlchemyQualityRules.Purity(.061) is > 0 and < 20 &&
            AlchemyQualityRules.Polarity(.73, .72, .20) is > 0 and < 20, "跨档连续且临界未成丹不直接归零");
        var leadBase = inputs.Select(i => i.Role == AlchemySlotSimulation.SlotRole.Shi ? i with
        {
            Craft = new MaterialCraftData { texture = i.Craft.texture, element = i.Craft.element, polarity = i.Craft.polarity,
                utilities = new Godot.Collections.Array<MaterialUtilityGrade>(i.Craft.utilities.Where(u => u.utility_id != "引")) }
        } : i).ToArray();
        var leadAdded = leadBase.Select(i => i.Role == AlchemySlotSimulation.SlotRole.Shi ? i with
        {
            Craft = new MaterialCraftData { texture = i.Craft.texture, element = i.Craft.element, polarity = i.Craft.polarity,
                utilities = new Godot.Collections.Array<MaterialUtilityGrade>(i.Craft.utilities.Concat(new[] { new MaterialUtilityGrade { utility_id = "引", grade = 4 } })) }
        } : i).ToArray();
        var noLead = AlchemySlotSimulation.Simulate(leadBase, 203, 1000, requestedCount: 4);
        var withLead = AlchemySlotSimulation.Simulate(leadAdded, 203, 1000, requestedCount: 4);
        verify(withLead.Pills[0].MainMass > noLead.Pills[0].MainMass &&
            withLead.Pills.Skip(1).Sum(p => p.MainMass) < noLead.Pills.Skip(1).Sum(p => p.MainMass) &&
            Math.Abs(withLead.Pills.Sum(p => p.MainMass) - noLead.Pills.Sum(p => p.MainMass)) < 1e-7,
            "引提高首颗主药份额并真实扣减其余颗且总量守恒");
        verify(withLead.StorageMultiplier == 1 && noLead.StorageMultiplier == 1, "成品没有额外藏性时长分档");
        foreach (var id in new[] { "huichun", "tongmai", "jingxin" })
        foreach (int tier in Enumerable.Range(0, 5))
        {
            var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(id, tier));
            verify(item is not null && item.validate_definition().Count == 0 && item.can_stack && item.max_stack == 999 &&
                item.pill_id == id && item.efficacy_tier == tier && StoragePanel.build_item_usage_effect(item).Contains(item.EffectText),
                $"{id}档位{tier}正式定义堆叠与行囊效果说明一致");
        }
        var actors = new List<Character>();
        Character Actor(IReadOnlyList<AlchemySlotSimulation.Input> materials)
        {
            var data = new HumanData { alchemy_proficiency = 36, base_alchemy_proficiency = 36,
                max_spirit_perception = 100, current_world_stamina = 100,
                inventory_item_paths = new(materials.Select(i => i.Path)),
                inventory_item_counts = new(materials.Select(i => i.Dose * 3)),
                inventory_item_positions = new(Enumerable.Range(0, materials.Count)) };
            var actor = new Character { entity_data = data }; actors.Add(actor); return actor;
        }
        try
        {
            var actor = Actor(inputs);
            var state = (HumanData)actor.entity_data!;
            state.used_spirit_perception = 70;
            verify(actor.TryBrewAlchemy(inputs, 1, out var result, out _) && result is not null && result.OutputCount == 1,
                "正式炼丹用真实库存与人物灵识结算");
            verify(inputs.All(i => actor.get_inventory_total_count(i.Path) == i.Dose * 2) &&
                Math.Abs(actor.current_world_stamina - (100 - result!.EnergyNeed)) < .001 && actor.used_spirit_perception == 70,
                "整炉一次扣料扣精力且保留其他事务灵识占用");
            string product = AlchemyProductCatalog.Path(result!.ProductId, AlchemyQualityRules.Tier(result.Pills.Single().Quality));
            verify(actor.get_inventory_total_count(product) == 1, "成品按丹种丹阶丹效档进入唯一行囊");
            var pill = GD.Load<PillItemData>(product);
            actor.current_health = 1;
            verify(actor.TryUseAlchemyPill(pill) && actor.current_health == 1 + AlchemyProductCatalog.Healing[pill.efficacy_tier] &&
                actor.get_inventory_total_count(product) == 0 && actor.used_spirit_perception == 70, "使用丹药效果与消耗同时生效且不清空其他占用");
            var stale = Actor(inputs);
            ((HumanData)stale.entity_data!).used_spirit_perception = 99;
            verify(!stale.TryBrewAlchemy(inputs, 1, out _, out _) && stale.current_world_stamina == 100 &&
                inputs.All(i => stale.get_inventory_total_count(i.Path) == i.Dose * 3), "开炼重检已变化的灵识不足且零扣除");
            var tired = Actor(inputs); ((HumanData)tired.entity_data!).current_world_stamina = 0;
            verify(!tired.TryBrewAlchemy(inputs, 1, out _, out _) &&
                inputs.All(i => tired.get_inventory_total_count(i.Path) == i.Dose * 3), "精力不足不扣料");
            var missing = Actor(inputs); ((HumanData)missing.entity_data!).inventory_item_counts[0] = 0;
            verify(!missing.TryBrewAlchemy(inputs, 1, out _, out _) && missing.current_world_stamina == 100, "材料余额变化后开炼失败不扣精力");
            var full = Actor(inputs); var fullData = (HumanData)full.entity_data!;
            while (fullData.inventory_item_paths.Count < Character.InventorySlotCapacity)
            {
                fullData.inventory_item_positions.Add(fullData.inventory_item_paths.Count);
                fullData.inventory_item_paths.Add(inputs[0].Path); fullData.inventory_item_counts.Add(99);
            }
            verify(!full.TryBrewAlchemy(inputs, 1, out _, out var reason) && reason.Contains("行囊空间") &&
                full.current_world_stamina == 100 && fullData.inventory_item_paths.Count == Character.InventorySlotCapacity,
                "满包预先检查并完整保留资源");
            var samePathItem = _materials.Single(m => m.Name == "山蚕长丝");
            var unknown = new[] { new AlchemySlotSimulation.Input(samePathItem.Name, samePathItem.Path,
                samePathItem.Craft, 1, AlchemySlotSimulation.SlotRole.Jun) };
            var failed = Actor(unknown);
            verify(failed.TryBrewAlchemy(unknown, 1, out var failure, out _) && failure!.OrdinaryFailure &&
                failed.get_inventory_total_count(unknown[0].Path) == 2 && failed.current_world_stamina < 100,
                "普通失败真实消耗且不生成丹药");
            // 同档物品跨炉合并；无收益不允许消耗。
            var medicine = GD.Load<PillItemData>(AlchemyProductCatalog.Path("huichun", 0));
            verify(actor.add_item_to_inventory(medicine.ResourcePath, 2) && actor.add_item_to_inventory(medicine.ResourcePath, 1) &&
                state.inventory_item_paths.Count(path => path == medicine.ResourcePath) == 1, "同档跨炉产物堆叠");
            actor.get_inventory_snapshot();
            verify(actor.used_spirit_perception == 70, "查看行囊及普通入包不清零其他事务灵识占用");
            actor.current_health = actor.max_health;
            verify(!actor.TryUseAlchemyPill(medicine) && actor.get_inventory_total_count(medicine.ResourcePath) == 3, "生命已满不浪费回春丹");
            var mana = GD.Load<PillItemData>(AlchemyProductCatalog.Path("tongmai", 4));
            actor.add_item_to_inventory(mana.ResourcePath); actor.max_mana = 100; actor.current_mana = 10;
            verify(actor.TryUseAlchemyPill(mana) && actor.current_mana == 38, "通脉丹按本档固定百分比恢复法力");
            var calm = GD.Load<PillItemData>(AlchemyProductCatalog.Path("jingxin", 4));
            actor.add_item_to_inventory(calm.ResourcePath, 2); state.mood_value = -3;
            verify(actor.TryUseAlchemyPill(calm) && state.mood_value == 0 && !actor.TryUseAlchemyPill(calm), "静心丹最多安定至零且不可刷正心境");
            string save = $"user://alchemy/alchemy_check_{Guid.NewGuid():N}.tres";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://alchemy"));
            try
            {
                verify(ResourceSaver.Save(state, save) == Error.Ok, "丹药与材料通过既有人物资源保存");
                var loaded = ResourceLoader.Load<HumanData>(save, cacheMode: ResourceLoader.CacheMode.Ignore);
                verify(loaded.inventory_item_paths.SequenceEqual(state.inventory_item_paths) &&
                    loaded.inventory_item_counts.SequenceEqual(state.inventory_item_counts) &&
                    loaded.current_world_stamina == state.current_world_stamina, "人物读回保留档位物品数量与精力");
            }
            finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save)); }
            verify(!actor.TryBrewAlchemy(inputs, 5, out _, out _) && !actor.TryBrewAlchemy(inputs, -1, out _, out _),
                "正式结算拒绝不合法颗数");
            var live = Actor(inputs);
            AddChild(live);
            live.max_spirit_perception = 100;
            live.used_spirit_perception = 70;
            var liveData = (HumanData)live.entity_data!;
            liveData.alchemy_proficiency = 36; liveData.current_world_stamina = 100;
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            int beforeHours = time.get_total_game_hours();
            verify(live.TryBrewAlchemy(inputs, 1, out _, out _) &&
                time.get_total_game_hours() - beforeHours == (int)(2 * AlchemySlotSimulation.DurationHours(inputs)),
                "正式人物炼制按时辰推进唯一历法");
            verify(live.used_spirit_perception == 70, "正式炉次结束不遗留本炉灵识占用");
            OwnerCharacter = live;
            ApplySampleRoute(route);
            verify(_spiritHeaderValue.Text == "30/100" && _energyHeaderValue.Text == $"{live.current_world_stamina:0.#}" &&
                Attainment == 36, "正式页面读取人物真实灵识精力造诣");
            _attainmentInput.Value = 1196;
            verify(Attainment == 36 && _attainmentInput.Value == 36, "正式页面不能通过试演控件改造诣");
            OwnerCharacter = null;
            TotalSpirit = 24; OtherSpiritOccupation = 0; Attainment = 36;
            ClearSelections();
            var workshop = new BaigongPanel();
            AddChild(workshop);
            try
            {
                workshop.show_for(live);
                workshop.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "丹药").EmitSignal(Button.SignalName.Pressed);
                workshop.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "进入炼丹房").EmitSignal(Button.SignalName.Pressed);
                var formalScene = workshop.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
                verify(formalScene.OwnerCharacter == live && formalScene.Attainment == live.AlchemyAttainment,
                    "百工丹药真实按钮打开绑定人物的正式炼丹房");
                var currentScene = GetTree().CurrentScene;
                formalScene.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "〈 返回").EmitSignal(Button.SignalName.Pressed);
                verify(formalScene.IsQueuedForDeletion() && GetTree().CurrentScene == currentScene,
                    "正式炼丹返回只关闭页面不重置旅程");
                formalScene.Free();
            }
            finally { workshop.Free(); }
            var positive = _materials.First(m => m.Craft.polarity > 0 &&
                AlchemyRecipeCatalog.Pills.Any(p => m.Grade(p.CoreUtility) > 0) &&
                _materials.Any(n => n.Craft.polarity < 0 && n.Craft.texture == m.Craft.texture));
            var negative = _materials.First(m => m.Craft.polarity < 0 && m.Craft.texture == positive.Craft.texture);
            var explosiveInputs = new[]
            {
                new AlchemySlotSimulation.Input(positive.Name, positive.Path, positive.Craft, 1, AlchemySlotSimulation.SlotRole.Jun),
                new AlchemySlotSimulation.Input(negative.Name, negative.Path, negative.Craft, 1, AlchemySlotSimulation.SlotRole.Chen)
            };
            var explosive = Actor(explosiveInputs);
            var explosiveData = (HumanData)explosive.entity_data!;
            explosiveData.alchemy_proficiency = 1196; explosive.max_spirit_perception = 1000;
            verify(explosive.TryBrewAlchemy(explosiveInputs, 1, out var blast, out _) && blast!.Exploded &&
                blast.OutputCount == 0 && blast.Pills.Count == 0 && blast.Quality == 0 && blast.QualityName == "未成丹" && explosive.current_world_stamina < 100 &&
                explosive.get_inventory_total_count(positive.Path) == 2,
                "炸炉消耗投料精力且不生成丹药或残渣");
            string notebook = ProjectSettings.GlobalizePath($"user://alchemy/quantity_{Guid.NewGuid():N}.json");
            try
            {
                AlchemyRecipeStore.Save(notebook, [new("quantity", "颗数保存", [new("material", 1, -1)], 3)]);
                verify(AlchemyRecipeStore.Load(notebook).Single().PillCount == 0, "自拟丹方不保存指定产量");
                System.IO.File.WriteAllText(notebook, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Version = 1, Recipes = new[] { new { Id = "old", Name = "旧方", Ingredients = new[] { new AlchemyRecipeStore.Entry("material", 1, -1) } } }
                }));
                verify(AlchemyRecipeStore.Load(notebook).Single().PillCount == 0, "旧丹方缺颗数字段按自动产量兼容");
            }
            finally { System.IO.File.Delete(notebook); }
        }
        finally { foreach (var actor in actors) actor.Free(); }
    }
}
