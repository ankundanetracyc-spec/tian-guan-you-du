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
                ResourceLoader.Load<ItemData>(path) is not { } item || !GongfaAvailability.IsItemEnabled(item)) continue;

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
        var rows = Math.Clamp((_items.Count + 5) / 6, 1, 3);
        var selected = _items.Where(item => item.Selected).ToList();
        var hasStatus = selected.Count == 0 || required_new_slots(selected) > (_recipient?.get_inventory_free_slot_count() ?? 0);
        set_page_layout(new Vector2(1000, 176 + rows * 158 + (hasStatus ? 32 : 0)), false);

        var heading = text(_items.Count == 0 ? title : "获得物品", 34, Ink);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        place(heading, _page, 180, 23, 640, 46);

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
        place(scroll, _page, 30, 90, 940, rows * 158 - 10);
        _claimScroll = scroll;
        style_scrollbar(scroll.GetVScrollBar());
        var grid = new GridContainer
        {
            Name = "ClaimItemGrid",
            Columns = 6,
            CustomMinimumSize = new Vector2(918, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);

        foreach (var item in _items)
        {
            var card = build_item_card(item.Data, item.Count, item.Selected,
                148, 148, () =>
                {
                    item.Selected = !item.Selected;
                    build_claim_page("获得物品");
                });
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
        var required = required_new_slots(selected);
        var free = _recipient?.get_inventory_free_slot_count() ?? 0;
        var overflow = selected.Count > 0 && required > free;
        if (selected.Count == 0)
            status.Text = "请点选要领取的物品";
        else if (overflow)
            status.Text = $"行囊空间不足 · 还需腾出 {required - free} 格";

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

        var leftTitle = text($"我的行囊　{_recipient.get_inventory_slot_count()}/{_recipient.max_pack_slots}", 24, Ink);
        place(leftTitle, _page, 52, 118, 650, 38);
        var rightTitle = text($"待丢弃物品　{_discardEntries.Count} 格", 24, Ink);
        place(rightTitle, _page, 966, 118, 650, 38);
        place(rule(), _page, 52, 166, 654, 1);
        place(rule(), _page, 966, 166, 654, 1);

        var leftScroll = create_scroll("PackDiscardSourceScroll");
        place(leftScroll, _page, 52, 180, 654, 714);
        style_scrollbar(leftScroll.GetVScrollBar());
        var leftGrid = new GridContainer { Columns = 5, CustomMinimumSize = new Vector2(630, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftGrid.AddThemeConstantOverride("h_separation", 10);
        leftGrid.AddThemeConstantOverride("v_separation", 10);
        leftScroll.AddChild(leftGrid);
        foreach (var entry in _packEntries.ToArray())
        {
            var card = build_item_card(entry.Name, entry.Icon, entry.Count, false, 118, 148, entry.Item, () =>
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
        var rightGrid = new GridContainer { Columns = 5, CustomMinimumSize = new Vector2(630, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rightGrid.AddThemeConstantOverride("h_separation", 10);
        rightGrid.AddThemeConstantOverride("v_separation", 10);
        rightScroll.AddChild(rightGrid);
        foreach (var entry in _discardEntries.ToArray())
        {
            var card = build_item_card(entry.Name, entry.Icon, entry.Count, true, 118, 148, entry.Item, () =>
            {
                _discardEntries.Remove(entry);
                _packEntries.Add(entry);
                build_organize_page();
            });
            rightGrid.AddChild(card);
            attach_pack_hover(card, entry);
        }

        var freeAfterDiscard = (_recipient.get_inventory_free_slot_count() + _discardEntries.Count);
        var remaining = Math.Max(0, required_new_slots(_items.Where(item => item.Selected).ToList()) - freeAfterDiscard);
        var status = text(remaining > 0 ? $"整理后仍需腾出 {remaining} 格" : "点击左侧物品移入待丢弃，点击右侧物品可移回", 17, SecondaryInk);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.HorizontalAlignment = HorizontalAlignment.Center;
        place(status, _page, 746, 330, 180, 120);
        var direction = text("→", 38, SecondaryInk);
        direction.HorizontalAlignment = HorizontalAlignment.Center;
        place(direction, _page, 746, 230, 180, 70);
        var confirm = make_action_button(_page, "确认丢弃", new(744, 470), new(184, 54), true, discard_and_return_to_claim);
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
        if (_recipient is null) return;
        var selected = _items.Where(item => item.Selected).ToList();
        if (selected.Count == 0) return;

        var required = required_new_slots(selected);
        if (required > _recipient.get_inventory_free_slot_count())
        {
            build_claim_page("获得物品");
            return;
        }

        var added = new List<ClaimItem>();
        foreach (var item in selected)
        {
            if (!_recipient.add_item_to_inventory_silently(item.Path, item.Count))
            {
                // 预检使用与行囊相同的堆叠规则；遇到运行态数据变动时保留队列供玩家重试。
                GD.PushError($"领取物品失败，奖励仍保留在领取界面：{item.Path} ×{item.Count}");
                build_claim_page("获得物品");
                return;
            }
            added.Add(item);
        }
        foreach (var item in added) _recipient.publish_item_acquired_notice(item.Path, item.Count);

        var claimedEntries = collect_entries(selected);
        var abandonedEntries = collect_entries(_items.Where(item => !item.Selected));
        try { _claimedCallback?.Invoke(claimedEntries); }
        catch (Exception error) { GD.PushError($"领取后处理失败：{error}"); }
        try { _abandonedCallback?.Invoke(abandonedEntries); }
        catch (Exception error) { GD.PushError($"放弃未选物品处理失败：{error}"); }
        close_panel();
    }

    private int required_new_slots(IReadOnlyCollection<ClaimItem> selected)
    {
        if (_recipient is null || selected.Count == 0) return 0;
        var inventory = _recipient.get_inventory_snapshot();
        var required = 0;
        foreach (var group in selected.GroupBy(item => item.Path, StringComparer.Ordinal))
        {
            var item = group.First().Data;
            var count = group.Sum(value => value.Count);
            if (!item.can_stack)
            {
                required += count;
                continue;
            }
            var existingRows = inventory.Where(value => value.GetValueOrDefault("item_path", "").AsString() == group.Key).ToList();
            if (existingRows.Count == 0) required++;
        }
        return required;
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

    private Control build_item_card(string name, Texture2D? icon, int count, bool selected, float width, float height, ItemData? item, Action clicked)
    {
        var card = new Control
        {
            Name = "ItemCard",
            CustomMinimumSize = new Vector2(width, height),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        var frame = new InventoryPaperFrame { FillPaper = true };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(frame);

        var iconWidth = Math.Min(width - 24, 132);
        var iconRect = new TextureRect
        {
            // 行囊格宽约156，扣除左右12边距后图标区域约132；领取卡保持这份
            // 图标宽度，纵向按紧凑卡片可用空间收缩，绘制模式也与行囊相同。
            Position = new Vector2((width - iconWidth) * 0.5f, 10),
            Size = new Vector2(iconWidth, Math.Max(1, height - 72)),
            Texture = icon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconRect.Material = null;
        if (item is PillItemData pill) AlchemyPillVisual.Apply(iconRect, pill.efficacy_tier, pill.pill_id);
        card.AddChild(iconRect);
        var quantityText = !_organizing ? "" : item is PillItemData
            ? $" × {Math.Max(1, count)}"
            : count > 1 ? $" * {count}" : "";
        var displayName = text("", 20, Ink);
        displayName.Name = "ItemCaption";
        var captionFont = displayName.GetThemeFont("font");
        var captionSize = 20;
        var availableWidth = width - 14;
        var twoLines = quantityText.Length > 0 &&
            captionFont.GetStringSize(name + quantityText, HorizontalAlignment.Left, -1, captionSize).X > availableWidth;
        if (twoLines) captionSize = 18;
        // 放不下时数量换至名称下一行并完整保留，只有名称本身允许省略。
        while (captionSize > 12 && captionFont.GetStringSize(quantityText, HorizontalAlignment.Left, -1, captionSize).X > availableWidth)
            captionSize--;
        var shortenedName = name;
        while (shortenedName.Length > 0 &&
            captionFont.GetStringSize(shortenedName + (shortenedName == name ? "" : "…") + (twoLines ? "" : quantityText),
                HorizontalAlignment.Left, -1, captionSize).X > availableWidth)
        {
            var elements = System.Globalization.StringInfo.ParseCombiningCharacters(shortenedName);
            shortenedName = shortenedName[..elements[^1]];
        }
        displayName.Text = shortenedName + (shortenedName == name ? "" : "…") +
            (twoLines ? "\n" + quantityText.TrimStart() : quantityText);
        displayName.AddThemeFontSizeOverride("font_size", captionSize);
        displayName.Position = new Vector2(5, height - 59);
        displayName.Size = new Vector2(width - 10, 52);
        displayName.HorizontalAlignment = HorizontalAlignment.Center;
        displayName.AutowrapMode = TextServer.AutowrapMode.Off;
        displayName.MaxLinesVisible = 2;
        card.AddChild(displayName);

        // 选中状态复用行囊的玉色描边；不在原物品图上压复选标记。
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
        var subtitle = $"法宝 · {artifact.artifact_tier} · {(string.IsNullOrWhiteSpace(artifact.element) ? "五行未定" : artifact.element)}";
        set_card_hover(card, new HoverInfoManager.HoverInfoContent(entry.Name,
            $"耐久 {Math.Max(0, artifact.current_durability)}/{Math.Max(0, artifact.max_durability)}\n灵识 {Math.Max(0, artifact.spirit_cost)}",
            entry.Icon, subtitle));
    }

    private HoverInfoManager.HoverInfoContent item_hover(ItemData item, int count, IEnumerable<GDictionary>? entries = null)
    {
        var content = StoragePanel.ItemHover(item, count, item.ResourcePath, _recipient);
        var sourceNames = entries?.Select(value => value.GetValueOrDefault("source", "").AsString())
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList() ?? [];
        if (sourceNames.Count == 0) return content;
        var sections = (content.Sections ?? new[] { new HoverInfoManager.HoverInfoSection("", content.Body) }).ToList();
        sections.Add(new("来源", string.Join("、", sourceNames)));
        return HoverInfoManager.Structured(content.Title, content.Icon, content.Subtitle, sections.ToArray());
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
