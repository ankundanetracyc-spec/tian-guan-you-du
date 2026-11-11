using System;
using Godot;

/// <summary>Shared capacity accounting for storage inventories. Combat-board shapes remain separate.</summary>
public static class InventoryCapacityRules
{
    public static int ItemVolume(ItemData? item) => item is null ? 0 : item.get_storage_volume();

    public static int ArtifactVolume(ArtifactInstanceData? artifact)
    {
        if (artifact is null || !ForgedArtifactInventory.TryRead(artifact, out var forged)) return 0;
        return Math.Max(0, ForgedArtifactInventory.StorageVolume(forged));
    }

    public static int Used(ItemData? item, int count) => item is null || count <= 0
        ? 0 : (int)Math.Min(int.MaxValue, (long)ItemVolume(item) * count);

    public static int Used(ArtifactInstanceData? artifact) => ArtifactVolume(artifact);

    public static int Capacity(int width, int height) => Math.Max(0, width) * Math.Max(0, height);
}
