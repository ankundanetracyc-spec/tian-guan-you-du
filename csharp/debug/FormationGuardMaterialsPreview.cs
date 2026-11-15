using Godot;

/// <summary>铜纹守攻素材的隔离原生展示验收；复用真实试运行器，不读取人物存档。</summary>
public partial class FormationGuardMaterialsPreview : Control
{
    private const string Evidence = FormationGuardBoardCanvas.ArtDirectory;
    private FormationGuardPanel _panel = null!;
    private readonly System.Collections.Generic.List<string> _checks = [];
    public override async void _Ready()
    {
        GetWindow().Mode = Window.ModeEnum.Windowed; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new FormationGuardPanel { Name = "FormationGuardPanel" }; AddChild(_panel);
        if (!OS.GetCmdlineUserArgs().Contains("--formation-guard-art-smoke")) { _panel.Closed += () => GetTree().Quit(); return; }
        GetWindow().Position = new(-30000, -30000);
        try
        {
            await Resize(1920, 1080); var board = Find<FormationGuardBoardCanvas>("GuardBoardCanvas");
            Check(board.Visible && Find<OptionButton>("GuardViewMode").Selected == 0, "默认显示真实铜纹素材阵盘");
            Check(board.HasAllAssets, "盘底与十件实物素材全部来自当前铜纹图");
            Check(board.GetLayoutIssues().Count == 0, "实际控制graph连接齐全，朱砂供能无交叉或穿件");
            await SetHealth(20); var guard = _panel.Runtime.GetSnapshot();
            Check(guard.GuardRunning && guard.GuardPayments == 1 && guard.EnergySpent == _panel.Runtime.Configuration.GuardCost, "低生命盘面反映真实开罩和付款");
            await Capture("01_守备态_1920.png");
            await SetHealth(80); await Click("GuardSpell"); var attack = _panel.Runtime.GetSnapshot();
            Check(attack.Attacks == 1 && attack.EnemyHealth < 100 && !attack.GuardRunning, "攻击路径显示同一运行器真实伤敌和停罩");
            await Capture("02_攻态_1920.png");
            await Resize(1280, 900); await SetHealth(20); await Click("GuardNextRound");
            Check(_panel.Runtime.GetSnapshot().GuardRunning && GetViewport().GetVisibleRect().Encloses(Find<RichTextLabel>("GuardHistory").GetGlobalRect()), "1280守备阵盘与底部记录完整可见");
            await Capture("03_守备态_1280.png");
            await Resize(1920, 1080); await Click("GuardEnlargeBoard");
            Check(Find<FormationGuardBoardCanvas>("GuardBoardCloseupCanvas").HasAllAssets, "实际按钮打开原生放大阵盘");
            await Capture("04_原生放大阵盘_1920.png"); await Click("GuardBoardClose");
            await CaptureBoard("05_阵盘单独大图.png", new(1600, 1600));
            await CaptureBoard("06_朱砂供能接口.png", new(1000, 1600), new Rect2(new Vector2(6.15f, 1.2f) * 128, new Vector2(1.85f, 3.6f) * 128));
            await CaptureBoard("08_阵源与金线T.png", new(1600, 600), new Rect2(new Vector2(2.8f, 5.7f) * 128, new Vector2(4.7f, 1.7f) * 128));
            await CaptureBoard("09_开关三口接口.png", new(1100, 1000), new Rect2(new Vector2(3.55f, 4.4f) * 128, new Vector2(2.1f, 1.9f) * 128));
            await Resize(1280, 900); var energyBefore = _panel.Runtime.GetSnapshot().SourceEnergy;
            var modes = Find<OptionButton>("GuardViewMode"); modes.Select(1); modes.EmitSignal(OptionButton.SignalName.ItemSelected, 1); await Frames(3);
            Check(Find<Control>("GuardLiveGraph").Visible && !board.Visible && _panel.Runtime.GetSnapshot().SourceEnergy == energyBefore, "流程切换读取同一运行状态且不结算资源");
            await Capture("07_流程复核_1280.png");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence)); WriteJson("layout_验收.json", board.DescribeLayout());
            var settingsTab = Find<TabContainer>("GuardSidebar"); var bar = settingsTab.GetTabBar(); await ClickPoint(bar.GlobalPosition + bar.GetTabRect(1).GetCenter());
            var reserve = Find<SpinBox>("GuardParam_reserve"); reserve.Value = 2; var restart = Find<Button>("GuardRestart");
            var scroll = restart.FindParent("试验参数") as ScrollContainer ?? throw new Exception("缺少参数滚动区"); scroll.EnsureControlVisible(restart); await Frames(3); await ClickControl(restart);
            Check(modes.Selected == 1 && modes.IsItemDisabled(0), "附加预算节点尚无素材时自动使用完整流程，不伪装实物连接");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            WriteJson("原生素材验收.json", new { date = "2026-11-09", passed = true, isolated_profile = true, real_runtime = true, bitmap_pixel_processing = false, screenshots = "native viewport / subviewport render", checks = _checks });
            GD.Print($"FORMATION_GUARD_ART_PASS checks={_checks.Count}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("FORMATION_GUARD_ART_FAIL " + error); GetTree().Quit(1); }
    }
    private static void WriteJson(string file, object value) => System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence + file), System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    private T Find<T>(string name) where T : Node => _panel.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private async System.Threading.Tasks.Task SetHealth(int health) { Find<SpinBox>("GuardHealthInput").Value = health; await Click("GuardSetHealth"); }
    private async System.Threading.Tasks.Task Click(string name) => await ClickControl(Find<Control>(name));
    private async System.Threading.Tasks.Task ClickControl(Control control)
    {
        if (!control.IsVisibleInTree()) throw new Exception("控件不可见 " + control.Name); await ClickPoint(control.GetGlobalRect().GetCenter());
    }
    private async System.Threading.Tasks.Task ClickPoint(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true); await Frames(2);
    }
    private async System.Threading.Tasks.Task Resize(int w, int h) { GetWindow().Size = new(w, h); GetWindow().ContentScaleSize = new(w, h); await Frames(5); }
    private async System.Threading.Tasks.Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool passed, string message) { if (!passed) throw new Exception(message); _checks.Add(message); GD.Print("FORMATION_GUARD_ART_CHECK " + message); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); Check(image is not null && !image.IsEmpty() && image.SavePng(Evidence + name) == Error.Ok, "原生截图 " + name);
    }
    private async System.Threading.Tasks.Task CaptureBoard(string name, Vector2I size, Rect2? focus = null)
    {
        var viewport = new SubViewport { Name = "CopperEvidenceViewport", Size = size, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport); var board = new FormationGuardBoardCanvas { Size = size, Theme = _panel.Theme, FocusRegion = focus, ShowLabels = focus is null }; viewport.AddChild(board);
        board.SetState(_panel.Runtime, _panel.LastResult!); await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = viewport.GetTexture().GetImage(); Check(image is not null && !image.IsEmpty() && image.SavePng(Evidence + name) == Error.Ok, "独立原生盘面渲染 " + name);
        viewport.QueueFree(); await Frames(2);
    }
}
