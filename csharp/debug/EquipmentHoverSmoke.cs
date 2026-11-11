using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Real inventory controls and formal equipment definitions, isolated from saved journeys.</summary>
public partial class EquipmentHoverSmoke : Node
{
    private int _checks;
    private Vector2 _pointer;
    private void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private void Move(Vector2 point) { _pointer = point; GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true); }
    private void Click(Control target)
    {
        Move(target.GetGlobalTransformWithCanvas() * (target.Size / 2));
        foreach (var pressed in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton { Position = _pointer, GlobalPosition = _pointer, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    }
    private async Task Settle() { for (int i = 0; i < 18; i++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); Move(_pointer); } }
    private static HoverInfoManager.HoverInfoContent Content(StoragePanel panel, StorageItemSlot slot) =>
        (HoverInfoManager.HoverInfoContent)typeof(StoragePanel).GetMethod("build_hover_content", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, new object[] { slot })!;
    private void Order(HoverInfoManager.HoverInfoContent info)
    {
        int previous = -1;
        foreach (var field in new[] { "品阶：", "器类：", "器蕴：", "锋锐：", "装备效果：", "化法：", "技能：", "简介：" })
        {
            var index = info.Body.IndexOf(previous < 0 ? field : "\n" + field, StringComparison.Ordinal); Check(index > previous, "器物固定字段顺序 " + field); previous = index;
        }
        Check(info.Subtitle == "" && info.ShowRules && info.RulesTitle == "器物说明" && !info.Body.Contains("res://"), "器物通用规则由独立说明入口提供");
    }
    private async Task Capture(Control card, string name, string prefix = "21_器物统一_")
    {
        var rect = card.GetGlobalRect(); var viewport = GetViewport().GetVisibleRect();
        Check(card.Visible && rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.End.X + 1 && rect.End.Y <= viewport.End.Y + 1, "悬停完整位于画布 " + name);
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screen = GetViewport().GetTexture().GetImage();
        using var crop = screen.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
        Check(crop.SavePng("res://docs/美术规范/信息栏整理_20261025/" + prefix + name + ".png") == Error.Ok, "截图保存 " + name);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var originalMode = PlayerInfoText.Mode;
        try
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
            var owner = new Character { entity_data = new HumanData { unique_id = "equipment-hover-isolated" } }; AddChild(owner);
            foreach (var name in new[] { "素行剑", "承露剑" }) Check(owner.add_item_to_inventory_silently($"res://resources/entities/equipment/{name}.tres"), name + "真实物品入隔离行囊");
            var offer = GD.Load<SectArtifactOfferData>("res://resources/entities/equipment/清和剑.tres");
            var artifact = offer.CreateArtifact(); Check(owner.add_artifact_to_inventory(artifact), "正式法器样本入隔离行囊");
            var count = owner.get_inventory_slot_count();
            var storage = GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>();
            AddChild(storage); storage.bind_player(owner); storage.show_panel(); await Settle();
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            foreach (var slot in storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>()
                .Where(s => !s.is_empty && (s.artifact_data is not null || GD.Load<ItemData>(s.item_path) is EquipmentData)).ToArray())
            {
                var info = Content(storage, slot); Order(info);
                if (slot.artifact_data is null) Check(info.Body.Contains("器类：剑\n器蕴：无\n锋锐：无") && info.Body.Replace("\n\n", "\n").Contains("化法：无\n技能：无"), "凡器缺项明确显示无且器蕴在器类下方");
                else
                {
                    Check(info.Body.Contains("\n锋锐：5\n\n装备效果：\n") && !info.Body.Contains("本体锋锐"), "锋锐独立成行且装备效果另起块");
                    Check(info.Body.Contains("\n带“剑”关键词的技能，") && !info.Body.Contains("属性加成：") && !info.Body.Contains("条件效果："), "效果合并且关键词限制单独一行");
                    Check(info.Body.Contains("化法：\n适用范围：能应对并施加化解，且带“气血”关键词的单体物理伤害技能") && info.Body.Split("适用范围：").Length == 2, "多项化法效果属于同一组");
                    Check(!info.Body.Contains("绑定1个") && !info.Body.Contains("本件贡献") && info.Body.Contains("\n\n化法："), "去掉默认说明并分隔信息组");
                }
                Move(slot.GetGlobalTransformWithCanvas() * (slot.Size / 2)); HoverInfoManager.SetInfo(slot, info, true); await Settle();
                await Capture(card, info.Title);
            }
            var artifactSlot = storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().Single(s => s.artifact_data?.instance_id == artifact.instance_id);
            var preview = EquipmentItemPresentation.Hover(offer);
            Check(preview.Body == Content(storage, artifactSlot).Body, "兑换商品与真实入包快照字段一致");
            Click(artifactSlot); await Settle();
            var popup = storage.FindChild("ItemPopup", true, false) as Control ?? throw new Exception("无操作菜单");
            var action = popup.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Visible && b.Text == "装备");
            Click(action); await Settle(); Check(owner.is_artifact_equipped(artifact.instance_id), "真实法器装备成功");
            Check(owner.unequip_artifact(artifact.instance_id), "真实法器卸下成功");
            Check(count == owner.get_inventory_slot_count(), "查看与装备不改变持有数量");
            GetWindow().ContentScaleSize = new Vector2I(1280, 720); GetWindow().Size = new Vector2I(1280, 720); await Settle();
            artifactSlot = storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().Single(s => s.artifact_data?.instance_id == artifact.instance_id);
            Move(artifactSlot.GetGlobalTransformWithCanvas() * (artifactSlot.Size / 2)); HoverInfoManager.SetInfo(artifactSlot, Content(storage, artifactSlot), true); await Settle();
            await Capture(card, "法器小窗口");
            storage.hide_panel();
            var clothingNames = new[] { "男_玄青云竹袍", "女_青瓷云梅袍", "玉虚内门常服" };
            foreach (var name in clothingNames) Check(owner.add_item_to_inventory_silently($"res://resources/entities/items/clothing/{name}.tres"), "衣着入隔离行囊 " + name);
            storage.show_panel(); await Settle();
            foreach (var name in clothingNames)
            {
                var slot = storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().First(s => s.item_path == $"res://resources/entities/items/clothing/{name}.tres");
                var info = Content(storage, slot);
                var audience = name.StartsWith("男_") ? "男修" : name.StartsWith("女_") ? "女修" : "通用";
                Check(info.Body.Contains("适用人物：" + audience) && info.Body.Contains("简介：") && info.Subtitle == "", "衣着真实适用人物与简介 " + name);
                Check(!info.Body.Contains("品阶：") && !info.Body.Contains("装备效果：") && !info.Body.Contains("化法：") && !info.Body.Contains("数量") && !info.Body.Contains("立绘") && !info.ShowRules, "衣着不套器物字段或通用操作说明 " + name);
                Move(slot.GetGlobalTransformWithCanvas() * (slot.Size / 2)); HoverInfoManager.SetInfo(slot, info, true); await Settle();
                await Capture(card, info.Title, "22_衣着统一_");
            }
            var malePath = "res://resources/entities/items/clothing/男_玄青云竹袍.tres";
            var femalePath = "res://resources/entities/items/clothing/女_青瓷云梅袍.tres";
            var health = owner.max_health;
            var clothesCount = owner.get_inventory_slot_count();
            Check(!owner.equip_inventory_item(femalePath), "男修不能穿女修衣着");
            var maleSlot = storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().First(s => s.item_path == malePath);
            Click(maleSlot); await Settle();
            action = popup.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Visible && b.Text == "穿上");
            Click(action); await Settle();
            Check(owner.is_item_equipped(malePath), "真实行囊穿上衣着成功");
            Check(owner.max_health == health && owner.get_inventory_slot_count() == clothesCount, "换装不改战斗属性或持有数量");
            var emptyClothing = new PortraitClothingData { description = "" };
            Check(ClothingItemPresentation.Hover(emptyClothing).Body.TrimEnd().EndsWith("简介："), "衣着空简介保留位置");
            storage.hide_panel();
            GD.Print($"EQUIPMENT_HOVER_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(originalMode, persist: false); }
    }
}
