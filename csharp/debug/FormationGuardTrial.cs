using Godot;

/// <summary>不入树的独立试验人物，隐藏原生点击验收或单独手动试运行入口。</summary>
public partial class FormationGuardTrial : Control
{
    private const string Evidence = "res://docs/美术规范/守攻阵原生试运行_20261109";
    private FormationGuardPanel _panel = null!;
    private readonly System.Collections.Generic.List<string> _checks = [];
    public override async void _Ready()
    {
        GetWindow().Mode = Window.ModeEnum.Windowed; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (!OS.GetCmdlineUserArgs().Contains("--formation-guard-smoke"))
        {
            _panel = new FormationGuardPanel { Name = "FormationGuardPanel" }; _panel.Closed += () => GetTree().Quit(); AddChild(_panel); return;
        }
        GetWindow().Position = new Vector2I(-30000, -30000);
        try
        {
            await Resize(1920, 1080);
            var background = new ColorRect { Color = new Color("242a2e") }; background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
            var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
            AddChild(margin); var workbench = new FormationWorkbench(); margin.AddChild(workbench); await Frames(4);
            var entry = workbench.FindChild("OpenGuardTrial", true, false) as Button ?? throw new Exception("缺少守攻入口");
            await ClickControl(entry);
            _panel = GetTree().Root.GetNodeOrNull<FormationGuardPanel>("FormationGuardPanel") ?? throw new Exception("守攻页没有打开");
            Check(_panel.Runtime.GetSnapshot().Health == 100 && _panel.Runtime.GetSnapshot().EnemyHealth == 100, "设计台按钮真实打开独立人物守攻页");
            await SetHealth(20);
            var low = _panel.Runtime.GetSnapshot(); var config = _panel.Runtime.Configuration;
            Check(low.Mode == FormationTruth.True && low.GuardRunning && low.GuardRemaining == config.GuardCapacity && low.SourceEnergy == config.InitialEnergy - config.GuardCost, "低生命实际开罩并扣试验阵能");
            Check(_panel.TrialOwner.shield == low.Shield && _panel.TrialOwner.current_health == 20, "显示护盾与真实Character同步");
            await Capture("01_低生命守备_1920.png");

            await Resize(1280, 900); await SetHealth(45);
            var middle = _panel.Runtime.GetSnapshot();
            Check(middle.Mode == FormationTruth.True && middle.GuardPayments == low.GuardPayments && middle.SourceEnergy == low.SourceEnergy, "从低区恢复到中间保持守备且不重复付款");
            await Click("GuardSpell");
            Check(_panel.Runtime.GetSnapshot().EnemyHealth == 100 && _panel.Runtime.GetSnapshot().Attacks == 0, "守备许可关闭事件开关，实际不攻击");
            await Capture("02_区间保持_1280.png");

            await SetHealth(80); var beforeAttack = _panel.Runtime.GetSnapshot(); await Click("GuardSpell");
            var attack = _panel.Runtime.GetSnapshot();
            Check(attack.Mode == FormationTruth.False && !attack.GuardRunning && attack.EnemyHealth == 100 - config.AttackDamage && attack.SourceEnergy == beforeAttack.SourceEnergy - config.AttackCost, "高生命撤本器罩，施法事件真实扣费攻击敌人");
            await Click("GuardRepeat");
            Check(_panel.LastResult?.Duplicate == true && _panel.Runtime.GetSnapshot().EnemyHealth == attack.EnemyHealth && _panel.Runtime.GetSnapshot().EnergySpent == attack.EnergySpent, "重发同一施法不重复攻击或扣费");
            await Resize(1920, 1080); await Capture("03_转攻实际生效_1920.png");

            await RestartThroughUi(); await Resize(1280, 900); await SetHealth(20);
            await Click("GuardOrdinaryShield"); await Click("GuardDamage");
            var mixed = _panel.Runtime.GetSnapshot(); Check(mixed.GuardRemaining == config.GuardCapacity - 10 && mixed.Shield == mixed.GuardRemaining + 5, "真实受击按来源扣除护罩，普通盾仍在");
            await SetHealth(80); var stopped = _panel.Runtime.GetSnapshot();
            Check(!stopped.GuardRunning && stopped.GuardRemaining == 0 && stopped.Shield == 5 && _panel.TrialOwner.shield == 5, "关罩只撤本器余量，普通5盾保持");
            await SetHealth(20); var sameRound = _panel.Runtime.GetSnapshot();
            Check(!sameRound.GuardRunning && sameRound.GuardPayments == 1 && sameRound.SourceEnergy == stopped.SourceEnergy, "同段关闭再开不刷新护罩容量或费用账本");
            await Capture("04_关罩保留普通盾_1280.png");

            await RestartThroughUi(); await SetHealth(20); await Click("GuardObservation");
            var unknown = _panel.Runtime.GetSnapshot(); Check(unknown.Mode == FormationTruth.Unknown && unknown.GuardRunning, "观测未知不改成安全，已运行护罩保持实际服务");
            await Click("GuardNextRound"); var renewed = _panel.Runtime.GetSnapshot();
            Check(renewed.GuardPayments == 2 && renewed.GuardRemaining == config.GuardCapacity && renewed.EnergySpent == 2L * config.GuardCost, "新共同回合真实续付，即使公共盾总量相同仍成功");
            await Click("GuardRepeat"); Check(_panel.Runtime.GetSnapshot().Round == renewed.Round && _panel.Runtime.GetSnapshot().GuardPayments == 2, "重发回合边界不重复续费或回能");
            await Click("GuardSpell"); Check(_panel.Runtime.GetSnapshot().Attacks == 0 && _panel.Runtime.GetSnapshot().EnemyHealth == 100, "未知期间施法不被补发为攻击");
            await Capture("05_未知与真实续费_1280.png");
            await SetHealth(0); var dead = _panel.Runtime.GetSnapshot(); await Click("GuardSpell");
            Check(!dead.GuardRunning && _panel.Runtime.GetSnapshot().Attacks == 0 && _panel.Runtime.GetSnapshot().EnergySpent == dead.EnergySpent, "死者没有新防护或攻击输出");
            Check(GetViewport().GetVisibleRect().Encloses(_panel.GetGlobalRect()), "1280原生守攻页完整位于视口内");
            Check(GetViewport().GetVisibleRect().Encloses(Find<RichTextLabel>("GuardHistory").GetGlobalRect()), "1280底部操作记录未被标题或正文挤出窗口");
            await Click("GuardReturn"); await Frames(3);
            Check(!GodotObject.IsInstanceValid(_panel) && workbench.IsVisibleInTree(), "原生返回释放试验人物并回到设计台");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var record = new { date = "2026-11-09", passed = true, isolated_profile = true, fixture_characters_enter_tree = false, character_save_opened = false, input = "native Viewport.PushInput mouse hit testing", checks = _checks };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence + "/原生验收.json"), System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print($"FORMATION_GUARD_UI_PASS checks={_checks.Count}"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError("FORMATION_GUARD_UI_FAIL " + exception); GetTree().Quit(1); }
    }
    private T Find<T>(string name) where T : Node => _panel.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private async System.Threading.Tasks.Task SetHealth(int value) { Find<SpinBox>("GuardHealthInput").Value = value; await Click("GuardSetHealth"); }
    private async System.Threading.Tasks.Task RestartThroughUi()
    {
        var tabs = Find<TabContainer>("GuardSidebar"); var bar = tabs.GetTabBar(); await ClickPoint(bar.GlobalPosition + bar.GetTabRect(1).GetCenter());
        var button = Find<Button>("GuardRestart"); var scroll = button.FindParent("试验参数") as ScrollContainer ?? throw new Exception("参数滚动区域丢失");
        scroll.EnsureControlVisible(button); await Frames(3); await ClickControl(button);
        await ClickPoint(bar.GlobalPosition + bar.GetTabRect(0).GetCenter());
        Check(_panel.Runtime.GetSnapshot().Health == 100 && _panel.Runtime.GetSnapshot().EnergySpent == 0, "参数页重新试运行明确新建试验，不洗正式局内账本");
    }
    private async System.Threading.Tasks.Task Click(string name) => await ClickControl(Find<Control>(name));
    private async System.Threading.Tasks.Task ClickControl(Control control)
    {
        if (!control.IsVisibleInTree()) throw new Exception("控件不可见：" + control.Name);
        await ClickPoint(control.GetGlobalRect().GetCenter());
    }
    private async System.Threading.Tasks.Task ClickPoint(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true); await Frames(2);
    }
    private async System.Threading.Tasks.Task Resize(int width, int height) { GetWindow().Size = new(width, height); GetWindow().ContentScaleSize = new(width, height); await Frames(5); }
    private async System.Threading.Tasks.Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool passed, string message) { if (!passed) throw new Exception(message); _checks.Add(message); GD.Print("FORMATION_GUARD_UI_CHECK " + message); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence)); using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty() && image.SavePng(Evidence + "/" + name) == Error.Ok, "原生截图 " + name);
    }
}
