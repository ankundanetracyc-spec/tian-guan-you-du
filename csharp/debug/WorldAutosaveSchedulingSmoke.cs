using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>Real MainScene scheduling checks, using only a newly created disposable slot.</summary>
public partial class WorldAutosaveSchedulingSmoke : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, Private)!.GetValue(value)!;
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Private)!.SetValue(value, data);
    private static object? Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private)!.Invoke(value, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static PersistentGameSave Disk(string path) => ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep)!;

    public override async void _Ready()
    {
        string slotPath = "", hiddenPath = "";
        GameSaveManager? saves = null;
        MainScene? main = null;
        try
        {
            saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "测试不得使用现有玩家槽");
            saves.begin_new_game("自动存档调度回归");
            slotPath = ProjectSettings.GlobalizePath($"user://game_saves/{saves.active_slot_id}.tres");
            hiddenPath = Path.Combine(Path.GetDirectoryName(slotPath)!, ".pending_autosave_smoke.tres");
            var fixture = new PersistentGameSave {
                year = 5, month = 1, day = 8, hour = 9,
                current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166),
                npc_population_version = 1, npc_simulation_enabled = false, npc_last_processed_month = 48,
                player_data = new HumanData { entity_name = "调度回归", cultivation_realm = "云息三层", current_age = 20, current_world_stamina = 100 },
                scene_progress = new D { ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true, ["xuanjian_awakened"] = true } };
            var sourceNpc = new WorldNpcData { npc_id = "autosave_smoke", npc_name = "旧年行客", current_node_id = "yuxu_xianzong_core" };
            for (var i = 0; i < 40; i++) sourceNpc.action_history.Add(new NpcActionRecord { action_id = "rest", month_index = i, outcome = "旧记录" + i });
            fixture.world_npcs.Add(sourceNpc);
            Set(saves, "_pendingLoad", fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景加载失败");
            await Frames(12);
            main = (MainScene)GetTree().CurrentScene;
            main.SetProcess(false); // Drive the real autosave entry deterministically below.
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            var npc = GetNode<NpcWorldManager>("/root/NpcWorldManager").get_npc("autosave_smoke")!;
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var player = Read<Character>(main, "player");
            var map = Read<Control>(main, "tile_world_map");
            CheckLetterBoundaries(main, clock);
            Check((bool)Call(main, "save_progress_now", false)!, "初始保存失败");
            _ = Disk(slotPath); // Prime ResourceLoader before replacing the file.

            Call(main, "mark_save_dirty");
            Call(main, "process_save_autosave", 9999.0);
            Check(!saves.IsBackgroundSaveBusy, "游戏时间未推进却按现实时间自动保存");
            clock.advance_time(2160 - 1, "autosave_smoke");
            Call(main, "process_save_autosave", 2.0);
            Check(!saves.IsBackgroundSaveBusy, "未满三个月自动保存");
            clock.advance_time(1, "autosave_smoke");
            Call(main, "process_save_autosave", 2.0);
            Check(saves.IsBackgroundSaveBusy, "自动保存未启动后台写入");
            Check(!saves.begin_background_save(main.capture_battle_world_snapshot()!), "忙碌时允许重复写入");
            player.entity_data!.entity_name = "移动后的最新角色";
            npc.action_history[0].outcome = "旧历史已修改";
            npc.action_history.Add(new NpcActionRecord { action_id = "rest", month_index = 48, outcome = "新记录" });
            map.Call("set_player_cell", new Vector2I(35, 166));
            Call(main, "on_tile_world_player_cell_changed", new Vector2I(35, 166));
            clock.advance_time(1, "autosave_smoke");
            Call(main, "mark_save_dirty");
            await Drain(saves, main);
            Check(Read<bool>(main, "_save_is_dirty"), "写入期间的新变更被旧完成通知清除");
            var older = Disk(slotPath);
            Check(older.player_data!.entity_name != "移动后的最新角色" && older.world_npcs[0].action_history[0].outcome != "旧历史已修改", "后台快照混入捕获后的活数据");
            Call(main, "process_save_autosave", 9999.0);
            Check(!saves.IsBackgroundSaveBusy, "首次自动保存后未等待下个三个月");
            clock.advance_time(2160, "autosave_smoke");
            Call(main, "process_save_autosave", 2.0);
            await Drain(saves, main);
            Check(!Read<bool>(main, "_save_is_dirty"), "第二次保存后未清除 dirty");
            var latest = Disk(slotPath);
            Check(latest.player_data!.entity_name == "移动后的最新角色" && latest.world_cell == new Vector2I(35, 166) && latest.hour == clock.hour &&
                latest.world_npcs[0].action_history.Count == npc.action_history.Count && latest.world_npcs[0].action_history[0].outcome == "旧历史已修改", "第二次快照未保持坐标/历法/历史一致");

            clock.advance_time(2160 * 2, "autosave_smoke");
            Call(main, "mark_save_dirty"); Call(main, "process_save_autosave", 2.0);
            Check(saves.IsBackgroundSaveBusy, "手动保存前没有旧 writer");
            player.entity_data.entity_name = "手动保存最终值"; Call(main, "mark_save_dirty");
            Check((bool)Call(main, "save_progress_now", false)! && !saves.IsBackgroundSaveBusy, "手动保存未等待旧 writer");
            Check(saves.load_slot(saves.active_slot_id) && saves.consume_pending_load()!.player_data!.entity_name == "手动保存最终值", "load_slot 命中陈旧缓存或旧 writer 覆盖新值");
            Call(main, "mark_save_dirty");
            Call(main, "process_save_autosave", 9999.0);
            Check(!saves.IsBackgroundSaveBusy, "手动保存没有重置三个月间隔");
            clock.advance_time(2160, "autosave_smoke");

            // A locked destination gives a real write/replace failure without changing player files.
            using (var locked = new FileStream(slotPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                player.entity_data.entity_name = "失败后仍待保存"; Call(main, "mark_save_dirty");
                Call(main, "process_save_autosave", 2.0);
                Check(saves.IsBackgroundSaveBusy, "失败用例未启动 writer");
                await Drain(saves, main);
                Check(Read<bool>(main, "_save_is_dirty"), "写入失败清除了 dirty");
            }
            Check((bool)Call(main, "save_progress_now", false)!, "写入失败后不能重试");

            Check(ResourceSaver.Save(main.capture_battle_world_snapshot()!, hiddenPath) == Error.Ok, "临时文件样本创建失败");
            Check(!saves.get_save_slots().Any(item => item["id"].AsString().StartsWith(".pending_", StringComparison.Ordinal)), "临时文件出现在槽列表");
            player.entity_data.entity_name = "退出等待最终值";
            Check(saves.begin_background_save(main.capture_battle_world_snapshot()!), "退出用例未启动 writer");
            saves._ExitTree();
            Check(!saves.IsBackgroundSaveBusy && Disk(slotPath).player_data!.entity_name == "退出等待最终值", "退出未等待 writer 完成");
            GD.Print("WORLD_AUTOSAVE_SCHEDULING PASS: live scene, revision guard, detached history, next save, manual ordering, fresh load, busy guard, failure dirty, exit drain, hidden temporary slots");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); return; }
        finally
        {
            saves?.finish_background_save();
            if (main is not null) Set(main, "_skipExitSave", true);
            if (File.Exists(slotPath)) File.Delete(slotPath);
            if (File.Exists(hiddenPath)) File.Delete(hiddenPath);
        }
        GetTree().Quit();
    }

    private async Task Drain(GameSaveManager saves, MainScene main)
    {
        for (var frame = 0; frame < 600 && saves.IsBackgroundSaveBusy; frame++)
        {
            await Frames();
            Call(main, "process_save_autosave", 0.0);
        }
        Check(!saves.IsBackgroundSaveBusy, "后台保存超时");
    }

    private static void CheckLetterBoundaries(MainScene main, GameTimeMgr clock)
    {
        var mailbox = Read<LetterMailbox>(main, "_letterMailbox");
        var start = clock.get_total_game_hours();
        void At(int offset)
        {
            var absolute = start + offset;
            clock.restore_calendar(absolute / 8640 + 1, absolute / 720 % 12 + 1, absolute / 24 % 30 + 1, absolute % 24, 0);
            Call(main, "refresh_letter_invitation_tasks_if_changed");
        }
        bool Stamp(string text) => Read<string>(main, "_letterInvitationTaskStamp").Contains(text, StringComparison.Ordinal);
        var letter = new LetterData { letter_id = "schedule-first", source_event_id = "schedule-first", subject = "邀约", body = "调度测试", sent_at_hour = start,
            invitation = new LetterInvitationData { activity_key = "test", activity_name = "测试", place_name = "测试地点", reply_by_hour = start + 5, starts_at_hour = start + 10, ends_at_hour = start + 20 } };
        mailbox.letters.Add(letter); mailbox.EmitChanged();
        Check(mailbox.Reply(letter.letter_id, true, start) && Stamp("schedule-first:upcoming"), "Reply Changed 未立即更新 upcoming");
        Check(Read<int>(main, "_nextLetterInvitationBoundary") == start + 10, "未记录下个邀约边界");
        var cachedHour = Read<int>(main, "_letterInvitationStampHour");
        At(1);
        Check(Read<int>(main, "_letterInvitationStampHour") == cachedHour, "未过边界仍重扫信箱");
        At(10); Check(Stamp("schedule-first:active"), "开始边界未更新 active");
        At(20); Check(Stamp("schedule-first:expired"), "结束边界未更新 expired");
        At(0); Check(Stamp("schedule-first:upcoming"), "历法回拨未重算");
        At(30); Check(Stamp("schedule-first:expired"), "一次跨越多个边界未更新");
        Check(mailbox.CompleteInvitation(letter.letter_id) && Stamp("schedule-first:completed"), "Complete Changed 未立即更新");
        mailbox.letters.Remove(letter); mailbox.EmitChanged(); At(0);
    }
}
