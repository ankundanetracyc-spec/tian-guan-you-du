using Godot;
using System.Reflection;

/// <summary>Real MainScene callback, equal derived rewards, failure recovery, isolated save and UI regression.</summary>
public partial class CondensationBalanceSmoke : Control
{
    private int _checks;
    private const string SavePath = "user://condensation_balance_roundtrip.tres";
    private void Check(bool value, string why) { _checks++; if (!value) throw new Exception(why); }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Write(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private async Task Frames(int count = 3) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("condensation_balance", StringComparison.OrdinalIgnoreCase), "Use isolated APPDATA.");
            Engine.MaxFps = 60;
            GetViewport().GuiDisableInput = true;
            if (DisplayServer.GetName() == "headless") Engine.TimeScale = 12;
            await Frames();
            if (OS.GetCmdlineUserArgs().Contains("--read-saved"))
            {
                var restored = GD.Load<PersistentGameSave>(SavePath);
                Check(restored?.player_data is { has_spirit_core: true, cultivation_realm: "云息七层" }, "Formal snapshot did not survive process restart.");
                var loaded = new Character { entity_data = restored!.player_data, ProcessMode = ProcessModeEnum.Disabled };
                AddChild(loaded); loaded.Hide();
                AssertRewards(loaded);
                GD.Print($"CONDENSATION_BALANCE_SAVE_READ_PASS: checks={_checks}; score={loaded.condensation_final_score}");
                GetTree().Quit(); return;
            }

            GetTree().CurrentScene = null;
            var profile = GetNode<GameStartProfile>("/root/GameStartProfile");
            profile.roll_fated_aptitude();
            profile.fated_youngest_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER;
            profile.fated_youngest_root_name = "金木水三灵根";
            profile.fated_youngest_root_weights = [34, 33, 33, 0, 0];
            profile.fated_base_physique = profile.fated_base_intelligence = profile.fated_base_soul = 5;
            profile.fated_base_comprehension = 50;
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
            for (var i = 0; i < 1500 && !main.StartupComplete; i++) await Frames(1);
            Check(main.StartupComplete && main.StartupError is null, "MainScene startup did not complete.");
            Check(Read<Character?>(main, "player") is not null, "MainScene did not create actor.");
            if (DisplayServer.GetName() != "headless")
            {
                var size = OS.GetCmdlineUserArgs().Contains("--small-window") ? new Vector2I(1280, 720) : new Vector2I(1920, 1080);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                GetWindow().Unfocusable = true;
                DisplayServer.WindowSetSize(size);
                await Frames(6);
                Check(GetWindow().Size == size, "Actual test window size differs from requested size.");
            }
            var player = Read<Character>(main, "player");
            var human = (HumanData)player.entity_data!;
            human.cultivation_realm = "云息六层"; human.spirit_strands = 144;
            var realm = Read<RealmPanel>(main, "realm_panel");
            Write(realm, "_observedRealm", "云息六层"); // Fixture starts after the first six layers.
            var presentationStarts = 0;
            realm.PresentationStarted += () => presentationStarts++;
            var book = GD.Load<GongfaResource>("res://resources/gongfa/玄枢含章经.tres");
            human.main_gongfa_path = book.ResourcePath; player.main_gongfa = book;
            human.world_status_records.Clear(); player.refresh_gongfa_effects();
            Read<StoryDialoguePanel?>(main, "story_dialogue")?.Hide();
            Check(player.can_attempt_condensation_breakthrough(), "Formal six-to-seven entry unavailable.");
            var panel = Read<CondensationCardGamePanel>(main, "condensation_panel");
            var reports = 0;
            panel.CondensationFinished += (_, _, _) => reports++;

            Call(main, "open_condensation", player);
            Check(panel.Visible && !Read<bool>(panel, "_testMode"), "MainScene did not open formal mode.");
            Check(Read<List<string>>(panel, "_items").Count == 0, "Formal mode has items.");
            var formalReturn = Read<Button>(panel, "_returnButton");
            Check(formalReturn.Visible && formalReturn.Text == "返回修炼" && (formalReturn.GetThemeStylebox("normal") as StyleBoxTexture)?.Texture == CondensationUiArt.Rest && !Read<Button>(panel, "_restartButton").Visible, "Formal navigation buttons do not use the original end-turn artwork and visibility.");
            var formalButtons = panel.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree() && !string.IsNullOrWhiteSpace(b.Text)).ToArray();
            Check(formalButtons.Length > 5 && formalButtons.All(b => (b.GetThemeStylebox("normal") as StyleBoxTexture)?.Texture == CondensationUiArt.Rest), "Visible formal buttons do not share the original end-turn artwork.");
            var returnTextWidth = formalReturn.GetThemeFont("font").GetStringSize(formalReturn.Text, HorizontalAlignment.Left, -1, formalReturn.GetThemeFontSize("font_size")).X;
            Check(returnTextWidth + 8 <= formalReturn.Size.X + 1, "Formal return button text is clipped.");
            await Capture(panel, "setup");
            Call(panel, "ShowRewards", new Action(() => Call(panel, "ShowSetup")));
            await Capture(panel, "rewards");
            Check(Labels(panel).Any(label => label.Text.Contains("体魄、灵慧、神魂各定额＋0.25")), "Missing pre-game equal-reward rule.");
            Call(panel, "ShowSetup");
            Call(panel, "BeginGame");
            var initial = Read<CondensationCardGame>(panel, "_game");
            var opening = Read<Control>(panel, "_modal");
            Check(Read<bool>(panel, "_openingSwapPending") && opening.Visible && initial.SwapOpen, "Formal start did not open the starting-hand choice.");
            ((Button)opening.FindChild("OpeningCard0", true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
            ((Button)opening.FindChild("OpeningCard1", true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
            opening.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "换牌并开始").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!opening.Visible && !initial.SwapOpen && initial.Actions == 3 && initial.Hand.Count == 6 && human.spirit_strands == 144, "Formal opening exchange did not start the same game correctly.");
            await Capture(panel, "formal_play");

            var failed = CreateGame(panel, 0);
            while (!failed.Finished) failed.EndRound();
            Write(panel, "_game", failed); Call(panel, "ShowResult");
            Check(reports == 1 && player.cultivation_realm == "云息六层" && player.spirit_strands == 144 && player.has_active_severe_injury(), "Failed game changed realm/strands or missed injury.");
            Check(!realm.IsPresenting && !realm.HasPendingPresentation, "Failed condensation started a success presentation.");
            Check(!player.can_attempt_condensation_breakthrough(), "Injured actor can immediately retry.");
            Check(!Labels(panel).Any(label => label.Text.Contains("凝核成功已写入")), "Failure page claims success.");
            Call(panel, "ShowResult"); Check(reports == 1, "Repeated result reports twice.");
            Call(main, "advance_player_world_status_rest", 119);
            Check(player.has_active_severe_injury(), "Injury recovered too early.");
            Call(main, "advance_player_world_status_rest", 1);
            Check(player.can_attempt_condensation_breakthrough(), "120-hour rest did not restore retry.");

            Call(main, "open_condensation", player);
            var game = CreateGame(panel, 0);
            CondensationBalanceAuditPolicy.Run(game, 3);
            Check(game.Finished && !game.Failed, "Controlled legal playthrough failed.");
            Write(panel, "_game", game); Call(panel, "Refresh");
            Check(reports == 2 && player.cultivation_realm == "云息七层" && player.condensation_final_score == game.Score, "Actual MainScene callback did not commit score and realm.");
            Check(!panel.Visible && realm.HasPendingPresentation, "Formal result did not hand off to the unified presentation.");
            Check(!Read<RealmQiVisual>(panel, "_qi").IsTransitioning, "Formal condensation played its old duplicate animation.");
            AssertRewards(player);
            Check(!player.complete_condensation(game.Score), "Completed actor can receive another condensation result.");
            Call(panel, "ShowResult"); Check(reports == 2, "Reopening success reports twice.");
            for (var i = 0; i < 600 && !realm.IsPresenting; i++) await Frames(1);
            Check(realm.IsPresenting && presentationStarts == 1 && !panel.Visible, "Unified condensation did not start or its result covered the animation.");
            Check(Read<string>(realm, "_pendingFrom") == "云息六层", "Fixture did not exercise the condensation animation.");
            var settledAt = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            Check(Read<List<Control>>(realm, "_presentationSurroundings").All(control => !control.Visible), "Formal condensation did not hide surrounding information.");
            for (var i = 0; i < 1200 && realm.IsPresenting; i++) await Frames(1);
            Check(!realm.IsPresenting && realm.Visible && !panel.Visible, $"Condensation did not finish in the normal realm view: presenting={realm.IsPresenting}, realm={realm.Visible}, cards={panel.Visible}.");
            realm.Hide(); await Frames(2);
            Check(panel.Visible && Read<Control>(panel, "_modal").Visible && reports == 2 && presentationStarts == 1, "Closing the realm view lost or replayed the condensation result.");
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == settledAt && player.condensation_final_score == game.Score, "Condensation animation/restoration changed the settled data.");
            await Capture(panel, "result");
            var outcome = Labels(panel).Single(label => label.Name == "CondensationOutcome");
            Check(outcome.Text == CondensationOutcomeRules.Describe(game.Score), "UI and runtime reward formulas differ.");
            var saved = (PersistentGameSave)Call(main, "capture_progress_snapshot", false)!;
            Check(ResourceSaver.Save(saved, SavePath) == Error.Ok, "Formal snapshot save failed.");
            var readback = ResourceLoader.Load<PersistentGameSave>(SavePath, "", ResourceLoader.CacheMode.IgnoreDeep);
            Check(readback?.player_data?.condensation_final_score == game.Score, "Score lost on disk.");
            var detached = new Character { entity_data = readback!.player_data, ProcessMode = ProcessModeEnum.Disabled };
            AddChild(detached); detached.Hide(); AssertRewards(detached);
            Call(panel, "RequestClose");
            panel.SuspendResultForRealmPresentation(); panel.RestoreResultAfterRealmPresentation();
            Check(!panel.Visible, "A later realm presentation resurrected the dismissed condensation result.");

            var originalScore = player.condensation_final_score;
            panel.ShowFor(player, true);
            var trialRestart = Read<Button>(panel, "_restartButton");
            Check(trialRestart.Visible && (trialRestart.GetThemeStylebox("normal") as StyleBoxTexture)?.Texture == CondensationUiArt.Rest, "Trial restart button is missing the original end-turn artwork.");
            Call(panel, "BeginGame");
            var trialOpening = Read<CondensationCardGame>(panel, "_game");
            Check(Read<bool>(panel, "_openingSwapPending") && trialOpening.SwapOpen, "Trial did not open the starting-hand choice.");
            Read<Control>(panel, "_modal").FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "不换，直接开始").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!trialOpening.SwapOpen && !Read<Control>(panel, "_modal").Visible && player.condensation_final_score == originalScore, "Skipping trial opening changed formal data or did not start.");
            var trial = CreateGame(panel, 7); CondensationBalanceAuditPolicy.Run(trial, 3);
            Write(panel, "_game", trial); Call(panel, "ShowResult");
            Check(reports == 2 && player.condensation_final_score == originalScore, "Trial wrote a formal result.");
            Write(main, "_skipExitSave", true);
            GD.Print($"CONDENSATION_BALANCE_SMOKE_PASS: checks={_checks}; score={game.Score}; trial={trial.Score}; equalBonus={player.get_condensation_flat_bonus("physique")}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError($"checks={_checks}: {error}"); GetTree().Quit(1); }
    }

    private static IEnumerable<Label> Labels(CondensationCardGamePanel panel)
        => Read<Control>(panel, "_modal").FindChildren("*", "Label", true, false).OfType<Label>();
    private static CondensationCardGame CreateGame(CondensationCardGamePanel panel, int seed)
        => new(Read<int>(panel, "_comprehension"), Read<int>(panel, "_intelligence"), Read<int>(panel, "_rootWeight"),
            CondensationCardGame.Pool.ToDictionary(card => card.Name, card => card.Count), [], seed);

    private void AssertRewards(Character actor)
    {
        var expected = (actor.condensation_final_score - 100) / 5 * .25f;
        foreach (var key in new[] { "physique", "intelligence", "soul" })
        {
            Check(actor.get_condensation_flat_bonus(key) == expected, "Unequal permanent reward.");
            var basis = actor.entity_data!.Get("base_" + key).AsSingle();
            var (growth, flat) = actor.get_realm_attribute_adjustment(key);
            var value = basis * growth + flat + expected;
            Check(Math.Abs(actor.calculate_realm_base_attribute(key, basis) - value) < .0001, "Derived attribute omits or multiplies condensation.");
            actor.recalculate_attributes(); actor.recalculate_attributes();
            Check(Math.Abs(actor.calculate_realm_base_attribute(key, basis) - value) < .0001, "Repeated recalculation duplicates reward.");
        }
        Check(actor.get_condensation_flat_bonus("comprehension") == 0 && actor.get_condensation_flat_bonus("fortune") == 0, "Unrelated attribute reward.");
        Check(actor.get_spirit_core_summary().Contains("体魄、灵慧、神魂各定额"), "Character summary is outdated.");
        var realms = GetNode<RealmManager>("/root/RealmManager");
        var multiplier = actor.get_condensation_spirit_requirement_multiplier();
        var accumulated = 144;
        foreach (var (tier, increment) in new[] { (8, 54), (9, 60), (10, 66) })
        {
            accumulated += (int)Math.Ceiling(increment * multiplier);
            var config = realms.get_realm_config(tier == 8 ? "云息八层" : tier == 9 ? "云息九层" : "云息十层")!;
            Check(realms.get_effective_required_attributes(actor, config)["spirit_strands"].AsInt32() == accumulated, "Later realm requirements are not rounded per increment.");
        }
        Check(actor.get_foundation_cloud_bonus() == CondensationOutcomeRules.FoundationCloudBonus(actor.condensation_final_score), "Deferred foundation bonus differs.");
    }

    private async Task Capture(CondensationCardGamePanel panel, string name)
    {
        await Frames(3);
        var modal = Read<Control>(panel, "_modal");
        var box = modal.GetChild<Control>(0);
        foreach (var label in Labels(panel))
        {
            Check(label.Position.Y + label.Size.Y <= box.Size.Y + 1, "Modal text extends below panel: " + label.Text);
            var lineCount = label.GetLineCount();
            var requiredHeight = lineCount * label.GetLineHeight() + Math.Max(0, lineCount - 1) * label.GetThemeConstant("line_spacing");
            Check(requiredHeight <= label.Size.Y + 1, "Modal text is clipped: " + label.Text);
        }
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = "res://.godot/test-results/condensation_balance";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng($"{directory}/{name}_{GetWindow().Size.X}.png");
    }
}
