using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Character
{
    internal static string claim_inventory_stamp(IEnumerable<SpatialInventoryEntry> entries) => string.Join("|",
        entries.OrderBy(entry => entry.InstanceId, StringComparer.Ordinal).Select(entry =>
            $"{entry.InstanceId}:{entry.ItemPath}:{entry.ArtifactId}:{entry.SlotIndex}:{entry.RegionId}:{entry.X}:{entry.Y}:{entry.Width}:{entry.Height}:{entry.Rotation}:{entry.IsEquipped}:{entry.Count}:{entry.Volume}"));

    internal bool try_commit_claim_inventory(IReadOnlyList<SpatialInventoryEntry> baseline,
        IReadOnlyList<SpatialInventoryEntry> layout, IReadOnlyDictionary<string, int> allowedIncoming, Func<bool>? beforeCommit = null)
    {
        if (human is null || _spatialCommitting) return false;
        var current = get_spatial_inventory();
        if (claim_inventory_stamp(current) != claim_inventory_stamp(baseline)) return false;
        var originals = current.ToDictionary(entry => entry.InstanceId, StringComparer.Ordinal);
        if (layout.Select(entry => entry.InstanceId).Distinct(StringComparer.Ordinal).Count() != layout.Count) return false;
        var retained = layout.Select(entry => entry.InstanceId).ToHashSet(StringComparer.Ordinal);
        if (current.Any(entry => !retained.Contains(entry.InstanceId) && entry.IsEquipped)) return false;
        if (current.Any(entry => !retained.Contains(entry.InstanceId) && entry.Artifact?.travel_in_use == true)) return false;
        if (current.Any(entry => !retained.Contains(entry.InstanceId) && has_nonempty_storage_bag(entry.InstanceId))) return false;
        if (human.is_in_breakthrough_retreat && !string.IsNullOrEmpty(human.breakthrough_pill_path)
            && current.Any(entry => entry.ItemPath == human.breakthrough_pill_path)
            && !layout.Any(entry => entry.ItemPath == human.breakthrough_pill_path && originals.ContainsKey(entry.InstanceId))) return false;

        var added = layout.Where(entry => !originals.ContainsKey(entry.InstanceId)).ToList();
        if (added.Any(entry => entry.Artifact is not null || entry.ArtifactId.Length > 0 || entry.ItemPath.Length == 0)) return false;
        if (added.GroupBy(entry => entry.ItemPath).Any(group => group.Sum(entry => (long)entry.Count) > allowedIncoming.GetValueOrDefault(group.Key))) return false;

        long usedCapacity = 0;
        foreach (var entry in layout)
        {
            var old = originals.GetValueOrDefault(entry.InstanceId);
            if (old is not null && (old.ItemPath != entry.ItemPath || old.ArtifactId != entry.ArtifactId || old.Count != entry.Count)) return false;
            var item = entry.ItemPath.Length > 0 ? load_inventory_item(entry.ItemPath) : null;
            var artifact = old?.Artifact;
            if (item is null && artifact is null) return false;
            if (entry.Count < 1) return false;
            if (item is not null)
            {
                int maximum = item.can_stack ? Math.Max(1, item.max_stack) : 1;
                if (entry.Count > maximum || entry.Volume != InventoryCapacityRules.ItemVolume(item)) return false;
                if (old is null && (!GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item))) return false;
                usedCapacity += InventoryCapacityRules.Used(item, entry.Count);
            }
            else
            {
                if (entry.Count != 1 || entry.Volume != InventoryCapacityRules.ArtifactVolume(artifact)) return false;
                usedCapacity += InventoryCapacityRules.Used(artifact);
            }
            if (usedCapacity > InventoryCapacity) return false;
        }

        var placements = new Godot.Collections.Array<InventoryPlacementData>();
        var paths = new Godot.Collections.Array<string>(); var counts = new Godot.Collections.Array<int>();
        var slots = new Godot.Collections.Array<int>(); var artifacts = new Godot.Collections.Array<ArtifactInstanceData>();
        var artifactSlots = new Godot.Collections.Array<int>();
        int nextSlot = current.Select(entry => entry.SlotIndex).DefaultIfEmpty(-1).Max() + 1;
        foreach (var entry in layout)
        {
            var old = originals.GetValueOrDefault(entry.InstanceId);
            int slot = old?.SlotIndex ?? nextSlot++;
            var placement = new InventoryPlacementData { instance_id = entry.InstanceId, item_path = entry.ItemPath,
                artifact_id = entry.ArtifactId, row_slot = slot, region_id = entry.RegionId,
                cell_x = entry.X, cell_y = entry.Y, rotation = entry.Rotation };
            placements.Add(placement);
            if (old?.Artifact is { } artifact) { artifacts.Add(artifact); artifactSlots.Add(slot); }
            else { paths.Add(entry.ItemPath); counts.Add(entry.Count); slots.Add(slot); }
        }

        _spatialCommitting = true;
        try
        {
            if (beforeCommit is not null && !beforeCommit()) return false;
            human.inventory_item_paths = paths; human.inventory_item_counts = counts; human.inventory_item_positions = slots;
            human.artifact_inventory = artifacts; human.artifact_inventory_positions = artifactSlots;
            human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        return true;
    }
}
