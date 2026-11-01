using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>通用物品领取页。奖励先留在领取队列中，确认后才写入角色行囊。</summary>
public partial class ItemClaimPanel : CanvasLayer
{
    private const string FramePath = "res://textures/ui/inventory_20261012/item_claim_shell_v1.png";
    private static readonly Vector2 DesignSize = new(1672, 941);
    private static readonly Color Ink = new("303d38");
    private static readonly Color SecondaryInk = new("697267");
    private static readonly Color Jade = new("5e8476");
    private static readonly Color PaperLine = new("b6a27e");

    private Control? _canvas;
    private Control? _page;
    private TextureRect? _shell;
    private TextureRect? _organizerBackdrop;
    private Vector2 _layoutSize = DesignSize;
    private ScrollContainer? _claimScroll;
    private Character? _recipient;
    private readonly List<ClaimItem> _items = [];
    private readonly List<PackEntry> _packEntries = [];
    private readonly List<PackEntry> _discardEntries = [];
    private Action<GArray>? _claimedCallback;
    private Action<GArray>? _abandonedCallback;
    private bool _previousPause;
    private bool _presented;
    private bool _organizing;
    private int _restoreClaimScroll;
    private bool _claimCommitting;

    public event Action? Closed;

    private sealed class ClaimItem
    {
        public required string Path { get; init; }
        public required ItemData Data { get; init; }
        public required List<GDictionary> Entries { get; init; }
        public int Count { get; set; }
        public bool Selected { get; set; } = true;
    }

    private sealed class PackEntry
    {
        public bool IsArtifact { get; init; }
        public string Path { get; init; } = "";
        public string InstanceId { get; init; } = "";
        public string Name { get; init; } = "";
        public int Count { get; init; } = 1;
        public Texture2D? Icon { get; init; }
        public ItemData? Item { get; init; }
        public ArtifactInstanceData? Artifact { get; init; }
    }

    public override void _EnterTree()
    {
        Layer = 125;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        build_shell();
        fit_canvas();
        GetViewport().SizeChanged += fit_canvas;
    }

    public override void _ExitTree()
    {
        if (GetViewport() is { } viewport && GodotObject.IsInstanceValid(viewport))
            viewport.SizeChanged -= fit_canvas;
        HoverInfoManager.HideAll();
    }

    public bool Present(
        Character recipient,
        GArray rewards,
        string title = "获得物品",
        Action<GArray>? claimed = null,
        Action<GArray>? abandoned = null)
    {
        if (_presented || recipient is null || !GodotObject.IsInstanceValid(recipient)) return false;
        _recipient = recipient;
        _claimedCallback = claimed;
        _abandonedCallback = abandoned;
        if (!read_reward_entries(rewards) || _items.Count == 0) return false;

        _previousPause = GetTree().Paused;
        GetTree().Paused = true;
        _presented = true;
        build_claim_page(title);
        return true;
    }

    private bool read_reward_entries(GArray rewards)
    {
        var byPath = new System.Collections.Generic.Dictionary<string, ClaimItem>(StringComparer.Ordinal);
        foreach (var value in rewards)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var entry = value.AsGodotDictionary();
            var path = entry.GetValueOrDefault("item_path", "").AsString();
            var count = entry.GetValueOrDefault("count", 0).AsInt32();
            if (string.IsNullOrWhiteSpace(path) || count <= 0 ||
                ResourceLoader.Load<ItemData>(path) is not { } item || (!GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item))) continue;

            if (!byPath.TryGetValue(path, out var grouped))
            {
                grouped = new ClaimItem { Path = path, Data = item, Entries = [] };
                byPath.Add(path, grouped);
            }
            grouped.Count = (int)Math.Min(int.MaxValue, (long)grouped.Count + count);
            grouped.Entries.Add(entry);
        }

        _items.Clear();
        _items.AddRange(byPath.Values);
        return _items.Count > 0;
    }

    private void build_shell()
    {
        var shade = new ColorRect
        {
            Name = "ReceiptBackdropShade",
            Color = new Color(0.06f, 0.07f, 0.06f, 0.46f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        shade.GuiInput += on_backdrop_input;
        AddChild(shade);

        _organizerBackdrop = new TextureRect
        {
            Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"), Region = new Rect2(270, 0, 1402, 941), FilterClip = true },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _organizerBackdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_organizerBackdrop);

        _canvas = new Control { Name = "ReceiptCanvas", Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Pass };
        AddChild(_canvas);
        var shellTexture = ResourceLoader.Load<Texture2D>(FramePath)
            ?? ResourceLoader.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png");
        var shell = new TextureRect
        {
            Name = "PaperShell",
            // 素材四周自带透明留边，裁掉留边后纸框与控件安全区才一致。
            Texture = shellTexture is null ? null : new AtlasTexture
            {
                Atlas = shellTexture,
                Region = new Rect2(12, 60, 1628, 848),
                FilterClip = true
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(shell);
        _shell = shell;
        _page = new Control { Name = "ReceiptPage", Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Stop };
        _canvas.AddChild(_page);
    }

    private void fit_canvas()
    {
        if (_canvas is null) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = Mathf.Min(viewport.X / DesignSize.X, viewport.Y / DesignSize.Y);
        _canvas.Size = _layoutSize;
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (viewport - _layoutSize * scale) * 0.5f;
    }

    private void set_page_layout(Vector2 size, bool organizing)
    {
        _layoutSize = size;
        _organizing = organizing;
        if (_page is not null) _page.Size = size;
        if (_shell is not null) _shell.Visible = !organizing;
        if (_organizerBackdrop is not null) _organizerBackdrop.Visible = organizing;
        fit_canvas();
    }

    private void build_claim_page(string title)
    {
        if (_page is null) return;
        var scrollPosition = _claimScroll?.ScrollVertical ?? _restoreClaimScroll;
        _restoreClaimScroll = 0;
        clear_page();
        var rows = Math.Clamp(_items.Count, 1, 6);
        var selected = _items.Where(item => item.Selected).ToList();
        var hasStatus = selected.Count == 0 || !can_claim(selected);
        set_page_layout(new Vector2(1000, 176 + rows * 96 + (hasStatus ? 32 : 0)), false);

        var heading = text(_items.Count == 0 ? title : "获得物品", 34, Ink);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        place(heading, _page, 180, 23, 640, 46);

        var allSelected = _items.All(item => item.Selected);
        var selectAll = make_action_button(_page, allSelected ? "取消全选" : "全选", new(30, 28), new(122, 38), false, () =>
        {
            foreach (var item in _items) item.Selected = !allSelected;
            build_claim_page("获得物品");
        });
        selectAll.Name = "ClaimSelectAll";
        selectAll.AddThemeFontSizeOverride("font_size", 18);

        var totalKinds = text($"共 {_items.Count} 种", 18, SecondaryInk);
        totalKinds.HorizontalAlignment = HorizontalAlignment.Right;
        place(totalKinds, _page, 802, 38, 160, 30);
        place(rule(), _page, 30, 78, 940, 1);

        var scroll = new ScrollContainer
        {
            Name = "ClaimItemsScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        place(scroll, _page, 30, 90, 940, rows * 96 - 8);
        _claimScroll = scroll;
        style_scrollbar(scroll.GetVScrollBar());
        var grid = new GridContainer
        {
            Name = "ClaimItemGrid",
            Columns = 1,
            CustomMinimumSize = new Vector2(918, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(grid);

        foreach (var item in _items)
        {
            var sources = item.Entries.Select(entry => entry.GetValueOrDefault("source", "").AsString())
                .Where(source => !string.IsNullOrWhiteSpace(source)).Distinct().ToArray();
            var card = build_item_card(item.Data.entity_name, load_icon(item.Data.icon_path), item.Count, item.Selected,
                918, 88, item.Data, () =>
                {
                    item.Selected = !item.Selected;
                    build_claim_page("获得物品");
                }, sources.Length == 0 ? "" : "来源：" + string.Join("、", sources));
            grid.AddChild(card);
            set_card_hover(card, item_hover(item.Data, item.Count, item.Entries));
        }

        place(rule(), _page, 30, _layoutSize.Y - 78, 940, 1);
        update_claim_footer();
        if (scrollPosition > 0) CallDeferred(nameof(restore_claim_scroll), scrollPosition);
    }

    private void restore_claim_scroll(int value)
    {
        if (_claimScroll is not null && GodotObject.IsInstanceValid(_claimScroll))
            _claimScroll.ScrollVertical = value;
    }

    private void update_claim_footer()
    {
        if (_page is null) return;
        foreach (var name in new[] { "ClaimFooterStatus", "ClaimFooterClose", "ClaimFooterPrimary" })
            _page.GetNodeOrNull<Control>(name)?.QueueFree();

        var selected = _items.Where(item => item.Selected).ToList();
        var status = text("", 18, SecondaryInk);
        status.Name = "ClaimFooterStatus";
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.MaxLinesVisible = 2;
        status.HorizontalAlignment = HorizontalAlignment.Center;
        place(status, _page, 32, _layoutSize.Y - 105, 936, 28);
        var required = required_cells(selected);
        var free = _recipient?.get_inventory_free_slot_count() ?? 0;
        var overflow = selected.Count > 0 && !can_claim(selected);
        if (selected.Count == 0)
            status.Text = "请点选要领取的物品";
        else if (overflow)
            status.Text = required > free ? $"行囊空间不足 · 至少还需腾出 {required - free} 格" : "现有空位无法放下所选物品，请先整理行囊";

        var primary = make_action_button(_page, "领取", new(308, _layoutSize.Y - 65), new(180, 46), true,
            overflow ? open_organizer : claim_selected_items);
        primary.Name = "ClaimFooterPrimary";
        primary.Disabled = selected.Count == 0;
        var cancel = make_action_button(_page, "取消", new(512, _layoutSize.Y - 65), new(180, 46), false,
            () => close_and_abandon(collect_entries(_items)));
        cancel.Name = "ClaimFooterClose";
    }

    private void open_organizer()
    {
        if (_recipient is null) return;
        _restoreClaimScroll = _claimScroll?.ScrollVertical ?? 0;
        _organizing = true;
        _packEntries.Clear();
        _discardEntries.Clear();
        foreach (var value in _recipient.get_inventory_snapshot())
        {
            var path = value.GetValueOrDefault("item_path", "").AsString();
            if (string.IsNullOrWhiteSpace(path) || value.GetValueOrDefault("equipped", false).AsBool()) continue;
            if (ResourceLoader.Load<ItemData>(path) is not { } item) continue;
            var icon = load_icon(item.icon_path);
            _packEntries.Add(new PackEntry
            {
                Path = path, Name = item.entity_name,
                Count = Math.Max(1, value.GetValueOrDefault("count", 1).AsInt32()),
                Icon = icon, Item = item
            });
        }
        foreach (var artifact in _recipient.get_artifact_items())
        {
            if (artifact is null || _recipient.is_artifact_equipped(artifact.instance_id)) continue;
            var icon = load_icon(ForgedArtifactInventory.IconPath(artifact));
            _packEntries.Add(new PackEntry
            {
                IsArtifact = true, InstanceId = artifact.instance_id,
                Name = string.IsNullOrWhiteSpace(artifact.artifact_name) ? artifact.form?.form_name ?? "未命名法宝" : artifact.artifact_name,
                Icon = icon, Artifact = artifact
            });
        }
        build_organize_page();
    }

    private void build_organize_page()
    {
        if (_page is null || _recipient is null) return;
        clear_page();
        set_page_layout(DesignSize, true);
        var heading = text("整理行囊", 34, Ink);
        place(heading, _page, 34, 26, 600, 52);
        make_action_button(_page, "返回领取", new(1518, 26), new(120, 46), false, () => build_claim_page("获得物品"));

        var leftFrame = new InventoryPaperFrame();
        place(leftFrame, _page, 34, 104, 690, 811);
        var rightFrame = new InventoryPaperFrame();
        place(rightFrame, _page, 948, 104, 690, 811);

        var leftTitle = text($"我的行囊　{_recipient.get_inventory_occupied_cell_count()}/120 格", 24, Ink);
        place(leftTitle, _page, 52, 118, 650, 38);
        var rightTitle = text($"待丢弃物品　{_discardEntries.Sum(pack_cells)} 格", 24, Ink);
        place(rightTitle, _page, 966, 118, 650, 38);
        place(rule(), _page, 52, 166, 654, 1);
        place(rule(), _page, 966, 166, 654, 1);

        var leftScroll = create_scroll("PackDiscardSourceScroll");
        place(leftScroll, _page, 52, 180, 654, 714);
        style_scrollbar(leftScroll.GetVScrollBar());
        var leftGrid = new GridContainer { Name = "PackSourceList", Columns = 1, CustomMinimumSize = new Vector2(630, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        leftGrid.AddThemeConstantOverride("v_separation", 8);
        leftScroll.AddChild(leftGrid);
        foreach (var entry in _packEntries.ToArray())
        {
            var card = build_item_card(entry.Name, entry.Icon, entry.Count, false, 630, 88, entry.Item, () =>
            {
                _packEntries.Remove(entry);
                _discardEntries.Add(entry);
                build_organize_page();
            });
            leftGrid.AddChild(card);
            attach_pack_hover(card, entry);
        }

        var rightScroll = create_scroll("PendingDiscardItemsScroll");
        place(rightScroll, _page, 966, 180, 654, 714);
        style_scrollbar(rightScroll.GetVScrollBar());
        var rightGrid = new GridContainer { Name = "PendingDiscardList", Columns = 1, CustomMinimumSize = new Vector2(630, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        rightGrid.AddThemeConstantOverride("v_separation", 8);
        rightScroll.AddChild(rightGrid);
        foreach (var entry in _discardEntries.ToArray())
        {
            var card = build_item_card(entry.Name, entry.Icon, entry.Count, true, 630, 88, entry.Item, () =>
            {
                _discardEntries.Remove(entry);
                _packEntries.Add(entry);
                build_organize_page();
            });
            rightGrid.AddChild(card);
            attach_pack_hover(card, entry);
        }

        var selected = _items.Where(item => item.Selected).ToList();
        var freeAfterDiscard = _recipient.get_inventory_free_slot_count() + _discardEntries.Sum(pack_cells);
        var remaining = Math.Max(0, required_cells(selected) - freeAfterDiscard);
        var fitsAfterDiscard = can_claim_after_discard(selected);
        var status = text(!fitsAfterDiscard
            ? remaining > 0 ? $"整理后至少还需腾出 {remaining} 格" : "整理后空位形状仍无法放下所选物品"
            : "点击左侧物品移入待丢弃，点击右侧物品可移回", 17, SecondaryInk);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.HorizontalAlignment = HorizontalAlignment.Center;
        place(status, _page, 746, 330, 180, 120);
        var direction = text("→", 38, SecondaryInk);
        direction.HorizontalAlignment = HorizontalAlignment.Center;
        place(direction, _page, 746, 230, 180, 70);
        var confirm = make_action_button(_page, "确认丢弃", new(744, 470), new(184, 54), true, discard_and_return_to_claim);
        confirm.Name = "ClaimDiscardConfirm";
        confirm.Disabled = _discardEntries.Count == 0;
    }

    private void discard_and_return_to_claim()
    {
        if (_recipient is null) return;
        var failed = new List<PackEntry>();
        foreach (var entry in _discardEntries)
        {
            var removed = entry.IsArtifact
                ? _recipient.remove_artifact_from_inventory(entry.InstanceId)
                : _recipient.remove_item_from_inventory(entry.Path, entry.Count);
            if (!removed) failed.Add(entry);
        }
        if (failed.Count > 0)
        {
            _discardEntries.Clear();
            _discardEntries.AddRange(failed);
            GD.PushWarning($"行囊整理中有 {failed.Count} 件物品未能移除，领取时将重新检查空间。 ");
            build_organize_page();
            return;
        }
        _discardEntries.Clear();
        _packEntries.Clear();
        _organizing = false;
        build_claim_page("获得物品");
    }

    private void claim_selected_items()
    {
        if (_recipient is null || !_presented || _claimCommitting) return;
        var selected = _items.Where(item => item.Selected).ToList();
        if (selected.Count == 0) return;

        if (!can_claim(selected))
        {
            build_claim_page("获得物品");
            return;
        }

        _claimCommitting = true;
        try
        {
            // The queue is consumed only after one atomic spatial transaction succeeds.
            // Recheck inside the transaction: inventory may have changed since the last selection.
            if (!_recipient.try_exchange_inventory(new System.Collections.Generic.Dictionary<string, int>(), claim_batch(selected)))
            {
                build_claim_page("获得物品");
                return;
            }
            foreach (var item in selected) _recipient.publish_item_acquired_notice(item.Path, item.Count);

            var claimedEntries = collect_entries(selected);
            var abandonedEntries = collect_entries(_items.Where(item => !item.Selected));
            try { _claimedCallback?.Invoke(claimedEntries); }
            catch (Exception error) { GD.PushError($"领取后处理失败：{error}"); }
            try { _abandonedCallback?.Invoke(abandonedEntries); }
            catch (Exception error) { GD.PushError($"放弃未选物品处理失败：{error}"); }
            close_panel();
        }
        finally { _claimCommitting = false; }
    }

    private static System.Collections.Generic.Dictionary<string, int> claim_batch(IReadOnlyCollection<ClaimItem> selected)
    {
        var batch = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in selected.GroupBy(item => item.Path, StringComparer.Ordinal))
            batch[group.Key] = checked(group.Sum(item => item.Count));
        return batch;
    }

    private bool can_claim(IReadOnlyCollection<ClaimItem> selected) => _recipient?.can_add_inventory_batch(claim_batch(selected)) == true;
    private static int required_cells(IReadOnlyCollection<ClaimItem> selected) =>
        (int)Math.Min(int.MaxValue, selected.Sum(item => (long)item.Count * item.Data.get_inventory_cell_count()));
    private static int pack_cells(PackEntry entry)
    {
        if (entry.Item is not null) return (int)Math.Min(int.MaxValue, (long)entry.Count * entry.Item.get_inventory_cell_count());
        if (entry.Artifact is not null && ForgedArtifactInventory.TryRead(entry.Artifact, out var forged))
        { var size = ForgedArtifactPresentation.InventorySize(forged); return size.X * size.Y; }
        return 0;
    }
    private bool can_claim_after_discard(IReadOnlyCollection<ClaimItem> selected)
    {
        if (_recipient?.entity_data is not HumanData human) return false;
        var hypothetical = new Character { entity_data = human.Duplicate(true) as HumanData };
        try
        {
            foreach (var entry in _discardEntries)
                if (!(entry.IsArtifact ? hypothetical.remove_artifact_from_inventory(entry.InstanceId) : hypothetical.remove_item_from_inventory(entry.Path, entry.Count))) return false;
            return hypothetical.can_add_inventory_batch(claim_batch(selected));
        }
        finally { hypothetical.Free(); }
    }

    private static GArray collect_entries(IEnumerable<ClaimItem> items)
    {
        var result = new GArray();
        foreach (var item in items)
            foreach (var entry in item.Entries)
                result.Add(entry);
        return result;
    }

    private void close_and_abandon(GArray entries)
    {
        try { _abandonedCallback?.Invoke(entries); }
        catch (Exception error) { GD.PushError($"放弃未领取物品处理失败：{error}"); }
        close_panel();
    }

    private void close_panel()
    {
        if (!_presented) return;
        _presented = false;
        HoverInfoManager.HideAll();
        GetTree().Paused = _previousPause;
        Closed?.Invoke();
        QueueFree();
    }

    private void on_backdrop_input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            close_and_abandon(collect_entries(_items));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_presented || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_organizing) build_claim_page("获得物品");
        else close_and_abandon(collect_entries(_items));
        GetViewport().SetInputAsHandled();
    }

    private void clear_page()
    {
        if (_page is null) return;
        HoverInfoManager.HideAll();
        _claimScroll = null;
        foreach (var child in _page.GetChildren())
        {
            _page.RemoveChild(child);
            child.QueueFree();
        }
    }

    private Control build_item_card(ItemData item, int count, bool selected, float width, float height, Action clicked)
    {
        return build_item_card(item.entity_name, load_icon(item.icon_path), count, selected, width, height, item, clicked);
    }

    private Control build_item_card(string name, Texture2D? icon, int count, bool selected, float width, float height, ItemData? item, Action clicked, string context = "")
    {
        var card = new Control
        {
            Name = "ItemCard",
            CustomMinimumSize = new Vector2(width, height),
            Size = new Vector2(width, height),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        var frame = new InventoryPaperFrame { FillPaper = true };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(frame);

        var iconRect = new TextureRect
        {
            Name = "ItemIcon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = icon,
            Position = new Vector2(10, 8),
            Size = new Vector2(72, 72)
        };
        card.AddChild(iconRect);

        var titleWidth = Math.Max(80, width - 338);
        var displayName = text(name, 22, Ink);
        displayName.Name = "ItemCaption";
        displayName.Position = new Vector2(96, 11);
        displayName.Size = new Vector2(titleWidth, 32);
        card.AddChild(displayName);

        var metadata = item_row_metadata(item);
        if (!string.IsNullOrWhiteSpace(context)) metadata = string.IsNullOrWhiteSpace(metadata) ? context : metadata + " · " + context;
        var identity = text(metadata, 17, SecondaryInk);
        identity.Name = "ItemIdentity";
        identity.Position = new Vector2(96, 46);
        identity.Size = new Vector2(titleWidth, 26);
        card.AddChild(identity);

        var quantity = text($"× {Math.Max(1, count):N0}", 20, Ink);
        quantity.Name = "ItemQuantity";
        quantity.HorizontalAlignment = HorizontalAlignment.Right;
        quantity.Position = new Vector2(width - 226, 12);
        quantity.Size = new Vector2(140, 64);
        card.AddChild(quantity);

        if (_organizing)
        {
            var direction = text(selected ? "移回" : "移出", 19, Jade);
            direction.Name = "ItemRowAction";
            direction.HorizontalAlignment = HorizontalAlignment.Center;
            direction.Position = new Vector2(width - 78, 12);
            direction.Size = new Vector2(64, 64);
            card.AddChild(direction);
        }
        else
        {
            var selection = new InventoryPaperFrame
            {
                Name = "ItemSelectionBox", FillPaper = false, Selected = selected,
                Position = new Vector2(width - 49, 30), Size = new Vector2(28, 28)
            };
            card.AddChild(selection);
            var check = text(selected ? "✓" : "", 21, Jade);
            check.Name = "ItemSelectionMark";
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.Position = selection.Position;
            check.Size = selection.Size;
            card.AddChild(check);
        }

        // 选中描边与右侧勾选独立，不盖住物品图。
        frame.Selected = selected;
        frame.QueueRedraw();

        var hit = new Button { Name = "ItemHitTarget", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        hit.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        set_transparent_button(hit);
        hit.MouseEntered += () => { frame.Selected = true; frame.QueueRedraw(); };
        hit.MouseExited += () => { frame.Selected = selected; frame.QueueRedraw(); };
        hit.Pressed += clicked;
        card.AddChild(hit);
        return card;
    }

    private static string item_row_metadata(ItemData? item)
    {
        if (item is null) return "器物";
        if (item is PortraitClothingData clothing)
            return "衣着 · " + (clothing.portrait_gender switch { "male" => "男修", "female" => "女修", "both" => "通用", _ => "未标注" });
        var category = StoragePanel.category_of(item, item.ResourcePath);
        var grade = item.craft_data is MaterialCraftData material ? material.material_grade_name()
            : ManualItemPresentation.IsManual(item) ? "" : item.get_grade_string();
        return string.Join(" · ", new[] { category, grade }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void set_card_hover(Control card, HoverInfoManager.HoverInfoContent content)
    {
        // 顶层点击按钮接收鼠标事件，详情必须绑定在同一个控件上。
        HoverInfoManager.SetInfo(card.GetNodeOrNull<Button>("ItemHitTarget") ?? (Control)card, content);
    }

    private void attach_pack_hover(Control card, PackEntry entry)
    {
        if (entry.Item is { } item)
        {
            set_card_hover(card, item_hover(item, entry.Count));
            return;
        }
        if (entry.Artifact is not { } artifact) return;
        set_card_hover(card, EquipmentItemPresentation.Hover(artifact));
    }

    private HoverInfoManager.HoverInfoContent item_hover(ItemData item, int count, IEnumerable<GDictionary>? entries = null)
    {
        var content = StoragePanel.ItemHover(item, count, item.ResourcePath, _recipient);
        if (item is PortraitClothingData || MaterialItemPresentation.IsMaterial(item) || AlchemyPillVisual.IsPill(item) || ManualItemPresentation.IsManual(item) || EquipmentItemPresentation.IsEquipment(item)) return content;
        var sourceNames = entries?.Select(value => value.GetValueOrDefault("source", "").AsString())
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList() ?? [];
        if (sourceNames.Count == 0) return content;
        var sections = (content.Sections ?? new[] { new HoverInfoManager.HoverInfoSection("", content.Body) }).ToList();
        sections.Add(new("来源", string.Join("、", sourceNames)));
        return content with { Sections = sections.ToArray(), Body = string.Join("\n\n", sections.Select(section => string.IsNullOrWhiteSpace(section.Title) ? section.Body : section.Title + "\n" + section.Body)) };
    }

    private static ScrollContainer create_scroll(string name) => new()
    {
        Name = name,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        ClipContents = true,
        MouseFilter = Control.MouseFilterEnum.Stop
    };

    private static Texture2D? load_icon(string path) => string.IsNullOrWhiteSpace(path) ? null : ResourceLoader.Load<Texture2D>(path);

    private static Button make_action_button(Control parent, string label, Vector2 position, Vector2 size, bool primary, Action action)
    {
        var button = new Button
        {
            Text = label, Position = position, Size = size,
            ClipText = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("a69c84"));
        set_transparent_button(button);
        var frame = new InventoryPaperFrame { FillPaper = false, Gold = primary };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(frame);
        button.MouseEntered += () => { frame.Selected = true; frame.QueueRedraw(); };
        button.MouseExited += () => { frame.Selected = false; frame.QueueRedraw(); };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static void set_transparent_button(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }

    private static Label text(string value, int size, Color color)
    {
        var label = new Label
        {
            Text = value, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        label.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        label.AddThemeConstantOverride("outline_size", 0);
        return label;
    }

    private static ColorRect rule() => new()
    {
        Color = new Color(PaperLine, 0.70f),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private static void place(Control control, Control parent, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        parent.AddChild(control);
    }

    private static void style_scrollbar(VScrollBar scrollbar)
    {
        scrollbar.CustomMinimumSize = new Vector2(12, 0);
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color("e0d6bd"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        scrollbar.AddThemeStyleboxOverride("grabber", new StyleBoxFlat { BgColor = new Color("879184"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        scrollbar.AddThemeStyleboxOverride("grabber_highlight", new StyleBoxFlat { BgColor = Jade, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        scrollbar.AddThemeStyleboxOverride("grabber_pressed", new StyleBoxFlat { BgColor = new Color("4c6e62"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
    }
}
