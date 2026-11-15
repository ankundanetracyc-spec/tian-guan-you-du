using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using R = FormationGuardAssaultRuntime;
using Kind = FormationGridModel.Kind;

public partial class FormationFreeBuildUiSmoke
{
    private bool IsFlowRun => OS.GetCmdlineUserArgs().Contains("--formation-class-flow-ui-smoke");
    private readonly List<object> _flowFrames = [];
    private readonly List<object> _flowPlayerErrors = [];
    private string _flowOccurrence = "";
    private R.Snapshot? _flowSnapshot;
    private async Task RunClassificationAndFlow()
    {
        _stage = "分类与坐标";
        Check(Workbench.SelectedPartCategory == 0, "默认输入分类");
        await ReloadFlowExample();
        await Click(Find<Button>("OpenGuardTrial")); await WaitForFlow();
        var trial = Workbench.EditableTrial!; var snapshot = trial.GetSnapshot(); var diagram = Canvas.Model.Serialize();
        var categories = new[] { "Inputs", "Logic", "Outputs", "Source" };
        var expected = new[] {
            new[] { Kind.Sensor, Kind.StateInput }, new[] { Kind.Compare, Kind.Not, Kind.Switch, Kind.Filter, Kind.Counter, Kind.Split, Kind.Merge },
            new[] { Kind.Ward, Kind.Guard, Kind.Fire }, new[] { Kind.Source }
        };
        for (var index = 0; index < categories.Length; index++)
        {
            await Click(Find<Button>("FormationCategory" + categories[index]));
            Check(Workbench.SelectedPartCategory == index && Enumerable.Range(0, 4).All(i => Find<Control>("FormationParts" + categories[i]).Visible == (i == index)), "分类仅显示自身页面 " + categories[index]);
            Check(expected[index].All(kind => Find<Button>("Part" + kind).IsVisibleInTree()), "该类部件清楚归属 " + categories[index]);
            Check(Canvas.Model.Serialize() == diagram && ReferenceEquals(Workbench.EditableTrial, trial) && trial.GetSnapshot() == snapshot, "切分类不改图或清账本 " + categories[index]);
            await MoveOutside(); await Capture($"0{index + 1}_{categories[index]}分类.png");
        }
        Check(Workbench.FindChild("PartEye", true, false) is null && Canvas.Model.Components.Values.All(p => p.Kind != Kind.Eye), "部件册不再展示阵眼，阵图也没有固定阵眼");
        var guide = Find<Label>("FormationCoordinateHelp");
        Check(guide.IsVisibleInTree() && guide.Text.Contains("先列后行", StringComparison.Ordinal) && guide.Text.Contains("左上(1,1)", StringComparison.Ordinal), "常驻坐标说明明确一基列行方向");
        await Click(Find<Button>("FormationCategoryInputs")); await Click(Find<Button>("PartStateInput"));
        await Click(Find<Button>("FormationCategoryLogic"));
        Check(Canvas.Tool == FormationBoardCanvas.EditTool.Select && Canvas.Model.Serialize() == diagram, "切走正在放置件所属分类时回移动且不误放件");

        _stage = "一次真实输入与原始动画帧";
        await SetHealth(80); await WaitForFlow();
        var before = trial.GetSnapshot(); await Click(Find<Button>("ControlTrialSend"));
        var after = trial.GetSnapshot(); var plan = Workbench.LastFlowPlan ?? throw new Exception("没有真实播放计划");
        var attack = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Fire).Id;
        Check(after.Attacks == before.Attacks + 1 && after.SourceEnergy == before.SourceEnergy - 3 && after.EnemyHealth < before.EnemyHealth, "本次真实事件仅结算一次伤害与费用");
        Check(plan.Items.Any(item => !item.IsEnergy) && plan.PaidOutputIds.SequenceEqual(new[] { attack }) && plan.Items.Where(item => item.IsEnergy).All(item => item.Route.ToComponentId == attack), "红线有真实信息，金线只连本次实际付款火击");
        _flowOccurrence = plan.OccurrenceId; _flowSnapshot = after;
        Check(_flowOccurrence.Length > 0 && Canvas.FlowPlan is not null && Canvas.IsFlowPlaying, "原生画布实际播放同一发生的计划");
        VerifyDrawnPortDirections();
        await CaptureFlowFrames(plan, after, Workbench.LastControlResult!);
        Check(trial.GetSnapshot() == after && Workbench.LastFlowPlan?.OccurrenceId == _flowOccurrence, "所有动画帧保持同一事件和结算快照");

        _stage = "纯回放";
        await Click(Find<Button>("ControlTrialReplayFlow"));
        Check(Canvas.IsFlowPlaying && Canvas.FlowPlan?.OccurrenceId == _flowOccurrence && trial.GetSnapshot() == after, "真实回放按钮只重播显示，不再扣费或攻击");
        await WaitForFlow();
        await Click(Find<Button>("ControlTrialRepeat"));
        Check(Workbench.LastControlResult?.Duplicate == true && Canvas.FlowPlan?.Items.Count == 0 && trial.GetSnapshot() == after, "同次重发不自动播放旧信号或旧付款");
        if (!PreviewOnly) await VerifyFlowFailures();
        var actor = Workbench.ControlTrialOwner; var enemy = Workbench.ControlTrialEnemy;
        await Click(Find<Button>("ReturnFormationWorkspace")); await Frames(3);
        Check(!GodotObject.IsInstanceValid(_workspace) && !GodotObject.IsInstanceValid(actor) && !GodotObject.IsInstanceValid(enemy), "返回清理试演与动画宿主");
        WriteFlowReport(true, null); _archive.QueueFree(); _owner.Free(); await Frames(2);
        GD.Print($"FORMATION_CLASS_FLOW_UI_PASS mode={(PreviewOnly ? "preview" : "full")} width={_width} height={_height} checks={_checks.Count}"); GetTree().Quit();
    }
    private async Task ReloadFlowExample() { await Pick(Find<OptionButton>("LogicExamples"), 0); await Pick(Find<OptionButton>("LogicExamples"), 5); }
    private async Task VerifyFlowFailures()
    {
        _stage = "受阻和限次不伪造供能";
        await ReloadFlowExample(); await SetHealth(20); await Click(Find<Button>("ControlTrialSend"));
        var blocked = Workbench.LastFlowPlan!;
        Check(blocked.Items.Any(item => !item.IsEnergy && item.Route.FromComponentId == "cast") && blocked.Items.All(item => !item.IsEnergy) &&
            !blocked.Items.Any(item => item.Route.FromComponentId == "switch" && item.Route.ToComponentId == "attack"), "关着的开关确实收到事件，但不传火击或假画金线");
        await SetHealth(80); await Click(Find<Button>("ControlTrialSend")); var paid = Workbench.EditableTrial!.GetSnapshot();
        await Click(Find<Button>("ControlTrialSend"));
        Check(Workbench.LastFlowPlan!.Items.Any(item => item.Route.ToComponentId == "attack") && Workbench.LastFlowPlan.Items.All(item => !item.IsEnergy) && Workbench.EditableTrial.GetSnapshot() == paid,
            "次数已用时请求可到阵器，但没有付款金线或重复伤害");

        _stage = "短中文和真实一基坐标";
        await ReloadFlowExample();
        var signal = FormationGridFlowRoutes.Build(Canvas.Model).Single(route => route.Kind == FormationGridModel.PortKind.Signal && route.ToComponentId == "guard");
        await ClickCell(signal.WireCells[0], MouseButton.Right); await Click(Find<Button>("FormationControlTrialTab"));
        var message = Find<Label>("ControlTrialStatus").Text; SafePlayerError(message);
        var port = Canvas.Model.Components["guard"].Ports.Single(p => p.IsInput && p.Kind == FormationGridModel.PortKind.Signal);
        Check(message.Contains($"（{port.Cell.X + 1},{port.Cell.Y + 1}）", StringComparison.Ordinal) && Canvas.ProblemMarkerCount > 0,
            "信号断口用正确一基接口坐标和盘面标记说明");
        Check(Workbench.EditableTrial is null && !Canvas.IsFlowPlaying && Canvas.FlowPlan is null, "坏图清理旧播放，不能沿旧路线继续动画");
        await MoveOutside(); await Capture("08_短报错与断口坐标.png");

        await ReloadFlowExample();
        var power = FormationGridFlowRoutes.Build(Canvas.Model).Single(route => route.Kind == FormationGridModel.PortKind.Energy && route.ToComponentId == "attack");
        await ClickCell(power.WireCells[0], MouseButton.Right); await Click(Find<Button>("FormationControlTrialTab"));
        message = Find<Label>("ControlTrialStatus").Text; SafePlayerError(message);
        var missing = Canvas.Model.Components.Values.SelectMany(part => part.Ports).Where(p => p.IsInput && p.Kind == FormationGridModel.PortKind.Energy && (Canvas.Model.GetConnectionMask(p.Cell) & p.Facing) == 0).ToArray();
        Check(message.Contains("供能线", StringComparison.Ordinal) && missing.Any(p => message.Contains($"（{p.Cell.X + 1},{p.Cell.Y + 1}）", StringComparison.Ordinal)) && Workbench.EditableTrial is null,
            "断供提示对应真实供能入口坐标，不是内部错误链");
        await ReloadFlowExample();
        await ClickCell(Canvas.Model.Components.Values.Single(part => part.Kind == Kind.Source).Origin, MouseButton.Right);
        await Click(Find<Button>("FormationControlTrialTab")); message = Find<Label>("ControlTrialStatus").Text; SafePlayerError(message);
        Check(message.Contains("缺少阵源", StringComparison.Ordinal) && Workbench.EditableTrial is null, "空源给短中文原因并拒绝试演");

        await ReloadFlowExample();
        var diagram = Canvas.Model.Serialize(); var runtime = Workbench.EditableTrial; var state = runtime!.GetSnapshot();
        await Click(Find<Button>("SaveDraft"));
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_draftPath), "{\"version\":999,\"model\":\"unknown\"}");
        await Click(Find<Button>("LoadDraft")); message = Find<Label>("FormationStatus").Text; SafePlayerError(message);
        Check(message.Contains("无法读取", StringComparison.Ordinal) && Canvas.Model.Serialize() == diagram && ReferenceEquals(runtime, Workbench.EditableTrial) && runtime.GetSnapshot() == state,
            "未知坏保存给玩家中文提示，保留旧图和账目");
        await Click(Find<Button>("SaveDraft"));
    }
    private void SafePlayerError(string text)
    {
        _flowPlayerErrors.Add(new { text, board_size = Canvas.Model.BoardSize, marker_count = Canvas.ProblemMarkerCount });
        Check(text.Length <= 65 && !text.Contains('\n') && !System.Text.RegularExpressions.Regex.IsMatch(text, "[A-Za-z_]{2,}"), "玩家报错简短且无内部ID或英文异常尾巴");
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "[（(](\\d+)[,，](\\d+)[）)]"))
            Check(int.Parse(match.Groups[1].Value) is >= 1 && int.Parse(match.Groups[1].Value) <= Canvas.Model.BoardSize && int.Parse(match.Groups[2].Value) is >= 1 && int.Parse(match.Groups[2].Value) <= Canvas.Model.BoardSize,
                "显示的坐标为本盘一基列行");
    }
    private void VerifyDrawnPortDirections()
    {
        foreach (var route in FormationGridFlowRoutes.Build(Canvas.Model))
        {
            var source = Canvas.Model.Components[route.FromComponentId].Ports.Single(p => !p.IsInput && p.Kind == route.Kind && p.Name == route.FromPortName);
            var target = Canvas.Model.Components[route.ToComponentId].Ports.Single(p => p.IsInput && p.Kind == route.Kind && p.Name == route.ToPortName);
            var points = Canvas.GetFlowPath(route).ToArray();
            var segments = points.Zip(points.Skip(1)).Select(pair => pair.Second - pair.First).Where(vector => vector.LengthSquared() > .001).ToArray();
            Check(segments.Length > 0 && segments[0].Dot(FlowDirection(source.Facing)) > .001f && segments[^1].Dot(FlowDirection(target.Facing)) < -.001f,
                "实际绘制从出口向外、沿入口向内，区分同名入出口 " + route.FromComponentId + "→" + route.ToComponentId);
        }
    }
    private static Vector2 FlowDirection(FormationGridModel.Direction direction) => direction switch
    {
        FormationGridModel.Direction.North => Vector2.Up, FormationGridModel.Direction.East => Vector2.Right,
        FormationGridModel.Direction.South => Vector2.Down, FormationGridModel.Direction.West => Vector2.Left, _ => Vector2.Zero
    };
    private async Task CaptureFlowFrames(FormationFlowPlayback.Plan plan, R.Snapshot settled, R.Result result)
    {
        var red = plan.Items.Where(item => !item.IsEnergy && item.Route.FromComponentId == "health").First();
        var gold = plan.Items.First(item => item.IsEnergy);
        var targets = new[] { Math.Max(.13, red.Delay + red.Duration * .55), Math.Max(.23, red.Delay + red.Duration * .88), gold.Delay + gold.Duration * .55 };
        var next = 0d; var index = 0; var shots = 0; var hashes = new List<string>();
        var raw = Evidence + "/动画原帧"; if (_width == 1280) DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(raw));
        var started = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - started < 20000)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var time = Canvas.FlowPlaybackTime;
            if (time + .001 < next && Canvas.IsFlowPlaying) continue;
            using var frame = GetViewport().GetTexture().GetImage(); var hash = BoardHash(frame);
            var filename = $"frame_{index:D4}.png";
            if (_width == 1280 && frame.SavePng(raw + "/" + filename) != Error.Ok) throw new Exception("原始动画帧保存失败");
            _flowFrames.Add(new { file = _width == 1280 ? "动画原帧/" + filename : null, flow_seconds = time, occurrence_id = plan.OccurrenceId, board_sha256 = hash, snapshot = Workbench.EditableTrial!.GetSnapshot() });
            if (Workbench.EditableTrial!.GetSnapshot() != settled || !ReferenceEquals(Workbench.LastControlResult, result)) throw new Exception("动画期间发生了额外业务结算");
            if (shots < targets.Length && time >= targets[shots])
            {
                var shot = $"0{shots + 5}_同次传导_{shots + 1}.png";
                if (frame.SavePng(Evidence + "/" + shot) != Error.Ok) throw new Exception("阶段帧保存失败");
                _shots.Add(new { file = shot, width = _width, height = _height }); hashes.Add(hash); shots++;
            }
            index++; next = time + .08;
            if (!Canvas.IsFlowPlaying) break;
        }
        var boardSide = Canvas.GridRect.Size.X / FormationBoardCanvas.WorkingFraction;
        var localStart = Canvas.GridRect.Position - Vector2.One * ((boardSide - Canvas.GridRect.Size.X) / 2);
        var transform = Canvas.GetGlobalTransformWithCanvas(); var screenStart = transform * localStart; var screenEnd = transform * (localStart + Vector2.One * boardSide);
        var frameLog = new { occurrence_id = plan.OccurrenceId, duration = plan.Duration, final_flow_seconds = Canvas.FlowPlaybackTime,
            wall_seconds = (Time.GetTicksMsec() - started) / 1000d, engine_time_scale = Engine.TimeScale, target_times = targets, captured_stage_frames = shots,
            board_capture_rect = new { x = (int)Math.Floor(screenStart.X), y = (int)Math.Floor(screenStart.Y), width = (int)Math.Ceiling(screenEnd.X - screenStart.X), height = (int)Math.Ceiling(screenEnd.Y - screenStart.Y) },
            settled_snapshot = settled, plan_items = plan.Items, frames = _flowFrames };
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence + "/动画帧.json"), System.Text.Json.JsonSerializer.Serialize(frameLog,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Check(shots == 3 && hashes.Distinct().Count() == 3, "同次发生的前中后真实盘面像素不同，脉点确实移动");
        Check(!Canvas.IsFlowPlaying && Canvas.FlowPlaybackTime >= plan.Duration, "完整覆盖播放末尾，wall仅作看门狗");
    }
    private string BoardHash(Image image)
    {
        var format = image.GetFormat(); var channels = format == Image.Format.Rgba8 ? 4 : format == Image.Format.Rgb8 ? 3 : 0;
        if (channels == 0) return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData()));
        var transform = Canvas.GetGlobalTransformWithCanvas(); var first = transform * Canvas.GridRect.Position; var last = transform * Canvas.GridRect.End;
        var left = Math.Clamp((int)Math.Floor(first.X), 0, image.GetWidth() - 1); var top = Math.Clamp((int)Math.Floor(first.Y), 0, image.GetHeight() - 1);
        var right = Math.Clamp((int)Math.Ceiling(last.X), left + 1, image.GetWidth()); var bottom = Math.Clamp((int)Math.Ceiling(last.Y), top + 1, image.GetHeight());
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256); var bytes = image.GetData();
        for (var y = top; y < bottom; y++) hash.AppendData(bytes.AsSpan((y * image.GetWidth() + left) * channels, (right - left) * channels));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private async Task WaitForFlow()
    {
        var started = Time.GetTicksMsec(); while (Canvas.IsFlowPlaying && Time.GetTicksMsec() - started < 12000) await Frames(1);
        if (Canvas.IsFlowPlaying) throw new Exception("显示播放未结束");
    }
    private void WriteFlowReport(bool passed, string? error)
    {
        var folder = ProjectSettings.GlobalizePath(Evidence); System.IO.Directory.CreateDirectory(folder);
        var report = new { date = "2026-11-09", passed, error, stage = _stage, preview_only = PreviewOnly, resolution = new[] { _width, _height }, native_checks = _checks.Count,
            checks = _checks, screenshots = _shots, flow_occurrence = _flowOccurrence, settled_snapshot = _flowSnapshot, flow_frame_count = _flowFrames.Count, player_errors = _flowPlayerErrors,
            real_journey_loaded = false, character_save_opened = false, older_106_baseline_reused = false, visual_review = "pending category and raw-frame inspection" };
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, passed ? "分类传导验收.json" : "分类传导验收失败.json"), System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
