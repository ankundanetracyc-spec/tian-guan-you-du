using Godot;
using System.Reflection;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Actual receipt pointer input, spatial transactions and callback conservation; no player saves.</summary>
public partial class ItemClaimSpatialNativeSmoke : Node
{
    private const string Stone = "res://resources/entities/items/灵石.tres", Book = "res://resources/entities/items/玄枢含章经秘籍.tres", Iron = "res://resources/entities/items/玄铁.tres";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks;
    private readonly List<Character> _players = [];
    private ItemClaimPanel _panel = null!;
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private Character Player() { var p = new Character { entity_data = new HumanData { entity_name = "领取空间验证" } }; _players.Add(p); return p; }
    private static string Stamp(Character p) => Character.claim_inventory_stamp(p.get_spatial_inventory());
    private object? Call(string name, params object?[] args) => typeof(ItemClaimPanel).GetMethod(name, Flags)!.Invoke(_panel, args);
    private T Field<T>(string name) => (T)typeof(ItemClaimPanel).GetField(name, Flags)!.GetValue(_panel)!;
    private List<SpatialInventoryEntry> Layout => Field<List<SpatialInventoryEntry>>("_layout");
    private static int Count(GArray array, string path) => array.Sum(v => { var d = v.AsGodotDictionary(); return d["item_path"].AsString() == path ? d["count"].AsInt32() : 0; });
    private void Present(Character p, params (string Path, int Count)[] items)
    {
        var rewards = new GArray(); foreach (var i in items) rewards.Add(new GDictionary { ["item_path"] = i.Path, ["count"] = i.Count, ["source"] = "测试来源" });
        _panel = new ItemClaimPanel(); AddChild(_panel); Check(_panel.Present(p, rewards), "领取页无法打开");
    }
    private async Task Frames(int n = 4) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Move(Vector2 p) => GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = MouseButtonMask.Left }, true);
    private void Mouse(Vector2 p, bool pressed) => GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    private void Key(Key key) { foreach (bool down in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down }, true); }
    private Vector2 CanvasPoint(Vector2 p) => Field<Control>("_canvas").GetGlobalTransformWithCanvas() * p;
    private async Task Grab(Control c)
    {
        var p = c.GetGlobalTransformWithCanvas() * (c.Size * .5f); Move(p); Mouse(p, true); Move(p + new Vector2(9, 0)); await Frames(2);
        Check(_panel.FindChild("DraggedItem", true, false) is not null, $"真实鼠标未抓起物品 check={_checks} point={p} drag={Field<object?>("_drag") is not null} viewport={GetViewport().GetVisibleRect().Size}");
    }
    private Control Row(string path) => Field<VBoxContainer>("_rows").GetChildren().OfType<Control>().First(r => r.HasMeta("item_path") && r.GetMeta("item_path").AsString() == path);
    private Dictionary<string, SpatialInventoryEntry> Withdrawn => Field<Dictionary<string, SpatialInventoryEntry>>("_withdrawn");
    private Control WithdrawnRow(string id) => Field<VBoxContainer>("_rows").GetChildren().OfType<Control>().First(r => r.HasMeta("withdrawn_instance_id") && r.GetMeta("withdrawn_instance_id").AsString() == id);
    private async Task DropLeft()
    {
        var p = CanvasPoint(new(220, 420)); Move(p); Mouse(p, false); await Frames();
    }
    private async Task Drop(string region, float x, float y)
    {
        var surface = Field<NativeInventorySurface>("_surface"); var r = Field<Dictionary<string, Rect2>>("_regions")[region];
        var p = surface.GetGlobalTransformWithCanvas() * (r.Position + new Vector2(x, y) * InventoryGridMetrics.DesignCell); Move(p); await Frames(2); Mouse(p, false); await Frames();
    }
    private async Task Close()
    { Call("commit_receipt"); await Frames(); Check(!GetTree().Paused, "关闭后暂停未恢复"); }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var reviewRoot = OS.GetEnvironment("STACKLANDS_GRID_CAPTURE_ROOT");
        var folder = reviewRoot.Length > 0 ? reviewRoot.TrimEnd('/') + "/领取/" : "res://docs/美术规范/领取空间收纳_20261103/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        using var img = GetViewport().GetTexture().GetImage(); Check(img.SavePng(folder + name) == Error.Ok, "截图保存失败");
    }
    private async Task AlchemyReceipt()
    {
        var actor = Player(); var data = (HumanData)actor.entity_data!;
        data.base_alchemy_proficiency = data.alchemy_proficiency = 36; data.max_spirit_perception = 10000; data.current_world_stamina = 100;
        AddChild(actor);
        var facilities = new LocationCraftingPanel(); AddChild(facilities); facilities.ShowFor(actor, true);
        var room = facilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
        typeof(AlchemySlotBrewPreviewScene).GetMethod("ApplySampleRoute", Flags)!.Invoke(room, [AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last(), false]);
        var inputs = (IReadOnlyList<AlchemySlotSimulation.Input>)typeof(AlchemySlotBrewPreviewScene).GetMethod("GetSimulationInputs", Flags)!.Invoke(room, null)!;
        foreach (var input in inputs) Check(actor.add_item_to_inventory(input.Path, input.Dose), $"炼丹样本投料入包失败：{input.Path} ×{input.Dose}，已有{actor.get_inventory_occupied_cell_count()}格");
        typeof(AlchemySlotBrewPreviewScene).GetField("_simpleAlchemyPresentation", Flags)!.SetValue(room, true);
        var task = (Task)typeof(AlchemySlotBrewPreviewScene).GetMethod("BrewWithPresentation", Flags)!.Invoke(room, null)!;
        for (int i = 0; i < 600 && !task.IsCompleted; i++)
        {
            room.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "确定 · 按丹效装瓶" && b.IsVisibleInTree())?.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(1);
        }
        Check(task.IsCompleted, "真实炼丹演出未到领取"); await task;
        _panel = (ItemClaimPanel)typeof(AlchemySlotBrewPreviewScene).GetField("_alchemyClaim", Flags)!.GetValue(room)!;
        Check(_panel is not null && inputs.All(i => actor.get_inventory_total_count(i.Path) == 0), "开炼应扣投料并进入共享领取");
        int oldCount = actor.get_spatial_inventory().Count;
        _panel!.FindChild("ClaimAutoPlace", true, false)!.Call("emit_signal", "pressed");
        Check(actor.get_spatial_inventory().Count == oldCount && Layout.Count > oldCount, "自动放入应只预览产物");
        _panel.FindChild("ClaimFooterPrimary", true, false)!.Call("emit_signal", "pressed"); await Frames();
        Check(actor.get_spatial_inventory().Count > oldCount && !GetTree().Paused, "炼丹确认未入包或未解除暂停");
        facilities.ReturnToLocation(); await Frames(); Check(!facilities.Visible, "领取完成后不能返回地点"); facilities.QueueFree();
    }
    private async Task LeftRoundTrip()
    {
        var p = Player();
        Check(p.add_item_to_inventory(Book) && p.add_item_to_inventory(Iron), "回收往返样例入包失败");
        var before = Stamp(p); var original = p.get_spatial_inventory().Single(e => e.ItemPath == Book);
        Present(p, (Book, 1)); await Frames();
        GArray claimed = new(), abandoned = new(); int notices = 0;
        typeof(ItemClaimPanel).GetField("_claimedCallback", Flags)!.SetValue(_panel, (Action<GArray>)(a => claimed = a));
        typeof(ItemClaimPanel).GetField("_abandonedCallback", Flags)!.SetValue(_panel, (Action<GArray>)(a => abandoned = a));
        p.ItemAcquired += (_, _, n) => notices += n;
        var source = Field<Dictionary<string, StorageItemSlot>>("_slots")[original.InstanceId];
        await Grab(source);
        Check(!source.IsVisibleInTree() && _panel.fits(original, original.RegionId, original.X, original.Y, original.Rotation), "领取拖动残留原物品或未释放自身占格");
        Key(Godot.Key.Escape); await Frames();
        Check(source.IsVisibleInTree() && Layout.Contains(original) && Stamp(p) == before, "取消领取拖动未恢复原物品");
        await Grab(source); Key(Godot.Key.R); await Drop("back", 1.5f, 1);
        var rotated = Layout.Single(e => e.InstanceId == original.InstanceId);
        await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[original.InstanceId]); await DropLeft();
        Check(Withdrawn[original.InstanceId] == rotated && Layout.All(e => e.InstanceId != original.InstanceId) && Stamp(p) == before, "左侧未保留原身份朝向或提前丢弃");
        Check(ReferenceEquals(WithdrawnRow(original.InstanceId), Row(Book)) && Row(Book).GetChildren().OfType<Label>().Any(l => l.Text == "×2")
            && Field<VBoxContainer>("_rows").GetChildren().OfType<Control>().Count(r => r.HasMeta("item_path") && r.GetMeta("item_path").AsString() == Book) == 1,
            "移出物品未与同类待领取合并为一行或数量错误");
        Check(!_panel.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text is "已移出行囊" or "待放入行囊"), "统一待领取列表仍显示额外分组");
        await Capture("05_领取_移出后可拖回.png");
        await Grab(WithdrawnRow(original.InstanceId)); await Drop("back", 6.5f, 9);
        Check(Withdrawn.Count == 0 && Layout.Single(e => e.InstanceId == original.InstanceId) is { Rotation: 1, X: 5, Y: 8 } && Stamp(p) == before, "拖回原物品丢失身份朝向或提前提交");
        Call("auto_place"); var incoming = Layout.Single(e => e.ItemPath == Book && e.InstanceId != original.InstanceId);
        await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[incoming.InstanceId]); await DropLeft();
        Check(Withdrawn.Count == 0 && Row(Book).GetChildren().OfType<Label>().Any(l => l.Text == "×1") && Layout.All(e => e.InstanceId != incoming.InstanceId), "新领物品未还原待领取数量或混入原有物品");
        await Grab(Row(Book)); await Drop("back", 1, 1.5f);
        Check(Layout.Count(e => e.ItemPath == Book) == 2, "新物品拖回后无法重新领取或重复生成");
        await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[original.InstanceId]); await DropLeft();
        Call("request_finish"); await Frames(); var dialog = Field<ConfirmationDialog>("_leaveDialog");
        Check(dialog.Visible && dialog.DialogText.Contains("丢弃") && Stamp(p) == before, "仅有移出物品时未明确确认丢弃");
        dialog.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(!dialog.Visible && Withdrawn.ContainsKey(original.InstanceId), "继续整理丢失可恢复物品");
        await Grab(WithdrawnRow(original.InstanceId)); await Drop("back", 6.5f, 9);
        Call("commit_receipt"); await Frames();
        Check(p.get_inventory_total_count(Book) == 2 && p.get_spatial_inventory().Count(e => e.InstanceId == original.InstanceId) == 1, "领取提交未保留原实例或数量不守恒");
        Check(Count(claimed, Book) == 1 && Count(abandoned, Book) == 0 && notices == 1, "移出拖回被当作新奖励重复通知");

        var stale = Player(); stale.add_item_to_inventory(Book); Present(stale, (Stone, 1));
        var staleBook = Layout.Single(); Call("discard_entry", staleBook); stale.add_item_to_inventory(Iron); before = Stamp(stale);
        Call("commit_receipt");
        Check(Withdrawn.Count == 0 && Layout.Any(e => e.InstanceId == staleBook.InstanceId) && Stamp(stale) == before, "库存变化后左侧残留副本或覆盖真实库存");
        await Close();

        var crafted = Player(); var artifact = GD.Load<SectArtifactOfferData>("res://resources/entities/equipment/清和剑.tres").CreateArtifact();
        Check(crafted.add_artifact_to_inventory(artifact), "器物回收样例失败"); Present(crafted, (Stone, 1));
        var entry = Layout.Single(); Check((bool)Call("discard_entry", entry)!, "器物无法移至左侧"); Call("refresh", ""); await Frames();
        await Grab(WithdrawnRow(entry.InstanceId)); await Drop("back", entry.Width * .5f, entry.Height * .5f);
        Call("commit_receipt"); await Frames();
        Check(crafted.get_artifact_items().Single().instance_id == artifact.instance_id && crafted.get_artifact_items().Single().forged_snapshot_json == artifact.forged_snapshot_json, "器物回收丢失实例或炼器快照");
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            var full = Player(); Check(full.add_item_to_inventory(Stone, Character.InventorySlotCapacity), "满包样本失败"); Present(full, (Stone, 1));
            var before = Stamp(full); Call("auto_place"); Check(Layout.Count == Character.InventorySlotCapacity && Stamp(full) == before, "满包自动放入改动库存或预览");
            var remove = Layout.First(); Check((bool)Call("discard_entry", remove)!, "不能预览丢弃");
            Check(Stamp(full) == before, "确认前提前丢弃"); Call("auto_place"); Check(Layout.Count == Character.InventorySlotCapacity, "丢弃腾空后不能放入");
            Call("commit_receipt"); Check(full.get_inventory_total_count(Stone) == Character.InventorySlotCapacity && !full.get_spatial_inventory().Any(e => e.InstanceId == remove.InstanceId), "没有丢弃指定实例"); await Frames();

            var partial = Player(); partial.add_item_to_inventory(Stone, Character.InventorySlotCapacity - 1); Present(partial, (Stone, 1), (Book, 1)); before = Stamp(partial);
            Call("auto_place"); Check(Layout.Count == Character.InventorySlotCapacity - 1 && Stamp(partial) == before, "失败批次部分放入"); await Close();

            var p = Player(); p.add_item_to_inventory(Book); var oldBook = p.get_spatial_inventory().Single();
            Present(p, (Book, 2), (Stone, 3)); await Frames(10); before = Stamp(p);
            GArray claimed = new(), abandoned = new(); int notices = 0, changes = 0;
            typeof(ItemClaimPanel).GetField("_claimedCallback", Flags)!.SetValue(_panel, (Action<GArray>)(a => claimed = a));
            typeof(ItemClaimPanel).GetField("_abandonedCallback", Flags)!.SetValue(_panel, (Action<GArray>)(a => abandoned = a));
            p.ItemAcquired += (_, _, n) => notices += n;
            p.InventoryChanged += _ => { changes++; Call("commit_receipt"); };
            await Grab(Row(Book)); Key(Godot.Key.R); await Drop("back", 1.5f, 1);
            Check(Layout.Count == 2 && Layout.Last().Rotation == 1 && Layout.Last().RegionId == "back", "新物品R旋转入包失败");
            Check(Stamp(p) == before, "预览提前写入人物");
            var newBook = Layout.Last(); await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[newBook.InstanceId]); Key(Godot.Key.Escape); await Frames();
            Check(Layout.Contains(newBook) && Field<bool>("_presented"), "Esc未仅取消拖动");
            await Grab(Row(Stone)); await Drop("back", .5f, .5f);
            Check(Layout.Count == 2, "重叠放置应退回待领取");
            await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[oldBook.InstanceId]); Key(Godot.Key.R); await Drop("waist", 1.5f, 1);
            Check(Layout.First(e => e.InstanceId == oldBook.InstanceId).Rotation == 1, "现有物品旋转失败");
            await Capture("01_领取收纳_1920.png");
            await Grab(Field<Dictionary<string, StorageItemSlot>>("_slots")[oldBook.InstanceId]); var outside = CanvasPoint(new(720, 980)); Move(outside); Mouse(outside, false); await Frames();
            Check(Layout.Count == 1 && Stamp(p) == before, "拖出未移除预览或提前实扣");
            Call("request_finish"); await Frames(); var dialog = Field<ConfirmationDialog>("_leaveDialog");
            Check(dialog.Visible && GetTree().Paused, "剩余物品没有退出确认"); dialog.Hide();
            _panel.FindChild("ClaimFooterPrimary", true, false)!.Call("emit_signal", "pressed"); await Frames();
            Check(dialog.Visible, "确认领取应提示剩余物品"); dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed); await Frames();
            Check(p.get_inventory_total_count(Book) == 1 && p.get_spatial_inventory().Single().InstanceId == newBook.InstanceId, "精确摆放与指定丢弃提交失败");
            Check(Count(claimed, Book) == 1 && Count(abandoned, Book) == 1 && Count(abandoned, Stone) == 3, "部分领取来源回调不守恒");
            Check(notices == 1 && changes == 1 && !GetTree().Paused, "提交重入或通知次数错误");

            var stale = Player(); Present(stale, (Book, 1)); Call("auto_place"); stale.add_item_to_inventory(Stone, Character.InventorySlotCapacity); before = Stamp(stale);
            Call("commit_receipt"); Check(Stamp(stale) == before && Field<bool>("_presented") && Layout.Count == Character.InventorySlotCapacity, "动态库存变化覆盖真实数据"); await Close();

            var protectedPlayer = Player(); const string sword = "res://resources/entities/equipment/素行剑.tres";
            Check(protectedPlayer.add_item_to_inventory(sword) && protectedPlayer.equip_inventory_item(sword), "装备样本创建失败");
            Present(protectedPlayer, (Stone, 1)); Check(!(bool)Call("discard_entry", Layout.Single())!, "使用中物品被丢弃"); await Close();
            var discardedNew = Player(); Present(discardedNew, (Book, 2)); Call("auto_place");
            Check((bool)Call("discard_entry", Layout.First())!, "新领物品无法丢弃"); Call("commit_receipt"); await Frames();
            Check(discardedNew.get_inventory_total_count(Book) == 1, "丢弃的新领物品仍然入包");

            await LeftRoundTrip();
            await AlchemyReceipt();

            var preview = Player(); preview.add_item_to_inventory(Book, 2); preview.add_item_to_inventory(Stone, 12); preview.add_item_to_inventory(Iron); preview.add_item_to_inventory("res://resources/entities/items/alchemy/huichun_2.tres");
            Present(preview, (Book, 1), (Stone, 6), (Iron, 1), ("res://resources/entities/items/alchemy/huichun_2.tres", 2)); await Frames(10);
            await Capture("02_领取收纳_完整.png"); GetWindow().Size = new(1280, 720); await Frames(12);
            Check(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).Encloses(Field<NativeInventorySurface>("_surface").GetGlobalRect()), "小窗五区裁切");
            await Capture("03_领取收纳_1280.png"); GetWindow().GrabFocus(); await Frames(5); await Grab(Row(Book)); Key(Godot.Key.R);
            var hoverPoint = Field<NativeInventorySurface>("_surface").GetGlobalTransformWithCanvas() * (Field<Dictionary<string, Rect2>>("_regions")["back"].Position + new Vector2(6.5f, 9) * InventoryGridMetrics.DesignCell);
            Move(hoverPoint); await Capture("04_领取收纳_拖动预览.png"); await Drop("back", 6.5f, 9); Check(Layout.Last() is { Rotation: 1, RegionId: "back", X: 5, Y: 8 }, "小窗领取不能放入新增末端格或旋转错误");
            await Capture("04_领取收纳_旋转.png"); await Close();
            GetTree().Paused = true; Present(Player(), (Stone, 1)); Call("auto_place"); Call("commit_receipt"); await Frames(); Check(GetTree().Paused, "原暂停状态丢失"); GetTree().Paused = false;
            foreach (var player in _players) player.Free(); GD.Print($"ITEM_CLAIM_SPATIAL_NATIVE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
