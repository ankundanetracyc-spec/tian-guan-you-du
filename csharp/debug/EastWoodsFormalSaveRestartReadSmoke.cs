using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Loads a formal East Woods save in a fresh process and verifies its persisted state.</summary>
public partial class EastWoodsFormalSaveRestartReadSmoke : Node
{
    private const string SaveName = "东林地跨进程验收-资源点版";
    private const string LayoutSeed = "formal-restart-east-woods-20261013";
    private const string MineralNodeId = "outer_13_00";
    private const string MineralProfileId = "spirit_stone_vein";
    private const string MineralActionId = "mine_spirit_stone";
    private const string MineralItemPath = "res://resources/entities/items/灵石.tres";
    private const string DutyInstanceId = "formal_restart_patrol_instance_20261013";
    private const string RecoveryStateKey = "__east_woods_ecology";
    private const string MineralProgressKey = "outer_13_00:mine_spirit_stone";
    private const int MineralResourcePeriodHours = 90 * 24;
    private const int ResourceElapsedHours = 63;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public override void _Ready()
    {
        try
        {
            var manager = GetNode<GameSaveManager>("/root/GameSaveManager");
            var matchingSlots = manager.get_save_slots()
                .Where(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName)
                .ToArray();
            Check(matchingSlots.Length == 1, $"隔离目录应有且仅有一个资源点验收槽，实际为 {matchingSlots.Length} 个。");
            var slotId = matchingSlots[0].GetValueOrDefault("id", "").AsString();
            Check(!string.IsNullOrWhiteSpace(slotId) && manager.load_slot(slotId), "新进程无法通过正式存档管理器载入资源点验收槽。");
            var save = manager.consume_pending_load();
            Check(save is not null && save.format_version == PersistentGameSave.CurrentFormatVersion,
                "正式存档未迁移至当前版本或没有返回存档数据。");

            Check(save!.year == 2 && save.month == 1 && save.day == 15 && save.hour == 10 && Math.Abs(save.pending_hours - 0.375f) < 0.001f,
                "正式读档后的日历快照不一致。");
            Check(save.current_node_id == "east_woods", "正式存档没有保留东林地世界位置。");
            Check(save.player_data is { } player && player.sect_affiliation_id == "yuxu_xianzong" &&
                  player.sect_current_rank == "外门弟子" && player.sect_active_yuxu_duty_id == "east_woods_patrol" &&
                  player.sect_active_yuxu_duty_instance_id == DutyInstanceId && player.sect_active_yuxu_duty_stage == 3 &&
                  !player.sect_active_yuxu_duty_ready,
                "正式读档没有保留角色身份与进行中的巡查进度。");
            var inventoryIndex = save.player_data!.inventory_item_paths.IndexOf(MineralItemPath);
            Check(inventoryIndex >= 0 && inventoryIndex < save.player_data.inventory_item_counts.Count &&
                  save.player_data.inventory_item_counts[inventoryIndex] == 2,
                "正式读档没有保留角色行囊中的灵石数量。");

            var route = save.scene_progress.GetValueOrDefault("east_woods_dungeon_state", new Dictionary()).AsGodotDictionary();
            Check(route.GetValueOrDefault("inside", false).AsBool() && route.GetValueOrDefault("current", "").AsString() == MineralNodeId,
                "正式存档没有保留副本内位置。");
            var layout = route.GetValueOrDefault("layout", new Dictionary()).AsGodotDictionary();
            EastWoodsDungeonRules.validate_layout(layout);
            EastWoodsDungeonRules.restore_layout(layout);
            Check(EastWoodsDungeonRules.LayoutSeed == LayoutSeed, $"读档后的地图种子改变：{EastWoodsDungeonRules.LayoutSeed}。");
            var savedLayoutJson = Json.Stringify(layout);
            EastWoodsDungeonRules.generate_layout(LayoutSeed);
            Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state()) == savedLayoutJson,
                "跨进程读档后的地图格图、连线或资源类别与原种子不一致。");
            EastWoodsDungeonRules.restore_layout(layout);

            var actions = route.GetValueOrDefault("actions", new Dictionary()).AsGodotDictionary();
            var mineralNode = EastWoodsDungeonRules.get_node(MineralNodeId);
            var mineralAction = EastWoodsActions.Content(MineralNodeId);
            Check(mineralNode is { ResourceCategory: EastWoodsResourceCategory.Mineral, ResourceNodeId: MineralProfileId } &&
                  mineralAction is { Id: MineralActionId, ItemPath: MineralItemPath, Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral } &&
                  actions.GetValueOrDefault(MineralProgressKey, 0).AsInt32() == 1,
                "正式读档后的矿脉类别、匹配采集行动或剩余量不一致。");
            Check(actions.GetValueOrDefault("north_turn:old_trap", 0).AsInt32() == 1,
                "正式读档后的一次性事件进度丢失或可以再次触发。");

            var ecology = actions.GetValueOrDefault(RecoveryStateKey, new Dictionary()).AsGodotDictionary();
            var cursors = ecology.GetValueOrDefault("resource_cursors", new Dictionary()).AsGodotDictionary();
            var cursorHour = cursors.GetValueOrDefault(MineralProgressKey, -1).AsInt32();
            var savedHour = (int)((save.year - 1) * 360L * 24 + (save.month - 1) * 30L * 24 + (save.day - 1) * 24 + save.hour);
            Check(cursorHour >= 0 && savedHour - cursorHour == ResourceElapsedHours &&
                  MineralResourcePeriodHours - (savedHour - cursorHour) == MineralResourcePeriodHours - ResourceElapsedHours,
                "正式读档后的90日矿脉恢复游标或余时不一致。");
            Check(DynamicDungeonExpedition.ValidateState(save.dynamic_dungeon_state), "正式读档后的动态副本状态无效。");

            var savePath = ProjectSettings.GlobalizePath($"user://game_saves/{slotId}.tres");
            DirAccess.RemoveAbsolute(savePath);
            GD.Print($"EAST_WOODS_FORMAL_SAVE_RESTART_PASS: fresh-process GameSaveManager load; calendar={save.year}-{save.month}-{save.day} {save.hour}:00 + {save.pending_hours:F3}h; seed={LayoutSeed}; mineral={MineralNodeId}/{MineralProfileId}; inventory=spirit_stone x2; recovery_in={MineralResourcePeriodHours - ResourceElapsedHours}h; duty={DutyInstanceId} stage=3/ongoing");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("EAST_WOODS_FORMAL_SAVE_RESTART_FAILED: " + error);
            GetTree().Quit(1);
        }
    }
}
