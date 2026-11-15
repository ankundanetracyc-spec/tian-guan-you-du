using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Service = TalismanCraftingService;

public partial class HoverCategoryNativeSmoke
{
    private async Task ItemSamples()
    {
        foreach (var (id, category, path) in new[]
        {
            ("材料", "材料", "res://resources/entities/items/玄铁.tres"),
            ("衣着", "衣着", "res://resources/entities/items/clothing/玉虚内门常服.tres"),
            ("储物袋", "储物袋（既有隔离定义）", "res://tools/fixtures/storage_bag_narrow.tres"),
            ("秘籍", "秘籍", "res://resources/entities/items/玄枢含章经秘籍.tres"),
            ("凡器", "普通剑器", "res://resources/entities/equipment/素行剑.tres"),
            ("清和", "清和正式商品", "res://resources/entities/equipment/清和剑.tres"),
            ("突破丹", "突破丹", Character.YunxiThreeToFourPillPath),
        })
        {
            var item = GD.Load<ItemData>(path);
            await ContentSample(id, category, "StoragePanel.ItemHover", path, () => StoragePanel.ItemHover(item, user: _owner));
        }
        var hide = GD.Load<ItemData>("res://resources/entities/items/兽皮.tres");
        var hideCraft = (MaterialCraftData)hide.craft_data!;
        var hideDetail = PlayerInfoText.Detailed(StoragePanel.ItemHover(hide).Body);
        Check(Math.Abs(hideCraft.material_score() - 2.5) < .0001 && hideCraft.material_grade_name() == "9品上",
            "real fractional material assessment and actual grade remain unchanged");
        Check(hideDetail.Contains("材评：2.5 = 1×2＋1×0.5") && !hideDetail.Contains("取整") && !hideDetail.Contains("小阶"),
            "fractional material source preserves weighted inputs without internal mapping notes", hideDetail);
        foreach (var kind in new[] { "paper", "ink" })
        {
            await Attempt("符工" + kind, async () =>
            {
                var liquid = TalismanCraftingCatalog.DiscoverMaterials().First(entry => entry.Stock(_owner) > 0 && entry.Texture == "液体" && entry.UtilityLayers.Count > 0);
                var material = kind == "paper" ? TalismanCraftingCatalog.DiscoverMaterials().First(entry => entry.Stock(_owner) > 0 && entry.Texture == "纤维") : liquid;
                var request = new Service.Request { Mode = kind, TechniqueId = kind == "paper" ? "yangmai" : "junyan",
                    Materials = kind == "paper" ? [new(material.Path, 1), new(liquid.Path, 1)] : [new(liquid.Path, 1)],
                    RetainedUtilities = material.UtilityLayers.Select(layer => layer.Name).Distinct().Take(4).ToList() };
                var quote = Service.Evaluate(_owner, request);
                if (quote.PlannedProducts.Count == 0) throw new Exception(string.Join("；", quote.Blockers));
                var snapshot = quote.PlannedProducts[0].Snapshot;
                var item = FrozenItem(snapshot, kind == "paper" ? "符纸" : "符墨");
                await ContentSample("符工_" + kind, kind == "paper" ? "符纸" : "符墨", "TalismanCraftingService.Evaluate → StoragePanel.ItemHover",
                    string.Join("；", request.Materials.Select(selection => selection.Path)) + "；只报价未执行制作", () => StoragePanel.ItemHover(item, user: _owner));
            });
        }
        var brush = FrozenItem(Service.BrushSnapshot("qingzhu"), "青竹符笔");
        await ContentSample("符笔", "符笔", "TalismanCraftingService.BrushSnapshot → StoragePanel.ItemHover", "正式青竹符笔冻结参数", () => StoragePanel.ItemHover(brush));
        foreach (var recipe in new[] { "hushen", "yanshi" })
        {
            var product = FrozenItem(new Service.ProductSnapshot { Kind = "talisman", RecipeId = recipe, Tier = 2 }, TalismanCombatCatalog.Name(recipe));
            await ContentSample("成符_" + recipe, "成符", "StoragePanel.ItemHover → TalismanCombatCatalog.Hover",
                TalismanCombatCatalog.SkillPath(recipe, 2) + "；隔离冻结档位定义", () => StoragePanel.ItemHover(product));
        }
        var travel = GD.Load<ItemTravelData>("res://resources/world/bai_chuan/yuxu_escort_boat_travel_20261106.tres");
        var travelItem = new ItemData { entity_name = "行旅行具接口样本", item_category = "器具", travel_data = travel };
        await ContentSample("行旅", "行旅字段（真实接引舟参数组合）", "StoragePanel.ItemHover → ItemTravelPresentation.Append",
            travel.ResourcePath + "；隔离载体，非玩家已有成器", () => StoragePanel.ItemHover(travelItem));
        var root = ProjectSettings.GlobalizePath("res://");
        var fallback = System.IO.Directory.EnumerateFiles(root + "resources/entities/items", "*.tres")
            .Select(path => GD.Load<ItemData>("res://" + System.IO.Path.GetRelativePath(root, path).Replace('\\', '/')))
            .FirstOrDefault(item => item is not null && item.GetType() == typeof(ItemData)
                && !MaterialItemPresentation.IsMaterial(item) && !ManualItemPresentation.IsManual(item) && !AlchemyPillVisual.IsPill(item));
        if (fallback is not null)
            await ContentSample("普通物品兜底", "普通物品兜底", "StoragePanel.ItemHoverWithoutTravel fallback", fallback.ResourcePath, () => StoragePanel.ItemHover(fallback));
        else _limitations.Add("普通物品兜底：当前根物品目录没有合格的真实定义，不用临时手写正文补齐类别。");
    }
    private static TalismanProductItemData FrozenItem(Service.ProductSnapshot snapshot, string name) => new()
    {
        entity_name = name, definition_id = "hover-category-" + snapshot.Kind + snapshot.RecipeId,
        snapshot_json = JsonSerializer.Serialize(snapshot), icon_path = TalismanArtCatalog.IconPath(snapshot),
        item_category = snapshot.Kind == "talisman" ? "符箓" : "器具", item_subcategory = snapshot.Kind,
        inventory_width = 1, inventory_height = snapshot.Kind == "ink" ? 1 : 2,
        battle_skill_path = snapshot.Kind == "talisman" ? TalismanCombatCatalog.SkillPath(snapshot.RecipeId, snapshot.Tier) : "",
    };
    private async Task StatusEntries()
    {
        _anchor.Hide();
        var unit = new Creature { entity_data = new HumanData { entity_name = "隔离状态实例", max_health = 100, current_health = 80 } };
        AddChild(unit); unit.Hide(); unit.shield = 24;
        unit.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/外伤.tres"), stacks: 2);
        unit.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/僵直.tres"), stacks: 30);
        var manager = new BattleManager(); var overlay = new BattleUnitOverlay(); overlay.Setup(manager);
        var row = new HBoxContainer { Position = new(100, GetViewport().GetVisibleRect().Size.Y - 100), Size = new(240, 48) }; AddChild(row);
        Field<Dictionary<Creature, Control>>(overlay, "views")[unit] = row;
        try
        {
            foreach (var id in new[] { "外伤", "僵直", "护盾" })
            {
                var status = unit.status_snapshot().FirstOrDefault(value => value.effect?.effect_id == id);
                var count = status?.stacks ?? unit.shield;
                var badge = (Control)Invoke(overlay, "CreateStatusBadge", id, id, count, "", id != "护盾", status)!;
                row.AddChild(badge); await Frames(2);
                foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
                {
                    PlayerInfoText.SetMode(mode, persist: false); HoverInfoManager.HideAll(); await NativeSource(badge, "status " + id);
                    badge.EmitSignal(Control.SignalName.MouseEntered); await Frames(20);
                    var info = status is not null ? BattleStatusPresentation.Instance(status, id) : BattleStatusPresentation.Resource(unit, id, count, unit.max_mana, "");
                    var key = "状态_" + id + (mode == PlayerInfoText.InformationMode.Simple ? "_简略" : "_详细");
                    VerifyShared(key, info);
                    await Capture(_card, key, "状态", "正式状态徽标控件（隔离容器）", "BattleUnitOverlay.CreateStatusBadge → BuildStatusHoverInfo", badge.GetPath(), SharedText());
                }
                badge.EmitSignal(Control.SignalName.MouseExited);
            }
        }
        finally { row.Free(); overlay.Free(); manager.Free(); unit.Free(); _anchor.Show(); }
        await ContentSample("炁", "公共资源", "BattleStatusPresentation.Resource", "隔离人物实际法力上限" + _owner.max_mana + "；零炁计数样本",
            () => BattleStatusPresentation.Resource(_owner, "炁", 0, _owner.max_mana, ""));
    }
    private async Task CharacterEntries()
    {
        _anchor.Hide();
        var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel);
        try
        {
            panel.show_entity(_owner); Invoke(panel, "render_new_page", 1); await Frames();
            var attribute = Nodes<Control>(panel).First(control => control.IsVisibleInTree() && control.HasMeta("attribute_hover_row")
                && control.GetMeta("attribute_hover_row").AsString() == "PhysiqueRow");
            foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
            {
                PlayerInfoText.SetMode(mode, persist: false); await NativeSource(attribute, "actual attribute"); attribute.EmitSignal(Control.SignalName.MouseEntered); await Frames(20);
                var info = CharacterAttributePresentation.Build(_owner, "PhysiqueRow", "体魄");
                var key = "人物实际属性" + (mode == PlayerInfoText.InformationMode.Simple ? "_简略" : "_详细");
                VerifyShared(key, info); await Capture(_card, key, "人物资料", "正式人物属性行", "CharacterAttributePresentation.Bind", attribute.GetPath(), SharedText());
            }
            HoverInfoManager.HideAll(); Invoke(panel, "render_new_page", 5); await Frames();
            var pageText = string.Join("\n", Nodes<Label>(panel).Where(node => node.IsVisibleInTree()).Select(node => node.Text));
            Check(!pageText.Contains("临时灵识") && !pageText.Contains("临时占用"), "actual equipment page contains no temporary spirit occupation wording", pageText);
            await CapturePage("正式穿戴说明", "InfoPanel.render_new_page(5)", "正式穿戴页文本核对，不代表执行过装备或交易流程");
            var spirit = Nodes<Label>(panel).First(node => node.IsVisibleInTree() && node.Text.StartsWith("灵识 ", StringComparison.Ordinal));
            foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
            {
                PlayerInfoText.SetMode(mode, persist: false); await NativeSource(spirit, "actual equipment spirit"); spirit.EmitSignal(Control.SignalName.MouseEntered); await Frames(20);
                Check(SharedText().Contains("持续占用灵识") && !SharedText().Contains("临时"), "actual equipment spirit hover describes persistent occupation");
                await Capture(_card, "穿戴灵识_" + mode, "穿戴说明", "正式穿戴页说明控件", "InfoPanel.build_equipment_bonds → CharacterHover", spirit.GetPath(), SharedText());
            }
            var npc = new WorldNpcData { npc_id = "hover-category-npc", npc_name = "资料复核同修", cultivation_realm = "云息二层", current_age = 24, is_alive = true };
            panel.show_npc(npc); await Frames();
            var npcText = string.Join("\n", Nodes<Label>(panel).Where(node => node.IsVisibleInTree()).Select(node => node.Text));
            Check(npcText.Contains("未探明"), "actual unobserved NPC page preserves unknown private attributes");
            await CapturePage("NPC未知资料", "InfoPanel.show_npc", "普通资料页展示；不计作NPC属性悬停浮层验收");
            _limitations.Add("NPC调用了正式InfoPanel.show_npc并核对未知资料；该资料页当前为普通文字、无可独立调用的属性悬停，本轮不计作NPC浮层入口通过。");
        }
        finally { HoverInfoManager.HideAll(); panel.Free(); _anchor.Show(); }
    }
}
