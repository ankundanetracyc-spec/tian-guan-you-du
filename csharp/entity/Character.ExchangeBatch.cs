using Godot;

/// <summary>将普通货品和独立器物作为一次库存事务接收。</summary>
public partial class Character
{
    internal bool try_add_exchange_batch(IReadOnlyDictionary<string, int> items,
        IReadOnlyList<ArtifactInstanceData> newArtifacts, Func<bool> beforeCommit)
    {
        if (human is null || _spatialCommitting) return false;
        if (!prepare_spatial_exchange(new Dictionary<string, int>(), items,
                out var paths, out var counts, out var slots, out _)) return false;

        var artifacts = human.artifact_inventory.ToList();
        var artifactSlots = human.artifact_inventory_positions.ToList();
        var ids = artifacts.Select(item => item.instance_id).ToHashSet(StringComparer.Ordinal);
        int nextSlot = slots.Concat(artifactSlots).DefaultIfEmpty(-1).Max() + 1;
        foreach (var artifact in newArtifacts)
        {
            if (artifact is null || !ForgedArtifactInventory.TryRead(artifact, out _)) return false;
            artifact.ensure_instance_id();
            if (!ids.Add(artifact.instance_id)) return false;
            artifacts.Add(artifact);
            artifactSlots.Add(nextSlot++);
        }

        if (!plan_spatial_inventory(spatial_candidates(paths, counts, slots, artifacts, artifactSlots),
                human.inventory_placements, out var placements, preserveUnplaced: true)) return false;

        _spatialCommitting = true;
        try
        {
            if (!beforeCommit()) return false;
            foreach (var artifact in newArtifacts)
                if (artifact.current_durability <= 0 && artifact.max_durability > 0)
                    artifact.current_durability = artifact.max_durability;
            human.inventory_item_paths = new(paths);
            human.inventory_item_counts = new(counts);
            human.inventory_item_positions = new(slots);
            human.artifact_inventory = new(artifacts);
            human.artifact_inventory_positions = new(artifactSlots);
            human.inventory_placements = placements;
        }
        finally { _spatialCommitting = false; }

        notify_inventory_changed();
        foreach (var (path, count) in items) publish_item_acquired_notice(path, count);
        return true;
    }
}

