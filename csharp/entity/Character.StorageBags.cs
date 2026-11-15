using Godot;
using System.Collections.Generic;

public sealed record StorageBagInfo(string InstanceId, string Name, string Quality, int Width, int Height, int UsedCapacity, int Ordinal = 0)
{
    public int Capacity => InventoryCapacityRules.Capacity(Width, Height);
    public int OccupiedCells => UsedCapacity; // Kept as a source-compatible alias for older callers.
    public string DisplayName => $"{Name} · {Quality}" + (Ordinal > 0 ? $"（{Ordinal}）" : "");
}

public partial class Character
{
    public const string StorageBagRegion = "interior";

    private StorageBagContentsData? bag_contents(string id) => human?.storage_bag_contents?.FirstOrDefault(c => c is not null && c.bag_instance_id == id);
    public bool has_nonempty_storage_bag(string id) => bag_contents(id)?.items.Count > 0;
    private int get_nonempty_storage_bag_count(string path) => human?.inventory_placements.Count(p => p.item_path == path && has_nonempty_storage_bag(p.instance_id)) ?? 0;

    public IReadOnlyList<StorageBagInfo> get_storage_bags()
    {
        var result = new List<StorageBagInfo>();
        var physicalBags = get_spatial_inventory().Where(e => e.Item is StorageBagItemData).OrderBy(e => e.SlotIndex).ToArray();
        foreach (var bag in physicalBags)
        {
            var definition = (StorageBagItemData)bag.Item!;
            var contents = bag_contents(bag.InstanceId);
            int width = contents?.width ?? definition.storage_width, height = contents?.height ?? definition.storage_height;
            if (width <= 0 || height <= 0) continue;
            result.Add(new(bag.InstanceId, StorageBagItemData.ItemName, contents?.quality ?? definition.storage_quality,
                width, height, get_storage_bag_inventory(bag.InstanceId).Sum(e => e.CapacityUsed)));
        }
        for (int i = 0; i < result.Count; i++)
        {
            var sameGrade = result.Where(r => r.Quality == result[i].Quality).ToArray();
            if (sameGrade.Length > 1) result[i] = result[i] with { Ordinal = Array.FindIndex(sameGrade, r => r.InstanceId == result[i].InstanceId) + 1 };
        }
        return result;
    }

    public IReadOnlyList<SpatialInventoryEntry> get_storage_bag_inventory(string id)
    {
        var contents = bag_contents(id);
        if (contents is null || contents.width <= 0 || contents.height <= 0) return [];
        var pending = contents.items.Where(i => i is not null).Select((i, index) =>
        {
            var item = load_inventory_item(i.item_path);
            int volume = item is not null ? InventoryCapacityRules.ItemVolume(item) : artifact_inventory_volume(i.artifact);
            int rotation = ((i.rotation % 4) + 4) % 4;
            if (item?.inventory_can_rotate == false) rotation = 0;
            var size = stored_display_size(item, i.artifact, rotation);
            return new SpatialInventoryEntry(i.instance_id, i.item_path, i.artifact?.instance_id ?? "", StorageBagRegion,
                i.cell_x, i.cell_y, size.X, size.Y, rotation, index, item, i.artifact, false, id, i.count, volume);
        }).ToList();
        var placed = new List<SpatialInventoryEntry>();
        foreach (var entry in pending)
        {
            if (display_fits(contents.width, contents.height, entry.X, entry.Y, entry.Size, entry.InstanceId, placed))
            { placed.Add(entry); continue; }
            bool found = false;
            var turns = entry.Item?.inventory_can_rotate == false ? new[] { 0 } : new[] { entry.Rotation, (entry.Rotation + 1) % 4 };
            foreach (int turn in turns)
            {
                var size = stored_display_size(entry.Item, entry.Artifact, turn);
                for (int y = 0; y <= contents.height - size.Y && !found; y++)
                    for (int x = 0; x <= contents.width - size.X && !found; x++)
                        if (display_fits(contents.width, contents.height, x, y, size, entry.InstanceId, placed))
                        { placed.Add(entry with { RegionId = StorageBagRegion, X = x, Y = y, Width = size.X, Height = size.Y, Rotation = turn }); found = true; }
                if (found) break;
            }
            if (!found) placed.Add(entry with { RegionId = "", X = -1, Y = -1, Rotation = 0 });
        }
        return placed;
    }

    public IReadOnlyList<SpatialInventoryEntry> get_accessible_spatial_inventory()
    {
        var entries = get_spatial_inventory().ToList();
        var seen = entries.Select(e => e.InstanceId).ToHashSet(StringComparer.Ordinal);
        // Only directly owned bags are readable. Never recurse through bag content.
        foreach (var bag in entries.Where(e => e.Item is StorageBagItemData).ToArray())
            entries.AddRange(get_storage_bag_inventory(bag.InstanceId).Where(e => seen.Add(e.InstanceId)));
        return entries;
    }

    public bool can_store_spatial_item(string id)
    {
        var entry = get_accessible_spatial_inventory().FirstOrDefault(e => e.InstanceId == id);
        return entry is not null && entry.Item is not StorageBagItemData && !storage_transfer_protected(entry);
    }

    /// <summary>Remove selected items from one directly-owned bag as part of a caller-owned transaction.</summary>
    public bool try_remove_storage_items(string containerId, IReadOnlyCollection<string> instanceIds, Func<bool> beforeCommit)
    {
        if (human is null || string.IsNullOrWhiteSpace(containerId) || instanceIds.Count == 0) return false;
        var requested = instanceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (requested.Count != instanceIds.Count) return false;
        var entries = get_accessible_spatial_inventory().Where(e => requested.Contains(e.InstanceId)).ToArray();
        if (entries.Length != requested.Count || entries.Any(e => e.ContainerId != containerId || e.Item is null
            || e.Item is StorageBagItemData || storage_transfer_protected(e))) return false;

        var contents = human.storage_bag_contents?.Duplicate(true) ?? [];
        foreach (var entry in entries)
        {
            var bag = contents.SingleOrDefault(c => c.bag_instance_id == containerId);
            var index = bag?.items.ToList().FindIndex(item => item.instance_id == entry.InstanceId) ?? -1;
            if (bag is null || index < 0) return false;
            bag.items.RemoveAt(index);
        }
        if (!beforeCommit()) return false;
        human.storage_bag_contents = contents;
        notify_inventory_changed();
        return true;
    }

    internal bool can_discard_volume_entry(SpatialInventoryEntry entry) =>
        !storage_transfer_protected(entry) && !has_nonempty_storage_bag(entry.InstanceId);

    internal bool try_discard_volume_entry(SpatialInventoryEntry selected)
    {
        if (human is null || _spatialCommitting) return false;
        var entry = get_accessible_spatial_inventory().FirstOrDefault(current =>
            current.InstanceId == selected.InstanceId && current.ContainerId == selected.ContainerId
            && current.Count == selected.Count);
        if (entry is null || !can_discard_volume_entry(entry)) return false;

        if (entry.ContainerId.Length > 0)
        {
            if (entry.Item is not null)
                return try_remove_storage_items(entry.ContainerId, [entry.InstanceId], () => true);
            if (entry.Artifact is null || bag_contents(entry.ContainerId) is not { } bag) return false;
            int index = bag.items.ToList().FindIndex(item => item.instance_id == entry.InstanceId);
            if (index < 0) return false;
            bag.items.RemoveAt(index);
        }
        else if (entry.Artifact is not null)
        {
            return remove_artifact_from_inventory(entry.ArtifactId);
        }
        else
        {
            int index = human.inventory_item_positions.IndexOf(entry.SlotIndex);
            if (index < 0 || human.inventory_item_paths[index] != entry.ItemPath || human.inventory_item_counts[index] != entry.Count)
                return false;
            human.inventory_item_paths.RemoveAt(index);
            human.inventory_item_counts.RemoveAt(index);
            human.inventory_item_positions.RemoveAt(index);
            int placement = human.inventory_placements.ToList().FindIndex(item => item.instance_id == entry.InstanceId);
            if (placement >= 0) human.inventory_placements.RemoveAt(placement);
        }

        notify_inventory_changed();
        return true;
    }

    private bool storage_transfer_protected(SpatialInventoryEntry entry) => entry.IsEquipped || entry.Artifact?.travel_in_use == true
        || (entry.ContainerId.Length == 0 && human is { is_in_breakthrough_retreat: true } h
            && h.breakthrough_pill_path == entry.ItemPath && get_inventory_total_count(entry.ItemPath) - entry.Count <= 1);

    private IReadOnlyList<SpatialInventoryRegion> storage_regions(string containerId)
    {
        if (containerId.Length == 0) return InventoryRegions;
        var bag = get_storage_bags().FirstOrDefault(r => r.InstanceId == containerId);
        return bag is null ? [] : [new(StorageBagRegion, "袋内空间", bag.Width, bag.Height)];
    }

    private static Vector2I stored_display_size(ItemData? item, ArtifactInstanceData? artifact, int rotation)
    {
        Vector2I size = item?.get_inventory_size() ?? (artifact is not null && ForgedArtifactInventory.TryRead(artifact, out var forged)
            ? ForgedArtifactPresentation.InventorySize(forged) : Vector2I.Zero);
        return rotation % 2 == 0 ? size : new Vector2I(size.Y, size.X);
    }

    private static bool display_fits(int width, int height, int x, int y, Vector2I size, string except,
        IEnumerable<SpatialInventoryEntry> occupied)
    {
        if (x < 0 || y < 0 || size.X <= 0 || size.Y <= 0 || (long)x + size.X > width || (long)y + size.Y > height) return false;
        return !occupied.Any(other => other.InstanceId != except && other.IsPlaced
            && x < (long)other.X + other.Width && (long)x + size.X > other.X
            && y < (long)other.Y + other.Height && (long)y + size.Y > other.Y);
    }

    private (string Region, int X, int Y, int Rotation) find_display_position(SpatialInventoryEntry entry,
        string containerId, string preferredRegion, IEnumerable<SpatialInventoryEntry> occupied)
    {
        var regions = storage_regions(containerId);
        var turns = entry.Item?.inventory_can_rotate == false ? new[] { 0 } : new[] { entry.Rotation, (entry.Rotation + 1) % 4 };
        foreach (int turn in turns)
            foreach (var region in regions.OrderBy(r => r.Id == preferredRegion ? 0 : 1))
            {
                var size = stored_display_size(entry.Item, entry.Artifact, turn);
                for (int y = 0; y <= region.Height - size.Y; y++)
                    for (int x = 0; x <= region.Width - size.X; x++)
                        if (display_fits(region.Width, region.Height, x, y, size, entry.InstanceId,
                            occupied.Where(other => other.ContainerId == containerId && other.RegionId == region.Id)))
                            return (region.Id, x, y, turn);
            }
        return ("", -1, -1, 0);
    }

    public bool can_move_stored_item(string instanceId, string containerId, string regionId, int x, int y, int rotation)
    {
        var all = get_accessible_spatial_inventory();
        var entry = all.FirstOrDefault(e => e.InstanceId == instanceId);
        if (entry is null || rotation is < 0 or > 3) return false;
        if (containerId.Length > 0 && entry.Item is StorageBagItemData) return false;
        if (entry.ContainerId != containerId && storage_transfer_protected(entry)) return false;
        var region = storage_regions(containerId).FirstOrDefault(r => r.Id == regionId);
        if (region is null) return false;
        if (entry.Item?.inventory_can_rotate == false && rotation % 2 != 0) return false;
        if (entry.ContainerId == containerId)
            return display_fits(region.Width, region.Height, x, y, stored_display_size(entry.Item, entry.Artifact, rotation),
                instanceId, all.Where(e => e.ContainerId == containerId && e.RegionId == regionId));
        int targetCapacity = containerId.Length == 0 ? InventoryCapacity
            : get_storage_bags().FirstOrDefault(b => b.InstanceId == containerId)?.Capacity ?? 0;
        int used = all.Where(e => e.ContainerId == containerId && e.InstanceId != instanceId).Sum(e => e.CapacityUsed);
        return targetCapacity > 0 && (long)used + entry.CapacityUsed <= targetCapacity;
    }

    public bool try_transfer_storage_item(string instanceId, string containerId, out string reason)
    {
        reason = "容量不足，已保留原处";
        var all = get_accessible_spatial_inventory();
        var entry = all.FirstOrDefault(e => e.InstanceId == instanceId);
        if (entry is null) { reason = "物品已变化，请重新选择"; return false; }
        if (entry.ContainerId == containerId) return false;
        if (containerId.Length > 0 && entry.Item is StorageBagItemData) { reason = "储物袋不能放入储物袋"; return false; }
        if (storage_transfer_protected(entry)) { reason = "使用中或突破预留的物品不能收纳"; return false; }
        var region = storage_regions(containerId).FirstOrDefault();
        if (region is null) { reason = "目标容量未配置，已保留原处"; return false; }
        if (!can_move_stored_item(instanceId, containerId, region.Id, -1, -1, 0)) return false;
        return move_stored_item(instanceId, containerId, region.Id, -1, -1, 0);
    }

    public bool move_stored_item(string instanceId, string containerId, string regionId, int x, int y, int rotation)
    {
        if (human is null || _spatialCommitting || !can_move_stored_item(instanceId, containerId, regionId, x, y, rotation)) return false;
        var all = get_accessible_spatial_inventory();
        var entry = all.Single(e => e.InstanceId == instanceId);
        if (entry.ContainerId.Length == 0 && containerId.Length == 0) return move_spatial_item(instanceId, regionId, x, y, rotation);
        var bagInfo = containerId.Length > 0 ? get_storage_bags().Single(r => r.InstanceId == containerId) : null;
        var paths = human.inventory_item_paths.Duplicate(); var counts = human.inventory_item_counts.Duplicate();
        var slots = human.inventory_item_positions.Duplicate(); var artifacts = human.artifact_inventory.Duplicate();
        var artifactSlots = human.artifact_inventory_positions.Duplicate();
        var placements = human.inventory_placements.Duplicate(true);
        var contents = human.storage_bag_contents?.Duplicate(true) ?? [];

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
                if (index < 0 || counts[index] != entry.Count || (entry.Item?.can_stack != true && counts[index] != 1)) return false;
                paths.RemoveAt(index); counts.RemoveAt(index); slots.RemoveAt(index);
            }
            int p = placements.ToList().FindIndex(i => i.instance_id == instanceId);
            if (p < 0) return false;
            placements.RemoveAt(p);
        }
        else
        {
            var source = contents.SingleOrDefault(c => c.bag_instance_id == entry.ContainerId);
            int index = source?.items.ToList().FindIndex(i => i.instance_id == instanceId) ?? -1;
            if (source is null || index < 0) return false;
            source.items.RemoveAt(index);
        }
        if (containerId.Length == 0)
        {
            int nextSlot = slots.Concat(artifactSlots).DefaultIfEmpty(-1).Max() + 1;
            if (entry.Artifact is not null) { artifacts.Add(entry.Artifact); artifactSlots.Add(nextSlot); }
            else { paths.Add(entry.ItemPath); counts.Add(entry.Count); slots.Add(nextSlot); }
            placements.Add(new InventoryPlacementData { instance_id = instanceId, row_slot = nextSlot,
                item_path = entry.ItemPath, artifact_id = entry.ArtifactId, region_id = regionId, cell_x = x, cell_y = y, rotation = rotation });
        }
        else
        {
            var target = contents.SingleOrDefault(c => c.bag_instance_id == containerId);
            if (target is null)
            {
                target = new StorageBagContentsData { bag_instance_id = containerId, quality = bagInfo!.Quality, width = bagInfo.Width, height = bagInfo.Height };
                contents.Add(target);
            }
            var display = entry.ContainerId == containerId
                ? (regionId, x, y, rotation)
                : find_display_position(entry, containerId, regionId, all.Where(e => e.ContainerId == containerId && e.InstanceId != instanceId));
            target.items.Add(new StorageBagStoredItemData { instance_id = instanceId, item_path = entry.ItemPath,
                count = entry.Count, artifact = entry.Artifact, cell_x = display.Item2, cell_y = display.Item3, rotation = display.Item4 });
        }
        if (entry.ContainerId.Length == 0 || containerId.Length == 0)
        {
            if (!plan_spatial_inventory(spatial_candidates(paths, counts, slots, artifacts, artifactSlots), placements,
                out var plannedPlacements, preserveUnplaced: true)) return false;
            placements = plannedPlacements;
        }
        _spatialCommitting = true;
        try
        {
            human.inventory_item_paths = paths; human.inventory_item_counts = counts; human.inventory_item_positions = slots;
            human.artifact_inventory = artifacts; human.artifact_inventory_positions = artifactSlots;
            human.inventory_placements = placements; human.storage_bag_contents = contents;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        return true;
    }
}
