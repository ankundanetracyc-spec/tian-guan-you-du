using Godot;

public partial class StoragePanel
{
    private string _nativeContainerId = "";
    private Dictionary<string, Rect2>? _nativeBodyRegions;
    private Label? _nativeSpaceTitle;
    private Label? _nativePageTitle;
    private Panel? _nativeIndexPanel, _nativeSpacePanel, _nativeRingFrame;
    private static readonly Vector2 RingViewportSize = new(1824, 864);
    private Button? _nativeBodyReturn;
    private ScrollContainer? _nativeSpaceScroll;
    private readonly List<Label> _nativeRegionNames = [];
    private readonly List<Button> _nativeStorageActions = [];

    private IReadOnlyList<SpatialInventoryRegion> native_current_regions()
    {
        if (_nativeContainerId.Length == 0) return Character.InventoryRegions;
        var ring = _player?.get_storage_rings().FirstOrDefault(r => r.InstanceId == _nativeContainerId);
        return ring is null ? [] : [new(Character.StorageRingRegion, "戒内空间", ring.Width, ring.Height)];
    }

    internal bool open_native_container(string id)
    {
        if (id.Length > 0 && _player?.get_storage_rings().Any(r => r.InstanceId == id) != true) return false;
        cancel_native_drag(); clear_active_info(); _nativeContainerId = id;
        if (_nativeStatus is not null) _nativeStatus.Text = "";
        if (_nativeSpaceScroll is not null) { _nativeSpaceScroll.ScrollHorizontal = 0; _nativeSpaceScroll.ScrollVertical = 0; }
        refresh_native_inventory();
        return true;
    }

    private void configure_native_container()
    {
        if (_nativeSurface is null || _nativeBodyRegions is null) return;
        var ring = _nativeContainerId.Length == 0 ? null : _player?.get_storage_rings().FirstOrDefault(r => r.InstanceId == _nativeContainerId);
        if (ring is null) _nativeContainerId = "";
        foreach (var label in _nativeRegionNames) { label.GetParent()?.RemoveChild(label); label.QueueFree(); }
        _nativeRegionNames.Clear(); _nativeRegions.Clear();
        if (ring is null)
        {
            layout_body_after_ring_modal();
            _nativeIndexPanel!.Show(); _nativeSpacePanel!.Show(); _nativeRingFrame!.Hide();
            _nativePageTitle!.Text = "行囊";
            _nativeSpaceScroll!.Position = new(486, 258); _nativeSpaceScroll.Size = new(1362, 710);
            _nativeStatus!.Position = new(486, 204);
            if (_nativeCount is not null) _nativeCount.Position = new(1458, 982);
            foreach (var pair in _nativeBodyRegions) _nativeRegions[pair.Key] = pair.Value;
            _nativeSurface.CustomMinimumSize = new(1350, 698);
            _nativeSpaceTitle!.Text = "行囊"; _nativeBodyReturn!.Hide();
        }
        else
        {
            var size = new Vector2(ring.Width, ring.Height) * NativeCell;
            var viewport = layout_ring_modal(ring, size);
            var origin = new Vector2(Mathf.Max(0, (viewport.X - size.X) / 2), Mathf.Max(0, (viewport.Y - size.Y) / 2));
            _nativeRegions[Character.StorageRingRegion] = new Rect2(origin, size);
            _nativeSurface.CustomMinimumSize = new(Mathf.Max(viewport.X, size.X), Mathf.Max(viewport.Y, size.Y));
            _nativeSpaceTitle!.Text = "行囊"; _nativeBodyReturn!.Show();
        }
        foreach (var region in native_current_regions().Where(_ => ring is null))
        {
            var rect = _nativeRegions[region.Id]; var label = text(region.Name, 25, new("52664a"));
            place(label, _nativeSurface, rect.Position.X, rect.Position.Y - 43, rect.Size.X, 36);
            _nativeRegionNames.Add(label);
        }
    }

    private SpatialInventoryEntry? native_slot_entry(StorageItemSlot slot) => slot.HasMeta("spatial_instance_id")
        ? native_entries().FirstOrDefault(e => e.InstanceId == slot.GetMeta("spatial_instance_id").AsString()) : null;

    private bool has_native_storage_actions(StorageItemSlot slot)
    {
        var entry = native_slot_entry(slot);
        return entry is not null && entry.Item is not StorageRingItemData && _player?.can_store_spatial_item(entry.InstanceId) == true
            && (entry.ContainerId.Length > 0 || _player.get_storage_rings().Count > 0);
    }

    private void clear_native_storage_actions()
    {
        foreach (var button in _nativeStorageActions) { button.GetParent()?.RemoveChild(button); button.QueueFree(); }
        _nativeStorageActions.Clear();
    }

    private void configure_native_storage_actions()
    {
        clear_native_storage_actions();
        if (_active_slot is null || _action_row is null || _player is null) return;
        var entry = native_slot_entry(_active_slot);
        if (entry is null || !has_native_storage_actions(_active_slot)) return;
        if (entry.ContainerId.Length > 0) _action_button?.Hide();
        void Add(string title, string container)
        {
            var button = paper_button(title, 23, true);
            button.CustomMinimumSize = new(0, 52); button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _action_row.AddChild(button); _action_row.MoveChild(button, _action_row.GetChildCount() - 2);
            button.Pressed += () =>
            {
                var id = entry.InstanceId; clear_active_info();
                bool success = _player.try_transfer_storage_item(id, container, out var reason);
                if (success) { _nativeLocated = id; open_native_container(container); }
                _nativeStatus!.Text = success ? "已转移：" + native_name(entry) : reason;
            };
            _nativeStorageActions.Add(button);
        }
        if (entry.ContainerId.Length > 0) Add("取回行囊", "");
        foreach (var ring in _player.get_storage_rings().Where(r => r.InstanceId != entry.ContainerId))
            Add("移入" + ring.DisplayName, ring.InstanceId);
        _cancel_button?.Show();
    }
}
