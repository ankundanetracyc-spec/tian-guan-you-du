using Godot;
using System.Reflection;

/// <summary>Exercise the actual player panel and pointer handlers without touching any journey save.</summary>
public partial class InventoryNativeUiSmoke : Node
{
    private StoragePanel _panel = null!;
    private Character _player = null!;
    private int _checks;
    private readonly string _book = "res://resources/entities/items/玄枢含章经秘籍.tres";
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private T Field<T>(string name) => (T)typeof(StoragePanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_panel)!;
    private async Task Frames(int count = 5) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Move(Vector2 p, bool held = false) => GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, ButtonMask = held ? MouseButtonMask.Left : 0 }, true);
    private void Mouse(Vector2 p, bool pressed, bool twice = false) => GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = pressed, DoubleClick = twice }, true);
    private void Key(Key key) { foreach (var pressed in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed }, true); }
    private SpatialInventoryEntry Entry(string id) => _player.get_spatial_inventory().First(e => e.InstanceId == id);
    private async Task Grab(string id)
    {
        var slot = Field<Dictionary<string, StorageItemSlot>>("_nativeSlots")[id];
        var p = slot.GetGlobalTransformWithCanvas() * (slot.Size * .5f);
        Move(p); Mouse(p, true); Move(p + new Vector2(9, 0), true); await Frames(2);
        Check(Field<object?>("_nativeDrag") is not null && _panel.FindChild("DraggedItem", true, false) is not null, "真实鼠标未抓起物品");
        Check(!slot.IsVisibleInTree(), "拖动时原位置仍显示物品");
    }
    private string Fingerprint() => string.Join("|", _player.get_spatial_inventory().Select(e => $"{e.InstanceId}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}"));
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var reviewRoot = OS.GetEnvironment("STACKLANDS_GRID_CAPTURE_ROOT");
        var path = reviewRoot.Length > 0 ? reviewRoot.TrimEnd('/') + "/行囊/" : "res://docs/美术规范/行囊交易善功堂实装_20261031/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(path + name) == Error.Ok, "原生截图保存失败");
    }
    public override async void _Ready()
    {
        try
        {
            _player = new Character { entity_data = new HumanData { entity_name = "行囊实装测试", current_world_stamina = 100 } };
            AddChild(_player);
            Check(_player.add_item_to_inventory(_book, 2), "新档书册入包失败");
            Check(_player.add_item_to_inventory("res://resources/entities/items/灵石.tres", 12), "新档灵石入包失败");
            Check(_player.add_item_to_inventory("res://resources/entities/items/alchemy/huichun_1.tres", 2), "新档丹药入包失败");
            Check(_player.add_item_to_inventory("res://resources/entities/items/玄铁.tres"), "新档玄铁入包失败");
            _panel = new StoragePanel(); AddChild(_panel); _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _panel.bind_player(_player); _panel.show_panel(); await Frames(12);
            var entries = _player.get_spatial_inventory();
            Check(Field<Dictionary<string, StorageItemSlot>>("_nativeSlots").Count == entries.Count, "实际物品与空间控件数量不符");
            Check(Character.InventoryRegions.All(r => Field<Dictionary<string, Rect2>>("_nativeRegions")[r.Id].Size == new Vector2(r.Width, r.Height) * InventoryGridMetrics.DesignCell), "显示区域与200格真实空间不一致");
            var rail = Field<ScrollContainer>("_nativeCategoryScroll");
            var tab = Field<Dictionary<string, Button>>("_category_buttons")["材料"];
            var tabPoint = tab.GetGlobalTransformWithCanvas() * (tab.Size * .5f);
            Move(tabPoint); Mouse(tabPoint, true); Mouse(tabPoint, false); await Frames();
            Check(Field<string>("_category_filter") == "材料", "分类单击失效");
            var railPoint = rail.GetGlobalTransformWithCanvas() * new Vector2(240, 20);
            Move(railPoint); Mouse(railPoint, true); Move(railPoint - new Vector2(160, 0), true); Mouse(railPoint - new Vector2(160, 0), false); await Frames();
            Check(rail.ScrollHorizontal > 0 && Field<string>("_category_filter") == "材料", "分类横拖未滚动或误切换分类");
            var priorScroll = rail.ScrollHorizontal;
            GetViewport().PushInput(new InputEventMouseButton { Position = railPoint, GlobalPosition = railPoint, ButtonIndex = MouseButton.WheelDown, Pressed = true }, true); await Frames();
            Check(rail.ScrollHorizontal > priorScroll, "分类滚轮横滑无效");
            rail.ScrollHorizontal = 0; _panel.filter_to_category(""); await Frames();
            var rows = Field<Dictionary<string, Control>>("_nativeRows"); Check(rows.ContainsKey(_book), "缺少真实秘籍清册");
            var row = rows[_book]; var center = row.GetGlobalTransformWithCanvas() * (row.Size * .5f);
            Move(center); Mouse(center, true, true); Mouse(center, false); await Frames();
            var first = Field<string>("_nativeLocated"); Check(entries.Any(e => e.ItemPath == _book && e.InstanceId == first), "双击未定位实际书册");
            row = Field<Dictionary<string, Control>>("_nativeRows")[_book]; center = row.GetGlobalTransformWithCanvas() * (row.Size * .5f);
            Move(center); Mouse(center, true, true); Mouse(center, false); await Frames();
            Check(Field<string>("_nativeLocated") != first, "第二次双击未定位第二本书");
            _panel.filter_to_category("丹药"); Check(Field<Dictionary<string, StorageItemSlot>>("_nativeSlots").Count == entries.Count, "筛选隐藏了实际占格");
            _panel.filter_to_category(""); await Frames();
            await Grab(first); Key(Godot.Key.R);
            var target = (from region in Character.InventoryRegions from y in Enumerable.Range(0, region.Height) from x in Enumerable.Range(0, region.Width)
                where _player.can_move_spatial_item(first, region.Id, x, y, 1) select (region.Id, X: x, Y: y)).First();
            var surface = Field<NativeInventorySurface>("_nativeSurface"); var regionRect = Field<Dictionary<string, Rect2>>("_nativeRegions")[target.Id];
            var p = surface.GetGlobalTransformWithCanvas() * (regionRect.Position + new Vector2(target.X + 1.5f, target.Y + 1) * InventoryGridMetrics.DesignCell);
            Move(p, true); await Frames(2); await Capture("01_原生行囊旋转拖动.png"); Mouse(p, false); await Frames();
            var book = Entry(first); Check(book.Rotation == 1 && book.Width == 3 && book.Height == 2, "R旋转或真实入位失败");
            Check(book.RegionId == target.Id && book.X == target.X && book.Y == target.Y, "鼠标位置与真实落格不一致");
            var before = Fingerprint(); await Grab(first); Move(new(2, 2), true); Mouse(new(2, 2), false); await Frames();
            Check(Fingerprint() == before, "界外松手改变库存");
            await Grab(first); Key(Godot.Key.R); Key(Godot.Key.Escape); await Frames(); Check(Fingerprint() == before && _panel.Visible && Field<Dictionary<string, StorageItemSlot>>("_nativeSlots")[first].IsVisibleInTree(), "Esc取消移动未恢复或错误关闭行囊");
            Check(_panel.FindChild("DraggedItem", true, false) is null, "取消后拖动图像残留");
            Check(_player.can_move_spatial_item(first, "back", 5, 8, 1), "新增背囊末端空间样例被占用");
            await Grab(first);
            var expandedTarget = surface.GetGlobalTransformWithCanvas() * (Field<Dictionary<string, Rect2>>("_nativeRegions")["back"].Position + new Vector2(6.5f, 9) * InventoryGridMetrics.DesignCell);
            Move(expandedTarget, true); await Frames(2); Mouse(expandedTarget, false); await Frames();
            Check(Entry(first) is { RegionId: "back", X: 5, Y: 8, Rotation: 1 }, "实际鼠标无法放入新增背囊边缘格");
            await Capture("02_原生行囊空间与清册.png");
            GetWindow().Size = new(1280, 720); await Frames(12);
            var canvas = Field<Control>("_canvas"); Check(Mathf.IsEqualApprox(canvas.Scale.X, canvas.Scale.Y), "窄窗格子缩放不一致");
            Check(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).Encloses(surface.GetGlobalRect()), "窄窗五区未完整显示");
            await Capture("03_原生行囊1280.png");
            _panel.hide_panel(); Check(!_panel.Visible, "返回无法关闭正式行囊");
            GD.Print($"INVENTORY_NATIVE_UI_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
