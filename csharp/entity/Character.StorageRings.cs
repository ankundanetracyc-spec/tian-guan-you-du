using Godot;
using System.Collections.Generic;

public sealed record StorageRingInfo(string InstanceId, string Name, string Quality, int Width, int Height, int OccupiedCells, int Ordinal = 0)
{
    public string DisplayName => $"{Name} · {Quality}" + (Ordinal > 0 ? $"（{Ordinal}）" : "");
}

public partial class Character
{
    public const string StorageRingRegion = "interior";

    private StorageRingContentsData? ring_contents(string id) => human?.storage_ring_contents?.FirstOrDefault(c => c is not null && c.ring_instance_id == id);
    public bool has_nonempty_storage_ring(string id) => ring_contents(id)?.items.Count > 0;
    private int get_nonempty_storage_ring_count(string path) => human?.inventory_placements.Count(p => p.item_path == path && has_nonempty_storage_ring(p.instance_id)) ?? 0;

    public IReadOnlyList<StorageRingInfo> get_storage_rings()
    {
        var result = new List<StorageRingInfo>();
        var physicalRings = get_spatial_inventory().Where(e => e.Item is StorageRingItemData).OrderBy(e => e.SlotIndex).ToArray();
        foreach (var ring in physicalRings)
        {
            var definition = (StorageRingItemData)ring.Item!;
            var contents = ring_contents(ring.InstanceId);
            int width = contents?.width ?? definition.storage_width, height = contents?.height ?? definition.storage_height;
            if (width <= 0 || height <= 0) continue;
            result.Add(new(ring.InstanceId, StorageRingItemData.ItemName, contents?.quality ?? definition.storage_quality,
                width, height, get_storage_ring_inventory(ring.InstanceId).Sum(e => e.Width * e.Height)));
        }
        for (int i = 0; i < result.Count; i++)
        {
            var sameGrade = result.Where(r => r.Quality == result[i].Quality).ToArray();
            if (sameGrade.Length > 1) result[i] = result[i] with { Ordinal = Array.FindIndex(sameGrade, r => r.InstanceId == result[i].InstanceId) + 1 };
        }
        return result;
    }

    public IReadOnlyList<SpatialInventoryEntry> get_storage_ring_inventory(string id)
    {
        var contents = ring_contents(id);
        if (contents is null) return [];
        return contents.items.Where(i => i is not null).Select((i, index) =>
        {
            var item = load_inventory_item(i.item_path);
            var size = item?.get_inventory_size() ?? artifact_inventory_size(i.artifact);
            if ((i.rotation & 1) != 0) size = new(size.Y, size.X);
            return new SpatialInventoryEntry(i.instance_id, i.item_path, i.artifact?.instance_id ?? "", StorageRingRegion,
                i.cell_x, i.cell_y, size.X, size.Y, i.rotation, index, item, i.artifact, false, id);
        }).ToList();
    }

    public IReadOnlyList<SpatialInventoryEntry> get_accessible_spatial_inventory()
    {
        var entries = get_spatial_inventory().ToList();
        var seen = entries.Select(e => e.InstanceId).ToHashSet(StringComparer.Ordinal);
        // Only direct, actually owned rings are readable. Never recurse through ring content.
        foreach (var ring in entries.Where(e => e.Item is StorageRingItemData).ToArray())
            entries.AddRange(get_storage_ring_inventory(ring.InstanceId).Where(e => seen.Add(e.InstanceId)));
        return entries;
    }

    public bool can_store_spatial_item(string id)
    {
        var entry = get_accessible_spatial_inventory().FirstOrDefault(e => e.InstanceId == id);
        return entry is not null && entry.Item is not StorageRingItemData && !storage_transfer_protected(entry);
    }

    private bool storage_transfer_protected(SpatialInventoryEntry entry) => entry.IsEquipped
        || (entry.ContainerId.Length == 0 && human is { is_in_breakthrough_retreat: true } h
            && h.breakthrough_pill_path == entry.ItemPath && get_inventory_total_count(entry.ItemPath) <= 1);

    private IReadOnlyList<SpatialInventoryRegion> storage_regions(string containerId)
    {
        if (containerId.Length == 0) return InventoryRegions;
        var ring = get_storage_rings().FirstOrDefault(r => r.InstanceId == containerId);
        return ring is null ? [] : [new(StorageRingRegion, "戒内空间", ring.Width, ring.Height)];
    }

    public bool can_move_stored_item(string instanceId, string containerId, string regionId, int x, int y, int rotation)
    {
        var all = get_accessible_spatial_inventory();
        var entry = all.FirstOrDefault(e => e.InstanceId == instanceId);
        if (entry is null || rotation is < 0 or > 3 || entry.Item?.inventory_can_rotate == false && rotation != 0) return false;
        if (containerId.Length > 0 && entry.Item is StorageRingItemData) return false;
        if (entry.ContainerId != containerId && storage_transfer_protected(entry)) return false;
        var region = storage_regions(containerId).FirstOrDefault(r => r.Id == regionId);
        var size = entry.Item?.get_inventory_size() ?? artifact_inventory_size(entry.Artifact);
        if ((rotation & 1) != 0) size = new(size.Y, size.X);
        if (region is null || x < 0 || y < 0 || size.X <= 0 || size.Y <= 0
            || (long)x + size.X > region.Width || (long)y + size.Y > region.Height) return false;
        return !all.Any(e => e.ContainerId == containerId && e.RegionId == regionId && e.InstanceId != instanceId
            && x < (long)e.X + e.Width && (long)x + size.X > e.X && y < (long)e.Y + e.Height && (long)y + size.Y > e.Y);
    }

    public bool try_transfer_storage_item(string instanceId, string containerId, out string reason)
    {
        reason = "空间不足，已保留原处";
        var all = get_accessible_spatial_inventory();
        var entry = all.FirstOrDefault(e => e.InstanceId == instanceId);
        if (entry is null) { reason = "物品已变化，请重新选择"; return false; }
        if (entry.ContainerId == containerId) return false;
        if (containerId.Length > 0 && entry.Item is StorageRingItemData) { reason = "储物戒指不能放入储物戒指"; return false; }
        if (storage_transfer_protected(entry)) { reason = "使用中或突破预留的物品不能收纳"; return false; }
        var regions = storage_regions(containerId);
        var occupied = all.Where(e => e.IsPlaced && e.ContainerId == containerId && e.InstanceId != instanceId).ToArray();
        foreach (int rotation in entry.Item?.inventory_can_rotate == false ? new[] { 0 } : new[] { entry.Rotation, (entry.Rotation + 1) % 4 })
        {
            var size = entry.Item?.get_inventory_size() ?? artifact_inventory_size(entry.Artifact);
            if ((rotation & 1) != 0) size = new(size.Y, size.X);
            foreach (var region in regions)
                for (int y = 0; y <= region.Height - size.Y; y++)
                    for (int x = 0; x <= region.Width - size.X; x++)
                        if (!occupied.Any(e => e.RegionId == region.Id && x < (long)e.X + e.Width && (long)x + size.X > e.X
                            && y < (long)e.Y + e.Height && (long)y + size.Y > e.Y))
                            return move_stored_item(instanceId, containerId, region.Id, x, y, rotation);
        }
        return false;
    }

    public bool move_stored_item(string instanceId, string containerId, string regionId, int x, int y, int rotation)
    {
        if (human is null || _spatialCommitting || !can_move_stored_item(instanceId, containerId, regionId, x, y, rotation)) return false;
        var entry = get_accessible_spatial_inventory().Single(e => e.InstanceId == instanceId);
        if (entry.ContainerId.Length == 0 && containerId.Length == 0) return move_spatial_item(instanceId, regionId, x, y, rotation);
        var ringInfo = containerId.Length > 0 ? get_storage_rings().Single(r => r.InstanceId == containerId) : null;
        var paths = human.inventory_item_paths.Duplicate(); var counts = human.inventory_item_counts.Duplicate();
        var slots = human.inventory_item_positions.Duplicate(); var artifacts = human.artifact_inventory.Duplicate();
        var artifactSlots = human.artifact_inventory_positions.Duplicate();
        var placements = human.inventory_placements.Duplicate(true);
        var contents = human.storage_ring_contents?.Duplicate(true) ?? [];

        if (entry.ContainerId.Length == 0)
        {
            if (entry.Artifact is not null)
            {
                int index = artifactSlots.IndexOf(entry.SlotIndex);
                if (index < 0) return false;
                artifacts.RemoveAt(index); artifactSlots.RemoveAt(index);
            }
            else
            {
                int index = slots.IndexOf(entry.SlotIndex);
                if (index < 0 || counts[index] != 1) return false;
                paths.RemoveAt(index); counts.RemoveAt(index); slots.RemoveAt(index);
            }
            int p = placements.ToList().FindIndex(i => i.instance_id == instanceId);
            if (p < 0) return false;
            placements.RemoveAt(p);
        }
        else
        {
            var source = contents.SingleOrDefault(c => c.ring_instance_id == entry.ContainerId);
            int index = source?.items.ToList().FindIndex(i => i.instance_id == instanceId) ?? -1;
            if (source is null || index < 0) return false;
            source.items.RemoveAt(index);
        }
        if (containerId.Length == 0)
        {
            int nextSlot = slots.Concat(artifactSlots).DefaultIfEmpty(-1).Max() + 1;
            if (entry.Artifact is not null) { artifacts.Add(entry.Artifact); artifactSlots.Add(nextSlot); }
            else { paths.Add(entry.ItemPath); counts.Add(1); slots.Add(nextSlot); }
            placements.Add(new InventoryPlacementData { instance_id = instanceId, row_slot = nextSlot,
                item_path = entry.ItemPath, artifact_id = entry.ArtifactId, region_id = regionId, cell_x = x, cell_y = y, rotation = rotation });
        }
        else
        {
            var target = contents.SingleOrDefault(c => c.ring_instance_id == containerId);
            if (target is null)
            {
                target = new StorageRingContentsData { ring_instance_id = containerId, quality = ringInfo!.Quality, width = ringInfo.Width, height = ringInfo.Height };
                contents.Add(target);
            }
            target.items.Add(new StorageRingStoredItemData { instance_id = instanceId, item_path = entry.ItemPath,
                artifact = entry.Artifact, cell_x = x, cell_y = y, rotation = rotation });
        }
        _spatialCommitting = true;
        try
        {
            human.inventory_item_paths = paths; human.inventory_item_counts = counts; human.inventory_item_positions = slots;
            human.artifact_inventory = artifacts; human.artifact_inventory_positions = artifactSlots;
            human.inventory_placements = placements; human.storage_ring_contents = contents;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        return true;
    }
}
