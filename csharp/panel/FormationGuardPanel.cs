using Godot;
using ActionKind = FormationGuardAssaultRuntime.ActionKind;
using TrialInput = FormationGuardAssaultRuntime.Input;

/// <summary>隔离守攻试运行。由真实控制图和真实角色宿主执行，不把高级图塞进基础盘。</summary>
public partial class FormationGuardPanel : Control
{
    public event Action? Closed;
    public FormationGuardAssaultRuntime Runtime { get; private set; } = null!;
    public FormationGuardAssaultRuntime.Result? LastResult { get; private set; }
    public Character TrialOwner { get; private set; } = null!;
    public Character Enemy { get; private set; } = null!;
    private FormationGuardActor? _actor;
    private long _sequence;
    private TrialInput? _lastInput;
    private readonly System.Collections.Generic.Dictionary<string, SpinBox> _parameters = [];
    private readonly System.Collections.Generic.Dictionary<string, Label> _readouts = [];
    private readonly System.Collections.Generic.List<string> _history = [];
    private GridContainer _stateGrid = null!;
    private Label _result = null!, _totals = null!, _fixtureError = null!;
    private RichTextLabel _steps = null!, _log = null!;
    private GuardFlowCanvas _flow = null!;
    private FormationGuardBoardCanvas _board = null!;
    private OptionButton _viewMode = null!;
    private Button _enlarge = null!;
    private Label _viewHint = null!;
    private CheckButton _enabled = null!, _observation = null!, _supply = null!;
    private SpinBox _health = null!, _damage = null!, _ordinary = null!, _energy = null!;
    private Button _repeat = null!;
    private bool _updating;

    public override void _Ready() => BuildPaperUi();

    private static VBoxContainer Page(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 9); scroll.AddChild(box); return box;
    }
    private void BuildOperations(VBoxContainer box)
    {
        var healthRow = Row(box); _health = Number("GuardHealthInput", 100, 0, 100); healthRow.AddChild(_health);
        healthRow.AddChild(Button("设生命（试验）", "GuardSetHealth", () => Send(ActionKind.SetHealth, (int)_health.Value)));
        var damageRow = Row(box); _damage = Number("GuardDamageInput", 10); damageRow.AddChild(_damage);
        damageRow.AddChild(Button("受击", "GuardDamage", () => Send(ActionKind.Damage, (int)_damage.Value)));
        damageRow.AddChild(Button("治疗", "GuardHeal", () => Send(ActionKind.Heal, (int)_damage.Value)));
        box.AddChild(Button("完成一次施法", "GuardSpell", () => Send(ActionKind.Spell)));
        var ordinaryRow = Row(box); _ordinary = Number("GuardOrdinaryInput", 5); ordinaryRow.AddChild(_ordinary);
        ordinaryRow.AddChild(Button("普通补盾", "GuardOrdinaryShield", () => Send(ActionKind.OrdinaryShield, (int)_ordinary.Value)));
        var energyRow = Row(box); _energy = Number("GuardEnergyInput", 4); energyRow.AddChild(_energy);
        energyRow.AddChild(Button("增加阵能", "GuardAddEnergy", () => Send(ActionKind.Energy, (int)_energy.Value)));
        var actions = Row(box); actions.AddChild(Button("下一共同回合", "GuardNextRound", () => Send(ActionKind.NextRound)));
        _repeat = Button("重发上次", "GuardRepeat", RepeatLast); actions.AddChild(_repeat);
        box.AddChild(Label("受击会先实际扣盾／生命。‘完成施法’是已完成事件输入，不额外施放一张牌。", 12, "786c56"));
        _enabled = Toggle(box, "阵法启用", "GuardEnabled", value => Send(ActionKind.Enable, flag: value));
        _observation = Toggle(box, "生命观测有效", "GuardObservation", value => Send(ActionKind.Observation, flag: value));
        _supply = Toggle(box, "供能来源有效", "GuardSupply", value => Send(ActionKind.Supply, flag: value));
        _result = Label("", 16); _result.Name = "GuardResult"; box.AddChild(_result);
        _steps = new RichTextLabel { Name = "GuardSteps", BbcodeEnabled = false, FitContent = false, ScrollActive = true, CustomMinimumSize = new Vector2(0, 170), SelectionEnabled = true };
        _steps.AddThemeFontSizeOverride("normal_font_size", 18); box.AddChild(_steps);
    }
    private void BuildParameters(VBoxContainer box)
    {
        box.AddChild(Label("本次试演设置", 22));
        box.AddChild(Label("修改后重新试演。设置只用于本页。", 18, "786c56"));
        foreach (var (key, caption, value) in new[]
        {
            ("capacity", "阵源容量", 12), ("initial", "初始阵能", 12), ("recharge", "每共同回合回复", 3),
            ("sourcePeak", "阵源峰值", 6), ("linkPeak", "灵络峰值", 6), ("boardPeak", "盘体峰值", 6),
            ("guardCost", "护罩每段费用", 2), ("guardCapacity", "护罩每段容量", 18), ("guardLoad", "护罩维持负荷", 2),
            ("attackCost", "火击费用", 3), ("attackDamage", "火击伤害", 9), ("attackLoad", "火击负荷", 3),
            ("attackLimit", "每共同回合火击限次", 1), ("low", "生命低阈值%（转守）", 30), ("high", "生命高阈值%（转攻）", 60),
            ("reserve", "攻击后保留阵能", 0)
        })
        {
            var row = Row(box); var captionLabel = Label(caption, 14); captionLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(captionLabel);
            var field = Number("GuardParam_" + key, value, 0, key is "low" or "high" ? 100 : 10000); field.CustomMinimumSize = new Vector2(92, 30); row.AddChild(field); _parameters[key] = field;
        }
        box.AddChild(Button("重新试运行", "GuardRestart", RestartTrial));
        _fixtureError = Label("", 13, "a4533d"); _fixtureError.Name = "GuardFixtureError"; box.AddChild(_fixtureError);
    }
    private int P(string key) => (int)_parameters[key].Value;
    public void RestartTrial()
    {
        try
        {
            var settings = new FormationGuardAssaultRuntime.Settings(P("capacity"), P("initial"), P("recharge"), P("sourcePeak"), P("linkPeak"), P("boardPeak"),
                P("guardCost"), P("guardCapacity"), P("guardLoad"), P("attackCost"), P("attackDamage"), P("attackLoad"), P("attackLimit"), P("low"), P("high"), P("reserve"));
            FormationGuardAssaultRuntime.Validate(settings);
            var owner = TrialCharacter(100, 20); var enemy = TrialCharacter(100, 0);
            var actor = new FormationGuardActor(owner, enemy);
            FormationGuardAssaultRuntime runtime;
            try { runtime = new FormationGuardAssaultRuntime(settings, actor); }
            catch { FreeTrialCharacter(owner); FreeTrialCharacter(enemy); throw; }
            ReleaseActors(); TrialOwner = owner; Enemy = enemy; _actor = actor; Runtime = runtime;
            _sequence = 0; _lastInput = null; LastResult = null; _history.Clear(); _fixtureError.Text = "";
            _health.Value = 100; Send(ActionKind.Refresh); _lastInput = null; _repeat.Disabled = true;
        }
        catch (ArgumentException exception) { _fixtureError.Text = exception.Message; }
    }
    private static Character TrialCharacter(int health, int mana) => new()
    {
        entity_data = new HumanData { max_health = health, current_health = health, max_mana = mana, current_mana = mana }
    };
    public void Send(ActionKind kind, int value = 0, bool flag = true)
    {
        if (Runtime is null || _updating) return;
        var input = new TrialInput("guard_trial_" + ++_sequence, kind, value, flag); Apply(input); _lastInput = input; _repeat.Disabled = false;
    }
    public void RepeatLast() { if (_lastInput is not null) Apply(_lastInput); }
    private void Apply(TrialInput input)
    {
        LastResult = Runtime.Apply(input); Render(LastResult);
        var action = input.Kind switch
        {
            ActionKind.SetHealth => "设生命", ActionKind.Damage => "受击", ActionKind.Heal => "治疗", ActionKind.Spell => "施法完成",
            ActionKind.OrdinaryShield => "普通补盾", ActionKind.Energy => "阵能补给", ActionKind.NextRound => "新共同回合",
            ActionKind.Observation => input.Flag ? "观测恢复" : "观测未知", ActionKind.Supply => input.Flag ? "供能恢复" : "供能失效",
            ActionKind.Enable => input.Flag ? "启用" : "停用", _ => "初始采样"
        };
        _history.Insert(0, $"第{_sequence}次操作 · {action}{(LastResult.Duplicate ? "（同次重发）" : "")}：{string.Join(" ", LastResult.Messages)}");
        if (_history.Count > 8) _history.RemoveRange(8, _history.Count - 8);
        _log.Text = string.Join("\n", _history);
    }
    private void Render(FormationGuardAssaultRuntime.Result result)
    {
        var s = result.Snapshot; var config = Runtime.Configuration;
        _readouts["health"].Text = $"{s.Health}/{s.MaxHealth}"; _readouts["mana"].Text = s.Mana.ToString();
        _readouts["enemy"].Text = s.EnemyHealth.ToString(); _readouts["shield"].Text = s.Shield.ToString();
        _readouts["guard"].Text = $"{s.GuardRemaining} · {(s.GuardRunning ? "运行" : "停止")}";
        _readouts["source"].Text = $"{s.SourceEnergy}/{config.SourceCapacity}";
        _readouts["mode"].Text = s.ModeText + " / 攻击" + Truth(s.AttackAllowed);
        _totals.Text = $"共同回合{s.Round}　护罩付款{s.GuardPayments}次　火击{s.Attacks}次　实际耗阵能{s.EnergySpent}　{(s.GuardPaidThisRound ? "本段护罩额度已用" : "本段尚未取得护罩")}";
        _result.Text = string.Join("\n", result.Messages);
        _steps.Text = result.Duplicate ? "同次操作已处理：没有重复扣费或重新执行。" : string.Join("\n", result.Steps.Select(step => step.Label + "：" + step.Value));
        _updating = true; _enabled.SetPressedNoSignal(s.Enabled); _observation.SetPressedNoSignal(s.ObservationKnown); _supply.SetPressedNoSignal(s.SupplyValid); _updating = false;
        _flow.SetState(Runtime, result);
        _board.SetState(Runtime, result);
        var hasUnplacedBudget = Runtime.Configuration.AttackReserve > 0;
        _viewMode.SetItemDisabled(0, hasUnplacedBudget); if (hasUnplacedBudget) _viewMode.Select(1); ApplyView();
    }
    private void ApplyView()
    {
        var board = _viewMode.Selected == 0; _board.Visible = board; _flow.Visible = !board; _enlarge.Disabled = !board;
        _viewHint.Text = Runtime is not null && Runtime.Configuration.AttackReserve > 0 ? "附加预算节点尚无实体素材，当前用流程核对完整控制图。" : board ? "朱砂传信号，浅赭金供能；悬停元件查看当前值。" : "流程读取同一运行定义；许可与实际执行分别显示。";
    }
    private void OpenBoardCloseup()
    {
        if (LastResult is null || GetNodeOrNull<Control>("GuardBoardEnlarged") is not null) return;
        var overlay = new Control { Name = "GuardBoardEnlarged", ZIndex = 30, MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(overlay);
        var design = FormationPaperChrome.Mount(overlay);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, side == "top" ? 10 : 24);
        design.AddChild(margin); var box = new VBoxContainer(); margin.AddChild(box);
        var header = FormationPaperChrome.Paper("GuardCloseupHeader", 8); box.AddChild(header);
        var bar = Row(header); var title = Label("守攻阵", 22); title.AutowrapMode = TextServer.AutowrapMode.Off; title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(title); bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        bar.AddChild(FormationPaperChrome.Return("GuardBoardClose", () => overlay.QueueFree()));
        var canvas = new FormationGuardBoardCanvas { Name = "GuardBoardCloseupCanvas", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ShowLabels = false };
        box.AddChild(canvas); canvas.SetState(Runtime, LastResult);
    }
    public static string Truth(FormationTruth truth) => truth == FormationTruth.True ? "允许" : truth == FormationTruth.False ? "关闭" : "未知";
    private void ReleaseActors()
    {
        if ((_actor as object) is IDisposable disposable) disposable.Dispose();
        FreeTrialCharacter(TrialOwner); FreeTrialCharacter(Enemy); _actor = null;
    }
    private static void FreeTrialCharacter(Character? actor)
    {
        if (!GodotObject.IsInstanceValid(actor)) return;
        var data = actor!.entity_data; actor.Free();
        if (GodotObject.IsInstanceValid(data)) data.Dispose();
    }
    public void Close() { Closed?.Invoke(); QueueFree(); }
    public override void _ExitTree() => ReleaseActors();
    private CheckButton Toggle(Node parent, string title, string name, Action<bool> action)
    {
        var check = new CheckButton { Name = name, Text = title, ButtonPressed = true };
        check.Toggled += value => { if (!_updating) action(value); }; parent.AddChild(check); return check;
    }
    private static HBoxContainer Row(Node parent) { var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 7); parent.AddChild(row); return row; }
    private static SpinBox Number(string name, int value, int min = 0, int max = 10000)
    {
        var field = new SpinBox { Name = name, MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(86, 43), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        CraftingPaperStyle.ApplyInput(field.GetLineEdit(), 22); return field;
    }
    private static Label Label(string text, int size, string color = "39433c")
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", Math.Max(18, size));
        label.AddThemeColorOverride("font_color", color == "a4533d" ? CraftingPaperStyle.Danger : color == "39433c" ? CraftingPaperStyle.Ink : CraftingPaperStyle.Muted); return label;
    }
    private static Button Button(string text, string name, Action action)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 46) }; CraftingPaperStyle.ApplyButton(button); button.Pressed += action; return button;
    }
    private static Theme PaperTheme() => CraftingPaperStyle.MakeTheme();

    private partial class GuardFlowCanvas : Control
    {
        private FormationGuardAssaultRuntime? _runtime;
        private FormationGuardAssaultRuntime.Result? _result;
        private readonly System.Collections.Generic.Dictionary<string, ControlStep> _values = [];
        public void SetState(FormationGuardAssaultRuntime runtime, FormationGuardAssaultRuntime.Result result)
        {
            _runtime = runtime; _result = result;
            if (!result.Duplicate) { _values.Clear(); foreach (var step in result.Steps) _values[step.NodeId] = step; }
            QueueRedraw();
        }
        public override void _Draw()
        {
            if (_runtime is null || _result is null) return;
            var reserve = _runtime.Configuration.AttackReserve > 0;
            var positions = new System.Collections.Generic.Dictionary<string, Vector2>
            {
                ["health"] = new(20, 25), ["compare"] = new(250, 25), ["branch"] = new(540, 25),
                ["guard"] = new(540, 245), ["invert"] = new(280, 170), ["cast"] = new(20, 310),
                ["switch"] = new(280, 310), ["attack"] = new(280, 455),
                ["energy"] = new(20, 25), ["reserve"] = new(20, 170), ["allow"] = new(280, 170)
            };
            if (reserve)
            {
                positions["health"] = new(250, 25); positions["compare"] = new(490, 25); positions["branch"] = new(780, 25);
                positions["guard"] = new(780, 245); positions["invert"] = new(540, 170); positions["allow"] = new(280, 170);
                positions["cast"] = new(250, 310); positions["switch"] = new(540, 310); positions["attack"] = new(540, 455);
            }
            var bounds = new Vector2(reserve ? 1020 : 790, 565);
            var scale = Mathf.Min(Size.X / bounds.X, Size.Y / bounds.Y);
            DrawSetTransform((Size - bounds * scale) / 2, 0, Vector2.One * scale);
            var font = GetThemeDefaultFont(); var size = new Vector2(205, 87);
            foreach (var link in _runtime.Circuit.Links)
            {
                if (!positions.TryGetValue(link.From, out var from) || !positions.TryGetValue(link.To, out var to)) continue;
                var a = from + size / 2; var b = to + size / 2;
                var vertical = Math.Abs(a.Y - b.Y) > Math.Abs(a.X - b.X);
                var axis = vertical ? new Vector2(0, Math.Sign(b.Y - a.Y)) : new Vector2(Math.Sign(b.X - a.X), 0);
                a += axis * (vertical ? size.Y : size.X) / 2; b -= axis * (vertical ? size.Y : size.X) / 2;
                var value = _values.GetValueOrDefault(link.From);
                var eventLine = _runtime.Circuit.Nodes[link.From].Kind is FormationControlCircuit.NodeKind.EventInput or FormationControlCircuit.NodeKind.Switch;
                var active = value?.Passed == true && (!eventLine || !_result.Duplicate);
                var color = new Color(active ? "b65d48" : "ac9c80");
                DrawLine(a, b, color, 2.1f, true);
                var tip = (b - a).Normalized(); var normal = new Vector2(-tip.Y, tip.X);
                DrawColoredPolygon(new[] { b, b - tip * 8 + normal * 4, b - tip * 8 - normal * 4 }, color);
            }
            foreach (var node in _runtime.Circuit.Nodes.Values)
            {
                if (!positions.TryGetValue(node.Id, out var position)) continue;
                var step = _values.GetValueOrDefault(node.Id); var value = step?.Value ?? "待采样";
                var stateInput = node.Kind == FormationControlCircuit.NodeKind.StateInput;
                var eventNode = node.Kind is FormationControlCircuit.NodeKind.EventInput or FormationControlCircuit.NodeKind.Switch or FormationControlCircuit.NodeKind.AttackOutput;
                if (_result.Duplicate && eventNode) value = "重发未执行";
                var fill = step?.Passed == true ? "dce2d0" : value.Contains("未知") ? "e6ccb7" : "e0d2b6";
                var style = new StyleBoxFlat { BgColor = new Color(fill), BorderColor = new Color("8f7a5b"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 };
                DrawStyleBox(style, new Rect2(position, size));
                DrawString(font, position + new Vector2(8, 26), node.Label, HorizontalAlignment.Center, size.X - 16, 22, new Color("39433c"));
                var type = eventNode ? "事件" : stateInput ? "数值" : "开关状态";
                DrawString(font, position + new Vector2(8, 47), type + " · " + value, HorizontalAlignment.Center, size.X - 16, 16, new Color("76684f"));
                var factual = node.Id == "guard" ? (_result.Snapshot.GuardRunning ? $"实际运行 · 剩{_result.Snapshot.GuardRemaining}" : "实际停止") : node.Id == "attack" ? $"实际火击累计{_result.Snapshot.Attacks}次" : node.Id == "compare" ? $"≤{_runtime.Configuration.LowHealth}%守 ≥{_runtime.Configuration.HighHealth}%攻" : "";
                if (factual.Length > 0) DrawString(font, position + new Vector2(7, 71), factual, HorizontalAlignment.Center, size.X - 14, 15, new Color("76684f"));
            }
            DrawSetTransform(Vector2.Zero);
        }
    }
}
