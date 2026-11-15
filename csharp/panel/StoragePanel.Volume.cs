using Godot;

public partial class StoragePanel
{
    private GridContainer? _volumeCards;
    private ScrollContainer _volumeScroll = null!;
    private VBoxContainer _volumeContainers = null!;
    private Label _volumeTitle = null!, _volumeCapacity = null!, _volumeStatus = null!, _volumeEmpty = null!;
    private Button _volumeDiscard = null!, _volumeUse = null!, _volumeMove = null!;
    private PopupMenu? _volumeTransferMenu;
    private SpatialInventoryEntry? _volumeSelected;
    private static Texture2D? _volumeCardTexture;
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
        _canvas = new Control { Name = "InventoryCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass,
            Theme = CraftingPaperStyle.MakeTheme() };
        AddChild(_canvas);
        place(text("行囊", 34, Ink), _canvas, 34, 18, 320, 54);
        var back = new Button { Text = "返回", Name = "StorageReturn" };
        PaperButtonFeedback.Apply(back);
        _canvas.AddChild(back);
        CraftingPaperHeader.PlaceReturn(_canvas, back);
        CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += hide_panel;

        place(volume_rule(), _canvas, 34, 89, 1848, 1);
        place(volume_rule(), _canvas, 300, 116, 1, 892);
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
            PaperButtonFeedback.Apply(tab, CraftingPaperStyle.ButtonKind.Regular, 22);
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

        place(volume_rule(), _canvas, 326, 216, 1556, 1);
        _volumeScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        place(_volumeScroll, _canvas, 326, 232, 1556, 720);
        _volumeCards = new GridContainer { Columns = 7 };
        _volumeCards.AddThemeConstantOverride("h_separation", 12);
        _volumeCards.AddThemeConstantOverride("v_separation", 12);
        _volumeScroll.AddChild(_volumeCards);
        _volumeEmpty = text("", 22, CraftingPaperStyle.Muted);
        place(_volumeEmpty, _canvas, 350, 254, 400, 40);
        _volumeStatus = text("", 22, Ink);
        place(_volumeStatus, _canvas, 326, 979, 780, 50);
        _volumeUse = volume_action("", 1332, use_volume_selection);
        _volumeMove = volume_action("", 1514, move_volume_selection, 224);
        _volumeDiscard = volume_action("丢弃", 1758, discard_volume_selection);
    }

    private static ColorRect volume_rule() => new()
    {
        Color = new Color("b6a384") { A = .5f },
        MouseFilter = MouseFilterEnum.Ignore
    };

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
        string display_name(SpatialInventoryEntry entry)
        {
            if (entry.Item is not StorageBagItemData) return native_name(entry);
            int bagIndex = bags.ToList().FindIndex(bag => bag.InstanceId == entry.InstanceId);
            return bagIndex < 0 ? native_name(entry) : volume_bag_name(bagIndex + 1);
        }
        var shown = contents.Where(entry =>
            (_category_filter.Length == 0 || (entry.Artifact is not null || entry.Item is SectArtifactOfferData
                ? _category_filter is "器物" or ArtifactFilter
                : entry.Item is not null && category_of(entry.Item, entry.ItemPath) == _category_filter))
            && (display_name(entry).Contains(_search_text, StringComparison.OrdinalIgnoreCase)
                || native_name(entry).Contains(_search_text, StringComparison.OrdinalIgnoreCase))).ToList();
        _volumeEmpty.Text = _search_text.Length > 0 ? "未找到物品" : "暂无物品";
        _volumeEmpty.Visible = shown.Count == 0;
        _volumeSelected = shown.FirstOrDefault(entry => entry.InstanceId == _volumeSelected?.InstanceId);
        clear_volume_children(_volumeCards);
        foreach (var entry in shown)
        {
            var card = make_volume_tile(display_name(entry), entry.Count, entry.Artifact is { } artifact
                ? ForgedArtifactInventory.IconPath(artifact) : entry.Item?.icon_path ?? "", new Vector2(210, 202));
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
        var button = new Button { CustomMinimumSize = new(224, 58) };
        PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 22, _nativeContainerId == id);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        button.AddChild(row);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 14;
        row.OffsetRight = -14;
        var name = text(title, 22, Ink);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);
        row.AddChild(text($"{used} / {capacity}", 19, Ink));
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
        bool inventoryCard = size.Y >= 180;
        float iconSize = inventoryCard ? 112 : Mathf.Min(64, size.Y - 44);
        float iconTop = inventoryCard ? 22 : 9;
        place(image, tile, (size.X - iconSize) / 2, iconTop, iconSize, iconSize);
        var caption = CraftingPaperStyle.Text(volume_caption(name, count, size.X - 20), 22);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        caption.MouseFilter = MouseFilterEnum.Ignore;
        place(caption, tile, 10, size.Y - 44, size.X - 20, 30);
        return tile;
    }

    private static string volume_caption(string name, int count, float width)
    {
        string suffix = $" ×{count}";
        var font = YuxuLibraryArt.Font();
        if (font.GetStringSize(name + suffix, HorizontalAlignment.Left, -1, 22).X <= width)
            return name + suffix;

        string shortened = name;
        while (shortened.Length > 0 && font.GetStringSize(shortened + "…" + suffix, HorizontalAlignment.Left, -1, 22).X > width)
        {
            int[] starts = System.Globalization.StringInfo.ParseCombiningCharacters(shortened);
            shortened = shortened[..starts[^1]];
        }
        return shortened + "…" + suffix;
    }

    private static void style_volume_tile(Button tile, bool selected)
    {
        tile.AddThemeStyleboxOverride("normal", volume_tile_face(selected ? .86f : 1f));
        tile.AddThemeStyleboxOverride("hover", volume_tile_face(selected ? .84f : .95f));
        var pressed = volume_tile_face(.79f);
        if (pressed is StyleBoxTexture pressedPaper)
        {
            pressedPaper.ExpandMarginTop = -1;
            pressedPaper.ExpandMarginBottom = 1;
        }
        tile.AddThemeStyleboxOverride("pressed", pressed);
        tile.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBox volume_tile_face(float brightness)
    {
        if (_volumeCardTexture is null && YuxuLibraryArt.Texture("res://textures/ui/inventory_volume_20261115/item_card_square.png") is { } source)
        {
            using var image = source.GetImage();
            image.Resize(256, 256, Image.Interpolation.Lanczos);
            _volumeCardTexture = ImageTexture.CreateFromImage(image);
        }
        if (_volumeCardTexture is null)
            return new StyleBoxFlat { BgColor = new Color("f5ead5"), BorderColor = new Color("cab799"),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1 };
        return new StyleBoxTexture
        {
            Texture = _volumeCardTexture,
            TextureMarginLeft = 24, TextureMarginTop = 24, TextureMarginRight = 24, TextureMarginBottom = 24,
            ModulateColor = new Color(brightness, brightness, brightness)
        };
    }

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
        var menu = new PopupMenu { Theme = CraftingPaperStyle.MakeTheme() };
        _volumeTransferMenu = menu;
        var bags = player.get_storage_bags();
        for (int index = 0; index < bags.Count; index++)
            menu.AddItem($"{volume_bag_name(index + 1)}  {bags[index].UsedCapacity} / {bags[index].Capacity}", index);
        _volumeMove.AddChild(menu);
        menu.IdPressed += index => transfer_volume_entry(entry, bags[(int)index].InstanceId);
        menu.PopupHide += () =>
        {
            if (ReferenceEquals(_volumeTransferMenu, menu)) _volumeTransferMenu = null;
            menu.QueueFree();
        };
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


