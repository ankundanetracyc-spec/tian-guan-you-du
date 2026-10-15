using Godot;

/// <summary>Independent preview of the outfit-specific, full-body combat pose library.</summary>
public partial class BattlePoseLibraryShowcase : Node2D
{
    private const string PoseRoot = "res://textures/characters/male_pose_library_20261015_v1/poses/";
    private const float StageFloorY = 900f;
    private const float CharacterScale = .445f;
    private const float PoseBlendDuration = .10f;
    private const float PreviewFps = 30f;

    private static readonly (string Label, string Action, string Key)[] ActionButtons =
    [
        ("接近", "approach", "1"),
        ("格挡应对", "guard", "2"),
        ("快速近战", "quick", "3"),
        ("沉身重击", "heavy", "4"),
        ("身法连段", "flow", "5"),
        ("原地施法", "cast", "6"),
        ("受击打断", "hit", "H"),
        ("倒飞回位", "return", "R"),
        ("站立退场", "death", "D"),
        ("回到待机", "idle", "0")
    ];

    private static readonly System.Collections.Generic.Dictionary<string, string> PoseFiles = new()
    {
        ["idle"] = "idle.png",
        ["approach"] = "approach.png",
        ["guard"] = "guard.png",
        ["quick_melee"] = "quick_melee.png",
        ["heavy_melee"] = "heavy_melee.png",
        ["flow_melee"] = "flow_melee.png",
        ["cast"] = "cast.png",
        ["hurt"] = "hurt.png",
        ["flight_return"] = "flight_return.png",
        ["death_stand"] = "death_stand.png"
    };

    private sealed record MotionKey(float Time, string Pose, Vector2 Offset, float RotationDegrees = 0f, float Alpha = 1f);
    private sealed record MotionClip(string Name, string Description, float Duration, MotionKey[] Keys, bool Death = false);

    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _textureCache = new();
    private Node2D? _characterRoot;
    private Sprite2D? _poseFrom;
    private Sprite2D? _poseTo;
    private Label? _statusLabel;
    private Label? _timeLabel;
    private Button? _pauseButton;
    private HSlider? _timeline;

    private MotionClip? _clip;
    private float _clipTime;
    private float _idleClock;
    private float _currentAlpha = 1f;
    private float _currentRotation;
    private Vector2 _currentOffset;
    private string _currentPose = "idle";
    private string _clipStatus = "男修 V2 · 防守待机";
    private bool _paused;
    private bool _draggingTimeline;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        QueueRedraw();
        BuildCharacter();
        BuildInterface();
        ShowPose("idle", "男修 V2 · 防守待机");
    }

    public override void _Process(double delta)
    {
        if (_paused) return;

        if (_clip is null)
        {
            _idleClock += (float)delta;
            if (_characterRoot is not null)
                _characterRoot.Position = new Vector2(1100f, StageFloorY + Mathf.Sin(_idleClock * 2.2f) * 1.2f);
            return;
        }

        _clipTime = Mathf.Min(_clip.Duration, _clipTime + (float)delta);
        EvaluateClip(_clipTime);
        UpdateTimelineUi();
        if (_clipTime >= _clip.Duration)
            CompleteClip();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
            return;
        }

        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Space)
        {
            TogglePause();
            return;
        }
        if (key.Keycode == Key.Left) { StepFrame(-1); return; }
        if (key.Keycode == Key.Right) { StepFrame(1); return; }

        foreach (var (label, action, hotkey) in ActionButtons)
        {
            if (hotkey.Length == 1 && char.IsDigit(hotkey[0]) && key.Keycode == (Key)((int)Key.Key0 + (hotkey[0] - '0')))
            {
                PlayAction(action);
                return;
            }
            if (hotkey == "H" && key.Keycode == Key.H) { PlayAction(action); return; }
            if (hotkey == "R" && key.Keycode == Key.R) { PlayAction(action); return; }
            if (hotkey == "D" && key.Keycode == Key.D) { PlayAction(action); return; }
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0f, 0f, 1920f, 1080f), new Color("#202a2b"));
        DrawLine(new Vector2(500f, StageFloorY), new Vector2(1740f, StageFloorY), new Color("#738381", .62f), 1.5f, true);
        DrawLine(new Vector2(510f, StageFloorY + 3f), new Vector2(1730f, StageFloorY + 3f), new Color("#151f20", .32f), 1f, true);
    }

    private void BuildCharacter()
    {
        _characterRoot = new Node2D { Name = "MalePoseRoot", Position = new Vector2(1100f, StageFloorY), ZIndex = 1 };
        AddChild(_characterRoot);

        var shadow = new Polygon2D
        {
            Name = "GroundingShadow",
            Color = new Color("#0d1718", .30f),
            Polygon = CreateEllipse(new Vector2(0f, 2f), 150f, 12f),
            ZIndex = -1
        };
        _characterRoot.AddChild(shadow);

        _poseFrom = CreatePoseSprite("PoseFrom", 0);
        _poseTo = CreatePoseSprite("PoseTo", 1);
        _characterRoot.AddChild(_poseFrom);
        _characterRoot.AddChild(_poseTo);
    }

    private static Vector2[] CreateEllipse(Vector2 center, float radiusX, float radiusY)
    {
        var points = new Vector2[40];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Tau * i / points.Length;
            points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }

    private Sprite2D CreatePoseSprite(string nodeName, int zIndex)
    {
        return new Sprite2D
        {
            Name = nodeName,
            Centered = true,
            Position = new Vector2(0f, -342f),
            Scale = Vector2.One * CharacterScale,
            ZIndex = zIndex,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear
        };
    }

    private void BuildInterface()
    {
        var layer = new CanvasLayer { Name = "PoseLibraryUI", Layer = 5 };
        AddChild(layer);

        var font = ResourceLoader.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        var title = new Label
        {
            Text = "局内动作姿势库 · 男修 V2",
            Position = new Vector2(42f, 30f),
            Size = new Vector2(740f, 46f)
        };
        StyleLabel(title, font, 28, new Color("#f1e8cd"));
        layer.AddChild(title);

        var subtitle = new Label
        {
            Text = "一套衣装一张待机姿势；动作按类别制作本衣装对应的全身姿势图",
            Position = new Vector2(44f, 78f),
            Size = new Vector2(970f, 30f)
        };
        StyleLabel(subtitle, font, 17, new Color("#c9d0c4"));
        layer.AddChild(subtitle);

        var panel = new PanelContainer
        {
            Name = "PoseActions",
            Position = new Vector2(40f, 142f),
            Size = new Vector2(440f, 748f),
            CustomMinimumSize = new Vector2(440f, 748f)
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color("#e9dfc7", .96f),
            BorderColor = new Color("#122323", .85f),
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9
        };
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetContentMarginAll(18f);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        layer.AddChild(panel);

        var column = new VBoxContainer { Name = "ActionColumn" };
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        var heading = new Label { Text = "动作状态", HorizontalAlignment = HorizontalAlignment.Center };
        StyleLabel(heading, font, 22, new Color("#1e3937"));
        column.AddChild(heading);

        _statusLabel = new Label
        {
            Text = _clipStatus,
            CustomMinimumSize = new Vector2(0f, 50f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        StyleLabel(_statusLabel, font, 16, new Color("#43554e"));
        column.AddChild(_statusLabel);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 9);
        column.AddChild(grid);
        foreach (var (label, action, hotkey) in ActionButtons)
        {
            var button = CreateButton($"{label}  [{hotkey}]", font, new Vector2(190f, 54f));
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () => PlayAction(action);
            grid.AddChild(button);
        }

        var note = new Label
        {
            Text = "近战：接近 → 接触 → 命中停顿 → 倒飞回位\n受击可打断；退场为站立摇晃后淡出。",
            CustomMinimumSize = new Vector2(0f, 82f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        StyleLabel(note, font, 15, new Color("#59645b"));
        column.AddChild(note);

        BuildPlaybackControls(layer, font);

        var stageLabel = new Label
        {
            Text = "男修 V2 · 无剑姿势样片",
            Position = new Vector2(760f, 155f),
            Size = new Vector2(700f, 38f),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        StyleLabel(stageLabel, font, 20, new Color("#e9dfc7"));
        layer.AddChild(stageLabel);

        var back = CreateButton("返回首页  [Esc]", font, new Vector2(185f, 48f));
        back.Position = new Vector2(1685f, 40f);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        layer.AddChild(back);
    }

    private void BuildPlaybackControls(CanvasLayer layer, FontFile? font)
    {
        var panel = new PanelContainer
        {
            Name = "TimelinePanel",
            Position = new Vector2(505f, 938f),
            Size = new Vector2(1365f, 112f),
            CustomMinimumSize = new Vector2(1365f, 112f)
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#e9dfc7", .94f),
            BorderColor = new Color("#122323", .78f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(12f);
        panel.AddThemeStyleboxOverride("panel", style);
        layer.AddChild(panel);

        var row = new HBoxContainer { Name = "PlaybackRow" };
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        _pauseButton = CreateButton("暂停  [Space]", font, new Vector2(165f, 54f));
        _pauseButton.Pressed += TogglePause;
        row.AddChild(_pauseButton);

        var stepBack = CreateButton("上一帧  [←]", font, new Vector2(132f, 54f));
        stepBack.Pressed += () => StepFrame(-1);
        row.AddChild(stepBack);

        var stepForward = CreateButton("下一帧  [→]", font, new Vector2(132f, 54f));
        stepForward.Pressed += () => StepFrame(1);
        row.AddChild(stepForward);

        _timeline = new HSlider
        {
            Name = "Timeline",
            MinValue = 0,
            MaxValue = 1,
            Step = 1.0 / PreviewFps,
            Value = 0,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(360f, 48f)
        };
        _timeline.ValueChanged += OnTimelineChanged;
        row.AddChild(_timeline);

        _timeLabel = new Label
        {
            Text = "待机",
            CustomMinimumSize = new Vector2(130f, 54f),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        StyleLabel(_timeLabel, font, 15, new Color("#43554e"));
        row.AddChild(_timeLabel);
    }

    private static Button CreateButton(string text, FontFile? font, Vector2 minimumSize)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = minimumSize,
            FocusMode = Control.FocusModeEnum.None
        };
        button.AddThemeColorOverride("font_color", new Color("#27413d"));
        button.AddThemeColorOverride("font_hover_color", new Color("#172d2b"));
        button.AddThemeColorOverride("font_pressed_color", new Color("#172d2b"));
        button.AddThemeFontSizeOverride("font_size", 16);
        if (font is not null) button.AddThemeFontOverride("font", font);
        var normal = new StyleBoxFlat { BgColor = new Color("#d8cbb0"), BorderColor = new Color("#65746b") };
        normal.SetBorderWidthAll(1);
        normal.SetCornerRadiusAll(5);
        button.AddThemeStyleboxOverride("normal", normal);
        var hover = new StyleBoxFlat { BgColor = new Color("#e7ddc8"), BorderColor = new Color("#49665d") };
        hover.SetBorderWidthAll(1);
        hover.SetCornerRadiusAll(5);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        return button;
    }

    private static void StyleLabel(Label label, FontFile? font, int size, Color color)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (font is not null) label.AddThemeFontOverride("font", font);
    }

    private void PlayAction(string action)
    {
        if (action == "idle")
        {
            StopToIdle();
            return;
        }

        var startPose = _currentPose;
        var startOffset = _currentOffset;
        var startRotation = _currentRotation;
        MotionKey start = new(0f, startPose, startOffset, startRotation, 1f);
        MotionClip clip = action switch
        {
            "approach" => new("接近", "短步贴近，随后回到原位。", .68f,
            [start, new(.04f, "approach", startOffset), new(.22f, "approach", startOffset + new Vector2(155f, 0f)), new(.34f, "approach", startOffset + new Vector2(155f, 0f)), new(.52f, "idle", Vector2.Zero), new(.68f, "idle", Vector2.Zero)]),
            "guard" => new("格挡应对", "收肘护住躯干，保持落脚稳定。", .62f,
            [start, new(.04f, "guard", startOffset), new(.36f, "guard", startOffset), new(.50f, "idle", Vector2.Zero), new(.62f, "idle", Vector2.Zero)]),
            "quick" => BuildCloseAttack("快速近战", "quick_melee", start),
            "heavy" => BuildCloseAttack("沉身重击", "heavy_melee", start),
            "flow" => BuildFlowAttack(start),
            "cast" => new("原地施法", "双脚留在原位，以躯干和双手形成施法势。", .64f,
            [start, new(.04f, "cast", startOffset), new(.38f, "cast", startOffset), new(.48f, "idle", Vector2.Zero), new(.64f, "idle", Vector2.Zero)]),
            "hit" => new("受击打断", "受击姿势打断当前动作，随后恢复待机。", .52f,
            [start, new(.02f, "hurt", startOffset + new Vector2(-22f, -8f), -7f), new(.14f, "hurt", startOffset + new Vector2(-15f, -4f), -4f), new(.30f, "idle", Vector2.Zero), new(.52f, "idle", Vector2.Zero)]),
            "return" => new("倒飞回位", "身体后仰，沿短弧线倒飞回起始位置。", .56f,
            [new(0f, "flight_return", new Vector2(185f, -30f), -12f), new(.08f, "flight_return", new Vector2(170f, -34f), -12f), new(.30f, "flight_return", Vector2.Zero, 0f), new(.34f, "idle", Vector2.Zero), new(.56f, "idle", Vector2.Zero)]),
            "death" => new("站立退场", "身体轻晃、重心失守，保持站立后淡出。", .86f,
            [start, new(.03f, "death_stand", startOffset), new(.18f, "death_stand", startOffset, -4f), new(.32f, "death_stand", startOffset, 4f), new(.46f, "death_stand", startOffset, -2f), new(.72f, "death_stand", startOffset, 0f, 0f), new(.86f, "death_stand", startOffset, 0f, 0f)], true),
            _ => new("待机", "回到防守待机姿势。", .16f, [start, new(.16f, "idle", Vector2.Zero)])
        };

        StartClip(clip);
    }

    private static MotionClip BuildCloseAttack(string name, string attackPose, MotionKey start)
    {
        return new MotionClip(name, "接近 0.18 秒、接触 0.18 秒，命中后沿短弧线倒飞回位。", .78f,
        [start,
         new(.04f, "approach", start.Offset, start.RotationDegrees),
         new(.22f, "approach", start.Offset + new Vector2(185f, 0f), start.RotationDegrees),
         new(.23f, attackPose, start.Offset + new Vector2(185f, 0f), start.RotationDegrees),
         new(.39f, attackPose, start.Offset + new Vector2(185f, 0f), start.RotationDegrees),
         new(.40f, "flight_return", start.Offset + new Vector2(165f, -34f), -10f),
         new(.62f, "flight_return", Vector2.Zero, 0f),
         new(.64f, "idle", Vector2.Zero),
         new(.78f, "idle", Vector2.Zero)]);
    }

    private static MotionClip BuildFlowAttack(MotionKey start)
    {
        return new MotionClip("身法连段", "先转身接近，再以横向腰胯带动的姿势掠过，倒飞回位。", .88f,
        [start,
         new(.04f, "approach", start.Offset, start.RotationDegrees),
         new(.22f, "approach", start.Offset + new Vector2(185f, 0f), start.RotationDegrees),
         new(.23f, "flow_melee", start.Offset + new Vector2(185f, 0f), 5f),
         new(.39f, "flow_melee", start.Offset + new Vector2(185f, 0f), 5f),
         new(.40f, "quick_melee", start.Offset + new Vector2(174f, -18f), -3f),
         new(.48f, "quick_melee", start.Offset + new Vector2(160f, -23f), -5f),
         new(.49f, "flight_return", start.Offset + new Vector2(150f, -35f), -10f),
         new(.71f, "flight_return", Vector2.Zero, 0f),
         new(.73f, "idle", Vector2.Zero),
         new(.88f, "idle", Vector2.Zero)]);
    }

    private void StartClip(MotionClip clip)
    {
        _clip = clip;
        _clipTime = 0f;
        _paused = false;
        _clipStatus = $"{clip.Name} · {clip.Description}";
        UpdateStatus();
        if (_pauseButton is not null) _pauseButton.Text = "暂停  [Space]";
        EvaluateClip(0f);
        UpdateTimelineUi();
    }

    private void EvaluateClip(float time)
    {
        if (_clip is null || _characterRoot is null) return;
        var keys = _clip.Keys;
        var index = 0;
        while (index + 1 < keys.Length && keys[index + 1].Time <= time) index++;
        var current = keys[index];
        var next = index + 1 < keys.Length ? keys[index + 1] : current;
        var span = Mathf.Max(.0001f, next.Time - current.Time);
        var progress = index + 1 < keys.Length ? Mathf.Clamp((time - current.Time) / span, 0f, 1f) : 1f;
        var offset = current.Offset.Lerp(next.Offset, progress);
        var rotation = Mathf.Lerp(current.RotationDegrees, next.RotationDegrees, progress);
        var alpha = Mathf.Lerp(current.Alpha, next.Alpha, progress);

        var fromPose = current.Pose;
        var toPose = current.Pose;
        var blend = 1f;
        if (index > 0 && current.Pose != keys[index - 1].Pose)
        {
            fromPose = keys[index - 1].Pose;
            toPose = current.Pose;
            blend = Mathf.Clamp((time - current.Time) / PoseBlendDuration, 0f, 1f);
        }

        _currentPose = toPose;
        _currentOffset = offset;
        _currentRotation = rotation;
        _currentAlpha = alpha;
        _characterRoot.Position = new Vector2(1100f, StageFloorY) + offset;
        _characterRoot.RotationDegrees = rotation;
        ApplyPoseBlend(fromPose, toPose, blend, alpha);
    }

    private void ApplyPoseBlend(string fromPose, string toPose, float blend, float alpha)
    {
        if (_poseFrom is null || _poseTo is null) return;
        _poseFrom.Texture = LoadPose(fromPose);
        _poseTo.Texture = LoadPose(toPose);
        if (fromPose == toPose)
        {
            _poseFrom.Modulate = new Color(1f, 1f, 1f, alpha);
            _poseTo.Modulate = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            _poseFrom.Modulate = new Color(1f, 1f, 1f, alpha * (1f - blend));
            _poseTo.Modulate = new Color(1f, 1f, 1f, alpha * blend);
        }
    }

    private Texture2D? LoadPose(string pose)
    {
        if (_textureCache.TryGetValue(pose, out var cached)) return cached;
        if (!PoseFiles.TryGetValue(pose, out var file)) return null;
        var path = PoseRoot + file;
        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture is null)
        {
            GD.PushError($"姿势库图片缺失或无法加载：{path}");
            return null;
        }
        _textureCache[pose] = texture;
        return texture;
    }

    private void ShowPose(string pose, string status)
    {
        _clip = null;
        _clipTime = 0f;
        _paused = false;
        _currentPose = pose;
        _currentOffset = Vector2.Zero;
        _currentRotation = 0f;
        _currentAlpha = 1f;
        if (_characterRoot is not null)
        {
            _characterRoot.Position = new Vector2(1100f, StageFloorY);
            _characterRoot.RotationDegrees = 0f;
        }
        ApplyPoseBlend(pose, pose, 1f, 1f);
        _clipStatus = status;
        UpdateStatus();
        if (_pauseButton is not null) _pauseButton.Text = "暂停  [Space]";
        UpdateTimelineUi();
    }

    private void StopToIdle()
    {
        ShowPose("idle", "男修 V2 · 防守待机");
    }

    private void CompleteClip()
    {
        var completed = _clip;
        if (completed is null) return;
        if (completed.Death)
        {
            _clip = null;
            _currentPose = "death_stand";
            _currentAlpha = 0f;
            _currentOffset = Vector2.Zero;
            _currentRotation = 0f;
            ApplyPoseBlend("death_stand", "death_stand", 1f, 0f);
            _clipStatus = "站立退场 · 已淡出；点击待机可重置。";
            UpdateStatus();
            UpdateTimelineUi();
            return;
        }
        ShowPose("idle", $"{completed.Name} · 已回到防守待机");
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_pauseButton is not null) _pauseButton.Text = _paused ? "继续  [Space]" : "暂停  [Space]";
        if (_clip is null)
            _clipStatus = _paused ? "防守待机 · 已暂停" : "男修 V2 · 防守待机";
        else
            _clipStatus = $"{_clip.Name} · {(_paused ? "已暂停" : _clip.Description)}";
        UpdateStatus();
    }

    private void StepFrame(int direction)
    {
        if (_clip is null)
        {
            _paused = true;
            _idleClock = 0f;
            if (_characterRoot is not null) _characterRoot.Position = new Vector2(1100f, StageFloorY);
            if (_pauseButton is not null) _pauseButton.Text = "继续  [Space]";
            _clipStatus = "防守待机 · 已暂停";
            UpdateStatus();
            return;
        }
        _paused = true;
        if (_pauseButton is not null) _pauseButton.Text = "继续  [Space]";
        _clipTime = Mathf.Clamp(_clipTime + direction / PreviewFps, 0f, _clip.Duration);
        EvaluateClip(_clipTime);
        UpdateTimelineUi();
        _clipStatus = $"{_clip.Name} · 逐帧查看（{PreviewFps:0} fps）";
        UpdateStatus();
    }

    private void OnTimelineChanged(double value)
    {
        if (_draggingTimeline || _clip is null) return;
        _paused = true;
        if (_pauseButton is not null) _pauseButton.Text = "继续  [Space]";
        _clipTime = (float)value * _clip.Duration;
        EvaluateClip(_clipTime);
        _clipStatus = $"{_clip.Name} · 时间轴定位（{_clipTime:0.00}s）";
        UpdateStatus();
        UpdateTimelineUi();
    }

    private void UpdateTimelineUi()
    {
        if (_timeline is null || _timeLabel is null) return;
        _draggingTimeline = true;
        if (_clip is null)
        {
            _timeline.SetValueNoSignal(0d);
            _timeline.Editable = false;
            _timeLabel.Text = _paused ? "待机 · 暂停" : "待机";
        }
        else
        {
            _timeline.Editable = true;
            _timeline.SetValueNoSignal(_clip.Duration <= 0f ? 0d : _clipTime / _clip.Duration);
            _timeLabel.Text = $"{_clipTime:0.00} / {_clip.Duration:0.00}s";
        }
        _draggingTimeline = false;
    }

    private void UpdateStatus()
    {
        if (_statusLabel is not null) _statusLabel.Text = _clipStatus;
    }
}
