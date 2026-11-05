using Godot;
using System.Reflection;

/// <summary>Isolated container transactions and real storage controls; never writes a journey save.</summary>
public partial class StorageRingInventorySmoke : Node
{
    private const string Narrow = "res://tools/fixtures/storage_ring_narrow.tres", Wide = "res://tools/fixtures/storage_ring_wide.tres", Large = "res://tools/fixtures/storage_ring_large.tres";
    private const string Book = "res://resources/entities/items/玄枢含章经秘籍.tres", Stone = "res://resources/entities/items/灵石.tres";
    private const string Pill = "res://resources/entities/items/alchemy/huichun_1.tres", Sword = "res://resources/entities/equipment/素行剑.tres";
    private const string Clothes = "res://resources/entities/items/clothing/玉虚内门常服.tres";
    private const string Fullscreen = "res://tools/fixtures/storage_ring_fullscreen.tres";
    private const string Review = "res://docs/美术规范/储物戒指与行囊尺度_20261104/";
    private readonly List<Character> _models = [];
    private int _checks;
    private Character _player = null!;
    private StoragePanel _panel = null!;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool ok, string why) { _checks++; if (!ok) throw new InvalidOperationException(why); }
    private Character Model() { var p = new Character { entity_data = new HumanData { entity_name = "戒指隔离验证" } }; _models.Add(p); return p; }
    private T Field<T>(string name) => (T)typeof(StoragePanel).GetField(name, Flags)!.GetValue(_panel)!;
    private static string Stamp(Character p) => string.Join("|", p.get_accessible_spatial_inventory().OrderBy(e => e.InstanceId)
        .Select(e => $"{e.InstanceId}:{e.ItemPath}:{e.ArtifactId}:{e.ContainerId}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}:{e.Artifact?.forged_snapshot_json}"));
    private static SpatialInventoryEntry Entry(Character p, string id) => p.get_accessible_spatial_inventory().Single(e => e.InstanceId == id);

    private void Models()
    {
        var p = Model();
        Check(GD.Load<StorageRingItemData>(Narrow).validate_definition().Count == 0, "戒指定义校验失败");
        Check(StoragePanel.ItemHover(GD.Load<StorageRingItemData>(Narrow)).Body.Contains("3 × 2"), "通用悬停缺少戒指空间");
        Check(p.add_item_to_inventory(Narrow, 2) && p.add_item_to_inventory(Wide) && p.add_item_to_inventory(Book, 2) && p.add_item_to_inventory(Stone), "基础物品入包失败");
        var rings = p.get_storage_rings(); var small = rings.Where(r => r.Width == 3).ToArray(); var wide = rings.Single(r => r.Width == 8);
        Check(small.Length == 2 && small[0].InstanceId != small[1].InstanceId && small[0].DisplayName != small[1].DisplayName
            && small[0].Name == StorageRingItemData.ItemName && small[1].Name == StorageRingItemData.ItemName, "同名同品戒指必须独立并能区分");
        Check(p.get_spatial_inventory().Where(e => e.Item is StorageRingItemData).All(e => e.Width == 1 && e.Height == 1), "戒指本体不是1格");
        var books = p.get_spatial_inventory().Where(e => e.ItemPath == Book).ToArray();
        int notices = 0, changes = 0; p.ItemAcquired += (_, _, n) => notices += n; p.InventoryChanged += _ => changes++;
        Check(p.try_transfer_storage_item(books[0].InstanceId, small[0].InstanceId, out _), "矩形旋转收纳失败");
        Check(p.get_storage_ring_inventory(small[0].InstanceId).Single().Rotation == 1, "窄腔应真实旋转书册而非只看面积");
        Check(p.try_transfer_storage_item(books[1].InstanceId, small[1].InstanceId, out _), "第二枚戒指无法独立装入");
        Check(p.get_storage_ring_inventory(small[0].InstanceId).Single().InstanceId == books[0].InstanceId
            && p.get_storage_ring_inventory(small[1].InstanceId).Single().InstanceId == books[1].InstanceId, "多戒串库存");
        Check(notices == 0 && changes == 2 && p.get_inventory_total_count(Book) == 0, "收纳不得新增物品通知或把戒内当可用库存");
        var stone = p.get_spatial_inventory().Single(e => e.ItemPath == Stone); string before = Stamp(p);
        Check(!p.try_transfer_storage_item(stone.InstanceId, small[0].InstanceId, out _) && Stamp(p) == before, "满戒失败改变物品");
        Check(!p.try_transfer_storage_item(wide.InstanceId, small[0].InstanceId, out _), "不能装入空戒指");
        Check(!p.try_transfer_storage_item(small[0].InstanceId, small[0].InstanceId, out _), "不能把戒指装进自身");
        Check(!p.try_transfer_storage_item(small[0].InstanceId, wide.InstanceId, out _) && Stamp(p) == before, "不能嵌套有物戒指");
        int charged = 0;
        Check(!p.try_exchange_inventory(new Dictionary<string, int> { [Narrow] = 1 }, new Dictionary<string, int>(),
            beforeCommit: () => { charged++; return true; }) && charged == 0 && Stamp(p) == before, "不能出售有物戒指或提前扣费");
        var baseline = p.get_spatial_inventory();
        Check(!p.try_commit_claim_inventory(baseline, baseline.Where(e => e.InstanceId != small[0].InstanceId).ToList(), new Dictionary<string, int>()) && Stamp(p) == before,
            "领取事务不能丢弃有物戒指");
        Check(p.try_transfer_storage_item(books[1].InstanceId, "", out _), "取回失败");
        Check(p.remove_item_from_inventory(Narrow) && p.get_storage_rings().Single(r => r.Width == 3).InstanceId == small[0].InstanceId,
            "应只扣空戒，保留同类有物戒指");
        Check(p.try_transfer_storage_item(books[0].InstanceId, wide.InstanceId, out _), "戒间转移普通物品失败");
        Check(p.get_storage_ring_inventory(small[0].InstanceId).Count == 0 && Entry(p, books[0].InstanceId).ContainerId == wide.InstanceId, "转移未保持单一归属");
        Check(p.move_stored_item(books[0].InstanceId, wide.InstanceId, Character.StorageRingRegion, 4, 1, 1), "戒内精确移动失败");
        Check(!p.can_move_stored_item(books[0].InstanceId, wide.InstanceId, Character.StorageRingRegion, 7, 0, 1)
            && !p.can_move_stored_item(books[0].InstanceId, wide.InstanceId, Character.StorageRingRegion, -1, 0, 0), "戒内边界未校验");
        Check(p.try_transfer_storage_item(stone.InstanceId, wide.InstanceId, out _), "小件入戒失败");
        var storedStone = Entry(p, stone.InstanceId);
        Check(!p.can_move_stored_item(books[0].InstanceId, wide.InstanceId, Character.StorageRingRegion, storedStone.X, storedStone.Y, 0), "戒内碰撞未校验");
        Check(p.move_spatial_item(wide.InstanceId, "back", 5, 7, 0) && Entry(p, books[0].InstanceId).ContainerId == wide.InstanceId, "移动戒指本体丢失内容");

        Check(p.add_item_to_inventory(Large), "大戒夹具入包失败"); var large = p.get_storage_rings().Single(r => r.Width == 40);
        var offer = GD.Load<SectArtifactOfferData>("res://resources/entities/equipment/清和剑.tres"); var artifact = offer.CreateArtifact();
        Check(p.add_artifact_to_inventory(artifact) && p.try_transfer_storage_item(artifact.instance_id, large.InstanceId, out _), "器物不能收纳");
        Check(Entry(p, artifact.instance_id).Artifact?.forged_snapshot_json == artifact.forged_snapshot_json, "器物快照丢失");
        Check(p.add_item_to_inventory(Pill), "丹药入包失败"); var pill = p.get_spatial_inventory().Single(e => e.ItemPath == Pill);
        ((HumanData)p.entity_data!).is_in_breakthrough_retreat = true; ((HumanData)p.entity_data!).breakthrough_pill_path = Pill;
        Check(!p.try_transfer_storage_item(pill.InstanceId, large.InstanceId, out _), "突破预留丹药被收纳");
        ((HumanData)p.entity_data!).is_in_breakthrough_retreat = false;
        Check(p.try_transfer_storage_item(pill.InstanceId, large.InstanceId, out _) && !p.remove_item_from_inventory(Pill) && !p.set_battle_supply(0, Pill, 1),
            "戒内物品不能直接消费或编入战备");
        Check(p.add_item_to_inventory(Sword), "装备测试入包失败"); ((HumanData)p.entity_data!).weapon_path = Sword;
        var sword = p.get_spatial_inventory().Single(e => e.ItemPath == Sword);
        Check(!p.try_transfer_storage_item(sword.InstanceId, large.InstanceId, out _), "在用装备能移入戒指");
        ((HumanData)p.entity_data!).weapon_path = "";
        before = Stamp(p);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://outputs/inventory_scale_20261104"));
        const string saved = "res://outputs/inventory_scale_20261104/ring_roundtrip.tres";
        Check(ResourceSaver.Save(p.entity_data, saved) == Error.Ok, "角色保存失败");
        using (var stampFile = Godot.FileAccess.Open("res://outputs/inventory_scale_20261104/ring_roundtrip_stamp.txt", Godot.FileAccess.ModeFlags.Write)) stampFile.StoreString(before);
        var restored = Model(); restored.entity_data = ResourceLoader.Load<HumanData>(saved, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(Stamp(restored) == before, "戒内身份、位置、朝向或器物快照读档不一致");
        Check(restored.get_storage_rings().Single(r => r.InstanceId == wide.InstanceId) is { Width: 8, Height: 4, Quality: "八品" }, "矩形与品质读档不一致");
        Check(restored.try_transfer_storage_item(artifact.instance_id, "", out _) && restored.get_artifact_items().Any(a => a.instance_id == artifact.instance_id), "器物取回未回到原系统");
        Check(restored.get_accessible_spatial_inventory().Select(e => e.InstanceId).Distinct().Count() == restored.get_accessible_spatial_inventory().Count,
            "跨容器查询重复返回实例");

        var full = Model(); full.add_item_to_inventory(Narrow); full.add_item_to_inventory(Book);
        var book = full.get_spatial_inventory().Single(e => e.ItemPath == Book); var ring = full.get_storage_rings().Single();
        full.try_transfer_storage_item(book.InstanceId, ring.InstanceId, out _);
        Check(full.add_item_to_inventory(Stone, 119), "满包取回测试布置失败"); before = Stamp(full);
        Check(!full.try_transfer_storage_item(book.InstanceId, "", out _) && Stamp(full) == before, "满包取出不应部分提交");
        var many = Model(); many.add_item_to_inventory(Large); var bigId = many.get_storage_rings().Single().InstanceId;
        for (int i = 0; i < 130; i++)
        {
            if (!many.add_item_to_inventory(Stone)) throw new InvalidOperationException("循环入包失败");
            var item = many.get_spatial_inventory().Single(e => e.ItemPath == Stone);
            if (!many.try_transfer_storage_item(item.InstanceId, bigId, out _)) throw new InvalidOperationException("超过120件戒内收纳失败");
        }
        Check(many.get_storage_ring_inventory(bigId).Count == 130 && many.get_inventory_occupied_cell_count() == 1,
            "戒内容量不应套用普通行囊120件限制");
        var bottle = GD.Load<ItemData>(Pill); var clothing = GD.Load<ItemData>(Clothes);
        Check(bottle.get_inventory_size() == new Vector2I(1, 2) && clothing.get_inventory_size() == new Vector2I(2, 3), "修订尺寸未接入正式资源");
        InventoryIconDensityPack.Verify(); InventoryIconDensityPack.VerifyBindings();
        GD.Print($"STORAGE_RING_MODEL_OK {_checks}");
    }

    private async Task Frames(int n = 5) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Move(Vector2 point, bool held = false)
    {
        if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(point);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = held ? MouseButtonMask.Left : 0 }, true);
    }
    private void Mouse(Vector2 point, bool pressed) => GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    private async Task Click(Control control)
    {
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f); Move(point);
        Mouse(point, true); Mouse(point, false); await Frames();
    }
    private async Task Capture(string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        HoverInfoManager.HideAll(); await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); Check(image.SavePng(Review + file) == Error.Ok, "截图失败");
    }
    private StorageItemSlot Slot(string id) => Field<Dictionary<string, StorageItemSlot>>("_nativeSlots")[id];

    private async Task Interface()
    {
        _player = new Character { entity_data = new HumanData { entity_name = "收纳试阅", current_world_stamina = 100 } }; AddChild(_player);
        Check(_player.add_item_to_inventory(Narrow) && _player.add_item_to_inventory(Wide) && _player.add_item_to_inventory(Book, 2)
            && _player.add_item_to_inventory(Pill, 4) && _player.add_item_to_inventory(Stone, 12) && _player.add_item_to_inventory(Clothes), "界面样本失败");
        _panel = new StoragePanel(); AddChild(_panel); _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.bind_player(_player); _panel.show_panel(); await Frames(12);
        Check(Field<Dictionary<string, StorageItemSlot>>("_nativeSlots").All(pair => pair.Value.Size == (Vector2)Entry(_player, pair.Key).Size * InventoryGridMetrics.DesignCell), "物品点击范围必须等于真实占格");
        await Capture("01_行囊尺度1920.png");
        var narrow = _player.get_storage_rings().Single(r => r.Width == 3); var wide = _player.get_storage_rings().Single(r => r.Width == 8);
        var books = _player.get_spatial_inventory().Where(e => e.ItemPath == Book).ToArray();
        Check(_player.try_transfer_storage_item(books[0].InstanceId, narrow.InstanceId, out _), "界面定位样本失败");
        _panel.filter_to_category("秘籍");
        Check(_panel.native_cycle_group(Book), "清册定位失败"); var first = Field<string>("_nativeContainerId");
        Check(_panel.native_cycle_group(Book) && Field<string>("_nativeContainerId") != first && Field<string>("_category_filter") == "秘籍", "清册不能跨戒指循环或重置分类");
        _panel.filter_to_category(""); _panel.open_native_container(""); await Frames();
        await Click(Slot(narrow.InstanceId)); Check(Field<string>("_nativeContainerId") == narrow.InstanceId, "单击戒指未打开子页");
        await Click(Field<Button>("_nativeBodyReturn")); Check(Field<string>("_nativeContainerId") == "", "返回行囊失败");
        await Click(Slot(books[1].InstanceId));
        var into = Field<List<Button>>("_nativeStorageActions").Single(b => b.Text.Contains(wide.DisplayName));
        await Click(into); Check(Entry(_player, books[1].InstanceId).ContainerId == wide.InstanceId && Field<string>("_nativeContainerId") == wide.InstanceId, "实际移入按钮失败");
        var slot = Slot(books[1].InstanceId); var point = slot.GetGlobalTransformWithCanvas() * (slot.Size * .5f);
        Move(point); Mouse(point, true); Move(point + new Vector2(10, 0), true); await Frames(2);
        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = pressed }, true);
        var surface = Field<NativeInventorySurface>("_nativeSurface"); var rect = Field<Dictionary<string, Rect2>>("_nativeRegions")[Character.StorageRingRegion];
        point = surface.GetGlobalTransformWithCanvas() * (rect.Position + new Vector2(5.5f, 2) * InventoryGridMetrics.DesignCell);
        Move(point, true); Mouse(point, false); await Frames();
        Check(Entry(_player, books[1].InstanceId) is { Rotation: 1, X: 4, Y: 1 }, "戒内真实拖动旋转失败");
        await Click(Slot(books[1].InstanceId));
        Check(!Field<Button>("_action_button").Visible, "戒内物品不应直接显示使用");
        await Click(Field<List<Button>>("_nativeStorageActions").Single(b => b.Text == "取回行囊"));
        Check(Entry(_player, books[1].InstanceId).ContainerId == "", "实际取回按钮失败");
        var returnBook = Entry(_player, books[1].InstanceId); _player.move_spatial_item(returnBook.InstanceId, returnBook.RegionId, returnBook.X, returnBook.Y, 0);
        _player.try_transfer_storage_item(books[1].InstanceId, wide.InstanceId, out _);
        var clothes = _player.get_spatial_inventory().FirstOrDefault(e => e.Item is PortraitClothingData && !e.IsEquipped);
        if (clothes is not null) _player.try_transfer_storage_item(clothes.InstanceId, wide.InstanceId, out _);
        foreach (var item in _player.get_spatial_inventory().Where(e => e.ItemPath == Pill || e.ItemPath == Stone).Take(6).ToArray())
            _player.try_transfer_storage_item(item.InstanceId, wide.InstanceId, out _);
        _panel.open_native_container(wide.InstanceId); await Frames(); await Capture("02_戒指矩形收纳1920.png");
        GetWindow().Size = new(1280, 720); await Frames(12); await Capture("03_戒指矩形收纳1280.png");
        var visibleSlots = Field<Dictionary<string, StorageItemSlot>>("_nativeSlots");
        Check(visibleSlots.Count == _player.get_storage_ring_inventory(wide.InstanceId).Count, "戒内物品控件与实例不一致");
        Check(visibleSlots.Values.All(s => Mathf.IsEqualApprox(s.Size.X / s.Size.Y, (float)Entry(_player, s.GetMeta("spatial_instance_id").AsString()).Width / Entry(_player, s.GetMeta("spatial_instance_id").AsString()).Height)), "小窗口物品比例失真");
        _panel.open_native_container(""); await Frames(); await Capture("04_行囊尺度1280.png");
        Check(_player.add_item_to_inventory(Large), "大空间界面样本失败"); var large = _player.get_storage_rings().Single(r => r.Width == 40);
        _panel.open_native_container(large.InstanceId); await Frames(12);
        rect = Field<Dictionary<string, Rect2>>("_nativeRegions")[Character.StorageRingRegion];
        Check(rect.Size == new Vector2(40, 22) * InventoryGridMetrics.DesignCell
            && Field<ScrollContainer>("_nativeSpaceScroll").GetHScrollBar().MaxValue > 1362, "大戒指应保持格距并滚动");
        _panel.open_native_container(""); await Frames();
        Check(Field<Dictionary<string, StorageItemSlot>>("_nativeSlots").Count == _player.get_spatial_inventory().Count, "返回后混入戒内物品占格");
        _player.remove_item_from_inventory(Large);
        Check(_player.add_item_to_inventory(Fullscreen), "整屏试样入包失败");
        var full = _player.get_storage_rings().Single(r => r.Width == 38 && r.Height == 18);
        var sampleClothesPath = new[] { Clothes, "res://resources/entities/items/clothing/玉虚外门常服.tres" }.First(path => !_player.is_item_equipped(path));
        _player.add_item_to_inventory(sampleClothesPath);
        var sampleClothes = _player.get_spatial_inventory().First(e => e.ItemPath == sampleClothesPath);
        _player.try_transfer_storage_item(sampleClothes.InstanceId, full.InstanceId, out _);
        foreach (var item in _player.get_accessible_spatial_inventory().Where(e => e.ItemPath == Book || e.ItemPath == Pill || e.ItemPath == Stone).Take(5).ToArray())
            _player.try_transfer_storage_item(item.InstanceId, full.InstanceId, out _);
        _panel.open_native_container(full.InstanceId); await Frames(12);
        rect = Field<Dictionary<string, Rect2>>("_nativeRegions")[Character.StorageRingRegion];
        Check(rect.Size == new Vector2(1824, 864) && rect.Size == Field<ScrollContainer>("_nativeSpaceScroll").Size, "整屏网格未铺满正文");
        Check(Field<ColorRect>("_nativeRingShade").Visible && Field<Control>("_nativeParentUnderlay").Visible
            && Field<ScrollContainer>("_nativeSpaceScroll").ZIndex > Field<ColorRect>("_nativeRingShade").ZIndex, "戒指子页未覆盖暗罩或未保留下层行囊");
        Check(Slot(sampleClothes.InstanceId).Size == new Vector2(96, 144), "整屏展示不应放大单格或物品");
        GetWindow().Size = new(1920, 1080); await Frames(12); await Capture("05_整屏戒指1920.png");
        GetWindow().Size = new(1280, 720); await Frames(12); await Capture("06_整屏戒指1280.png");
        Check(_player.get_storage_rings().Single(r => r.InstanceId == full.InstanceId) is { Width: 38, Height: 18 }, "改变窗口改变了戒指容量");
        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = pressed }, true);
        await Frames();
        Check(Field<string>("_nativeContainerId") == "" && _panel.Visible && Field<Panel>("_nativeIndexPanel").Visible, "Esc未返回主行囊并恢复清册");
        await ModalInteraction(wide.InstanceId);
        GD.Print($"STORAGE_RING_INTERFACE_OK {_checks}");
    }

    private async Task ModalInteraction(string ringId)
    {
        _panel.filter_to_category("材料");
        var search = Field<Panel>("_nativeIndexPanel").FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().Single();
        search.GrabFocus();
        string before = Stamp(_player);
        string progress = string.Join("|", Field<Dictionary<string, string>>("_nativeCursors").OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value));
        _panel.open_native_container(ringId); await Frames();
        var shade = Field<ColorRect>("_nativeRingShade");
        Check(shade.Visible && shade.Color.A > 0 && shade.MouseFilter == Control.MouseFilterEnum.Stop, "暗罩未显示或未拦截输入");
        Check(Field<Control>("_nativeParentUnderlay").Visible && GetViewport().GuiGetFocusOwner() != search && search.FocusMode == Control.FocusModeEnum.None,
            "下层行囊未显示或仍持有键盘焦点");
        var region = Field<Dictionary<string, Rect2>>("_nativeRegions")[Character.StorageRingRegion];
        var info = _player.get_storage_rings().Single(r => r.InstanceId == ringId);
        var contents = _player.get_storage_ring_inventory(ringId);
        var empty = (from y in Enumerable.Range(0, info.Height) from x in Enumerable.Range(0, info.Width)
                     where contents.All(e => x < e.X || x >= e.X + e.Width || y < e.Y || y >= e.Y + e.Height) select new Vector2(x + .5f, y + .5f)).First();
        var surface = Field<NativeInventorySurface>("_nativeSurface");
        var point = surface.GetGlobalTransformWithCanvas() * (region.Position + empty * InventoryGridMetrics.DesignCell);
        Move(point); Mouse(point, true); Mouse(point, false); await Frames();
        Check(Field<string>("_nativeContainerId") == ringId, "点击戒内空格错误关闭子页");
        var item = contents.First(); var slot = Slot(item.InstanceId);
        point = slot.GetGlobalTransformWithCanvas() * (slot.Size * .5f);
        Move(point); Mouse(point, true); Move(point + new Vector2(10, 0), true); await Frames(2);
        point = shade.GetGlobalRect().Position + new Vector2(2, 2);
        Move(point, true); Mouse(point, false); await Frames();
        Check(Field<string>("_nativeContainerId") == ringId && Stamp(_player) == before, "拖动到暗罩松手应撤销，不能返回或改变物品");
        var behind = Field<Dictionary<string, Button>>("_category_buttons")["秘籍"];
        point = behind.GetGlobalTransformWithCanvas() * (behind.Size * .5f);
        Check(!Field<Panel>("_nativeRingFrame").GetGlobalRect().HasPoint(point), "暗罩点击测试点不在子页外");
        Move(point); Mouse(point, true); Mouse(point, false); await Frames();
        Check(Field<string>("_nativeContainerId") == "" && _panel.Visible && !shade.Visible, "点击暗罩未仅返回主行囊");
        Check(Field<string>("_category_filter") == "材料" && Stamp(_player) == before
            && progress == string.Join("|", Field<Dictionary<string, string>>("_nativeCursors").OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)),
            "暗罩点击穿透下层，或改变筛选、定位进度及物品");
        _panel.filter_to_category("");
        GetWindow().Size = new(1920, 1080); _panel.open_native_container(ringId); await Frames(12); await Capture("07_戒指暗罩1920.png");
        GetWindow().Size = new(1280, 720); await Frames(12); await Capture("08_戒指暗罩1280.png");
        point = shade.GetGlobalRect().Position + new Vector2(2, 2);
        Move(point); Mouse(point, true); Mouse(point, false); await Frames();
        Check(Field<string>("_nativeContainerId") == "" && Field<Panel>("_nativeIndexPanel").Visible && search.FocusMode == Control.FocusModeEnum.All,
            "小窗口暗罩返回未恢复行囊交互");
    }

    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Review));
            if (OS.GetCmdlineUserArgs().Contains("--read-ring-save"))
            {
                var loaded = Model(); loaded.entity_data = ResourceLoader.Load<HumanData>("res://outputs/inventory_scale_20261104/ring_roundtrip.tres", cacheMode: ResourceLoader.CacheMode.Ignore);
                Check(Stamp(loaded) == Godot.FileAccess.GetFileAsString("res://outputs/inventory_scale_20261104/ring_roundtrip_stamp.txt"), "跨进程戒内身份、位置和朝向不一致");
                Check(loaded.get_storage_rings().Any(r => r is { Width: 8, Height: 4, Quality: "八品" }), "跨进程矩形或品质未恢复");
                Check(loaded.get_storage_rings().Where(r => r.OccupiedCells > 0).Count() >= 2, "跨进程戒内内容未恢复");
                GD.Print($"STORAGE_RING_RESTART_OK {_checks}"); GetTree().Quit(); return;
            }
            if (DisplayServer.GetName() != "headless") { GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new(1920, 1080); await Frames(2); }
            Models(); await Interface();
            GD.Print($"STORAGE_RING_SMOKE_OK {_checks}"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("STORAGE_RING_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }
    public override void _ExitTree() { foreach (var model in _models) if (GodotObject.IsInstanceValid(model)) model.Free(); }
}
