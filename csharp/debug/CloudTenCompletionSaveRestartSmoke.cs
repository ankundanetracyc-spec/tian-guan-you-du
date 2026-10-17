using Godot;
using Godot.Collections;
using System;
using System.Linq;
using System.IO;

/// <summary>Fresh-start Cloud Breath completion and cross-process formal-slot round-trip for completed Yuanxi.</summary>
public partial class CloudTenCompletionSaveRestartSmoke : Node
{
    private const string SaveName = "云息十层归元九息闭环验收";
    private const string AppDataArgument = "--expected-appdata=";
    private const int ActivityWindowHoursPerMonth = 15 * 24;
    private const int CultivationWindowHoursPerMonth = 15 * 24;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string UserArgument(string prefix)
        => OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? "";

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void VerifyIsolatedUserData()
    {
        var expectedAppData = UserArgument(AppDataArgument);
        Check(!string.IsNullOrWhiteSpace(expectedAppData), "缺少隔离 APPDATA 参数，拒绝访问用户默认存档目录。");
        Check(string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase),
            "当前进程 APPDATA 与隔离目录不一致，拒绝继续。");
        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        var expectedUserData = NormalizePath(Path.Combine(expectedAppData, customName));
        Check(string.Equals(NormalizePath(OS.GetUserDataDir()), expectedUserData, StringComparison.OrdinalIgnoreCase),
            "user:// 没有指向隔离目录，拒绝继续。");
    }

    public override void _Ready()
    {
        try
        {
            VerifyIsolatedUserData();
            var phase = UserArgument("--phase=");
            if (phase == "write") write_endpoint_save();
            else if (phase == "read") read_endpoint_save();
            else throw new InvalidOperationException("请提供 --phase=write 或 --phase=read。");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("CLOUD_TEN_COMPLETION_SAVE_RESTART_FAILED: " + error);
            GetTree().Quit(1);
        }
    }

    private void write_endpoint_save()
    {
        var manager = GetSaveManager();
        Check(!manager.get_save_slots().Any(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName),
            "隔离目录已有同名云息圆满槽，为避免覆盖拒绝继续。");
        manager.begin_new_game(SaveName);

        var time = GetGameTime();
        Check(time.get_total_game_hours() == 0, "新旅程未从第一日零时开始。");
        var journeyStartHour = time.get_total_game_hours();

        var tree = GetTree();
        var profile = tree.Root.GetNodeOrNull<GameStartProfile>("GameStartProfile")
            ?? throw new InvalidOperationException("GameStartProfile autoload 不可用。");
        profile.player_name = "云息闭环验收者";
        profile.fated_youngest_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER;
        profile.fated_youngest_root_name = "金木水三灵根";
        profile.fated_youngest_element = 1;
        profile.fated_youngest_root_weights = [34, 33, 33, 0, 0];
        var playerData = new HumanData
        {
            cultivation_realm = "凡人",
            ambient_lingqi_pressure = 100f,
            current_world_stamina = WorldStaminaRules.InitialCapacity,
            max_world_stamina = WorldStaminaRules.InitialCapacity,
        };
        profile.apply_to_youngest(playerData);
        var player = new Character { Name = "CloudTenJourneyPlayer", entity_data = playerData };
        AddChild(player);
        Check(player.IsInsideTree() && GetRealmManager() is not null,
            "新旅程角色未进入可访问境界系统的场景树。");
        var main = new EastWoodsActionHarness { Name = "FreeStartHarness" };
        AddChild(main);
        var mainType = typeof(MainScene);
        mainType.GetField("player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(main, player);
        mainType.GetMethod("initialize_story_free_start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(main, null);
        mainType.GetMethod("show_story_free_start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(main, null);
        var startedWithoutStory = (bool)mainType.GetMethod("has_open_world_access", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(main, null)!;
        var startNodeId = mainType.GetField("_player_world_node_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(main) as string ?? "";
        Check(startedWithoutStory &&
              mainType.GetField("_story_free_start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(main) is true &&
              startNodeId == "white_stone_town" &&
              playerData.intro_gongfa_path == profile.selected_initial_gongfa_path,
            "正式无剧情开局没有落在白石镇，或未保留开局功法参考。");
        Check(player.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 1 &&
              player.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 1,
            "新开局的关口丹数量不正确。");

        var initialGongfa = ResourceLoader.Load<GongfaResource>(profile.selected_initial_gongfa_path)
            ?? throw new InvalidOperationException("开局选择的功法无法加载。");
        Check(player.get_pending_intro_gongfa()?.ResourcePath == initialGongfa.ResourcePath,
            "白石镇无剧情开局没有保留所选初始功法。");
        var firstChapter = initialGongfa.get_sorted_chapters().FirstOrDefault()
            ?? throw new InvalidOperationException("初始功法没有首卷。");
        for (var attempt = 0; attempt < 4 && !player.has_completed_intro_gongfa_first_volume(); attempt++)
        {
            player.current_world_stamina = player.max_world_stamina;
            var study = player.study_for_hours(24);
            Check(study.GetValueOrDefault("success", false).AsBool(), "新开局不能研读所选初始功法首卷。");
        }
        Check(player.has_completed_intro_gongfa_first_volume() &&
              player.is_gongfa_chapter_learned(initialGongfa, firstChapter.get_id()) &&
              player.equip_main_gongfa(initialGongfa),
            "首卷研修完成后不能装备功法开始修炼。");
        time.add_hours(Mathf.CeilToInt(player.max_world_stamina - player.current_world_stamina));
        player.restore_world_stamina(player.max_world_stamina);

        var campaignMonths = 0;
        var condensationCount = 0;
        var refinementUses = 0;
        var activityEventCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        while (player.cultivation_realm != "云息十层" && campaignMonths < 2400)
        {
            set_practice_environment(player);
            simulate_cultivation_window(player, time, ref refinementUses, ref condensationCount);
            if (player.cultivation_realm == "云息十层")
                break;
            simulate_activity_window(player, time, campaignMonths, activityEventCounts, ref refinementUses);
            campaignMonths++;
        }
        Check(player.cultivation_realm == "云息十层", "真实修炼流程未能在 200 年内到达云息十层。");
        var cloudTenHour = time.get_total_game_hours();
        Check(player.has_spirit_core && player.core_count == 1 && player.yuanxi_count == 0,
            "六至七层凝核关口没有通过当前凝核写回逻辑。");
        Check(player.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 0 &&
              player.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 0,
            "三至四或九至十关口未在成功晋层时消耗对应丹药。");
        set_practice_environment(player);
        Check(player.start_yuanxi(9), "云息十层不能开始归元九息。");
        player.advance_yuanxi(9504);
        Check(player.is_yunxi_complete() && !player.is_yuanxi_active() &&
              time.get_total_game_hours() - cloudTenHour == 9504,
            "真实归元闭关没有按 9504 小时完成九息。");
        var totalHours = time.get_total_game_hours() - journeyStartHour;
        var totalYears = totalHours / (double)CultivationBottleneckRules.HoursPerGameYear;
        Check(totalYears is >= 12d and <= 15d,
            $"三灵根在确定性行动日历下的实际角色流程耗时 {totalYears:F3} 年，未进入 12–15 年区间。");

        EastWoodsDungeonRules.generate_layout("cloud-ten-completion-save-v1");
        var layoutState = EastWoodsDungeonRules.get_layout_state();
        var save = new PersistentGameSave
        {
            player_data = player.entity_data?.Duplicate(true) as HumanData
                ?? throw new InvalidOperationException("无法复制云息圆满角色数据。"),
            current_node_id = startNodeId,
            year = time.year,
            month = time.month,
            day = time.day,
            hour = time.hour,
            pending_hours = time.get_pending_hours(),
            scene_progress = new Dictionary
            {
                ["east_woods_dungeon_state"] = new Dictionary { ["inside"] = false, ["layout"] = layoutState },
            },
            dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(time.get_total_game_hours()),
        };
        Check(manager.save_snapshot(save), "正式存档管理器未能保存云息圆满端点。");
        Check(manager.get_save_slots().Any(slot => slot.GetValueOrDefault("id", "").AsString() == manager.active_slot_id &&
            slot.GetValueOrDefault("name", "").AsString() == SaveName), "云息圆满端点没有进入正式存档槽列表。");
        GD.Print($"CLOUD_TEN_COMPLETION_SAVE_WRITE_PASS: slot={manager.active_slot_id}; date={time.year}-{time.month}-{time.day}; realm=云息十层; yuanxi=9/9; elapsed={totalHours}h; months={campaignMonths}; refinements={refinementUses}; condensation={condensationCount}; sources={string.Join(",", activityEventCounts.Select(pair => $"{pair.Key}:{pair.Value}"))}");
    }

    private static void read_endpoint_save()
    {
        var manager = GetSaveManager();
        var matchingSlots = manager.get_save_slots()
            .Where(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName)
            .ToArray();
        Check(matchingSlots.Length == 1, $"隔离目录应有且仅有一个圆满端点存档，实际为 {matchingSlots.Length} 个。");
        var slotId = matchingSlots[0].GetValueOrDefault("id", "").AsString();
        Check(manager.load_slot(slotId), "新进程未能通过正式存档管理器载入云息圆满端点。");
        var save = manager.consume_pending_load();
        Check(save is { format_version: PersistentGameSave.CurrentFormatVersion },
            "正式存档没有恢复当前格式版本。");
        var savedTotalHours = (((long)save!.year - 1) * 360 + ((long)save.month - 1) * 30 + save.day - 1) * 24 + save.hour;
        var savedYears = savedTotalHours / (double)CultivationBottleneckRules.HoursPerGameYear;
        Check(savedYears is >= 12d and <= 15d && save.current_node_id == "white_stone_town",
            $"正式新旅程存档没有恢复白石镇所在地点或 12–15 年端点历法（{savedYears:F3} 年）。");
        var player = save!.player_data ?? throw new InvalidOperationException("正式云息圆满槽缺少主角数据。");
        Check(player is { cultivation_realm: "云息十层", yuanxi_count: 9, yuanxi_target: 9, yuanxi_active: false,
                yuanxi_phase: 0, yuanxi_phase_hours: 0, yuanxi_notice: "归元九息已成，云息十层圆满。" },
            "新进程没有恢复云息十层与归元九息圆满状态。");
        Check(!player.inventory_item_paths.Contains(Character.YunxiThreeToFourPillPath) &&
              !player.inventory_item_paths.Contains(Character.YunxiNineToTenPillPath),
            "圆满端点行囊中仍存在已在关口消耗的两枚丹药。");
        Check(player.spirit_root_type == (int)GongfaResource.Wuxing.METAL_WOOD_WATER &&
              player.root_weights.Count == 5 &&
              Enumerable.Range(0, 5).Select(index => player.root_weights[index].AsInt32()).SequenceEqual([34, 33, 33, 0, 0]) &&
              player.intro_gongfa_path == "res://resources/gongfa/黄庭养形篇.tres" &&
              player.learned_gongfa_chapters.GetValueOrDefault(player.intro_gongfa_path, new Array<string>()).AsGodotArray()
                  .Contains(ResourceLoader.Load<GongfaResource>(player.intro_gongfa_path)!.get_sorted_chapters()[0].get_id()),
            "正式新旅程存档没有恢复普通三灵根与开局首卷修习结果。");
        Check(DynamicDungeonExpedition.ValidateState(save.dynamic_dungeon_state), "闭环端点的动态副本状态恢复无效。");

        var savePath = ProjectSettings.GlobalizePath($"user://game_saves/{slotId}.tres");
        DirAccess.RemoveAbsolute(savePath);
        GD.Print($"CLOUD_TEN_COMPLETION_SAVE_RESTART_PASS: fresh-process load; slot={slotId}; realm={player!.cultivation_realm}; yuanxi={player.yuanxi_count}/9; two_breakthrough_pills_consumed; format=v{save!.format_version}");
    }

    private static void set_practice_environment(Character player)
        => player.ambient_lingqi_pressure = player.get_realm_required_lingqi_pressure_for_ui();

    private static void simulate_cultivation_window(Character player, GameTimeMgr time, ref int refinementUses, ref int condensationCount)
    {
        var remaining = CultivationWindowHoursPerMonth;
        while (remaining > 0 && player.cultivation_realm != "云息十层")
        {
            set_practice_environment(player);
            refine_available_records(player, ref refinementUses);
            if (player.is_breakthrough_ready())
            {
                complete_ready_breakthrough(player, ref condensationCount);
                continue;
            }
            Check(player.can_cultivate(), $"{player.cultivation_realm} 不能继续修炼且没有可完成的关口。");

            var neededStrands = player.get_strands_until_next_realm();
            var hoursToGate = player.get_world_cultivation_preview_for_strands(neededStrands)
                .GetValueOrDefault("hours", 0).AsInt32();
            Check(neededStrands > 0 && hoursToGate > 0,
                $"{player.cultivation_realm} 找不到有效的下一层灵力门槛。");
            if (neededStrands > 0 && hoursToGate > 0 && hoursToGate <= remaining)
            {
                var result = player.cultivate_with_world_stamina_for_strands(neededStrands);
                Check(result.GetValueOrDefault("success", false).AsBool(), $"{player.cultivation_realm} 实际纳气至关口失败。");
                var spentHours = result.GetValueOrDefault("hours", 0).AsInt32();
                Check(spentHours > 0 && spentHours <= remaining, "突破关口吐纳用时异常。");
                remaining -= spentHours;
                refine_available_records(player, ref refinementUses);
                if (player.is_breakthrough_ready()) complete_ready_breakthrough(player, ref condensationCount);
            }
            else
            {
                var result = player.cultivate_with_world_stamina_for_hours(Math.Min(24, remaining));
                Check(result.GetValueOrDefault("success", false).AsBool(), $"{player.cultivation_realm} 的日常吐纳失败。");
                var spentHours = result.GetValueOrDefault("hours", 0).AsInt32();
                Check(spentHours > 0 && spentHours <= remaining, "日常吐纳用时异常。");
                remaining -= spentHours;
                refine_available_records(player, ref refinementUses);
            }
        }
        if (remaining > 0 && player.cultivation_realm == "云息十层") time.add_hours(remaining);
    }

    private static void complete_ready_breakthrough(Character player, ref int condensationCount)
    {
        var realms = GetRealmManager();
        var target = realms.get_next_realm_name(player.cultivation_realm);
        Check(player.is_breakthrough_ready() && !string.IsNullOrWhiteSpace(target),
            $"{player.cultivation_realm} 的关口条件没有满足。");
        if (target == "云息七层")
        {
            Check(player.requires_condensation_breakthrough() && player.complete_condensation(1f, 1f, 0f, "验收云息灵核"),
                "云息六至七层凝核实际结算失败。");
            condensationCount++;
            return;
        }
        Check(player.start_breakthrough_retreat(), $"进入{target}的实际闭关突破失败。");
        Check(player.cultivation_realm == target, $"突破闭关结束后未进入{target}。");
    }

    private static void simulate_activity_window(
        Character player, GameTimeMgr time, int monthIndex,
        System.Collections.Generic.Dictionary<string, int> eventCounts, ref int refinementUses)
    {
        var year = monthIndex / 12 + 1;
        var month = monthIndex % 12;
        var sources = new List<string> { "field.gathering", "journey.step", "sect.yaopu_watch" };
        if (month % 3 == 0) sources.AddRange(["combat.victory", "sect.official_duty"]);
        if (month % 2 == 0) sources.Add("study.progress");
        var elapsed = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            var eventHour = (index + 1) * ActivityWindowHoursPerMonth / (sources.Count + 1);
            var advance = eventHour - elapsed;
            if (advance > 0) time.add_hours(advance);
            elapsed = eventHour;
            var sourceId = sources[index];
            eventCounts[sourceId] = eventCounts.GetValueOrDefault(sourceId) + 1;
            var stableEventId = $"pacing:{sourceId}:{year}:{month + 1}:{index + 1}";
            player.try_grant_renewable_cultivation_inspiration(stableEventId, sourceId, out _, out _);
            refine_available_records(player, ref refinementUses);
        }
        if (elapsed < ActivityWindowHoursPerMonth) time.add_hours(ActivityWindowHoursPerMonth - elapsed);
    }

    private static void refine_available_records(Character player, ref int refinementUses)
    {
        var summary = player.get_cultivation_bottleneck_summary();
        if (!summary.GetValueOrDefault("applicable", false).AsBool() ||
            summary.GetValueOrDefault("level", 0).AsInt32() <= 0 ||
            summary.GetValueOrDefault("penalty", 0d).AsDouble() <= 0d)
            return;
        foreach (var record in player.get_cultivation_inspiration_records()
                     .OrderByDescending(record => record.quality_rank)
                     .ThenBy(record => record.expires_at_game_hour)
                     .ToArray())
            if (player.try_refine_cultivation_bottleneck(record.record_id, out _)) refinementUses++;
    }

    private static GameSaveManager GetSaveManager()
        => Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<GameSaveManager>("GameSaveManager") ?? throw new InvalidOperationException("GameSaveManager autoload 不可用。")
            : throw new InvalidOperationException("SceneTree 不可用。");

    private static GameTimeMgr GetGameTime()
        => Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<GameTimeMgr>("GameTimeManager") ?? throw new InvalidOperationException("GameTimeManager autoload 不可用。")
            : throw new InvalidOperationException("SceneTree 不可用。");

    private static RealmManager GetRealmManager()
        => Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<RealmManager>("RealmManager") ?? throw new InvalidOperationException("RealmManager autoload 不可用。")
            : throw new InvalidOperationException("SceneTree 不可用。");
}
