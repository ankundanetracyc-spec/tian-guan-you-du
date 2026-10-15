using Godot;

/// <summary>水墨行动过渡。吐纳演出只推演显示时间，结束后一次性提交行动与跨月演算。</summary>
public partial class ActionTransitionPanel : Control
{
    private const string AssetRoot = "res://textures/ui/cultivation_transition_20261015/";
    private static readonly Vector2 DesignSize = new(1672, 941);
    private ColorRect _shade = null!;
    private Control _center = null!, _ringLayer = null!;
    private ShaderMaterial _ringMaterial = null!;
    private Label _title = null!, _state = null!, _detail = null!, _elapsed = null!;
    private Label _startDate = null!, _currentDate = null!, _endDate = null!;
    private ActionTimelineStrip _timeline = null!;
    private GameTimeMgr? _clock;
    private Tween? _animation;
    private bool _playing, _cultivation, _committed, _previousPause;
    private int _startHours, _plannedHours, _lastShownHour = -1;
    private double _progressValue, _motion;
    public bool IsPlaying => _playing;
    public bool RegisterAsAction { get; set; } = true;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 900;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
        if (RegisterAsAction) AddToGroup("action_transition");
        build();
        Resized += fit;
        fit();
        Hide();
    }

    public override void _Process(double delta)
    {
        if (!_playing) return;
        _motion += delta;
        // 呼吸只改变笔触的极小幅度，不旋转进度，也不让文字随之缩放。
        _ringLayer.Scale = Vector2.One * (1f + .004f * Mathf.Sin((float)_motion * 1.25f));
    }

    public override void _Input(InputEvent @event)
    {
        if (_playing) GetViewport().SetInputAsHandled();
    }

    private void begin(string title, bool cultivation, bool pauseTree = true, double fadeSeconds = .4)
    {
        HoverInfoManager.HideAll();
        _playing = true; _cultivation = cultivation; _committed = false;
        _previousPause = GetTree().Paused;
        _motion = 0; _lastShownHour = -1;
        _title.Text = title;
        _state.Visible = !cultivation;
        _state.Text = "时光流转中…";
        _state.Position = new Vector2(236, cultivation ? 838 : 688);
        _detail.Position = new Vector2(236, cultivation ? 874 : 742);
        _detail.Size = new Vector2(1200, cultivation ? 55 : 120);
        _detail.Text = "";
        _elapsed.Visible = _timeline.Visible = _startDate.Visible = _currentDate.Visible = _endDate.Visible = cultivation;
        set_progress(0);
        _shade.Modulate = _center.Modulate = new Color(1, 1, 1, 0);
        _ringLayer.Scale = Vector2.One;
        Show(); MoveToFront();
        if (pauseTree) GetTree().Paused = true;
        _animation = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _animation.SetParallel();
        _animation.TweenProperty(_shade, "modulate:a", 1f, fadeSeconds);
        _animation.TweenProperty(_center, "modulate:a", 1f, fadeSeconds);
        _animation.SetParallel(false);
    }

    /// <summary>跨场景加载只借用视觉层，暂停和场景生命周期由调用者管理。</summary>
    public async System.Threading.Tasks.Task show_loading_async(string title, string detail, double fadeSeconds = .25)
    {
        if (_playing) throw new InvalidOperationException("加载界面正在处理另一项行动");
        begin(title, false, pauseTree: false, fadeSeconds: fadeSeconds);
        _state.Text = "正在准备…";
        _detail.Text = detail;
        await ToSignal(_animation!, Tween.SignalName.Finished);
    }

    public void update_loading(double ratio, string state)
    {
        set_progress(ratio * 100);
        _state.Text = state;
    }

    public async System.Threading.Tasks.Task hide_loading_async(double seconds = .3, bool completed = true)
    {
        if (!_playing) return;
        if (completed) { set_progress(100); _state.Text = "已就绪"; }
        else _state.Text = "载入未完成";
        var fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        fade.TweenProperty(_shade, "modulate:a", 0f, seconds);
        fade.TweenProperty(_center, "modulate:a", 0f, seconds);
        await ToSignal(fade, Tween.SignalName.Finished);
        Hide(); _playing = false;
    }

    public void play(string title, string plan, string complete, Action settle, Action? after = null)
    {
        if (_playing) return;
        begin(title, false);
        _detail.Text = plan;
        _animation!.TweenMethod(Callable.From<double>(set_progress), 0d, 92d, 1.65)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _animation.TweenCallback(Callable.From(() => settle_and_finish(settle, complete, after)));
    }

    public void play_cultivation(string title, int hours, string complete, Action settle, Action? after = null)
    {
        if (_playing) return;
        _clock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (_clock is null || hours <= 0) { play(title, "", complete, settle, after); return; }
        _startHours = _clock.get_total_game_hours();
        _plannedHours = hours;
        begin(title, true);
        _startDate.Text = GameTimeText.Calendar(_startHours);
        _endDate.Text = "预计结束 · " + GameTimeText.Calendar(_startHours + hours);
        update_preview();
        // 短行动也有完整入定过程；长段闭关延长演出但不按游戏时长等比例等待。
        var seconds = Math.Clamp(3.5 + Math.Log10(Math.Max(1, hours)) * 1.35, 3.5, 9);
        _animation!.TweenMethod(Callable.From<double>(set_progress), 0d, 100d, seconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _animation.TweenInterval(.25);
        _animation.TweenCallback(Callable.From(() => settle_and_finish(settle, complete, after)));
    }

    private void set_progress(double value)
    {
        _progressValue = Math.Clamp(value, 0, 100);
        _ringMaterial.SetShaderParameter("fill_amount", (float)(_progressValue / 100));
        _timeline.Ratio = (float)(_progressValue / 100);
        if (_cultivation) update_preview();
    }

    private void update_preview()
    {
        var elapsed = _committed && _clock is not null
            ? Math.Max(0, _clock.get_total_game_hours() - _startHours)
            : (int)Math.Floor(_plannedHours * _progressValue / 100);
        if (elapsed == _lastShownHour) return;
        _lastShownHour = elapsed;
        _elapsed.Text = $"已过 {GameTimeText.Duration(elapsed)} · 共 {GameTimeText.Duration(_plannedHours)}";
        _currentDate.Text = "现在 · " + GameTimeText.Calendar(_startHours + elapsed);
        if (_committed) _endDate.Text = "结束 · " + GameTimeText.Calendar(_startHours + elapsed);
    }

    private async void settle_and_finish(Action settle, string complete, Action? after)
    {
        var manager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var deferred = false;
        var success = false;
        try
        {
            _state.Show(); _state.Text = "正在结算…";
            if (manager is not null) { manager.defer_time_settlement(); deferred = true; }
            settle();
            _committed = true; _lastShownHour = -1;
            if (_cultivation) update_preview();
            if (manager is not null)
                await manager.flush_time_settlement_async((ratio, state) =>
                {
                    if (!_cultivation) set_progress(92 + ratio * 8);
                    _state.Text = state;
                });
            _state.Text = "已完成"; _detail.Text = complete; set_progress(100);
            if (_cultivation && _clock?.get_total_game_hours() == _startHours)
            { _state.Text = "本次未推进时间"; _detail.Text = "请返回静室查看当前修炼条件。"; }
            await ToSignal(GetTree().CreateTimer(_cultivation ? .9 : .35, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            success = true;
        }
        catch (Exception ex)
        {
            GD.PushError($"长行动结算失败：{ex}");
            if (deferred && manager?.HasPendingSettlement == true) await manager.flush_time_settlement_async();
        }
        finally
        {
            GetTree().Paused = _previousPause;
            Hide(); _playing = false;
        }
        if (success) after?.Invoke();
    }

    // 真正的后台演算直接反映计算进度，不额外添加修炼演出等待。
    public async System.Threading.Tasks.Task run_work_async(string title, Func<Action<double, string>, System.Threading.Tasks.Task> work)
    {
        if (_playing) throw new InvalidOperationException("加载界面正在处理另一项行动");
        HoverInfoManager.HideAll();
        _playing = true; _cultivation = false;
        _previousPause = GetTree().Paused;
        try
        {
            _title.Text = title; _state.Text = "正在准备…"; _state.Show();
            _state.Position = new Vector2(236, 688);
            _detail.Position = new Vector2(236, 742); _detail.Size = new Vector2(1200, 120);
            _detail.Text = "人物、行旅与行动记录结算完成后继续。";
            _elapsed.Hide(); _timeline.Hide(); _startDate.Hide(); _currentDate.Hide(); _endDate.Hide();
            set_progress(0); _shade.Modulate = _center.Modulate = Colors.White;
            Show(); MoveToFront(); GetTree().Paused = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await work((ratio, state) => { set_progress(ratio * 100); _state.Text = state; });
        }
        finally
        {
            Hide(); _playing = false; GetTree().Paused = _previousPause;
        }
    }

    private void fit()
    {
        var scale = Math.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _center.Scale = Vector2.One * scale;
        _center.Position = (Size - DesignSize * scale) * .5f;
    }

    private static ShaderMaterial ring_material(Color color, float fill)
    {
        var shader = new Shader { Code = """
            shader_type canvas_item;
            uniform vec4 ink_color : source_color = vec4(0.6, 0.56, 0.46, 1.0);
            uniform float fill_amount = 1.0;
            void fragment() {
                vec4 brush = texture(TEXTURE, UV);
                vec2 p = UV - vec2(0.5);
                float angle = mod(atan(p.x, -p.y) + 6.2831853, 6.2831853) / 6.2831853;
                float reveal = smoothstep(angle - 0.0015, angle + 0.0015, fill_amount);
                if (fill_amount <= 0.0) reveal = 0.0;
                if (fill_amount >= 1.0) reveal = 1.0;
                float grain = dot(brush.rgb, vec3(0.299, 0.587, 0.114));
                COLOR = vec4(ink_color.rgb * (0.72 + grain * 0.35), brush.a * reveal * ink_color.a);
            }
            """ };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("ink_color", color);
        material.SetShaderParameter("fill_amount", fill);
        return material;
    }

    private void build()
    {
        _shade = new ColorRect { Color = new Color("211f1a"), MouseFilter = MouseFilterEnum.Ignore };
        _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_shade);
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>(AssetRoot + "ink_background_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = """
                shader_type canvas_item;
                void fragment() {
                    vec2 drift = vec2(sin(UV.y * 9.0 + TIME * 0.11), sin(UV.x * 8.0 - TIME * 0.09)) * 0.0015;
                    COLOR = texture(TEXTURE, clamp(UV + drift, vec2(0.002), vec2(0.998)));
                }
                """ } }
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _shade.AddChild(background);
        _center = new Control { Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore }; AddChild(_center);
        _ringLayer = new Control { Position = new Vector2(476, -5), Size = new Vector2(720, 720), PivotOffset = new Vector2(360, 360), MouseFilter = MouseFilterEnum.Ignore };
        _center.AddChild(_ringLayer);
        var texture = GD.Load<Texture2D>(AssetRoot + "brush_ring_v1.png");
        var ring = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Material = ring_material(new Color("5b574e"), 1), MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(720, 720)
        };
        _ringLayer.AddChild(ring);
        _ringMaterial = ring_material(new Color("a0957b"), 0);
        var progress = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Material = _ringMaterial, MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(720, 720)
        };
        _ringLayer.AddChild(progress);
        _title = text(new Vector2(386, 303), new Vector2(900, 100), 64, new Color("d9ceba"));
        _elapsed = text(new Vector2(236, 667), new Vector2(1200, 55), 31, new Color("c9beaa"));
        _timeline = new ActionTimelineStrip { Position = new Vector2(300, 748), Size = new Vector2(1072, 24), MouseFilter = MouseFilterEnum.Ignore };
        _center.AddChild(_timeline);
        _startDate = text(new Vector2(100, 787), new Vector2(480, 42), 23, new Color("b9af9c"));
        _currentDate = text(new Vector2(586, 787), new Vector2(500, 42), 23, new Color("a69a7e"));
        _endDate = text(new Vector2(1082, 787), new Vector2(500, 42), 23, new Color("b9af9c"));
        _state = text(new Vector2(236, 688), new Vector2(1200, 35), 20, new Color("aaa18e"));
        _detail = text(new Vector2(236, 742), new Vector2(1200, 120), 19, new Color("aaa18e"));
        _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private Label text(Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var label = new Label
        {
            Position = position, Size = size, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        label.AddThemeColorOverride("font_outline_color", Colors.Transparent);
        label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        _center.AddChild(label);
        return label;
    }
}

/// <summary>固定笔触的细时间轴，只有完成部分和时间游标移动。</summary>
public partial class ActionTimelineStrip : Control
{
    private float _ratio;
    public float Ratio { get => _ratio; set { _ratio = Mathf.Clamp(value, 0, 1); QueueRedraw(); } }

    public override void _Draw()
    {
        var width = Size.X;
        var active = new Color("978a70");
        var inactive = new Color("625d51");
        for (var i = 0; i < 268; i++)
        {
            var x = width * i / 268f;
            var next = width * (i + 1) / 268f;
            var y = 12 + Mathf.Sin(i * 2.71f) * .6f;
            var color = x < width * _ratio ? active : inactive;
            color.A = .65f + .23f * Mathf.Sin(i * 1.37f);
            DrawLine(new Vector2(x, y), new Vector2(next, 12 + Mathf.Sin((i + 1) * 2.71f) * .6f), color, 1.5f + .6f * Mathf.Sin(i * .93f), true);
        }
        foreach (var x in new[] { 0f, width, width * _ratio })
        {
            var color = x == width * _ratio ? active : new Color("b8ae98");
            DrawLine(new Vector2(x, 1), new Vector2(x, 23), new Color(color, .55f), 1, true);
            DrawCircle(new Vector2(x, 12), 6, color, true, -1, true);
        }
    }
}
