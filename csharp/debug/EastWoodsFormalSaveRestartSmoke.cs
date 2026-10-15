using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// Cross-process acceptance of the formal save slot while inside East Woods.
/// The main scene is the active scene root; the bootstrap loads the read slot
/// before MainScene initializes. Both phases use the same isolated APPDATA root.
/// </summary>
public partial class EastWoodsFormalSaveRestartSmoke : Node
{
    private const string LayoutSeed = "formal-restart-east-woods-20261013";
    private const string SaveName = "东林地跨进程验收-资源点版";
    private const string MineralProfileId = "spirit_stone_vein";
    private const string MineralActionId = "mine_spirit_stone";
    private const string MineralItemPath = "res://resources/entities/items/灵石.tres";
    private const string RecoveryStateKey = "__east_woods_ecology";
    private const string ResourceCursorKey = "resource_cursors";
    private const string BeastCursorKey = "ordinary_beast_cursor_hour";
    private const string BeastRecordsKey = "ordinary_beast_encounters";
    private const string DutyInstanceId = "formal_restart_patrol_instance_20261013";
    private const int CommonResourcePeriodHours = 7 * 24;
    private const int MineralResourcePeriodHours = 90 * 24;
    private const int ResourceElapsedHours = 63;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static object? Invoke(MainScene target, string method, params object[] args)
    {
        var info = typeof(MainScene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Check(info is not null, $"找不到 MainScene.{method}。");
        return info!.Invoke(target, args);
    }

    private static object? ReadField(object target, string name)
    {
        var info = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Check(info is not null, $"找不到字段 {name}。");
        return info!.GetValue(target);
    }

    private static void WriteField(object target, string name, object value)
    {
        var info = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Check(info is not null, $"找不到字段 {name}。");
        info!.SetValue(target, value);
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static Dictionary<string, string> SnapshotSaveFiles(string savesPath)
    {
        if (!Directory.Exists(savesPath)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(savesPath, "*.tres")
            .ToDictionary(path => Path.GetFileName(path) ?? throw new InvalidOperationException($"无法取得存档文件名：{path}"),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string UserArgument(string prefix)
        => OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? "";

    private static string VerifyIsolatedUserDataDirectory()
    {
        var expectedAppData = UserArgument("--expected-appdata=");
        Check(!string.IsNullOrWhiteSpace(expectedAppData), "缺少临时 APPDATA 参数；拒绝触碰默认用户存档目录。");
        Check(string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase),
            "Godot 进程的 APPDATA 与本次临时目录不一致；拒绝写档。");

        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        Check(!string.IsNullOrWhiteSpace(customName), "项目没有配置独立 user:// 目录名；拒绝写档。");
        var expectedUserData = NormalizePath(Path.Combine(expectedAppData, customName));
        var actualUserData = NormalizePath(OS.GetUserDataDir());
        Check(string.Equals(actualUserData, expectedUserData, StringComparison.OrdinalIgnoreCase),
            $"user:// 未解析到隔离目录：期望 {expectedUserData}，实际 {actualUserData}；拒绝写档。");

        var savesPath = NormalizePath(ProjectSettings.GlobalizePath("user://game_saves"));
        Check(savesPath.StartsWith(actualUserData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            $"正式存档目录越出隔离 user://：{savesPath}。");
        GD.Print($"EAST_WOODS_RESTART_ISOLATED_USER_DATA: {actualUserData}");
        return savesPath;
    }

    private async Task Frames(int count = 8)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    public override async void _Ready()
    {
        try
        {
            await Frames(1);
            var main = GetTree().CurrentScene as MainScene;
            Check(main is not null && main == GetParent(), "验收场景必须以正式 MainScene 作为当前场景根节点。");
            var savesPath = VerifyIsolatedUserDataDirectory();
            var phase = UserArgument("--phase=");
            if (phase == "write") await WritePhase(savesPath, main!);
            else if (phase == "read") await ReadPhase(savesPath, main!);
            else throw new InvalidOperationException("请提供 --phase=write 或 --phase=read。");
        }
        catch (Exception error)
        {
            GD.PushError("EAST_WOODS_FORMAL_SAVE_RESTART_FAILED: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task WritePhase(string savesPath, MainScene main)
    {
        var originalSaveFiles = SnapshotSaveFiles(savesPath);
        var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
        Check(!saveManager.get_save_slots().Any(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName),
            "隔离目录中已存在同名验收槽；为避免覆盖，拒绝继续。");
        saveManager.begin_new_game(SaveName);

        var player = ReadField(main, "player") as Character;
        Check(player?.entity_data is HumanData, "正式主场景未创建可保存主角。");
        var human = (HumanData)player!.entity_data!;

        var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
        time.restore_calendar(2, 1, 15, 10, 0.375f);
        var currentHour = time.get_total_game_hours();
        Check(currentHour > MineralResourcePeriodHours, "测试日历未达到90日矿脉周期验收时刻。");

        var previousMineralCount = player.get_inventory_total_count(MineralItemPath);
        if (previousMineralCount > 0)
            Check(player.remove_item_from_inventory(MineralItemPath, previousMineralCount), "无法准备隔离的灵石行囊样本。");
        Check(player.add_item_to_inventory(MineralItemPath, 2), "无法将灵石写入验收角色行囊。");
        Check(player.get_inventory_total_count(MineralItemPath) == 2, "写档前灵石行囊样本数量不正确。");

        human.sect_affiliation_id = "yuxu_xianzong";
        human.sect_current_rank = "外门弟子";
        human.sect_active_yuxu_duty_id = "east_woods_patrol";
        human.sect_active_yuxu_duty_instance_id = DutyInstanceId;
        human.sect_active_yuxu_duty_started_hour = currentHour - 24;
        human.sect_active_yuxu_duty_stage = 3;
        human.sect_active_yuxu_duty_ready = false;

        EastWoodsDungeonRules.generate_layout(LayoutSeed);
        var layout = EastWoodsDungeonRules.get_layout_state();
        var mineralNodeId = EastWoodsActions.MineralPointNodeId();
        var mineralNode = EastWoodsDungeonRules.get_node(mineralNodeId);
        var mineralAction = EastWoodsActions.Content(mineralNodeId);
        Check(mineralNode is { ResourceCategory: EastWoodsResourceCategory.Mineral, ResourceNodeId: MineralProfileId } &&
              mineralAction is { Id: MineralActionId, ItemPath: MineralItemPath, Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral },
            "种子地图未提供类别匹配的专用灵石矿脉与既有矿采行动。");

        var commonResourceKey = EastWoodsEcology.ActionProgressKey("fern_lowland", "gather_root");
        var mineralResourceKey = EastWoodsEcology.ActionProgressKey(mineralNodeId, MineralActionId);
        var actions = new GDictionary
        {
            [commonResourceKey] = 2,
            [mineralResourceKey] = 1,
            [EastWoodsEcology.ActionProgressKey("north_turn", "old_trap")] = 1,
            [EastWoodsEcology.ActionProgressKey("outer_13_06", "wolf")] = 1,
            [RecoveryStateKey] = new GDictionary
            {
                [ResourceCursorKey] = new GDictionary
                {
                    [commonResourceKey] = currentHour - ResourceElapsedHours,
                    [mineralResourceKey] = currentHour - ResourceElapsedHours,
                },
                [BeastCursorKey] = currentHour - 14 * 24,
                ["next_beast_identity"] = 1,
                [BeastRecordsKey] = new GArray(),
                ["pending_beast_checks"] = new GDictionary(),
            },
        };
        var route = new GDictionary
        {
            ["current"] = mineralNodeId,
            ["visited"] = new Godot.Collections.Array<string> { "forest_edge", mineralNodeId, "north_turn" },
            ["discovered"] = new Godot.Collections.Array<string> { "forest_edge", mineralNodeId, "north_turn", "outer_13_06" },
            ["actions"] = actions,
            ["inside"] = true,
            ["layout"] = layout,
        };

        WriteField(main, "_player_world_node_id", "east_woods");
        WriteField(main, "_east_woods_dungeon_state", route);
        Invoke(main, "open_east_woods_dungeon");
        await Frames(8);

        var panel = ReadField(main, "_east_woods_dungeon") as EastWoodsDungeonPanel;
        Check(panel is { Visible: true }, "未能进入正式东林地副本界面。");
        var liveRoute = panel!.get_route_state();
        var liveActions = liveRoute.GetValueOrDefault("actions", new GDictionary()).AsGodotDictionary();
        var ecology = liveActions.GetValueOrDefault(RecoveryStateKey, new GDictionary()).AsGodotDictionary();
        var encounterRecords = ecology.GetValueOrDefault(BeastRecordsKey, new GArray()).AsGodotArray();
        Check(encounterRecords.Count == 1 && encounterRecords[0].AsGodotDictionary().GetValueOrDefault("encounter_id", "").AsString() == "ewb_000001",
            "准备保存的普通遭遇 ID 不符合预期。");
        Check(liveActions.GetValueOrDefault(EastWoodsEcology.ActionProgressKey("north_turn", "old_trap"), 0).AsInt32() == 1,
            "准备保存的一次性事件未标记完成。");
        Check(panel.get_current_route_node_id() == mineralNodeId && panel.action_remaining(mineralNodeId, mineralAction!) == 1,
            "准备保存的矿脉位置或剩余储量不符合预期。");
        var savedLayoutNode = layout["nodes"].AsGodotArray()
            .Select(value => value.AsGodotDictionary())
            .Single(node => node.GetValueOrDefault("id", "").AsString() == mineralNodeId);
        Check(savedLayoutNode.GetValueOrDefault("resource_category", 0).AsInt32() == (int)EastWoodsResourceCategory.Mineral &&
              savedLayoutNode.GetValueOrDefault("resource_node_id", "").AsString() == MineralProfileId,
            "写档前完整地图快照没有序列化矿脉类别和资源 ID。");

        var activeTask = Invoke(main, "get_active_yuxu_journal_task") as JournalPanel.Entry;
        Check(activeTask is { Title: "东林地巡查", State: "进行中" } && activeTask.Id == "yuxu.duty." + DutyInstanceId,
            "准备保存的宗门任务未处于指定进行中状态。");

        var saveMethod = typeof(MainScene).GetMethod("save_progress_now", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(saveMethod is not null && saveMethod.Invoke(main, new object[] { false }) is true,
            "正式 MainScene 存档流程未成功写入当前槽。");
        Check(!originalSaveFiles.ContainsKey(saveManager.active_slot_id + ".tres"), "新验收槽与既有临时槽发生 ID 冲突。");
        var filesAfterWrite = SnapshotSaveFiles(savesPath);
        foreach (var original in originalSaveFiles)
            Check(filesAfterWrite.TryGetValue(original.Key, out var hash) && hash == original.Value,
                $"写入新验收槽时改动了既有隔离文件 {original.Key}。");
        var slots = saveManager.get_save_slots();
        Check(slots.Any(slot => slot.GetValueOrDefault("id", "").AsString() == saveManager.active_slot_id &&
                                slot.GetValueOrDefault("name", "").AsString() == SaveName),
            "正式存档槽没有列出新建资源点验收旅程。");

        GD.Print($"EAST_WOODS_FORMAL_SAVE_WRITE_PASS: slot={saveManager.active_slot_id}; date={time.year}-{time.month}-{time.day} {time.hour}:00 + {time.get_pending_hours():F3}h; seed={LayoutSeed}; mineral={mineralNodeId}/{MineralProfileId} remaining=1; player_inventory=spirit_stone x2; mineral_recovery_in={MineralResourcePeriodHours - ResourceElapsedHours}h; common_recovery_in={CommonResourcePeriodHours - ResourceElapsedHours}h; encounter=ewb_000001; event=north_turn/old_trap completed; duty={DutyInstanceId} stage=3/ongoing; preserved_existing_slots={originalSaveFiles.Count}");
        GetTree().Quit();
    }

    private async Task ReadPhase(string savesPath, MainScene main)
    {
        Check(Directory.Exists(savesPath), "新进程未找到隔离临时槽目录。");
        var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
        var slots = saveManager.get_save_slots();
        var matchingSlots = slots.Where(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName).ToArray();
        Check(matchingSlots.Length == 1, $"隔离目录应有且仅有一个资源点验收槽，实际为 {matchingSlots.Length} 个。");
        var slotId = matchingSlots[0].GetValueOrDefault("id", "").AsString();
        Check(!string.IsNullOrWhiteSpace(slotId) && saveManager.active_slot_id == slotId,
            "重启引导器没有在正式 MainScene 初始化前载入指定验收槽。");
        await Frames(12);

        var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
        Check(time.year == 2 && time.month == 1 && time.day == 15 && time.hour == 10 && Math.Abs(time.get_pending_hours() - 0.375f) < 0.001f,
            $"正式读档后日历不一致：{time.year}-{time.month}-{time.day} {time.hour}:00 + {time.get_pending_hours():F3}h。");
        Check(EastWoodsDungeonRules.LayoutSeed == LayoutSeed, $"跨进程读档后地图种子改变：{EastWoodsDungeonRules.LayoutSeed}。");

        var panel = ReadField(main, "_east_woods_dungeon") as EastWoodsDungeonPanel;
        Check(panel is { Visible: true }, "读档没有恢复到副本内界面。");
        var route = panel!.get_route_state();
        var mineralNodeId = EastWoodsActions.MineralPointNodeId();
        var mineralNode = EastWoodsDungeonRules.get_node(mineralNodeId);
        var mineralAction = EastWoodsActions.Content(mineralNodeId);
        Check(route.GetValueOrDefault("inside", false).AsBool() && route.GetValueOrDefault("current", "").AsString() == mineralNodeId,
            "副本内状态或当前矿脉节点未恢复。");
        var storedLayout = route.GetValueOrDefault("layout", new GDictionary()).AsGodotDictionary();
        var storedLayoutJson = Json.Stringify(storedLayout);
        EastWoodsDungeonRules.generate_layout(LayoutSeed);
        Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state()) == storedLayoutJson,
            "正式存档恢复的完整地图格图、连线或资源类别与该种子不一致。");
        EastWoodsDungeonRules.restore_layout(storedLayout);

        var actions = route.GetValueOrDefault("actions", new GDictionary()).AsGodotDictionary();
        Check(mineralNode is { ResourceCategory: EastWoodsResourceCategory.Mineral, ResourceNodeId: MineralProfileId } &&
              mineralAction is { Id: MineralActionId, ItemPath: MineralItemPath, Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral } &&
              panel.action_remaining(mineralNodeId, mineralAction!) == 1,
            "正式读档后矿脉类别、行动或剩余储量不一致。");
        var player = ReadField(main, "player") as Character;
        Check(player is not null && player.get_inventory_total_count(MineralItemPath) == 2,
            "正式读档后角色行囊中的灵石数量没有保留。");

        var ecology = actions.GetValueOrDefault(RecoveryStateKey, new GDictionary()).AsGodotDictionary();
        var resourceCursors = ecology.GetValueOrDefault(ResourceCursorKey, new GDictionary()).AsGodotDictionary();
        var mineralResourceKey = EastWoodsEcology.ActionProgressKey(mineralNodeId, MineralActionId);
        var savedHour = time.get_total_game_hours();
        var cursorHour = resourceCursors.GetValueOrDefault(mineralResourceKey, -1).AsInt32();
        Check(cursorHour >= 0 && savedHour >= cursorHour, "读档后的矿脉恢复游标缺失或晚于游戏时刻。");
        var elapsed = (savedHour - cursorHour) % MineralResourcePeriodHours;
        var recoveryIn = MineralResourcePeriodHours - elapsed;
        Check(elapsed == ResourceElapsedHours && recoveryIn == MineralResourcePeriodHours - ResourceElapsedHours,
            $"读档后的90日矿脉周期余时不一致：应为 {MineralResourcePeriodHours - ResourceElapsedHours}h，实际 {recoveryIn}h。");

        var encounterRecords = ecology.GetValueOrDefault(BeastRecordsKey, new GArray()).AsGodotArray();
        var activeEncounters = encounterRecords
            .Where(value => value.VariantType == Variant.Type.Dictionary && value.AsGodotDictionary().GetValueOrDefault("status", "active").AsString() == "active")
            .Select(value => value.AsGodotDictionary())
            .ToArray();
        Check(activeEncounters.Length == 1 && activeEncounters[0].GetValueOrDefault("encounter_id", "").AsString() == "ewb_000001" &&
              activeEncounters[0].GetValueOrDefault("action_id", "").AsString() == "wolf_recovery_000001",
            "读档后的普通遭遇 ID 丢失、改变或重复生成。");
        Check(actions.GetValueOrDefault(EastWoodsEcology.ActionProgressKey("north_turn", "old_trap"), 0).AsInt32() == 1 &&
              panel.action_remaining("north_turn", EastWoodsActions.Content("north_turn")!) == 0,
            "读档后一次性陷坑事件进度丢失或可再次触发。");

        Check(player?.entity_data is HumanData { } human &&
              human.sect_active_yuxu_duty_id == "east_woods_patrol" &&
              human.sect_active_yuxu_duty_instance_id == DutyInstanceId &&
              human.sect_active_yuxu_duty_stage == 3 && !human.sect_active_yuxu_duty_ready &&
              human.sect_active_yuxu_duty_started_hour == savedHour - 24,
            "正式读档后宗门巡查任务的实例、阶段或回报状态改变。");
        var activeTask = Invoke(main, "get_active_yuxu_journal_task") as JournalPanel.Entry;
        Check(activeTask is { Title: "东林地巡查", State: "进行中" } && activeTask.Id == "yuxu.duty." + DutyInstanceId,
            "读档后任务栏没有恢复相同的进行中任务。");

        GD.Print($"EAST_WOODS_FORMAL_SAVE_RESTART_PASS: fresh-process slot={slotId}; calendar={time.year}-{time.month}-{time.day} {time.hour}:00 + {time.get_pending_hours():F3}h; seed={EastWoodsDungeonRules.LayoutSeed}; layout=128 nodes and saved edges restored; mineral={mineralNodeId}/{MineralProfileId} category=Mineral remaining=1; player_inventory=spirit_stone x2; mineral_recovery_in={recoveryIn}h with cursor={cursorHour}; encounter=ewb_000001 exactly once; event=north_turn/old_trap completed; duty={DutyInstanceId} stage=3/ongoing");
        GetTree().Quit();
    }
}
