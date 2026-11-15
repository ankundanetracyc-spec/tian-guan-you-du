using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class ForgedArtifactInventorySmoke : Node
{
    private const string Save = "user://tests/forged_inventory_foundation.tres";
    private const string Evidence = "res://docs/美术规范/新器物行囊底座_20261023/";
    private int _checks;
    private void Check(bool ok, string text) { _checks++; if (!ok) throw new InvalidOperationException(text); }
    private static object? Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--read-forged-inventory"))
            {
                var loaded = ResourceLoader.Load<PersistentGameSave>(Save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
                Check(loaded is not null && loaded.format_version == PersistentGameSave.CurrentFormatVersion, "正式保存容器版本读回");
                var items = loaded!.player_data!.artifact_inventory;
                Check(items.Count == 2 && items[0].instance_id == "inventory-smoke-a" && items[1].instance_id == "inventory-smoke-b", "跨进程保留两个同名唯一实例");
                Check(items.All(item => ForgedArtifactInventory.TryRead(item, out var a) && a.Sharpness > 0 && a.Sources.Count > 0 && a.ParameterSnapshot.Length > 0), "跨进程读回完整属性与材料参数快照");
                Check(items[0].artifact_name == items[1].artifact_name && loaded.player_data.equipped_artifact_ids.Count == 0, "不堆叠且不自动装备");
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Save));
                GD.Print($"FORGED_INVENTORY_RESTART_OK {_checks}"); GetTree().Quit(); return;
            }
            var model = new ProcessForgingTrial();
            var snapshot = model.Evaluate(model.ExampleDraft(1)).Product!;
            snapshot.InstanceId = "inventory-smoke-a";
            var first = ForgedArtifactInventory.Create(snapshot);
            string originalName = snapshot.Name;
            snapshot.Name = "修改输入不得影响已入包快照";
            Check(ForgedArtifactInventory.TryRead(first, out var archived) && archived.Name == originalName, "固定快照不受输入对象变动影响");
            archived.InstanceId = "inventory-smoke-b";
            var second = ForgedArtifactInventory.Create(archived);
            var data = new HumanData { unique_id = "forged-inventory-test-owner", max_health = 100, current_health = 100,
                artifact_slot_count = 4, max_spirit_perception = 0, used_spirit_perception = 0 };
            var owner = new Character { entity_data = data }; AddChild(owner);
            int initialSlots = owner.get_inventory_slot_count(); // 人物可能自带外观衣物，同样占正式行囊位置。
            Check(owner.add_artifact_to_inventory(first), "新快照进入正式角色行囊接口");
            Check(!owner.add_artifact_to_inventory(first) && data.artifact_inventory.Count == 1, "同实例重复提交不增加物品");
            Check(owner.add_artifact_to_inventory(second) && data.artifact_inventory.Count == 2, "同名不同实例分别入包");
            Check(data.artifact_inventory_positions.Concat(data.inventory_item_positions).Distinct().Count() == initialSlots + 2 && owner.get_inventory_slot_count() == initialSlots + 2, "两个实例各占一格且不与既有物品位置重叠");
            Check(owner.equip_artifact(first.instance_id) && data.used_spirit_perception == 0 && owner.unequip_artifact(first.instance_id), "新剑器可装备卸下且不占灵识");
            var invalid = new ArtifactInstanceData { instance_id = "broken", forged_snapshot_json = "{broken" };
            Check(!owner.add_artifact_to_inventory(invalid) && data.artifact_inventory.Count == 2, "损坏快照拒绝入包");
            Check(ForgedArtifactInventory.Hover(first).Icon is not null, "新器物有真实图标");
            Check(!ForgedArtifactInventory.Hover(first).Body.Contains("主功用") && !ForgedArtifactInventory.Hover(first).Subtitle.Contains("内炼"), "新详情不套旧五行主功用祭法");
            var legacy = new ArtifactInstanceData { instance_id = "legacy" };
            Check(!ForgedArtifactInventory.IsForged(legacy) && owner.add_artifact_to_inventory(legacy), "旧器物仍可入包");
            Check(owner.equip_artifact(legacy.instance_id) && owner.unequip_artifact(legacy.instance_id), "旧实例仍可沿原规则装备卸下");
            Check(owner.remove_artifact_from_inventory("legacy") && owner.remove_artifact_from_inventory(second.instance_id), "唯一物品移出释放位置");
            Check(owner.add_artifact_to_inventory(second), "重新入包保留实例");
            for (int i = owner.get_inventory_slot_count(); i < Character.InventorySlotCapacity; i++)
                Check(owner.add_artifact_to_inventory(new ArtifactInstanceData { instance_id = "filler-" + i }), "填满行囊");
            Check(!owner.add_artifact_to_inventory(new ArtifactInstanceData { instance_id = "overflow" }), "满行囊不吞物品");
            foreach (var filler in data.artifact_inventory.Where(x => x.instance_id.StartsWith("filler-")).ToArray()) owner.remove_artifact_from_inventory(filler.instance_id);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://tests"));
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = data, save_name = "器物行囊隔离验收" }, Save) == Error.Ok, "完整正式存档容器保存成功");

            var storage = GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>(); AddChild(storage);
            storage.bind_player(owner); storage.show_panel();
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var slot = storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().First(x => x.artifact_data?.instance_id == first.instance_id);
            var content = (HoverInfoManager.HoverInfoContent)Invoke(storage, "build_hover_content", slot)!;
            Check(content.Title == originalName && content.Icon is not null && content.Body.Contains("\n锋锐：" + archived.Sharpness), "真实行囊读取新实例详情");
            var mouse = slot.GetGlobalTransformWithCanvas() * (slot.Size / 2);
            Input.FlushBufferedEvents(); Input.ParseInputEvent(new InputEventMouseMotion { Position = mouse, GlobalPosition = mouse }); Input.FlushBufferedEvents();
            Invoke(storage, "on_slot_info_requested", slot, false);
            await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
            Check(GetTree().Root.FindChild("SharedHoverInfoCard", true, false) is Control { Visible: true }, "真实行囊共用悬停显示");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            Check(image.SavePng(Evidence + "01_行囊器物.png") == Error.Ok, "行囊截图保存");
            HoverInfoManager.HideAll();
            Invoke(storage, "on_slot_info_requested", slot, true);
            await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
            var action = (Button)typeof(StoragePanel).GetField("_action_button", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(storage)!;
            Check(action.IsVisibleInTree() && !action.Disabled && action.Text == "装备", "新剑器显示正式装备操作");
            storage.hide_panel(); Check(owner.get_inventory_slot_count() == initialSlots + 2, "查看不改库存");
            Input.WarpMouse(new(1880, 1040)); storage.QueueFree(); owner.QueueFree();
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"FORGED_INVENTORY_SMOKE_OK {_checks}"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
