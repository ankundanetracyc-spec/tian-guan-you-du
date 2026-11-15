using Godot;
using Godot.Collections;

/// <summary>已解锁盘面及摆放配置。旧槽位备份仅用于核对，不作为第二套战斗加成。</summary>
public partial class CombatBoardLayout : Resource
{
    public const int CurrentLayoutVersion = 2;
    [Export] public int layout_version = CurrentLayoutVersion;
    [Export] public Vector2I size = new(2, 4);
    /// <summary>留空表示旧盘／固定盘的外框格均可用；人物成长盘显式保存已解锁坐标。</summary>
    [Export] public Array<Vector2I> unlocked_cells = [];
    [Export] public bool confirmed;
    [Export] public bool migrated_from_legacy;
    [Export] public string preset_id = "";
    [Export] public string preset_name = "";
    public string Title { get => string.IsNullOrWhiteSpace(preset_name) ? ResourceName : preset_name; set { preset_name = value; ResourceName = value; } }
    [Export] public Array<CombatBoardPlacement> placements = [];
    [Export] public Array<CombatBoardPendingEntry> pending_entries = [];
    [Export] public Array<string> legacy_skill_keys = [];
    [Export] public Godot.Collections.Dictionary<string, string> legacy_rewrite_bindings = new();

    public CombatBoardLayout CopyConfiguration() => new()
    {
        ResourceName = Title,
        preset_name = Title, preset_id = preset_id,
        layout_version = layout_version,
        size = size,
        unlocked_cells = unlocked_cells.Duplicate(),
        confirmed = confirmed,
        migrated_from_legacy = migrated_from_legacy,
        placements = new(placements.Select(item => item.CopyConfiguration())),
        pending_entries = new(pending_entries.Select(item => new CombatBoardPendingEntry
        { source_key = item.source_key, original_target_key = item.original_target_key, legacy_slot_index = item.legacy_slot_index, reason = item.reason })),
        legacy_skill_keys = legacy_skill_keys.Duplicate(),
        legacy_rewrite_bindings = legacy_rewrite_bindings.Duplicate()
    };
}
