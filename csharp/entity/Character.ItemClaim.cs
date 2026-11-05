using Godot;

public partial class Character
{
    // The receipt edits a private layout. Commit only against the exact inventory it opened with.
    internal static string claim_inventory_stamp(IEnumerable<SpatialInventoryEntry> entries) => string.Join("|",
        entries.OrderBy(e => e.InstanceId, StringComparer.Ordinal).Select(e =>
            $"{e.InstanceId}:{e.ItemPath}:{e.ArtifactId}:{e.SlotIndex}:{e.RegionId}:{e.X}:{e.Y}:{e.Width}:{e.Height}:{e.Rotation}:{e.IsEquipped}"));

    internal bool try_commit_claim_inventory(IReadOnlyList<SpatialInventoryEntry> baseline,
        IReadOnlyList<SpatialInventoryEntry> layout, IReadOnlyDictionary<string, int> allowedIncoming)
    {
        if (human is null || _spatialCommitting || layout.Count > InventorySlotCapacity) return false;
        var current = get_spatial_inventory();
        if (claim_inventory_stamp(current) != claim_inventory_stamp(baseline)) return false;
        var originals = current.ToDictionary(e => e.InstanceId, StringComparer.Ordinal);
        if (layout.Select(e => e.InstanceId).Distinct().Count() != layout.Count) return false;
        var retained = layout.Select(e => e.InstanceId).ToHashSet(StringComparer.Ordinal);
        if (current.Any(e => !retained.Contains(e.InstanceId) && e.IsEquipped)) return false;
        if (current.Any(e => !retained.Contains(e.InstanceId) && has_nonempty_storage_ring(e.InstanceId))) return false;
        if (human.is_in_breakthrough_retreat && !string.IsNullOrEmpty(human.breakthrough_pill_path)
            && current.Any(e => e.ItemPath == human.breakthrough_pill_path)
            && !layout.Any(e => e.ItemPath == human.breakthrough_pill_path && originals.ContainsKey(e.InstanceId))) return false;
        var added = layout.Where(e => !originals.ContainsKey(e.InstanceId)).ToList();
        if (added.Any(e => e.Artifact is not null || e.ArtifactId.Length > 0 || e.ItemPath.Length == 0)) return false;
        if (added.GroupBy(e => e.ItemPath).Any(g => g.Count() > allowedIncoming.GetValueOrDefault(g.Key))) return false;
        var occupied = new List<(InventoryPlacementData Placement, Vector2I Size)>();
        var placements = new Godot.Collections.Array<InventoryPlacementData>();
        var paths = new Godot.Collections.Array<string>(); var counts = new Godot.Collections.Array<int>();
        var slots = new Godot.Collections.Array<int>();
        var artifacts = new Godot.Collections.Array<ArtifactInstanceData>(); var artifactSlots = new Godot.Collections.Array<int>();
        int nextSlot = current.Select(e => e.SlotIndex).DefaultIfEmpty(-1).Max() + 1;
        foreach (var entry in layout)
        {
            var old = originals.GetValueOrDefault(entry.InstanceId);
            if (old is not null && (old.ItemPath != entry.ItemPath || old.ArtifactId != entry.ArtifactId)) return false;
            var item = entry.ItemPath.Length > 0 ? load_inventory_item(entry.ItemPath) : null;
            var artifact = old?.Artifact;
            if (item is null && artifact is null) return false;
            if (old is null && (item is null || !GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item))) return false;
            if (entry.Rotation is < 0 or > 3 || (item?.inventory_can_rotate == false && entry.Rotation != 0)) return false;
            var size = item?.get_inventory_size() ?? artifact_inventory_size(artifact);
            if ((entry.Rotation & 1) != 0) size = new(size.Y, size.X);
            if (size != entry.Size || !spatial_fits(entry.RegionId, entry.X, entry.Y, size, occupied)) return false;
            int slot = old?.SlotIndex ?? nextSlot++;
            var p = new InventoryPlacementData { instance_id = entry.InstanceId, item_path = entry.ItemPath,
                artifact_id = entry.ArtifactId, row_slot = slot, region_id = entry.RegionId,
                cell_x = entry.X, cell_y = entry.Y, rotation = entry.Rotation };
            placements.Add(p); occupied.Add((p, size));
            if (artifact is not null) { artifacts.Add(artifact); artifactSlots.Add(slot); }
            else { paths.Add(entry.ItemPath); counts.Add(1); slots.Add(slot); }
        }
        _spatialCommitting = true;
        try
        {
            human.inventory_item_paths = paths; human.inventory_item_counts = counts; human.inventory_item_positions = slots;
            human.artifact_inventory = artifacts; human.artifact_inventory_positions = artifactSlots;
            human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        return true;
    }
}
