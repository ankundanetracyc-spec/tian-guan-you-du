using Godot;
using System.Reflection;

/// <summary>隔离角色验收装备页，不读取或写入正式存档。</summary>
public partial class EquipmentPagePreview : Node
{
    private const string Evidence = "res://docs/美术规范/装备页四槽改版_20261023/汇总布局_20261026/";
    private static int _checks;
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; GD.Print("PASS " + message); }
    private static void Click(InfoPanel panel, string text) => panel.FindChildren("*", "Button", true, false).OfType<Button>()
        .First(button => button.IsVisibleInTree() && button.Text.StartsWith(text)).EmitSignal(BaseButton.SignalName.Pressed);
    private static void Call(InfoPanel panel, string name, params object[] args) =>
        typeof(InfoPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, args);
    private async System.Threading.Tasks.Task Settle()
    { for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Settle(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Evidence + name + ".png") == Error.Ok, "截图 " + name);
    }
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var owner = new Character(); AddChild(owner); owner.Hide();
            var data = new HumanData { entity_name = "装备页预览", gender = 0, appearance_seed = 121, cultivation_realm = "云息三层" };
            owner.load_from_data(data);
            data = (HumanData)owner.entity_data!;
            var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel);
            panel.show_entity(owner); Call(panel, "render_new_page", 5); await Settle();
            var pages = (Dictionary<int, Control>)typeof(InfoPanel).GetField("_characterPages", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            var page = pages[5];
            Check(page.FindChildren("EquipmentSlot*", "Button", true, false).Count == 4, "主界面四个装备槽");
            Check(!page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text is "常规穿戴" or "法宝" or "战备"), "撤下旧分栏与战备占位");
            Check(page.FindChildren("EquipmentSupplySlot*", "Button", true, false).Count == 3, "右侧三个真实共用战备格");
            var clothingSlot = (Button)page.FindChild("EquipmentClothingSlot", true, false)!;
            Check(clothingSlot.Size.X == clothingSlot.Size.Y && clothingSlot.FindChildren("*", "TextureRect", true, false).Count == 1, "衣着为正方形格子并读取当前衣服图标");
            var clothingPath = data.portrait_clothing_item_path;
            clothingSlot.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(panel.GetNode<WardrobePicker>("WardrobePicker").Visible, "衣着格打开现有换装界面");
            panel.GetNode<WardrobePicker>("WardrobePicker").FindChild("CancelClothing", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(data.portrait_clothing_item_path == clothingPath, "取消换装保留当前衣着");
            await Capture("01_空槽");
            const string sword = "res://resources/entities/equipment/素行剑.tres";
            Check(owner.add_item_to_inventory_silently(sword) && owner.equip_inventory_item(sword, "weapon"), "测试武器正常入包及装备");
            Call(panel, "build_character_equipment", owner); await Settle();
            var totalPanel = page.FindChild("EquipmentTotalsPanel", true, false)!;
            Check(totalPanel.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "精准") &&
                totalPanel.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "+2"), "汇总读取真实已接入装备加成");
            await Capture("02_武器详情");
            page.FindChild("EquipmentSlot2", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(!page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text is "器物一槽" or "装备详情"), "不再展示单器详情");
            Check(data.weapon_path == sword, "空候选操作不修改已装备武器");
            Call(panel, "hide_tooltip");
            GetWindow().Size = new Vector2I(1280, 720); await Settle();
            await Capture("03_小窗口");
            data.headwear_path = sword;
            Call(panel, "build_character_equipment", owner); await Settle();
            Check(!page.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text.Contains("旧版装备")), "遗留字段也不再生成旧版装备入口");
            data.headwear_path = "";
            Check(data.weapon_path == sword, "移除兼容入口不影响当前武器装备");
            const string heal = "res://resources/entities/items/alchemy/huichun_1.tres";
            const string mana = "res://resources/entities/items/alchemy/tongmai_0.tres";
            owner.add_item_to_inventory_silently(heal, 7); owner.add_item_to_inventory_silently(mana, 2);
            Call(panel, "build_character_equipment", owner); await Settle();
            page.FindChild("EquipmentSupplySlot0", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "回春丹 · 良效"); await Settle();
            Check(owner.battle_supplies[0].item_path == heal && owner.battle_supplies[0].count == 5, "点击真实格子从本人行囊编入最多5颗");
            page.FindChild("EquipmentSupplySlot1", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "回春丹 · 良效"); await Settle();
            Check(owner.battle_supplies[1].count == 2, "重复丹药位只使用剩余可预留数量");
            page.FindChild("EquipmentSupplySlot0", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "减少1颗"); await Settle();
            Check(owner.battle_supplies[0].count == 4, "已配置战备可以减量");
            page.FindChild("EquipmentSupplySlot0", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "增加1颗"); await Settle();
            Check(owner.battle_supplies[0].count == 5, "余量允许时可以增加1颗");
            page.FindChild("EquipmentSupplySlot1", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "移除此位物品"); await Settle();
            Check(owner.battle_supplies[1].count == 0 && owner.get_inventory_total_count(heal) == 7, "移除预留不会消费丹药");
            page.FindChild("EquipmentSupplySlot1", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "通脉丹 · 薄效"); await Settle();
            Check(owner.battle_supplies[1].item_path == mana && owner.battle_supplies[1].count == 2, "战备选择区分真实物品及档位");
            var suppliesPanel = (Control)page.FindChild("EquipmentSuppliesPanel", true, false)!;
            Check(suppliesPanel.Position.X == 788 && suppliesPanel.Position.Y == 600 && suppliesPanel.GetRect().End.Y <= 795, "右侧战备与汇总区域互不重叠");
            Check(page.FindChildren("*", "ScrollContainer", true, false).Count == 0, "装备主页面无内部滚动容器");
            Check(!page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("点击配置") || label.Text.Contains("武器一件") || label.Text.Contains("仅调整外观")), "移除多余解释说明");
            var attireSlot = (Control)page.FindChild("EquipmentClothingSlot", true, false)!;
            Check(attireSlot.Position.X == 8 && attireSlot.Position.Y == 530, "衣着格并入左侧装备列，不再独占下方整行");
            page.FindChild("EquipmentSlot0", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Click(panel, "素行剑"); await Settle();
            Check(data.weapon_path == sword, "点击装备格直接选择更换并保留有效装备");
            var snapshot = owner.get_snapshot_data();
            Check(snapshot.battle_supplies[0].count == 5 && snapshot.battle_supplies[1].count == 2 && snapshot.pack_item_counts[snapshot.pack_item_paths.IndexOf(heal)] == 7, "装备页配置直接进入战前快照且不扣库存");
            var savePath = "user://equipment_supply_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save(data, savePath) == Error.Ok && GD.Load<HumanData>(savePath)!.battle_supplies[0].count == 5, "装备页战备可持久保存");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            GetWindow().Size = new Vector2I(1920, 1080); Call(panel, "render_new_page", 5); await Settle();
            await Capture("04_右侧战备与衣着格");
            GetWindow().Size = new Vector2I(1280, 720); await Settle();
            await Capture("05_右侧战备小窗口");
            var item = ForgedArtifactInventory.Create(new ProcessForgingTrial.Artifact {
                InstanceId = "equipment-summary-fixture", Name = "汇总验证剑", Category = "剑", Tier = "法器",
                BondContributions = ["惊鸿 +2", "抱朴 +2"], Effects = [new() { CarrierId = "base", AtomId = "physical_defense", Value = 10 }] });
            owner.add_artifact_to_inventory(item); owner.equip_artifact(item.instance_id);
            Call(panel, "build_character_equipment", owner); await Settle();
            var actualTotals = (List<(string Name, string Value)>)typeof(InfoPanel).GetMethod("EquipmentTotals", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [owner])!;
            Check(actualTotals.Any(row => row.Name == "物理防御" && row.Value == "+10 / +10%") && actualTotals.Any(row => row.Name == "剑技物伤" && row.Value == "+10%"), "总加成汇合真实器物基础与激活器蕴，区分固定和百分比");
            Check(owner.physical_defense == 11 && page.FindChild("EquipmentBondsPanel", true, false)!.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "惊鸿  2/4"), "器蕴累计与角色实际收益一致");
            var defense = owner.physical_defense; Call(panel, "build_character_equipment", owner); await Settle();
            Check(owner.physical_defense == defense, "反复查看汇总不改写或重复叠加属性");
            await Capture("06_器蕴激活汇总");
            owner.SetMeta("artifact_battle_clone", true); Call(panel, "build_character_equipment", owner); await Settle();
            page.FindChild("EquipmentSupplySlot0", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(!owner.set_battle_supply(0, heal, 1) && owner.battle_supplies[0].count == 5, "战斗替身的装备战备只查看不编辑");
            GD.Print($"EQUIPMENT_PAGE_PREVIEW_OK: {_checks} checks");
            Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
