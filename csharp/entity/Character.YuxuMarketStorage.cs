using System;
using System.Linq;

public partial class Character
{
    internal bool try_remove_one_market_storage_item(string containerId, string instanceId, string path, Func<bool> beforeCommit)
    {
        if (human is null || _spatialCommitting || string.IsNullOrWhiteSpace(containerId)) return false;
        var entry = get_storage_bag_inventory(containerId).FirstOrDefault(item => item.InstanceId == instanceId);
        if (entry is null || entry.Item is null || entry.ItemPath != path || entry.Count < 1 || !can_store_spatial_item(instanceId)) return false;
        var contents = human.storage_bag_contents.Duplicate();
        int bagIndex = contents.ToList().FindIndex(item => item.bag_instance_id == containerId);
        if (bagIndex < 0) return false;
        var original = contents[bagIndex];
        var bag = (StorageBagContentsData)original.Duplicate(false);
        bag.items = new Godot.Collections.Array<StorageBagStoredItemData>(
            original.items.Select(item => (StorageBagStoredItemData)item.Duplicate(true)));
        contents[bagIndex] = bag;
        var index = bag.items.ToList().FindIndex(item => item.instance_id == instanceId);
        if (index < 0 || bag.items[index].count != entry.Count) return false;
        if (entry.Count == 1) bag.items.RemoveAt(index);
        else bag.items[index].count--;
        _spatialCommitting = true;
        try
        {
            if (!beforeCommit()) return false;
            human.storage_bag_contents = contents;
        }
        finally { _spatialCommitting = false; }
        notify_inventory_changed();
        return true;
    }
}
