using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed record SpatialInventoryRegion(string Id, string Name, int Width, int Height);
public sealed record SpatialInventoryEntry(string InstanceId, string ItemPath, string ArtifactId, string RegionId,
    int X, int Y, int Width, int Height, int Rotation, int SlotIndex, ItemData? Item, ArtifactInstanceData? Artifact,
    bool IsEquipped, string ContainerId = "", int Count = 1, int Volume = 1)
{
    public bool IsPlaced => !string.IsNullOrEmpty(RegionId) && X >= 0 && Y >= 0;
    public Vector2I Size => new(Width, Height);
    public int CapacityUsed => (int)Math.Min(int.MaxValue, (long)Math.Max(0, Volume) * Math.Max(0, Count));
}

public partial class Character
{
    private bool _spatialCommitting;
    private bool _consumingBreakthroughReservation;
    private int? _spatialReconciledStamp;
    private readonly Dictionary<string, ItemData?> _spatialItemCache = new(StringComparer.Ordinal);

    private int spatial_state_stamp()
    {
        var stamp = new HashCode();
        if (human is null) return 0;
        foreach (var value in human.inventory_item_paths) stamp.Add(value, StringComparer.Ordinal);
        foreach (var value in human.inventory_item_counts) stamp.Add(value);
        foreach (var value in human.inventory_item_positions) stamp.Add(value);
        foreach (var value in human.artifact_inventory_positions) stamp.Add(value);
        foreach (var value in human.artifact_inventory) { stamp.Add(value?.instance_id); stamp.Add(value?.forged_snapshot_json); }
        foreach (var p in human.inventory_placements)
        {
            stamp.Add(p.instance_id); stamp.Add(p.row_slot); stamp.Add(p.item_path); stamp.Add(p.artifact_id);
            stamp.Add(p.region_id); stamp.Add(p.cell_x); stamp.Add(p.cell_y); stamp.Add(p.rotation);
        }
        return stamp.ToHashCode();
    }

    public static IReadOnlyList<SpatialInventoryRegion> InventoryRegions { get; } = new SpatialInventoryRegion[]
    {
        new("chest", "胸前", 8, 5), new("sleeve-left", "左袖", 4, 5), new("sleeve-right", "右袖", 4, 5),
        new("waist", "腰间", 8, 5), new("back", "背囊", 8, 10)
    };

    private sealed record SpatialCandidate(int Slot, string Path, ItemData? Item, ArtifactInstanceData? Artifact, int Count = 1)
    {
        public int CapacityUsed => Item is not null
            ? InventoryCapacityRules.Used(Item, Count) : InventoryCapacityRules.Used(Artifact);
        public Vector2I DisplaySize => Item?.get_inventory_size() ?? (Artifact is not null && ForgedArtifactInventory.TryRead(Artifact, out var forged)
            ? ForgedArtifactPresentation.InventorySize(forged) : Vector2I.Zero);
        public bool CanRotate => Item?.inventory_can_rotate ?? true;
        public string ArtifactId => Artifact?.instance_id ?? "";
    }

    private static int artifact_inventory_volume(ArtifactInstanceData? artifact) => InventoryCapacityRules.ArtifactVolume(artifact);

    private List<SpatialCandidate> spatial_candidates(IReadOnlyList<string> paths, IReadOnlyList<int> counts,
        IReadOnlyList<int> slots, IReadOnlyList<ArtifactInstanceData> artifacts, IReadOnlyList<int> artifactSlots)
    {
        var result = new List<SpatialCandidate>();
        for (int i = 0; i < paths.Count; i++)
            result.Add(new(slots[i], paths[i], load_inventory_item(paths[i]), null, counts[i]));
        for (int i = 0; i < artifacts.Count; i++)
            if (artifacts[i] is { } artifact) result.Add(new(artifactSlots[i], "", null, artifact));
        return result;
    }

    private static bool spatial_fits(string regionId, int x, int y, Vector2I size,
        IEnumerable<(InventoryPlacementData Placement, Vector2I Size)> occupied, string except = "")
    {
        var region = InventoryRegions.FirstOrDefault(r => r.Id == regionId);
        if (region is null || x < 0 || y < 0 || size.X <= 0 || size.Y <= 0
            || (long)x + size.X > region.Width || (long)y + size.Y > region.Height) return false;
        return !occupied.Any(o => o.Placement.instance_id != except && o.Placement.region_id == regionId
            && x < (long)o.Placement.cell_x + o.Size.X && (long)x + size.X > o.Placement.cell_x
            && y < (long)o.Placement.cell_y + o.Size.Y && (long)y + size.Y > o.Placement.cell_y);
    }

    private bool plan_spatial_inventory(List<SpatialCandidate> candidates, IEnumerable<InventoryPlacementData> previous,
        out Godot.Collections.Array<InventoryPlacementData> placements, bool preserveUnplaced = false, bool allowOverCapacity = false)
    {
        placements = [];
        long usedCapacity = candidates.Sum(candidate => (long)candidate.CapacityUsed);
        int totalCapacity = InventoryCapacity;
        if (usedCapacity > totalCapacity && !allowOverCapacity) return false;

        var existing = previous.Where(p => p is not null).ToList();
        var occupied = new List<(InventoryPlacementData Placement, Vector2I Size)>();
        var pending = new List<(SpatialCandidate Item, InventoryPlacementData Placement)>();
        foreach (var item in candidates)
        {
            var old = existing.FirstOrDefault(p => p.row_slot == item.Slot && p.item_path == item.Path && p.artifact_id == item.ArtifactId);
            var placement = old?.Duplicate() as InventoryPlacementData ?? new InventoryPlacementData
            { instance_id = item.ArtifactId.Length > 0 ? item.ArtifactId : Guid.NewGuid().ToString("N"), row_slot = item.Slot,
                item_path = item.Path, artifact_id = item.ArtifactId };
            if (string.IsNullOrWhiteSpace(placement.instance_id)) placement.instance_id = Guid.NewGuid().ToString("N");
            placement.rotation = ((placement.rotation % 4) + 4) % 4;
            if (item.CapacityUsed <= 0)
            {
                if (!preserveUnplaced) return false;
                placement.region_id = ""; placement.cell_x = placement.cell_y = -1;
                placements.Add(placement); continue;
            }
            var size = placement.rotation % 2 == 0 ? item.DisplaySize : new Vector2I(item.DisplaySize.Y, item.DisplaySize.X);
            if (!item.CanRotate) { placement.rotation = 0; size = item.DisplaySize; }
            if (spatial_fits(placement.region_id, placement.cell_x, placement.cell_y, size, occupied))
                occupied.Add((placement, size));
            else pending.Add((item, placement));
            placements.Add(placement);
        }

        // Physical shapes are presentation only. Reflow where possible; inability to display a shape
        // never rejects an inventory transaction whose capacity check succeeded.
        foreach (var entry in pending.OrderByDescending(e => (long)e.Item.DisplaySize.X * e.Item.DisplaySize.Y))
        {
            bool found = false;
            foreach (var rotation in entry.Item.CanRotate ? new[] { entry.Placement.rotation, (entry.Placement.rotation + 1) % 4 } : new[] { 0 })
            {
                var size = rotation % 2 == 0 ? entry.Item.DisplaySize : new Vector2I(entry.Item.DisplaySize.Y, entry.Item.DisplaySize.X);
                foreach (var region in InventoryRegions)
                {
                    for (int y = 0; y <= region.Height - size.Y && !found; y++)
                        for (int x = 0; x <= region.Width - size.X && !found; x++)
                            if (spatial_fits(region.Id, x, y, size, occupied))
                        {
                            entry.Placement.region_id = region.Id; entry.Placement.cell_x = x; entry.Placement.cell_y = y; entry.Placement.rotation = rotation;
                            occupied.Add((entry.Placement, size)); found = true;
                        }
                    if (found) break;
                }
                if (found) break;
            }
            if (!found)
            {
                entry.Placement.region_id = ""; entry.Placement.cell_x = entry.Placement.cell_y = -1; entry.Placement.rotation = 0;
            }
        }
        return true;
    }

    private void normalize_inventory_stacks()
    {
        if (human is null) return;
        var paths = new List<string>(); var counts = new List<int>(); var slots = new List<int>();
        int nextSlot = human.inventory_item_positions.Concat(human.artifact_inventory_positions).DefaultIfEmpty(-1).Max() + 1;
        for (int sourceIndex = 0; sourceIndex < human.inventory_item_paths.Count; sourceIndex++)
        {
            string path = human.inventory_item_paths[sourceIndex];
            int remaining = Math.Max(1, human.inventory_item_counts[sourceIndex]);
            var item = load_inventory_item(path);
            int maxStack = item?.can_stack == true ? Math.Max(1, item.max_stack) : 1;
            if (maxStack == 1 && remaining > InventorySlotCapacity)
            {
                paths.Add(path); counts.Add(remaining); slots.Add(human.inventory_item_positions[sourceIndex]);
                continue;
            }
            bool first = true;
            while (remaining > 0)
            {
                int target = -1;
                if (maxStack > 1)
                    for (int i = 0; i < paths.Count; i++)
                        if (paths[i] == path && counts[i] < maxStack) { target = i; break; }
                if (target >= 0)
                {
                    int moved = Math.Min(remaining, maxStack - counts[target]);
                    counts[target] += moved; remaining -= moved;
                    continue;
                }
                int amount = Math.Min(remaining, maxStack);
                int slot = first ? human.inventory_item_positions[sourceIndex] : nextSlot++;
                paths.Add(path); counts.Add(amount); slots.Add(slot); remaining -= amount; first = false;
            }
        }
        human.inventory_item_paths = new(paths); human.inventory_item_counts = new(counts); human.inventory_item_positions = new(slots);
    }

    private void ensure_spatial_inventory_state()
    {
        if (human is null) return;
        human.inventory_placements ??= [];
        if (_spatialReconciledStamp == spatial_state_stamp()) return;
        normalize_inventory_stacks();
        var candidates = spatial_candidates(human.inventory_item_paths, human.inventory_item_counts, human.inventory_item_positions,
            human.artifact_inventory, human.artifact_inventory_positions);
        plan_spatial_inventory(candidates, human.inventory_placements, out var placements, preserveUnplaced: true, allowOverCapacity: true);
        human.inventory_placements = placements;
        _spatialReconciledStamp = spatial_state_stamp();
    }

    public IReadOnlyList<SpatialInventoryEntry> get_spatial_inventory()
    {
        ensure_inventory_state();
        if (human is null) return [];
        var candidates = spatial_candidates(human.inventory_item_paths, human.inventory_item_counts,
            human.inventory_item_positions, human.artifact_inventory, human.artifact_inventory_positions);
        return candidates.Select(item =>
        {
            var placement = human.inventory_placements.FirstOrDefault(p => p.row_slot == item.Slot);
            int volume = item.Item is not null ? InventoryCapacityRules.ItemVolume(item.Item) : artifact_inventory_volume(item.Artifact);
            var size = item.DisplaySize;
            int rotation = placement?.rotation ?? 0;
            if ((rotation & 1) != 0) size = new Vector2I(size.Y, size.X);
            return new SpatialInventoryEntry(placement?.instance_id ?? item.ArtifactId, item.Path, item.ArtifactId,
                placement?.region_id ?? "", placement?.cell_x ?? -1, placement?.cell_y ?? -1, size.X, size.Y, rotation, item.Slot,
                item.Item, item.Artifact,
                item.Artifact is not null ? is_artifact_equipped(item.ArtifactId) : is_item_equipped(item.Path),
                Count: item.Count, Volume: volume);
        }).ToList();
    }

    public int get_inventory_used_capacity() => get_spatial_inventory().Sum(entry => entry.CapacityUsed);
    // Compatibility alias: this reports used capacity, not occupied display cells.
    public int get_inventory_occupied_cell_count() => get_inventory_used_capacity();

    public bool can_move_spatial_item(string instanceId, string regionId, int x, int y, int rotation)
    {
        if (rotation is < 0 or > 3) return false;
        var entries = get_spatial_inventory();
        if (entries.All(entry => entry.InstanceId != instanceId)) return false;
        var item = entries.First(entry => entry.InstanceId == instanceId);
        var size = item.Size;
        if ((rotation & 1) != 0) size = new Vector2I(size.Y, size.X);
        var occupied = entries.Where(entry => entry.IsPlaced && entry.InstanceId != instanceId).Select(entry =>
            (new InventoryPlacementData { instance_id = entry.InstanceId, region_id = entry.RegionId, cell_x = entry.X, cell_y = entry.Y }, entry.Size));
        return spatial_fits(regionId, x, y, size, occupied);
    }

    public bool move_spatial_item(string instanceId, string regionId, int x, int y, int rotation)
    {
        if (_spatialCommitting || !can_move_spatial_item(instanceId, regionId, x, y, rotation) || human is null) return false;
        var placement = human.inventory_placements.First(i => i.instance_id == instanceId);
        placement.region_id = regionId; placement.cell_x = x; placement.cell_y = y; placement.rotation = rotation;
        notify_inventory_changed(); return true;
    }

    private bool prepare_spatial_exchange(IReadOnlyDictionary<string, int> outgoing, IReadOnlyDictionary<string, int> incoming,
        out List<string> paths, out List<int> counts, out List<int> slots, out Godot.Collections.Array<InventoryPlacementData> placements)
    {
        paths = []; counts = []; slots = []; placements = [];
        if (human is null) return false;
        ensure_inventory_state();
        paths = human.inventory_item_paths.ToList(); counts = human.inventory_item_counts.ToList(); slots = human.inventory_item_positions.ToList();
        foreach (var (path, count) in outgoing)
        {
            int reserved = !_consumingBreakthroughReservation && human.is_in_breakthrough_retreat && human.breakthrough_pill_path == path ? 1 : 0;
            if (count < 0 || get_inventory_total_count(path) - get_equipped_item_count(path) - get_nonempty_storage_bag_count(path) - reserved < count) return false;
            int remaining = count;
            for (int i = paths.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (paths[i] != path) continue;
                int rowSlot = slots[i];
                var placement = human.inventory_placements.FirstOrDefault(p => p.row_slot == rowSlot);
                if (placement is not null && has_nonempty_storage_bag(placement.instance_id)) continue;
                int amount = Math.Min(counts[i], remaining); counts[i] -= amount; remaining -= amount;
                if (counts[i] == 0) { paths.RemoveAt(i); counts.RemoveAt(i); slots.RemoveAt(i); }
            }
            if (remaining != 0) return false;
        }
        if (incoming.Any(pair => pair.Value < 0)) return false;
        int next = human.inventory_item_positions.Concat(human.artifact_inventory_positions).DefaultIfEmpty(-1).Max() + 1;
        foreach (var (path, requested) in incoming)
        {
            if (requested == 0) continue;
            var item = load_inventory_item(path);
            if (item is null || !GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item)) return false;
            if (item is StorageBagItemData bag && bag.validate_definition().Count > 0) return false;
            int remaining = requested;
            int maxStack = item.can_stack ? Math.Max(1, item.max_stack) : 1;
            if (maxStack > 1)
                for (int i = 0; i < paths.Count && remaining > 0; i++)
                {
                    if (paths[i] != path || counts[i] >= maxStack) continue;
                    int added = Math.Min(remaining, maxStack - counts[i]);
                    counts[i] += added; remaining -= added;
                }
            while (remaining > 0)
            {
                int amount = Math.Min(remaining, maxStack);
                paths.Add(path); counts.Add(amount); slots.Add(next++); remaining -= amount;
            }
        }
        return plan_spatial_inventory(spatial_candidates(paths, counts, slots, human.artifact_inventory,
            human.artifact_inventory_positions), human.inventory_placements, out placements, preserveUnplaced: true);
    }

    public bool can_exchange_inventory(IReadOnlyDictionary<string, int> outgoing, IReadOnlyDictionary<string, int> incoming)
        => prepare_spatial_exchange(outgoing, incoming, out _, out _, out _, out _);

    public bool try_exchange_inventory(IReadOnlyDictionary<string, int> outgoing, IReadOnlyDictionary<string, int> incoming,
        bool announceAcquisition = false, Func<bool>? beforeCommit = null)
    {
        if (_spatialCommitting || !prepare_spatial_exchange(outgoing, incoming, out var paths, out var counts, out var slots, out var placements) || human is null) return false;
        _spatialCommitting = true;
        try
        {
            if (beforeCommit is not null && !beforeCommit()) return false;
            human.inventory_item_paths = new(paths); human.inventory_item_counts = new(counts);
            human.inventory_item_positions = new(slots); human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        if (announceAcquisition)
            foreach (var (path, count) in incoming) if (count > 0) publish_item_acquired_notice(path, count);
        return true;
    }

    public bool can_add_inventory_batch(IReadOnlyDictionary<string, int> incoming) =>
        can_exchange_inventory(new Dictionary<string, int>(), incoming);

    /// <summary>Plan the whole artifact capacity before charging or publishing inventory changes.</summary>
    public bool try_add_artifact_to_inventory(ArtifactInstanceData artifact, Func<bool>? beforeCommit = null)
    {
        if (human is null || artifact is null || _spatialCommitting) return false;
        if (!ForgedArtifactInventory.TryRead(artifact, out _)) return false;
        ensure_inventory_state(); artifact.ensure_instance_id();
        if (find_artifact(artifact.instance_id) is not null) return false;
        var artifactList = human.artifact_inventory.ToList(); artifactList.Add(artifact);
        var slots = human.artifact_inventory_positions.ToList();
        int next = human.inventory_item_positions.Concat(human.artifact_inventory_positions).DefaultIfEmpty(-1).Max() + 1;
        slots.Add(next);
        if (!plan_spatial_inventory(spatial_candidates(human.inventory_item_paths, human.inventory_item_counts,
                human.inventory_item_positions, artifactList, slots), human.inventory_placements, out var placements, preserveUnplaced: true)) return false;
        _spatialCommitting = true;
        try
        {
            if (beforeCommit is not null && !beforeCommit()) return false;
            if (artifact.current_durability <= 0 && artifact.max_durability > 0) artifact.current_durability = artifact.max_durability;
            human.artifact_inventory = new(artifactList); human.artifact_inventory_positions = new(slots); human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed(); return true;
    }
}
