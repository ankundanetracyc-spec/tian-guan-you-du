using Godot;
using GArray = Godot.Collections.Array;

public sealed record SpatialInventoryRegion(string Id, string Name, int Width, int Height);
public sealed record SpatialInventoryEntry(string InstanceId, string ItemPath, string ArtifactId, string RegionId,
    int X, int Y, int Width, int Height, int Rotation, int SlotIndex, ItemData? Item, ArtifactInstanceData? Artifact, bool IsEquipped)
{
    public bool IsPlaced => !string.IsNullOrEmpty(RegionId) && X >= 0 && Y >= 0;
    public Vector2I Size => new(Width, Height);
}

public partial class Character
{
    private bool _spatialCommitting;
    private bool _consumingBreakthroughReservation;
    private int? _spatialReconciledStamp;
    private readonly System.Collections.Generic.Dictionary<string, ItemData?> _spatialItemCache = new(StringComparer.Ordinal);
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
        new("chest", "胸前", 6, 4), new("sleeve-left", "左袖", 3, 4), new("sleeve-right", "右袖", 3, 4),
        new("waist", "腰间", 6, 4), new("back", "背囊", 6, 8)
    };
    private sealed record SpatialCandidate(int Slot, string Path, ItemData? Item, ArtifactInstanceData? Artifact)
    {
        public Vector2I BaseSize => Item?.get_inventory_size() ?? artifact_inventory_size(Artifact);
        public bool CanRotate => Item?.inventory_can_rotate ?? true;
        public string ArtifactId => Artifact?.instance_id ?? "";
    }
    private static Vector2I artifact_inventory_size(ArtifactInstanceData? artifact) => artifact is not null && ForgedArtifactInventory.TryRead(artifact, out var forged)
        ? ForgedArtifactPresentation.InventorySize(forged) : Vector2I.Zero;
    private List<SpatialCandidate> spatial_candidates(IReadOnlyList<string> paths, IReadOnlyList<int> slots,
        IReadOnlyList<ArtifactInstanceData> artifacts, IReadOnlyList<int> artifactSlots)
    {
        var result = new List<SpatialCandidate>();
        for (int i = 0; i < paths.Count; i++) result.Add(new(slots[i], paths[i], load_inventory_item(paths[i]), null));
        for (int i = 0; i < artifacts.Count; i++) if (artifacts[i] is { } a) result.Add(new(artifactSlots[i], "", null, a));
        return result;
    }
    private static Vector2I spatial_size(SpatialCandidate item, int rotation)
    {
        var size = item.BaseSize;
        return (rotation & 1) != 0 ? new Vector2I(size.Y, size.X) : size;
    }
    private static bool spatial_fits(string regionId, int x, int y, Vector2I size,
        IEnumerable<(InventoryPlacementData Placement, Vector2I Size)> occupied, string except = "")
    {
        var region = InventoryRegions.FirstOrDefault(r => r.Id == regionId);
        if (region is null || x < 0 || y < 0 || size.X <= 0 || size.Y <= 0 || x + size.X > region.Width || y + size.Y > region.Height) return false;
        return !occupied.Any(o => o.Placement.instance_id != except && o.Placement.region_id == regionId
            && x < o.Placement.cell_x + o.Size.X && x + size.X > o.Placement.cell_x
            && y < o.Placement.cell_y + o.Size.Y && y + size.Y > o.Placement.cell_y);
    }
    private bool plan_spatial_inventory(List<SpatialCandidate> candidates, IEnumerable<InventoryPlacementData> previous,
        out Godot.Collections.Array<InventoryPlacementData> placements, bool preserveUnplaced = false)
    {
        placements = [];
        var existing = previous.Where(p => p is not null).ToList();
        var occupied = new List<(InventoryPlacementData Placement, Vector2I Size)>();
        var pending = new List<(SpatialCandidate Item, InventoryPlacementData Placement)>();
        foreach (var item in candidates)
        {
            var old = existing.FirstOrDefault(p => p.row_slot == item.Slot && p.item_path == item.Path && p.artifact_id == item.ArtifactId);
            var p = old?.Duplicate() as InventoryPlacementData ?? new InventoryPlacementData
            { instance_id = item.ArtifactId.Length > 0 ? item.ArtifactId : Guid.NewGuid().ToString("N"), row_slot = item.Slot, item_path = item.Path, artifact_id = item.ArtifactId };
            if (string.IsNullOrWhiteSpace(p.instance_id)) p.instance_id = Guid.NewGuid().ToString("N");
            p.rotation = ((p.rotation % 4) + 4) % 4;
            if (!item.CanRotate) p.rotation = 0;
            var size = spatial_size(item, p.rotation);
            if (spatial_fits(p.region_id, p.cell_x, p.cell_y, size, occupied)) occupied.Add((p, size));
            else pending.Add((item, p));
            placements.Add(p);
        }
        // Place additions largest first without disturbing an existing valid placement.
        foreach (var entry in pending.OrderByDescending(e => e.Item.BaseSize.X * e.Item.BaseSize.Y))
        {
            bool found = false;
            foreach (var rotation in entry.Item.CanRotate ? new[] { entry.Placement.rotation, (entry.Placement.rotation + 1) % 4 } : new[] { 0 })
            {
                var size = spatial_size(entry.Item, rotation);
                foreach (var r in InventoryRegions)
                {
                    for (int y = 0; y <= r.Height - size.Y && !found; y++)
                        for (int x = 0; x <= r.Width - size.X && !found; x++)
                            if (spatial_fits(r.Id, x, y, size, occupied))
                            {
                                entry.Placement.region_id = r.Id; entry.Placement.cell_x = x; entry.Placement.cell_y = y; entry.Placement.rotation = rotation;
                                occupied.Add((entry.Placement, size)); found = true;
                            }
                    if (found) break;
                }
                if (found) break;
            }
            if (!found)
            {
                if (!preserveUnplaced) return false;
                // Unsupported overfull direct seeds are retained, never silently truncated.
                entry.Placement.region_id = ""; entry.Placement.cell_x = entry.Placement.cell_y = -1;
            }
        }
        return true;
    }
    private void ensure_spatial_inventory_state()
    {
        if (human is null) return;
        human.inventory_placements ??= [];
        if (_spatialReconciledStamp == spatial_state_stamp()) return;
        var used = new HashSet<int>();
        int next = 0;
        int Allocate() { while (used.Contains(next)) next++; used.Add(next); return next++; }
        for (int i = 0; i < human.inventory_item_paths.Count; i++)
        {
            var slot = human.inventory_item_positions[i];
            if (slot < 0 || !used.Add(slot)) human.inventory_item_positions[i] = Allocate();
        }
        for (int i = 0; i < human.artifact_inventory.Count; i++)
        {
            var slot = human.artifact_inventory_positions[i];
            if (slot < 0 || !used.Add(slot)) human.artifact_inventory_positions[i] = Allocate();
        }
        // All supported fresh inventories consist of one physical entry per copy, regardless of can_stack.
        for (int i = human.inventory_item_paths.Count - 1; i >= 0; i--)
        {
            int count = human.inventory_item_counts[i];
            if (count <= 1 || count > InventorySlotCapacity) continue;
            human.inventory_item_counts[i] = 1;
            for (int n = 1; n < count; n++)
            { human.inventory_item_paths.Add(human.inventory_item_paths[i]); human.inventory_item_counts.Add(1); human.inventory_item_positions.Add(Allocate()); }
        }
        var candidates = spatial_candidates(human.inventory_item_paths, human.inventory_item_positions, human.artifact_inventory, human.artifact_inventory_positions);
        plan_spatial_inventory(candidates, human.inventory_placements, out var placements, preserveUnplaced: true);
        human.inventory_placements = placements;
        _spatialReconciledStamp = spatial_state_stamp();
    }
    public IReadOnlyList<SpatialInventoryEntry> get_spatial_inventory()
    {
        ensure_inventory_state();
        if (human is null) return [];
        var candidates = spatial_candidates(human.inventory_item_paths, human.inventory_item_positions, human.artifact_inventory, human.artifact_inventory_positions);
        return candidates.Select(item =>
        {
            var p = human.inventory_placements.First(p => p.row_slot == item.Slot);
            var size = spatial_size(item, p.rotation);
            return new SpatialInventoryEntry(p.instance_id, item.Path, item.ArtifactId, p.region_id, p.cell_x, p.cell_y, size.X, size.Y,
                p.rotation, item.Slot, item.Item, item.Artifact, item.Artifact is not null ? is_artifact_equipped(item.ArtifactId) : is_item_equipped(item.Path));
        }).ToList();
    }
    public int get_inventory_occupied_cell_count() => get_spatial_inventory().Where(e => e.IsPlaced).Sum(e => e.Width * e.Height);
    public bool can_move_spatial_item(string instanceId, string regionId, int x, int y, int rotation)
    {
        var entries = get_spatial_inventory(); var item = entries.FirstOrDefault(i => i.InstanceId == instanceId);
        if (item is null || rotation is < 0 or > 3 || (item.Item is { inventory_can_rotate: false } && rotation != 0)) return false;
        var size = item.Item?.get_inventory_size() ?? artifact_inventory_size(item.Artifact);
        if ((rotation & 1) != 0) size = new(size.Y, size.X);
        var occupied = entries.Where(i => i.IsPlaced && i.InstanceId != instanceId).Select(i =>
            (new InventoryPlacementData { instance_id = i.InstanceId, region_id = i.RegionId, cell_x = i.X, cell_y = i.Y }, new Vector2I(i.Width, i.Height)));
        return spatial_fits(regionId, x, y, size, occupied);
    }
    public bool move_spatial_item(string instanceId, string regionId, int x, int y, int rotation)
    {
        if (_spatialCommitting) return false;
        if (!can_move_spatial_item(instanceId, regionId, x, y, rotation) || human is null) return false;
        var p = human.inventory_placements.First(i => i.instance_id == instanceId);
        p.region_id = regionId; p.cell_x = x; p.cell_y = y; p.rotation = rotation;
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
            var reservedForBreakthrough = !_consumingBreakthroughReservation && human.is_in_breakthrough_retreat && human.breakthrough_pill_path == path ? 1 : 0;
            if (count < 0 || get_inventory_total_count(path) - get_equipped_item_count(path) - reservedForBreakthrough < count) return false;
            int remaining = count;
            for (int i = paths.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (paths[i] != path) continue;
                int amount = Math.Min(counts[i], remaining); counts[i] -= amount; remaining -= amount;
                if (counts[i] == 0) { paths.RemoveAt(i); counts.RemoveAt(i); slots.RemoveAt(i); }
            }
            if (remaining != 0) return false;
        }
        long incomingCount = incoming.Values.Aggregate(0L, (n, c) => n + c);
        if (incoming.Any(p => p.Value < 0) || incomingCount > InventorySlotCapacity || paths.Count + incomingCount + human.artifact_inventory.Count > InventorySlotCapacity) return false;
        // Do not reuse a removed row ID in the same transaction: its old placement belongs to a different instance.
        int next = human.inventory_item_positions.Concat(human.artifact_inventory_positions).DefaultIfEmpty(-1).Max() + 1;
        foreach (var (path, count) in incoming)
        {
            var item = load_inventory_item(path);
            if (count > 0 && (item is null || !GongfaAvailability.IsItemEnabled(item))) return false;
            for (int n = 0; n < count; n++) { paths.Add(path); counts.Add(1); slots.Add(next++); }
        }
        return plan_spatial_inventory(spatial_candidates(paths, slots, human.artifact_inventory, human.artifact_inventory_positions), human.inventory_placements, out placements, preserveUnplaced: incomingCount == 0);
    }
    public bool can_exchange_inventory(IReadOnlyDictionary<string, int> outgoing, IReadOnlyDictionary<string, int> incoming)
        => prepare_spatial_exchange(outgoing, incoming, out _, out _, out _, out _);
    public bool try_exchange_inventory(IReadOnlyDictionary<string, int> outgoing, IReadOnlyDictionary<string, int> incoming,
        bool announceAcquisition = false, Func<bool>? beforeCommit = null)
    {
        if (_spatialCommitting) return false;
        if (!prepare_spatial_exchange(outgoing, incoming, out var paths, out var counts, out var slots, out var placements) || human is null) return false;
        _spatialCommitting = true;
        try
        {
            if (beforeCommit is not null && !beforeCommit()) return false;
            human.inventory_item_paths = new(paths); human.inventory_item_counts = new(counts); human.inventory_item_positions = new(slots); human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        if (announceAcquisition) foreach (var (path, count) in incoming) if (count > 0) publish_item_acquired_notice(path, count);
        return true;
    }
    public bool can_add_inventory_batch(IReadOnlyDictionary<string, int> incoming) => can_exchange_inventory(new System.Collections.Generic.Dictionary<string, int>(), incoming);

    /// <summary>Plan the whole artifact placement before charging or publishing inventory changes.</summary>
    public bool try_add_artifact_to_inventory(ArtifactInstanceData artifact, Func<bool>? beforeCommit = null)
    {
        if (human is null || artifact is null || _spatialCommitting) return false;
        if (ForgedArtifactInventory.IsForged(artifact) && !ForgedArtifactInventory.TryRead(artifact, out _)) return false;
        ensure_inventory_state(); artifact.ensure_instance_id();
        if (find_artifact(artifact.instance_id) is not null) return false;
        var artifactList = human.artifact_inventory.ToList(); artifactList.Add(artifact);
        var slots = human.artifact_inventory_positions.ToList();
        int next = human.inventory_item_positions.Concat(human.artifact_inventory_positions).DefaultIfEmpty(-1).Max() + 1;
        slots.Add(next);
        if (!plan_spatial_inventory(spatial_candidates(human.inventory_item_paths, human.inventory_item_positions, artifactList, slots), human.inventory_placements, out var placements)) return false;
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
