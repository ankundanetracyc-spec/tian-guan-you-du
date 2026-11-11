using Godot;

public partial class StoragePanel
{
    private GridContainer? _volumeCards;
    private ScrollContainer _volumeScroll = null!;
    private VBoxContainer _volumeContainers = null!;
    private Label _volumeTitle = null!, _volumeCapacity = null!, _volumeStatus = null!;
    private Button _volumeDiscard = null!, _volumeUse = null!, _volumeMove = null!;
    private SpatialInventoryEntry? _volumeSelected;
    internal event Action? VolumeClosed;

    internal Control UseVolumeForReceiving()
    {
        _volumeScroll.Size = new Vector2(1160, _volumeScroll.Size.Y);
        _volumeCards!.Columns = 5;
        return _canvas!;
    }

    private void build_volume_inventory_view()
    {
        _inventory_font = GD.Load<FontFile>(CraftingPaperStyle.FontPath);
        var matte = new ColorRect { Color = new("efe4cf"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(matte);
        matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "InventoryCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_canvas);
        place(text("行囊", 36, Ink), _canvas, 34, 20, 320, 54);
        var back = new Button { Text = "返回", Name = "StorageReturn" };
        PaperButtonFeedback.Apply(back);
        _canvas.AddChild(back);
        CraftingPaperHeader.PlaceReturn(_canvas, back);
        CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += hide_panel;

        place(new ColorRect { Color = new("b6a384"), MouseFilter = MouseFilterEnum.Ignore }, _canvas, 300, 116, 1, 892);
        place(text("收纳", 29, Ink), _canvas, 34, 110, 226, 42);
        var bags = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        place(bags, _canvas, 34, 168, 246, 830);
        _volumeContainers = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _volumeContainers.AddThemeConstantOverride("separation", 10);
        bags.AddChild(_volumeContainers);

        _volumeTitle = text("", 29, Ink);
        _volumeCapacity = text("", 24, Ink);
        _volumeCapacity.HorizontalAlignment = HorizontalAlignment.Right;
        place(_volumeTitle, _canvas, 326, 110, 640, 42);
        place(_volumeCapacity, _canvas, 1500, 110, 382, 42);
        int x = 326;
        foreach (string category in new[] { "全部", "装备", "材料", "秘籍", "阵盘", "器具", "符箓", "丹药", "灵膳", "器物" })
        {
            var tab = new Button { Text = category, ToggleMode = true };
            string key = normalize_category(category);
            PaperButtonFeedback.Apply(tab, CraftingPaperStyle.ButtonKind.Tab, 22);
            place(tab, _canvas, x, 166, 86, 42);
            _category_buttons[key] = tab;
            tab.Pressed += () => filter_to_category(key);
            x += 94;
        }
        var search = new LineEdit { PlaceholderText = "查找物品", ClearButtonEnabled = true };
        CraftingPaperStyle.ApplyInput(search, 22);
        place(search, _canvas, 1640, 166, 242, 42);
        search.TextChanged += value =>
        {
            _search_text = value.Trim();
            _volumeScroll.ScrollVertical = 0;
            refresh_volume_inventory();
        };

        _volumeScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        place(_volumeScroll, _canvas, 326, 232, 1556, 720);
        _volumeCards = new GridContainer { Columns = 7 };
        _volumeCards.AddThemeConstantOverride("h_separation", 12);
        _volumeCards.AddThemeConstantOverride("v_separation", 12);
        _volumeScroll.AddChild(_volumeCards);
        _volumeStatus = text("", 22, Ink);
        place(_volumeStatus, _canvas, 326, 979, 780, 50);
        _volumeUse = volume_action("", 1332, use_volume_selection);
        _volumeMove = volume_action("", 1514, move_volume_selection, 224);
        _volumeDiscard = volume_action("丢弃", 1758, discard_volume_selection);
    }

    private Button volume_action(string title, float x, Action action, float width = 124)
    {
        var button = new Button { Text = title };
        PaperButtonFeedback.Apply(button);
        place(button, _canvas!, x, 979, width, 50);
        button.Pressed += action;
        return button;
    }

    private static string volume_bag_name(int index) => "储物袋" + (index < 10 ? "一二三四五六七八九"[index - 1].ToString() : index.ToString());

    private void refresh_volume_inventory()
    {
        if (_volumeCards is null) return;
        var bags = _player?.get_storage_bags() ?? [];
        var bag = bags.FirstOrDefault(item => item.InstanceId == _nativeContainerId);
        if (bag is null) _nativeContainerId = "";
        string name = bag is null ? "随身行囊" : volume_bag_name(bags.ToList().IndexOf(bag) + 1);
        int used = bag?.UsedCapacity ?? _player?.get_inventory_used_capacity() ?? 0;
        int capacity = bag?.Capacity ?? Character.InventoryCapacity;
        _volumeTitle.Text = name;
        _volumeCapacity.Text = $"容量 {used} / {capacity}";

        clear_volume_children(_volumeContainers);
        add_volume_container("", "随身行囊", _player?.get_inventory_used_capacity() ?? 0, Character.InventoryCapacity);
        for (int index = 0; index < bags.Count; index++)
        {
            var owned = bags[index];
            add_volume_container(owned.InstanceId, volume_bag_name(index + 1), owned.UsedCapacity, owned.Capacity);
        }
        var contents = (bag is null ? _player?.get_spatial_inventory() : _player?.get_storage_bag_inventory(bag.InstanceId)) ?? [];
        var shown = contents.Where(entry =>
            (_category_filter.Length == 0 || (entry.Artifact is not null || entry.Item is SectArtifactOfferData
                ? _category_filter is "器物" or ArtifactFilter
                : entry.Item is not null && category_of(entry.Item, entry.ItemPath) == _category_filter))
            && native_name(entry).Contains(_search_text, StringComparison.OrdinalIgnoreCase)).ToList();
        _volumeSelected = shown.FirstOrDefault(entry => entry.InstanceId == _volumeSelected?.InstanceId);
        clear_volume_children(_volumeCards);
        foreach (var entry in shown)
        {
            var card = make_volume_tile(native_name(entry), entry.Count, entry.Artifact is { } artifact
                ? ForgedArtifactInventory.IconPath(artifact) : entry.Item?.icon_path ?? "", new Vector2(210, 142));
            style_volume_tile(card, entry.InstanceId == _volumeSelected?.InstanceId);
            _volumeCards.AddChild(card);
            card.SetMeta("inventory_id", entry.InstanceId);
            HoverInfoManager.SetProvider(card, () => native_hover(entry));
            card.Pressed += () =>
            {
                _volumeStatus.Text = "";
                _volumeSelected = entry;
                foreach (var tile in _volumeCards.GetChildren().OfType<Button>())
                    style_volume_tile(tile, tile.GetMeta("inventory_id").AsString() == entry.InstanceId);
                refresh_volume_actions();
            };
        }
        refresh_volume_actions();
    }

    private void add_volume_container(string id, string title, int used, int capacity)
    {
        var button = new Button { Text = $"{title}\n{used} / {capacity}", CustomMinimumSize = new(224, 70) };
        PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Tab, 23, _nativeContainerId == id);
        _volumeContainers.AddChild(button);
        button.Pressed += () => open_native_container(id);
    }

    private static void clear_volume_children(Node parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    internal static Button make_volume_tile(string name, int count, string? icon, Vector2 size)
    {
        var tile = new Button { CustomMinimumSize = size, FocusMode = FocusModeEnum.None };
        style_volume_tile(tile, false);
        var image = new TextureRect
        {
            Texture = string.IsNullOrEmpty(icon) ? null : YuxuLibraryArt.Texture(icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        place(image, tile, (size.X - 64) / 2, 12, 64, 64);
        var caption = CraftingPaperStyle.Text($"{name} ×{count}", 22);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        caption.MouseFilter = MouseFilterEnum.Ignore;
        place(caption, tile, 6, size.Y - 32, size.X - 12, 28);
        return tile;
    }

    private static void style_volume_tile(Button tile, bool selected)
    {
        tile.AddThemeStyleboxOverride("normal", volume_tile_face(selected ? "e5d3b6" : "f5ead5"));
        tile.AddThemeStyleboxOverride("hover", volume_tile_face("eadbbf"));
        tile.AddThemeStyleboxOverride("pressed", volume_tile_face("ddc8a6"));
        tile.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBoxFlat volume_tile_face(string color) => new()
    {
        BgColor = new(color), BorderColor = new("cab799"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
    };

    private string? volume_move_destination()
    {
        if (_player is null || _volumeSelected is not { } entry || !_player.can_store_spatial_item(entry.InstanceId)) return null;
        return entry.ContainerId.Length > 0 ? "" : _player.get_storage_bags().FirstOrDefault(bag => bag.InstanceId == _volumeMoveTarget)?.InstanceId;
    }

    private string _volumeMoveTarget = "";
    private void refresh_volume_actions()
    {
        var selected = _volumeSelected;
        var player = _player;
        _volumeDiscard.Disabled = selected is null || player?.can_discard_volume_entry(selected) != true;
        _volumeUse.Hide();
        _volumeMove.Hide();
        if (selected is null || player is null) return;
        if (selected.ContainerId.Length == 0)
        {
            string? action = selected.Artifact is not null || selected.Item is EquipmentData
                ? selected.IsEquipped ? "卸下" : "装备"
                : selected.Item is PortraitClothingData clothing && player.entity_data is HumanData human && clothing.can_be_worn_by(human)
                    ? selected.IsEquipped ? null : "穿上"
                    : selected.Item is { } item && _item_use_handler?.CanUse(player, item, selected.Count) == true ? "使用" : null;
            if (action is not null) { _volumeUse.Text = action; _volumeUse.Show(); }
        }
        if (!player.can_store_spatial_item(selected.InstanceId)) return;
        if (selected.ContainerId.Length > 0 || player.get_storage_bags().Count > 0)
        {
            _volumeMove.Text = selected.ContainerId.Length > 0 ? "取回随身行囊" : "转入储物袋 ▸";
            _volumeMove.Show();
        }
    }

    private void use_volume_selection()
    {
        if (_player is not { } player || _volumeSelected is not { ContainerId.Length: 0 } entry) return;
        bool changed = entry.Artifact is { } artifact
            ? entry.IsEquipped ? player.unequip_artifact(artifact.instance_id) : player.equip_artifact(artifact.instance_id)
            : entry.Item switch
            {
                PortraitClothingData => player.equip_inventory_item(entry.ItemPath, "portrait_clothing"),
                EquipmentData => entry.IsEquipped ? player.unequip_inventory_item(entry.ItemPath) : player.equip_inventory_item(entry.ItemPath),
                { } item when _item_use_handler?.CanUse(player, item, entry.Count) == true => _item_use_handler.TryUse(player, item, entry.Count),
                _ => false
            };
        if (changed && entry.Item is PortraitClothingData)
            GetTree().CallGroup("human_info_panel", "refresh_after_clothing_item");
        _volumeStatus.Text = changed ? "" : "当前无法执行";
    }

    private void move_volume_selection()
    {
        if (_player is not { } player || _volumeSelected is not { } entry) return;
        if (entry.ContainerId.Length > 0) { transfer_volume_entry(entry, ""); return; }
        var menu = new PopupMenu();
        var bags = player.get_storage_bags();
        for (int index = 0; index < bags.Count; index++)
            menu.AddItem($"{volume_bag_name(index + 1)}  {bags[index].UsedCapacity} / {bags[index].Capacity}", index);
        _volumeMove.AddChild(menu);
        menu.IdPressed += index => transfer_volume_entry(entry, bags[(int)index].InstanceId);
        menu.PopupHide += menu.QueueFree;
        var screen = _volumeMove.GetScreenPosition();
        menu.Position = (Vector2I)new Vector2(screen.X, screen.Y - 38 * bags.Count);
        menu.Popup();
    }

    private void transfer_volume_entry(SpatialInventoryEntry entry, string destination)
    {
        if (_player is not { } player) return;
        bool success = player.try_transfer_storage_item(entry.InstanceId, destination, out string reason);
        _volumeStatus.Text = success ? "已转移" : reason;
    }

    private void discard_volume_selection()
    {
        if (_volumeSelected is not { } entry || _player is not { } player) return;
        _volumeStatus.Text = player.try_discard_volume_entry(entry) ? "已丢弃" : "该物品当前无法丢弃";
    }
}


