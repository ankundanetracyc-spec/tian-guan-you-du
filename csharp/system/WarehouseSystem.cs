using Godot;
using Godot.Collections;

/// <summary>
/// 旧仓库接口的兼容外壳。项目运行时不再创建共享仓库；若旧调用点尚未迁移，统一转发到当前玩家行囊。
/// </summary>
public partial class WarehouseSystem : Node
{
    [Signal] public delegate void contents_changedEventHandler();

    private Character? player => GetTree().GetFirstNodeInGroup("main_scene") is MainScene main ? main.get_player_character() : null;

    public int get_slot_count() => player?.get_inventory_snapshot().Count ?? 0;
    public bool is_empty() => get_slot_count() == 0;
    public string get_item_path(int slot_index)
    {
        var snapshot = player?.get_inventory_snapshot();
        return snapshot is not null && slot_index >= 0 && slot_index < snapshot.Count ? snapshot[slot_index].GetValueOrDefault("item_path", "").AsString() : "";
    }
    public int get_item_count(int slot_index)
    {
        var snapshot = player?.get_inventory_snapshot();
        return snapshot is not null && slot_index >= 0 && slot_index < snapshot.Count ? snapshot[slot_index].GetValueOrDefault("count", 0).AsInt32() : 0;
    }
    public Array<Dictionary> get_snapshot() => player?.get_inventory_snapshot() ?? new Array<Dictionary>();
    public bool add_item(string item_path, int count = 1)
    {
        var added = player?.add_item_to_inventory(item_path, count) ?? false;
        if (added) EmitSignal(SignalName.contents_changed);
        return added;
    }
    public bool remove_item(string item_path, int count = 1)
    {
        var removed = player?.remove_item_from_inventory(item_path, count) ?? false;
        if (removed) EmitSignal(SignalName.contents_changed);
        return removed;
    }
    public int get_total_count(string item_path) => player?.get_inventory_total_count(item_path) ?? 0;
    public bool has_item(string item_path, int count = 1) => player?.has_inventory_item(item_path, count) ?? false;
    public void clear_all()
    {
        if (player is null) return;
        var snapshot = player.get_inventory_snapshot();
        var changed = false;
        foreach (var entry in snapshot)
        {
            var path = entry.GetValueOrDefault("item_path", "").AsString();
            var count = entry.GetValueOrDefault("count", 0).AsInt32();
            if (player.remove_item_from_inventory(path, count)) changed = true;
        }
        if (changed) EmitSignal(SignalName.contents_changed);
    }
}
