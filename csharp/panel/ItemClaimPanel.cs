using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>领取工作台：左侧待领取，右侧五区空间；确认时一次提交。</summary>
public partial class ItemClaimPanel : CanvasLayer
{
    private static readonly Vector2 DesignSize = new(1920, 1080);
    private static readonly Color Ink = new("303d38"), Muted = new("697267"), Jade = new("5e8476");
    private const float Cell = InventoryGridMetrics.DesignCell;
    private readonly Dictionary<string, Rect2> _regions = new()
    {
        ["chest"] = new(292, 45, 288, 192), ["sleeve-left"] = new(0, 269, 144, 192),
        ["sleeve-right"] = new(692, 269, 144, 192), ["waist"] = new(292, 489, 288, 192),
        ["back"] = new(920, 161, 288, 384)
    };
    private sealed class ClaimItem
    {
        public required string Path;
        public required ItemData Data;
        public readonly List<GDictionary> Entries = [];
        public int Count, Discarded;
        public bool Selected = true;
    }
    private readonly List<ClaimItem> _items = [];
    private readonly List<SpatialInventoryEntry> _baseline = [], _layout = [];
    private readonly Dictionary<string, ClaimItem> _incoming = [];
    private readonly Dictionary<string, StorageItemSlot> _slots = [];
    private Control _canvas = null!;
    private NativeInventorySurface _surface = null!;
    private VBoxContainer _rows = null!;
    private Label _status = null!, _count = null!;
    private ScrollContainer _scroll = null!;
    private ConfirmationDialog? _leaveDialog;
    private Character? _recipient;
    private Action<GArray>? _claimedCallback, _abandonedCallback;
    private bool _previousPause, _presented, _claimCommitting;
    public event Action? Closed;
    public override void _EnterTree() { Layer = 125; ProcessMode = ProcessModeEnum.Always; }
    public override void _Ready()
    {
        build_view(); fit_canvas(); GetViewport().SizeChanged += fit_canvas; GetWindow().FocusExited += cancel_drag;
    }
    public override void _ExitTree()
    {
        cancel_drag();
        if (GetViewport() is { } v) v.SizeChanged -= fit_canvas;
        if (GetWindow() is { } w) w.FocusExited -= cancel_drag;
        HoverInfoManager.HideAll();
    }
    public bool Present(Character recipient, GArray rewards, string title = "获得物品", Action<GArray>? claimed = null, Action<GArray>? abandoned = null)
    {
        if (_presented || recipient is null || !GodotObject.IsInstanceValid(recipient)) return false;
        foreach (var value in rewards)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var source = value.AsGodotDictionary(); var path = source.GetValueOrDefault("item_path", "").AsString();
            int count = source.GetValueOrDefault("count", 0).AsInt32();
            if (count <= 0 || string.IsNullOrWhiteSpace(path) || ResourceLoader.Load<ItemData>(path) is not { } data
                || !GongfaAvailability.IsItemEnabled(data) || !AlchemyProductCatalog.IsItemEnabled(data)) continue;
            var item = _items.FirstOrDefault(i => i.Path == path);
            if (item is null) { item = new ClaimItem { Path = path, Data = data }; _items.Add(item); }
            item.Count = (int)Math.Min(int.MaxValue, (long)item.Count + count); item.Entries.Add(source.Duplicate());
        }
        if (_items.Count == 0) return false;
        _recipient = recipient; _claimedCallback = claimed; _abandonedCallback = abandoned;
        _baseline.AddRange(recipient.get_spatial_inventory()); _layout.AddRange(_baseline);
        _previousPause = GetTree().Paused; GetTree().Paused = true; _presented = true; refresh(); return true;
    }
    private int placed(ClaimItem item) => _layout.Count(e => _incoming.GetValueOrDefault(e.InstanceId) == item);
    private int remaining(ClaimItem item) => Math.Max(0, item.Count - item.Discarded - placed(item));
    private static string entry_name(SpatialInventoryEntry e) => e.Item?.entity_name ?? e.Artifact?.artifact_name ?? "器物";
    private void fit_canvas()
    {
        cancel_drag(); var v = GetViewport().GetVisibleRect().Size;
        float scale = Mathf.Min(v.X / DesignSize.X, v.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (v - DesignSize * scale) * .5f;
    }
    private void refresh(string message = "")
    {
        cancel_drag(); HoverInfoManager.HideAll();
        foreach (var c in _rows.GetChildren()) { _rows.RemoveChild(c); c.QueueFree(); }
        foreach (var slot in _slots.Values) { _surface.RemoveChild(slot); slot.QueueFree(); } _slots.Clear();
        foreach (var item in _items.Where(i => remaining(i) > 0))
        {
            var row = new Button { Name = "ClaimRow", CustomMinimumSize = new(0, 92), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            style_button(row); row.AddThemeStyleboxOverride("normal", InventoryUiPaper.Style(item.Selected ? "index_row_selected.png" : "index_row_normal.png", 42, 10));
            _rows.AddChild(row); row.SetMeta("item_path", item.Path);
            put(new TextureRect { Texture = load_icon(item.Data.icon_path), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }, row, 12, 12, 58, 68);
            put(text(item.Data.entity_name, 22, Ink), row, 80, 23, 208, 46);
            var quantity = text("×" + remaining(item).ToString("N0"), 22, Jade); quantity.HorizontalAlignment = HorizontalAlignment.Right;
            put(quantity, row, 280, 23, 80, 46);
            HoverInfoManager.SetInfo(row, StoragePanel.ItemHover(item.Data, remaining(item), item.Path, _recipient));
            row.GuiInput += ev => { if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } m) { start_incoming_drag(item, row, m); row.AcceptEvent(); } };
        }
        if (!_items.Any(i => remaining(i) > 0)) _rows.AddChild(text("已全部处理", 22, Muted));
        var scene = GD.Load<PackedScene>("res://tscn/panel/StorageItemSlot.tscn");
        foreach (var entry in _layout.Where(e => e.IsPlaced))
        {
            var slot = scene.Instantiate<StorageItemSlot>(); _surface.AddChild(slot); slot.spatial_drag_enabled = true;
            if (entry.Artifact is not null) slot.set_artifact(entry.Artifact);
            else slot.set_item(entry.SlotIndex, entry.ItemPath, entry.Item!, 1);
            var original = (entry.Rotation & 1) == 0 ? entry.Size : new Vector2I(entry.Height, entry.Width);
            slot.configure_spatial_layout(new Vector2(entry.Width, entry.Height) * Cell, new Vector2(original.X, original.Y) * Cell, entry.Rotation);
            slot.Position = _regions[entry.RegionId].Position + new Vector2(entry.X, entry.Y) * Cell;
            slot.SetMeta("spatial_instance_id", entry.InstanceId); _slots[entry.InstanceId] = slot;
            var info = entry.Artifact is not null ? EquipmentItemPresentation.Hover(entry.Artifact) : StoragePanel.ItemHover(entry.Item!, 1, entry.ItemPath, _recipient);
            HoverInfoManager.SetInfo(slot, info with { Footprint = entry.Size });
            slot.GuiInput += ev => { if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } m) { start_pack_drag(entry, slot, m); slot.AcceptEvent(); } };
        }
        _status.Text = message; _count.Text = $"占用 {_layout.Sum(e => e.Width * e.Height)} / 120 格"; _surface.QueueRedraw();
    }
    internal bool fits(SpatialInventoryEntry item, string regionId, int x, int y, int rotation)
    {
        var region = Character.InventoryRegions.FirstOrDefault(r => r.Id == regionId);
        if (region is null || rotation is < 0 or > 3 || item.Item?.inventory_can_rotate == false && rotation != 0) return false;
        var size = rotated_size(item, rotation);
        return x >= 0 && y >= 0 && x + size.X <= region.Width && y + size.Y <= region.Height
            && !_layout.Any(e => e.InstanceId != item.InstanceId && e.RegionId == regionId
                && x < e.X + e.Width && x + size.X > e.X && y < e.Y + e.Height && y + size.Y > e.Y);
    }
    private static Vector2I rotated_size(SpatialInventoryEntry item, int rotation)
    { var size = (item.Rotation & 1) == 0 ? item.Size : new Vector2I(item.Height, item.Width); return (rotation & 1) == 0 ? size : new(size.Y, size.X); }
    private SpatialInventoryEntry incoming_entry(ClaimItem item) => new(Guid.NewGuid().ToString("N"), item.Path, "", "", -1, -1,
        item.Data.get_inventory_size().X, item.Data.get_inventory_size().Y, 0, -1, item.Data, null, false);
    private bool place_entry(SpatialInventoryEntry entry, ClaimItem? incoming, string region, int x, int y, int rotation)
    {
        if (!fits(entry, region, x, y, rotation) || incoming is not null && remaining(incoming) <= 0) return false;
        var size = rotated_size(entry, rotation); _layout.RemoveAll(e => e.InstanceId == entry.InstanceId);
        _layout.Add(entry with { RegionId = region, X = x, Y = y, Width = size.X, Height = size.Y, Rotation = rotation });
        if (incoming is not null) _incoming[entry.InstanceId] = incoming; return true;
    }
    private void auto_place()
    {
        if (!_presented || _claimCommitting) return;
        var selected = _items.Where(i => i.Selected && remaining(i) > 0).OrderByDescending(i => i.Data.get_inventory_cell_count()).ToList();
        var before = _layout.ToList(); var oldIds = _incoming.Keys.ToHashSet();
        foreach (var item in selected)
        {
            int count = remaining(item);
            if (count > Character.InventorySlotCapacity) { rollback(); return; }
            for (int n = 0; n < count; n++)
            {
                var entry = incoming_entry(item); bool done = false;
                foreach (var rotation in item.Data.inventory_can_rotate ? new[] { 0, 1 } : new[] { 0 })
                {
                    foreach (var r in Character.InventoryRegions)
                    {
                        for (int y = 0; y < r.Height && !done; y++) for (int x = 0; x < r.Width && !done; x++) done = place_entry(entry, item, r.Id, x, y, rotation);
                        if (done) break;
                    }
                    if (done) break;
                }
                if (!done) { rollback(); return; }
            }
        }
        refresh(selected.Count == 0 ? "请点选要自动放入的物品" : "已预览放入，确认领取后生效");
        void rollback()
        {
            _layout.Clear(); _layout.AddRange(before);
            foreach (var id in _incoming.Keys.Where(id => !oldIds.Contains(id)).ToArray()) _incoming.Remove(id);
            refresh("现有空位无法放下所选物品，请调整摆放或减少选择");
        }
    }
    private bool discard_entry(SpatialInventoryEntry entry)
    {
        if (entry.IsEquipped || _recipient?.has_nonempty_storage_ring(entry.InstanceId) == true) return false;
        if (_recipient?.entity_data is HumanData h && h.is_in_breakthrough_retreat && h.breakthrough_pill_path == entry.ItemPath
            && _layout.Count(e => e.ItemPath == entry.ItemPath && !_incoming.ContainsKey(e.InstanceId)) <= 1) return false;
        if (_layout.RemoveAll(e => e.InstanceId == entry.InstanceId) == 0) return false;
        if (_incoming.Remove(entry.InstanceId, out var item)) item.Discarded++; return true;
    }
    private void request_finish()
    {
        if (!_presented || _claimCommitting || _drag is not null) return;
        if (!_items.Any(i => remaining(i) > 0)) { commit_receipt(); return; }
        if (_leaveDialog is not null && GodotObject.IsInstanceValid(_leaveDialog)) { _leaveDialog.PopupCentered(); return; }
        _leaveDialog = new ConfirmationDialog { Title = "放弃剩余物品", DialogText = "还有物品未领取，离开后将放弃。是否结束领取？", ProcessMode = ProcessModeEnum.Always };
        AddChild(_leaveDialog); _leaveDialog.GetOkButton().Text = "结束领取"; _leaveDialog.GetCancelButton().Text = "继续整理";
        _leaveDialog.Confirmed += commit_receipt; _leaveDialog.PopupCentered(new Vector2I(580, 160));
    }
    private GArray callback_entries(bool claimed)
    {
        var result = new GArray();
        foreach (var item in _items)
        {
            int takeClaimed = placed(item);
            foreach (var source in item.Entries)
            {
                int total = source.GetValueOrDefault("count", 0).AsInt32(); int take = Math.Min(takeClaimed, total); takeClaimed -= take;
                int count = claimed ? take : total - take;
                if (count <= 0) continue;
                var copy = source.Duplicate(); copy["count"] = count; result.Add(copy);
            }
        }
        return result;
    }
    private void commit_receipt()
    {
        if (!_presented || _claimCommitting || _recipient is null) return;
        _claimCommitting = true;
        try
        {
            var claimed = callback_entries(true); var abandoned = callback_entries(false);
            if (!_recipient.try_commit_claim_inventory(_baseline, _layout, _items.ToDictionary(i => i.Path, i => i.Count)))
            {
                _baseline.Clear(); _baseline.AddRange(_recipient.get_spatial_inventory());
                _layout.Clear(); _layout.AddRange(_baseline); _incoming.Clear(); foreach (var item in _items) item.Discarded = 0;
                refresh("行囊已变化，已重新读取；本次领取与丢弃均未提交"); return;
            }
            _presented = false;
            foreach (var item in _items) _recipient.publish_item_acquired_notice(item.Path, placed(item));
            try { _claimedCallback?.Invoke(claimed); } catch (Exception e) { GD.PushError($"领取后处理失败：{e}"); }
            try { _abandonedCallback?.Invoke(abandoned); } catch (Exception e) { GD.PushError($"放弃后处理失败：{e}"); }
            HoverInfoManager.HideAll(); GetTree().Paused = _previousPause; Closed?.Invoke(); QueueFree();
        }
        finally { _claimCommitting = false; }
    }
    // Existing integration probes use the old atomic auto-claim entry point.
    private void claim_selected_items()
    { if (!_presented || _claimCommitting) return; auto_place(); if (_items.Where(i => i.Selected).All(i => remaining(i) == 0)) commit_receipt(); }
}
