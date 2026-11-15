using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>One real save slot, six separate processes, official room controls from Cloud Seven to nine breaths.</summary>
public partial class CloudSevenFormalJourneySmoke : Control
{
    private const string ManifestPath = "user://cloud_seven_formal_manifest.json";
    private const string BookPath = "res://resources/gongfa/玄枢含章经.tres";
    private MainScene _main = null!;
    private Character _player = null!;
    private HumanData Data => (HumanData)_player.entity_data!;
    private GameTimeMgr Clock => GetNode<GameTimeMgr>("/root/GameTimeManager");
    private GameSaveManager Saves => GetNode<GameSaveManager>("/root/GameSaveManager");
    private CultivationUI Room => Read<CultivationUI>(_main, "cultivation_ui");
    private RunState _state = new();
    private string _stage = "";
    private int _checks;
    private int _presentationStarts;
    private bool _presentedBeforeSettlement, _queuedDuringAction;
    private sealed class RunState
    {
        public string Slot { get; set; } = "";
        public string Step { get; set; } = "";
        public string PlayerId { get; set; } = "";
        public string Realm { get; set; } = "";
        public int Strands { get; set; }
        public float Fraction { get; set; }
        public int Breaths { get; set; }
        public bool Active { get; set; }
        public int Target { get; set; }
        public int Phase { get; set; }
        public double PhaseHours { get; set; }
        public double Reduction { get; set; }
        public double Time { get; set; }
        public double BreathStart { get; set; }
        public double ExpectedBreathHours { get; set; }
        public double LostBreathHours { get; set; }
        public double OtherActionHours { get; set; }
        public double JourneyStart { get; set; }
        public float Stamina { get; set; }
    }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Write(object target, string field, object? value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    private static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private void Check(bool ok, string why) { _checks++; if (!ok) throw new Exception(why); }
    private double Now => Clock.get_total_game_hours() + Clock.get_pending_hours();
    private async Task Frames(int count = 3) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Press(Button button) => button.EmitSignal(BaseButton.SignalName.Pressed);
    private static HashSet<ulong> Orphans()
    {
        using var ids = (Godot.Collections.Array)Node.GetOrphanNodeIds();
        return ids.Select(id => id.AsUInt64()).ToHashSet();
    }

    public override async void _Ready()
    {
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            GetViewport().GuiDisableInput = true; // Drive controls directly without accepting desktop clicks into the world map.
            Check(OS.GetEnvironment("APPDATA").Contains("cloud_seven_formal", StringComparison.OrdinalIgnoreCase), "Use isolated APPDATA.");
            var windowedRun = OS.GetCmdlineUserArgs().Contains("--windowed-run");
            Check(windowedRun ? DisplayServer.GetName() != "headless" : DisplayServer.GetName() == "headless", "Requested display mode was not established.");
            GD.Print($"CLOUD_SEVEN_DISPLAY: {DisplayServer.GetName()}; renderer={RenderingServer.GetCurrentRenderingMethod()}");
            _stage = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--stage="))?[8..] ?? "seed";
            Engine.TimeScale = 8; // Keep the focus phase observable; game hours still come from production actions.
            Engine.MaxFps = 60;
            await Frames();
            if (_stage != "seed")
            {
                _state = JsonSerializer.Deserialize<RunState>(Godot.FileAccess.GetFileAsString(ManifestPath))!;
                Check(Saves.load_slot(_state.Slot), "Official slot load failed.");
            }
            else
            {
                var profile = GetNode<GameStartProfile>("/root/GameStartProfile");
                // Fixed test profile: creation-roll rules are outside this progression check.
                profile.has_fated_roll = true; profile.player_name = "云息后半程正式验收";
                profile.fated_youngest_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER;
                profile.fated_youngest_root_name = "金木水三灵根"; profile.fated_youngest_root_weights = [34, 33, 33, 0, 0];
                profile.fated_base_physique = profile.fated_base_intelligence = profile.fated_base_soul = 5;
                profile.fated_base_comprehension = 50;
                Saves.begin_new_game(profile.player_name);
            }
            var orphanBaseline = Orphans();
            GetTree().CurrentScene = null;
            _main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(_main); GetTree().CurrentScene = _main;
            for (var i = 0; i < 1500 && !_main.StartupComplete; i++) await Frames(1);
            Check(_main.StartupComplete && _main.StartupError is null, "MainScene startup failed.");
            Write(_main, "_skipExitSave", true); // A failed read assertion must not replace its input checkpoint.
            _player = Read<Character>(_main, "player");
            var realmPanel = Read<RealmPanel>(_main, "realm_panel");
            realmPanel.PresentationStarted += () =>
            {
                _presentationStarts++;
                _presentedBeforeSettlement |= Read<ActionTransitionPanel?>(_main, "action_transition")?.IsPlaying == true ||
                    GetNode<NpcWorldManager>("/root/NpcWorldManager").HasPendingSettlement || Clock.IsSmoothAdvanceBusy || GetTree().Paused;
            };
            _player.BreakthroughCompleted += (_, _) =>
            {
                if (Read<ActionTransitionPanel?>(_main, "action_transition")?.IsPlaying == true)
                    _queuedDuringAction = realmPanel.HasPendingPresentation && !realmPanel.IsPresenting;
            };
            await Idle();
            if (DisplayServer.GetName() != "headless")
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                GetWindow().Unfocusable = true;
                DisplayServer.WindowSetSize(new Vector2I(1920, 1080)); await Frames(4);
            }
            GD.Print($"CLOUD_SEVEN_STAGE_BEGIN: {_stage}; slot={Saves.active_slot_id}; realm={_player.cultivation_realm}; breaths={_player.yuanxi_count}");
            if (_stage == "seed") await Seed();
            else
            {
                AssertRestored();
                switch (_stage)
                {
                    case "warmup": await Warmup(); break;
                    case "to_yuanxi": await ToYuanxi(); break;
                    case "resume": await ResumeAndInterrupt(); break;
                    case "finish": await Finish(); break;
                    case "verify": await Verify(); break;
                    default: throw new Exception("Unknown stage.");
                }
            }
            var result = $"slot={Saves.active_slot_id}; realm={_player.cultivation_realm}; breaths={_player.yuanxi_count}; clock={Now:0.######}";
            Write(_main, "_skipExitSave", true);
            var tree = GetTree(); tree.CurrentScene = null;
            _main.QueueFree(); await Frames(8);
            _main = null!; _player = null!;
            // Give Godot frames to drain native releases; blocking finalizer waits can stall rendering cleanup.
            for (var i = 0; i < 2; i++) { GC.Collect(); await Frames(8); }
            var orphanDetails = Orphans().Except(orphanBaseline).Select(id =>
            {
                var node = GodotObject.InstanceFromId(id) as Node;
                return $"{id}:{node?.GetClass()}:{node?.Name}:{(node is Label label ? label.Text : "")}";
            }).ToArray();
            Check(orphanDetails.Length == 0, "Main scene left orphan nodes after exit: " + string.Join(", ", orphanDetails));
            GD.Print($"CLOUD_SEVEN_STAGE_PASS: {_stage}; checks={_checks}; {result}");
            tree.Quit();
        }
        catch (Exception error) { GD.PushError($"CLOUD_SEVEN_STAGE_FAIL: {_stage}; checks={_checks}; {error}"); GetTree().Quit(1); }
    }

    private async Task Idle()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (Read<ActionTransitionPanel?>(_main, "action_transition")?.IsPlaying == true ||
               GetNode<NpcWorldManager>("/root/NpcWorldManager").HasPendingSettlement || Clock.IsSmoothAdvanceBusy)
        {
            if (watch.Elapsed.TotalSeconds > 120) throw new Exception("World settlement timed out.");
            await Frames(1);
        }
        await Frames(3);
    }

    private async Task Seed()
    {
        // Fixture preparation ends at Cloud Seven. No realm, strands or phase is assigned after this stage.
        var book = GD.Load<GongfaResource>(BookPath);
        Check(_player.try_select_baishi_first_volume(book), "Cannot register fixture's first volume.");
        Check(_player.select_gongfa_for_study(book), "Cannot select fixture study.");
        _player.study_for_world_hours(24); await Idle();
        Check(_player.is_gongfa_chapter_learned(book, book.get_sorted_chapters().First().get_id()) && _player.switch_main_gongfa(book), "Fixture first volume not learned/equipped.");
        Data.cultivation_realm = "云息六层"; Data.spirit_strands = 144; _player.refresh_gongfa_effects();
        Write(Read<RealmPanel>(_main, "realm_panel"), "_observedRealm", "云息六层");
        Call(_main, "on_condensation_finished", _player, true, 125);
        Check(_player.cultivation_realm == "云息七层" && _player.condensation_final_score == 125, "Official condensation callback did not produce fixture Cloud Seven.");
        Data.sect_affiliation_id = "yuxu_xianzong"; Data.sect_current_rank = "外门弟子";
        Data.current_world_stamina = 0; // Ordinary cultivation and Yuanxi must both support zero stamina.
        _player.remove_item_from_inventory(Character.YunxiThreeToFourPillPath, 1);
        var lecture = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        lecture.departed_qingxi = lecture.heard_lecture = lecture.roots_checked = lecture.fang_returned = lecture.reached_yunxi = lecture.finished = lecture.assignments_released = true;
        Write(_main, "_player_world_node_id", "yuxu_xianzong_core");
        Write(_main, "_nodeWorldState", new Godot.Collections.Dictionary());
        Read<StoryDialoguePanel>(_main, "story_dialogue").Hide();
        await VerifyFormalPresentation("condensation");
        _state.JourneyStart = Now;
        Check(_player.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 1, "Starting breakthrough pill missing.");
        await Save("seed");
    }

    private void AssertRestored()
    {
        Check(Saves.active_slot_id == _state.Slot && _player.unique_id == _state.PlayerId, "Slot or player identity changed.");
        Check(_player.cultivation_realm == _state.Realm && _player.spirit_strands == _state.Strands, "Realm/strands changed on load.");
        Check(Math.Abs(_player.get_cultivation_progress() - _state.Fraction) < .00001, "Fractional cultivation lost.");
        Check(_player.yuanxi_count == _state.Breaths && Data.yuanxi_active == _state.Active && Data.yuanxi_phase == _state.Phase && Data.yuanxi_target == _state.Target,
            $"Breath/phase/target changed on load: expected={_state.Breaths}/{_state.Active}/{_state.Phase}/{_state.Target}, actual={_player.yuanxi_count}/{Data.yuanxi_active}/{Data.yuanxi_phase}/{Data.yuanxi_target}; clock={Now}/{_state.Time}; retreatClock={Data.yuanxi_clock_hour}; notice={Data.yuanxi_notice}.");
        Check(Math.Abs(Data.yuanxi_phase_hours - _state.PhaseHours) < .00001 && Math.Abs(Data.yuanxi_duration_reduction - _state.Reduction) < .000001, "Phase progress or pill benefit lost.");
        Check(Math.Abs(Now - _state.Time) < .001, "Load advanced time or dropped fractional hours.");
        Check(_player.condensation_final_score == 125 && _player.get_condensation_flat_bonus("physique") == 1.25f, "Condensation result changed.");
        Check(Math.Abs(_player.current_world_stamina - _state.Stamina) < .0001, "Stamina changed on load.");
    }

    private async Task OpenRoom()
    {
        Read<RealmPanel>(_main, "realm_panel").Hide();
        var overview = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
        Call(_main, "open_faction_overview_if_available", "yuxu_xianzong_core");
        overview.Call("_on_peak_pressed", "guanfu");
        var peak = overview.GetNode<Control>("PeakInterior");
        var buttons = peak.Get("_action_buttons").AsGodotDictionary();
        var meditation = buttons["meditation"].AsGodotObject() as Button;
        Check(meditation is { Disabled: false }, "Legal sect meditation action unavailable.");
        Press(meditation!); await Frames();
        Check(Room.IsVisibleInTree(), "Meditation did not open official cultivation UI.");
    }

    private void Goal(string title)
    {
        var task = (JournalPanel.Entry)Call(_main, "get_cultivation_guidance_task")!;
        Check(task.Title == title, $"Wrong progression goal: expected {title}, actual {task.Title}.");
    }

    private async Task Cultivate(int mode, int amount)
    {
        var selector = Read<OptionButton>(Room, "_mode"); selector.Select(mode); selector.EmitSignal(OptionButton.SignalName.ItemSelected, mode);
        Read<SpinBox>(Room, mode == 1 ? "_days" : "_strands").Value = amount;
        Call(Room, "refresh");
        var button = Read<Button>(Room, "_cultivate");
        Check(!button.Disabled, "Official cultivation action disabled: " + Read<Label>(Room, "_plan").Text);
        var before = Now; Press(button); await Idle();
        Check(Now > before, "Cultivation did not advance calendar.");
        Check(_player.current_world_stamina == 0, "Cultivation charged/restored stamina.");
    }

    private async Task Warmup()
    {
        Goal("稳固灵核"); await OpenRoom(); await Cultivate(1, 1);
        Check(_player.cultivation_realm == "云息七层" && _player.get_cultivation_progress() > 0, "Warmup unexpectedly advanced realm or lost sub-strand progress.");
        await Save("partial_seven");
    }

    private async Task ToYuanxi()
    {
        foreach (var (realm, target, amount) in new[] { ("云息七层", "云息八层", 58), ("云息八层", "云息九层", 64), ("云息九层", "云息十层", 71) })
        {
            Check(_player.cultivation_realm == realm, "Wrong stage start.");
            Goal(realm == "云息九层" ? "晋入云息十层" : "稳固灵核");
            await OpenRoom();
            var progress = _player.get_next_realm_progress();
            Check(progress["required"].AsInt32() == amount, "Wrong per-layer condensation requirement.");
            await Cultivate(0, _player.get_strands_until_next_realm());
            Check(_player.cultivation_realm == realm && _player.is_breakthrough_ready(), "Cultivation auto-promoted or failed to reach requirement.");
            Call(Room, "select_room_page", 2);
            var button = Read<Button>(Room, "_breakthrough");
            Check(!button.Disabled, "Official breakthrough button disabled.");
            if (target == "云息十层")
            {
                var picker = Read<OptionButton>(Room, "_breakthroughPillPicker");
                Check(picker.ItemCount == 1 && picker.GetItemMetadata(picker.Selected).AsString() == Character.YunxiNineToTenPillPath, "Wrong selected breakthrough pill.");
            }
            _queuedDuringAction = false;
            Press(button); await Idle();
            Check(_player.cultivation_realm == target, "Official breakthrough did not advance realm.");
            Check(_queuedDuringAction, "Ordinary breakthrough did not queue its presentation during settlement.");
            await VerifyFormalPresentation(target, target == "云息九层");
            GD.Print($"CLOUD_SEVEN_REALM: {realm}->{target}; strands={_player.spirit_strands}; clock={Now:0.######}");
            await Capture(target);
        }
        Check(_player.spirit_strands == 337 && _player.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 0, "Final strands/pill consumption incorrect.");
        Check(Math.Abs(Data.yuanxi_duration_reduction - .02) < .000001, "Thin pill did not persist two-percent duration reduction.");
        Goal("归元养息"); await OpenRoom();
        var panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        _state.BreathStart = Now;
        _state.ExpectedBreathHours = (5760 / _player.get_yuanxi_efficiency() + 3744) * (1 - Data.yuanxi_duration_reduction);
        Press(Read<Button>(panel, "_one"));
        for (var i = 0; i < 10; i++) panel._Process(.25);
        Check(_player.is_yuanxi_active() && _player.yuanxi_count == 0 && Data.yuanxi_phase_hours > 0, "First breath did not start.");
        await Capture("first_breath_active");
        await Save("first_breath_active");
    }

    private async Task TickUntil(YuanxiPanel panel, Func<bool> condition)
    {
        for (var tick = 0; !condition(); tick++)
        {
            if (tick > 3000) throw new Exception("Yuanxi progression stalled.");
            Check(panel.IsVisibleInTree() && _player.is_yuanxi_active(), "Yuanxi stopped before target.");
            panel._Process(.25);
            if (tick % 24 == 0) await Frames(1);
        }
        await Idle();
    }

    private async Task ResumeAndInterrupt()
    {
        await OpenRoom(); var panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        Check(_player.is_yuanxi_active() && !Read<bool>(panel, "_playing"), "Load should preserve retreat and await continue.");
        var hiddenAt = Now; var hiddenProgress = Data.yuanxi_phase_hours;
        Press(Read<Button>(panel, "_continue")); Room.close_panel(); await Frames(8); panel._Process(.25);
        Check(_player.is_yuanxi_active() && Math.Abs(Now - hiddenAt) < .001 && Data.yuanxi_phase_hours == hiddenProgress, "Closing the room advanced time or cleared retreat.");
        await OpenRoom(); panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        Press(Read<Button>(panel, "_continue")); await TickUntil(panel, () => _player.yuanxi_count == 1);
        Check(!_player.is_yuanxi_active(), "Single-breath target did not stop.");
        Read<SpinBox>(panel, "_target").Value = 3; Press(Read<Button>(panel, "_continuous"));
        var before = Now; for (var i = 0; i < 15; i++) panel._Process(.25);
        _state.LostBreathHours += Now - before;
        Check(_player.yuanxi_count == 1 && Data.yuanxi_phase_hours > 0, "Interruption fixture has no unfinished breath.");
        Press(Read<Button>(panel, "_stop"));
        Check(!_player.is_yuanxi_active() && _player.yuanxi_count == 1 && Data.yuanxi_phase == 0 && Data.yuanxi_phase_hours == 0, "Exit did not clear only the unfinished breath.");
        Press(Read<Button>(panel, "_continuous"));
        before = Now; for (var i = 0; i < 2; i++) panel._Process(.25);
        _state.LostBreathHours += Now - before;
        Room.close_panel();
        Call(_main, "on_yuxu_life_action_requested", "rest", "guanfu");
        var rest = Read<RestPanel>(_main, "_restPanel");
        Read<SpinBox>(rest, "_hours").Value = 1;
        var restStarted = Now; Press(Read<Button>(rest, "_start")); await Idle();
        _state.OtherActionHours += Now - restStarted;
        Check(Math.Abs(Now - restStarted - 1) < .001 && !_player.is_yuanxi_active() && _player.yuanxi_count == 1 && Data.yuanxi_phase_hours == 0,
            "Rest did not interrupt only the unfinished breath.");
        Read<StoryDialoguePanel>(_main, "story_dialogue").Hide();
        await OpenRoom(); panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        Read<SpinBox>(panel, "_target").Value = 9; Press(Read<Button>(panel, "_continuous"));
        await TickUntil(panel, () => _player.yuanxi_count == 3);
        for (var i = 0; i < 10; i++) panel._Process(.25);
        Check(_player.is_yuanxi_active() && _player.yuanxi_count == 3 && Data.yuanxi_phase_hours > 0, "Mid-fourth-breath checkpoint invalid.");
        await Save("fourth_breath_active");
    }

    private async Task Finish()
    {
        await OpenRoom(); var panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        Press(Read<Button>(panel, "_continue")); await TickUntil(panel, () => _player.is_yunxi_complete());
        await VerifyFormalPresentation("nine_breath_completion");
        Check(!_player.is_yuanxi_active() && _player.cultivation_realm == "云息十层" && _player.spirit_strands == 337, "Completion altered realm or ordinary strands.");
        var actual = Now - _state.BreathStart - _state.LostBreathHours - _state.OtherActionHours;
        Check(Math.Abs(actual - _state.ExpectedBreathHours) < .05, $"Nine-breath clock mismatch: actual={actual}, expected={_state.ExpectedBreathHours}.");
        Goal("准备铸玄基"); await OpenRoom();
        panel = Read<YuanxiPanel>(Room, "_yuanxiPanel"); panel.SetProcess(false);
        Check(Read<Button>(panel, "_one").Disabled && Read<Button>(panel, "_continuous").Disabled, "Completed actor can keep gaining breaths.");
        Call(Room, "select_room_page", 2);
        Check(Read<Button>(Room, "_breakthrough").Disabled && Read<Button>(Room, "_breakthrough").Text == "准备铸玄基", "Completion left an executable or incorrect breakthrough entry.");
        Call(Room, "select_room_page", 0);
        GD.Print($"CLOUD_SEVEN_COMPLETION: elapsed={Now - _state.JourneyStart:0.######}; yuanxi={actual:0.######}; interrupted={_state.LostBreathHours}; nineBreathExpected={_state.ExpectedBreathHours:0.######}");
        await Capture("completed"); await Save("complete");
    }

    private async Task Verify()
    {
        Check(_player.is_yunxi_complete() && !_player.is_yuanxi_active() && _player.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 0, "Final save not complete or pill duplicated.");
        Goal("准备铸玄基"); await OpenRoom();
        Check(Read<YuanxiPanel>(Room, "_yuanxiPanel").Visible, "Completed state did not reopen correct room.");
        var before = Now; await Frames(12); Check(Math.Abs(Now - before) < .001, "Completed UI advanced calendar.");
        Check(_presentationStarts == 0 && !Read<RealmPanel>(_main, "realm_panel").HasPendingPresentation, "Loading completion replayed its presentation.");
        Check(Saves.get_save_slots().Count == 1, "Run created another slot.");
        await Capture("reloaded_complete");
    }

    private async Task Save(string step)
    {
        await Idle(); Check((bool)Call(_main, "save_progress_now", false)!, "Official slot save failed.");
        _state.Slot = Saves.active_slot_id; _state.Step = step; _state.PlayerId = _player.unique_id;
        _state.Realm = _player.cultivation_realm; _state.Strands = _player.spirit_strands; _state.Fraction = _player.get_cultivation_progress();
        _state.Breaths = _player.yuanxi_count; _state.Active = Data.yuanxi_active; _state.Target = Data.yuanxi_target;
        _state.Phase = Data.yuanxi_phase; _state.PhaseHours = Data.yuanxi_phase_hours; _state.Reduction = Data.yuanxi_duration_reduction; _state.Time = Now;
        _state.Stamina = _player.current_world_stamina;
        var disk = ResourceLoader.Load<PersistentGameSave>($"user://game_saves/{Saves.active_slot_id}.tres", "", ResourceLoader.CacheMode.IgnoreDeep)?.player_data;
        Check(disk is not null && disk.yuanxi_count == _state.Breaths && disk.yuanxi_active == _state.Active && disk.yuanxi_phase == _state.Phase && disk.yuanxi_target == _state.Target && Math.Abs(disk.yuanxi_phase_hours - _state.PhaseHours) < .00001,
            $"Written checkpoint differs from live retreat at {step}: live={_state.Breaths}/{_state.Active}/{_state.Phase}/{_state.PhaseHours}, disk={disk?.yuanxi_count}/{disk?.yuanxi_active}/{disk?.yuanxi_phase}/{disk?.yuanxi_phase_hours}.");
        using var file = Godot.FileAccess.Open(ManifestPath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(_state));
    }

    private async Task VerifyFormalPresentation(string label, bool skip = false)
    {
        var realm = Read<RealmPanel>(_main, "realm_panel");
        for (var i = 0; i < 120 && !realm.IsPresenting; i++) await Frames(1);
        Check(realm.IsPresenting && !_presentedBeforeSettlement, "Formal presentation was missed or began before settlement: " + label);
        Check(!Room.Visible && Read<CondensationCardGamePanel>(_main, "condensation_panel").Visible == false, "Previous screen covered formal presentation.");
        for (var i = 0; i < 120 && realm.IsPresenting && Read<List<Control>>(realm, "_presentationSurroundings").Any(control => control.Visible); i++) await Frames(1);
        Check(Read<List<Control>>(realm, "_presentationSurroundings").All(control => !control.Visible), "Formal presentation left surrounding information visible.");
        var time = Now; var strands = _player.spirit_strands; var breaths = _player.yuanxi_count;
        await Capture(label + "_focus");
        if (skip) Press(Read<Button>(realm, "_skipAnimation"));
        for (var i = 0; i < 120 && realm.IsPresenting; i++) await Frames(1);
        Check(!realm.IsPresenting && Read<List<Control>>(realm, "_presentationSurroundings").All(control => control.Visible && control.Modulate.A == 1), "Formal presentation did not restore information.");
        Check(Read<RealmQiVisual>(realm, "_qiVisual").Scale.IsEqualApprox(Vector2.One), "Formal visual remained enlarged.");
        Check(Math.Abs(Now - time) < .001 && _player.spirit_strands == strands && _player.yuanxi_count == breaths, "Formal presentation altered the settled outcome.");
    }

    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://.godot/test-results/cloud_seven_formal");
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using var capture = GetViewport().GetTexture().GetImage(); capture.SavePng(folder + "/" + name + ".png");
    }
}
