using Godot;
using System.Reflection;

/// <summary>使用正式境界界面的独立演出预览；临时人物不写入旅程。</summary>
public partial class RealmPresentationPreview : Control
{
    private RealmPanel _panel = null!;
    private Character _actor = null!;
    private HumanData Data => (HumanData)_actor.entity_data!;
    private string _example = "path", _captureDirectory = "";
    private bool _automated, _starting;
    private int _checks;
    private static T Read<T>(object instance, string field) => (T)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void Write(object instance, string field, object value) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    private static void Call(object instance, string method, params object[] arguments) => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
    private async Task Frames(int count = 3) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    private double Clock => GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() + GetNode<GameTimeMgr>("/root/GameTimeManager").get_pending_hours();

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("realm_presentation_preview", StringComparison.OrdinalIgnoreCase), "Use an isolated preview directory.");
            var arguments = OS.GetCmdlineUserArgs();
            _example = arguments.FirstOrDefault(arg => arg.StartsWith("--example="))?[10..] ?? "path";
            _captureDirectory = arguments.FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..] ?? "";
            _automated = arguments.Contains("--verify") || _captureDirectory.Length > 0;
            if (_automated) GetViewport().GuiDisableInput = true;
            Engine.MaxFps = 60;
            if (DisplayServer.GetName() != "headless")
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                if (_automated) GetWindow().Unfocusable = true;
                DisplayServer.WindowSetSize(arguments.Contains("--large-window") ? new Vector2I(1920, 1080) : new Vector2I(1280, 720));
                DisplayServer.WindowSetTitle("突破演出预览 · R重播 · 1凝核 / 2续环 / 3圆满");
            }
            _actor = new Character(); AddChild(_actor);
            _actor.load_from_data(new HumanData
            {
                entity_name = "演出预览", cultivation_realm = "云息七层", spirit_strands = 144,
                condensation_final_score = 125, base_physique = 5, base_intelligence = 5, base_soul = 5,
                root_weights = [34, 33, 33, 0, 0], current_age = 16, max_age = 100
            });
            _actor.Hide();
            _panel = new RealmPanel(); AddChild(_panel);
            _panel.VisibilityChanged += () => { if (!_automated && !_panel.Visible && !_starting) GetTree().Quit(); };
            await Frames(5);
            if (arguments.Contains("--verify"))
            {
                Engine.TimeScale = 12;
                await Verify();
                GD.Print($"REALM_PRESENTATION_OK: checks={_checks}; display={DisplayServer.GetName()}");
                GetTree().Quit(); return;
            }
            if (_captureDirectory.Length > 0)
            {
                Check(DisplayServer.GetName() != "headless", "Captures require an actual renderer.");
                _captureDirectory = ProjectSettings.GlobalizePath(_captureDirectory);
                DirAccess.MakeDirRecursiveAbsolute(_captureDirectory);
                Prepare(_example); await Frames();
                await Capture("before.png");
                await Delay(1.2);
                Trigger(_example); await Delay(1.0);
                await Capture("during.png");
                while (_panel.IsPresenting && Read<Label>(_panel, "_presentationPhase").Text != (_example == "complete" ? "云息十层圆满" : "已突破至" + Data.cultivation_realm)) await Frames(1);
                await Delay(.3); await Capture("result.png");
                await AwaitCompletion(); await Delay(1.5);
                await Capture("after.png");
                GD.Print($"REALM_PRESENTATION_CAPTURE_OK: example={_example}; captures=4; directory={_captureDirectory}");
                GetTree().Quit(); return;
            }
            await PlayExample(_example);
        }
        catch (Exception error) { GD.PushError($"REALM_PRESENTATION_FAIL: {error}"); GetTree().Quit(1); }
    }

    private void Prepare(string example)
    {
        _panel.Hide();
        Data.cultivation_realm = example == "cloud" ? "云息一层" : example == "core" ? "云息六层" : example == "complete" ? "云息十层" : "云息七层";
        Data.spirit_strands = example == "cloud" ? 6 : example == "complete" ? 337 : 144;
        Data.yuanxi_count = example == "complete" ? 8 : 0;
        Data.yuanxi_active = false;
        _panel.show_for(_actor);
        Write(_panel, "_observedRealm", Data.cultivation_realm);
        Write(_panel, "_observedBreaths", Data.yuanxi_count);
    }

    private void Trigger(string example)
    {
        // 只配置临时角色来触发正式呈现回调；这里不模拟或发放游戏奖励。
        if (example == "complete")
        {
            Data.yuanxi_count = 9;
            Call(_panel, "OnYuanxiChanged", _actor);
        }
        else
        {
            Data.cultivation_realm = example == "cloud" ? "云息二层" : example == "core" ? "云息七层" : "云息八层";
            Data.spirit_strands = example == "cloud" ? 18 : example == "core" ? 144 : 202;
            Call(_panel, "OnBreakthrough", _actor, Data.cultivation_realm);
        }
    }

    private async Task PlayExample(string example)
    {
        if (_starting) return;
        _starting = true; _example = example;
        Prepare(example); await Delay(1.0); Trigger(example);
        _starting = false;
    }

    private async Task AwaitCompletion()
    {
        await Frames(2);
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (_panel.IsPresenting)
        {
            if (timeout.Elapsed.TotalSeconds > 25) throw new Exception("Presentation did not finish.");
            await Frames(1);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_automated || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is not (Key.R or Key.Key1 or Key.Key2 or Key.Key3)) return;
        var selected = key.Keycode == Key.Key1 ? "core" : key.Keycode == Key.Key2 ? "path" : key.Keycode == Key.Key3 ? "complete" : _example;
        _ = PlayExample(selected);
        GetViewport().SetInputAsHandled();
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(_captureDirectory + "/" + name);
    }

    private void AssertUnchanged(double time, string realm, int strands, int breaths)
    {
        Check(Math.Abs(Clock - time) < .00001 && Data.cultivation_realm == realm && Data.spirit_strands == strands && Data.yuanxi_count == breaths && Data.condensation_final_score == 125,
            "Presentation changed calendar or cultivation data.");
    }

    private void AssertRestored()
    {
        Check(!_panel.IsPresenting && !Read<VBoxContainer>(_panel, "_presentationLayer").Visible, "Focus layer remained active.");
        Check(Read<List<Control>>(_panel, "_presentationSurroundings").All(control => control.Visible && control.Modulate.A == 1), "Surrounding information was not fully restored.");
        Check(Read<RealmQiVisual>(_panel, "_qiVisual").Scale.IsEqualApprox(Vector2.One), "Visual remained enlarged.");
    }

    private async Task Verify()
    {
        var ready = false;
        _panel.SetPresentationReadiness(() => ready);
        Prepare("path"); Trigger("path"); await Delay(.6);
        Check(_panel.HasPendingPresentation && !_panel.IsPresenting, "Presentation ignored settlement readiness.");
        var queuedAt = Clock;
        ready = true; await Frames(2);
        Check(!_panel.HasPendingPresentation && _panel.IsPresenting, "Settled presentation did not start.");
        Read<Button>(_panel, "_skipAnimation").EmitSignal(BaseButton.SignalName.Pressed);
        AssertRestored(); Check(Clock == queuedAt, "Waiting/presenting advanced the game calendar.");
        foreach (var example in new[] { "cloud", "core", "path", "complete" })
        {
            Prepare(example); Trigger(example); await Frames(2);
            var time = Clock; var realm = Data.cultivation_realm; var strands = Data.spirit_strands; var breaths = Data.yuanxi_count;
            Check(_panel.IsPresenting && Read<Button>(_panel, "_skipAnimation").Visible, "Presentation/skip did not start.");
            await Delay(.6);
            Check(Read<List<Control>>(_panel, "_presentationSurroundings").All(control => !control.Visible), "Surrounding text remained visible during the change.");
            Check(Read<RealmQiVisual>(_panel, "_qiVisual").Scale.X > 1.6f, "Visual was not enlarged.");
            Check(!Read<Label>(_panel, "_presentationPhase").Text.StartsWith("已突破至"), "Result was revealed before the change finished.");
            var close = _panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "关闭");
            Check(close.IsVisibleInTree(), "Close was hidden by focus mode.");
            AssertUnchanged(time, realm, strands, breaths);
            await AwaitCompletion(); AssertRestored(); AssertUnchanged(time, realm, strands, breaths);

            Prepare(example); Trigger(example); await Frames(2);
            Read<Button>(_panel, "_skipAnimation").EmitSignal(BaseButton.SignalName.Pressed);
            AssertRestored();
            await Delay(.6); AssertRestored();
            Check(Data.cultivation_realm == realm && Data.yuanxi_count == breaths, "Skip altered the outcome.");

            Prepare(example); Trigger(example); await Delay(.6);
            _panel.Hide(); AssertRestored();
            await Delay(.6); _panel.show_for(_actor); AssertRestored();
            Check(Data.cultivation_realm == realm && Data.yuanxi_count == breaths, "Close/reopen altered the outcome.");
        }
    }
}
