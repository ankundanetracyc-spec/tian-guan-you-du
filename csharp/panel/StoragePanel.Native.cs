using Godot;

public partial class StoragePanel
{
    private const float NativeCell = InventoryGridMetrics.DesignCell;
    private VBoxContainer? _nativeIndex;
    private ScrollContainer? _nativeIndexScroll, _nativeCategoryScroll;
    private Label? _nativeStatus, _nativeCount;
    private NativeInventorySurface? _nativeSurface;
    private readonly Dictionary<string, StorageItemSlot> _nativeSlots = [];
    private readonly Dictionary<string, Control> _nativeRows = [];
    private readonly Dictionary<string, string> _nativeCursors = [];
    private readonly Dictionary<string, Rect2> _nativeRegions = new()
    {
        ["chest"] = InventoryGridMetrics.CenteredRegion(556, 144, 8, 5), ["sleeve-left"] = InventoryGridMetrics.CenteredRegion(192, 368, 4, 5),
        ["sleeve-right"] = InventoryGridMetrics.CenteredRegion(884, 368, 4, 5), ["waist"] = InventoryGridMetrics.CenteredRegion(556, 588, 8, 5),
        ["back"] = InventoryGridMetrics.CenteredRegion(1184, 354, 8, 10)
    };
    private string _nativeLocated = "", _nativeSelected = "";
    private static string native_group(SpatialInventoryEntry item) => item.Item is StorageBagItemData ? "bag:" + item.InstanceId : item.Artifact is not null ? "artifact:" + item.ArtifactId : item.ItemPath;
    private string native_name(SpatialInventoryEntry item) => item.Item is StorageBagItemData
        ? _player?.get_storage_bags().FirstOrDefault(r => r.InstanceId == item.InstanceId)?.DisplayName ?? StorageBagItemData.ItemName
        : item.Artifact is not null ? artifact_name(item.Artifact) : item.Item?.entity_name ?? "未知物品";
    private static string native_category(SpatialInventoryEntry item) => item.Artifact is not null || item.Item is EquipmentData || item.Item is SectArtifactOfferData ? "器物" : item.Item?.item_category ?? "";
    private List<SpatialInventoryEntry> native_entries() => _player?.get_accessible_spatial_inventory().ToList() ?? [];
    private bool native_matches(SpatialInventoryEntry item) => (_category_filter == "" || native_category(item) == (_category_filter == ArtifactFilter ? "器物" : _category_filter))
        && (_search_text.Length == 0 || native_name(item).Contains(_search_text, StringComparison.OrdinalIgnoreCase));

    private static StyleBoxTexture native_paper(string file, int margin = 44) => InventoryUiPaper.Style(file, margin, margin == 44 ? 18 : 10);
    private Panel native_panel(float x, float y, float width, float height)
    {
        var panel = new Panel { MouseFilter = MouseFilterEnum.Pass };
        panel.AddThemeStyleboxOverride("panel", native_paper("index_panel_paper.png"));
        place(panel, _canvas!, x, y, width, height); return panel;
    }
    private void build_inventory_view()
    {
        _inventory_font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        _canvas = new Control { Name = "InventoryCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_canvas);
        place(new ColorRect { Color = new("e8e0c9"), MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, 1920, 1080);
        place(new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new(1, 1, 1, .2f), MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, 1920, 1080);
        var back = paper_button("返回", CraftingPaperHeader.FontSize, true);
        back.Name = "StorageReturn";
        _nativeMainBack = back;
        _canvas.AddChild(back); CraftingPaperHeader.PlaceReturn(_canvas, back); CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += () => { if (_nativeContainerId.Length > 0) open_native_container(""); else hide_panel(); };
        _nativePageTitle = text("行囊", 44, Ink); _nativePageTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        place(_nativePageTitle, _canvas, 124, 26, 1380, 70);
        _nativePageRule = rule(); place(_nativePageRule, _canvas, 48, 102, 1824, 1);
        var indexPanel = native_panel(42, 124, 392, 906);
        _nativeIndexPanel = indexPanel;
        var search = new LineEdit { PlaceholderText = "查找物品", ClearButtonEnabled = true };
        style_input(search); place(search, indexPanel, 30, 30, 332, 48);
        search.TextChanged += value => { _search_text = value.Trim(); refresh_slots(); };
        var prev = paper_button("‹", 28, true); var next = paper_button("›", 28, true);
        place(prev, indexPanel, 24, 91, 24, 42); place(next, indexPanel, 340, 91, 24, 42);
        _nativeCategoryScroll = new ScrollContainer { Name = "CategoryScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        place(_nativeCategoryScroll, indexPanel, 50, 92, 284, 44);
        var categories = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        categories.AddThemeConstantOverride("separation", 5); _nativeCategoryScroll.AddChild(categories);
        foreach (var name in new[] { "全部", "装备", "材料", "秘籍", "阵盘", "器具", "符箓", "丹药", "灵膳", "器物" })
        {
            var key = name == "全部" ? "" : name == "器物" ? ArtifactFilter : name;
            var button = paper_button(name, 20, true); button.CustomMinimumSize = new(66, 40);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(state, native_paper("index_row_normal.png", 42));
            button.AddThemeStyleboxOverride("focus", focus_style());
            categories.AddChild(button); _category_buttons[key] = button; button.Pressed += () => filter_to_category(key);
        }
        prev.Pressed += () => _nativeCategoryScroll.ScrollHorizontal -= 220;
        next.Pressed += () => _nativeCategoryScroll.ScrollHorizontal += 220;
        _nativeCategoryScroll.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            { _nativeCategoryScroll.ScrollHorizontal += wheel.ButtonIndex == MouseButton.WheelDown ? 80 : -80; _nativeCategoryScroll.AcceptEvent(); }
        };
        place(text("物品", 18, new("7d856c")), indexPanel, 40, 148, 220, 36);
        var quantityTitle = text("数量", 18, new("7d856c")); quantityTitle.HorizontalAlignment = HorizontalAlignment.Right;
        place(quantityTitle, indexPanel, 265, 148, 66, 36);
        _nativeIndexScroll = new ScrollContainer { Name = "ItemIndex", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        place(_nativeIndexScroll, indexPanel, 30, 190, 332, 675);
        _nativeIndex = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _nativeIndex.AddThemeConstantOverride("separation", 4); _nativeIndexScroll.AddChild(_nativeIndex); style_scrollbar(_nativeIndexScroll.GetVScrollBar());
        var space = native_panel(458, 124, 1416, 906);
        _nativeSpacePanel = space;
        build_native_garment_lining(space);
        _nativeBagFrame = native_panel(36, 120, 1848, 888); _nativeBagFrame.Hide();
        _nativeSpaceTitle = text("行囊", 28, new("4b5142")); place(_nativeSpaceTitle, space, 28, 20, 1060, 45);
        _nativeBodyReturn = paper_button("返回", CraftingPaperHeader.FontSize, true); _nativeBodyReturn.Name = "StorageBagReturn";
        _canvas.AddChild(_nativeBodyReturn); CraftingPaperHeader.AddReturnArrow(_nativeBodyReturn);
        _nativeBodyReturn.Pressed += () => open_native_container(""); _nativeBodyReturn.Hide();
        var spaceRule = rule(); spaceRule.Color = new("a59e856e"); place(spaceRule, space, 28, 76, 1356, 1);
        _nativeStatus = text("", 18, new("5a7b50")); place(_nativeStatus, _canvas, 486, 204, 1300, 38);
        _nativeSpaceScroll = new ScrollContainer { Name = "StorageSpaceScroll", MouseFilter = MouseFilterEnum.Pass };
        place(_nativeSpaceScroll, _canvas, 486, 258, 1362, 710);
        _nativeSurface = new NativeInventorySurface { Name = "BodyStorage", MouseFilter = MouseFilterEnum.Pass, CustomMinimumSize = new(1350, 698) };
        _nativeSpaceScroll.AddChild(_nativeSurface); _nativeSurface.DrawContents = draw_native_inventory;
        _nativeBodyRegions = new(_nativeRegions); build_bag_modal(); configure_native_container();
        var bottomRule = rule(); bottomRule.Color = new("a59e856e"); place(bottomRule, space, 28, 846, 1356, 1);
        _nativeCount = text("", 18, new("73775e")); _nativeCount.HorizontalAlignment = HorizontalAlignment.Right;
        place(_nativeCount, _canvas, 1458, 982, 384, 32);
        // Keep the existing shared action popup and legacy review attachment interfaces available.
        _grid_scroll = new ScrollContainer { Visible = false }; _canvas.AddChild(_grid_scroll);
        _grid = new GridContainer(); _grid_scroll.AddChild(_grid);
        _empty = text("", 20, Ink); _empty.Hide(); _canvas.AddChild(_empty);
        _summary = _nativeCount; build_detail_popup();
        GetWindow().FocusExited += cancel_native_drag;
    }

    private HoverInfoManager.HoverInfoContent native_hover(SpatialInventoryEntry item)
    {
        var info = item.Artifact is not null ? EquipmentItemPresentation.Hover(item.Artifact, _player) : item.Item is not null ? ItemHover(item.Item, item.Count, item.ItemPath, _player) : new HoverInfoManager.HoverInfoContent(native_name(item), "");
        if (item.Item is StorageBagItemData && _player?.get_storage_bags().FirstOrDefault(r => r.InstanceId == item.InstanceId) is { } bag)
            info = info with { Title = StorageBagItemData.ItemName, Subtitle = $"器具 · {bag.Quality}", Sections = null,
                Body = $"袋内容量：{bag.Width} × {bag.Height}\n已用容量：{bag.UsedCapacity} / {bag.Capacity}\n不能放入储物袋。" };
        return ItemLayoutPresentation.AttachStorageVolume(info, item.Item is not null
            ? item.Volume : InventoryCapacityRules.ArtifactVolume(item.Artifact), item.Item?.can_stack == true ? item.Count : null);
    }
    private void refresh_native_inventory()
    {
        if (_nativeIndex is null || _nativeSurface is null) return;
        cancel_native_drag(); clear_active_info();
        configure_native_container();
        var entries = native_entries();
        if (!string.IsNullOrEmpty(_nativeLocated) && !entries.Any(e => e.InstanceId == _nativeLocated))
        { _nativeLocated = ""; if (_nativeStatus is not null) _nativeStatus.Text = ""; }
        foreach (var id in _nativeCursors.Keys.ToArray()) if (!entries.Any(e => e.InstanceId == _nativeCursors[id])) _nativeCursors.Remove(id);
        foreach (var child in _nativeIndex.GetChildren()) { _nativeIndex.RemoveChild(child); child.QueueFree(); }
        foreach (var slot in _nativeSlots.Values) { _nativeSurface.RemoveChild(slot); slot.QueueFree(); }
        _nativeSlots.Clear(); _nativeRows.Clear();
        var scene = GD.Load<PackedScene>("res://tscn/panel/StorageItemSlot.tscn");
        foreach (var entry in entries.Where(e => e.IsPlaced && e.ContainerId == _nativeContainerId))
        {
            if (!_nativeRegions.TryGetValue(entry.RegionId, out var rect)) continue;
            var slot = scene.Instantiate<StorageItemSlot>(); _nativeSurface.AddChild(slot);
            slot.spatial_drag_enabled = true;
            if (entry.Artifact is not null) { slot.set_artifact(entry.Artifact); slot.slot_index = entry.SlotIndex; }
            else if (entry.Item is not null) slot.set_item(entry.SlotIndex, entry.ItemPath, entry.Item, entry.Count);
            var original = (entry.Rotation & 1) == 0 ? entry.Size : new Vector2I(entry.Height, entry.Width);
            slot.configure_spatial_layout(new(entry.Width * NativeCell, entry.Height * NativeCell), new(original.X * NativeCell, original.Y * NativeCell), entry.Rotation);
            slot.Position = rect.Position + new Vector2(entry.X, entry.Y) * NativeCell;
            slot.Modulate = native_matches(entry) ? Colors.White : new(1, 1, 1, .25f);
            slot.SetMeta("spatial_instance_id", entry.InstanceId);
            HoverInfoManager.SetProvider(slot, () => native_hover(entry));
            slot.GuiInput += ev => native_slot_input(entry, slot, ev);
            slot.MouseEntered += () => { if (_nativeDrag is null && !_pinned_selection) { _nativeSelected = entry.InstanceId; _nativeSurface.QueueRedraw(); } };
            slot.MouseExited += () => { if (_nativeDrag is null && !_pinned_selection) { _nativeSelected = ""; _nativeSurface.QueueRedraw(); } };
            _nativeSlots[entry.InstanceId] = slot;
        }
        foreach (var group in entries.GroupBy(native_group).Where(g => g.Any(native_matches)).OrderBy(g => native_name(g.First()), StringComparer.CurrentCulture))
        {
            var list = group.OrderBy(e => e.InstanceId, StringComparer.Ordinal).ToList(); var entry = list.FirstOrDefault(e => _nativeCursors.GetValueOrDefault(group.Key) == e.InstanceId) ?? list[0];
            var row = new Button { Name = "IndexRow", CustomMinimumSize = new(0, 72), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand, ClipText = true };
            row.AddThemeStyleboxOverride("normal", native_paper(_nativeLocated == entry.InstanceId ? "index_row_selected.png" : "index_row_normal.png", 42));
            row.AddThemeStyleboxOverride("hover", native_paper("index_row_selected.png", 42));
            row.AddThemeStyleboxOverride("pressed", native_paper("index_row_selected.png", 42));
            row.AddThemeStyleboxOverride("focus", focus_style());
            _nativeIndex.AddChild(row); _nativeRows[group.Key] = row;
            var name = text(native_name(entry), 22, Ink); name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            place(name, row, 24, 17, 220, 38);
            var count = text("×" + list.Sum(e => e.Count), 25, new("587650")); count.HorizontalAlignment = HorizontalAlignment.Right;
            var progress = list.FindIndex(e => _nativeCursors.GetValueOrDefault(group.Key) == e.InstanceId);
            place(count, row, 246, progress >= 0 ? 6 : 18, 62, 34);
            if (progress >= 0)
            { var label = text($"第{progress + 1}/{list.Count}件", 12, new("77876b")); label.HorizontalAlignment = HorizontalAlignment.Right; place(label, row, 228, 43, 80, 20); }
            HoverInfoManager.SetProvider(row, () => native_hover(entry));
            row.SetMeta("spatial_group", group.Key);
            row.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
                { if (mouse.DoubleClick) native_cycle_group(group.Key); else { _nativeSelected = entry.InstanceId; _nativeSurface.QueueRedraw(); } row.AcceptEvent(); }
                if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter }) { native_cycle_group(group.Key); row.AcceptEvent(); }
            };
        }
        if (_nativeIndex.GetChildCount() == 0) _nativeIndex.AddChild(text("没有找到对应物品", 20, new("858d75")));
        var visible = entries.Where(e => e.ContainerId == _nativeContainerId);
        int capacity = _nativeContainerId.Length == 0 ? Character.InventoryCapacity
            : _player?.get_storage_bags().FirstOrDefault(b => b.InstanceId == _nativeContainerId)?.Capacity ?? 0;
        _nativeCount!.Text = $"已用容量 {visible.Sum(e => e.CapacityUsed)} / {capacity}";
        set_parent_inventory_focus(_nativeContainerId.Length == 0);
        _nativeSurface.QueueRedraw();
    }

    internal bool native_cycle_group(string key)
    {
        var group = native_entries().Where(e => native_group(e) == key).OrderBy(e => e.InstanceId, StringComparer.Ordinal).ToList();
        if (group.Count == 0) return false;
        var index = group.FindIndex(e => e.InstanceId == _nativeCursors.GetValueOrDefault(key));
        var item = group[(index + 1) % group.Count];
        _nativeCursors[key] = item.InstanceId; _nativeLocated = _nativeSelected = item.InstanceId;
        _nativeContainerId = item.ContainerId;
        var scroll = _nativeIndexScroll?.ScrollVertical ?? 0;
        refresh_native_inventory();
        if (_nativeIndexScroll is not null) _nativeIndexScroll.ScrollVertical = scroll;
        _nativeStatus!.Text = $"已定位：{native_name(item)} · 第{(index + 1) % group.Count + 1}/{group.Count}件";
        if (_nativeSlots.TryGetValue(item.InstanceId, out var target)) _nativeSpaceScroll?.EnsureControlVisible(target);
        return true;
    }
    private void draw_native_inventory(NativeInventorySurface surface)
    {
        foreach (var region in native_current_regions())
        {
            var rect = _nativeRegions[region.Id];
            if (_nativeContainerId.Length == 0)
            {
                draw_native_garment_grid(surface, region, rect);
                continue;
            }
            draw_native_bag_grid(surface, region, rect);
        }
        var draggingId = _nativeDrag is { Active: true } drag ? drag.Item.InstanceId : "";
        foreach (var entry in native_entries().Where(e => e.IsPlaced && e.ContainerId == _nativeContainerId && e.InstanceId != draggingId && (e.InstanceId == _nativeLocated || e.InstanceId == _nativeSelected)))
        {
            var rect = new Rect2(_nativeRegions[entry.RegionId].Position + new Vector2(entry.X, entry.Y) * NativeCell, new Vector2(entry.Width, entry.Height) * NativeCell);
            surface.DrawRect(rect, new(.55f, .7f, .44f, _nativeContainerId.Length == 0 ? .06f : .15f));
            // 描边中心线内缩半个线宽，让外缘与图标显示格形一致。
            surface.DrawRect(rect.Grow(-1), new(_nativeContainerId.Length == 0 ? "7e8869" : "5f8150"), false, 2);
        }
        draw_native_drop(surface);
    }
}

public partial class NativeInventorySurface : Control
{
    public Action<NativeInventorySurface>? DrawContents;
    public override void _Draw() => DrawContents?.Invoke(this);
}
